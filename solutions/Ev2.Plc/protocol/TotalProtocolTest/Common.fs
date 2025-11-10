module TotalProtocolTest.Common

open System
open System.Net.Sockets
open System.Threading

/// PLC connection endpoint definition.
type Endpoint = { Name: string; IpAddress: string; Port: int }

/// Mitsubishi test configuration extracted from the environment.
type MitsubishiConfig =
    { BitDevice: string
      BitAddress: int
      WordDevice: string
      WordAddress: int }

/// LS Electric address configuration.
type LsConfig =
    { BoolAddress: string
      WordAddress: string
      FloatAddress: string }

/// Allen-Bradley tag configuration.
type AbConfig =
    { BoolTag: string
      DIntTag: string
      RealTag: string }

/// Siemens Merker area address configuration.
type SiemensConfig =
    { BitAddress: string
      ByteAddress: string
      WordAddress: string }

/// Aggregated runtime configuration consumed by protocol tests.
type RuntimeConfig =
    { Iterations: int
      DelayMs: int
      Mitsubishi: MitsubishiConfig
      Ls: LsConfig
      Ab: AbConfig
      Siemens: SiemensConfig }

module Env =
    let private tryGetValue (names: string list) =
        names
        |> List.tryPick (fun name ->
            match Environment.GetEnvironmentVariable name with
            | null | "" -> None
            | value -> Some value)

    let getString names fallback =
        match tryGetValue names with
        | Some value -> value
        | None -> fallback

    let getInt names fallback =
        match tryGetValue names with
        | Some raw ->
            match Int32.TryParse raw with
            | true, value -> value
            | _ -> failwithf "환경 변수 %s 값을 정수로 해석할 수 없습니다: %s" (List.head names) raw
        | None -> fallback

        
    let lsEfmtb = { Name = "LS XGI EFMTB"; IpAddress = "192.168.9.100"; Port = 2004 }
    let lsLocalEthernet = { Name = "LS XGI LocalEthernet"; IpAddress = "192.168.9.102"; Port = 2004 }
    let abEndpoint = { Name = "Allen-Bradley EtherNet/IP"; IpAddress = "192.168.9.110"; Port = 44818 }
    let mxLocalEthernet = { Name = "Mitsubishi LocalEthernet"; IpAddress = "192.168.9.120"; Port = 7777 }
    let mxEthernet = { Name = "Mitsubishi Ethernet TCP"; IpAddress = "192.168.9.121"; Port = 5002 }
    let siemensCp = { Name = "Siemens CP"; IpAddress = "192.168.9.96"; Port = 102 }
    let siemensLocalEthernet = { Name = "Siemens LocalEthernet"; IpAddress = "192.168.9.97"; Port = 102 }


    let runtimeConfig () =
        let iterations = getInt [ "TOTALTEST_ITERATIONS"; "BITTEST_ITERATIONS" ] 5
        let delay = getInt [ "TOTALTEST_DELAY_MS"; "BITTEST_DELAY_MS" ] 0

        let mitsuBitDevice = getString [ "TOTALTEST_MITSU_BIT_DEVICE" ] "B"
        let mitsuBitAddress = getInt [ "TOTALTEST_MITSU_BIT_ADDRESS"; "BITTEST_MITSU_M_ADDRESS" ] 20
        let mitsuWordDevice = getString [ "TOTALTEST_MITSU_WORD_DEVICE" ] "D"
        let mitsuWordAddress = getInt [ "TOTALTEST_MITSU_WORD_ADDRESS" ] 0

        let lsBitIndex = getInt [ "TOTALTEST_LS_BOOL_INDEX"; "BITTEST_LS_M_INDEX" ] 0
        let lsBoolAddress = getString [ "TOTALTEST_LS_BOOL_ADDRESS" ] (sprintf "MX%d" lsBitIndex)
        let lsWordAddress = getString [ "TOTALTEST_LS_WORD_ADDRESS" ] (sprintf "MW%d" (lsBitIndex * 2))
        let lsFloatAddress = getString [ "TOTALTEST_LS_FLOAT_ADDRESS" ] (sprintf "MD%d" (lsBitIndex * 2))

        let abBoolTag = getString [ "TOTALTEST_AB_BOOL_TAG"; "BITTEST_AB_M_TAG" ] "bit1"
        let abDintTag = getString [ "TOTALTEST_AB_DINT_TAG" ] "ProductID"
        let abRealTag = getString [ "TOTALTEST_AB_REAL_TAG" ] "Flow_Rate"

        let siemensBitAddress = getString [ "TOTALTEST_SIEMENS_BIT_ADDRESS"; "BITTEST_SIEMENS_M_ADDRESS" ] "M1000.0"
        let siemensByteAddress = getString [ "TOTALTEST_SIEMENS_BYTE_ADDRESS" ] "MB1000"
        let siemensWordAddress = getString [ "TOTALTEST_SIEMENS_WORD_ADDRESS" ] "MW1000"

        { Iterations = iterations
          DelayMs = delay
          Mitsubishi =
            { BitDevice = mitsuBitDevice
              BitAddress = mitsuBitAddress
              WordDevice = mitsuWordDevice
              WordAddress = mitsuWordAddress }
          Ls =
            { BoolAddress = lsBoolAddress
              WordAddress = lsWordAddress
              FloatAddress = lsFloatAddress }
          Ab =
            { BoolTag = abBoolTag
              DIntTag = abDintTag
              RealTag = abRealTag }
          Siemens =
            { BitAddress = siemensBitAddress
              ByteAddress = siemensByteAddress
              WordAddress = siemensWordAddress } }

