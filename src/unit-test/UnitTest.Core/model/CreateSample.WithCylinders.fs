namespace T

open Dual.Common.Base
open Dual.Common.UnitTest.FS
open Dual.Common.Core.FS

open Ev2.Core.FS

[<AutoOpen>]

module CreateSampleWithCylinderModule =
    (* Cylinder *)
    let mutable edSystemCyl = getNull<DsSystem>()
    let mutable edApiCall1aCyl = getNull<ApiCall>()
    let mutable edApiCall1bCyl = getNull<ApiCall>()
    let mutable edApiCall2aCyl = getNull<ApiCall>()
    let mutable edApiCall2bCyl = getNull<ApiCall>()
    let mutable edApiDef1Cyl  = getNull<ApiDef>()

    let mutable edFlowCyl     = getNull<Flow>()
    let mutable edWork1Cyl    = getNull<Work>()
    let mutable edWork2Cyl    = getNull<Work>()
    let mutable edCall1aCyl   = getNull<Call>()
    let mutable edCall1bCyl   = getNull<Call>()

    let createEditableSystemCylinder() =
        if isItNull edSystemCyl then
            edApiDef1Cyl <- ApiDef.Create(Name = "ApiDef1Cyl")
            edApiCall1aCyl <-
                ApiCall.Create()
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

            edSystemCyl  <-
                DsSystem.Create()
                |> tee (fun z ->
                    z.Name <- "Cylinder"
                    z.IRI  <- "urn:ev2:system:Cylinder"
                    z.Properties.Integer <- 999
                    z.Properties.Text <- "Hello Cylinder")
            edFlowCyl    <- Flow.Create(Name = "CylFlow")
            edWork1Cyl   <- Work.Create() |> tee (fun z -> z.Name <- "BoundedWork1")
            edWork2Cyl   <- Work.Create() |> tee (fun z -> z.Name <- "BoundedWork2")

            edFlowCyl.AddWorks [edWork1Cyl; edWork2Cyl;]
            edSystemCyl.AddWorks [edWork1Cyl; edWork2Cyl;]
            edSystemCyl.AddFlows [edFlowCyl]
            edSystemCyl.AddApiDefs [edApiDef1Cyl]
            edSystemCyl.AddApiCalls [edApiCall1aCyl]

            let edArrowW = ArrowBetweenWorks.Create(edWork1Cyl, edWork2Cyl, DbArrowType.Reset, Name="Cyl Work 간 연결 arrow")
            edSystemCyl.AddArrows [edArrowW]


            edCall1aCyl  <-
                Call.Create()
                |> tee(fun z ->
                    z.Name     <- "Call1a"
                    z.CallType <- DbCallType.Parallel
                    z.AutoConditions <- ApiCallValueSpecs()
                    z.CommonConditions <- ApiCallValueSpecs()
                    z.Timeout  <- Some 30
                    z.ApiCallGuids.AddRange [edApiCall1aCyl.Guid] )


            edCall1bCyl <-
                Call.Create()
                |> tee (fun z ->
                    z.Name <- "Call1bCyl"
                    z.CallType <- DbCallType.Repeat)

            edWork1Cyl.AddCalls [edCall1aCyl; edCall1bCyl]
            edFlowCyl.AddWorks([edWork1Cyl])

            let edArrow1 = ArrowBetweenCalls.Create(edCall1aCyl, edCall1bCyl, DbArrowType.Start)
            edWork1Cyl.AddArrows [edArrow1]

            edSystemCyl.EnumerateRtObjects()
            |> iter (fun edObj ->
                // 최초 생성시, DB 삽입 전이므로 Id 가 None 이어야 함
                edObj.Id.IsNone === true
            )
