namespace Ev2.PLC.Common.Types

open System

// ===================================
// Domain Modeling - Vendor and Protocol Types
// ===================================

/// LS Electric 모델
type LSElectricModel = 
    // XGI Series
    | XGI_CPUE
    | XGI_CPUH
    | XGI_CPUS
    | XGI_CPUU
    | XGI_CPUUN
    | XGI_CPUU_D
    // XGK Series  
    | XGK_CPUA
    | XGK_CPUE
    | XGK_CPUH
    | XGK_CPUHN
    | XGK_CPUS
    | XGK_CPUSN
    | XGK_CPUU
    | XGK_CPUUN
    // XGB Series
    | XGB_DR16C
    | XGB_GIPAM
    | XGB_KL
    | XGB_XBCE
    | XGB_XBCH
    | XGB_XBCS
    | XGB_XBCU
    | XGB_XBCXS
    | XGB_XBMH
    | XGB_XBMH2
    | XGB_XBMHP
    | XGB_XBMS
    // XGR Series
    | XGR_CPUH
    // Other series
    | XMC
    | Master_K
    | GLOFA
    | Custom of string
    
    member this.IsXGI =
        match this with
        | XGI_CPUE | XGI_CPUH | XGI_CPUS 
        | XGI_CPUU | XGI_CPUUN | XGI_CPUU_D -> true
        | _ -> false


/// Mitsubishi 모델
type MitsubishiModel =
    | FX3U
    | FX3G
    | FX5U
    | Q_Series
    | L_Series
    | R_Series
    | IQ_F
    | IQ_R
    | Custom of string
    
/// Siemens 모델
type SiemensModel =
    | S7_200
    | S7_200_SMART
    | S7_300
    | S7_400
    | S7_1200
    | S7_1500
    | LOGO_8
    | ET200SP
    | Custom of string
    

/// Allen-Bradley 모델
type AllenBradleyModel =
    | ControlLogix
    | CompactLogix
    | MicroLogix
    | Micro800
    | PLC5
    | SLC500
    | FlexLogix
    | Custom of string
    

/// 범용 PLC 모델
type GenericModel = GenericModel of string with
    static member Create(model: string) =
        let trimmed = model.Trim()
        if String.IsNullOrWhiteSpace trimmed then 
            None 
        else 
            Some(GenericModel trimmed)
    
    member this.DisplayName = 
        let (GenericModel value) = this
        value

/// LS Electric 전용 프로토콜
type LSElectricProtocol = 
    | XGT_Server
    | XGT_Client  
    | Cnet
    | FastEnet
    
    member this.Name =
        match this with
        | XGT_Server -> "XGT Server"
        | XGT_Client -> "XGT Client"
        | Cnet -> "Cnet"
        | FastEnet -> "Fast Enet"
    
    member this.DefaultPort =
        match this with
        | XGT_Server -> 2004
        | XGT_Client -> 2005
        | Cnet -> 2003
        | FastEnet -> 2006

/// Mitsubishi 전용 프로토콜
type MitsubishiProtocol = 
    | MC_3E  // MC Protocol 3E Frame
    | MC_4E  // MC Protocol 4E Frame
    | MC_1E  // MC Protocol 1E Frame
    | SLMP   // Seamless Message Protocol
    | CC_Link
    | CC_Link_IE
    
    member this.Name =
        match this with
        | MC_3E -> "MC Protocol 3E"
        | MC_4E -> "MC Protocol 4E"
        | MC_1E -> "MC Protocol 1E"
        | SLMP -> "SLMP"
        | CC_Link -> "CC-Link"
        | CC_Link_IE -> "CC-Link IE"
    
    member this.DefaultPort =
        match this with
        | MC_3E | MC_4E -> 1280
        | MC_1E -> 1281
        | SLMP -> 1282
        | CC_Link -> 5010
        | CC_Link_IE -> 61450

