namespace PLC.CodeGen.Common

open Dual.Common.Base.FS
open Dual.Common.Core.FS
open System.Runtime.CompilerServices
open System.Globalization
open Dual.PLC.TagParser.FS
open System


[<AutoOpen>]
module LSEAddressPattern =


    let subBitPattern (size: int) (str: string) =
        match System.Int32.TryParse(str) with
        | true, v when v < size -> Some(v)
        | _ -> None

    /// for speed up, use memoized cache
    let private deviceTypeFromStringMemoized = DU.fromStringMemoized<DeviceType>

    let (|ByteSubBitPattern|_|) = subBitPattern 8
    let (|WordSubBitPattern|_|) = subBitPattern 16
    let (|DWordSubBitPattern|_|) = subBitPattern 32
    let (|LWordSubBitPattern|_|) = subBitPattern 64
    let (|DevicePattern|_|) (str: string) = deviceTypeFromStringMemoized str
    let (|DataTypePattern|_|) (str:string)=
        try
            Some <|  str.FromDeviceMnemonic()
        with exn ->
            None

    //let (|HexPattern|_|) (str: string) =
    //        match System.Int32.TryParse(str, NumberStyles.HexNumber, CultureInfo.CurrentCulture) with
    //        | true, v -> Some(v)
    //        | _ -> None


    let Xgk5Digit = [ "L"; "N"; "D"; "R" ]
    let Xgk4Digit = [ "P"; "M"; "K"; "F"; "T"; "C" ]

    let getXgkBitText (device:string, offset: int) : string =
        let word = offset / 16
        let bit = offset % 16
        match device.ToUpper() with
        | d when Xgk5Digit |> List.contains d ->
            device + sprintf "%05i.%X" word bit
        | d when Xgk4Digit |> List.contains d ->
            device + sprintf "%04i%X" word bit
        | _ -> failwithf $"XGK device({device})는 지원하지 않습니다."

    let getXgkWordText (device:string, offsetByte: int) : string =
        
        let wordIndex = offsetByte / 2
        match device.ToUpper() with
        | d when Xgk5Digit |> List.contains d ->
            sprintf "%s%05i" d wordIndex
        | d when Xgk4Digit |> List.contains d ->
            sprintf "%s%04i" d wordIndex
        | _ -> failwithf $"XGK device({device})는 지원하지 않습니다."

    let getXgkTextByType (device:string, offset: int, isBool:bool) : string =
        if isBool
        then getXgkBitText(device, offset)
        else
            getXgkWordText(device, offset * 8)


    let getXgKTextByTag (device:LsTagInfo) : string =
        let isBit = device.DataType = DataType.Bit

        let offset = if isBit
                     then  device.BitOffset
                     else  device.ByteOffset

        getXgkTextByType (device.Device.ToString(), offset, isBit)

    let getXgiIOTextBySize (device:string, offset: int, bitSize:int, iSlot:int, sumBit:int) : string =
        if bitSize = 1
        then $"%%{device}X0.{iSlot}.{(offset-sumBit) % 64}"  //test ahn 아날로그 base 1로 일단 고정
        else
            match bitSize with  //test ahn  xgi 규격확인
            | 8 -> $"%%{device}B{offset+1024}"
            | 16 -> $"%%{device}W{offset+1024}"
            | 32 -> $"%%{device}D{offset+1024}"
            | 64 -> $"%%{device}L{offset+1024}"
            | _ -> failwithf $"Invalid size :{bitSize}"


    let getXgiMemoryTextBySize (device:string, offset: int, bitSize:int) : string =
        if bitSize = 1
        then $"%%{device}X{offset}"
        else
            if offset%8 = 0 
            then getXgiIOTextBySize (device, offset, bitSize, 0, 0)
            else failwithf $"Word Address는 8의 배수여야 합니다. {offset}"


    let private deviceTypeDic =
        [|
            for t in DU.getEnumValues(typedefof<DeviceType>) do
                t.ToString(), t :?> DeviceType
        |] |> Tuple.toReadOnlyDictionary

    let private dataTypeDic =
        [|
            1, DataType.Bit
            8, DataType.Byte
            16, DataType.Word
            32, DataType.DWord
            64, DataType.LWord
        |] |> Tuple.toReadOnlyDictionary


    let createTagInfo = LsTagInfo.Create >> Some
    let (|LsTagXGIPattern|_|) ((modelId: int option), (tag: string)) =
        match tryParseXgiTag tag with
        | Some (device, dataSize, bitOffset) ->
            let deviceType, dataType = deviceTypeDic[device], dataTypeDic[dataSize]
            createTagInfo(tag, deviceType, dataType, bitOffset, modelId)
        | None ->
            None


    let (|LsTagXGKPattern|_|) ((modelId: int option), (tag: string)) =
        match tryParseXgkTag tag with
        | Some (device, dataSize, bitOffset) ->
            let deviceType, dataType = deviceTypeDic[device], dataTypeDic[dataSize]
            createTagInfo(tag, deviceType, dataType, bitOffset, modelId)
        | None ->
            None

    let tryParseXGITag tag = (|LsTagXGIPattern|_|) (None, tag)
    let tryParseXGKTag tag = (|LsTagXGKPattern|_|) (None, tag)
    let tryParseXGKTagByBitType tag = (|LsTagXGKPattern|_|) (None, tag)

    let tryParseXGITagByCpu (tag: string) (modelId: int) = (|LsTagXGIPattern|_|) (modelId |> Some, tag)
    let tryParseXGKTagByCpu (tag: string) (modelId: int) = (|LsTagXGKPattern|_|) (modelId |> Some, tag)

    let isXgiTag tag = tryParseXGITag tag |> Option.isSome
    let isXgkTag tag = tryParseXGKTag tag |> Option.isSome



type LSEAddressPatternExt =
    [<Extension>] static member IsXGIAddress (x:string) = isXgiTag x
///XGK 검증필요
    [<Extension>] static member IsXGKAddress (x:string) = isXgkTag x
