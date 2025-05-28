module TestProject.UnitTest.CoreExtension

open NUnit.Framework
open Dual.EV2.Core
open Dual.EV2.CoreExtension

[<TestFixture>]
type ``CoreExtension Unit Tests`` () =

    [<Test>]
    member _.``Call에 AutoPreCondition 추가 테스트`` () =
        let work = Work("Work1", System("sysX", Project("proj")), Flow("flow1", System("sysX", Project("proj"))))
        let call = Call("Call1", work)
        call.AddAutoPre("x > 0")
        Assert.AreEqual(1, call.Param.AutoPreConditions.Count)
        Assert.AreEqual("x > 0", call.Param.AutoPreConditions.[0])

    [<Test>]
    member _.``Call에 SafetyCondition 추가 테스트`` () =
        let work = Work("Work1", System("sysX", Project("proj")), Flow("flow1", System("sysX", Project("proj"))))
        let call = Call("Call1", work)
        call.AddSafety("y < 100")
        Assert.AreEqual(1, call.Param.SafetyConditions.Count)
        Assert.AreEqual("y < 100", call.Param.SafetyConditions.[0])

    [<Test>]
    member _.``Work에 Call 추가 테스트`` () =
        let system = System("sysY", Project("projY"))
        let flow = Flow("flowY", system)
        let work = Work("WorkY", system, flow)
        let call = Call("CallY", work)
        work.AddCall(call)
        Assert.AreEqual(1, work.Calls.Count)
        Assert.AreSame(call, work.Calls.[0])

    [<Test>]
    member _.``Work에 Call 간 엣지 추가 테스트`` () =
        let work = Work("WorkZ", System("sysZ", Project("projZ")), Flow("flowZ", System("sysZ", Project("projZ"))))
        let call1 = Call("Call1", work)
        let call2 = Call("Call2", work)
        work.AddCall(call1)
        work.AddCall(call2)
        work.AddCallEdge(call1, call2)
        Assert.AreEqual(1, work.CallGraph.Count)

    [<Test>]
    member _.``System에 Flow 추가 테스트`` () =
        let system = System("sysA", Project("projA"))
        let flow = Flow("flowA", system)
        system.AddFlow(flow)
        Assert.AreEqual(1, system.Flows.Count)
        Assert.AreSame(flow, system.Flows.[0])

    [<Test>]
    member _.``System에 Work 간 엣지 추가 테스트`` () =
        let system = System("sysB", Project("projB"))
        let flow = Flow("flowB", system)
        let work1 = Work("Work1", system, flow)
        let work2 = Work("Work2", system, flow)
        system.Works.Add(work1)
        system.Works.Add(work2)
        system.AddWorkEdge(work1, work2)
        Assert.AreEqual(1, system.WorkGraph.Count)

    [<Test>]
    member _.``Project에 System 추가 및 타겟 설정 테스트`` () =
        let project = Project("projMain")
        let system = System("sysMain", project)
        project.AddSystem(system)
        project.AddTargetSystem("sysMain")
        Assert.AreEqual(1, project.Systems.Count)
        Assert.AreEqual(1, project.TargetSystemIds.Count)
        Assert.AreEqual("sysMain", project.TargetSystemIds.[0])
