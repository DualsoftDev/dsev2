module TotalProtocolTest.AbTest

open System
open TotalProtocolTest.Common
open Ev2.AbProtocol.Core
open Ev2.AbProtocol.Client

let private readTagAs<'T> (client: ABClient) (tagName: string) (dataType: DataType) =
    match client.ReadTag(tagName, dataType) with
    | NoError, valueOpt ->
        match valueOpt with
        | Some value ->
            match ABClientUtil.tryConvertWithType typeof<'T> value with
            | Some converted -> Ok (converted :?> 'T)
            | None ->
                Error (
                    sprintf
                        "[Allen-Bradley] %s tag type mismatch (expected: %s, actual: %s)"
                        tagName
                        typeof<'T>.Name
                        (value.GetType().Name)
                )
        | None ->
            Error (sprintf "[Allen-Bradley] %s tag returned no data." tagName)
    | error, _ ->
        Error (sprintf "Failed to read Allen-Bradley tag %s: %s" tagName error.Message)

let private writeTagValue<'T> (client: ABClient) (tagName: string) (dataType: DataType) (value: 'T) =
    match ABClientUtil.encodeValue dataType (box value) with
    | Some payload ->
        match client.WriteTag(tagName, dataType, payload) with
        | NoError -> Ok ()
        | error -> Error (sprintf "Failed to write Allen-Bradley tag %s: %s" tagName error.Message)
    | None ->
        Error (sprintf "Unable to encode Allen-Bradley tag %s value as %A." tagName dataType)

let private mutateDint (value: int32) =
    let toggled = value ^^^ 0x0000FFFF
    if toggled <> value then toggled else value + 1

let private mutateReal (value: single) =
    if Single.IsNaN value then 1.0f
    elif Single.IsPositiveInfinity value then value - 1.0f
    elif Single.IsNegativeInfinity value then 0.0f
    else
        let candidate = value + 1.25f
        if candidate = value then value + 2.5f else candidate

let private formatBool value = if value then "ON" else "OFF"
let private formatDint value = sprintf "%d" value
let private formatReal value = sprintf "%.3ff" value

let run (config: RuntimeConfig) (endpoint: Endpoint) =
    tcpPing endpoint 2000

    let timeoutMs = max 5000 (config.DelayMs * 25)
    let connectionConfig =
        { ConnectionConfig.Create(endpoint.IpAddress, port = endpoint.Port) with
            Timeout = TimeSpan.FromMilliseconds(float timeoutMs)
            RetryDelay = TimeSpan.FromMilliseconds(200.0)
            MaxRetries = 3
        }

    let packetLogger = fun direction (bytes: byte[]) length ->
        let hexStr = bytes.[0..length-1] |> Array.map (sprintf "%02X") |> String.concat " "
        printfn "[%s] %s: %s" endpoint.Name direction hexStr
    printfn "[DEBUG] Creating ABClient with packet logger for %s" endpoint.Name
    use client = new ABClient(connectionConfig, packetLogger = packetLogger)

    match client.Connect() with
    | NoError, _ ->
        match client.PlcInfo with
        | Some info ->
            printfn "[OK] Connected to %s PLC (%s, Rev %O)" endpoint.Name info.ProductName info.Revision
        | None ->
            printfn "[OK] Connected to %s PLC" endpoint.Name

        let ab = config.Ab

        let boolLabel = sprintf "%s %s" endpoint.Name ab.BoolTag
        printfn "[OK] Toggling %s" boolLabel
        runToggle config.Iterations config.DelayMs boolLabel
            (fun () -> readTagAs<bool> client ab.BoolTag DataType.BOOL)
            (fun value -> writeTagValue client ab.BoolTag DataType.BOOL value)
            not
            formatBool

        let dintLabel = sprintf "%s %s" endpoint.Name ab.DIntTag
        printfn "[OK] Toggling %s" dintLabel
        runToggle config.Iterations config.DelayMs dintLabel
            (fun () -> readTagAs<int32> client ab.DIntTag DataType.DINT)
            (fun value -> writeTagValue client ab.DIntTag DataType.DINT value)
            mutateDint
            formatDint

        let realLabel = sprintf "%s %s" endpoint.Name ab.RealTag
        printfn "[OK] Toggling %s" realLabel
        runToggle config.Iterations config.DelayMs realLabel
            (fun () -> readTagAs<single> client ab.RealTag DataType.REAL)
            (fun value -> writeTagValue client ab.RealTag DataType.REAL value)
            mutateReal
            formatReal
    | error, _ ->
        failwithf "[Allen-Bradley] Failed to connect %s: %s" endpoint.Name error.Message
