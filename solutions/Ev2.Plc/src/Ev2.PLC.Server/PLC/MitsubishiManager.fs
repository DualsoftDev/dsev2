namespace DSPLCServer.PLC

open System
open Microsoft.Extensions.Logging
open DSPLCServer.Common
open DSPLCServer.Database

/// Mitsubishi PLC 관리자 (더미 구현)
type MitsubishiManager(plcId: string, connectionString: string, logger: ILogger) =
    inherit PLCManagerBase(plcId, PlcVendor.Mitsubishi, connectionString, logger)
    
    let mutable isConnected = false
    let random = Random()
    
    /// 연결 문자열 파싱
    member private this.ParseConnectionString() =
        // 예: Host=192.168.1.100;Port=1280;Protocol=MC;Station=0
        let parts = connectionString.Split(';')
        let paramDict = 
            parts 
            |> Array.choose (fun part ->
                let kvp = part.Split('=')
                if kvp.Length = 2 then Some (kvp.[0].Trim(), kvp.[1].Trim())
                else None)
            |> dict
        
        let host = paramDict.TryGetValue("Host") |> fun (found, value) -> if found then value else "127.0.0.1"
        let port = paramDict.TryGetValue("Port") |> fun (found, value) -> if found then int value else 1280
        let protocol = paramDict.TryGetValue("Protocol") |> fun (found, value) -> if found then value else "MC"
        let station = paramDict.TryGetValue("Station") |> fun (found, value) -> if found then int value else 0
        
        (host, port, protocol, station)
    
    /// 태그 주소 유효성 검증 (Mitsubishi 형식)
    member private this.ValidateTagAddress(address: string) =
        // Mitsubishi 주소 형식: D100, M0, X0, Y0, T0, C0 등
        System.Text.RegularExpressions.Regex.IsMatch(address, @"^[DMXYTCSLBFW]\d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
    
    /// 더미 값 생성
    member private this.GenerateDummyValue(dataType: TagDataType) =
        match dataType with
        | Bool -> BoolValue (random.Next(2) = 1)
        | Int16 | Int32 | Int64 -> IntValue (int64 (random.Next(-32768, 32767)))
        | Float32 | Float64 -> FloatValue (random.NextDouble() * 1000.0)
        | String -> StringValue $"Mitsubishi_{random.Next(100)}"
        | Bytes -> BytesValue [| byte (random.Next(256)); byte (random.Next(256)) |]
    
    override this.ConnectImpl() =
        try
            let (host, port, protocol, station) = this.ParseConnectionString()
            logger.LogInformation("Connecting to Mitsubishi PLC at {Host}:{Port} using {Protocol} (Station: {Station})", host, port, protocol, station)
            
            // 더미 연결 시뮬레이션
            System.Threading.Thread.Sleep(150)  // Mitsubishi는 연결이 조금 더 오래 걸림
            
            if host = "fail" then
                failwith "Simulated connection failure"
            
            isConnected <- true
            logger.LogInformation("Successfully connected to Mitsubishi PLC {PlcId}", plcId)
            true
            
        with
        | ex ->
            logger.LogError(ex, "Failed to connect to Mitsubishi PLC {PlcId}", plcId)
            false
    
    override this.DisconnectImpl() =
        try
            logger.LogInformation("Disconnecting from Mitsubishi PLC {PlcId}", plcId)
            
            // 더미 연결 해제 시뮬레이션
            System.Threading.Thread.Sleep(50)
            
            isConnected <- false
            logger.LogInformation("Successfully disconnected from Mitsubishi PLC {PlcId}", plcId)
            
        with
        | ex ->
            logger.LogError(ex, "Error during disconnection from Mitsubishi PLC {PlcId}", plcId)
    
    override this.ReadTagImpl(tag: TagConfiguration) =
        if not isConnected then
            failwith "PLC is not connected"
        
        if not (this.ValidateTagAddress(tag.Address)) then
            failwith $"Invalid tag address: {tag.Address}"
        
        try
            // 더미 읽기 시뮬레이션
            System.Threading.Thread.Sleep(random.Next(10, 50))
            
            let value = this.GenerateDummyValue(tag.DataType)
            logger.LogTrace("Read tag {TagName} ({Address}) = {Value}", tag.Name, tag.Address, value.ToString())
            
            value
            
        with
        | ex ->
            logger.LogError(ex, "Failed to read tag {TagName} from Mitsubishi PLC {PlcId}", tag.Name, plcId)
            reraise()
    
    override this.HealthCheckImpl() =
        try
            if not isConnected then
                false
            else
                // 더미 헬스 체크 시뮬레이션
                System.Threading.Thread.Sleep(20)
                
                // 85% 확률로 건강함 (Mitsubishi는 약간 더 불안정)
                let isHealthy = random.Next(20) < 17
                logger.LogTrace("Health check for Mitsubishi PLC {PlcId}: {IsHealthy}", plcId, isHealthy)
                
                isHealthy
            
        with
        | ex ->
            logger.LogError(ex, "Health check failed for Mitsubishi PLC {PlcId}", plcId)
            false