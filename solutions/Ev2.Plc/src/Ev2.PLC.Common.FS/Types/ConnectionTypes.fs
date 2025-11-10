namespace Ev2.PLC.Common.Types

open System
open System.Net

// ===================================
// Connection Types - Universal connection management for PLC communications
// ===================================

/// Connection status for PLC communications
type ConnectionStatus =
    /// Not connected to the PLC
    | Disconnected
    /// Attempting to establish connection
    | Connecting
    /// Successfully connected and operational
    | Connected
    /// Attempting to reconnect after connection loss
    | Reconnecting
    /// Connected but in maintenance mode (limited operations)
    | Maintenance
    /// Connection is in error state
    | Error of errorCode: string * message: string

    /// Check if connection allows normal operations
    member this.IsOperational =
        match this with
        | Connected -> true
        | _ -> false

    /// Check if connection allows read operations
    member this.CanRead =
        match this with
        | Connected | Maintenance -> true
        | _ -> false

    /// Check if connection allows write operations
    member this.CanWrite =
        match this with
        | Connected -> true
        | _ -> false


    /// Get display name for the status
    member this.DisplayName =
        match this with
        | Disconnected -> "Disconnected"
        | Connecting -> "Connecting"
        | Connected -> "Connected"
        | Reconnecting -> "Reconnecting"
        | Maintenance -> "Maintenance"
        | Error (code, _) -> $"Error ({code})"

/// Network transport type
type TransportType =
    | TCP
    | UDP
    | Serial
    | USB
    | Ethernet

    member this.Name =
        match this with
        | TCP -> "TCP"
        | UDP -> "UDP"
        | Serial -> "Serial"
        | USB -> "USB"
        | Ethernet -> "Ethernet"

    member this.IsNetworkBased =
        match this with
        | TCP | UDP | Ethernet -> true
        | Serial | USB -> false

/// Network endpoint information
type NetworkEndpoint = {
    Host: string
    Port: int
    TransportType: TransportType
    LocalEndpoint: IPEndPoint option
    Timeout: int // milliseconds
    KeepAlive: bool
} with
    static member Create(host: string, port: int, transport: TransportType, ?timeout: int, ?keepAlive: bool) = {
        Host = host
        Port = port
        TransportType = transport
        LocalEndpoint = None
        Timeout = defaultArg timeout 5000
        KeepAlive = defaultArg keepAlive true
    }

    member this.ToIPEndPoint() =
        try
            if this.TransportType.IsNetworkBased then
                let addresses = Dns.GetHostAddresses(this.Host)
                if addresses.Length > 0 then
                    Some (IPEndPoint(addresses.[0], this.Port))
                else
                    None
            else
                None
        with
        | _ -> None

    member this.IsValid =
        match this.TransportType with
        | TCP | UDP | Ethernet -> 
            not (String.IsNullOrWhiteSpace(this.Host)) && this.Port > 0 && this.Port <= 65535
        | Serial | USB -> 
            not (String.IsNullOrWhiteSpace(this.Host)) // Host contains device path

/// Serial port configuration
type SerialConfig = {
    PortName: string
    BaudRate: int
    DataBits: int
    StopBits: int
    Parity: string
    FlowControl: string
    ReadTimeout: int
    WriteTimeout: int
} with
    static member Default = {
        PortName = "COM1"
        BaudRate = 9600
        DataBits = 8
        StopBits = 1
        Parity = "None"
        FlowControl = "None"
        ReadTimeout = 1000
        WriteTimeout = 1000
    }

    static member Create(portName: string, ?baudRate: int, ?dataBits: int) = {
        SerialConfig.Default with
            PortName = portName
            BaudRate = defaultArg baudRate 9600
            DataBits = defaultArg dataBits 8
    }

