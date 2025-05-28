module TestProject.UnitTest.JsonIO

open NUnit.Framework
open System.IO
open Dual.EV2.JsonIO
open Dual.EV2.Core
open System.Collections.Generic
open Dual.EV2.JsonIO.JsonIO

[<TestFixture>]
type ``JsonIO Tests`` () =

    let testFilePath = "test_system.json"

    [<TearDown>]
    member _.Cleanup() =
        if File.Exists(testFilePath) then File.Delete(testFilePath)

    [<Test>]
    member _.``RawSystem 저장 및 로드 테스트`` () =
        let raw : RawSystem = {
            name = "sysTest"
            flows = [|
                { name = "flow1"
                  works = [|
                    { name = "WorkA"
                      calls = [|
                        { name = "CallA"; apiCalls = [|
                            { name = "ApiCallA"; targetApiDef = { name = "API1"; system = "sysX" } }
                        |] }
                      |]
                      callGraph = [| [| "CallA"; "CallB" |] |] }
                  |]
                  workGraph = [| [| "WorkA"; "WorkB" |] |] }
            |]
            apiDefs = [| { name = "API1"; system = "sysX" } |]
        }

        saveRawSystemToJson testFilePath raw
        Assert.IsTrue(File.Exists(testFilePath))

        let loaded = loadRawSystemFromJson testFilePath
        Assert.AreEqual("sysTest", loaded.name)
        Assert.AreEqual(1, loaded.flows.Length)
        Assert.AreEqual("flow1", loaded.flows.[0].name)



    [<Test>]
    member _.``System 로드 시 누락된 Call 자동 생성됨`` () =
        let filePath = "temp_system.json"
        let raw : RawSystem = {
            name = "sysJson"
            flows = [|
                { name = "flow1"
                  works = [|
                    { name = "WorkA"
                      calls = [||]
                      callGraph = [| [| "A"; "B" |] |] }
                  |]
                  workGraph = [||] }
            |]
            apiDefs = [||]
        }

        saveRawSystemToJson filePath raw

        let project = Project("projX")
        let systemDic = Dictionary<string, System>()
        let sys = JsonIO.loadSystemFromJson filePath "sysJson" project systemDic

        let work = sys.Works |> Seq.find (fun w -> w.Name = "WorkA")
        let callNames = work.Calls |> Seq.map (fun c -> c.Name) |> Set.ofSeq
        Assert.IsTrue(callNames.Contains "A")
        Assert.IsTrue(callNames.Contains "B")

        File.Delete filePath
