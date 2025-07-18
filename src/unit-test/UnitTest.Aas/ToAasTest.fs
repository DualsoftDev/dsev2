namespace T.Core

open Dual.Ev2
open Dual.Ev2.Aas
open NUnit.Framework
open Dual.Common.UnitTest.FS
open Ev2.Core.FS
open Dual.Common.Base
open T
open System.IO
open Dual.Common.Core.FS


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


            let project = Project.FromJson(dsProject)
            let json = project.ToJson()
            json =~= dsProject
            ()

    /// Json Test
    type T2() =
        [<Test>]
        member _.``Project: instance -> Aas Test`` () =
            let rtProject = dsProject |> Project.FromJson
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

            let rtProject2 = Project.FromJson(json2)

            ()


        [<Test>]
        member _.``Project with cylinder: instance -> Aas Test`` () =
            createEditableProject()
            createEditableSystemCylinder()
            let originalEdProject = edProject
            let edProject = edProject.Replicate() |> validateRuntime
            let edSysCyl1 = edSystemCyl.Duplicate(Name="실린더 instance1")
            let edSysCyl2 = edSystemCyl.Duplicate(Name="실린더 instance2")
            let edSysCyl3 = edSystemCyl.Duplicate(Name="실린더 instance3")
            [edSysCyl1; edSysCyl2; edSysCyl3] |> iter edProject.AddPassiveSystem

            let projJson = edProject.ToJson(Path.Combine(testDataDir(), "project.json"))
            let njProject1 = NjProject.FromJson(projJson)
            let jNodeSM = njProject1.ToSjSubmodel()
            let aasJson = jNodeSM.Stringify()
            let submodel = aasJson |> J.CreateIClassFromJson<Aas.Submodel>
            let njProject2 = NjProject.FromISubmodel(submodel)
            let json2 = EmJson.ToJson(njProject2)

            projJson === json2


            njProject1.InjectToExistingAasxFile("test.aasx") |> ignore
            //njProject1.InjectToExistingAasxFile("04_PLC_통신_r5.aasx") |> ignore
            ()
