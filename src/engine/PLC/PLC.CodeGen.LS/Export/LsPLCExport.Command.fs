namespace PLC.CodeGen.LS

open System.Linq

open PLC.CodeGen.Common
open PLC.CodeGen.LS.Config.POU.Program.LDRoutine
open Dual.Common.Core.FS
open Engine.Core
open FB
open ConvertorPrologModule
open System.Collections.Generic

[<AutoOpen>]
module internal rec Command =
    type CommandTypes with

        member x.CoilTerminalTag =
            /// Terminal End Tag
            let tet (fc: #IFunctionCommand) = fc.TerminalEndTag

            match x with
            | CoilCmd cc -> tet (cc)
            | PredicateCmd pc -> tet (pc)
            | FunctionCmd fc -> tet (fc)
            | FunctionBlockCmd fbc -> tet (fbc)
            | _ -> failwith "ERROR"

        member x.InstanceName =
            match x with
            | FunctionBlockCmd(fbc) -> fbc.GetInstanceText()
            | _ -> failwith "do not make instanceTag"

        member x.VarType =
            match x with
            | FunctionBlockCmd(fbc) ->
                match fbc with
                | TimerMode ts ->
                    match ts.Timer.Type with
                    | TON -> VarType.TON
                    | TOF -> VarType.TOFF
                    | TMR -> VarType.TMR

                | CounterMode cs ->
                    match cs.Counter.Type with
                    | CTU -> VarType.CTU_INT
                    | CTD -> VarType.CTD_INT
                    | CTUD -> VarType.CTUD_INT
                    | CTR -> VarType.CTR
            | _ -> failwithlog "do not make instanceTag"

        member x.LDEnum =
            match x with
            | CoilCmd(cc) ->
                match cc with
                | COMCoil _ -> ElementType.CoilMode
                | COMClosedCoil _ -> ElementType.ClosedCoilMode
                | COMSetCoil _ -> ElementType.SetCoilMode
                | COMResetCoil _ -> ElementType.ResetCoilMode
                | COMPulseCoil _ -> ElementType.PulseCoilMode
                | COMNPulseCoil _ -> ElementType.NPulseCoilMode
            | (PredicateCmd _ | FunctionCmd _ | FunctionBlockCmd _ | ActionCmd _) -> ElementType.VertFBMode
            | _ -> failwith "ERROR"

    //let createOutputCoil(tag)    = CoilCmd(CoilOutputMode.COMCoil(tag))
    //let createOutputCoilNot(tag) = CoilCmd(CoilOutputMode.COMClosedCoil(tag))
    //let createOutputSet(tag)     = CoilCmd(CoilOutputMode.COMSetCoil(tag))
    //let createOutputRst(tag)     = CoilCmd(CoilOutputMode.COMResetCoil(tag))
    //let createOutputPulse(tag)   = CoilCmd(CoilOutputMode.COMPulseCoil(tag))
    //let createOutputNPulse(tag)  = CoilCmd(CoilOutputMode.COMNPulseCoil(tag))

    type IExpression with
        member x.GetTerminalString (prjParam: XgxProjectParams) =
            match x.Terminal with
            | Some t ->
                match t.Variable, t.Literal with
                | Some v, None -> getStorageText v
                | None, Some (:? ILiteralHolder as lh) ->
                    match prjParam.TargetType with
                    | XGK -> lh.ToTextWithoutTypeSuffix()
                    | _ -> lh.ToText()
                | _ -> failwith "ERROR: Unknown terminal literal case."
            | _ -> failwith "ERROR: Not a Terminal"

    /// Option<IExpression<bool>> to IExpression
    let private obe2e (obe: IExpression<bool> option) : IExpression = obe.Value :> IExpression
    let private flatten (exp: IExpression) = exp.Flatten() :?> FlatExpression

    let private bxi2rxi (bxi:BlockXmlInfo) : RungXmlInfo =
        {
            Coordinate = coord(bxi.X, bxi.Y)
            Xml = bxi.RungXmlInfos.Distinct().MergeXmls()        // Distinct(): dirty hack
            SpanXy = (bxi.TotalSpanX, bxi.TotalSpanY)
        }

    // <timer> for XGI
    let private bxiXgiFunctionBlockTimer (prjParam: XgxProjectParams) (x, y) (timerStatement: TimerStatement) : BlockXmlInfo =
        let ts = timerStatement
        let typ = ts.Timer.Type     // TON, TOF, TMR
        let time: int = int ts.Timer.PRE.Value

        let inputParameters =
            [   "PT", (literal2expr $"T#{time}MS") :> IExpression
                "IN", obe2e ts.RungInCondition
                match typ with
                | TMR -> "RST", obe2e ts.ResetCondition
                | _ -> () ]

        let outputParameters = []

        let blockXml =
            let cmd = FunctionBlockCmd(TimerMode(ts))
            bxiXgiFunctionBlockInstanceXmls prjParam (x, y) cmd inputParameters outputParameters

        blockXml

    let private bxiXgiFunctionBlockCounter (prjParam: XgxProjectParams) (x, y) (counterStatement: CounterStatement) : BlockXmlInfo =
        assert(prjParam.TargetType = XGI)
        //let paramDic = Dictionary<string, FuctionParameterShape>()
        let cs = counterStatement
        let pv = int16 cs.Counter.PRE.Value
        let typ = cs.Counter.Type

        let inputParameters =
            [   "PV", (literal2expr pv) :> IExpression
                match typ with
                | CTU -> // cu, r, pv,       q, cv
                    "CU", obe2e cs.UpCondition
                    "R", obe2e cs.ResetCondition
                | CTD -> // cd, ld, pv,       q, cv
                    "CD", obe2e cs.DownCondition
                    "LD", obe2e cs.LoadCondition
                | CTUD -> // cu, cd, r, ld, pv,       qu, qd, cv
                    "CU", obe2e cs.UpCondition
                    "CD", obe2e cs.DownCondition
                    "R", obe2e cs.ResetCondition
                    "LD", obe2e cs.LoadCondition
                | CTR -> // cd, pv, rst,       q, cv
                    "CD", obe2e cs.DownCondition
                    "RST", obe2e cs.ResetCondition ]

        let outputParameters = []

        let blockXml =
            let cmd = FunctionBlockCmd(CounterMode(cs))
            bxiXgiFunctionBlockInstanceXmls prjParam (x, y) cmd inputParameters outputParameters

        blockXml


    type System.Type with

        member x.GetSizeString(target:PlatformTarget) = systemTypeToXgxTypeName target x


    let bxiXgiPredicate (prjParam: XgxProjectParams) (x, y) (predicate: Predicate) : BlockXmlInfo =
        match predicate with
        | Compare(name, output, args) ->
            let namedInputParameters =
                [ "EN", fakeAlwaysOnExpression :> IExpression ]
                @ (args |> List.indexed |> List.map1st (fun n -> $"IN{n + 1}"))

            let outputParameters = [ "OUT", output ]

            let func =
                match name with
                | ("GT" | "GE" | "EQ" | "LE" | "LT" | "NE") ->
                    let opCompType = args[0].DataType.GetSizeString(prjParam.TargetType)

                    if name = "NE" then
                        $"{name}_{opCompType}" // NE_BOOL
                    else
                        $"{name}2_{opCompType}" // e.g "GT2_INT"
                | _ -> failwithlog "NOT YET"

            bxiXgiBox prjParam (x, y) func namedInputParameters outputParameters ""

    let bxiXgiFunction (prjParam: XgxProjectParams) (x, y) (cond:IExpression option) (func: Function) (target:PlatformTarget): BlockXmlInfo =
        match func with
        | Arithmetic(name, output, args) ->
            // argument 갯수에 따라서 다른 함수를 불러야 할 때 사용.  e.g "ADD3_INT" : 3개의 인수를 더하는 함수
            let arity = args.Length
            let namedInputParameters =
                [
                    yield "EN", cond |? (fakeAlwaysOnExpression :> IExpression)
                    match name with
                    | "NOT" ->  // Signle input case
                        assert(arity = 1)
                        yield "IN", args[0]
                    | "SHL" | "SHR" -> // Double input case
                        assert(arity = 2)
                        yield "IN", args[0]
                        yield "N", args[1]
                    | _ ->
                        yield! args |> List.indexed |> List.map1st (fun n -> $"IN{n + 1}")
                ]

            let outputParameters = [ "OUT", output ]

            let outputType = getType output
            let plcFuncType = systemTypeToXgxTypeName target outputType

            let func =
                let plcSizeType = systemTypeToXgiSizeTypeName outputType

                match name with
                | ("ADD" | "MUL") -> $"{name}{arity}_{plcFuncType}"
                | ("SUB" | "DIV") -> name // DIV 는 DIV, DIV2 만 존재함

                | ("AND" | "OR" | "XOR" ) -> $"{name}{arity}_{plcSizeType}"
                | ("NOT" | "SHL" | "SHR") -> $"{name}_{plcSizeType}"
                | _ -> failwithlog "NOT YET"

            bxiXgiBox prjParam (x, y) func namedInputParameters outputParameters ""

    let bxiXgiAction (prjParam: XgxProjectParams) (x, y) (func: PLCAction) : BlockXmlInfo =
        match func with
        | Move(condition, source, target) ->
            let namedInputParameters = [ "EN", condition :> IExpression; "IN", source ]

            let output = target :?> INamedExpressionizableTerminal
            let outputParameters = [ "OUT", output ]
            bxiXgiBox prjParam (x, y) XgiConstants.FunctionNameMove namedInputParameters outputParameters ""

    let bxiXgiFunctionBlockInstanceXmls
            (prjParam: XgxProjectParams)
            (rungStartX, rungStartY)
            (cmd: CommandTypes)
            (namedInputParameters: (string * IExpression) list)
            (namedOutputParameters: (string * INamedExpressionizableTerminal) list)
        : BlockXmlInfo =
            let func = cmd.VarType.ToString()
            let instanceName = cmd.InstanceName
            bxiXgiBox prjParam (rungStartX, rungStartY) func namedInputParameters namedOutputParameters instanceName

    /// cmd 인자로 주어진 function block 의 type 과
    /// namedInputParameters 로 주어진 function block 에 연결된 다릿발 정보를 이용해서
    /// function block rung 을 그린다.
    let bxiXgiBox
            (prjParam: XgxProjectParams)
            (rungStartX, rungStartY)
            (functionName: string)
            (namedInputParameters: (string * IExpression) list)
            (namedOutputParameters: (string * INamedExpressionizableTerminal) list)
            (instanceName: string)
        : BlockXmlInfo =
            let targetType = prjParam.TargetType
            let iDic = namedInputParameters |> dict
            let oDic = namedOutputParameters |> Tuple.toDictionary

            let systemTypeToXgxType (typ: System.Type) =
                systemTypeToXgxTypeName targetType typ |> DU.tryParseEnum<CheckType> |> Option.get

            /// 입력 인자들을 function 의 입력 순서 맞게 재배열
            let alignedInputParameters =
                /// e.g ["CD, 0x00200001, , 0"; "LD, 0x00200001, , 0"; "PV, 0x00200040, , 0"]
                let inputSpecs = getFunctionInputSpecs functionName |> Array.ofSeq

                namedInputParameters.Length = inputSpecs.Length
                |> verifyM "ERROR: Function input parameter mismatch."

                [|  for s in inputSpecs do
                       let exp = iDic[s.Name]
                       let exprDataType = systemTypeToXgxType exp.DataType

                       let typeCheckExcludes = [ "TON"; "TOF"; "RTO"; "CTU"; "CTD"; "CTUD"; "CTR" ] @ ["AND2"; "OR2"; "XOR2"; "NOT"] @ ["SHL"; "SHR"]

                       if (typeCheckExcludes.Any(fun ex -> functionName = ex || functionName.StartsWith($"{ex}_"))) then
                           () // xxx: timer, counter 에 대해서는 일단, type check skip
                       else
                           s.CheckType.HasFlag(exprDataType) |> verify

                       s.Name, exp, s.CheckType |]

            /// 출력 인자들을 function 의 출력 순서 맞게 재배열
            let alignedOutputParameters =
                /// e.g ["ENO, 0x00200001, , 0"; "OUT, 0x00200001, , 0";]
                let outputSpecs = getFunctionOutputSpecs functionName |> Array.ofSeq

                let typeCheckExcludes = [| "AND2"; "OR2"; "XOR2"; "NOT" |] @ [|"SHL"; "SHR"|] |> HashSet

                [   for (i, s) in outputSpecs.Indexed() do
                        option {
                            let! terminal = oDic.TryFindValue(s.Name)

                            match terminal with
                            | :? IStorage as storage ->
                                if (typeCheckExcludes.Any(fun ex -> functionName.StartsWith(ex))) then
                                    ()
                                else
                                    s.CheckType.HasFlag(systemTypeToXgxType storage.DataType) |> verify
                            | _ -> ()

                            return s.Name, i, terminal, s.CheckType
                        } ]
                |> List.choose id

            let (x, y) = (rungStartX, rungStartY)

            /// y 위치에 literal parameter 쓸 공간 확보 (x 좌표는 아직 미정)
            let reservedLiteralInputParam = ResizeArray<int * IExpression>()
            let mutable sy = 0

            let inputBlockXmls =
                [   for (portOffset, (_name, exp, checkType)) in alignedInputParameters.Indexed() do
                        if portOffset > 0 && exp.Terminal.IsSome then
                            (y + portOffset, exp) |> reservedLiteralInputParam.Add
                            sy <- sy + 1
                        else
                            checkType.HasFlag CheckType.BOOL
                            |> verifyM "ERROR: Only BOOL type can be used as compound expression for input."

                            let blockXml = bxiFunctionInputLadderBlock prjParam (x, y + sy) exp
                            portOffset, blockXml
                            sy <- sy + blockXml.TotalSpanY ]

            /// 입력 parameter 를 그렸을 때, 1 줄을 넘는 것들의 갯수 만큼 horizontal line spacing 필요
            let plusHorizontalPadding = inputBlockXmls.Count(fun (_, x) -> x.TotalSpanY > 1)

            /// function start X
            let fsx = x + inputBlockXmls.Max(fun (_, bxi) -> bxi.TotalSpanX) + plusHorizontalPadding

            let outputCellXmls =
                [
                    for (_portOffset, (_name, yoffset, terminal, _checkType)) in alignedOutputParameters.Indexed() do
                        let terminalText =
                            match terminal with
                            | :? IStorage as storage -> getStorageText storage
                            | _ -> failwithlog "ERROR"

                        rxiFBParameter (fsx + 1, y + yoffset) terminalText
                ]

            /// 문어발: input parameter end 와 function input adaptor 와의 'S' shape 연결
            let tentacleXmls =
                [
                    for (inputBlockIndex, (portOffset, b)) in inputBlockXmls.Indexed() do
                        let i = inputBlockIndex
                        let bex = x + b.X + b.TotalSpanX // block end X
                        let bey = b.Y
                        let c = coord (bex, bey)
                        let spanX = (fsx - bex)

                        if b.TotalSpanX > 1 then
                            /// 'S' shape 의 하단부 수평선 끝점 x 좌표
                            let hEndX = if i = 0 then fsx - 1 else bex + i - 1

                            yield!
                                tryHlineTo (bex, bey) (hEndX) >>- fun xml ->
                                    tracefn $"H: ({bex}, {bey}) -> ({hEndX}, {bey})"

                                    {   Coordinate = c
                                        Xml = xml
                                        SpanXy = (spanX, 1) }

                            if i > 0 then
                                let bexi = bex + i
                                let yi = y + portOffset
                                tracefn $"V: ({bexi - 1}, {bey}) -> [({bexi - 1}, {yi})]"
                                // 'S' shape 의 세로선 그리기
                                yield! rxisVLineUpTo (bexi - 1, bey) yi

                                // 'S' shape 의 상단부 수평선 그리기
                                yield!
                                    tryHlineTo (bexi, yi) (fsx - 1) >>- fun xml ->
                                        tracefn $"H: ({bexi}, {yi}) -> [({bexi}, {fsx - 1})]"

                                        let c2 = coord (bexi, yi)
                                        {   Coordinate = c2
                                            Xml = xml
                                            SpanXy = (spanX, 1) }
                ]

            let allXmls =
                [
                    (* Timer 의 PT, Counter 의 PV 등의 상수 값을 입력 모선에서 연결하지 않고, function cell 에 바로 입력 하기 위함*)
                    for (ry, rexp) in reservedLiteralInputParam do
                        let literal =
                            match rexp.Terminal with
                            | Some terminal ->
                                match terminal.Literal, terminal.Variable with
                                | Some(:? ILiteralHolder as literal), None -> literal.ToTextWithoutTypeSuffix()
                                | Some literal, None -> literal.ToText()
                                | None, Some variable -> getStorageText variable
                                | _ -> failwithlog "ERROR"
                            | _ -> failwithlog "ERROR"

                        rxiFBParameter (x + fsx - 1, ry) literal

                    yield! inputBlockXmls |> bind (fun (_, bx) -> bx.RungXmlInfos)
                    yield! outputCellXmls
                    yield! tentacleXmls
                    let x, y = rungStartX, rungStartY

                    //Command 결과출력
                    rxiFunctionAt (functionName, functionName) instanceName (x + fsx, y) ]


            {
                Xy = (x, y)
                TotalSpanXy = (fsx + 3, max sy (allXmls.Max(fun x -> x.SpanY)))
                RungXmlInfos = allXmls |> List.sortBy (fun x -> x.Coordinate)
            }


    /// (x, y) 위치에 cmd 를 생성.  cmd 가 차지하는 height 와 xml 목록을 반환
    let bxiCommand (prjParam: XgxProjectParams) (x, y) (cond:IExpression option) (cmd: CommandTypes) : BlockXmlInfo =
        match prjParam.TargetType with
        | XGI ->
            match cmd with
            | PredicateCmd(pc) ->
                assert(cond.IsNone)
                bxiXgiPredicate prjParam (x, y) pc
            | FunctionCmd(fc) -> bxiXgiFunction prjParam (x, y) cond fc XGI
            | ActionCmd(ac) ->
                assert(cond.IsNone)
                bxiXgiAction prjParam (x, y) ac
            | FunctionBlockCmd(fbc) ->
                assert(cond.IsNone)
                match fbc with
                | TimerMode(timerStatement)     -> bxiXgiFunctionBlockTimer prjParam (x, y) timerStatement
                | CounterMode(counterStatement) -> bxiXgiFunctionBlockCounter prjParam (x, y) counterStatement
            | _ -> failwithlog "Unknown CommandType"

        | XGK ->
            match cmd with
            | FunctionBlockCmd(fbc)     -> bxiXgkFBCommand prjParam (x, y) (cond, fbc)
            | XgkParamCmd(param, width) -> bxiXgkFBCommandWithParam prjParam (x, y) (cond, param, width)
            | _ -> failwithlog "Unknown CommandType"

        | _ -> failwithlog $"Unknown Target: {prjParam.TargetType}"

    /// (x, y) 위치에 coil 생성.  height(=1) 와 xml 목록을 반환
    let bxiCoil (prjParam: XgxProjectParams) (x, y) (expr: IExpression) (cmdExp: CommandTypes) (coilText:string) : BlockXmlInfo =

        let coilSpanX = coilCellX - x - 1

        let rxis:RungXmlInfo list =
            [
                assert(coilSpanX > 0)

                let exprBxi = bxiLadderBlock prjParam (x, y) expr
                yield bxi2rxi exprBxi



                let c = coord (exprBxi.X + exprBxi.TotalSpanX, y)
                let lengthParam = $"Param={dq}{3 * coilSpanX}{dq}"
                let xml = elementFull (int ElementType.MultiHorzLineMode) c lengthParam ""

                yield {
                    Coordinate = c
                    Xml = xml
                    SpanXy = (coilSpanX, 1) }

                let c = coord (coilCellX, y)
                let xml = elementBody (int cmdExp.LDEnum) c coilText        // coilText: XGK 에서는 직접변수를, XGI 에서는 변수명을 사용

                yield {
                    Coordinate = c
                    Xml = xml
                    SpanXy = (1, 1) }
            ]

        {   Xy = (x, y)
            TotalSpanXy = (coilCellX, rxis.Max(_.SpanY))
            RungXmlInfos = rxis }


    let bxiXgkFBCommandWithParam (prjParam: XgxProjectParams) (x, y) (cond:IExpression option, cmdParam: string, cmdWidth:int) : BlockXmlInfo =
        noop()
        let rxis: RungXmlInfo list =
            [
                let mutable sx = x
                match cond with
                | Some c ->
                    let exprBxi = bxiLadderBlock prjParam (x, y) c
                    yield bxi2rxi exprBxi
                    sx <- x + exprBxi.TotalSpanX
                | _ -> ()


                let spanX = (coilCellX - x - cmdWidth)
                let c = coord (sx, y)
                let xml =
                  let lengthParam = $"Param={dq}{3 * spanX}{dq}"
                  elementFull (int ElementType.MultiHorzLineMode) c lengthParam ""

                yield {
                    Coordinate = coord (x, y)
                    Xml = xml
                    SpanXy = (spanX, 1) }

                let xy = (coilCellX, y)
                yield {
                    Coordinate = coord xy
                    Xml = xgkFBAt cmdParam xy
                    SpanXy = (cmdWidth, 1) } ]

        {   Xy = (x, y)
            TotalSpanXy = (coilCellX, rxis.Max(_.SpanY))
            RungXmlInfos = rxis }

    let bxiXgkFBCommand (prjParam: XgxProjectParams) (x, y) (cond:IExpression option, fbc: FunctionBlock) : BlockXmlInfo =
        let cmdWidth = 3
        let cmdParam =
            match fbc with
            | TimerMode ts ->
                let t = ts.Timer
                let typ = t.Type.ToString()
                let var = t.Name
                let value =
                    let res = prjParam.GetXgkTimerResolution(t.TimerStruct.XgkStructVariableDevicePos)
                    int <| (float t.PRE.Value) / res
                $"Param={dq}{typ},{var},{value}{dq}"        // e.g : Param="TON,T0000,1000"
            | CounterMode cs ->
                let c = cs.Counter
                let typ = c.Type.ToString()
                let var = c.Name
                let value = c.PRE.Value
                $"Param={dq}{typ},{var},{value}{dq}"        // e.g : Param="CTU,C0000,1000"
        bxiXgkFBCommandWithParam prjParam (x, y) (cond, cmdParam, cmdWidth)


    /// 왼쪽에 FB (비교 연산 등) 를 그리고, 오른쪽에 coil 을 그린다.
    let xmlXgkFBLeft (x, y) (fbParam: string) (target: string) : XmlOutput =
        assert (x = 0)
        let inner =
            [
                xgkFBAt fbParam (x, y)

                let c = coord (x + 3, y)
                let spanX = coilCellX - 1
                let lengthParam = $"Param={dq}{3 * spanX}{dq}"
                elementFull (int ElementType.MultiHorzLineMode) c lengthParam ""

                let c = coord (coilCellX, y)
                elementBody (int ElementType.CoilMode) c target
            ] |> joinLines
        wrapWithRung inner


    /// 왼쪽에 condition (None 이면 _ON) 을 조건으로 우측에 FB (사칙 연산) 을 그린다.
    let xmlXgkFBRight (prjParam: XgxProjectParams) (x, y) (condition:IExpression<bool> option) (fbParam: string) : XmlOutput =
        let inner =
            [
                let cond = condition |? fakeAlwaysOnExpression
                let sub = bxiLadderBlock prjParam (x, y) cond
                mergeXmls sub.RungXmlInfos

                let c =
                    let newX = x + sub.TotalSpanX
                    let newY = y + sub.TotalSpanY - 1
                    coord(newX, newY)

                let spanX = coilCellX - 4
                let lengthParam = $"Param={dq}{3 * spanX}{dq}"
                elementFull (int ElementType.MultiHorzLineMode) c lengthParam ""

                xgkFBAt fbParam (coilCellX, y)

            ] |> joinLines
        wrapWithRung inner

    /// [rxi] for XGK Function Block
    let rxiXgkFB (prjParam: XgxProjectParams) (x, y) (condition:IExpression) (fbParam: string, fbWidth:int) : RungXmlInfo =
        assert (x = 0)
        let conditionBlockXml = bxiFunctionInputLadderBlock prjParam (x, y) condition
        let cbx = conditionBlockXml

        let c = coord (x + cbx.TotalSpanX, y)
        let spanX = coilCellX - 4
        let xml =
            [
                let lengthParam = $"Param={dq}{3 * spanX}{dq}"
                elementFull (int ElementType.MultiHorzLineMode) c lengthParam ""

                xgkFBAt fbParam (coilCellX - fbWidth - cbx.TotalSpanX, y)

            ] |> joinLines

        (* 좌측 expression 이 multiline 인 경우, 우측 FB 의 Coordinate 값이 expression 의 coordinate 중간에 삽입되는 형태로 정렬되어야 한다.  *)
        let xmls = cbx.RungXmlInfos @ [{ Coordinate = c; Xml = xml; SpanXy = (spanX, 1)}]

        {
            Coordinate = coord(0, y + cbx.TotalSpanY)
            Xml = mergeXmls xmls
            SpanXy = (fbWidth, 1)
        }

    /// function input 에 해당하는 expr 을 그리되, 맨 마지막을 multi horizontal line 연결 가능한 상태로 만든다.
    let bxiFunctionInputLadderBlock (prjParam: XgxProjectParams) (x, y) (expr: IExpression) : BlockXmlInfo =
        let blockXml = bxiLadderBlock prjParam (x, y) expr

        if expr |> flatten |> isFunctionBlockConnectable  then
            blockXml
        else
            let b = blockXml
            let x = b.X + b.TotalSpanX //+ 1

            let lineXml =
                let c = coord (x, b.Y)
                let xml = hlineStartMarkAt (x, b.Y)

                {   Coordinate = c
                    Xml = xml
                    SpanXy = (1, 1) }

            {   blockXml with
                    TotalSpanXy = (b.TotalSpanX + 1, b.TotalSpanY)
                    RungXmlInfos = b.RungXmlInfos +++ lineXml }

    /// x y 위치에서 expression 표현하기 위한 정보 반환
    /// {| Xml=[|c, str|]; NextX=sx; NextY=maxY; VLineUpRightMaxY=maxY |}
    /// - Xml : 좌표 * 결과 xml 문자열
    let rec internal bxiLadderBlock (prjParam: XgxProjectParams) (x, y) (objExpr: IExpressionBase) : BlockXmlInfo =
        let flatExp =
            match objExpr with
            | :? IExpression as exp -> flatten exp
            | :? FlatExpression as f -> f
            | _ -> failwith "ERROR"

        let c = coord (x, y)
        let isXgk, isXgi = prjParam.TargetType = XGK, prjParam.TargetType = XGI

        match flatExp with
        | FlatTerminal(terminal, pulse, neg) ->
            let mode =
                match pulse, neg with
                | Some(true),  true  -> ElementType.PulseClosedContactMode
                | Some(true),  false -> ElementType.PulseContactMode
                | Some(false), true  -> ElementType.NPulseClosedContactMode
                | Some(false), false -> ElementType.NPulseContactMode
                | None,        true  -> ElementType.ClosedContactMode
                | None,        false -> ElementType.ContactMode
                |> int

            // XGK 에서는 직접변수를, XGI 에서는 변수명을 사용
            let terminalText =
                match terminal, prjParam.TargetType with
                | :? IStorage as storage, XGK ->
                    if storage.Name.Contains (xgkTimerCounterContactMarking) then
                        storage.Name.Replace (xgkTimerCounterContactMarking, "")
                     else
                        match storage.Address, storage.Name with
                        | "", StartsWith("_") -> storage.Name
                        | _ -> storage.Address
                | :? IStorage as storage, _ ->   getStorageText storage
                | :? LiteralHolder<bool> as onoff, _ -> if onoff.Value then "_ON" else "_OFF"
                | _ ->
                    match terminal with
                    | :? IStorage as storage -> getStorageText storage
                    | _ -> failwithlog "ERROR"

            let str = elementBody mode c terminalText

            let xml = { Coordinate = c; Xml = str; SpanXy = (1, 1) }

            {   RungXmlInfos = [ xml ]
                Xy = (x, y)
                TotalSpanXy = (1, 1)
            }

        | FlatNary(And, exprs) ->
            let mutable sx = x

            let blockedExprXmls: BlockXmlInfo list =
                [ for exp in exprs do
                      let sub = bxiLadderBlock prjParam (sx, y) exp
                      sx <- sx + sub.TotalSpanX
                      sub ]

            let spanX = blockedExprXmls.Sum(fun x -> x.TotalSpanX)
            let spanY = blockedExprXmls.Max(fun x -> x.TotalSpanY)
            let exprXmls = blockedExprXmls |> List.collect (fun x -> x.RungXmlInfos)

            {   RungXmlInfos = exprXmls
                Xy = (x, y)
                TotalSpanXy = (spanX, spanY) }


        | FlatNary(Or, exprs) ->
            let mutable sy = y

            let blockedExprXmls: BlockXmlInfo list =
                [ for exp in exprs do
                      let sub = bxiLadderBlock prjParam (x, sy) exp
                      sy <- sy + sub.TotalSpanY
                      sub ]

            let spanX = blockedExprXmls.Max(fun x -> x.TotalSpanX)
            let spanY = blockedExprXmls.Sum(fun x -> x.TotalSpanY)
            let exprXmls = blockedExprXmls |> List.collect (fun x -> x.RungXmlInfos)

            let xmls =
                [   yield! exprXmls

                    let auxLineXmls =
                        [   for ri in blockedExprXmls do
                              if ri.TotalSpanX < spanX then
                                  let span = (spanX - ri.TotalSpanX - 1)
                                  let param = $"Param={dq}{span * 3}{dq}"
                                  let mode = int ElementType.MultiHorzLineMode
                                  let c = coord (x + ri.TotalSpanX, ri.Y)
                                  let xml = elementFull mode c param ""

                                  { Coordinate = c; Xml = xml; SpanXy = (span, 1) } ]

                    yield! auxLineXmls


                    // 좌측 vertical lines
                    if x >= 1 then
                        let dy =
                            blockedExprXmls
                            |> List.take(blockedExprXmls.Length - 1)
                            |> List.sumBy(fun e -> e.TotalSpanY)
                        yield! rxisVLineDownN (x - 1, y) dy

                    // ```OR variable length 역삼각형 test```
                    let lowestY =
                        blockedExprXmls.Where(fun sri -> sri.TotalSpanX <= spanX).Max(fun sri -> sri.Y)
                    // 우측 vertical lines
                    yield! rxisVLineDownN (x + spanX - 1, y) (lowestY - y) ]

            let xmls = xmls |> List.distinct // dirty hacking!

            {   RungXmlInfos = xmls
                Xy = (x, y)
                TotalSpanXy = (spanX, spanY) }

        | FlatNary(OpArithmetic _, _exprs) when isXgk ->
            failwithlog "ERROR : Should have been processed in early stage." // 사전에 미리 처리 되었어야 한다.  여기 들어오면 안된다. XgiStatement

        | FlatNary(OpCompare cmp, args) when isXgk ->
            let fbParam =
                let op = operatorToXgkFunctionName cmp args[0].DataType |> escapeXml
                let arg0, arg1 =
                    match args[0], args[1] with
                        | FlatTerminal(t0, _, _), FlatTerminal(t1, _, _) ->
                            t0.GetContact(), t1.GetContact()
                        | _ -> failwithlog "ERROR: Terminal is None"
                $"Param={dq}{op},{arg0},{arg1}{dq}"        // todo: XGK 에서는 직접변수를 사용

            let xml = xgkFBAt fbParam (x, y)
            {   RungXmlInfos = [ { Coordinate = coord (x, y); Xml = xml; SpanXy = (3, 1) } ]
                Xy = (x, y)
                TotalSpanXy = (3, 1)
            }

        | FlatNary((OpCompare _fn | OpArithmetic _fn), _args) when isXgi ->
            failwithlog "ERROR : Not yet!"  // todo

        // terminal case
        | FlatNary(OpUnit, inner :: []) -> bxiLadderBlock prjParam (x, y) inner

        // negation 없애기
        | FlatNary(Neg, inner :: []) -> FlatNary(OpUnit, [ inner.Negate() ]) |> bxiLadderBlock prjParam (x, y)


        | FlatNary(risingOrFallingAfter, flatExpArg::[]) when risingOrFallingAfter = RisingAfter || risingOrFallingAfter = FallingAfter ->
            let blockXml = bxiLadderBlock prjParam (x, y) flatExpArg
            let mode =
                match risingOrFallingAfter with
                | RisingAfter -> ElementType.RisingContact
                | FallingAfter -> ElementType.FallingContact
                | _ -> failwith "ERROR: Unexpected."
            let xx, yy = x + blockXml.TotalSpanX, y
            let c = coord (xx, yy)
            let xml = elementFull mode c "" ""
            {   blockXml with
                    Xy = (x, y)
                    TotalSpanXy = (blockXml.TotalSpanX + 1, blockXml.TotalSpanY)
                    RungXmlInfos = blockXml.RungXmlInfos +++ { Coordinate = c; Xml = xml; SpanXy = (1, 1) } }

        | _ -> failwithlog "Unknown FlatExpression case"

    type FlatExpression with
        member exp.BxiLadderBlock (prjParam: XgxProjectParams, (x, y)) = bxiLadderBlock prjParam (x, y) exp

    /// [rxi] Flat expression 을 논리 Cell 좌표계 x y 에서 시작하는 rung 를 작성한다.
    ///
    /// - xml 및 다음 y 좌표 반환
    ///
    /// - expr 이 None 이면 그리지 않는다.
    ///
    /// - cmdExp 이 None 이면 command 를 그리지 않는다.
    let rxiRung (prjParam: XgxProjectParams) (x, y) (condition: IExpression option) (cmdExp: CommandTypes) : RungXmlInfo =
        /// [rxi]
        let rxiRungImpl (x, y) (expr: IExpression option) (cmd: CommandTypes) : RungXmlInfo =
            let distinct bxi:BlockXmlInfo = { bxi with RungXmlInfos = bxi.RungXmlInfos |> List.distinct }

            //let exprSpanX, exprSpanY, exprXmls =
            //    match expr with
            //    | Some expr ->
            //        let exprBlockXmlElement = bxiLadderBlock prjParam (x, y) expr
            //        let ex = exprBlockXmlElement
            //        ex.TotalSpanX, ex.TotalSpanY, ex.XmlElements |> List.distinct    // dirty hack!
            //    | _ -> 0, 0, []

            let bxi =
                match cmd with
                | CoilCmd _cc ->
                    let coilText = // XGK 에서는 직접변수를, XGI 에서는 변수명을 사용
                        match prjParam.TargetType, cmd.CoilTerminalTag with
                        | XGK, (:? IStorage as stg) when not <| (stg :? XgkTimerCounterStructResetCoil) ->
                            stg.Address |> tee(fun a -> if (a.IsNullOrEmpty()) then failwith $"{stg.Name} 의 주소가 없습니다.")
                        | _ ->
                            match cmd.CoilTerminalTag with
                            | :? IStorage as storage -> getStorageText storage
                            | _ -> failwithlog "ERROR"
                    bxiCoil prjParam (x, y) expr.Value cmd coilText |> distinct

                | _ ->      // | PredicateCmd _pc | FunctionCmd _ | FunctionBlockCmd _ | ActionCmd _
                    bxiCommand prjParam (x, y) expr cmd |> distinct

            let c = coord (x, bxi.TotalSpanY + y)

            {
                Xml = bxi.RungXmlInfos.MergeXmls()
                Coordinate = c
                SpanXy = (bxi.TotalSpanX, bxi.TotalSpanY)
            }

        match prjParam.TargetType, cmdExp with
        | XGK, ActionCmd(Move(condition, source, target)) when source.Terminal.IsSome ->
            let fbParam, fbWidth =
                let s, d = source.GetTerminalString(prjParam),
                           match target with
                           | :? TimerCounterBaseStruct as t -> t.XgkStructVariableName
                           | _ -> target.Address

                let mov =
                    let st, tt = source.DataType, target.DataType

                    // move 의 type 이 동일해야 한다.  timer/counter 는 예외.  reset coil 이나 preset 설정 등 허용.
                    // st 의 type 을 모르는 경우는 포기 (obj)
                    assert (st = tt || tt = typeof<TimerCounterBaseStruct> || st = typeof<obj>)

                    operatorToXgkFunctionName "MOV" st

                $"Param={dq}{mov},{s},{d}{dq}", 3           // Param="MOV,source,destination"


            rxiXgkFB prjParam (x, y) condition (fbParam, fbWidth)

        | _ ->
            match prjParam.TargetType, condition, cmdExp with
            | (XGI, _, _) | (_, Some _, _) ->        // prjParam.TargetType = XGI || condition.IsSome
                rxiRungImpl (x, y) condition cmdExp
            | XGK, _, FunctionBlockCmd(fbc) ->
                match fbc with
                | CounterMode(counterStatement) when counterStatement.Counter.Type = CTUD ->
                    let counter = counterStatement.Counter
                    // CTUD, C, U, D, N
                    let up, down =      // reset 조건은 statement2statements 에서 counter 의 reset 조건을 따로 statement 로 추가하였으므로, 여기서는 무시한다.
                        match counterStatement.UpCondition, counterStatement.DownCondition with
                        | Some u, Some d -> u.GetTerminalString(prjParam), d.GetTerminalString(prjParam)
                        | _ -> failwithlog "ERROR"
                    let rungInCondition =
                        match condition with
                        | Some expr -> expr
                        | _ -> Expression.True :> IExpression
                        |> flatten
                    let pv = counter.PRE.Value

                    let mutable spanY = 1
                    let xml =
                        [
                            let { Xy = (_xx, yy); TotalSpanXy = (totalSpanX, totalSpanY); RungXmlInfos = xmls } : BlockXmlInfo =
                                rungInCondition.BxiLadderBlock(prjParam, (x, y))
                            xmls[0].Xml

                            hlineTo (totalSpanX, yy) (coilCellX - 5)

                            if totalSpanY > 1 then
                                spanY <- totalSpanY

                            let param =
                                let counterVariable = counter.CounterStruct.XgkStructVariableName
                                $"Param={dq}CTUD,{counterVariable},{up},{down},{pv}{dq}"
                            xgkFBAt param (coilCellX - 5 - 1, yy)
                        ] |> joinLines

                    { Xml = xml; Coordinate = coord(0, y + spanY); SpanXy = (coilCellX, spanY) }

                | _ ->
                    let exp =
                        match fbc with
                        | CounterMode(counterStatement) ->
                            counterStatement.GetUpOrDownCondition()
                        | TimerMode(timerStatement) ->
                            timerStatement.RungInCondition.Value
                    rxiRungImpl (x, y) (Some exp) cmdExp
            | _ ->
                    rxiRungImpl (x, y) condition cmdExp
