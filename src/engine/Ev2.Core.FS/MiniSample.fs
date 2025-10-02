namespace Ev2.Core.FS

open Dual.Common.Base
open Dual.Common.Core.FS
open System
open System.Threading

[<AutoOpen>]
module MiniSample =
    let createCylinder(name:string) =
        let sys = DsSystem.Create(Name=name)

        let workAdv = Work.Create(Name="ADVANCE", Guid=Guid.Parse("10000000-0000-0000-0000-000000000000"), Period=2000, ExternalStart="CylAdvanceStart")
        let workRet = Work.Create(Name="RETURN",  Guid=Guid.Parse("20000000-0000-0000-0000-000000000000"), Period=2000, ExternalStart="CylReturnStart")
        let flow = Flow.Create(Name="CylFlow")
        flow.AddWorks [workAdv; workRet]

        sys.AddWorks [workAdv; workRet;]
        sys.AddFlows [flow]

        let apiDefAdv = ApiDef.Create(Name = "ApiDefADV", TxGuid=workAdv.Guid, RxGuid=workAdv.Guid, Guid=Guid.Parse("30000000-0000-0000-0000-000000000000"))
        let apiDefRet = ApiDef.Create(Name = "ApiDefRET", TxGuid=workRet.Guid, RxGuid=workRet.Guid, Guid=Guid.Parse("40000000-0000-0000-0000-000000000000"))

        sys      .Properties.Text       <- "Sample System Properties in mini sample"
        flow     .Properties.FlowMemo   <- "Sample Flow Properties in mini sample"
        workAdv  .Properties.WorkMemo   <- "Sample Work(ADV) Properties in mini sample"
        workRet  .Properties.WorkMemo   <- "Sample Work(RET) Properties in mini sample"
        apiDefAdv.Properties.ApiDefMemo <- "Sample ApiDef(ADV) Properties in mini sample"
        apiDefRet.Properties.ApiDefMemo <- "Sample ApiDef(RET) Properties in mini sample"


        sys.AddApiDefs [apiDefAdv; apiDefRet]

        let arrowW = ArrowBetweenWorks.Create(workAdv, workRet, DbArrowType.Reset, Name="Cyl Work 간 연결 arrow")
        sys.AddArrows [arrowW]

        sys.OnLoaded()
        sys

    /// Extension type 테스트를 위한 간단한 Project 생성
    let create() =
        // Project 생성
        let project = Project.Create(Name = "TestProject")
        project.Properties.ProjectMemo <- "Sample Project in mini sample"
        let cyl = createCylinder "Cylinder1"
        project.AddPassiveSystem cyl

        // DsSystem 생성 및 추가
        let system =
            DsSystem.Create()
            |> tee (fun s ->
                s.Name <- "TestSystem"
                s.IRI <- "http://example.com/test/system"
            )

        // Flow 생성
        let flow =
            Flow.Create(Name = "TestFlow")

        // UI 요소 생성
        let button1 = DsButton.Create(Name = "Button1")
        let lamp1 = Lamp.Create(Name = "Lamp1")

        // UI 요소들의 Flow 설정
        button1.Flows.Add(flow)
        lamp1.Flows.Add(flow)

        // Button에 IOTags 샘플 추가
        button1.IOTags <-
            let inTag = TagWithSpec<bool>("ButtonIn", "I0.0", ValueSpec<bool>.Single true)
            let outTag = TagWithSpec<bool>("ButtonOut", "Q0.0", ValueSpec<bool>.Single false)
            IOTagsWithSpec(inTag, outTag)


        // Polymorphic UI 요소들 생성 및 등록
        let rtPolyButton =
            DsButton.Create()
            |> tee (fun z ->
                let inTag = TagWithSpec<bool>("PolyButtonIn", "I0.2", ValueSpec<bool>.Single false)
                let outTag = TagWithSpec<bool>("PolyButtonOut", "Q0.2", ValueSpec<bool>.Single true)
                z.IOTags <- IOTagsWithSpec(inTag, outTag))

        let rtPolyLamp =
            Lamp.Create()
            |> tee (fun z ->
                let inTag = TagWithSpec<int>("PolyLampIn", "DB20.DBW0", TypedValue<int>(99), ValueSpec<int>.Single 128)
                let outTag = TagWithSpec<int>("PolyLampOut", "DB20.DBW2", ValueSpec<int>.Single 255)
                z.IOTags <- IOTagsWithSpec(inTag, outTag))

        let rtPolyCondition =
            DsCondition.Create()
            |> tee (fun z ->
                let inTag = TagWithSpec<string>("PolyConditionIn", "Condition.Input", ValueSpec<string>.Single "RUN")
                let outTag = TagWithSpec<string>("PolyConditionOut", "Condition.Output", ValueSpec<string>.Single "OK")
                z.IOTags <- IOTagsWithSpec(inTag, outTag))

        let rtPolyAction =
            DsAction.Create()
            |> tee (fun z ->
                let inTag = TagWithSpec<double>("PolyActionIn", "Action.Input", ValueSpec<double>.Single 1.5)
                let outTag = TagWithSpec<double>("PolyActionOut", "Action.Output", ValueSpec<double>.Single 2.5)
                z.IOTags <- IOTagsWithSpec(inTag, outTag))

        [   rtPolyButton :> JsonPolymorphic
            rtPolyLamp
            rtPolyCondition
            rtPolyAction ]
        |> iter system.AddEntitiy

        // Work 생성
        let work1 =
            Work.Create()
            |> tee (fun w ->
                w.Name <- "Work1"
                w.ExternalStart <- "StartCommand1"
                w.Status4 <- Some DbStatus4.Ready
            )

        let work2 =
            Work.Create()
            |> tee (fun w ->
                w.Name <- "Work2"
                w.ExternalStart <- "StartCommand2"
                w.Status4 <- Some DbStatus4.Going
            )

        // ApiDef 생성
        let apiDefAdv = cyl.ApiDefs |> find(fun ad -> ad.Name = "ApiDefADV")
        let apiDefRet = cyl.ApiDefs |> find(fun ad -> ad.Name = "ApiDefRET")
        apiDefAdv |> validateRuntime |> ignore

        // ApiCall 생성
        let apiCall =
            ApiCall.Create()
            |> tee (fun a ->
                a.Name <- "TestApiCall"
                a.ApiDefGuid <- apiDefAdv.Guid
                a.InAddress <- "X0"
                a.OutAddress <- "Y0"
                a.InSymbol <- "X0"  // InSymbol 추가 (NOT NULL constraint)
                a.OutSymbol <- "Y0" // OutSymbol 추가 (NOT NULL constraint)
                // ApiCall에 IOTags 샘플 추가
                a.IOTags <-
                    let inTag = TagWithSpec<int>("ApiCallIn", "DB10.DBW0", ValueSpec<int>.Single 25)
                    let outTag = TagWithSpec<int>("ApiCallOut", "DB10.DBW2", ValueSpec<int>.Single 50)
                    IOTagsWithSpec(inTag, outTag)
            )

        // Call 생성
        let call1 =
            Call.Create()
            |> tee (fun c ->
                c.Name <- "Call1"
                c.AutoConditions <- ApiCallValueSpecs([ApiCallValueSpec(apiCall, ValueSpec<int>.Single 1); ApiCallValueSpec(apiCall, Multiple [1.1; 2.2; 3.3])])
                c.Status4 <- Some DbStatus4.Ready
                c.CallType <- DbCallType.Parallel
            )

        let call2 =
            Call.Create()
            |> tee (fun c ->
                c.Name <- "Call2"
                c.Status4 <- Some DbStatus4.Going
                c.CallType <- DbCallType.Repeat
            )

        // Call을 Work에 추가
        [call1; call2] |> work1.AddCalls


        // System에 요소들 추가
        [ work1; work2] |> system.AddWorks
        [ work1; work2] |> flow.AddWorks
        [ flow] |> system.AddFlows
        [ apiCall] |> system.AddApiCalls
        [ apiCall] |> call1.AddApiCalls
        button1 |> system.AddEntitiy
        lamp1 |> system.AddEntitiy

        // Project에 System 추가
        project.AddActiveSystem system
        project.OnLoaded()

        // 상태 변경 후, property changed event handling 을 위한, 충분한 시간을 줌.
        System.Threading.Tasks.Task.Run(fun () -> project.ActiveSystems[0].Works[0].Calls[0].Status4 <- Some DbStatus4.Going) |> ignore
        if not (isInUnitTest()) then
            Thread.Sleep(1000)

        project |> validateRuntime
