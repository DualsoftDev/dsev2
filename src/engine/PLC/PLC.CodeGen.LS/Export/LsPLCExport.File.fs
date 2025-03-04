namespace PLC.CodeGen.LS

open System.Reflection

open Dual.Common.Core.FS
open PLC.CodeGen.LS
open PLC.CodeGen.LS.Config.POU.Program.LDRoutine
open Engine.Core
open Dual.Common.Base.FS

[<AutoOpen>]
module internal XgiFile =
    [<Literal>]
    let XGIMaxX = 28

    /// text comment 를 xml wrapping 해서 반환
    let getCommentRungXml y cmt =
        let yy = y * 1024 + 1
        wrapWithRung $"<Element ElementType=\"{int ElementType.RungCommentMode}\" Coordinate=\"{yy}\">{cmt}</Element>"

    let getLableXml y cmt =
        let yy = y * 1024 + 1
        wrapWithRung $"<Element ElementType=\"{int ElementType.LabelMode}\" Coordinate=\"{yy}\">{cmt}</Element>"


    /// Program 마지막 부분에 END 추가
    let generateEndXml y =
        let yy = y * 1024 + 1

        sprintf
            """
            <Element ElementType="%d" Coordinate="%d" Param="90"></Element>
			<Element ElementType="%d" Coordinate="%d" Param="END">END</Element>
			"""
            (int ElementType.MultiHorzLineMode)
            yy
            (int ElementType.FBMode)
            (yy + 93)
        |> wrapWithRung


    /// Template XGI XML 문자열을 반환
    let getResource filename =
        let assembly = Assembly.GetExecutingAssembly()
        EmbeddedResource.readFile assembly filename
    //  static member CPUs      = [|"XGI-CPUE"; "XGI-CPUH"; "XGI-CPUS"; "XGI-CPUS/P"; "XGI-CPUU"; "XGI-CPUU/D"; "XGI-CPUUN" |]
    //static member CPUsID    = [|"106"     ; "102"     ; "104"     ; "110"       ; "100"     ; "107"       ; "111"       |]
    /// Template XGI XML 문자열을 반환
    let getTemplateXgxXml (targetType: PlatformTarget) =
        match targetType with
        | XGI -> "xgi-4.5.2.template.xml"
        | XGK -> "XGK-CPUUN-4.77.99.1.template.xml"
        | _ -> failwithlog "Not supported plc type"
        |> getResource
        |> Option.get

    /// Template XGI XML 문서 (XDocument) 반환
    let getTemplateXgxXmlDoc = getTemplateXgxXml >> DualXmlDocument.fromString



    /// rung 및 local var 에 대한 문자열 xml 을 전체 xml project file 에 embedding 시켜 outputPath 파일에 저장한다.
    /// Template file (EmptyLSISProject.xml) 에서 marking 된 위치를 참고로 하여 rung 및 local var 위치 파악함.
    (*
         symbolsLocal =        "<LocalVar Version="Ver 1.0" Count="1493"> <Symbols> <Symbol> ... </Symbol> ... <Symbol> ... </Symbol> </Symbols> .. </LocalVar>
         symbolsGlobal = "<GlobalVariable Version="Ver 1.0" Count="1493"> <Symbols> <Symbol> ... </Symbol> ... <Symbol> ... </Symbol> </Symbols> .. </GlobalVariable>
    *)
    let wrapWithXml (prjParam: XgxProjectParams) (rungs: XmlOutput) (localSymbolInfos:SymbolInfo list) (symbolsGlobal:string) (existingLSISprj: string option) =

        (*
         * Unit Test 전용 코드.
         * 실제 코드는 LsPLCExport.Export.fs 의 XgxProjectParams extension method 참고. GenerateXmlDocument()
         *)
        assert isInUnitTest()

        let targetType = prjParam.TargetType
        let xdoc, prjParam =
            match prjParam.TargetType, existingLSISprj with
            | XGK, Some existing ->
                let doc = DualXmlDocument.loadFromFile existing
                let counters = collectCounterAddressesXgk doc
                let timers = collectTimerAddressesXgk doc
                let newPrjParam = {
                    prjParam with
                        CounterCounterGenerator = counterGeneratorOverrideWithExclusionList prjParam.CounterCounterGenerator counters
                        TimerCounterGenerator = counterGeneratorOverrideWithExclusionList prjParam.TimerCounterGenerator timers
                }
                doc, newPrjParam
            | _, None ->
                let doc = getTemplateXgxXmlDoc targetType
                doc, prjParam
            | _, Some existing ->
                let doc = DualXmlDocument.loadFromFile existing
                doc, prjParam


        xdoc.GetXmlNode("Project").FirstChild.InnerText <- prjParam.ProjectName

        let pouName = "DsLogic"

        if null <> xdoc.SelectSingleNode(sprintf "//POU/Programs/Program/%s" pouName) then
            failwithlogf "POU name %s already exists.  Can't overwrite." pouName

        let programs = xdoc.SelectSingleNode("//POU/Programs")

        // Dirty hack "Scan Program" vs "?? ????"
        let taskName =
            xdoc.SelectNodes("//POU/Programs/Program").ToEnumerables()
            |> map (fun xmlnode -> xmlnode.Attributes.["Task"].Value)
            |> Seq.tryHead
            |> Option.defaultValue "Scan Program"

        printfn "%A" taskName


        /// POU/Programs/Program
        let programTemplate =
            sprintf
                """
			    <Program Task="%s" Version="256" LocalVariable="1" Kind="0" InstanceName="" Comment="" FindProgram="1" FindVar="1" Encrytption="">%s
                    <Body>
					    <LDRoutine>
						    <OnlineUploadData Compressed="1" dt:dt="bin.base64" xmlns:dt="urn:schemas-microsoft-com:datatypes">QlpoOTFBWSZTWY5iHkIAAA3eAOAQQAEwAAYEEQAAAaAAMQAACvKMj1MnqSRSSVXekyB44y38
    XckU4UJCOYh5CA==</OnlineUploadData>
					    </LDRoutine>
				    </Body>
				    <RungTable></RungTable>
			    </Program>"""
                taskName
                pouName
            |> DualXmlNode.ofString



        let programTemplate = programs.AdoptChild programTemplate

        (* xn = Xml Node *)
        /// LDRoutine 위치 : Rung 삽입 위치
        let xnLdRoutine = programTemplate.GetXmlNode "Body/LDRoutine"
        let onlineUploadData = xnLdRoutine.FirstChild

        (*
         * Rung 삽입
         *)
        let rungsXml = $"<Rungs>{rungs}</Rungs>" |> DualXmlNode.ofString

        for r in rungsXml.GetChildrenNodes() do
            onlineUploadData.InsertBefore r |> ignore

        (*
         * Global variables 삽입
         *)
        let xnGlobalVar = xdoc.GetXmlNodeTheGlobalVariable(targetType)

        (*
         * Local variables 삽입 - 동일 코드 중복.  수정시 동일하게 변경 필요
         *)
        let localSymbols = localSymbolInfos |> XGITag.generateLocalSymbolsXml prjParam |> DualXmlNode.ofString
        let programBody = xnLdRoutine.ParentNode
        programBody.InsertAfter localSymbols |> ignore

        let xnGlobalVarSymbols = xnGlobalVar.GetXmlNode "Symbols"

        let xnCountConainer =
            match targetType with
            | XGI -> xnGlobalVar
            | XGK -> xnGlobalVarSymbols
            | _ -> failwithlog $"Unknown Target: {targetType}"

        let countExistingGlobal =
            xnCountConainer.Attributes.["Count"].Value |> System.Int32.Parse

        let _globalSymbolXmls =     // 아래 side effect 를 위해 필요
            // symbolsGlobal = "<GlobalVariable Count="1493"> <Symbols> <Symbol> ... </Symbol> ... <Symbol> ... </Symbol>
            let neoGlobals = symbolsGlobal |> DualXmlNode.ofString
            let numNewGlobals = neoGlobals.Attributes.["Count"].Value |> System.Int32.Parse

            xnCountConainer.Attributes.["Count"].Value <- sprintf "%d" (countExistingGlobal + numNewGlobals)

            neoGlobals.SelectNodes(".//Symbols/Symbol").ToEnumerables()
            |> iter (xnGlobalVarSymbols.AdoptChild >> ignore)       // side effect

        if targetType = XGK then
            xdoc.MovePOULocalSymbolsToGlobalForXgk ()

        xdoc.ToText()
