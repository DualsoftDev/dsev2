namespace DSPLCServer.PLC

open System
open Microsoft.Extensions.Logging
open DSPLCServer.Common
open DSPLCServer.Database

/// LS Electric PLC 관리자 (더미 구현)
type LSElectricManager(plcId: string, connectionString: string, logger: ILogger) =
    inherit PLCManagerBase(plcId, PlcVendor.LSElectric, connectionString, logger)
    
    let mutable isConnected = false
    let random = Random()
    
    /// 연결 문자열 파싱
    member private this.ParseConnectionString() =
        // 예: Host=192.168.1.100;Port=2004;Protocol=XGT;Timeout=5000
        let parts = connectionString.Split(';')
        let paramDict = 
            parts 
            |> Array.choose (fun part ->
                let kvp = part.Split('=')
                if kvp.Length = 2 then Some (kvp.[0].Trim(), kvp.[1].Trim())
                else None)
            |> dict
        
        let host = paramDict.TryGetValue("Host") |> fun (found, value) -> if found then value else "127.0.0.1"
        let port = paramDict.TryGetValue("Port") |> fun (found, value) -> if found then int value else 2004
        let protocol = paramDict.TryGetValue("Protocol") |> fun (found, value) -> if found then value else "XGT"
        let timeout = paramDict.TryGetValue("Timeout") |> fun (found, value) -> if found then int value else 5000
        
        (host, port, protocol, timeout)
    
    /// 태그 주소 유효성 검증
    member private this.ValidateTagAddress(address: string) =
        // LS Electric 주소 형식: D0, M100, P0.0 등
        System.Text.RegularExpressions.Regex.IsMatch(address, @"^[DMPIQLFK]\d+(\.\d+)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
    
    /// 더미 값 생성
    member private this.GenerateDummyValue(dataType: TagDataType) =
        match dataType with
        | Bool -> BoolValue (random.Next(2) = 1)
        | Int16 | Int32 | Int64 -> IntValue (int64 (random.Next(-1000, 1000)))
        | Float32 | Float64 -> FloatValue (random.NextDouble() * 100.0)
        | String -> StringValue $"DummyString_{random.Next(100)}"
        | Bytes -> BytesValue [| byte (random.Next(256)); byte (random.Next(256)) |]
    
    override this.ConnectImpl() =
        try
            let (host, port, protocol, timeout) = this.ParseConnectionString()
            logger.LogInformation("Connecting to LS Electric PLC at {Host}:{Port} using {Protocol}", host, port, protocol)
            
            // 더미 연결 시뮬레이션
            System.Threading.Thread.Sleep(100)  // 연결 지연 시뮬레이션
            
            if host = "fail" then  // 테스트용 실패 케이스
                failwith "Simulated connection failure"
            
            isConnected <- true
            logger.LogInformation("Successfully connected to LS Electric PLC {PlcId}", plcId)
            true
            
        with
        | ex ->
            logger.LogError(ex, "Failed to connect to LS Electric PLC {PlcId}", plcId)
            false
    
    override this.DisconnectImpl() =
        try
            logger.LogInformation("Disconnecting from LS Electric PLC {PlcId}", plcId)
            
            // 더미 연결 해제 시뮬레이션
            System.Threading.Thread.Sleep(50)
            
            isConnected <- false
            logger.LogInformation("Successfully disconnected from LS Electric PLC {PlcId}", plcId)
            
        with
        | ex ->
            logger.LogError(ex, "Error during disconnection from LS Electric PLC {PlcId}", plcId)
    
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
            logger.LogError(ex, "Failed to read tag {TagName} from LS Electric PLC {PlcId}", tag.Name, plcId)
            reraise()
    
    override this.HealthCheckImpl() =
        try
            if not isConnected then
                false
            else
                // 더미 헬스 체크 시뮬레이션
                System.Threading.Thread.Sleep(20)
                
                // 90% 확률로 건강함
                let isHealthy = random.Next(10) < 9
                logger.LogTrace("Health check for LS Electric PLC {PlcId}: {IsHealthy}", plcId, isHealthy)
                
                isHealthy
            
        with
        | ex ->
            logger.LogError(ex, "Health check failed for LS Electric PLC {PlcId}", plcId)
            false
    
    /// LS Electric 특화 기능 - CPU 타입 읽기
    member this.ReadCpuType() =
        if not isConnected then
            failwith "PLC is not connected"
        
        try
            System.Threading.Thread.Sleep(50)
            
            let cpuTypes = [| "XGK"; "XGI"; "XGR"; "XGB"; "XBC" |]
            let cpuType = cpuTypes.[random.Next(cpuTypes.Length)]
            
            logger.LogInformation("LS Electric PLC {PlcId} CPU type: {CpuType}", plcId, cpuType)
            cpuType
            
        with
        | ex ->
            logger.LogError(ex, "Failed to read CPU type from LS Electric PLC {PlcId}", plcId)
            reraise()
    
    /// LS Electric 특화 기능 - 스캔 타임 읽기
    member this.ReadScanTime() =
        if not isConnected then
            failwith "PLC is not connected"
        
        try
            System.Threading.Thread.Sleep(30)
            
            let scanTime = random.NextDouble() * 10.0 + 1.0  // 1-11ms
            
            logger.LogInformation("LS Electric PLC {PlcId} scan time: {ScanTime:F2}ms", plcId, scanTime)
            scanTime
            
        with
        | ex ->
            logger.LogError(ex, "Failed to read scan time from LS Electric PLC {PlcId}", plcId)
            reraise()