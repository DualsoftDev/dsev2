namespace T

open Dual.Common.Base
open Dual.Common.UnitTest.FS
open Dual.Common.Core.FS

open Ev2.Core.FS

[<AutoOpen>]
module CreateSampleModule =

    let mutable edProject  = getNull<EdProject>()
    let mutable edSystem   = getNull<EdSystem>()
    let mutable edApiCall1a = getNull<EdApiCall>()
    let mutable edApiDef1a  = getNull<EdApiDef>()

    let mutable edFlow     = getNull<EdFlow>()
    let mutable edWork1    = getNull<EdWork>()
    let mutable edWork2    = getNull<EdWork>()
    let mutable edWork3    = getNull<EdWork>()
    let mutable edCall1a   = getNull<EdCall>()
    let mutable edCall1b   = getNull<EdCall>()
    let mutable edCall2a   = getNull<EdCall>()
    let mutable edCall2b   = getNull<EdCall>()

    let createEditableProject() =
        if isItNull edProject then
            edProject <- EdProject(Name = "MainProject")
            edApiDef1a <- EdApiDef(Name = "ApiDef1a")
            edApiCall1a <- EdApiCall(edApiDef1a.Guid, Name = "ApiCall1a", InAddress="InAddressX0", OutAddress="OutAddress1", InSymbol="XTag1", OutSymbol="YTag2", ValueType=DbDataType.Bool, Value="false")
            edSystem  <- EdSystem (Name = "MainSystem"(*, IsPrototype=true*))
            edFlow    <- EdFlow   (Name = "MainFlow")
            edWork1   <- EdWork   (Name = "BoundedWork1")
            edWork2   <- EdWork   (Name = "BoundedWork2", OptFlow=Some edFlow)
            edWork3   <- EdWork   (Name = "FreeWork1")
            edSystem.Works.AddRange([edWork1; edWork2; edWork3])
            edSystem.Flows.Add(edFlow)
            edSystem.ApiDefs.Add(edApiDef1a)
            edSystem.ApiCalls.Add(edApiCall1a)

            let edArrowW = EdArrowBetweenWorks(edWork1, edWork3, DbArrowType.Start, Name="Work 간 연결 arrow")
            edSystem.Arrows.Add(edArrowW)

            edApiDef1a <- EdApiDef(Name = "ApiDef1a")
            edSystem.ApiDefs.Add(edApiDef1a)

            edCall1a  <-
                EdCall (Name = "Call1a", CallType=DbCallType.Parallel, AutoPre="AutoPre 테스트 1", Safety="안전조건1", Timeout=Some 30)
                |> tee(fun z -> z.AddApiCalls [edApiCall1a])

            edCall1b  <- EdCall (Name = "Call1b", CallType=DbCallType.Repeat)
            edWork1.Calls.AddRange([edCall1a; edCall1b])
            edCall2a  <- EdCall (Name = "Call2a")
            edCall2b  <- EdCall (Name = "Call2b")
            edWork2.Calls.AddRange([edCall2a; edCall2b])
            edProject.ActiveSystems.Add(edSystem)
            edFlow.AddWorks([edWork1])

            let edArrow1 = EdArrowBetweenCalls(edCall1a, edCall1b, DbArrowType.Start)
            edWork1.Arrows.Add(edArrow1)
            let edArrow2 = EdArrowBetweenCalls(edCall2a, edCall2b, DbArrowType.Reset)
            edWork2.Arrows.Add(edArrow2)

            edProject.Fix()

            edProject.EnumerateEdObjects()
            |> iter (fun edObj ->
                // 최초 생성시, DB 삽입 전이므로 Id 가 None 이어야 함
                edObj.Id.IsNone === true
            )


