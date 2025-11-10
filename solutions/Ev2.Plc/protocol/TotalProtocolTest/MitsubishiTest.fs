module TotalProtocolTest.MitsubishiTest

open System
open Microsoft.FSharp.Reflection
open TotalProtocolTest.Common
open Ev2.MxProtocol.Core
open Ev2.MxProtocol.Client
open Ev2.MxProtocol.Core.Constants

let private parseDevice description deviceText =
    let normalize (value: string) = value.Trim().ToUpperInvariant()
    let tryParse (value: string) =
        let cases = FSharpType.GetUnionCases(typeof<DeviceCode>)
        cases
        |> Array.tryFind (fun c -> String.Equals(c.Name, value, StringComparison.OrdinalIgnoreCase))
        |> Option.map (fun case -> FSharpValue.MakeUnion(case, [||]) :?> DeviceCode)
    match deviceText with
    | null -> failwithf "[Mitsubishi] %s 디바이스 코드가 비어 있습니다." description
    | text ->
        let normalized = normalize text
        match tryParse normalized with
        | Some device -> device
        | None -> failwithf "[Mitsubishi] %s 디바이스 코드를 해석할 수 없습니다: %s" description normalized

let private readBit (client: MelsecClient) (device: DeviceCode) (address: int) =
    client.ReadBits(device, address, 1)
    |> Result.mapError (fun err -> sprintf "ReadBits(%A,%d) 실패: %s" device address err)
    |> Result.bind (function
        | [| value |] -> Ok value
        | values -> Error (sprintf "비트 데이터 길이가 예상과 다릅니다. 길이: %d" values.Length))

let private writeBit (client: MelsecClient) (device: DeviceCode) (address: int) (value: bool) =
    client.WriteBits(device, address, [| value |])
    |> Result.mapError (fun err -> sprintf "WriteBits(%A,%d) 실패: %s" device address err)

let private readWord (client: MelsecClient) (device: DeviceCode) (address: int) =
    client.ReadWords(device, address, 1)
    |> Result.mapError (fun err -> sprintf "ReadWords(%A,%d) 실패: %s" device address err)
    |> Result.bind (function
        | [| value |] -> Ok value
        | values -> Error (sprintf "워드 데이터 길이가 예상과 다릅니다. 길이: %d" values.Length))

let private writeWord (client: MelsecClient) (device: DeviceCode) (address: int) (value: uint16) =
    client.WriteWords(device, address, [| value |])
    |> Result.mapError (fun err -> sprintf "WriteWords(%A,%d) 실패: %s" device address err)

let private mutateWord (value: uint16) =
    let toggled = value ^^^ 0x00FFus
    if toggled <> value then toggled else value ^^^ 0xFFFFus

let private formatBool value = if value then "ON" else "OFF"

let private formatWord (value: uint16) = sprintf "0x%04X (%d)" value value

let run (config: RuntimeConfig) (endpoint: Endpoint) =
    tcpPing endpoint 2000

    let mitsu = config.Mitsubishi
    let bitDevice = parseDevice "bit" mitsu.BitDevice
    let wordDevice = parseDevice "word" mitsu.WordDevice

    let melsecConfig = Defaults.config endpoint.Name endpoint.IpAddress endpoint.Port
    let packetLogger = fun direction (bytes: byte[]) length ->
        let hexStr = bytes.[0..length-1] |> Array.map (sprintf "%02X") |> String.concat " "
        printfn "[%s] %s: %s" endpoint.Name direction hexStr
    printfn "[DEBUG] Creating MelsecClient with packet logger for %s" endpoint.Name
    use client = new MelsecClient(melsecConfig, packetLogger = packetLogger)

    let bitLabel = sprintf "%s %s%d" endpoint.Name mitsu.BitDevice mitsu.BitAddress
    printfn "[ㅇ] %s 비트 토글" bitLabel
    runToggle config.Iterations config.DelayMs bitLabel
        (fun () -> readBit client bitDevice mitsu.BitAddress)
        (fun value -> writeBit client bitDevice mitsu.BitAddress value)
        not
        formatBool

    let wordLabel = sprintf "%s %s%d" endpoint.Name mitsu.WordDevice mitsu.WordAddress
    printfn "[ㅇ] %s 워드 토글" wordLabel
    runToggle config.Iterations config.DelayMs wordLabel
        (fun () -> readWord client wordDevice mitsu.WordAddress)
        (fun value -> writeWord client wordDevice mitsu.WordAddress value)
        mutateWord
        formatWord