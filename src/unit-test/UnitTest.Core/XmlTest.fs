namespace T.Core


open NUnit.Framework
open Newtonsoft.Json

open Dual.Common.Base.CS

open Dual.Ev2
open System.Xml.Linq


//open System.Xml.Linq
open System.Xml
open Newtonsoft.Json.Converters
open Dual.Common.UnitTest.FS

module Xml =
    module testModule =
        let deserializeJsonToXmlWithSettings (json: string) (rootName:string): XmlDocument=
            // XmlNodeConverter 설정
            let xmlConverter = XmlNodeConverter()
            xmlConverter.DeserializeRootElementName <- rootName
            xmlConverter.WriteArrayAttribute <- true // 배열 처리를 명시적으로 설정

            // JsonSerializer 생성
            let serializer = JsonSerializer()
            serializer.Converters.Add(xmlConverter)

            // JSON -> XmlDocument 변환
            use stringReader = new System.IO.StringReader(json)
            use jsonReader = new JsonTextReader(stringReader)
            let xmlDoc = serializer.Deserialize<XmlDocument>(jsonReader)
            xmlDoc


        // 테스트 JSON 데이터
        let json = """
        {
          "Name": "system1",
          "Flows": [
            {
              "Name": "F1",
              "Vertices": [
                {
                  "Case": "Work",
                  "Fields": [
                    {
                      "Name": "F1W1",
                      "Vertices": [
                        { "Case": "Action", "Fields": { "Name": "F1W1C1" } },
                        { "Case": "Action", "Fields": { "Name": "F1W1C2" } }
                      ],
                      "Edges": [
                        { "Source": "F1W1C1", "Target": "F1W1C2", "EdgeType": { "Case": "Start" } }
                      ]
                    }
                  ]
                }
              ],
              "Edges": []
            }
          ]
        }
            """
        [<Test>]
        let testMe() =
            // JSON -> XML 변환 실행
            let xmlDocument = deserializeJsonToXmlWithSettings json "System"
            printfn "Generated XML:\n%s" (xmlDocument.OuterXml)

    let json = Json.json

    /// Json Test
    type T() =
        [<Test>]
        member _.``Minimal`` () =
            // JSON -> XDoc (XML)
            let xdoc = JsonConvert.DeserializeXmlNode(json, "System", writeArrayAttribute=true)
            DcClipboard.Write(xdoc.OuterXml)

            // XML(XmlDoc) -> JSON
            let json2 = JsonConvert.SerializeXmlNode(xdoc, Newtonsoft.Json.Formatting.Indented, omitRootObject=true)
            DcClipboard.Write(json2)
            let system2 = DsSystem.Deserialize(json2)

            json === json2

            ()


(*
JSON 배열([])은 XML에서 명시적으로 배열로 표현되지 않음.
*)