namespace Ev2.Core.FS

open Dual.Common.Core.FS
open Newtonsoft.Json

module MiniSample =

    let create(): Project =
        let rtProject = Project.Create(Name = "MainProject")
        let rtSystem  =
            DsSystem.Create()
            |> tee (fun z ->
                z.Name <- "MainSystem"(*, IsPrototype=true*)
                z.IRI <- "http://example.com/ev2/system/main"
                )

        let rtApiDef1 = ApiDef.Create(Name = "ApiDef1a")
        let rtApiDef2 = ApiDef.Create() |> tee (fun z -> z.Name <- "UnusedApi")
        [rtApiDef1; rtApiDef2;] |> rtSystem.AddApiDefs

        let rtApiCall1a =
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
        let rtApiCall1b =
            ApiCall.Create()
            |> tee (fun z ->
                z.ApiDefGuid <- rtApiDef2.Guid
                z.Name      <- "ApiCall1b"
                z.InAddress <- "X0"
                z.OutAddress<- "Y1"
                z.InSymbol  <- "XTag2"
                z.OutSymbol <- "YTag2")
        [rtApiCall1a; rtApiCall1b] |> rtSystem.AddApiCalls

        let rtFlow =
            Flow.Create(Name = "MainFlow")
            |> tee (fun z ->
                z.AddButtons    [ DsButton   (Name="MyButton1")]
                z.AddLamps      [ Lamp     (Name="MyLamp1")]
                z.AddConditions [ DsCondition(Name="MyCondition1")]
                z.AddActions    [ DsAction   (Name="MyAction1")]
                )
        let rtWork1 =
            Work.Create()
            |> tee (fun z ->
                z.Name      <- "BoundedWork1"
                z.Status4   <- Some DbStatus4.Ready
                z.Motion    <- "Fast my motion"
                z.Parameter <- {|Name="kwak"; Company="dualsoft"; Room=510|} |> JsonConvert.SerializeObject)
        let rtWork2 =
            Work.Create()
            |> tee (fun z ->
                z.Name    <- "BoundedWork2"
                z.Status4 <- Some DbStatus4.Going
                z.Script  <- "My script"
                z.Flow    <- Some rtFlow)
        let rtWork3 =
            Work.Create()
            |> tee (fun z ->
                z.Name <- "FreeWork1"
                z.Status4 <- Some DbStatus4.Finished
                z.IsFinished<-true)

        [rtWork1; rtWork2; rtWork3] |> rtSystem.AddWorks
        [rtFlow] |> rtSystem.AddFlows

        let edArrowW =
            ArrowBetweenWorks(rtWork1, rtWork3, DbArrowType.Start, Name="Work 간 연결 arrow")
            |> tee (fun z ->
                z.Parameter <- {| ArrowWidth=2.1; ArrowHead="Diamond"; ArrowTail="Rectangle" |} |> JsonConvert.SerializeObject)
        [edArrowW] |> rtSystem.AddArrows


        let rtCall1a =
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

        let rtCall1b =
            Call.Create()
            |> tee (fun z ->
                z.Name <- "Call1b"
                z.Status4 <- Some DbStatus4.Finished
                z.CallType <- DbCallType.Repeat)

        rtWork1.AddCalls [rtCall1a; rtCall1b]
        let rtCall2a = Call.Create() |> tee (fun z -> z.Name <- "Call2a"; z.Status4 <- Some DbStatus4.Homing)
        let rtCall2b = Call.Create() |> tee (fun z -> z.Name <- "Call2b"; z.Status4 <- Some DbStatus4.Finished)
        rtWork2.AddCalls [rtCall2a; rtCall2b]
        rtProject.AddActiveSystem rtSystem
        rtFlow.AddWorks([rtWork1])

        let edArrow1 = ArrowBetweenCalls(rtCall1a, rtCall1b, DbArrowType.Start)
        rtWork1.AddArrows [edArrow1]
        let edArrow2 = ArrowBetweenCalls(rtCall2a, rtCall2b, DbArrowType.Reset)
        rtWork2.AddArrows [edArrow2]

        rtProject

