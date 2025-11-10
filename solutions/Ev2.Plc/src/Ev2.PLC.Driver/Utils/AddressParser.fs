namespace Ev2.PLC.Driver.Utils

open System
open System.Text.RegularExpressions
open Ev2.PLC.Common.Types

/// 주소 파싱 유틸리티 모듈
module AddressParser =
    
    /// 기본 주소 패턴 (예: D100, M50, P0.1)
    let private basicAddressPattern = @"^([A-Za-z]+)(\d+)(?:\.(\d+))?(?:\[(\d+)\])?$"
    let private basicRegex = Regex(basicAddressPattern, RegexOptions.Compiled)
    
    /// 주소 파싱
    let parseBasicAddress (address: string) : PlcAddress option =
        let match' = basicRegex.Match(address)
        if match'.Success then
            let deviceType = match'.Groups.[1].Value
            let index = Int32.Parse(match'.Groups.[2].Value)
            let bitIndex = 
                if match'.Groups.[3].Success then 
                    Some (Int32.Parse(match'.Groups.[3].Value))
                else None
            let arrayLength = 
                if match'.Groups.[4].Success then 
                    Some (Int32.Parse(match'.Groups.[4].Value))
                else None
            
            Some {
                Raw = address
                DeviceType = deviceType
                Index = index
                BitIndex = bitIndex
                ArrayLength = arrayLength
                DataSize = 1
            }
        else
            None
    
    /// 주소 유효성 검증
    let validateAddress (address: string) : bool =
        parseBasicAddress address |> Option.isSome
    
    /// 주소 정규화 (대소문자 통일 등)
    let normalizeAddress (address: string) : string =
        address.Trim().ToUpper()
    
    /// 연속 주소 범위 생성
    let generateAddressRange (startAddress: string) (count: int) : string list =
        match parseBasicAddress startAddress with
        | Some addr when count > 0 ->
            [0 .. count - 1]
            |> List.map (fun i -> 
                let newIndex = addr.Index + i
                match addr.BitIndex with
                | Some bit -> $"{addr.DeviceType}{newIndex}.{bit}"
                | None -> $"{addr.DeviceType}{newIndex}")
        | _ -> []
    
    /// 주소 정렬 (효율적인 배치 처리를 위해)
    let sortAddresses (addresses: string list) : string list =
        addresses
        |> List.choose parseBasicAddress
        |> List.sortBy (fun addr -> (addr.DeviceType, addr.Index, addr.BitIndex))
        |> List.map (_.FullAddress)
    
    /// 연속된 주소 그룹 감지
    let detectContinuousGroups (addresses: string list) : (string * int) list =
        let parsedAddresses = 
            addresses 
            |> List.choose parseBasicAddress
            |> List.sortBy (fun addr -> (addr.DeviceType, addr.Index))
        
        let rec groupContinuous acc currentGroup remaining =
            match remaining with
            | [] -> 
                match currentGroup with
                | [] -> acc
                | group -> (List.rev group) :: acc
            | head :: tail ->
                match currentGroup with
                | [] -> groupContinuous acc [head] tail
                | prev :: _ when head.DeviceType = prev.DeviceType && head.Index = prev.Index + 1 ->
                    groupContinuous acc (head :: currentGroup) tail
                | _ -> 
                    let newAcc = (List.rev currentGroup) :: acc
                    groupContinuous newAcc [head] tail
        
        groupContinuous [] [] parsedAddresses
        |> List.filter (fun group -> group.Length > 1)
        |> List.map (fun group -> 
            let firstAddr = group |> List.head
            (firstAddr.FullAddress, group.Length))

/// 제조사별 특화 주소 파서 기본 클래스
[<AbstractClass>]
type AddressParserBase(vendor: PlcVendor) =
    
    /// 지원되는 디바이스 타입들
    abstract member SupportedDeviceTypes: string list
    
    /// 주소 파싱 (제조사별 구현)
    abstract member ParseAddress: address: string -> PlcAddress option
    
    /// 디바이스 타입별 최대 인덱스
    abstract member GetMaxIndex: deviceType: string -> int option
    
    /// 비트 주소 지원 여부
    abstract member SupportsBitAddressing: deviceType: string -> bool
    
    /// 배열 주소 지원 여부
    abstract member SupportsArrayAddressing: deviceType: string -> bool
    
    /// 기본 주소 유효성 검증
    member this.ValidateBasicFormat(address: string) =
        AddressParser.validateAddress address
    
    /// 주소 정규화
    member this.Normalize(address: string) =
        let normalized = AddressParser.normalizeAddress address
        match this.ParseAddress(normalized) with
        | Some _ -> Some normalized
        | None -> None
    
    /// 제조사 정보
    member this.Vendor = vendor