/// Connection configuration
type ConnectionConfig = {
    Endpoint: NetworkEndpoint option
    SerialConfig: SerialConfig option
    MaxRetries: int
    RetryInterval: int // milliseconds
    HealthCheckInterval: int // milliseconds
    ConnectionTimeout: int // milliseconds
    ReceiveBufferSize: int
    SendBufferSize: int
    AdditionalParams: Map<string, string>
} with
    static member Default = {
        Endpoint = None
        SerialConfig = None
        MaxRetries = 3
        RetryInterval = 1000
        HealthCheckInterval = 30000
        ConnectionTimeout = 5000
        ReceiveBufferSize = 8192
        SendBufferSize = 8192
        AdditionalParams = Map.empty
    }

    static member ForTCP(host: string, port: int, ?timeout: int) = {
        ConnectionConfig.Default with
            Endpoint = Some (NetworkEndpoint.Create(host, port, TCP, ?timeout = timeout))
            ConnectionTimeout = defaultArg timeout 5000
    }

    static member ForUDP(host: string, port: int, ?timeout: int) = {
        ConnectionConfig.Default with
            Endpoint = Some (NetworkEndpoint.Create(host, port, UDP, ?timeout = timeout))
            ConnectionTimeout = defaultArg timeout 5000
    }

    static member ForSerial(portName: string, ?baudRate: int) = {
        ConnectionConfig.Default with
            SerialConfig = Some (SerialConfig.Create(portName, ?baudRate = baudRate))
    }

    member this.IsValid =
        match this.Endpoint, this.SerialConfig with
        | Some endpoint, None -> endpoint.IsValid
        | None, Some _ -> true
        | Some _, Some _ -> false // Cannot have both
        | None, None -> false // Must have one

/// Reconnection policy
type ReconnectionPolicy = {
    MaxAttempts: int
    InitialDelay: TimeSpan
    MaxDelay: TimeSpan
    BackoffMultiplier: float
    JitterEnabled: bool
    ExponentialBackoff: bool
} with
    static member Default = {
        MaxAttempts = 5
        InitialDelay = TimeSpan.FromSeconds(1.0)
        MaxDelay = TimeSpan.FromMinutes(5.0)
        BackoffMultiplier = 2.0
        JitterEnabled = true
        ExponentialBackoff = true
    }

    static member Aggressive = {
        MaxAttempts = 10
        InitialDelay = TimeSpan.FromMilliseconds(500.0)
        MaxDelay = TimeSpan.FromSeconds(30.0)
        BackoffMultiplier = 1.5
        JitterEnabled = true
        ExponentialBackoff = true
    }

    static member Conservative = {
        MaxAttempts = 3
        InitialDelay = TimeSpan.FromSeconds(5.0)
        MaxDelay = TimeSpan.FromMinutes(10.0)
        BackoffMultiplier = 3.0
        JitterEnabled = false
        ExponentialBackoff = true
    }

    static member Disabled = {
        MaxAttempts = 0
        InitialDelay = TimeSpan.Zero
        MaxDelay = TimeSpan.Zero
        BackoffMultiplier = 1.0
        JitterEnabled = false
        ExponentialBackoff = false
    }

    member this.GetDelay(attemptNumber: int) =
        if attemptNumber <= 0 || this.MaxAttempts = 0 then
            TimeSpan.Zero
        else
            let baseDelay = 
                if this.ExponentialBackoff then
                    this.InitialDelay.TotalMilliseconds * (pown this.BackoffMultiplier (attemptNumber - 1))
                else
                    this.InitialDelay.TotalMilliseconds

            let cappedDelay = min baseDelay this.MaxDelay.TotalMilliseconds
            
            if this.JitterEnabled then
                let random = Random()
                let jitter = cappedDelay * 0.1 * (random.NextDouble() - 0.5)
                TimeSpan.FromMilliseconds(max 0.0 (cappedDelay + jitter))
            else
                TimeSpan.FromMilliseconds(cappedDelay)

