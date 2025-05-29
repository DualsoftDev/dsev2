module TestProject.UnitTest.Sample

open NUnit.Framework
open Dual.EV2.Core
open Dual.EV2.JsonIO
open Dual.EV2.AasxIO
open Dual.EV2.RuntimeDB.RuntimeDB
open System.IO
open Program

[<TestFixture>]
type ``Sample Mapping Tests`` () =

    let exportPath name ext = $"{name}.{ext}"
    let cleanup files = files |> List.iter (fun f -> if File.Exists(f) then File.Delete(f))

    [<TearDown>]
    member _.Cleanup() = ()
        //cleanup [ "projB.json"; "projB_runtime.db"; "./aasx_projB/sys-003.aasx" ]

    [<Test>]
    member _.``프로젝트가 2개 생성됨`` () =
        let projects = createProjectsFromMapping ()
        Assert.AreEqual(2, projects.Length)

    [<Test>]
    member _.``projA는 sys-001과 sys-002를 포함함`` () =
        let projA = createProjectsFromMapping () |> Array.find (fun p -> p.Name = "projA")
        let names = projA.Systems |> Seq.map (fun s -> s.Name) |> Set.ofSeq
        Assert.IsTrue(names.Contains("sysA"))
        Assert.IsTrue(names.Contains("sysB"))

    [<Test>]
    member _.``projB는 sys-003을 포함하고 활성화됨`` () =
        let projB = createProjectsFromMapping () |> Array.find (fun p -> p.Name = "projB")
        Assert.Contains("sys-003", projB.TargetSystemIds)

    [<Test>]
    member _.``sys-003 내에 WorkA, WorkB 존재`` () =
        let projB = createProjectsFromMapping () |> Array.find (fun p -> p.Name = "projB")
        let sys3 = projB.Systems |> Seq.find (fun s -> s.Name = "sys-003")
        let works = sys3.Works |> Seq.map (fun w -> w.Name) |> Set.ofSeq
        Assert.IsTrue(works.Contains("WorkA"))
        Assert.IsTrue(works.Contains("WorkB"))

    [<Test>]
    member _.``WorkA는 CallA와 CallB를 포함함`` () =
        let projB = createProjectsFromMapping () |> Array.find (fun p -> p.Name = "projB")
        let workA =
            projB.Systems
            |> Seq.find (fun s -> s.Name = "sys-003")
            |> fun s -> s.Works |> Seq.find (fun w -> w.Name = "WorkA")
        let callNames = workA.Calls |> Seq.map (fun c -> c.Name) |> Set.ofSeq
        Assert.AreEqual(Set.ofList ["CallA"; "CallB"], callNames)

    [<Test>]
    member _.``CallA는 ApiCallA를 포함함`` () =
        let projB = createProjectsFromMapping () |> Array.find (fun p -> p.Name = "projB")
        let callA =
            projB.Systems
            |> Seq.find (fun s -> s.Name = "sys-003")
            |> fun s -> s.Works |> Seq.find (fun w -> w.Name = "WorkA")
            |> fun w -> w.Calls |> Seq.find (fun c -> c.Name = "CallA")
        Assert.AreEqual(1, callA.ApiCalls.Count)
        Assert.AreEqual("ApiCallA", callA.ApiCalls.[0].Name)

    [<Test>]
    member _.``CallB는 ApiCallB를 포함함`` () =
        let projB = createProjectsFromMapping () |> Array.find (fun p -> p.Name = "projB")
        let callB =
            projB.Systems
            |> Seq.find (fun s -> s.Name = "sys-003")
            |> fun s -> s.Works |> Seq.find (fun w -> w.Name = "WorkA")
            |> fun w -> w.Calls |> Seq.find (fun c -> c.Name = "CallB")
        Assert.AreEqual(1, callB.ApiCalls.Count)
        Assert.AreEqual("ApiCallB", callB.ApiCalls.[0].Name)

    [<Test>]
    member _.``WorkA는 CallA→CallB 엣지를 가진다`` () =
        let projB = createProjectsFromMapping () |> Array.find (fun p -> p.Name = "projB")
        let workA =
            projB.Systems
            |> Seq.find (fun s -> s.Name = "sys-003")
            |> fun s -> s.Works |> Seq.find (fun w -> w.Name = "WorkA")
        let edgeNames = workA.CallGraph |> Seq.map (fun (s, t) -> s.Name, t.Name) |> Seq.toArray 
        Assert.Contains(("CallA", "CallB"), edgeNames)

    [<Test>]
    member _.``WorkA→WorkB WorkGraph 존재함`` () =
        let projB = createProjectsFromMapping () |> Array.find (fun p -> p.Name = "projB")
        let sys3 = projB.Systems |> Seq.find (fun s -> s.Name = "sys-003")
        let edge = sys3.WorkArrows |> Seq.map (fun (s, t) -> s.Name, t.Name) |> Seq.toArray 
        Assert.Contains(("WorkA", "WorkB"), edge)

    [<Test>]
    member _.``saveToFile로 JSON 파일 저장됨`` () =
        let projB = createProjectsFromMapping () |> Array.find (fun p -> p.Name = "projB")
        let root = { Projects = [|projB|] }
        let path = exportPath projB.Name "json"
        JsonIO.saveToFile path root
        Assert.IsTrue(File.Exists(path))

    [<Test>]
    member _.``AASX 파일이 시스템 단위로 저장됨`` () =
        let projB = createProjectsFromMapping () |> Array.find (fun p -> p.Name = "projB")
        let outDir = "./aasx_projB"
        AASX.exportAllAASX projB outDir
        let path = Path.Combine(outDir, "sys-003.aasx")
        Assert.IsTrue(File.Exists(path))



    [<Test>]
    member _.``fromProject 변환 후 테이블 수 검증`` () =
        let projB = createProjectsFromMapping () |> Array.find (fun p -> p.Name = "projB")
        let db = fromProject projB
        Assert.AreEqual(projB.Systems.Count, db.Systems.Count)

    [<Test>]
    member _.``saveToSqlite로 DB 저장됨`` () =
        let projB = createProjectsFromMapping () |> Array.find (fun p -> p.Name = "projB")
        let dbPath = exportPath "projB_runtime" "db"
        initializeSchema dbPath
        let db = fromProject projB
        saveToSqlite db dbPath
        Assert.IsTrue(File.Exists(dbPath))

    [<Test>]
    member _.``sys-004 (sysD)는 ApiDef를 포함함`` () =
        let projB = createProjectsFromMapping () |> Array.find (fun p -> p.Name = "projB")
        let sysD = projB.Systems |> Seq.find (fun s -> s.Name = "sysD")
        Assert.GreaterOrEqual(sysD.ApiDefs.Count, 2)

    [<Test>]
    member _.``ApiCallA의 Target은 sysD에 포함됨`` () =
        let projB = createProjectsFromMapping () |> Array.find (fun p -> p.Name = "projB")
        let callA =
            projB.Systems
            |> Seq.find (fun s -> s.Name = "sys-003")
            |> fun s -> s.Works |> Seq.find (fun w -> w.Name = "WorkA")
            |> fun w -> w.Calls |> Seq.find (fun c -> c.Name = "CallA")
        let targetSys = callA.ApiCalls.[0].TargetApiDef.System
        Assert.AreEqual("sysD", targetSys.Name)

    [<Test>]
    member _.``sys-003는 flow-001을 포함함`` () =
        let projB = createProjectsFromMapping () |> Array.find (fun p -> p.Name = "projB")
        let sys3 = projB.Systems |> Seq.find (fun s -> s.Name = "sys-003")
        Assert.AreEqual(1, sys3.Flows.Count)
        Assert.AreEqual("flow-001", sys3.Flows.[0].Name)

    [<Test>]
    member _.``sysD는 project에 연결되어 있음`` () =
        let projB = createProjectsFromMapping () |> Array.find (fun p -> p.Name = "projB")
        let sysD = projB.Systems |> Seq.find (fun s -> s.Name = "sysD")
        Assert.AreSame(projB, sysD.Project)
