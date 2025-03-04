namespace PLC.CodeGen.LS

open Dual.Common.Core.FS
open Engine.Core
open System.Runtime.CompilerServices
open System.Xml
open System.Collections.Generic
open Dual.Common.Base.CS

[<AutoOpen>]
module internal XgxXmlExtensionImpl =
    let getXPathGlobalVariable (xgx:PlatformTarget) =
        let var =
            match xgx with
            | XGI -> "GlobalVariable"
            | XGK -> "VariableComment"
            | _ -> failwithlog "Not supported plc type"
        $"//Configurations/Configuration/GlobalVariables/{var}"
    let xPathLocalVar = "//POU/Programs/Program/LocalVar"

type XgxXmlExtension =
    /// XmlNode '//Configurations/Configuration/GlobalVariables/{GlobalVariable, VariableComment}' 반환
    [<Extension>]
    static member GetXmlNodeTheGlobalVariable (xdoc:XmlDocument, xgx:PlatformTarget) : XmlNode = getXPathGlobalVariable xgx |> xdoc.SelectSingleNode

    /// XGK 에서 사용할 수 없는 변수명 체크
    [<Extension>]
    static member SanityCheckVariableNameForXgk (xdoc:XmlDocument) : unit =
        (* "load" 의 경우, global 변수로 선언은 가능하지만, XML 로 Rung 생성시,
         * 해당 변수 이름을 사용하지 못하고 대신 직접 변수로 메모리 주소를 적어야 한다.
         * 따라서 이러한 종류들은 원천적으로 사용하지 않는 것으로 한다.
         *)
        let invalidNames = [|"load"; "ld"|] |> HashSet
        let invalidSymbols =
            xdoc.GetXmlNodes($"//Symbol")
            |> map(fun x -> x.Attributes["Name"].Value)
            |> filter(fun x -> invalidNames.Contains x)
            |> toArray
        if invalidSymbols.Length > 0 then
            let errs = invalidSymbols |> String.concat ", "
            failwithlog $"Invalid symbol names for XGK: {errs}"

    /// XGI 기준으로 LocalVar 에 정의한 symbol 들을 XGK 인 경우에 한해, GlobalVariable 로 이동시킨다.
    [<Extension>]
    static member MovePOULocalSymbolsToGlobalForXgk (xdoc:XmlDocument) : unit =
        let xPathGlobalVar = getXPathGlobalVariable XGK
        let globalSymbols:Dictionary<string, XmlNode> =
            xdoc.GetXmlNodes($"{xPathGlobalVar}/Symbols/Symbol")
            |> map(fun x -> x.Attributes["Name"].Value, x)
            |> Tuple.toDictionary

        // LocalVar 에서만 정의된 symbols
        let localOnlySymbolss:XmlNode[] =
            xdoc.GetXmlNodes($"{xPathLocalVar}/Symbols/Symbol")
            |> filter(fun x ->
                x.Attributes.["Name"].Value
                |> globalSymbols.ContainsKey
                |> not)
            |> toArray

        // LocalVar 의 Symbol 들을 GlobalVar 로 이동
        let xnGlobalVarsContainer = xdoc.GetXmlNode($"{xPathGlobalVar}/Symbols")
        localOnlySymbolss |> Array.iter(fun x -> xnGlobalVarsContainer.AdoptChild x |> ignore)

        // LocalVar 정의 삭제
        xdoc.GetXmlNodes(xPathLocalVar).Iter(fun x -> x.ParentNode.RemoveChild x |> ignore)

        xnGlobalVarsContainer.Attributes["Count"].Value <- globalSymbols.Count.ToString()
        ()

    /// Xml document 상에서 제대로 생성되었는지 검사한다.
    ///
    /// - Symbol 의 DevicePos 가 음수인 Symbol 이 있는지 확인한다.
    [<Extension>]
    static member Check(xdoc:XmlDocument, xgx:PlatformTarget) =
        let checkSymbols() =
            let xPathGlobalVar = getXPathGlobalVariable xgx
            let globalSymbols:XmlNode[] = xdoc.GetXmlNodes($"{xPathGlobalVar}/Symbols/Symbol").ToArray()
            let localSymbolss:XmlNode[] = xdoc.GetXmlNodes($"{xPathLocalVar}/Symbols/Symbol").ToArray()

            let check (s:XmlNode) =
                let name = s.Attributes.["Name"].Value
                let devPos = s.Attributes["DevicePos"]
                if devPos <> null && devPos.Value.NonNullAny() && int devPos.Value < 0  then
                    failwith $"Symbol {name} has Invalid DevicePos attribute {devPos.Value}."

            for s in globalSymbols @ localSymbolss do
                check s

        let checkRungs() =
            let pous = xdoc.GetXmlNodes("//LDRoutine")
            use _ = DcLogger.CreateTraceEnabler()
            for pou in pous do
                let rungs = pou.GetXmlNodes("Rung")
                let mutable c = 0
                let getCoordinate (e:XmlNode) = e.Attributes.["Coordinate"].Value |> Parse.TryInt |> Option.get
                for r in rungs do
                    let rungName =
                        match r.Attributes.["Name"] with
                        | null -> "이름없음"
                        | n -> n.Value
                    let elements = r.GetXmlNodes("Element") |> toList
                    let maxCoord =
                        match elements with
                        | [] -> failwith $"Rung {rungName} has no elements."
                        | e::[] when getCoordinate(e) <= c ->
                            failwith $"Rung {rungName} has invalid coordinates : {getCoordinate(e)} <= {c}."
                        | _ ->
                            let coordinates = elements |> map (fun x -> Parse.TryInt x.Attributes.["Coordinate"].Value |> Option.get) |> toArray

                            (* C = A || B 래더 생성 시, 좌표 순서가 A, C, B 로 나와야 하지만, 현재는 A, B, C 순서로 나와서 일단 check 보류 *)
                            let isOrdered = coordinates |> pairwise |> Seq.forall (fun (a, b) -> a < b)
                            if not isOrdered then
                                tracefn $"WARN: Rung {rungName} has invalid coordinates."
                                //failwith $"Rung {rungName} has invalid coordinates."

                            coordinates |> Seq.last
                    c <- maxCoord
        checkSymbols()
        checkRungs()

        xdoc

