namespace PLC.CodeGen.Common

open System.Collections.Generic
open Engine.Core
open Dual.Common.Core.FS
open System.Text.RegularExpressions

[<AutoOpen>]
module MemoryAllocator =
    /// Unit -> address string 을 반환하는 함수 type
    type PLCMemoryAllocatorType = unit -> string

    type PLCMemoryAllocator =
        {
            BitAllocator: PLCMemoryAllocatorType
            ByteAllocator: PLCMemoryAllocatorType
            WordAllocator: PLCMemoryAllocatorType
            DWordAllocator: PLCMemoryAllocatorType
            LWordAllocator: PLCMemoryAllocatorType
        }
        static member Create(bitAllocator, byteAllocator, wordAllocator, dwordAllocator, lwordAllocator) =
            {
                BitAllocator = bitAllocator
                ByteAllocator = byteAllocator
                WordAllocator = wordAllocator
                DWordAllocator = dwordAllocator
                LWordAllocator = lwordAllocator
            }

    type IntRange = int * int

    type PLCMemoryAllocatorSpec =
        | RangeSpec of IntRange
        | AllocatorFunctions of PLCMemoryAllocator

    /// C# 에서 Func<Unit, xxx> 형태의 함수를 직접 호출 불가능해서, 대신 호출하는 함수
    let CsInvoke(allocator: PLCMemoryAllocatorType) = allocator()

    let getByteSizeFromPrefix prefix target =
        match prefix with
        | "B" ->
            match target with
            | XGI -> 1
            | XGK -> 2
            | _ -> failwithlog $"not support target {target} err"
        | "W" -> 2
        | "D" -> 4
        | "L" -> 8
        | _ -> failwithlog "ERROR"


    /// 주어진 memory type 에서 주소를 할당하 하는 함수 제공
    /// typ: {"M", "I", "Q"} 등이 가능하나 주로 "M"
    /// availableByteRange: 할당 가능한 [시작, 끝] byte 의 range (reservedBytes 에 포함된 부분은 제외됨)
    /// reservedBytes: 회피 영역
    let createMemoryAllocator
        (typ: string)
        (availableByteRange: IntRange)
        (FList(reservedBytes: int list))
        (target: PlatformTarget)
      : PLCMemoryAllocator =
        tracefn $"xxx-------------------------MemoryAllocator created!"
        let startByte, endByte = availableByteRange
        /// optional fragmented bit position
        let mutable ofBit: int option = None // Some (startByte * 8)
        /// optional framented byte [start, end] position
        let mutable ofByteRange: IntRange option = None
        let mutable byteCursor = startByte

        ///XGK 16bit 최소, XGI 8bit 최소
        let unitSize = if target = XGK then 16 else 8
        let getMemType(memType) =
                if target = XGK && memType = "B" then
                    "W" //XGK는 byte 단위가 없어서 Word로 치환
                else
                    memType

        let rec getAddress (reqMemType: string) : string =
            let reqMemType = getMemType reqMemType


            match reqMemType with
            | "X" ->
                let bitIndex =
                    match ofBit, ofByteRange with
                    | Some bit, _ when bit % unitSize = unitSize-1 -> // 마지막 fragment bit 을 쓰는 상황
                        ofBit <- None
                        bit
                    | Some bit, _ -> // 마지막이 아닌 여유 fragment bit 을 쓰는 상황
                        ofBit <- Some(bit + 1)
                        bit
                    | None, Some(s, e) ->
                        let bit = s * 8
                        ofBit <- Some(bit + 1)
                        ofByteRange <- if s + (unitSize/8) = e then None else Some(s + (unitSize/8), e)
                        bit
                    | None, None ->
                        let bit = byteCursor * 8
                        ofBit <- Some(bit + 1)
                        byteCursor <- byteCursor+unitSize/8
                        bit

                let byteIndex = bitIndex / 8

                if byteIndex > endByte then
                    failwithlog "ERROR: Limit exceeded."

                if reservedBytes |> List.contains byteIndex then
                    getAddress reqMemType
                else
                    let address =
                        if target = XGI then
                            $"%%{typ}{reqMemType}{bitIndex}"
                        elif target = XGK then
                            getXgkBitText(typ, bitIndex)
                        else
                            failwithlog "ERROR"
                    //debugfn "Address %s allocated" address
                    address


            | ("B" | "W" | "D" | "L") ->
                let byteSize = getByteSizeFromPrefix reqMemType target

                let byteIndex =
                    match ofByteRange with
                    | Some(fs, fe) when (fe - fs) > byteSize -> // fragmented bytes 로 해결하고도 남는 상황
                        ofByteRange <- Some(fs + byteSize, fe)
                        fs
                    | Some(fs, fe) when (fe - fs) = byteSize -> // fragmented bytes 를 전부 써서 해결 가능한 상황
                        ofByteRange <- None
                        fs
                    | _ -> // fragmented bytes 로 부족한 상황.  fragment 는 건드리지 않고 새로운 영역에서 할당
                        let byte =
                            if byteCursor % byteSize = 0 then
                                let byte = byteCursor
                                byteCursor <- byteCursor + byteSize
                                byte
                            else
                                let newPosition = (byteCursor + byteSize) / byteSize * byteSize
                                ofByteRange <- Some(byteCursor, newPosition)
                                byteCursor <- newPosition + byteSize
                                newPosition
                        byte

                if byteIndex + byteSize > endByte then
                    failwithlog "ERROR: Limit exceeded."

                let requiredByteIndices = [ byteIndex .. (byteIndex + byteSize - 1) ]
                let x = Seq.intersect requiredByteIndices reservedBytes |> Seq.any

                if x then
                    getAddress reqMemType
                else
                    let address =

                        if target = XGI then
                            $"%%{typ}{reqMemType}{byteIndex / byteSize}"
                        elif target = XGK then
                            getXgkWordText (typ, byteIndex)
                        else
                            failwithlog "ERROR"

                    //debugfn "Address %s allocated" address
                    address
            | _ -> failwithlog "ERROR"


        {
            BitAllocator   = fun () -> getAddress "X"
            ByteAllocator  = fun () -> getAddress "B"
            WordAllocator  = fun () -> getAddress "W"
            DWordAllocator = fun () -> getAddress "D"
            LWordAllocator = fun () -> getAddress "L"
        }


    type System.Type with

        member x.GetBitSize() =
            match x.Name with
            | BOOL    -> 1
            | CHAR    -> 8
            | FLOAT32 -> 32
            | FLOAT64 -> 64
            | INT16   -> 16
            | INT32   -> 32
            | INT64   -> 64
            | INT8    -> 8
            | STRING  -> (32*8)
            | UINT16  -> 16
            | UINT32  -> 32
            | UINT64  -> 64
            | UINT8   -> 8
            | _ -> failwithlog "ERROR"

        member x.GetByteSize() =
            match x.Name with
            | BOOL -> failwithlog "ERROR"
            | _ -> max 1 (x.GetBitSize() / 8)

        member x.GetMemorySizePrefix() =
            if x = typedefof<bool> then
                "X"
            elif x = typedefof<string> then
                "L"
            else
                match x.GetByteSize() with
                | 1 -> "B"
                | 2 -> "W"
                | 4 -> "D"
                | 8 -> "L"
                | _ -> failwithlog "GetMemorySizePrefix ERROR"


[<AutoOpen>]
module IECAddressModule =
    /// IEC address 를 표준화한다.  e.g "%i3" => "%IX3" ; "m34" => "%MX34"
    let standardizeAddress (address: string) =
        match address with
        | "_" -> "_"
        | _ ->
            let addr =
                if address.StartsWith("%") then
                    address.ToUpper()
                else
                    "%"+address.ToUpper()

            let dev, remaining = addr[1], addr.Substring(2)
            match dev, remaining with
            | ('I'|'Q'|'U'|'M'|'L'|'K'|'F'|'N'|'R'|'A'|'W'), RegexMatches @"^(\d+(\.\d+)?(\.\d+)?)$" -> $"%%{dev}X{remaining}"
            | _ -> addr