/// Connection metrics for monitoring
type ConnectionMetrics = {
    BytesSent: int64
    BytesReceived: int64
    MessagesSent: int64
    MessagesReceived: int64
    ErrorCount: int64
    LastActivityTime: DateTime
    ConnectionEstablishedTime: DateTime option
    TotalConnectTime: TimeSpan
    ReconnectionCount: int
    LastErrorTime: DateTime option
    LastErrorMessage: string option
} with
    static member Empty = {
        BytesSent = 0L
        BytesReceived = 0L
        MessagesSent = 0L
        MessagesReceived = 0L
        ErrorCount = 0L
        LastActivityTime = DateTime.UtcNow
        ConnectionEstablishedTime = None
        TotalConnectTime = TimeSpan.Zero
        ReconnectionCount = 0
        LastErrorTime = None
        LastErrorMessage = None
    }

    member this.UpdateActivity() = 
        { this with LastActivityTime = DateTime.UtcNow }

    member this.RecordBytesSent(count: int) = 
        { this with 
            BytesSent = this.BytesSent + int64 count
            LastActivityTime = DateTime.UtcNow }

    member this.RecordBytesReceived(count: int) = 
        { this with 
            BytesReceived = this.BytesReceived + int64 count
            LastActivityTime = DateTime.UtcNow }

    member this.RecordMessageSent() = 
        { this with 
            MessagesSent = this.MessagesSent + 1L
            LastActivityTime = DateTime.UtcNow }

    member this.RecordMessageReceived() = 
        { this with 
            MessagesReceived = this.MessagesReceived + 1L
            LastActivityTime = DateTime.UtcNow }

    member this.RecordError(errorMessage: string) = 
        { this with 
            ErrorCount = this.ErrorCount + 1L
            LastErrorTime = Some DateTime.UtcNow
            LastErrorMessage = Some errorMessage }

    member this.RecordConnected() = 
        { this with 
            ConnectionEstablishedTime = Some DateTime.UtcNow
            LastActivityTime = DateTime.UtcNow }

    member this.RecordReconnection() = 
        { this with ReconnectionCount = this.ReconnectionCount + 1 }

    member this.CalculateUptime() =
        match this.ConnectionEstablishedTime with
        | Some establishedTime -> DateTime.UtcNow - establishedTime
        | None -> TimeSpan.Zero

    member this.ThroughputSent() =
        let uptime = this.CalculateUptime()
        if uptime.TotalSeconds > 0.0 then
            float this.BytesSent / uptime.TotalSeconds
        else 0.0

    member this.ThroughputReceived() =
        let uptime = this.CalculateUptime()
        if uptime.TotalSeconds > 0.0 then
            float this.BytesReceived / uptime.TotalSeconds
        else 0.0

    member this.ErrorRate() =
        let totalMessages = this.MessagesSent + this.MessagesReceived
        if totalMessages = 0L then 0.0
        else (float this.ErrorCount) / (float totalMessages) * 100.0


/// Connection state change event
type ConnectionStateChangedEvent = {
    PlcId: string
    OldStatus: ConnectionStatus
    NewStatus: ConnectionStatus
    Timestamp: DateTime
    Reason: string option
} with
    static member Create(plcId: string, oldStatus: ConnectionStatus, newStatus: ConnectionStatus, ?reason: string) = {
        PlcId = plcId
        OldStatus = oldStatus
        NewStatus = newStatus
        Timestamp = DateTime.UtcNow
        Reason = reason
    }

/// Complete connection information
type ConnectionInfo = {
    PlcId: string
    Config: ConnectionConfig
    Status: ConnectionStatus
    Metrics: ConnectionMetrics
    ReconnectionPolicy: ReconnectionPolicy
    LastStateChange: DateTime
    StateHistory: ConnectionStateChangedEvent list
} with
    static member Create(plcId: string, config: ConnectionConfig, ?reconnectionPolicy: ReconnectionPolicy) = {
        PlcId = plcId
        Config = config
        Status = Disconnected
        Metrics = ConnectionMetrics.Empty
        ReconnectionPolicy = defaultArg reconnectionPolicy ReconnectionPolicy.Default
        LastStateChange = DateTime.UtcNow
        StateHistory = []
    }

    member this.IsOperational = this.Status.IsOperational
    member this.CanRead = this.Status.CanRead
    member this.CanWrite = this.Status.CanWrite

    member this.UpdateStatus(newStatus: ConnectionStatus, ?message: string) =
        let stateChangedEvent = ConnectionStateChangedEvent.Create(
            this.PlcId, 
            this.Status, 
            newStatus, 
            ?reason = message)
        
        { this with 
            Status = newStatus
            LastStateChange = DateTime.UtcNow
            StateHistory = stateChangedEvent :: (this.StateHistory |> List.take (min 100 this.StateHistory.Length)) }

    member this.UpdateMetrics(updateFn: ConnectionMetrics -> ConnectionMetrics) =
        { this with Metrics = updateFn this.Metrics }

