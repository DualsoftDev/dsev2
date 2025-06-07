namespace T

open Dual.Common.Base
open Dual.Common.UnitTest.FS
open Dual.Common.Core.FS

open Ev2.Core.FS

[<AutoOpen>]

module CreateSampleWithCylinderModule =
    (* Cylinder *)
    let mutable edSystemCyl = getNull<RtSystem>()
    let mutable edApiCall1aCyl = getNull<RtApiCall>()
    let mutable edApiCall1bCyl = getNull<RtApiCall>()
    let mutable edApiCall2aCyl = getNull<RtApiCall>()
    let mutable edApiCall2bCyl = getNull<RtApiCall>()
    let mutable edApiDef1Cyl  = getNull<RtApiDef>()

    let mutable edFlowCyl     = getNull<RtFlow>()
    let mutable edWork1Cyl    = getNull<RtWork>()
    let mutable edWork2Cyl    = getNull<RtWork>()
    let mutable edCall1aCyl   = getNull<RtCall>()
    let mutable edCall1bCyl   = getNull<RtCall>()

    let createEditableSystemCylinder() =
        if isItNull edSystemCyl then
            edApiDef1Cyl <- RtApiDef.Create(Name = "ApiDef1Cyl")
            edApiCall1aCyl <-
                RtApiCall.Create()
                |> tee (fun z ->
                    z.ApiDefGuid <- edApiDef1Cyl.Guid
                    z.Name       <- "ApiCall1aCyl"
                    z.InAddress  <- "InAddressX0"
                    z.OutAddress <- "OutAddress1"
                    z.InSymbol   <- "XTag1"
                    z.OutSymbol  <- "YTag2"
                    z.ValueSpec <-
                        Some <| Multiple [1; 2; 3]
                    )

            edSystemCyl  <- RtSystem.Create() |> tee (fun z -> z.Name <- "Cylinder")
            edFlowCyl    <- RtFlow   (Name = "CylFlow")
            edWork1Cyl   <- RtWork.Create() |> tee (fun z -> z.Name <- "BoundedWork1")
            edWork2Cyl   <- RtWork.Create() |> tee (fun z -> z.Name <- "BoundedWork2"; z.Flow <- Some edFlowCyl)

            edSystemCyl.AddWorks [edWork1Cyl; edWork2Cyl;]
            edSystemCyl.AddFlows [edFlowCyl]
            edSystemCyl.AddApiDefs [edApiDef1Cyl]
            edSystemCyl.AddApiCalls [edApiCall1aCyl]

            let edArrowW = RtArrowBetweenWorks(edWork1Cyl, edWork2Cyl, DbArrowType.Reset, Name="Cyl Work 간 연결 arrow")
            edSystemCyl.AddArrows [edArrowW]

            edApiDef1Cyl <- RtApiDef.Create(Name = "ApiDef1Cyl")
            edSystemCyl.AddApiDefs [edApiDef1Cyl]

            edCall1aCyl  <-
                RtCall.Create()
                |> tee(fun z ->
                    z.Name     <- "Call1a"
                    z.CallType <- DbCallType.Parallel
                    z.AutoConditions  .Add "AutoPre 테스트 1"
                    z.CommonConditions.Add "안전조건1"
                    z.Timeout  <- Some 30
                    z.ApiCallGuids.AddRange [edApiCall1aCyl.Guid] )


            edCall1bCyl <-
                RtCall.Create()
                |> tee (fun z ->
                    z.Name <- "Call1bCyl"
                    z.CallType <- DbCallType.Repeat)

            edWork1Cyl.AddCalls [edCall1aCyl; edCall1bCyl]
            edFlowCyl.AddWorks([edWork1Cyl])

            let edArrow1 = RtArrowBetweenCalls(edCall1aCyl, edCall1bCyl, DbArrowType.Start)
            edWork1Cyl.AddArrows [edArrow1]

            edSystemCyl.EnumerateRtObjects()
            |> iter (fun edObj ->
                // 최초 생성시, DB 삽입 전이므로 Id 가 None 이어야 함
                edObj.Id.IsNone === true
            )

