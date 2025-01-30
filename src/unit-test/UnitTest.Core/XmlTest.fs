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


        [<Test>]
        member _.testMe() =
            // JSON -> XmlDocument 변환 실행
            let xmlDocument = EmJson.JsonToXmlDocument(json, "System")
            printfn "Generated XML:\n%s" (xmlDocument.OuterXml)
            ()


(*
JSON 배열([])은 XML에서 명시적으로 배열로 표현되지 않음.
*)