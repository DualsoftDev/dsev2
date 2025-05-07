namespace T.Core

open Dual.Ev2
open Dual.Ev2.Aas
open Dual.Ev2.Aas.CoreToJsonViaCode
open NUnit.Framework
open Dual.Common.UnitTest.FS
open Dual.Common.Base


module FromAasTest =
    let dsJson = DsJson.dsJson
    let aasJson0 = Aas.aasJson0
    let aasXml0 = Aas.aasXml0
    ()
    /// Json Test
    type T() =
        [<Test>]
        member _.``AasShell: Json -> JObj -> {Xml, Json} conversion test`` () =
            let env = J.CreateIClassFromXml<Aas.Environment>(aasXml0)
            let xml = env.ToXml()
            xml === aasXml0

            env.ToJson() === aasJson0


            //let dsSystem = DsSystem.FromAasJsonENV(aasJson0)
            ()