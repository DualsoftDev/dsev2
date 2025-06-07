namespace T

open Dual.Common.Base
open Dual.Common.UnitTest.FS
open Dual.Common.Core.FS

open Ev2.Core.FS

[<AutoOpen>]
module CreateSampleModule =

    let mutable edProject  = getNull<RtProject>()
    let mutable edSystem   = getNull<RtSystem>()
    let mutable edApiCall1a = getNull<RtApiCall>()
    let mutable edApiDef1  = getNull<RtApiDef>()
    let mutable edApiDef2  = getNull<RtApiDef>()

    let mutable edFlow     = getNull<RtFlow>()
    let mutable edWork1    = getNull<RtWork>()
    let mutable edWork2    = getNull<RtWork>()
    let mutable edWork3    = getNull<RtWork>()
    let mutable edCall1a   = getNull<RtCall>()
    let mutable edCall1b   = getNull<RtCall>()
    let mutable edCall2a   = getNull<RtCall>()
    let mutable edCall2b   = getNull<RtCall>()

    let createEditableProject() =
        if isItNull edProject then
            edProject <- RtProject.Create(Name = "MainProject")
            edSystem  <-
                RtSystem.Create()
                |> tee (fun z ->
                    z.Name <- "MainSystem"(*, IsPrototype=true*)
                    z.IRI <- "http://example.com/ev2/system/main"
                    )

            edApiDef1 <- RtApiDef.Create(Name = "ApiDef1a")
            edApiDef2 <- RtApiDef.Create() |> tee (fun z -> z.Name <- "UnusedApi")
            [edApiDef1; edApiDef2;] |> edSystem.AddApiDefs

            edApiCall1a <-
                RtApiCall.Create()
                |> tee (fun z ->
                    z.ApiDefGuid <- edApiDef1.Guid
                    z.Name      <- "ApiCall1a"
                    z.InAddress <- "InAddressX0"
                    z.OutAddress<- "OutAddress1"
                    z.InSymbol  <- "XTag1"
                    z.OutSymbol <- "YTag2"
                    z.ValueParameter <-
                        Some <| Ranges [
                            { Lower = None; Upper = Some (3.14, Open) }
                            { Lower = Some (5.0, Open); Upper = Some (6.0, Open) }
                            { Lower = Some (7.1, Closed); Upper = None }
                        ]
                    )
            [edApiCall1a] |> edSystem.AddApiCalls

            edFlow    <- RtFlow   (Name = "MainFlow")
            edWork1 <-
                RtWork.Create()
                |> tee (fun z ->
                    z.Name      <- "BoundedWork1"
                    z.Status4   <- Some DbStatus4.Ready
                    z.Motion    <- "Fast my motion"
                    z.Parameter <- {|Name="kwak"; Company="dualsoft"; Room=510|} |> EmJson.ToJson)
            edWork2 <-
                RtWork.Create()
                |> tee (fun z ->
                    z.Name    <- "BoundedWork2"
                    z.Status4 <- Some DbStatus4.Going
                    z.Script  <- "My script"
                    z.Flow    <- Some edFlow)
            edWork3 <-
                RtWork.Create()
                |> tee (fun z ->
                    z.Name <- "FreeWork1"
                    z.Status4 <- Some DbStatus4.Finished
                    z.IsFinished<-true)

            [edWork1; edWork2; edWork3] |> edSystem.AddWorks
            [edFlow] |> edSystem.AddFlows

            let edArrowW =
                RtArrowBetweenWorks(edWork1, edWork3, DbArrowType.Start, Name="Work 간 연결 arrow")
                |> tee (fun z ->
                    z.Parameter <- {| ArrowWidth=2.1; ArrowHead="Diamond"; ArrowTail="Rectangle" |} |> EmJson.ToJson)
            [edArrowW] |> edSystem.AddArrows


            edCall1a  <-
                RtCall.Create()
                |> tee(fun z ->
                    z.Name     <- "Call1a"
                    z.Status4  <- Some DbStatus4.Ready
                    z.CallType <- DbCallType.Parallel
                    z.AutoPre  <- "AutoPre 테스트 1"
                    z.Safety   <- "안전조건1"
                    z.Timeout  <- Some 30
                    z.Parameter <- {|Type="call"; Count=3; Pi=3.14|} |> EmJson.ToJson
                    z.ApiCallGuids.AddRange [edApiCall1a.Guid] )

            edCall1b  <-
                RtCall.Create()
                |> tee (fun z ->
                    z.Name <- "Call1b"
                    z.Status4 <- Some DbStatus4.Finished
                    z.CallType <- DbCallType.Repeat)

            edWork1.AddCalls [edCall1a; edCall1b]
            edCall2a  <- RtCall.Create() |> tee (fun z -> z.Name <- "Call2a"; z.Status4 <- Some DbStatus4.Homing)
            edCall2b  <- RtCall.Create() |> tee (fun z -> z.Name <- "Call2b"; z.Status4 <- Some DbStatus4.Finished)
            edWork2.AddCalls [edCall2a; edCall2b]
            edProject.AddActiveSystem edSystem
            edFlow.AddWorks([edWork1])

            let edArrow1 = RtArrowBetweenCalls(edCall1a, edCall1b, DbArrowType.Start)
            edWork1.AddArrows [edArrow1]
            let edArrow2 = RtArrowBetweenCalls(edCall2a, edCall2b, DbArrowType.Reset)
            edWork2.AddArrows [edArrow2]

            edProject.EnumerateRtObjects()
            |> iter (fun edObj ->
                // 최초 생성시, DB 삽입 전이므로 Id 가 None 이어야 함
                edObj.Id.IsNone === true
            )


