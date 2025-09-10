namespace T

open Dual.Common.Base
open Dual.Common.UnitTest.FS
open Dual.Common.Core.FS

open Ev2.Core.FS
open Newtonsoft.Json

[<AutoOpen>]
module CreateSampleModule =

    let mutable rtProject  = getNull<Project>()
    let mutable rtSystem   = getNull<DsSystem>()
    let mutable rtCylinder = getNull<DsSystem>()
    let mutable rtApiCall1a = getNull<ApiCall>()
    let mutable rtApiCall1b = getNull<ApiCall>()
    let mutable rtApiDef1  = getNull<ApiDef>()
    let mutable rtApiDef2  = getNull<ApiDef>()

    let mutable rtFlow     = getNull<Flow>()
    let mutable rtWork1    = getNull<Work>()
    let mutable rtWork2    = getNull<Work>()
    let mutable rtWork3    = getNull<Work>()
    let mutable rtCall1a   = getNull<Call>()
    let mutable rtCall1b   = getNull<Call>()
    let mutable rtCall2a   = getNull<Call>()
    let mutable rtCall2b   = getNull<Call>()

    let createEditableProject() =
        if isItNull rtProject then
            rtProject <- Project.Create(Name = "MainProject")
            rtCylinder <- MiniSample.createCylinder("Cylinder")

            rtSystem <- DsSystem.Create(Name = "MainSystem", IRI="http://example.com/ev2/system/main")

            rtApiDef1 <- rtCylinder.ApiDefs.Find(fun ad -> ad.Name = "ApiDefADV")
            rtApiDef2 <- rtCylinder.ApiDefs.Find(fun ad -> ad.Name = "ApiDefRET")

            let valueSpec =
                Ranges [
                    { Lower = None; Upper = Some (3.14, Open) }
                    { Lower = Some (5.0, Open); Upper = Some (6.0, Open) }
                    { Lower = Some (7.1, Closed); Upper = None }
                ]

            rtApiCall1a <-
                ApiCall.Create()
                |> tee (fun z ->
                    z.ApiDefGuid <- rtApiDef1.Guid
                    z.Name      <- "ApiCall1a"
                    z.InAddress <- "InAddressX0"
                    z.OutAddress<- "OutAddress1"
                    z.InSymbol  <- "XTag1"
                    z.OutSymbol <- "YTag2"
                    z.ValueSpec <- Some valueSpec
                    )
            rtApiCall1b <-
                ApiCall.Create()
                |> tee (fun z ->
                    z.ApiDefGuid <- rtApiDef2.Guid
                    z.Name      <- "ApiCall1b"
                    z.InAddress <- "X0"
                    z.OutAddress<- "Y1"
                    z.InSymbol  <- "XTag2"
                    z.OutSymbol <- "YTag2")
            [rtApiCall1a; rtApiCall1b] |> rtSystem.AddApiCalls

            rtFlow <-
                Flow.Create(Name = "MainFlow")

            // UI 요소들 생성
            let button1 = DsButton.Create(Name="MyButton1")
            let lamp1 = Lamp.Create(Name="MyLamp1")
            let condition1 = DsCondition.Create(Name="MyCondition1")
            let action1 = DsAction.Create(Name="MyAction1")

            // UI 요소들의 Flow 설정
            button1.FlowGuid <- Some rtFlow.Guid
            lamp1.FlowGuid <- Some rtFlow.Guid
            condition1.FlowGuid <- Some rtFlow.Guid
            action1.FlowGuid <- Some rtFlow.Guid

            // System에 UI 요소들 추가
            [button1] |> rtSystem.AddButtons
            [lamp1] |> rtSystem.AddLamps
            [condition1] |> rtSystem.AddConditions
            [action1] |> rtSystem.AddActions

            rtWork1 <-
                Work.Create()
                |> tee (fun z ->
                    z.Name      <- "BoundedWork1"
                    z.Status4   <- Some DbStatus4.Ready
                    z.Motion    <- "Fast my motion"
                    z.Parameter <- {|Name="kwak"; Company="dualsoft"; Room=510|} |> JsonConvert.SerializeObject)
            rtWork2 <-
                Work.Create()
                |> tee (fun z ->
                    z.Name    <- "BoundedWork2"
                    z.Status4 <- Some DbStatus4.Going
                    z.Script  <- "My script")

            rtWork3 <-
                Work.Create()
                |> tee (fun z ->
                    z.Name <- "FreeWork1"
                    z.Status4 <- Some DbStatus4.Finished
                    z.IsFinished<-true)

            [rtWork1; rtWork2; rtWork3] |> rtSystem.AddWorks
            [rtFlow] |> rtSystem.AddFlows

            let edArrowW =
                ArrowBetweenWorks.Create(rtWork1, rtWork3, DbArrowType.Start, Name="Work 간 연결 arrow")
                |> tee (fun z ->
                    z.Parameter <- {| ArrowWidth=2.1; ArrowHead="Diamond"; ArrowTail="Rectangle" |} |> JsonConvert.SerializeObject)
            [edArrowW] |> rtSystem.AddArrows


            rtCall1a  <-
                Call.Create()
                |> tee(fun z ->
                    z.Name     <- "Call1a"
                    z.Status4  <- Some DbStatus4.Ready
                    z.CallType <- DbCallType.Parallel
                    z.AutoConditions <- ApiCallValueSpecs([ApiCallValueSpec(rtApiCall1a, Single 999); ApiCallValueSpec(rtApiCall1b, Multiple [1.1; 2.2; 3.3]); ])
                    z.CommonConditions <- ApiCallValueSpecs()
                    z.Timeout  <- Some 30
                    z.Parameter <- {|Type="call"; Count=3; Pi=3.14|} |> JsonConvert.SerializeObject
                    z.ApiCallGuids.AddRange [rtApiCall1a.Guid] )

            rtCall1b  <-
                Call.Create()
                |> tee (fun z ->
                    z.Name <- "Call1b"
                    z.Status4 <- Some DbStatus4.Finished
                    z.CallType <- DbCallType.Repeat)

            rtWork1.AddCalls [rtCall1a; rtCall1b]
            rtCall2a  <- Call.Create() |> tee (fun z -> z.Name <- "Call2a"; z.Status4 <- Some DbStatus4.Homing)
            rtCall2b  <- Call.Create() |> tee (fun z -> z.Name <- "Call2b"; z.Status4 <- Some DbStatus4.Finished)
            rtWork2.AddCalls [rtCall2a; rtCall2b]
            rtProject.AddPassiveSystem rtCylinder
            rtProject.AddActiveSystem rtSystem
            rtFlow.AddWorks([rtWork1; rtWork2; rtWork3])

            let edArrow1 = ArrowBetweenCalls.Create(rtCall1a, rtCall1b, DbArrowType.Start)
            rtWork1.AddArrows [edArrow1]
            let edArrow2 = ArrowBetweenCalls.Create(rtCall2a, rtCall2b, DbArrowType.Reset)
            rtWork2.AddArrows [edArrow2]

            rtProject.EnumerateRtObjects()
            |> iter (fun edObj ->
                // 최초 생성시, DB 삽입 전이므로 Id 가 None 이어야 함
                edObj.Id.IsNone === true
            )


