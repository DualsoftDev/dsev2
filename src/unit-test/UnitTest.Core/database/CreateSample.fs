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
    let mutable edApiDef1a  = getNull<RtApiDef>()

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
            edApiDef1a <- RtApiDef.Create(Name = "ApiDef1a")
            edApiCall1a <-
                RtApiCall.Create()
                |> tee (fun z ->
                    z.ApiDefGuid <- edApiDef1a.Guid
                    z.Name      <- "ApiCall1a"
                    z.InAddress <- "InAddressX0"
                    z.OutAddress<- "OutAddress1"
                    z.InSymbol  <- "XTag1"
                    z.OutSymbol <- "YTag2"
                    z.ValueType <- DbDataType.Bool
                    z.Value     <- "false")
            edSystem  <- RtSystem.Create() |> tee (fun z -> z.Name <- "MainSystem"(*, IsPrototype=true*))
            edFlow    <- RtFlow   (Name = "MainFlow")
            edWork1   <- RtWork.Create() |> tee (fun z -> z.Name <- "BoundedWork1")
            edWork2   <- RtWork.Create() |> tee (fun z -> z.Name <- "BoundedWork2"; z.OptFlow <- Some edFlow)
            edWork3   <- RtWork.Create() |> tee (fun z -> z.Name <- "FreeWork1")
            edSystem.Works.AddRange([edWork1; edWork2; edWork3])
            edSystem.Flows.Add(edFlow)
            edSystem.ApiDefs.Add(edApiDef1a)
            edSystem.ApiCalls.Add(edApiCall1a)

            let edArrowW = RtArrowBetweenWorks(edWork1, edWork3, DbArrowType.Start, Name="Work 간 연결 arrow")
            edSystem.Arrows.Add(edArrowW)

            edApiDef1a <- RtApiDef.Create() |> tee (fun z -> z.Name <- "ApiDef1a")
            edSystem.ApiDefs.Add(edApiDef1a)

            edCall1a  <-
                RtCall.Create()
                |> tee(fun z ->
                    z.Name     <- "Call1a"
                    z.CallType <- DbCallType.Parallel
                    z.AutoPre  <- "AutoPre 테스트 1"
                    z.Safety   <- "안전조건1"
                    z.Timeout  <- Some 30
                    z.ApiCallGuids.AddRange [edApiCall1a.Guid] )

            edCall1b  <-
                RtCall.Create()
                |> tee (fun z ->
                    z.Name <- "Call1b"
                    z.CallType <- DbCallType.Repeat)

            edWork1.Calls.AddRange([edCall1a; edCall1b])
            edCall2a  <- RtCall.Create() |> tee (fun z -> z.Name <- "Call2a")
            edCall2b  <- RtCall.Create() |> tee (fun z -> z.Name <- "Call2b")
            edWork2.Calls.AddRange([edCall2a; edCall2b])
            edProject.ActiveSystems.Add(edSystem)
            edFlow.AddWorks([edWork1])

            let edArrow1 = RtArrowBetweenCalls(edCall1a, edCall1b, DbArrowType.Start)
            edWork1.Arrows.Add(edArrow1)
            let edArrow2 = RtArrowBetweenCalls(edCall2a, edCall2b, DbArrowType.Reset)
            edWork2.Arrows.Add(edArrow2)

            edProject.Fix()

            edProject.EnumerateRtObjects()
            |> iter (fun edObj ->
                // 최초 생성시, DB 삽입 전이므로 Id 가 None 이어야 함
                edObj.Id.IsNone === true
            )


