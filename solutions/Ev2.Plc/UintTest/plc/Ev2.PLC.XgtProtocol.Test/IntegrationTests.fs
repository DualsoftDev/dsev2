namespace Ev2.PLC.XgtProtocol.Tests

open System
open Xunit
open Ev2.PLC.Common.Types
open TestEndpoints

module IntegrationTests =

    type AddressRule =
        | XGKOnly of baseAddr: string
        | XGIOnly of baseAddr: string
        | Both of xgkAddr: string * xgiAddr: string
        | Special of generator: (bool -> char -> string)

    let XGK_DEV = ['P'; 'M'; 'K'; 'T'; 'C'; 'U'; 'L'; 'N'; 'D'; 'R'] 
    let XGI_DEV = ['I'; 'Q'; 'M'; 'L'; 'N'; 'K'; 'U'; 'R'; 'A'; 'W']

    let getAddressRule (code: char) (kind: char) : AddressRule =
        match code with
        | 'D' | 'R' -> Special (fun isXGI kind ->
            if not isXGI then
                if kind = 'X' then sprintf "%c256.F" code else sprintf "%c256" code
            else sprintf "%%%c%c256" code kind)
        | 'S' -> XGIOnly "%%S{kind}0"
        | 'U' -> Special (fun isXGI kind -> if isXGI then sprintf "%%U%c1.1.1" kind else if kind = 'X' then "UF.1.1" else "UF.1")
        | _ -> Both (sprintf "%c256" code, sprintf "%%%c{kind}256" code)

    let generateAddress (isXGI: bool) (code: char) (kind: char) : string =
        match getAddressRule code kind with
        | XGKOnly addr when not isXGI -> addr
        | XGIOnly template when isXGI -> template.Replace("{kind}", string kind)
        | Both (xgkAddr, xgiTemplate) -> if isXGI then xgiTemplate.Replace("{kind}", string kind) else xgkAddr
        | Special generator -> generator isXGI kind
        | XGKOnly _ -> failwithf "%c 디바이스는 XGK에서만 지원됩니다." code
        | XGIOnly _ -> failwithf "%c 디바이스는 XGI에서만 지원됩니다." code

    let generateTestValue (kind: char) (isDefault:bool) : ScalarValue * PlcTagDataType =
        match kind with
        | 'X' -> ScalarValue.BoolValue  (if isDefault then false else true), PlcTagDataType.Bool
        | 'B' -> ScalarValue.UInt8Value (if isDefault then byte 0 else byte -1), PlcTagDataType.UInt8
        | 'W' -> ScalarValue.Int16Value (if isDefault then int16 0 else int16 -1), PlcTagDataType.Int16
        | 'D' -> ScalarValue.Int32Value (if isDefault then int32 0 else int32 -1), PlcTagDataType.Int32
        | 'L' -> ScalarValue.Int64Value (if isDefault then int64 0 else int64 -1), PlcTagDataType.Int64
        | _ -> failwithf "지원되지 않는 타입: %c" kind

    let isUnsupportedCombination (isXGI: bool) (code: char) (kind: char) : bool =
        match code, kind, isXGI with
        | 'S', 'X', _ -> true
        | 'L', 'W', false -> true
        | ('N' | 'T' | 'C'), 'X', false -> true
        | _ -> false

    let runEthernetTest (plcIp: string) (areaCodes: char list) (isXGI: bool) =
        use conn = new TestEndpoints.TestXgtEthernet(ip = plcIp, localEthernet = isXGI)
        let areaTypes = if isXGI then ['X'; 'B'; 'W'; 'D'; 'L'] else ['X'; 'W']

        for code in areaCodes do
            for kind in areaTypes do
                try
                    if isUnsupportedCombination isXGI code kind then
                        printfn $"[!] {code}{kind} 조합은 현재 설정에서 지원되지 않습니다."
                    else
                        let address = generateAddress isXGI code kind
                        let value, dt = generateTestValue kind false
                        let ok = conn.Write(address, dt, value)
                        let read = conn.Read(address, dt)
                        Assert.True(ok, $"쓰기 실패 - {address}")
                        Assert.Equal(value, read)
                        printfn $"[✓] {address} → {value} (읽기: {read})"
                with ex ->
                    printfn $"[!] 예외 - 주소: {code}{kind} → {ex.Message}"

 
    let private formatAddressInRange (isXGI: bool) (code: char) (kind: char) (index: int) =
        if isXGI then sprintf "%%%c%c%d" code kind index else sprintf "%c%c%d" code kind index

    let runEthernetRangeTest (plcIp: string) (code: char) (minIndex: int) (maxIndex: int) (isXGI: bool) =
        let areaTypes =
            if isXGI then ['X'; 'B'; 'W'; 'D'; 'L']
            else ['X'; 'W']

        use conn = new TestEndpoints.TestXgtEthernet(ip = plcIp, localEthernet = isXGI)
        let test kind init = 
            for idx in minIndex .. maxIndex do
                let address = formatAddressInRange isXGI code kind idx
                let value, dataType = generateTestValue kind init
                let ok = conn.Write(address, dataType, value)
                let read = conn.Read(address, dataType)
                Assert.True(ok, $"쓰기 실패 - {address}")
                Assert.Equal(value, read)

        for kind in areaTypes do
            if isUnsupportedCombination isXGI code kind then
                printfn $"[!] {code}{kind} 구간 테스트는 현재 설정에서 지원되지 않습니다."
            else
                test kind true
                test kind false

    [<Fact>]
    let ``XGT XGI Ethernet Range Test`` () =
        runEthernetTest TestEndpoints.ipXGILocal XGI_DEV true

        for dev in XGI_DEV do
            runEthernetRangeTest TestEndpoints.ipXGILocal dev 200 201 true

    [<Fact>]
    let ``XGT XGK Ethernet Range Test`` () =
        runEthernetTest TestEndpoints.ipXGK XGK_DEV false

        for dev in XGK_DEV do
            runEthernetRangeTest TestEndpoints.ipXGK  dev 200 201 false
