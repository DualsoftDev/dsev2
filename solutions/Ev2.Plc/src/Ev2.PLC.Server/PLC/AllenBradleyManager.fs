namespace DSPLCServer.PLC

open System
open Microsoft.Extensions.Logging
open DSPLCServer.Common
open DSPLCServer.Database

/// Allen-Bradley PLC 관리자 (더미 구현)
type AllenBradleyManager(plcId: string, connectionString: string, logger: ILogger) =
    inherit PLCManagerBase(plcId, PlcVendor.AllenBradley, connectionString, logger)
    
    let mutable isConnected = false
    let random = Random()
    
    /// 연결 문자열 파싱
    member private this.ParseConnectionString() =
        // 예: Host=192.168.1.100;Port=44818;Slot=0;Timeout=5000
        let parts = connectionString.Split(';')
        let paramDict = 
            parts 
            |> Array.choose (fun part ->
                let kvp = part.Split('=')
                if kvp.Length = 2 then Some (kvp.[0].Trim(), kvp.[1].Trim())
                else None)
            |> dict
        
        let host = paramDict.TryGetValue("Host") |> fun (found, value) -> if found then value else "127.0.0.1"
        let port = paramDict.TryGetValue("Port") |> fun (found, value) -> if found then int value else 44818
        let slot = paramDict.TryGetValue("Slot") |> fun (found, value) -> if found then int value else 0
        let timeout = paramDict.TryGetValue("Timeout") |> fun (found, value) -> if found then int value else 5000
        
        (host, port, slot, timeout)
    
    /// 태그 주소 유효성 검증 (Allen-Bradley 형식)
    member private this.ValidateTagAddress(address: string) =
        // Allen-Bradley 주소 형식: Controller.Tags.MyTag, Local:2:I.Data.0 등
        address.Length > 0 && not (address.Contains(" "))
    
    /// 더미 값 생성
    member private this.GenerateDummyValue(dataType: TagDataType) =
        match dataType with
        | Bool -> BoolValue (random.Next(2) = 1)
        | Int16 | Int32 | Int64 -> IntValue (int64 (random.Next(-65536, 65535)))
        | Float32 | Float64 -> FloatValue (random.NextDouble() * 2000.0 - 1000.0)
        | String -> StringValue $"AB_Tag_{random.Next(1000)}"
        | Bytes -> 
            let size = random.Next(1, 10)
            BytesValue (Array.init size (fun _ -> byte (random.Next(256))))
    
    override this.ConnectImpl() =
        try
            let (host, port, slot, timeout) = this.ParseConnectionString()
            logger.LogInformation("Connecting to Allen-Bradley PLC at {Host}:{Port} (Slot: {Slot})", host, port, slot)
            
            // 더미 연결 시뮬레이션
            System.Threading.Thread.Sleep(200)  // Allen-Bradley는 연결이 가장 오래 걸림
            
            if host = "fail" then
                failwith "Simulated connection failure"
            
            isConnected <- true
            logger.LogInformation("Successfully connected to Allen-Bradley PLC {PlcId}", plcId)
            true
            
        with
        | ex ->
            logger.LogError(ex, "Failed to connect to Allen-Bradley PLC {PlcId}", plcId)
            false
    
    override this.DisconnectImpl() =
        try
            logger.LogInformation("Disconnecting from Allen-Bradley PLC {PlcId}", plcId)
            
            // 더미 연결 해제 시뮬레이션
            System.Threading.Thread.Sleep(75)
            
            isConnected <- false
            logger.LogInformation("Successfully disconnected from Allen-Bradley PLC {PlcId}", plcId)
            
        with
        | ex ->
            logger.LogError(ex, "Error during disconnection from Allen-Bradley PLC {PlcId}", plcId)
    
    override this.ReadTagImpl(tag: TagConfiguration) =
        if not isConnected then
            failwith "PLC is not connected"
        
        if not (this.ValidateTagAddress(tag.Address)) then
            failwith $"Invalid tag address: {tag.Address}"
        
        try
            // 더미 읽기 시뮬레이션
            System.Threading.Thread.Sleep(random.Next(15, 60))
            
            let value = this.GenerateDummyValue(tag.DataType)
            logger.LogTrace("Read tag {TagName} ({Address}) = {Value}", tag.Name, tag.Address, value.ToString())
            
            value
            
        with
        | ex ->
            logger.LogError(ex, "Failed to read tag {TagName} from Allen-Bradley PLC {PlcId}", tag.Name, plcId)
            reraise()
    
    override this.HealthCheckImpl() =
        try
            if not isConnected then
                false
            else
                // 더미 헬스 체크 시뮬레이션
                System.Threading.Thread.Sleep(25)
                
                // 95% 확률로 건강함 (Allen-Bradley는 가장 안정적)
                let isHealthy = random.Next(20) < 19
                logger.LogTrace("Health check for Allen-Bradley PLC {PlcId}: {IsHealthy}", plcId, isHealthy)
                
                isHealthy
            
        with
        | ex ->
            logger.LogError(ex, "Health check failed for Allen-Bradley PLC {PlcId}", plcId)
            false