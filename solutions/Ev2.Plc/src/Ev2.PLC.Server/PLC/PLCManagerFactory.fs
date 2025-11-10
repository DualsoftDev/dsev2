namespace DSPLCServer.PLC

open Microsoft.Extensions.Logging
open DSPLCServer.Common
open DSPLCServer.Database

/// PLC 관리자 팩토리
module PLCManagerFactory =
    
    /// PLC 설정에서 PLC 관리자 생성
    let createManager (config: PLCConfiguration) (logger: ILogger) : IPLCManager =
        match config.Vendor with
        | LSElectric -> 
            new LSElectricManager(config.Id, config.ConnectionString, logger) :> IPLCManager
        | Mitsubishi -> 
            new MitsubishiManager(config.Id, config.ConnectionString, logger) :> IPLCManager
        | AllenBradley -> 
            new AllenBradleyManager(config.Id, config.ConnectionString, logger) :> IPLCManager
    
    /// 여러 PLC 설정에서 관리자들 생성
    let createManagers (configs: PLCConfiguration list) (loggerFactory: ILoggerFactory) : IPLCManager list =
        configs
        |> List.map (fun config ->
            let logger = loggerFactory.CreateLogger($"PLCManager.{config.Vendor}.{config.Id}")
            createManager config logger)
    
    /// 제조사별 기본 연결 문자열 생성 도우미
    let createConnectionString (vendor: PlcVendor) (host: string) (port: int option) (additionalParams: (string * string) list) =
        let baseConnectionString = $"Host={host}"
        let portString = port |> Option.map (fun p -> $";Port={p}") |> Option.defaultValue ""
        let paramsString = 
            additionalParams 
            |> List.map (fun (key, value) -> $";{key}={value}")
            |> String.concat ""
        
        match vendor with
        | LSElectric -> $"{baseConnectionString}{portString};Protocol=XGT{paramsString}"
        | Mitsubishi -> $"{baseConnectionString}{portString};Protocol=MC{paramsString}"
        | AllenBradley -> $"{baseConnectionString}{portString};Protocol=EIP{paramsString}"
    
    /// 지원되는 PLC 제조사 목록
    let getSupportedVendors() = [LSElectric; Mitsubishi; AllenBradley]
    
    /// 제조사별 기본 포트 반환
    let getDefaultPort (vendor: PlcVendor) =
        match vendor with
        | LSElectric -> 2004
        | Mitsubishi -> 1280 
        | AllenBradley -> 44818