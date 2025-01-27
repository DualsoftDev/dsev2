namespace T.Core


open System.Linq
open NUnit.Framework
open Newtonsoft.Json

open Dual.Common.UnitTest.FS
open Dual.Common.Base.CS
open Dual.Common.Base.FS
open Dual.Common.Core.FS

open Dual.Ev2

module Xml =
    let json = Json.json

    /// Json Test
    type T() =
        [<Test>]
        member _.``Minimal`` () =
            let system = DsSystem.Deserialize(json)
            let xml = system.SerializeToXml()
            DcClipboard.Write(xml)
