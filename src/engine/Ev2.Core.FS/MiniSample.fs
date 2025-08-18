namespace Ev2.Core.FS

open Dual.Common.Base
open Dual.Common.Core.FS

[<AutoOpen>]
module MiniSample =

    /// Extension type 테스트를 위한 간단한 Project 생성
    let create() =
        // Project 생성
        let project = Project.Create(Name = "TestProject")

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
            |> tee (fun f ->
                f.AddButtons [ new DsButton(Name="Button1") ]
                f.AddLamps [ new Lamp(Name="Lamp1") ]
            )

        // Work 생성
        let work1 =
            Work.Create()
            |> tee (fun w ->
                w.Name <- "Work1"
                w.Status4 <- Some DbStatus4.Ready
            )

        let work2 =
            Work.Create()
            |> tee (fun w ->
                w.Name <- "Work2"
                w.Status4 <- Some DbStatus4.Going
                w.Flow <- Some flow
            )

        // Call 생성
        let call1 =
            Call.Create()
            |> tee (fun c ->
                c.Name <- "Call1"
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

        // ApiDef 생성
        let apiDef =
            ApiDef.Create(Name = "TestApiDef")

        // ApiCall 생성
        let apiCall =
            ApiCall.Create()
            |> tee (fun a ->
                a.Name <- "TestApiCall"
                a.ApiDefGuid <- apiDef.Guid
                a.InAddress <- "X0"
                a.OutAddress <- "Y0"
                a.InSymbol <- "X0"  // InSymbol 추가 (NOT NULL constraint)
                a.OutSymbol <- "Y0" // OutSymbol 추가 (NOT NULL constraint)
            )

        // System에 요소들 추가
        [work1; work2] |> system.AddWorks
        [flow] |> system.AddFlows
        [apiDef] |> system.AddApiDefs
        [apiCall] |> system.AddApiCalls

        // Project에 System 추가
        project.AddActiveSystem system

        project
