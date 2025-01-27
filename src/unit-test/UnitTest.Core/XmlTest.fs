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
open Dual.Common.Base.FS

module Xml =
    module testModule =
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
            // JSON -> XmlDocument 변환 실행
            let xmlDocument = EmJson.JsonToXmlDocument(json, "System")
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
            let system2 = DsSystem.FromJson(json2)
            //json === json2
            let json3 = system2.ToJson()
            json === json3


            let xxx = EmJson.ToXml(system2, rootName="System")
            ()


(*
JSON 배열([])은 XML에서 명시적으로 배열로 표현되지 않음.
*)