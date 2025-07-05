namespace T.Core

open Dual.Ev2
open Dual.Ev2.Aas
open NUnit.Framework
open Dual.Common.UnitTest.FS
open Ev2.Core.FS
open Dual.Common.Base
open T
open System.IO


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
            json =~= dsProject

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

            let njSystem = NjSystem.ImportFromJson dsJson
            let json = njSystem.ToSjSubmodel().Stringify()
            //writeClipboard(json)

            let xml = J.CreateIClassFromJson<Aas.Submodel>(json).ToXml()

            ()


    /// Json Test
    type T2() =
        [<Test>]
        member _.``System: instance -> Aas Test`` () =
            //let njSystem = dsJson |> RtSystem.ImportFromJson |> NjSystem.fromRuntime
            let njSystem = NjSystem.ImportFromJson dsJson

            let smCall = njSystem.Works[0].Calls[0].ToSjSMC()
            let jsonCall = smCall.Stringify()
            let submodelCall = J.CreateIClassFromJson<Aas.SubmodelElementCollection>(smCall.ToJsonString())
            ()


            let jSm:JNode = njSystem.ToSjSubmodel()
            let aasJson = jSm.Stringify()
            let submodel = J.CreateIClassFromJson<Aas.Submodel>(aasJson)
            let aasXml = submodel.ToXml()

            let njSystem2 = NjSystem.FromISubmodel(submodel)




            let json2 = EmJson.ToJson(njSystem2)

            let rtSystem = RtSystem.ImportFromJson(json2)

            ()
        [<Test>]
        member _.``Project: instance -> Aas Test`` () =
            let rtProject = dsProject |> RtProject.FromJson
            let xxx = rtProject.ToJson()
            let njProject = NjProject.FromJson dsProject

            njProject.ExportToAasxFile("test.aasx") |> ignore

            let envJson:string = njProject.ToAasJsonStringENV()

            let jSm:JNode = njProject.ToSjSubmodel()
            let aasJson = jSm.Stringify()
            let submodel = J.CreateIClassFromJson<Aas.Submodel>(aasJson)
            let aasXml = submodel.ToXml()

            let njProject2 = NjProject.FromISubmodel(submodel)

            let json2 = EmJson.ToJson(njProject2)

            let rtProject2 = RtProject.FromJson(json2)

            ()


        [<Test>]
        member _.``Project with cylinder: instance -> Aas Test`` () =
            createEditableProject()
            createEditableSystemCylinder()
            let originalEdProject = edProject
            let edProject = edProject.Replicate() |> validateRuntime
            let prototype = edProject.AddMyPrototypeSystem edSystemCyl
            let edSysCyl1 = edProject.Instantiate(prototype, Name="실린더 instance1")
            let edSysCyl2 = edProject.Instantiate(prototype, Name="실린더 instance2")
            let edSysCyl3 = edProject.Instantiate(prototype, Name="실린더 instance3")

            let projJson = edProject.ToJson(Path.Combine(testDataDir(), "project.json"))
            let njProject1 = NjProject.FromJson(projJson)
            let jNodeSM = njProject1.ToSjSubmodel()
            let aasJson = jNodeSM.Stringify()
            let submodel = aasJson |> J.CreateIClassFromJson<Aas.Submodel>
            let njProject2 = NjProject.FromISubmodel(submodel)
            let json2 = EmJson.ToJson(njProject2)

            projJson === json2
            ()