/// Siemens 전용 프로토콜
type SiemensProtocol = 
    | S7_200
    | S7_300_400
    | S7_1200
    | S7_1500
    | Logo
    | Profinet
    
    member this.Name =
        match this with
        | S7_200 -> "S7-200"
        | S7_300_400 -> "S7-300/400"
        | S7_1200 -> "S7-1200"
        | S7_1500 -> "S7-1500"
        | Logo -> "LOGO!"
        | Profinet -> "PROFINET"
    
    member this.DefaultPort =
        match this with
        | S7_200 | S7_300_400 | S7_1200 | S7_1500 -> 102
        | Logo -> 102
        | Profinet -> 161

    member this.Rack =
        match this with
        | S7_200 | Logo -> 0
        | S7_300_400 | S7_1200 | S7_1500 -> 0
        | _ -> failwithf $"error Rack type {this}"
    
    member this.Slot =
        match this with
        | S7_200 | Logo -> 2
        | S7_300_400 -> 2
        | S7_1200 | S7_1500 -> 1
        | _ -> failwithf $"error Slot type {this}"

/// Allen-Bradley 전용 프로토콜
type AllenBradleyProtocol = 
    | EtherNetIP_PCCC      // Legacy PCCC over EtherNet/IP
    | EtherNetIP_Logix     // ControlLogix, CompactLogix
    | EtherNetIP_Micro800  // Micro800 series
    | CIP_Routing          // Common Industrial Protocol with routing
    | DF1                  // Serial protocol
    
    member this.Name =
        match this with
        | EtherNetIP_PCCC -> "EtherNet/IP PCCC"
        | EtherNetIP_Logix -> "EtherNet/IP Logix"
        | EtherNetIP_Micro800 -> "EtherNet/IP Micro800"
        | CIP_Routing -> "CIP with Routing"
        | DF1 -> "DF1 Serial"
    
    member this.DefaultPort =
        match this with
        | EtherNetIP_PCCC | EtherNetIP_Logix | EtherNetIP_Micro800 -> 44818
        | CIP_Routing -> 44818
        | DF1 -> 0 // Serial port

/// 범용 프로토콜 (Custom PLC용)
type GenericProtocol =
    | ModbusTCP
    | ModbusRTU
    | ModbusASCII
    | OPC_UA
    | TCP_IP
    | UDP
    
    member this.Name =
        match this with
        | ModbusTCP -> "Modbus TCP"
        | ModbusRTU -> "Modbus RTU"
        | ModbusASCII -> "Modbus ASCII"
        | OPC_UA -> "OPC UA"
        | TCP_IP -> "TCP/IP"
        | UDP -> "UDP"
    
    member this.DefaultPort =
        match this with
        | ModbusTCP -> 502
        | ModbusRTU | ModbusASCII -> 0 // Serial
        | OPC_UA -> 4840
        | TCP_IP -> 0 // Custom
        | UDP -> 0 // Custom

/// 통합 프로토콜 타입
type Protocol =
    | LSElectric of LSElectricProtocol
    | Mitsubishi of MitsubishiProtocol
    | Siemens of SiemensProtocol
    | AllenBradley of AllenBradleyProtocol
    | Generic of GenericProtocol
    
    member this.Name =
        match this with
        | LSElectric p -> p.Name
        | Mitsubishi p -> p.Name
        | Siemens p -> p.Name
        | AllenBradley p -> p.Name
        | Generic p -> p.Name
    
    member this.DefaultPort =
        match this with
        | LSElectric p -> p.DefaultPort
        | Mitsubishi p -> p.DefaultPort
        | Siemens p -> p.DefaultPort
        | AllenBradley p -> p.DefaultPort
        | Generic p -> p.DefaultPort

/// PLC 제조사 정보
type VendorInfo = {
    Name: string
    DefaultProtocol: Protocol
    SupportedProtocols: Protocol list
}