let tcpPing endpoint timeoutMs =
    use client = new TcpClient()
    let connectTask = client.ConnectAsync(endpoint.IpAddress, endpoint.Port) |> Async.AwaitTask
    try
        Async.RunSynchronously(connectTask, timeout = timeoutMs)
        if client.Connected then
            printfn "[ㅇ] %s (%s:%d) TCP 연결 확인" endpoint.Name endpoint.IpAddress endpoint.Port
        else
            failwithf "[%s] %s:%d TCP 연결이 성립하지 않았습니다." endpoint.Name endpoint.IpAddress endpoint.Port
    with
    | :? TimeoutException -> failwithf "[%s] %s:%d TCP 연결이 %dms 내에 성립하지 않았습니다." endpoint.Name endpoint.IpAddress endpoint.Port timeoutMs
    | :? SocketException as ex -> failwithf "[%s] %s:%d TCP 소켓 오류: %s" endpoint.Name endpoint.IpAddress endpoint.Port ex.Message

let udpPing endpoint timeoutMs =
    use client = new UdpClient()
    try
        client.Client.SendTimeout <- timeoutMs
        client.Connect(endpoint.IpAddress, endpoint.Port)
        let testData = Array.zeroCreate<byte> 1
        let sent = client.Send(testData, testData.Length)
        if sent > 0 then
            printfn "[ㅇ] %s (%s:%d) UDP 포트 전송 가능" endpoint.Name endpoint.IpAddress endpoint.Port
        else
            failwithf "[%s] %s:%d UDP 전송 실패" endpoint.Name endpoint.IpAddress endpoint.Port
    with
    | :? SocketException as ex ->
        failwithf "[%s] %s:%d UDP 소켓 오류: %s" endpoint.Name endpoint.IpAddress endpoint.Port ex.Message

let runToggle iterations delayMs label (read: unit -> Result<'a, string>) (write: 'a -> Result<unit, string>) (mutate: 'a -> 'a) (format: 'a -> string) =
    let readSafe () =
        match read() with
        | Ok value -> value
        | Error err -> failwithf "[%s] 읽기 실패: %s" label err

    let writeSafe value =
        match write value with
        | Ok () -> ()
        | Error err -> failwithf "[%s] 쓰기 실패 (%s): %s" label (format value) err

    let original = readSafe ()
    let alternate =
        let candidate = mutate original
        if candidate = original then
            failwithf "[%s] 변형 함수가 다른 값을 생성하지 못했습니다." label
        candidate

    printfn "    %s 초기값: %s / 테스트값: %s" label (format original) (format alternate)

    let mutable current = original
    try
        for step in 1 .. iterations do
            let desired = if step % 2 = 1 then alternate else original
            writeSafe desired
            let confirmed = readSafe ()
            if confirmed <> desired then
                failwithf "[%s] 확인 값이 기대와 다릅니다. 기대: %s, 실제: %s" label (format desired) (format confirmed)
            let previous = current
            current <- confirmed
            printfn "    %s #%d: %s -> %s" label step (format previous) (format confirmed)
            if delayMs > 0 then
                Thread.Sleep delayMs
    finally
        if current <> original then
            match write original with
            | Ok () ->
                printfn "    %s 복원: %s -> %s" label (format current) (format original)
            | Error err ->
                printfn "    %s 복원 실패 (%s -> %s): %s" label (format current) (format original) err
