namespace PLC.CodeGen.LS

open Dual.Common.Core.FS
open System.Xml
open System.Net
open System.Collections.Generic
open AddressConvert
open Dual.Common.Core.FS

[<AutoOpen>]
module FileRead =
    /// XG5000 을 프로젝트 파일을 Xml로 저장한 후 DS로 열었을때 가져온는 속성
    type XmlXG5000(filePath, cpu, ip, ioCard, globalVars, directVars, dicMaxDevice, rungs, dicDeviceFlag) =
        member x.FilePath = filePath
        member x.Cpu = cpu
        member x.Ip = ip
        member x.IOCard = ioCard
        member x.GlobalVars = globalVars
        member x.DirectVars = directVars
        member x.DicMaxDevice = dicMaxDevice // 주소 타입별 Max word size를 저장 Dict<deviceHead, MaxSize:int>
        member x.Rungs = rungs
        member x.DicDeviceFlag = dicDeviceFlag //System Device영역중 F영역만 불러온다

    and ModuleBase(Id, plcBase, plcSlot, comment) =
        member x.Id = Id: int
        member x.PlcBase = plcBase
        member x.PlcSlot = plcSlot
        member x.Comment = comment: string

    and ModuleIO(Id, plcBase, plcSlot, comment, pointIn, pointOut) =
        inherit ModuleBase(Id, plcBase, plcSlot, comment)
        member x.PointIn = pointIn //max Input Size  (0일 경우 Input 사용안함)
        member x.PointOut = pointOut //max Output Size (0일 경우 Output 사용안함)

    and ModuleEthernet(Id, plcBase, plcSlot, comment, stationNo, ip) =
        inherit ModuleBase(Id, plcBase, plcSlot, comment)
        member x.StationNo = stationNo
        member x.Ip = ip


    let getIpCpuEthernet (xdoc: XmlDocument) =
        xdoc.SelectNodes(xmlCnfPath + "/Parameters/Parameter/Safety_Comm")
        |> XmlExt.ToEnumerables
        |> Seq.map (fun v ->
            let intIp = v.Attributes.GetNamedItem("IPAddress").InnerText
            let addressSplit = IPAddress.Parse(intIp).ToString().Split('.')

            let ip =
                sprintf "%s.%s.%s.%s" addressSplit.[3] addressSplit.[2] addressSplit.[1] addressSplit.[0]

            ModuleEthernet(0, 0, 0, "Ethernet CPU 일체형", 0, ip) //CPU에 장착된 Ethernet 은 ip 외 정보는 0 으로 할당
        )

    let getIpFenet (xdoc: XmlDocument) =
        let FenetNodes1 =
            xdoc.SelectNodes(xmlCnfPath + "/XGPD/XGPD_CONFIG_INFO_GROUP/XGPD_CONFIG_INFO_FENET")
            |> XmlExt.ToEnumerables

        let FenetNodes2 =
            xdoc.SelectNodes(
                xmlCnfPath
                + "/XGPD/XGPD_CONFIG_INFO_GROUP/XGPD_CONFIG_INFO_FENET_XBCUXECU/XGPD_CONFIG_INFO_FENET"
            )
            |> XmlExt.ToEnumerables

        FenetNodes1 @ FenetNodes2
        |> Seq.map (fun v ->
            let intIpA = v.Attributes.GetNamedItem("IpAddr_0").InnerText
            let intIpB = v.Attributes.GetNamedItem("IpAddr_1").InnerText
            let intIpC = v.Attributes.GetNamedItem("IpAddr_2").InnerText
            let intIpD = v.Attributes.GetNamedItem("IpAddr_3").InnerText
            let stationNo = v.Attributes.GetNamedItem("StationNo").InnerText |> int
            let typeId = v.Attributes.GetNamedItem("Type").InnerText |> int
            let baseE = v.Attributes.GetNamedItem("Base").InnerText |> int
            let slot = v.Attributes.GetNamedItem("Slot").InnerText |> int
            let ip = sprintf "%s.%s.%s.%s" intIpA intIpB intIpC intIpD
            ModuleEthernet(typeId, baseE, slot, "", stationNo, ip))

    let getModules (xdoc: XmlDocument) =
        let parameterModule = xdoc.SelectNodes(xmlCnfPath + "/Parameters/Parameter/Module")

        let modules =
            parameterModule
            |> XmlExt.ToEnumerables
            |> Seq.map (fun e ->
                e.Attributes.["Id"].InnerText |> int,
                e.Attributes.["Base"].InnerText |> int,
                e.Attributes.["Slot"].InnerText |> int)

        modules

    let getCpu (xdoc: XmlDocument) =
        let configuration = xdoc.SelectSingleNode(xmlCnfPath)
        let cpuType = configuration.Attributes.["Type"].InnerText
        cpuType

    let getGlobalVarXGI (xdoc: XmlDocument) =
        let globals = xdoc.SelectSingleNode(xmlCnfPath + "/GlobalVariables/GlobalVariable")
        let numGlobals = globals.Attributes.["Count"].Value |> System.Int32.Parse

        globals.SelectNodes("//Symbols/Symbol")
        |> XmlExt.ToEnumerables
        |> Seq.map (fun e ->
            e.Attributes.["Name"].InnerText,
            e.Attributes.["Kind"].InnerText |> int,
            e.Attributes.["Type"].InnerText,
            e.Attributes.["InitValue"].InnerText,
            e.Attributes.["Address"].InnerText,
            e.Attributes.["Comment"].InnerText,
            e.Attributes.["Device"].InnerText)
        |> Seq.map (fun (name, kind, plctype, initValue, address, comment, device) ->
            { Name = name
              Comment = comment
              Device = device
              Kind = kind
              Type = plctype
              InitValue = initValue
              State = 0
              Address = address
              DevicePos = 0
              AddressIEC = address })


    let getAddressXGK (devices, devicePos, devType, dicMaxDevice: IDictionary<string, int>) =

        let getBitIEC devices devicePos = sprintf "%%%sX%d" devices devicePos
        let getWordIEC devices devicePos = sprintf "%%%sW%d" devices devicePos

        let getAddressBit (dHead, devicePos) =
            match dicMaxDevice.[dHead].ToString().length () with
            | 3 ->
                if (dHead = "D" || dHead = "R") then
                    sprintf "%s%03d.%X" devices (devicePos / 16) (devicePos % 16), getBitIEC devices devicePos
                else
                    sprintf "%s%03d%X" devices (devicePos / 16) (devicePos % 16), getBitIEC devices devicePos
            | 4 ->
                if (dHead = "D" || dHead = "R") then
                    sprintf "%s%04d.%X" devices (devicePos / 16) (devicePos % 16), getBitIEC devices devicePos
                else
                    sprintf "%s%04d%X" devices (devicePos / 16) (devicePos % 16), getBitIEC devices devicePos
            | 5 ->
                if (dHead = "D" || dHead = "R") then
                    sprintf "%s%05d.%X" devices (devicePos / 16) (devicePos % 16), getBitIEC devices devicePos
                else
                    sprintf "%s%05d%X" devices (devicePos / 16) (devicePos % 16), getBitIEC devices devicePos
            | 6 ->
                if (dHead = "D" || dHead = "R") then
                    sprintf "%s%06d.%X" devices (devicePos / 16) (devicePos % 16), getBitIEC devices devicePos
                else
                    sprintf "%s%06d%X" devices (devicePos / 16) (devicePos % 16), getBitIEC devices devicePos
            | _ -> failwithf "[%s][%d] 예외 디바이스 타입." dHead (dicMaxDevice.[dHead].ToString().length ())

        let getAddressWord (dHead, devicePos) =
            match dicMaxDevice.[dHead].ToString().length () with
            | 3 -> sprintf "%s%03d" devices devicePos, getWordIEC devices devicePos
            | 4 -> sprintf "%s%04d" devices devicePos, getWordIEC devices devicePos
            | 5 -> sprintf "%s%05d" devices devicePos, getWordIEC devices devicePos
            | 6 -> sprintf "%s%06d" devices devicePos, getWordIEC devices devicePos
            | _ -> failwithf "[%s][%d] 예외 디바이스 타입." dHead (dicMaxDevice.[dHead].ToString().length ())

        match devType with
        | "BIT" ->
            match devices with
            | "S" ->
                sprintf "%s%03d.%02d" devices (devicePos / 100) (devicePos % 100),
                sprintf "%%%s%03d.%02d" devices (devicePos / 100) (devicePos % 100)
            | "U" ->
                sprintf "%s%02d.%02d.%X" devices (devicePos / 16 / 32) (devicePos / 16 % 32) (devicePos % 16),
                sprintf "%%%s%02d.%02d.%d" devices (devicePos / 16 / 32) (devicePos / 16 % 32) (devicePos % 16)
            | _ -> getAddressBit (devices, devicePos)

        | "BIT/WORD" -> getAddressWord (devices, devicePos) //T, C 타입들
        | "WORD"
        | "DWORD" -> //word, dword
            match devices with
            | "S" -> sprintf "%s%03d" devices devicePos, sprintf "%%%s%03d" devices devicePos
            | "U" ->
                sprintf "%s%02d.%02d" devices (devicePos / 32) (devicePos % 32),
                sprintf "%%%s%02d.%02d" devices (devicePos / 32) (devicePos % 32)
            | _ -> getAddressWord (devices, devicePos)
        | _ -> failwithf "[%s] 예외 디바이스 타입." devType


    let getGlobalVarXGKnB (xdoc: XmlDocument, dicMaxDevice: IDictionary<string, int>) =
        let globals = xdoc.SelectSingleNode(xmlCnfPath + "/GlobalVariables/VariableComment")

        let numGlobals =
            globals.SelectSingleNode("Symbols").Attributes.["Count"].Value |> int

        globals.SelectNodes("//Symbols/Symbol")
        |> XmlExt.ToEnumerables
        |> Seq.map (fun e ->
            e.Attributes.["Name"].InnerText,
            e.Attributes.["Device"].InnerText,
            e.Attributes.["InitValue"].InnerText,
            e.Attributes.["DevicePos"].InnerText |> int,
            e.Attributes.["Type"].InnerText,
            e.Attributes.["Comment"].InnerText)
        |> Seq.map (fun (name, device, initValue, devicePos, devType, comment) ->
            let address, addressIEC = getAddressXGK (device, devicePos, devType, dicMaxDevice)

            { Name = name
              Comment = comment
              Device = device
              Kind = 0
              InitValue = initValue
              Type = devType
              State = 0
              Address = address
              DevicePos = devicePos
              AddressIEC = addressIEC })

    let getDirectVarXGI (xdoc: XmlDocument, cpuType) =
        let usingDirectVar (xdoc: XmlDocument) =
            xdoc.SelectSingleNode(xmlCnfPath + "/GlobalVariables/GlobalVariable/DirectVarComment")
            <> null

        if (usingDirectVar xdoc) then
            let globals =
                xdoc.SelectSingleNode(xmlCnfPath + "/GlobalVariables/GlobalVariable/DirectVarComment")

            let numGlobals = globals.Attributes.["Count"].Value |> System.Int32.Parse

            let getType (data) =
                match tryParseTag (cpuType) data with
                | Some v -> v.DataType.Totext()
                | None -> ""

            globals.SelectNodes("//DirectVar")
            |> XmlExt.ToEnumerables
            |> Seq.map (fun e ->
                e.Attributes.["Device"].InnerText,
                e.Attributes.["Comment"].InnerText,
                getType (e.Attributes.["Device"].InnerText))
        else
            Seq.empty


    /// XmlXG5000 타입으로 XML 정보를 불러온다.
    let getXml (fileName: string) =
        let newFile = fileName
        // let newFile = if(fileName = "") then testSampleXGI else fileName
        let xdoc = newFile |> DsXml.load

        let modules = getModules xdoc
        let cpuId = getCpu xdoc |> int
        let cpu = readConfigCPU () |> Seq.filter (fun f -> f.nPLCID = cpuId) |> Seq.head

        let findCnf (id: int) =
            readConfigIO ()
            |> Seq.tryFind (fun f -> f.HwID = id)
            |> Option.map (fun f -> f.NRefreshIn, f.NRefreshOut, f.Comments)

        let ioCards =
            modules
            |> Seq.map (fun (Id, pBase, pSlot) ->
                match findCnf (Id) with
                | Some(nRefreshIn, nRefreshOut, comments) ->
                    ModuleIO(Id, pBase, pSlot, comments, nRefreshIn, nRefreshOut)
                | _ -> ModuleIO(Id, pBase, pSlot, "카드정보가 없습니다", 0, 0))

        let ethernetIPs = getIpFenet xdoc @ getIpCpuEthernet xdoc

        let dicMaxDevice =
            readConfigDevice ()
            |> Seq.filter (fun f -> f.nPLCID = cpuId)
            |> Seq.map (fun m -> m.strDevice, m.nSize)
            |> dict

        let globalVars =
            match CpuType.FromID(cpu.nPLCID) with
            | Xgi
            | XgbIEC -> getGlobalVarXGI (xdoc)
            | Xgk
            | XgbMk -> getGlobalVarXGKnB (xdoc, dicMaxDevice)
            | _ -> failwithf "[%s] 해당 CPU 기종은 아직 지원하지 않습니다." cpu.strPLCType // 나머지 기종 테스트 필요

        //XGI만 directVars 있는듯..
        let directVarsXGI = getDirectVarXGI (xdoc, CpuType.FromID(cpu.nPLCID))
        let rungs = getRungs (xdoc, CpuType.FromID(cpu.nPLCID), dicMaxDevice)

        let dicDeviceFlag =
            readConfigFlag ()
            |> Seq.filter (fun f -> f.strDevice = "F")
            |> Seq.map (fun m ->
                (getAddressXGK (m.strDevice, m.nDevicePos, m.strType, dicMaxDevice)
                 |> fun (address, addressiec) -> address),
                m)
            |> dict


        XmlXG5000(newFile, cpu, ethernetIPs, ioCards, globalVars, directVarsXGI, dicMaxDevice, rungs, dicDeviceFlag)
