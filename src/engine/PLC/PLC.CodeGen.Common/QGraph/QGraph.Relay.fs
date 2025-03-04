namespace Dual.Core.QGraph

open System.Collections.Generic
open System.Diagnostics
open System.Runtime.CompilerServices
open FSharpPlus
open Dual.Common
open Dual.Core
open Dual.Core.Types
open Dual.Core.Types.Command


[<AutoOpen>]
module RelayMarkerPath =
    /// Relay Marker
    [<AbstractClass>]
    [<DebuggerDisplay("{ToText()}")>]
    type RelayMarker(id:int, location:IVertex, edge:IEdge, set:MemoryOnOffCondition, resetExtendables:MemoryOnOffCondition list) as this =
        do
            assert(resetExtendables |> List.length > 0)
        let mergedIds = HashSet<int>()
        let toTextHelper() =
            let m =
                if mergedIds.isNullOrEmpty() then ""
                else ", MergedIds=" + (mergedIds |> Seq.map string |> String.concat ",")
            sprintf "Loc=%A, Edge=%A, %s%s" this.Location this.Edge this.RelayConditionText m
        member x.Id = id
        member x.Set = set
        member val ResetExtendables = resetExtendables with get, set
        member x.Reset = x.ResetExtendables.[0]
        /// Relay marker 가 표시될 vertex 의 위치
        member x.Location = location
        /// this Relay marker 를 생성시킨 원인의 edge
        member x.Edge = edge
        /// None 값이 아니면, 다른 merged marker 구성에 참여한 marker 로 더 이상 쓰이지 않아야 한다.
        member val MergedToId:int option = None with get, set

        /// this relay marker 가 다른 relay marker 를 merge 한 결과일 때, merge 된 다른 relay 의 id 를 보관
        member x.MergedIds = mergedIds
        member x.RelayConditionText =
            sprintf "Set=%s, Reset=%s, ResetExtendables=%s"
                (x.Set.ToText()) (x.Reset.ToText())  (x.ResetExtendables |> Seq.map toString |> String.concat ",")
        /// 유일/유지 tagging 된(Discriminated Union) marker 의 id
        abstract member DuId: RMType<int> with get
        abstract ToText : unit -> string
        override x.ToString() = x.ToText()
        member x.ToTextHelper() = toTextHelper()
    /// 유일(Unique) 관련 Relay Marker
    and URM(id, location, edge, set, resetExtendables) =
        inherit RelayMarker(id, location, edge, set, resetExtendables)
        override x.DuId = Unique(id)
        override x.ToText() = sprintf "U(%d) %s" id (x.ToTextHelper())
    /// 유지(Sustainable) 관련 Relay Marker
    and SRM(id, location, edge, set, resetExtendables) =
        inherit RelayMarker(id, location, edge, set, resetExtendables)
        override x.DuId = Sustainable(id)
        override x.ToText() = sprintf "S(%d) %s" id (x.ToTextHelper())
    /// 동작(Moved) 관련 Relay Marker
    and MRM(id, location, edge, set, resetExtendables) =
        inherit RelayMarker(id, location, edge, set, resetExtendables)
        override x.DuId = Moved(id)
        override x.ToText() = sprintf "MV(%d) %s" id (x.ToTextHelper())


    and RMType<'a> =
    | Unique of 'a
    | Sustainable of 'a
    | Merged of 'a
    | Moved of 'a
    and TaggedId = RMType<int>

    and Relay = {
        Name    : string
        Set     : MemoryOnOffCondition
        Reset   : MemoryOnOffCondition
        /// Relay 를 생성한 원본 marker 정보 : debugging 용
        RelayMarker: RelayMarker

        Comments : string list
    }

    type ColiTerminalExpression(expr:Expression) =
        do
            match expr with
            | Terminal(_) -> ()
            | _ -> failwithlogf "This Not Terminal Expression"

        member val Terminal = expr
        member x.ToText() = expr.ToText()
        interface IExpressionTerminal with
            member x.ToText() = x.ToText()

    /// Rung 의 출력이 어디서부터 생성되었는지의 origin 정보
    type CoilOriginType =
        /// 신뢰 기반 relay 의 rung
        | Relay of Relay
        /// DAG 의 vertex 를 위한 rung.  동일 신호를 표현하는 vertex 가 복수개 존재가능하므로 list 를 가짐
        | Coil of ColiTerminalExpression
        | Function of IFunctionCommand
        | NotYetDefined

    /// PLC rung 생성을 위한 자료 구조
    type RungInfo = {
        Start: Expression
        /// Set 조건
        Set: Expression
        /// Reset 조건
        Reset: Expression
        /// 출력 등의 자기 유지
        Selfhold: Expression
        /// 수동 조건
        Manual: Expression
        /// 출력 interlock and/or 작업 완료 비접
        Interlock : Expression

        /// Rung 의 출력의 origin
        CoilOrigin : CoilOriginType

        /// Rung 의 comment.  라인 단위 복수개
        Comments : string list
    }
    let defaultExpressionInfo =
        let z = Expression.Zero
        { Start=z; Set=z; Reset=z; Selfhold=z; Manual=z; Interlock=z; CoilOrigin=NotYetDefined; Comments = [] }

    [<Extension>]
    type CoilOriginTypeExt =
        [<Extension>]
        static member toCoilOrigin (expr:Expression) =
            Coil(ColiTerminalExpression(expr))

        [<Extension>]
        static member fromPLCFunction (plcfunc:IFunctionCommand) =
            Function(plcfunc)

        [<Extension>]
        static member GetCoilTerminal(coilOrigin:CoilOriginType) =
            match coilOrigin with
            | Function(func) ->  func.TerminalEndTag
            | Relay(r) ->  RelayTag(r.Name) :> IExpressionTerminal
            | Coil(vs) ->
                match vs.Terminal with
                | Terminal(t) -> t
                |_-> failwith "Not support Expression coil"
            | NotYetDefined -> failwith "Not yet defined"

    [<Extension>] // type RungInfoExt =
    type RungInfoExt =
        [<Extension>]
        static member GetCoilTerminal(ri:RungInfo) =
            CoilOriginTypeExt.GetCoilTerminal ri.CoilOrigin
        [<Extension>]
        static member GetCoilName(ri:RungInfo) =
            RungInfoExt.GetCoilTerminal(ri) |> toText

    /// DAG 당 ladder 생성 정보
    type LadderInfo = {
        PrologComments: string list
        Rungs: RungInfo list
    }


    /// RelayMarker 작업 용 Workbook
    type RelayMarkerWorkbook = {
        Model : QgModel
        /// Edge 별 marking 된 id 들 : edge -> [id]
        Edge2IdsMap : MultiMap<IEdge, TaggedId>
        /// Vertex 별 markers : vertex -> [markers]
        Vertex2MarkersMap : MultiMap<IVertex, RelayMarker>
        /// Debugging purpose
        Id2EdgeMap : Dictionary<int, IEdge>
        /// 이미 처리된 markers.  marker 의 id 별로 처리 할 때에, 이미 merge 된 marker 와 동일 id 를 갖는 marker 들에 해당
        ProcessedMarkers : HashSet<RelayMarker>
        /// DAG 의 terminal 과 initial 을 연결하는 relay.  mutation 을 위해서 resizeArray 사용
        TerminalRelays : ResizeArray<Relay>
        /// id, Relay
        Id2RelayDic : Dictionary<int, Relay>
    }


    /// 모델의 모든 markers 반환
    let getAllMarkers (wb:RelayMarkerWorkbook) =
        wb.Vertex2MarkersMap.FlatValues |> List.ofSeq

    /// 주어진 marker [ms] 중에서 unprocessed 만 골라서 반환
    let getUnprocessedMarkers wb ms =
        ms
        |> List.differencePipe wb.ProcessedMarkers
        |> List.filter (fun m -> m.MergedToId.IsNone)

    /// 모든 unprocessed markers 를 반환
    let getAllUnprocessedMarkers wb =
        getAllMarkers wb |> getUnprocessedMarkers wb

    /// debugging 용 trace
    let traceMarkers header sep (ms:RelayMarker seq) =
        ms
        |> Seq.map toString
        |> String.concat sep
        |> logInfo "%s%s" header




    let getIdOfTag (tid:TaggedId) =
        match tid with
        | Unique(n)      -> n
        | Sustainable(n) -> n
        | Merged(n)      -> n
        | Moved(n)       -> n

    let isMoved (id:TaggedId) =
        match id with
        | Moved(i) -> true
        | _ -> false