/// PLC 제조사 종류
type PlcVendor =
    | LSElectric of model: LSElectricModel option * protocol: LSElectricProtocol option
    | Mitsubishi of model: MitsubishiModel option * protocol: MitsubishiProtocol option
    | Siemens of model: SiemensModel option * protocol: SiemensProtocol option
    | AllenBradley of model: AllenBradleyModel option * protocol: AllenBradleyProtocol option
    | Custom of manufacturer: string * model: GenericModel option * protocol: GenericProtocol option

    // Factory methods with protocol selection
    static member CreateLSElectric(?model: LSElectricModel, ?protocol: LSElectricProtocol) = 
        LSElectric(model, protocol)
    
    static member CreateMitsubishi(?model: MitsubishiModel, ?protocol: MitsubishiProtocol) = 
        Mitsubishi(model, protocol)
    
    static member CreateSiemens(?model: SiemensModel, ?protocol: SiemensProtocol) = 
        Siemens(model, protocol)
    
    static member CreateAllenBradley(?model: AllenBradleyModel, ?protocol: AllenBradleyProtocol) = 
        AllenBradley(model, protocol)
    
    static member CreateCustom(manufacturer: string, ?model: string, ?protocol: GenericProtocol) =
        let name = 
            let trimmed = manufacturer.Trim()
            if String.IsNullOrWhiteSpace trimmed then "Custom" else trimmed
        Custom(name, model |> Option.bind GenericModel.Create, protocol)

    // Vendor information
    member private this.VendorInfo =
        match this with
        | LSElectric (_, protocol) -> 
            let defaultProtocol = protocol |> Option.defaultValue XGT_Server
            { Name = "LS Electric"
              DefaultProtocol = Protocol.LSElectric defaultProtocol
              SupportedProtocols = 
                [XGT_Server; XGT_Client; Cnet; FastEnet]
                |> List.map Protocol.LSElectric }
                
        | Mitsubishi (_, protocol) -> 
            let defaultProtocol = protocol |> Option.defaultValue MC_3E
            { Name = "Mitsubishi Electric"
              DefaultProtocol = Protocol.Mitsubishi defaultProtocol
              SupportedProtocols = 
                [MC_3E; MC_4E; MC_1E; SLMP; CC_Link; CC_Link_IE]
                |> List.map Protocol.Mitsubishi }
                
        | Siemens (_, protocol) -> 
            let defaultProtocol = protocol |> Option.defaultValue S7_1200
            { Name = "Siemens"
              DefaultProtocol = Protocol.Siemens defaultProtocol
              SupportedProtocols = 
                [S7_200; S7_300_400; S7_1200; S7_1500; Logo; Profinet]
                |> List.map Protocol.Siemens }
                
        | AllenBradley (_, protocol) -> 
            let defaultProtocol = protocol |> Option.defaultValue EtherNetIP_Logix
            { Name = "Allen-Bradley"
              DefaultProtocol = Protocol.AllenBradley defaultProtocol
              SupportedProtocols = 
                [EtherNetIP_PCCC; EtherNetIP_Logix; EtherNetIP_Micro800; CIP_Routing; DF1]
                |> List.map Protocol.AllenBradley }
                
        | Custom (name, _, protocol) -> 
            let defaultProtocol = protocol |> Option.defaultValue ModbusTCP
            { Name = name
              DefaultProtocol = Protocol.Generic defaultProtocol
              SupportedProtocols = 
                [ModbusTCP; ModbusRTU; ModbusASCII; OPC_UA; TCP_IP; UDP]
                |> List.map Protocol.Generic }

    // Public properties
    member this.Manufacturer = this.VendorInfo.Name
    member this.DefaultProtocol = this.VendorInfo.DefaultProtocol
    member this.SupportedProtocols = this.VendorInfo.SupportedProtocols
    member this.DefaultPort = this.DefaultProtocol.DefaultPort

    member this.Model =
        match this with
        | LSElectric (model, _) -> model |> Option.map (fun m -> m.ToString())
        | Mitsubishi (model, _) -> model |> Option.map (fun m -> m.ToString())
        | Siemens (model, _) -> model |> Option.map (fun m -> m.ToString())
        | AllenBradley (model, _) -> model |> Option.map (fun m -> m.ToString())
        | Custom (_, model, _) -> model |> Option.map (fun m -> m.ToString())

    member this.CurrentProtocol =
        match this with
        | LSElectric (_, Some p) -> Protocol.LSElectric p
        | Mitsubishi (_, Some p) -> Protocol.Mitsubishi p
        | Siemens (_, Some p) -> Protocol.Siemens p
        | AllenBradley (_, Some p) -> Protocol.AllenBradley p
        | Custom (_, _, Some p) -> Protocol.Generic p
        | _ -> this.DefaultProtocol

    member this.DisplayName =
        let protocolInfo = 
            if this.CurrentProtocol <> this.DefaultProtocol then
                $" [{this.CurrentProtocol.Name}]"
            else ""
        
        match this.Manufacturer, this.Model with
        | manufacturer, Some model -> $"{manufacturer} ({model}){protocolInfo}"
        | manufacturer, None -> $"{manufacturer}{protocolInfo}"