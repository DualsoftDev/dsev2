namespace Dual.Plc2DS.LS


open Dual.Common.Core.FS
open Dual.Plc2DS
open System.Xml

module Xgx =


        (*
            - Project/Configurations/Configuration/GlobalVariables/GlobalVariable/Symbols/Symbol
                <Symbol Name="nn1" Comment="int16 nn1 = 1s;" Device="" Kind="6" Type="INT" InitValue="1" Address="" State="0"/>
            - Project/Configurations/Configuration/POU/Programs/Program/LocalVar/Symbols/Symbol
                <Symbol Name="nn1" Comment="int16 nn1 = 1s;" Device="" Kind="8" State="0"/>
            - Project/Configurations/Configuration/POU/Programs/Program/LocalVar/TempVar
                ???
            - UserFunctions/UserFunction/InputVariables/Variable
                <Variable Type="0" DataType="2097153" Trigger="0" UDTName="" Display="0" InstMemory="0">EN</Variable>
            - UserFunctions/UserFunction/OutputVariables/Variable
                <Variable Type="2" DataType="1" Trigger="0" UDTName="" Display="0" InstMemory="0">ENO</Variable>
            - UserFunctions/UserFunction/UserFunctionBlockVar/Symbols/Symbol
                <Symbol Name="a" Kind="1" Type="BOOL" State="0" Address="" Trigger="" InitValue=""
                    Comment="" Device="B" DevicePos="842" TotalSize="1" OrderIndex="9" HMI="0" EIP="0"
                    SturctureArrayOffset="0" ModuleInfo="" ArrayPointer="0" PtrType="" Motion="0"></Symbol>
        *)

    let IsXg5kXGT(xmlProjectFilePath:string) =
        let xdoc = DualXmlDocument.loadFromFile xmlProjectFilePath
        xdoc.GetXmlNode("//Configurations/Configuration/Parameters/Parameter/XGTBasicParam") <> null

    let [<Literal>] private globalVariable = "Project/Configurations/Configuration/GlobalVariables/GlobalVariable"
    let private getAttribute (xn:XmlNode) (attr:string) = xn.Attributes.[attr].Value

    let getGlobalSymbolXmlNodes (xmlDoc:XmlDocument) =
        xmlDoc.SelectNodes(globalVariable + "/Symbols/Symbol")
        // <Symbol Name="_0002_A1_RDY" Kind="6" Type="BOOL" State="12" Address="%UX0.2.0" Trigger="" InitValue="" Comment="위치결정 모듈: 1축 Ready" Device="U" DevicePos="1024" TotalSize="1" OrderIndex="-1" HMI="0" EIP="0" SturctureArrayOffset="0" ModuleInfo="SP:0:2:0" ArrayPointer="0"><MemberAddresses></MemberAddresses>
    let getDirectVarXmlNodes (xmlDoc:XmlDocument) =
        xmlDoc.SelectNodes(globalVariable + "/DirectVarComment/DirectVar")
        // <DirectVar Device="%IX0.0.0" Name="" Comment="RR 공급 감지 센서" Used="1"></DirectVar>

    let getGlobalAddresses (xmlDoc:XmlDocument) =
        ( getGlobalSymbolXmlNodes xmlDoc
          |> _.ToEnumerables()
          |> Seq.map (fun xn -> getAttribute xn "Address"))
        @
        ( getDirectVarXmlNodes xmlDoc
          |> _.ToEnumerables()
          |> Seq.map (fun xn -> getAttribute xn "Device"))


open Xgx
open System.Text.RegularExpressions

type XmlReader =
    static member ReadTags(xmlPath: string, ?usedOnly): PlcTagInfo[] =
        let usedOnly = usedOnly |? true
        let xdoc:XmlDocument = DualXmlDocument.loadFromFile xmlPath
        let dvars = getDirectVarXmlNodes xdoc
        let gvars = getGlobalSymbolXmlNodes xdoc    // <Symbol Name="RH_Servo_SA" Kind="6" Type="DINT" State="0" Address="" Trigger="" InitValue="" Comment="RH_서보 현재 위치" Device="A" DevicePos="114336" TotalSize="32" OrderIndex="-1" HMI="0" EIP="0" SturctureArrayOffset="0" ModuleInfo="" ArrayPointer="0" PtrType="" Motion="0"></Symbol>
        let addrPattern = Regex("^%(?<iom>[IQM])(?<size>[XBW])", RegexOptions.Compiled)
        [|
            for v in gvars do
                let name    = v.Attributes["Name"]   .Value
                let addr    = v.Attributes["Address"].Value
                let typ     = v.Attributes["Type"]   .Value
                let comment = v.Attributes["Comment"].Value
                let kind    = v.Attributes["Kind"]   .Value
                let comment = v.Attributes["Comment"].Value
                PlcTagInfo(typ="Tag", scope="GlobalVariable", variable=name, address=addr, dataType=typ)

            for v in dvars do
                let used = v.Attributes["Used"].Value
                if not usedOnly || used = "1" then
                    let name    = v.Attributes["Name"]   .Value
                    let comment = v.Attributes["Comment"].Value
                    let device  = v.Attributes["Device"] .Value

                    let dataType =
                        let m = addrPattern.Match(device)
                        if m.Success then
                            let iom = m.Groups.["iom"].Value
                            let size = m.Groups.["size"].Value
                            match size with
                            | "X" -> "BOOL"
                            | "B" -> "BYTE"
                            | "W" -> "WORD"
                            | _ -> failwithf "Unknown data type: %s" size
                        else
                            ""

                    PlcTagInfo(typ="Tag", scope="DirectVar", variable=comment, address=device, dataType=dataType)
        |]

