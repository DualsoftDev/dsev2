module TotalProtocolTest.SiemensTest

open System
open TotalProtocolTest.Common
open Ev2.S7Protocol.Core
open Ev2.S7Protocol.Client

let private mutateByte (value: byte) =
    let toggled = value ^^^ 0x0Fuy
    if toggled <> value then toggled else value ^^^ 0xFFuy

let private mutateInt16 (value: int16) =
    let toggled = (int value) ^^^ 0x00FF |> int16
    if toggled <> value then toggled else value + 1s

let private formatBool value = if value then "ON" else "OFF"
let private formatByte value = sprintf "0x%02X (%d)" value value
let private formatInt16 value = sprintf "%d" value

let run (config: RuntimeConfig) (endpoint: Endpoint) rack slot =
    tcpPing endpoint 2000

    let siemens = config.Siemens
    let clientConfig: S7Config =
        { Name = endpoint.Name
          IpAddress = endpoint.IpAddress
          CpuType = CpuType.S7300
          Rack = rack
          Slot = slot
          Port = endpoint.Port
          LocalTSAP = 0x0100
          RemoteTSAP = 0x0102
          Timeout = TimeSpan.FromSeconds 5.0
          MaxPDUSize = 240
          Password = None }

    let packetLogger = fun direction (bytes: byte[]) length ->
        let hexStr = bytes.[0..length-1] |> Array.map (sprintf "%02X") |> String.concat " "
        printfn "[%s] %s: %s" endpoint.Name direction hexStr
    printfn "[DEBUG] Creating S7Client with packet logger for %s" endpoint.Name
    use client = new S7Client(clientConfig, packetLogger = packetLogger)

    match client.Connect() with
    | Error err -> failwithf "[%s] 연결 실패: %s" endpoint.Name err
    | Ok _ ->
        try
            let bitLabel = sprintf "%s %s" endpoint.Name siemens.BitAddress
            printfn "[ㅇ] %s 비트 토글" bitLabel
            runToggle config.Iterations config.DelayMs bitLabel
                (fun () -> client.ReadBit(siemens.BitAddress))
                (fun value -> client.WriteBit(siemens.BitAddress, value))
                not
                formatBool

            let byteLabel = sprintf "%s %s" endpoint.Name siemens.ByteAddress
            printfn "[ㅇ] %s 바이트 토글" byteLabel
            runToggle config.Iterations config.DelayMs byteLabel
                (fun () -> client.ReadByte(siemens.ByteAddress))
                (fun value -> client.WriteByte(siemens.ByteAddress, value))
                mutateByte
                formatByte

            let wordLabel = sprintf "%s %s" endpoint.Name siemens.WordAddress
            printfn "[ㅇ] %s 워드 토글" wordLabel
            runToggle config.Iterations config.DelayMs wordLabel
                (fun () -> client.ReadInt16(siemens.WordAddress))
                (fun value -> client.WriteInt16(siemens.WordAddress, value))
                mutateInt16
                formatInt16
        finally
            client.Disconnect() |> ignore
