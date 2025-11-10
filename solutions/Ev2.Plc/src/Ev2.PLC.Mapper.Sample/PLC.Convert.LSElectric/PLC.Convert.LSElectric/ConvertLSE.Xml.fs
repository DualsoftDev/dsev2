namespace PLC.Convert.LSElectric

open Dual.Common.Core.FS
open System
open System.Xml
open System.Text.RegularExpressions

open XgxBase
open System.Collections
open System.Collections.Generic

module XgxXml =

    [<Literal>]
    let private globalVarPath = "Project/Configurations/Configuration/GlobalVariables/GlobalVariable"

    /// XML 노드에서 속성을 안전하게 가져오기
    let tryGetAttribute (node: XmlNode) (attr: string) =
        if isNull node || isNull node.Attributes || isNull node.Attributes.[attr] then "" 
        else node.Attributes.[attr].Value

    /// XmlNode에서 속성을 안전하게 추출한 뒤 IP 문자열로 변환
    let tryGetEFMTBIp (node: XmlNode) (attrPrefix: string) =
        let getIpByte idx = 
            match tryGetAttribute node (attrPrefix + idx.ToString()) with
            | "" -> 0uy
            | raw -> match Byte.TryParse(raw) with
                     | true, value -> value
                     | _ -> 0uy

        // 각 IP 주소의 4개 바이트를 가져옵니다.
        let b1 = getIpByte 0
        let b2 = getIpByte 1 
        let b3 = getIpByte 2
        let b4 = getIpByte 3

        // IP 주소를 점으로 구분하여 출력합니다.
        $"{b1}.{b2}.{b3}.{b4}"

    /// XmlNode에서 속성을 안전하게 추출한 뒤 IP 문자열로 변환
    let tryGetIp (node: XmlNode) (attr: string) =
        let raw = tryGetAttribute node attr
        match UInt32.TryParse(raw) with
        | true, ipInt ->
            let b1 = byte (ipInt &&& 0xFFu)
            let b2 = byte ((ipInt >>> 8) &&& 0xFFu)
            let b3 = byte ((ipInt >>> 16) &&& 0xFFu)
            let b4 = byte ((ipInt >>> 24) &&& 0xFFu)
            $"{b1}.{b2}.{b3}.{b4}"
        | _ -> "0.0.0.0"


    /// XG5000 XGT 타입 여부 확인
    let IsXg5kXGT (xmlPath: string) =
        let doc = DualXmlDocument.loadFromFile xmlPath
        doc.GetXmlNode("//Configurations/Configuration/Parameters/Parameter/XGTBasicParam") <> null

    /// Global 변수 내 Symbol 노드 리스트
    let getGlobalSymbolXmlNodes (doc: XmlDocument) =
        doc.SelectNodes($"{globalVarPath}/Symbols/Symbol")

    /// Global 변수 내 DirectVar 노드 리스트
    let getDirectVarXmlNodes (doc: XmlDocument) =
        doc.SelectNodes($"{globalVarPath}/DirectVarComment/DirectVar")


type XmlReader =

    static member ReadTags(xmlPath: string, ?usedOnly: bool) : PlcTagInfo[] * string array =
        let usedOnly = defaultArg usedOnly true
        let xdoc: XmlDocument = DualXmlDocument.loadFromFile xmlPath
        let addrPattern = Regex("^%(?<iom>[IQM])(?<size>[XBW])", RegexOptions.Compiled)

            // IP 주소 추출
        let ipNode = xdoc.SelectSingleNode("//Parameter[@Type='FENET PARAMETER']/Safety_Comm")
        let ip = XgxXml.tryGetIp ipNode "IPAddress"

      // XGPD_CONFIG_INFO_FENET에서 복수의 서브 IP 추출
        let ipSubNodes = xdoc.SelectNodes("//XGPD_CONFIG_INFO_FENET")
        let subIps = 
            ipSubNodes 
            |> _.ToEnumerables()
            |> Seq.map (fun node -> XgxXml.tryGetEFMTBIp node "IpAddr_") // "IpAddr_0", "IpAddr_1", "IpAddr_2", "IpAddr_3" 필드에서 서브 IP 추출
            |> Seq.toArray

        // GlobalSymbol → PlcTagInfo
        let parseGlobal (node: XmlNode) =
            let address = XgxXml.tryGetAttribute node "Address"
            let outputFlag =
                match XgxXml.tryGetAttribute node "ModuleInfo" with 
                | s when s <> "" -> s.Contains "OUT"
                | _ -> address.StartsWith("%Q")
                
            PlcTagInfo(
                typ      = "Tag",
                scope    = "GlobalVariable",
                variable = XgxXml.tryGetAttribute node "Name",
                dataType = XgxXml.tryGetAttribute node "Type",
                comment  = XgxXml.tryGetAttribute node "Comment",
                address  = address,
                outputFlag = outputFlag
            )

        let _DirectVarNames = Dictionary<String, PlcTagInfo>(); 
        // DirectVar → PlcTagInfo option
        let parseDirect (node: XmlNode) =
            let used = XgxXml.tryGetAttribute node "Used"
            let device = XgxXml.tryGetAttribute node "Device"
            let comment = XgxXml.tryGetAttribute node "Comment"

            if device <> "" && comment <> "" && not usedOnly || used = "1" then
                let dataType =
                    match addrPattern.Match(device) with
                    | m when m.Success ->
                        match m.Groups.["size"].Value with
                        | "X" -> "BOOL"
                        | "B" -> "BYTE"
                        | "W" -> "WORD"
                        | "D" -> "DWORD"
                        | "L" -> "LWORD"
                        | unknown -> failwithf "Unknown data type: %s" unknown
                    | _ -> ""

                let uniqName  = if _DirectVarNames.ContainsKey comment
                                then $"{comment}_{device}" 
                                else comment

                let directVar= Some (PlcTagInfo(
                    typ      = "Tag",
                    scope    = "DirectVar",
                    variable = uniqName,
                    address  = device,
                    dataType = dataType,
                    comment  = comment,
                    outputFlag = device.StartsWith("%Q")
                ))

                _DirectVarNames.Add(directVar.Value.Variable, directVar.Value);
                directVar

            else None

        // 전체 태그 수집
        let tags =
            [|
                for node in XgxXml.getGlobalSymbolXmlNodes xdoc |> _.ToEnumerables() do
                    yield parseGlobal node

                for node in XgxXml.getDirectVarXmlNodes xdoc |> _.ToEnumerables() do
                    match parseDirect node with
                    | Some tag -> yield tag
                    | None     -> ()
            |]

        // 반환: 태그 + 마스터 IP, 서브 IP
        tags, [|ip|]@subIps
