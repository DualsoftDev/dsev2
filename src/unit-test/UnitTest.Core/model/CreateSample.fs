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

            rtApiCall1a <-
                ApiCall.Create()
                |> tee (fun z ->
                    z.ApiDefGuid <- rtApiDef1.Guid
                    z.Name      <- "ApiCall1a"
                    z.InAddress <- "InAddressX0"
                    z.OutAddress<- "OutAddress1"
                    z.InSymbol  <- "XTag1"
                    z.OutSymbol <- "YTag2"
                    z.ValueSpec <-
                        Some <| Ranges [
                            { Lower = None; Upper = Some (3.14, Open) }
                            { Lower = Some (5.0, Open); Upper = Some (6.0, Open) }
                            { Lower = Some (7.1, Closed); Upper = None }
                        ]
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
                |> tee (fun z ->
                    z.AddButtons    [ new DsButton(Name="MyButton1")]
                    z.AddLamps      [ new Lamp(Name="MyLamp1")]
                    z.AddConditions [ new DsCondition(Name="MyCondition1")]
                    z.AddActions    [ new DsAction(Name="MyAction1")]
                    )
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
                    z.Script  <- "My script"
                    z.Flow    <- Some rtFlow)
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
                    z.AutoConditions.AddRange ["AutoPre 테스트 1"; "AutoConditions 테스트 2"]
                    z.CommonConditions.AddRange ["안전조건1"; "안전조건2"; ]
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
            rtFlow.AddWorks([rtWork1])

            let edArrow1 = ArrowBetweenCalls.Create(rtCall1a, rtCall1b, DbArrowType.Start)
            rtWork1.AddArrows [edArrow1]
            let edArrow2 = ArrowBetweenCalls.Create(rtCall2a, rtCall2b, DbArrowType.Reset)
            rtWork2.AddArrows [edArrow2]

            rtProject.EnumerateRtObjects()
            |> iter (fun edObj ->
                // 최초 생성시, DB 삽입 전이므로 Id 가 None 이어야 함
                edObj.Id.IsNone === true
            )


