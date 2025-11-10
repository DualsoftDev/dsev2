namespace Ev2.AbProtocol.Protocol

open System
open System.Net
open System.Net.Sockets
open System.Threading
open System.Text
open Ev2.AbProtocol.Core

/// <summary>
///     Manages the lifecycle of an EtherNet/IP session, wrapping socket creation,
///     registration, keep-alive and low-level send/receive operations.  The rest of
///     the protocol stack delegates raw communication tasks to this component.
/// </summary>
type SessionManager(config: ConnectionConfig, ?packetLogger: string -> byte[] -> int -> unit) =
    
    // ========================================
    // 내부 상태
    // ========================================
    
    let mutable socket: Socket option = None
    let mutable sessionHandle = 0u
    let mutable isConnected = false
    let mutable contextCounter = 0L
    let mutable lastActivity = DateTime.UtcNow
    let connectionLock = obj()
    let packetLogger = packetLogger

    let logPacket direction (bytes: byte[]) length =
        match packetLogger with
        | Some logger -> logger direction bytes length
        | None -> ()
    // ========================================
    // 컨텍스트 관리
    // ========================================
    
    /// 다음 컨텍스트 ID 생성
    let getNextContext() =
        Interlocked.Increment(&contextCounter) |> uint64
    
    // ========================================
    // 소켓 관리
    // ========================================
    
    /// 소켓 생성 및 설정
    let createSocket() =
        let sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        sock.ReceiveTimeout <- int config.Timeout.TotalMilliseconds
        sock.SendTimeout <- int config.Timeout.TotalMilliseconds
        sock.NoDelay <- true
        sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true)
        sock
    
    /// 소켓 안전하게 닫기
    let closeSocket (sock: Socket) =
        try
            sock.Shutdown(SocketShutdown.Both)
        with _ -> ()
        try
            sock.Close()
        with _ -> ()
    
    // ========================================
    // 데이터 송수신
    // ========================================
    
    /// 데이터 전송
    let trySend (sock: Socket) (data: byte[]) =
        try
            let sent = sock.Send(data)
            if sent <> data.Length then
                NetworkError SocketError.SocketError
            else
                NoError
        with
        | :? SocketException as ex ->
            NetworkError ex.SocketErrorCode
        | ex ->
            UnknownError ex.Message
    
    /// 데이터 수신
    let tryReceive (sock: Socket) (buffer: byte[]) =
        try
            let received = sock.Receive(buffer)
            if received = 0 then
                (ConnectionError "Connection closed by remote", None)
            else
                (NoError, Some (buffer, received))
        with
        | :? SocketException as ex ->
            (NetworkError ex.SocketErrorCode, None)
        | ex ->
            (UnknownError ex.Message, None)
    
    /// 데이터 송수신 (동기화)
    let sendReceive (data: byte[]) =
        lock connectionLock (fun () ->
            match socket with
            | None -> (ConnectionError "Not connected", None)
            | Some sock ->
                logPacket "[TX]" data data.Length
                
                match trySend sock data with
                | NoError ->
                    let buffer = Array.zeroCreate<byte> 4096
                    match tryReceive sock buffer with
                    | (NoError, Some (buffer, received)) ->
                        lastActivity <- DateTime.UtcNow
                        logPacket "[RX]" buffer received
                        (NoError, Some (buffer, received))
                    | (error, _) -> (error, None)
                | error -> (error, None)
        )
    
    /// 재시도 로직
    let rec sendReceiveWithRetry (data: byte[]) (retries: int) =
        match sendReceive data with
        | (NoError, result) -> (NoError, result)
        | (error, _) when retries > 0 ->
            Thread.Sleep(config.RetryDelay)
            sendReceiveWithRetry data (retries - 1)
        | (error, _) -> (error, None)
    
    // ========================================
    // 세션 관리
    // ========================================
    
    /// 세션 등록
    let registerSession() =
        let packet = PacketBuilder.buildRegisterSession()
        match sendReceiveWithRetry packet config.MaxRetries with
        | (NoError, Some (buffer, length)) ->
            let (error, handleOpt) = PacketParser.parseRegisterSession buffer
            match error with
            | NoError ->
                match handleOpt with
                | Some handle ->
                    sessionHandle <- handle
                    (NoError, Some handle)
                | None -> (UnknownError "Session handle not returned", None)
            | _ -> (error, None)
        | (error, _) -> (error, None)
    
    /// 세션 등록 해제 (안전)
    let unregisterSession() =
        if sessionHandle <> 0u then
            try
                let packet = PacketBuilder.buildUnregisterSession sessionHandle
                lock connectionLock (fun () ->
                    match socket with
                    | Some sock ->
                        try
                            sock.Send(packet) |> ignore
                        with _ -> ()
                    | None -> ()
                )
            with _ -> ()
            
            sessionHandle <- 0u
    
    // ========================================
    // 연결 관리
    // ========================================
    
    /// 비동기 연결 시도
    let tryConnect (sock: Socket) (endpoint: IPEndPoint) =
        try
            let connectResult = sock.BeginConnect(endpoint, null, null)
            let success = connectResult.AsyncWaitHandle.WaitOne(config.Timeout)
            
            if not success then
                closeSocket sock
                ConnectionError "Connection timeout"
            else
                try
                    sock.EndConnect(connectResult)
                    NoError
                with ex ->
                    closeSocket sock
                    ConnectionError (sprintf "Connection failed: %s" ex.Message)
        with ex ->
            closeSocket sock
            ConnectionError (sprintf "Connection error: %s" ex.Message)


    member this.Connect() =
        lock connectionLock (fun () ->
            if isConnected then
                (NoError, Some sessionHandle)
            else
                try
                    // IP 주소 파싱
                    let success, ipAddressNullable = IPAddress.TryParse(config.IpAddress)
                
                    if not success then
                        (ConnectionError (sprintf "Invalid IP address: %s" config.IpAddress), None)
                    else
                        // non-null 체크 후 안전하게 사용
                        match ipAddressNullable with
                        | null -> 
                            (ConnectionError (sprintf "Invalid IP address: %s" config.IpAddress), None)
                        | ipAddress ->
                            let sock = createSocket()
                            let endpoint = IPEndPoint(ipAddress, config.Port)
                        
                            match tryConnect sock endpoint with
                            | NoError ->
                                socket <- Some sock
                            
                                match registerSession() with
                                | (NoError, Some handle) ->
                                    isConnected <- true
                                    lastActivity <- DateTime.UtcNow
                                    (NoError, Some handle)
                                | (error, _) ->
                                    closeSocket sock
                                    socket <- None
                                    (error, None)
                            | error ->
                                (error, None)
                with ex ->
                    match socket with
                    | Some s -> closeSocket s
                    | None -> ()
                    socket <- None
                    (ConnectionError (sprintf "Connection exception: %s" ex.Message), None)
        )
    
    /// 연결 해제
    member this.Disconnect() =
        lock connectionLock (fun () ->
            if isConnected then
                unregisterSession()
                
                match socket with
                | Some sock -> closeSocket sock
                | None -> ()
                
                socket <- None
                isConnected <- false
                sessionHandle <- 0u
        )
    
    // ========================================
    // 패킷 전송 메서드
    // ========================================
    
    /// <summary>
    ///     Sends a packet that already includes an EIP header and returns the raw response buffer.
    ///     The call enforces context validation to ensure the reply belongs to the original request.
    /// </summary>
    member this.SendPacket(command: uint16, data: byte[]) =
        if not isConnected then
            (ConnectionError "Not connected", None)
        else
            let context = getNextContext()
            let header = PacketBuilder.buildEIPHeader command sessionHandle context data.Length
            let packet = Array.append header data
            
            match sendReceiveWithRetry packet config.MaxRetries with
            | (NoError, Some (buffer, length)) ->
                match PacketParser.parseEIPHeader buffer with
                | NoError ->
                    match PacketParser.parseEIPHeaderData buffer with
                    | Some (_, _, _, returnContext, _) when returnContext = context ->
                        (NoError, Some (buffer, length))
                    | Some _ ->
                        (SessionError "Context mismatch", None)
                    | None ->
                        (UnknownError "Cannot extract EIP header data", None)
                | error ->
                    (error, None)
            | (error, _) -> (error, None)
    
    /// Unconnected Send
    member this.SendUnconnected(cipData: byte[]) =
        let cpf = PacketBuilder.buildCPFUnconnected cipData
        this.SendPacket(Constants.EIP.UnconnectedSend, cpf)
    
    /// Connected Send
    member this.SendConnected(connectionId: uint32, sequenceNumber: uint16, cipData: byte[]) =
        let cpf = PacketBuilder.buildCPFConnected connectionId sequenceNumber cipData
        this.SendPacket(Constants.EIP.ConnectedSend, cpf)
    
    // ========================================
    // Keep-alive 및 유틸리티
    // ========================================
    
    /// Keep-alive 전송
    member this.KeepAlive() =
        if isConnected then
            let timeSinceLastActivity = DateTime.UtcNow - lastActivity
            if timeSinceLastActivity.TotalMilliseconds > float Constants.Performance.KeepAliveInterval then
                let packet = PacketBuilder.buildListServices()
                match sendReceive packet with
                | (NoError, _) -> lastActivity <- DateTime.UtcNow
                | _ -> ()
    
    /// 통계 반환
    member this.GetStatistics() =
        {
            CommunicationStats.Empty with
                ConnectionUptime = 
                    if isConnected then DateTime.UtcNow - lastActivity
                    else TimeSpan.Zero
        }
    
    // ========================================
    // 속성
    // ========================================
    
    /// 연결 상태
    member this.IsConnected = isConnected
    
    /// 세션 핸들
    member this.SessionHandle = sessionHandle
    
    /// 마지막 활동 시간
    member this.LastActivity = lastActivity
    
    /// 설정
    member this.Config = config
    
    // ========================================
    // IDisposable 구현
    // ========================================
    
    interface IDisposable with
        member this.Dispose() =
            this.Disconnect()

