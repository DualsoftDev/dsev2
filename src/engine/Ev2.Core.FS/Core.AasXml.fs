(* ChatGPT 생성 코드 *)


(* https://aas-core-works.github.io/aas-core3.0-csharp/getting_started/install.html
 *)

namespace rec Dual.Ev2

open Newtonsoft.Json
open System.Xml.Serialization
open System.IO
open System.Xml.Linq


[<AutoOpen>]
module AasXml =


    // AAS 서브모델 요소 생성
    let createSubmodelElement (name: string) (description: string) =
        let submodelElement = XElement(XName.Get("submodelElement"))
        submodelElement.Add(
            XElement(XName.Get("idShort"), name),
            XElement(XName.Get("description"), description)
        )
        submodelElement

    // DsSystem 객체에서 AAS 서브모델 생성
    let createAasSubmodel (dsSystem: DsSystem) =
        let submodel = XElement(XName.Get("submodel"))
        submodel.Add(
            XElement(XName.Get("identification"), dsSystem.Name),  // 시스템 이름
            XElement(XName.Get("submodelElements"))
        )

        // 각 Flow에 대해 서브모델 항목 생성
        for flow in dsSystem.Flows do
            let flowElement = createSubmodelElement flow.Name "Flow Description"
            submodel.Element(XName.Get("submodelElements")).Add(flowElement)

            // 각 Vertex에 대해 서브모델 항목 생성
            for vertex in flow.Vertices do
                match vertex with
                | Work work ->
                    let workElement = createSubmodelElement work.Name "Work Description"
                    submodel.Element(XName.Get("submodelElements")).Add(workElement)
                | Action action ->
                    let actionElement = createSubmodelElement action.Name "Action Description"
                    submodel.Element(XName.Get("submodelElements")).Add(actionElement)
                | _ -> () // 다른 경우 처리

        submodel

    // 기존 AAS XML을 로드하고 새로운 submodel 추가
    let updateAasXml (existingAasXml: string) (dsSystem: DsSystem) =
        let doc = XDocument.Load(existingAasXml)
        let newSubmodel = createAasSubmodel dsSystem

        // AAS XML의 submodels 요소에 새로운 submodel 추가
        doc.Root.Element(XName.Get("submodels")).Add(newSubmodel)

        doc.Save("updated_aas.xml")








    // 네임스페이스 정의
    let AasNamespace = "http://www.admin-shell.io/aas/3/0"

    type SubmodelElementCollection() =
        [<XmlElement("idShort", Namespace = "http://www.admin-shell.io/aas/3/0")>] member val IdShort = "" with get, set
        [<XmlElement("value", Namespace = "http://www.admin-shell.io/aas/3/0")>] member val Value = "" with get, set

    type Submodel() =
        [<XmlElement("submodelElements", Namespace = "http://www.admin-shell.io/aas/3/0")>]
        member val SubmodelElements: SubmodelElementCollection[] = [||] with get, set

    type AAS() =
        [<XmlElement("submodels", Namespace = "http://www.admin-shell.io/aas/3/0")>]
        member val Submodels: Submodel[] = [||] with get, set

    // JSON 직렬화 함수
    let serializeJson (obj: 't) =
        JsonConvert.SerializeObject(obj)

    // AAS XML 직렬화 함수 (네임스페이스 및 태그 이름 수정)
    let serializeAASXml (obj: 't) =
        let xmlRootAttr = XmlRootAttribute("aas")
        xmlRootAttr.Namespace <- AasNamespace
        let xmlSerializer = new XmlSerializer(typeof<'t>, xmlRootAttr)
        use stringWriter = new StringWriter()
        xmlSerializer.Serialize(stringWriter, obj)
        stringWriter.ToString()

    // AAS XML 역직렬화 함수 (네임스페이스 처리)
    let deserializeAASXml (xml: string) =
        let xmlRootAttr = XmlRootAttribute("aas")
        xmlRootAttr.Namespace <- AasNamespace
        let xmlSerializer = new XmlSerializer(typeof<AAS>, xmlRootAttr)
        use stringReader = new StringReader(xml)
        xmlSerializer.Deserialize(stringReader) :?> AAS

    // JSON 역직렬화 함수
    let deserializeJson (json: string) =
        JsonConvert.DeserializeObject<AAS>(json)

    // 예시 객체 생성
    let aasExample =
        let ele = SubmodelElementCollection(IdShort = "Name", Value = "system1")
        let submodel = Submodel(SubmodelElements = [|ele|])
        AAS(Submodels = [|submodel|])



    let test(json:string) =
        // JSON을 DsSystem 객체로 변환
        let dsSystem = JsonConvert.DeserializeObject<DsSystem>(json)

        // 기존 AAS XML 파일 경로
        let existingAasXml = "existing_aas.xml"

        // AAS XML 업데이트
        updateAasXml existingAasXml dsSystem



    let test2() =
        // 직렬화 예시
        let json = serializeJson aasExample
        let aasXml = serializeAASXml aasExample

        printfn "JSON Output: %s" json
        printfn "AAS XML Output:\n%s" aasXml

        // 역직렬화 예시
        let jsonInput = """{"Submodels":[{"SubmodelElements":[{"idShort":"Name","value":"system1"}]}]}"""
        let aasXmlInput = """<aas xmlns="http://www.admin-shell.io/aas/3/0">
                            <submodels>
                                <submodel>
                                    <submodelElements>
                                        <idShort>Name</idShort>
                                        <value>system1</value>
                                    </submodelElements>
                                </submodel>
                            </submodels>
                        </aas>"""

        // 역직렬화 실행
        let jsonObject = deserializeJson jsonInput
        let aasXmlObject = deserializeAASXml aasXmlInput

        // 출력 확인
        printfn "Deserialized JSON: %A" jsonObject
        printfn "Deserialized AAS XML: %A" aasXmlObject

        printf "Done!"



    let sampleAasSubmodel = """
<Submodel>
  <Identification>
    <Id type="IRI">http://example.org/submodel/system1</Id>
  </Identification>
  <SubmodelElements>
    <SubmodelElement>
      <IdShort>System1</IdShort>
      <Kind>Instance</Kind>
      <Value>
        <System>
          <Name>system1</Name>
          <Flows>
            <Flow>
              <Name>F1</Name>
              <Vertices>
                <Vertex>
                  <Case>Work</Case>
                  <Fields>
                    <Field>
                      <Name>F1W1</Name>
                      <Vertices>
                        <Vertex>
                          <Case>Action</Case>
                          <Fields>
                            <Field>
                              <Name>F1W1C1</Name>
                            </Field>
                          </Fields>
                        </Vertex>
                        <Vertex>
                          <Case>Action</Case>
                          <Fields>
                            <Field>
                              <Name>F1W1C2</Name>
                            </Field>
                          </Fields>
                        </Vertex>
                      </Vertices>
                    </Field>
                  </Fields>
                  <Edges>
                    <Edge>
                      <Source>F1W1C1</Source>
                      <Target>F1W1C2</Target>
                      <EdgeType>
                        <Case>Start</Case>
                      </EdgeType>
                    </Edge>
                  </Edges>
                </Vertex>
              </Vertices>
            </Flow>
          </Flows>
        </System>
      </Value>
    </SubmodelElement>
  </SubmodelElements>
</Submodel>
"""