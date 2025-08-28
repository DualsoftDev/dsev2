namespace T.Core

open Dual.Ev2
open Dual.Ev2.Aas
open NUnit.Framework
open Dual.Common.UnitTest.FS
open Ev2.Core.FS
open Dual.Common.Base
open T
open T.TestHelpers  // 테스트 헬퍼 함수 추가
open System.IO
open Dual.Common.Core.FS


module ToAasTest =
    let dsJson = DsJson.dsJson
    let dsProject = DsJson.dsProject
    let aasJson = Aas.aasJson0
    let aasXml = Aas.aasXml0

    /// Json Test
    type T() = // ``AasShell: JObj -> string conversion test``, ``AasShell: Json -> JObj -> {Xml, Json} conversion test``
        [<Test>]
        member _.``AasShell: JObj -> string conversion test`` () =
            let assetInformation = JObj().Set(N.AssetKind, "NotApplicable").Set(N.GlobalAssetId, "something_eea66fa1")
            let assetAdministrationShell = JObj().Set(N.Id, "something_142922d6").Set(N.AssetInformation, assetInformation).Set(N.ModelType, "AssetAdministrationShell")
            let assetAdministrationShells = JObj().Set(N.AssetAdministrationShells, J.CreateJArr( [assetAdministrationShell] ))
            let json = assetAdministrationShells.Stringify()
            json =~= aasJson

        [<Test>]
        member _.``AasShell: Json -> JObj -> {Xml, Json} conversion test`` () =
            let env = J.CreateIClassFromJson<Aas.Environment>(aasJson)
            let xml = env.ToXml()
            xml =~= aasXml


            let jsonObject = Aas.Jsonization.Serialize.ToJsonObject(env);
            let json = jsonObject.Stringify()
            json =~= aasJson

            env.ToJson() =~= aasJson


            let project = Project.FromJson(dsProject)
            let json = project.ToJson()
            json =~= dsProject
            ()

    /// Json Test
    type T2() = // ``Project: instance -> Aas Test``, ``Project: instance -> Aas Test2``, ``Project with cylinder: instance -> Aas Test``, ``Hello DS -> Aasx file``
        [<Test>]
        member _.``Project: instance -> Aas Test`` () =
            let rtProject = dsProject |> Project.FromJson
            let xxx = rtProject.ToJson()
            let njProject = NjProject.FromJson dsProject

            let aasxPath = getUniqueAasxPath()
            njProject.ExportToAasxFile(aasxPath) |> ignore
            cleanupTestFile aasxPath  // 테스트 후 정리

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
        member _.``Project: instance -> Aas Test2`` () =
            let rtProject = dsProject |> Project.FromJson
            let dbApi = pgsqlDbApi()
            let aasxPath = getUniqueAasxPath()
            rtProject.ExportToAasxFile(aasxPath, dbApi) |> ignore
            cleanupTestFile aasxPath  // 테스트 후 정리

            ()


        [<Test>]
        member _.``Project with cylinder: instance -> Aas Test`` () =
            createEditableProject()
            createEditableSystemCylinder()
            let originalEdProject = rtProject
            let edProject = rtProject.Replicate() |> validateRuntime
            let rtCyl = rtProject.Systems |> find (fun s -> s.Name = "Cylinder")
            let edSysCyl1 = rtCyl.Duplicate(Name="실린더 instance1")
            let edSysCyl2 = rtCyl.Duplicate(Name="실린더 instance2")
            let edSysCyl3 = rtCyl.Duplicate(Name="실린더 instance3")
            [edSysCyl1; edSysCyl2; edSysCyl3] |> iter edProject.AddPassiveSystem

            let projJson = edProject.ToJson(getUniqueJsonPath())
            let njProject1 = NjProject.FromJson(projJson)
            let jNodeSM = njProject1.ToSjSubmodel()
            let aasJson = jNodeSM.Stringify()
            let submodel = aasJson |> J.CreateIClassFromJson<Aas.Submodel>
            let njProject2 = NjProject.FromISubmodel(submodel)
            let json2 = EmJson.ToJson(njProject2)

            projJson =~= json2


            let aasxPath = getUniqueAasxPath()
            // 먼저 파일 생성이 필요할 수 있음
            // njProject1.InjectToExistingAasxFile(aasxPath) |> ignore
            cleanupTestFile aasxPath  // 테스트 후 정리
            ()


        [<Test>]
        member _.``Hello DS -> Aasx file`` () =
            let prj = createHelloDS()
            let jsonPath = getUniqueJsonPath()
            let aasxPath = getUniqueAasxPath()
            let json = prj.ToJson(jsonPath)
            prj.ExportToAasxFile(aasxPath)
            cleanupTestFiles [jsonPath; aasxPath]  // 테스트 후 정리

            ()
