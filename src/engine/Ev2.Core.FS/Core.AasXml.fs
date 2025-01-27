namespace rec Dual.Ev2

open Newtonsoft.Json

open System.Xml.Serialization
open System.IO



[<AutoOpen>]
module AasXml =

    type SubmodelElementCollection() =
        [<XmlElement("idShort")>] member val IdShort = "" with get, set
        [<XmlElement("value")>]   member val Value = "" with get, set
    
    type Submodel() =
        [<XmlElement("submodelElements")>] member val SubmodelElements: SubmodelElementCollection [] = [||] with get, set

    type AAS() =
        [<XmlElement("submodels")>] member val Submodels: Submodel[] = [||] with get, set

    // JSON 직렬화 함수
    let serializeJson (obj: 't) =
        JsonConvert.SerializeObject(obj)

    // AAS XML 직렬화 함수
    let serializeAASXml (obj: 't) =
        let xmlSerializer = new XmlSerializer(typeof<'t>)
        use stringWriter = new StringWriter()
        xmlSerializer.Serialize(stringWriter, obj)
        stringWriter.ToString()


    // JSON 역직렬화 함수
    let deserializeJson (json: string) =
        JsonConvert.DeserializeObject<AAS>(json)

    // AAS XML 역직렬화 함수
    let deserializeAASXml (xml: string) =
        let xmlSerializer = new XmlSerializer(typeof<AAS>)
        use stringReader = new StringReader(xml)
        xmlSerializer.Deserialize(stringReader) :?> AAS


    // 예시 객체 생성
    let aasExample =
        let ele = SubmodelElementCollection(IdShort = "Name", Value = "system1")
        let submodel = Submodel(SubmodelElements = [|ele|])
        AAS(Submodels = [|submodel|])


    let test() =
        // 직렬화 예시
        let json = serializeJson aasExample
        let aasXml = serializeAASXml aasExample

        printfn "JSON Output: %s" json
        printfn "AAS XML Output:\n%s" aasXml




        // 역직렬화 예시
        let jsonInput = """{"Submodels":[{"SubmodelElements":[{"idShort":"Name","value":"system1"}]}]}"""
        let aasXmlInput = """<aas:aas xmlns:aas="http://www.admin-shell.io/aas/3/0">
                            <aas:submodels>
                                <aas:submodel>
                                    <aas:submodelElements>
                                        <aas:SubmodelElementCollection>
                                            <aas:idShort>Name</aas:idShort>
                                            <aas:value>system1</aas:value>
                                        </aas:SubmodelElementCollection>
                                    </aas:submodelElements>
                                </aas:submodel>
                            </aas:submodels>
                        </aas:aas>"""

        // 역직렬화 실행
        let jsonObject = deserializeJson jsonInput
        let aasXmlObject = deserializeAASXml aasXmlInput

        // 출력 확인
        printfn "Deserialized JSON: %A" jsonObject
        printfn "Deserialized AAS XML: %A" aasXmlObject

        printf "Done!"
