namespace T.Core

(* ChatGPT 생성 코드 *)

open System
open System.Xml.Linq
open Newtonsoft.Json
open Dual.Ev2
open NUnit.Framework
module Aas1 =
    let [<Literal>] aas = "http://www.admin-shell.io/aas/3/0"
    // AAS Submodel에 맞는 XML 생성
    let createSubmodelXml (dsSystem: DsSystem): XElement =
        let submodelId = Guid.NewGuid().ToString()
        let submodel =
            XElement(
                XName.Get("submodel", aas),
                XElement(XName.Get("idShort", aas), dsSystem.Name),
                XElement(XName.Get("identification", aas),
                    XElement(XName.Get("id", aas), submodelId)
                ),
                XElement(XName.Get("submodelElements", aas),
                    dsSystem.Flows
                    |> Seq.map (fun flow ->
                        XElement(
                            XName.Get("submodelElement", aas),
                            XElement(XName.Get("idShort", aas), flow.Name),
                            XElement(XName.Get("category", aas), "Flow"),
                            XElement(XName.Get("value", aas),

                                JsonConvert.SerializeObject(flow.Vertices)

                            )
                        )
                    )
                )
            )
        submodel

    // 기존 AAS XML에 Submodel 삽입
    let addSubmodelToAasXml (xmlPath: string) (dsSystem: DsSystem) =
        let doc = XDocument.Load(xmlPath)
        let submodelsElement:XElement option =
            doc.Descendants(XName.Get("submodels", aas))
            |> Seq.tryHead

        match submodelsElement with
        | Some(element) ->
            let submodelXml = createSubmodelXml dsSystem
            element.Add(submodelXml)
            doc.Save(xmlPath)
        | None ->
            printfn "Submodels 태그를 찾을 수 없습니다."

    // Submodel로부터 DsSystem 복원
    let parseSubmodelToDsSystem (submodel: XElement) =
        let name = submodel.Element(XName.Get("idShort", aas)).Value
        let dsSystem = DsSystem(name)
        let flows =
            submodel.Element(XName.Get("submodelElements", aas))
            |> fun submodelElements ->
                submodelElements.Elements()
                |> Seq.map (fun element ->
                    let flowName = element.Element(XName.Get("idShort", aas)).Value
                    let verticesJson = element.Element(XName.Get("value", aas)).Value
                    let vertices = JsonConvert.DeserializeObject<VertexDetail[]>(verticesJson)
                    let flow = DsFlow(dsSystem, flowName)
                    flow.Vertices.AddRange(vertices)
                    flow
                )
                |> ResizeArray
        dsSystem.Flows <- flows
        dsSystem

    let test() =
        // 사용 예시
        let dsSystem = DsSystem("ExampleSystem")
        dsSystem.Flows.Add(DsFlow(dsSystem, "ExampleFlow"))
        let xmlPath = "path/to/your/aas.xml"

        // Submodel 추가
        addSubmodelToAasXml xmlPath dsSystem

        // Submodel 복원
        let submodelXml = XElement.Load("path/to/extracted/submodel.xml")
        let restoredSystem = parseSubmodelToDsSystem submodelXml
        ()



//module AasTest =
    let json = Json.json

    /// Json Test
    type T() =
        [<Test>]
        member _.``AAS2`` () =
            let system2 = DsSystem.FromJson(json)
            let xxx = createSubmodelXml system2
            ()