/// Module for working with connections
module Connection =
    
    /// Parse connection string into configuration
    let parseConnectionString (connectionString: string) =
        let parts = connectionString.Split([|';'|], StringSplitOptions.RemoveEmptyEntries)
        let paramDict = 
            parts 
            |> Array.choose (fun part ->
                let kvp = part.Split([|'='|], 2)
                if kvp.Length = 2 then 
                    Some (kvp.[0].Trim().ToLowerInvariant(), kvp.[1].Trim())
                else None)
            |> Map.ofArray

        let getValue key defaultValue = 
            paramDict |> Map.tryFind key |> Option.defaultValue defaultValue

        let getIntValue key defaultValue = 
            paramDict 
            |> Map.tryFind key 
            |> Option.bind (fun v -> 
                match Int32.TryParse(v) with
                | true, i -> Some i
                | false, _ -> None)
            |> Option.defaultValue defaultValue

        let transport = 
            match getValue "transport" "tcp" with
            | "tcp" -> TCP
            | "udp" -> UDP
            | "serial" -> Serial
            | "usb" -> USB
            | "ethernet" -> Ethernet
            | _ -> TCP

        match transport with
        | TCP | UDP | Ethernet ->
            let host = getValue "host" "127.0.0.1"
            let port = getIntValue "port" 502
            let timeout = getIntValue "timeout" 5000
            let endpoint = NetworkEndpoint.Create(host, port, transport, timeout = timeout)
            
            { ConnectionConfig.Default with
                Endpoint = Some endpoint
                ConnectionTimeout = timeout
                MaxRetries = getIntValue "maxretries" 3
                RetryInterval = getIntValue "retryinterval" 1000 }
                
        | Serial ->
            let portName = getValue "port" "COM1"
            let baudRate = getIntValue "baudrate" 9600
            let dataBits = getIntValue "databits" 8
            let serialConfig = SerialConfig.Create(portName, baudRate = baudRate, dataBits = dataBits)
            
            { ConnectionConfig.Default with
                SerialConfig = Some serialConfig
                MaxRetries = getIntValue "maxretries" 3
                RetryInterval = getIntValue "retryinterval" 1000 }
                
        | USB ->
            let devicePath = getValue "device" ""
            let serialConfig = { SerialConfig.Default with PortName = devicePath }
            
            { ConnectionConfig.Default with
                SerialConfig = Some serialConfig
                MaxRetries = getIntValue "maxretries" 3
                RetryInterval = getIntValue "retryinterval" 1000 }

    /// Create connection string from configuration
    let toConnectionString (config: ConnectionConfig) =
        match config.Endpoint, config.SerialConfig with
        | Some endpoint, None ->
            let transport = endpoint.TransportType.Name.ToLowerInvariant()
            let baseString = $"transport={transport};host={endpoint.Host};port={endpoint.Port};timeout={endpoint.Timeout}"
            let additionalParams = 
                config.AdditionalParams 
                |> Map.toList 
                |> List.map (fun (k, v) -> $"{k}={v}")
                |> String.concat ";"
            
            if String.IsNullOrEmpty(additionalParams) then baseString
            else $"{baseString};{additionalParams}"
            
        | None, Some serialConfig ->
            let baseString = $"transport=serial;port={serialConfig.PortName};baudrate={serialConfig.BaudRate};databits={serialConfig.DataBits}"
            let additionalParams = 
                config.AdditionalParams 
                |> Map.toList 
                |> List.map (fun (k, v) -> $"{k}={v}")
                |> String.concat ";"
            
            if String.IsNullOrEmpty(additionalParams) then baseString
            else $"{baseString};{additionalParams}"
            
        | _ -> ""

    /// Validate connection configuration
    let validateConfig (config: ConnectionConfig) : Result<unit, string> =
        if not config.IsValid then
            Result.Error "Invalid connection configuration"
        else
            match config.Endpoint, config.SerialConfig with
            | Some endpoint, None ->
                if endpoint.IsValid then Ok ()
                else Result.Error "Invalid network endpoint configuration"
            | None, Some serialConfig ->
                if String.IsNullOrWhiteSpace(serialConfig.PortName) then
                    Result.Error "Serial port name cannot be empty"
                else Ok ()
            | _ -> Result.Error "Must specify either network endpoint or serial configuration"