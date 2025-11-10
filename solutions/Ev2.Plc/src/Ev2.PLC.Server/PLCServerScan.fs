namespace DSPLCServer

open System
open System.Collections.Generic
open Dual.PLC.Common.FS
open XgtProtocol
open Engine.Core
open Engine.Runtime
open Engine.Core.MapperDataModule
open RuntimeEnvModule
open RuntimeEventModule
open MelsecProtocol
open OpcProtocol

[<AutoOpen>]
module RuntimeScanModule =

    type XgtScanWrapper(mgr: DsScanManagerBase<XgtPlcScan>) =
        interface IDsScanManager with
            member _.ActiveIPs = mgr.ActiveIPs :> seq<_>
            member _.GetScanner(ip) = mgr.GetScanner(ip) |> Option.map (fun s -> s :> DsScanBase)
            member _.DisconnectAll() = mgr.StopAll()
        member _.ScanManager = mgr

    type MxScanWrapper(mgr: DsScanManagerBase<MxPlcScan>) =
        interface IDsScanManager with
            member _.ActiveIPs = mgr.ActiveIPs :> seq<_>
            member _.GetScanner(ip) = mgr.GetScanner(ip) |> Option.map (fun s -> s :> DsScanBase)
            member _.DisconnectAll() = mgr.StopAll()
        member _.ScanManager = mgr

    type OpcScanWrapper(scanner: OpcPlcScan) =
        interface IDsScanManager with
            member _.ActiveIPs = [| scanner.EndpointUrl |] :> seq<_>
            member _.GetScanner(ip) = 
                if ip = scanner.EndpointUrl then Some (scanner :> DsScanBase)
                else None
            member _.DisconnectAll() = scanner.ConnectionClose()
            
        member _.StartScan(ip, tags) =
            if ip = scanner.EndpointUrl then
                scanner.Scan(tags)
            else
                failwith $"[❌ OPC 스캔 오류] 잘못된 IP 주소입니다: {ip}"

    let private createAddressMap (env: RuntimeEnv) =
        let dictMap = Dictionary<string, ResizeArray<TagInfo>>()
        ExportConfigsMoudle.getDsActionInterfaces(env.DsSystem)
        |> Seq.filter (fun tag -> tag.Address <> DsText.TextAddrEmpty && tag.Address <> DsText.TextNotUsed)
        |> Seq.iter (fun tag ->
            let pair = 
                {
                    Name = tag.Name
                    Address =  if env.ModelConfig.PcControlByOPC then OpcTagParser.getOpcNodeId(tag.Name) else tag.Address
                    DataType  = PlcDataSizeType.TryFromString tag.DataType
                    Comment = ""
                    IsLowSpeedArea = false
                    IsOutput = tag.DeviceType = IOType.Out
                }

            if dictMap.ContainsKey(tag.Address) then
                dictMap.[tag.Address].Add(pair)
            else
                dictMap.[tag.Address] <- ResizeArray [pair])

        for tag in env.UserMonitorTags do
            let pair = 
                {
                    Name = tag.Name
                    Address = tag.Address
                    DataType  = PlcDataSizeType.TryFromString tag.DataType
                    Comment = ""
                    IsLowSpeedArea = false
                    IsOutput = false
                }
            if dictMap.ContainsKey(tag.Address) then
                dictMap.[tag.Address].Add(pair)
            else
                dictMap.[tag.Address] <- ResizeArray [pair]

        dictMap

    /// 범용 PLC 스캔 시작
    let private scanGenericPlc
        (env: RuntimeEnv)
        (state: RuntimeScanState)
        (scanManager: IDsScanManager)
        =
        let tagInfos =
            state.DicAddressStg
            |> Seq.map (fun kv -> kv.Value |> Seq.head)
            |> Seq.toArray

        // 태그 등록
        state.DsScanTags <-
            match scanManager with
            | :? XgtScanWrapper as wrapper -> wrapper.ScanManager.StartScan(env.ModelConfig.HwIP, tagInfos)
            | :? MxScanWrapper as wrapper -> wrapper.ScanManager.StartScan(env.ModelConfig.HwIP, tagInfos)
            | :? OpcScanWrapper as wrapper -> wrapper.StartScan(env.ModelConfig.HwOPC, tagInfos)
            | _ -> failwith "Invalid scan manager"

        // 변경 알림 등록
        let connPath  = if scanManager :? OpcScanWrapper 
                        then env.ModelConfig.HwOPC 
                        else env.ModelConfig.HwIP

        match scanManager.GetScanner(connPath) with
        | Some scanner ->
                if env.ModelConfig.PcControlByOPC then
                    scanner.TagValueChangedNotify.AddHandler(fun _ e -> RuntimeEventModule.handleOPCScanTagChanged env state e)
                else 
                    scanner.TagValueChangedNotify.AddHandler(fun _ e -> RuntimeEventModule.handlePLCScanTagChanged env state e)
                    
        | None -> ()


    let scanDsIO (env: RuntimeEnv) : RuntimeScanState =
        let dictMap = createAddressMap env
        let isMonitorOnly = env.ModelConfig.RuntimeMode.IsMonitorOnlyMode()

        let state = {
            ScanManager = None
            DsScanTags = dict [] :> IDictionary<_, _>
            DicAddressStg = dictMap
        }

        match env.ModelConfig.HwTarget.HwIO with
        | HwIO.LS_XGI_IO | HwIO.LS_XGK_IO ->
            let mgr = XgtScanManager(env.ModelConfig.HwLocalCpuEthernet, -1, 3000, isMonitorOnly)
            let wrapper = XgtScanWrapper(mgr)
            state.ScanManager <- Some (wrapper :> IDsScanManager)
            scanGenericPlc env state wrapper
            state

        | HwIO.MELSEC_IO ->
            let mgr = MxScanManager(-1, 3000, isMonitorOnly, env.ModelConfig.HwPort, env.ModelConfig.HwEthernet = UDP)
            let wrapper = MxScanWrapper(mgr)
            state.ScanManager <- Some (wrapper :> IDsScanManager)
            scanGenericPlc env state wrapper
            state

        | HwIO.OPC_IO ->
            let timeoutMs = 3000
            
            // OPC 연결 설정 생성
            let opcConfig = {
                EndpointUrl = env.ModelConfig.HwOPC
                SecurityMode = Opc.Ua.MessageSecurityMode.None
                SecurityPolicy = ""//"http://opcfoundation.org/UA/SecurityPolicy#None"
                Username = None
                Password = None
                Timeout = timeoutMs
            }
            
            // OPC 스캐너 생성 및 연결
            let opcScanner = new OpcPlcScan(opcConfig, 50, isMonitorOnly)

            let wrapper = OpcScanWrapper(opcScanner)
            state.ScanManager <- Some (wrapper :> IDsScanManager)
            scanGenericPlc env state wrapper
            state


    let unsubscribeAll (state: RuntimeScanState) =
        match state.ScanManager with
        | Some mgr ->
            mgr.DisconnectAll()
            state.ScanManager <- None
        | None -> ()
        
  