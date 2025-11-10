module TotalProtocolTest.LsTest

open System
open TotalProtocolTest.Common
open Ev2.LsProtocol

let private adaptAddress (endpoint: Endpoint) (address: string) =
    if endpoint.Name.IndexOf("Local", StringComparison.OrdinalIgnoreCase) >= 0 && not (address.StartsWith("%", StringComparison.Ordinal)) then
        "%" + address
    else
        address

let private readBool (client: LsClient) address =
    try
        match client.Read(address, PlcTagDataType.Bool) with
        | ScalarValue.BoolValue value -> Ok value
        | other -> Error (sprintf "bool 값을 기대했으나 %A 을(를) 받았습니다." other)
    with ex -> Error ex.Message

let private writeBool (client: LsClient) address value =
    try
        client.Write(address, PlcTagDataType.Bool, ScalarValue.BoolValue value) |> ignore
        Ok ()
    with ex -> Error ex.Message

let private readInt16 (client: LsClient) address =
    try
        match client.Read(address, PlcTagDataType.Int16) with
        | ScalarValue.Int16Value value -> Ok value
        | ScalarValue.UInt16Value value -> Ok (int16 value)
        | other -> Error (sprintf "int16 값을 기대했으나 %A 을(를) 받았습니다." other)
    with ex -> Error ex.Message

let private writeInt16 (client: LsClient) address value =
    try
        client.Write(address, PlcTagDataType.Int16, ScalarValue.Int16Value value) |> ignore
        Ok ()
    with ex -> Error ex.Message

let private readFloat (client: LsClient) address =
    try
        match client.Read(address, PlcTagDataType.Float32) with
        | ScalarValue.Float32Value value -> Ok value
        | ScalarValue.Float64Value value -> Ok (float32 value)
        | other -> Error (sprintf "float 값을 기대했으나 %A 을(를) 받았습니다." other)
    with ex -> Error ex.Message

let private writeFloat (client: LsClient) address value =
    try
        client.Write(address, PlcTagDataType.Float32, ScalarValue.Float32Value value) |> ignore
        Ok ()
    with ex -> Error ex.Message

let private mutateInt16 (value: int16) =
    let toggled = (int value) ^^^ 0x000F |> int16
    if toggled <> value then toggled else value + 1s

let private mutateFloat (value: float32) =
    if Single.IsNaN value then 1.0f
    elif value = Single.PositiveInfinity then value - 1.0f
    elif value = Single.NegativeInfinity then 0.0f
    else
        let candidate = value + 1.25f
        if candidate = value then value + 2.5f else candidate

let private formatInt16 value = sprintf "%d" value
let private formatFloat value = sprintf "%.3ff" value

let run (config: RuntimeConfig) (endpoint: Endpoint) =
    tcpPing endpoint 2000
    let ls = config.Ls
    let timeout = max 1000 (config.DelayMs * 20)
    let isLocal = endpoint.Name.IndexOf("Local", StringComparison.OrdinalIgnoreCase) >= 0
    let packetLogger = fun direction (bytes: byte[]) length ->
        let hexStr = bytes.[0..length-1] |> Array.map (sprintf "%02X") |> String.concat " "
        printfn "[%s] %s: %s" endpoint.Name direction hexStr
    printfn "[DEBUG] Creating LsClient with packet logger for %s" endpoint.Name
    let client = new LsClient(endpoint.IpAddress, endpoint.Port, timeout, isLocal, packetLogger = packetLogger)
    match     client.Connect()  with
    | true -> ()
    | false -> failwithf "LS 이더넷 연결 실패: %s" endpoint.Name 


    try
        let boolAddress = adaptAddress endpoint ls.BoolAddress
        let boolLabel = sprintf "%s %s" endpoint.Name boolAddress
        printfn "[ㅇ] %s 비트 토글" boolLabel
        runToggle config.Iterations config.DelayMs boolLabel
            (fun () -> readBool client boolAddress)
            (fun value -> writeBool client boolAddress value)
            not
            (fun value -> if value then "ON" else "OFF")

        let wordAddress = adaptAddress endpoint ls.WordAddress
        let wordLabel = sprintf "%s %s" endpoint.Name wordAddress
        printfn "[ㅇ] %s 워드 토글" wordLabel
        runToggle config.Iterations config.DelayMs wordLabel
            (fun () -> readInt16 client wordAddress)
            (fun value -> writeInt16 client wordAddress value)
            mutateInt16
            formatInt16

        let floatAddress = adaptAddress endpoint ls.FloatAddress
        let floatLabel = sprintf "%s %s" endpoint.Name floatAddress
        printfn "[ㅇ] %s 실수 토글" floatLabel
        runToggle config.Iterations config.DelayMs floatLabel
            (fun () -> readFloat client floatAddress)
            (fun value -> writeFloat client floatAddress value)
            mutateFloat
            formatFloat
    finally
        client.Disconnect() |> ignore
