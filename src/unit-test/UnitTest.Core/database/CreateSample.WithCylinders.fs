namespace T

open Dual.Common.Base
open Dual.Common.UnitTest.FS
open Dual.Common.Core.FS

open Ev2.Core.FS

[<AutoOpen>]

module CreateSampleWithCylinderModule =
    (* Cylinder *)
    let mutable edSystemCyl = getNull<EdSystem>()
    let mutable edApiCall1aCyl = getNull<EdApiCall>()
    let mutable edApiCall1bCyl = getNull<EdApiCall>()
    let mutable edApiCall2aCyl = getNull<EdApiCall>()
    let mutable edApiCall2bCyl = getNull<EdApiCall>()
    let mutable edApiDef1Cyl  = getNull<EdApiDef>()

    let mutable edFlowCyl     = getNull<EdFlow>()
    let mutable edWork1Cyl    = getNull<EdWork>()
    let mutable edWork2Cyl    = getNull<EdWork>()
    let mutable edCall1aCyl   = getNull<EdCall>()
    let mutable edCall1bCyl   = getNull<EdCall>()

    let createEditableSystemCylinder() =
        if isItNull edSystemCyl then
            edApiDef1Cyl <- EdApiDef(Name = "ApiDef1Cyl")
            edApiCall1aCyl <- EdApiCall(edApiDef1Cyl.Guid, Name = "ApiCall1aCyl", InAddress="InAddressX0", OutAddress="OutAddress1", InSymbol="XTag1", OutSymbol="YTag2", ValueType=DbDataType.Bool, Value="false")
            edSystemCyl  <- EdSystem (Name = "Cylinder"(*, IsSaveAsReference=true*)  (*, IsPrototype=true*))
            edFlowCyl    <- EdFlow   (Name = "CylFlow")
            edWork1Cyl   <- EdWork   (Name = "BoundedWork1")
            edWork2Cyl   <- EdWork   (Name = "BoundedWork2", OptFlow=Some edFlowCyl)

            edSystemCyl.Works.AddRange([edWork1Cyl; edWork2Cyl;])
            edSystemCyl.Flows.Add(edFlowCyl)
            edSystemCyl.ApiDefs.Add(edApiDef1Cyl)
            edSystemCyl.ApiCalls.Add(edApiCall1aCyl)

            let edArrowW = EdArrowBetweenWorks(edWork1Cyl, edWork2Cyl, DbArrowType.Reset, Name="Cyl Work 간 연결 arrow")
            edSystemCyl.Arrows.Add(edArrowW)

            edApiDef1Cyl <- EdApiDef(Name = "ApiDef1Cyl")
            edSystemCyl.ApiDefs.Add(edApiDef1Cyl)

            edCall1aCyl  <-
                EdCall (Name = "Call1a", CallType=DbCallType.Parallel, AutoPre="AutoPre 테스트 1", Safety="안전조건1", Timeout=Some 30)
                |> tee(fun z -> z.AddApiCalls [edApiCall1aCyl])

            edCall1bCyl  <- EdCall (Name = "Call1bCyl", CallType=DbCallType.Repeat)
            edWork1Cyl.Calls.AddRange([edCall1aCyl; edCall1bCyl])
            edFlowCyl.AddWorks([edWork1Cyl])

            let edArrow1 = EdArrowBetweenCalls(edCall1aCyl, edCall1bCyl, DbArrowType.Start)
            edWork1Cyl.Arrows.Add(edArrow1)

            edSystemCyl.Fix()

            edSystemCyl.EnumerateEdObjects()
            |> iter (fun edObj ->
                // 최초 생성시, DB 삽입 전이므로 Id 가 None 이어야 함
                edObj.Id.IsNone === true
            )