/// 세션 관리 헬퍼 함수 (NoError 패턴)
module SessionHelpers =
    
    /// 세션이 유효한지 확인
    let isSessionValid (session: SessionManager) =
        session.IsConnected && session.SessionHandle <> 0u
    
    /// 세션 재연결 시도
    let tryReconnect (session: SessionManager) =
        session.Disconnect()
        Thread.Sleep(1000)  // 재연결 전 대기
        session.Connect()
    
    /// 안전한 작업 실행 (자동 재연결 포함)
    let executeWithRetry (session: SessionManager) (operation: unit -> (AbProtocolError * 'T option)) (maxRetries: int) =
        let rec retry attempt =
            if attempt >= maxRetries then
                (ConnectionError "Max retries exceeded", None)
            else
                match operation() with
                | (NoError, Some result) -> (NoError, Some result)
                | (ConnectionError _ as error, _) when attempt < maxRetries - 1 ->
                    match tryReconnect session with
                    | (NoError, _) -> retry (attempt + 1)
                    | _ -> (ConnectionError (sprintf "Reconnection failed after %d attempts" (attempt + 1)), None)
                | (SessionError _ as error, _) when attempt < maxRetries - 1 ->
                    match tryReconnect session with
                    | (NoError, _) -> retry (attempt + 1)
                    | _ -> (ConnectionError (sprintf "Reconnection failed after %d attempts" (attempt + 1)), None)
                | (error, result) -> (error, result)
        
        retry 0
    
    /// Keep-alive 타이머 생성
    let createKeepAliveTimer (session: SessionManager) (interval: TimeSpan) =
        let timer = new System.Timers.Timer(interval.TotalMilliseconds)
        timer.Elapsed.Add(fun _ -> session.KeepAlive())
        timer.AutoReset <- true
        timer
    
    /// 연결 상태 모니터링
    let monitorConnection (session: SessionManager) (onDisconnected: unit -> unit) =
        let timer = new System.Timers.Timer(5000.0)  // 5초마다 체크
        timer.Elapsed.Add(fun _ ->
            if not (isSessionValid session) then
                timer.Stop()
                onDisconnected()
        )
        timer.AutoReset <- true
        timer
    
    /// 안전한 연결 확인 및 복구
    let ensureConnected (session: SessionManager) =
        if not (isSessionValid session) then
            match tryReconnect session with
            | (NoError, Some handle) -> (NoError, true)
            | (error, _) -> (error, false)
        else
            (NoError, true)
    
    /// 여러 작업을 배치로 실행 (에러 처리 포함)
    let executeBatch (session: SessionManager) (operations: (unit -> (AbProtocolError * 'T option)) list) =
        let results = ResizeArray<AbProtocolError * 'T option>()
        let mutable continueProcessing = true
        
        for operation in operations do
            if continueProcessing then
                let (error, result) = executeWithRetry session operation 3
                results.Add((error, result))
                
                // 치명적 에러 발생 시 중단
                match error with
                | ConnectionError _ | SessionError _ -> continueProcessing <- false
                | _ -> ()
        
        results.ToArray()
    
    /// 연결 타임아웃 체크
    let checkTimeout (session: SessionManager) (timeout: TimeSpan) =
        let timeSinceLastActivity = DateTime.UtcNow - session.LastActivity
        if timeSinceLastActivity > timeout then
            (TimeoutError, false)
        else
            (NoError, true)
    
    /// 세션 상태 정보 가져오기
    let getSessionInfo (session: SessionManager) =
        {|
            IsConnected = session.IsConnected
            SessionHandle = session.SessionHandle
            LastActivity = session.LastActivity
            TimeSinceLastActivity = DateTime.UtcNow - session.LastActivity
            Config = session.Config
        |}
