module TestProject.UnitTest.Core

open NUnit.Framework
open Dual.EV2.Core

[<TestFixture>]
type ``Core Unit Tests`` () =

    [<Test>]
    member _.``프로젝트 생성시 이름과 초기 상태 확인`` () =
        let project = Project("projX")
        Assert.AreEqual("projX", project.Name)
        Assert.AreEqual(0, project.Systems.Count)
        Assert.AreEqual(0, project.SystemUsages.Count)

    [<Test>]
    member _.``System 생성시 이름과 프로젝트 연결 확인`` () =
        let project = Project("projY")
        let system = System("sysY", project)
        Assert.AreEqual("sysY", system.Name)
        Assert.AreSame(project, system.Project)

    [<Test>]
    member _.``Flow는 System과 이름 연결됨`` () =
        let project = Project("projF")
        let system = System("sysF", project)
        let flow = Flow("flowX", system)
        Assert.AreEqual("flowX", flow.Name)
        Assert.AreSame(system, flow.System)

    [<Test>]
    member _.``Work는 Flow, System에 속함`` () =
        let project = Project("projW")
        let system = System("sysW", project)
        let flow = Flow("flowW", system)
        let work = Work("WorkA", system, flow)
        Assert.AreEqual("WorkA", work.Name)
        Assert.AreSame(system, work.System)
        Assert.AreSame(flow, work.Flow)

    [<Test>]
    member _.``Call은 Work에 속하고 이름을 가진다`` () =
        let project = Project("projC")
        let system = System("sysC", project)
        let flow = Flow("flowC", system)
        let work = Work("WorkC", system, flow)
        let call = Call("CallA", work)
        Assert.AreEqual("CallA", call.Name)
        Assert.AreSame(work, call.Work)

    [<Test>]
    member _.``ApiCall은 Call과 ApiDef 연결한다`` () =
        let project = Project("projA")
        let sysA = System("sysA", project)
        let sysB = System("sysB", project)
        let flow = Flow("flowA", sysA)
        let work = Work("WorkA", sysA, flow)
        let call = Call("CallA", work)
        let apiDef = ApiDef("ApiX", sysB)
        let apiCall = ApiCall("ApiCallX", call, apiDef)
        Assert.AreEqual("ApiCallX", apiCall.Name)
        Assert.AreSame(call, apiCall.Call)
        Assert.AreSame(apiDef, apiCall.TargetApiDef)
