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
    let mutable rtPolyButton = getNull<DsButton>()
    let mutable rtPolyLamp = getNull<Lamp>()
    let mutable rtPolyCondition = getNull<DsCondition>()
    let mutable rtPolyAction = getNull<DsAction>()

    let createEditableProject() =
        if isItNull rtProject then
            rtProject <- Project.Create(Name = "MainProject")
            rtCylinder <- MiniSample.createCylinder("Cylinder")
            rtCylinder.Properties.Text  <- "Hello Cylinder"


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
            button1.Flows.Add rtFlow
            lamp1.Flows.Add rtFlow
            condition1.Flows.Add rtFlow
            action1.Flows.Add rtFlow

            // System에 UI 요소들 추가
            [button1 :> BLCABase; lamp1; condition1; action1] |> rtSystem.AddEntities

            // Polymorphic UI 요소들 생성 및 등록
            rtPolyButton <-
                DsButton.Create()
                |> tee (fun z ->
                    let inTag = TagWithSpec<bool>("PolyButtonIn", "I0.2", ValueSpec<bool>.Single false)
                    let outTag = TagWithSpec<bool>("PolyButtonOut", "Q0.2", ValueSpec<bool>.Single true)
                    z.IOTags <- IOTagsWithSpec(inTag, outTag))

            rtPolyLamp <-
                Lamp.Create()
                |> tee (fun z ->
                    let inTag = TagWithSpec<int>("PolyLampIn", "DB20.DBW0", ValueSpec<int>.Single 128)
                    let outTag = TagWithSpec<int>("PolyLampOut", "DB20.DBW2", ValueSpec<int>.Single 255)
                    z.IOTags <- IOTagsWithSpec(inTag, outTag))

            rtPolyCondition <-
                DsCondition.Create()
                |> tee (fun z ->
                    let inTag = TagWithSpec<string>("PolyConditionIn", "Condition.Input", ValueSpec<string>.Single "RUN")
                    let outTag = TagWithSpec<string>("PolyConditionOut", "Condition.Output", ValueSpec<string>.Single "OK")
                    z.IOTags <- IOTagsWithSpec(inTag, outTag))

            rtPolyAction <-
                DsAction.Create()
                |> tee (fun z ->
                    let inTag = TagWithSpec<double>("PolyActionIn", "Action.Input", ValueSpec<double>.Single 1.5)
                    let outTag = TagWithSpec<double>("PolyActionOut", "Action.Output", ValueSpec<double>.Single 2.5)
                    z.IOTags <- IOTagsWithSpec(inTag, outTag))

            [ rtPolyButton :> BLCABase
              rtPolyLamp :> BLCABase
              rtPolyCondition :> BLCABase
              rtPolyAction :> BLCABase ]
            |> iter rtSystem.AddEntitiy

            rtWork1 <-
                Work.Create()
                |> tee (fun z ->
                    z.Name      <- "BoundedWork1"
                    z.Status4   <- Some DbStatus4.Ready
                    z.Properties.Motion <- "Fast my motion"
                    z.Parameter <- {|Name="kwak"; Company="dualsoft"; Room=510|} |> JsonConvert.SerializeObject)
            rtWork2 <-
                Work.Create()
                |> tee (fun z ->
                    z.Name    <- "BoundedWork2"
                    z.Status4 <- Some DbStatus4.Going
                    z.Properties.Script  <- "My script")

            rtWork3 <-
                Work.Create()
                |> tee (fun z ->
                    z.Name <- "FreeWork1"
                    z.Status4 <- Some DbStatus4.Finished
                    z.Properties.IsFinished <- true)

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
                    z.Properties.CallType <- DbCallType.Parallel
                    z.AutoConditions <- ApiCallValueSpecs([ApiCallValueSpec(rtApiCall1a, Single 999); ApiCallValueSpec(rtApiCall1b, Multiple [1.1; 2.2; 3.3]); ])
                    z.CommonConditions <- ApiCallValueSpecs()
                    z.Properties.Timeout  <- Some 30
                    z.Parameter <- {|Type="call"; Count=3; Pi=3.14|} |> JsonConvert.SerializeObject
                    z.Properties.ApiCallGuids.AddRange [rtApiCall1a.Guid] )

            rtCall1b  <-
                Call.Create()
                |> tee (fun z ->
                    z.Name <- "Call1b"
                    z.Status4 <- Some DbStatus4.Finished
                    z.Properties.CallType <- DbCallType.Repeat)

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
