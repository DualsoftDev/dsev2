namespace T.Core

open Dual.Ev2
open Dual.Ev2.Aas
open Dual.Ev2.Aas.CoreToJsonViaCode
open NUnit.Framework
open Dual.Common.UnitTest.FS
open Dual.Common.Base
open System
open Dual.Common.Base
open Ev2.Core.FS


module ToAasTest =
    let dsJson = DsJson.dsJson
    let dsProject = DsJson.dsProject
    let aasJson = Aas.aasJson0
    let aasXml = Aas.aasXml0

    /// Json Test
    type T() =
        [<Test>]
        member _.``AasShell: JObj -> string conversion test`` () =
            let assetInformation = JObj().Set(N.AssetKind, "NotApplicable").Set(N.GlobalAssetId, "something_eea66fa1")
            let assetAdministrationShell = JObj().Set(N.Id, "something_142922d6").Set(N.AssetInformation, assetInformation).Set(N.ModelType, "AssetAdministrationShell")
            let assetAdministrationShells = JObj().Set(N.AssetAdministrationShells, J.CreateJArr( [assetAdministrationShell] ))
            let json = assetAdministrationShells.Stringify()
            json === aasJson

        [<Test>]
        member _.``AasShell: Json -> JObj -> {Xml, Json} conversion test`` () =
            let env = J.CreateIClassFromJson<Aas.Environment>(aasJson)
            let xml = env.ToXml()
            xml === aasXml


            let jsonObject = Aas.Jsonization.Serialize.ToJsonObject(env);
            let json = jsonObject.Stringify()
            json === aasJson

            env.ToJson() === aasJson


            let project = RtProject.FromJson(dsProject)
            let json = project.ToJson()
            json === dsJson

            //let json = project.ToJsonViaCode().Stringify()
            ()

        //[<Test>]
        //member _.``CoreToJsonViaCodeTest`` () =
        //    let system2 = DsSystem.FromJson(dsJson)
        //    let json = system2.ToJsonViaCode().Stringify()
        //    ()



        [<Test>]
        member _.``Edge: instance -> JObj -> Json -> Xml ConversionTest`` () =
            ()


        [<Test>]
        member _.``Action: instance -> JObj -> Json ConversionTest`` () =
            ()



        [<Test>]
        member _.``Work: instance -> JObj -> Json ConversionTest`` () =
            ()


        [<Test>]
        member _.``Flow: instance -> JObj -> Json ConversionTest`` () =
            ()


        [<Test>]
        member _.``X System: instance -> JObj(SMC) -> Json ConversionTest`` () =
            ()



        [<Test>]
        member _.``System: instance -> JObj(SM) -> Json ConversionTest`` () =

            let system2 = RtSystem.ImportFromJson(dsJson)
            let json = system2.ToSM().Stringify()
            //writeClipboard(json)

            let xml = J.CreateIClassFromJson<Aas.Submodel>(json).ToXml()

            ()


    /// Json Test
    type T2() =
        [<Test>]
        member _.``System: instance -> Aas Test`` () =
            let system2 = RtSystem.FromJson(dsJson)
            let smc = system2.ToSMC()
            ()
