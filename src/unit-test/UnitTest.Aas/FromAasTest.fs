namespace T.Core

open Dual.Ev2
open Dual.Ev2.Aas
open NUnit.Framework
open Dual.Common.UnitTest.FS
open Dual.Ev2.Aas.AasXModule2
open Dual.Ev2.Aas

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
            xml =~= aasXml0

            env.ToJson() =~= aasJson0


            //let dsSystem = DsSystem.FromAasJsonENV(aasJson0)
            ()

        [<Test>]
        member _.``Aasx xml submode xml fetch test`` () =
            // 1. AASX 파일에서 원본 XML 읽기
            let testAasxFile = "test.aasx"
            let submodelXml = getAasXmlFromAasxFile testAasxFile


            // XML이 비어있지 않은지 확인
            submodelXml.Length > 0 === true

            // AAS XML 네임스페이스 포함 확인
            submodelXml.Contains("admin-shell.io") === true
