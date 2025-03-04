namespace PLC.CodeGen.LS


open System
open Engine.Core
open Dual.Common.Core.FS
open PLC.CodeGen.Common
open PLC.CodeGen.LS


[<AutoOpen>]
module LsPLCExportExpressionModule =
    type ExpressionTransformers = {
        TerminalHandler: int*IExpression -> IExpression
        FunctionHandler: int*IExpression*IStorage option -> IExpression     // (level, expression, resultStore) -> new expression
    }

    type XgxStorage = ResizeArray<IStorage>
    type Augments(storages:XgxStorage, statements:StatementContainer) =
        new() = Augments(XgxStorage(), StatementContainer())
        member val Storages = storages        // ResizeArray<IStorage>
        member val Statements = statements    // ResizeArray<Statement>
        member val ExpressionStore:IStorage option = None with get, set

    /// '_ON' 에 대한 expression
    let fakeAlwaysOnExpression: Expression<bool> =
        let on = createXgxVariable "_ON" true "가짜 _ON" :?> XgxVar<bool>
        DuTerminal(DuVariable on)

    /// '_OFF' 에 대한 expression
    let fakeAlwaysOffExpression: Expression<bool> =
        let on = createXgxVariable "_OFF" true "가짜 _OFF" :?> XgxVar<bool>
        DuTerminal(DuVariable on)

    /// '_1ON' 에 대한 expression
    let fake1OnExpression: Expression<bool> =
        let on = createXgxVariable "_1ON" true "가짜 _1ON" :?> XgxVar<bool>
        DuTerminal(DuVariable on)


    let operatorToXgiFunctionName =
        function
        | ">"  -> "GT"
        | ">=" -> "GE"
        | "<"  -> "LT"
        | "<=" -> "LE"
        | "==" -> "EQ"
        | "!=" -> "NE"
        | "+"  -> "ADD"
        | "-"  -> "SUB"
        | "*"  -> "MUL"
        | "/"  -> "DIV"

        | "&" | "&&&" -> "AND"
        | "|" | "|||" -> "OR"
        | "^" | "^^^" -> "XOR"
        | "~" | "~~~" -> "NOT"

        | "<<" | "<<<" -> "SHL"
        | ">>" | ">>>" -> "SHR"

        | _ -> failwithlog "ERROR"


    /// {prefix}{Op}{suffix} 형태로 반환.  e.g "DADDU" : "{D}{ADD}{U}" => DWORD ADD UNSIGNED
    ///
    /// - prefix: "D" for DWORD, "R" for REAL, "L" for LONG REAL, "$" for STRING
    ///
    /// suffix: "U" for UNSIGNED
    let operatorToXgkFunctionName (op:string) (typ:Type) : string =
        let prefix =
            match typ with
            | _ when typ = typeof<byte> ->  // "S"       //"S" for short (1byte)
                failwith $"byte (sint) type operation {op} is not supported in XGK"     // byte 연산 지원 여부 확인 필요
            | _ when typ.IsOneOf(typeof<int32>, typeof<uint32>) -> "D"
            | _ when typ = typeof<single> -> "R"     //"R" for real
            | _ when typ = typeof<double> -> "L"     //"L" for long real
            | _ when typ = typeof<string> -> "$"     //"$" for string
            | _ when typ.IsOneOf(typeof<char>, typeof<int64>, typeof<uint64>) -> failwith "ERROR: type mismatch for XGK"
            | _ -> ""

        let unsigned =
            match typ with
            | _ when typ.IsOneOf(typeof<uint16>, typeof<uint32>) && op <> "MOV" -> "U"  // MOVE 는 "MOVU" 등이 없다.  size 만 중요하지 unsigned 여부는 중요하지 않다.
            | _ -> ""

        let opName =
            match op with
            | "+"  -> "ADD"
            | "-"  -> "SUB"
            | "*"  -> "MUL"
            | "/"  -> "DIV"
            | "!=" -> "<>"
            | "==" -> "="
            | "MOV" -> "MOV"

            | IsOpB _ -> failwith "XGK Bitwise operator not supported: op"
            //| "&" | "&&&" -> "BAND"
            //| "|" | "|||" -> "BOR"
            //| "^" | "^^^" -> "BXOR"
            //| "~" | "~~~" -> failwith "Not binary operation"

            | _ when isOpC op -> op
            | _ -> failwithlog "ERROR"

        if isOpB op then
            opName
        else if isOpC op then
            $"{unsigned}{prefix}{opName}"       // e.g "UD<="
        else
            $"{prefix}{opName}{unsigned}"

    let operatorToMnemonic op =
        try
            operatorToXgiFunctionName op
        with ex ->
            match op with
            | "||" -> "OR"
            | "&&" -> "AND"
            | "<>" -> "NE"
            | _ -> failwithlog "ERROR"


    let private getTmpName (nameHint: string) (n:int) = $"_t{n}_{nameHint}"
    type XgxProjectParams with
        /// 반환 객체가 실제 XgxVar<'T> 이긴 하나, 'T 를 인자로 받지 않아서 드러나지 않아서 IXgxVar 로 반환한다.
        member x.CreateAutoVariable(nameHint: string, initValue: obj, comment) : IXgxVar =
            let n = x.AutoVariableCounter()
            let name = getTmpName nameHint n
            createXgxVariable name initValue comment

        member x.CreateTypedAutoVariable(nameHint: string, initValue: 'T, comment) : XgxVar<'T> =
            let n = x.AutoVariableCounter()
            let name = getTmpName nameHint n

            let param =
                {   defaultStorageCreationParams (initValue) (VariableTag.PlcUserVariable|>int) with
                        Name = name
                        Comment = Some comment }

            XgxVar(param)
        member x.CreateAutoVariableWithFunctionExpression(exp:IExpression) =
            match exp.FunctionName with
            | Some op ->
                let tmpNameHint, comment = operatorToMnemonic op, exp.ToText()
                x.CreateAutoVariable(tmpNameHint, exp.BoxedEvaluatedValue, comment)
            | _ -> failwithlog "ERROR"

        member x.CreateAutoVariableWithFunctionExpression(pack:DynamicDictionary, exp:IExpression) =
            let var = x.CreateAutoVariableWithFunctionExpression(exp) :> IStorage
            let augs = pack["augments"] :?> Augments
            augs.Storages.Add var
            DuAssign(None, exp, var) |> augs.Statements.Add
            var


    type IExpression with
        /// 주어진 Expression 을 multi-line text 형태로 변환한다.
        member exp.ToTextFormat() : string =
            let tab n = String.replicate (n*4) " "
            let rec traverse (level:int) (exp:IExpression) =
                let space = tab level
                match exp.Terminal, exp.FunctionName with
                | Some terminal, None ->
                    match terminal.Variable, terminal.Literal with
                    | Some storage, None -> $"{space}Storage: {storage.ToText()}"
                    | None, Some literal -> $"{space}Literal: {literal.ToText()}"
                    | _ -> failwith "Invalid expression"

                | None, Some fn ->
                    [
                        $"{space}Function: {fn}"
                        for a in exp.FunctionArguments do
                            traverse (level + 1) a
                    ] |> String.concat "\r\n"
                | _ -> failwith "Invalid expression"
            traverse 0 exp

        member exp.Visit (f: IExpression -> IExpression) : IExpression =
            match exp.Terminal, exp.FunctionName with
            | Some _terminal, None ->
                f exp
            | None, Some _fn ->
                let args = exp.FunctionArguments |> map f
                exp.WithNewFunctionArguments args |> f
            | _ ->
                failwith "Invalid expression"

        member exp.Visit (expPath:IExpression list, f: IExpression list -> IExpression -> IExpression) : IExpression =
            match exp.Terminal, exp.FunctionName with
            | Some _terminal, None ->
                f expPath exp
            | None, Some _fn ->
                let args = exp.FunctionArguments |> map (fun a -> f (exp::expPath) a)
                exp.WithNewFunctionArguments args |> f expPath
            | _ ->
                failwith "Invalid expression"

        member exp.IsLiteralizable() : bool =
            let rec visit (exp:IExpression) : bool =
                match exp.Terminal, exp.FunctionName with
                | Some terminal, _ ->
                    terminal.Literal.IsSome
                | None, Some _fn ->
                    exp.FunctionArguments |> map visit |> Seq.forall id
                | _ ->
                    failwith "Invalid expression"
            visit exp


        /// Expression 을 flattern 할 수 있는 형태로 변환 : e.g !(a>b) => (a<=b)
        /// Non-terminal negation 을 terminal negation 으로 변경
        member x.ApplyNegate() : IExpression =
            let self = x
            let negate (expPath:IExpression list) (expr:IExpression) : IExpression =
                match expr.Terminal, expr.FunctionName with
                    | Some _terminal, None ->
                        if expr.DataType = typedefof<bool> then
                            expr.NegateBool()
                        else
                            // 비교 연산 하에서의 argument negation 은 무시한다.  (e.g. !(a > b) => a <= b.  연산자만 변경하고, a 와 b 의 negation 은 무시됨.)
                            assert(expPath.Head.FunctionName.Value |> isOpC)
                            expr
                    | None, Some "!" -> expr.FunctionArguments.ExactlyOne()
                    | None, Some _fn -> expr.NegateBool()
                    | _ -> failwith "Invalid expression"

            let rec visitArgs (expPath:IExpression list) (negated:bool) (expr:IExpression) : IExpression =
                match expr.Terminal, expr.FunctionName with
                | Some _terminal, None ->
                    match negated with
                    // terminal 의 negation 은 bool type 에 한정한다.
                    | true -> negate expPath expr
                    | _-> expr
                | None, Some _fn ->
                    visitFunction expPath negated expr
                | _ -> failwith "Invalid expression"

            and visitFunction (expPath:IExpression list) (negated:bool) (expr:IExpression) : IExpression =
                let args = expr.FunctionArguments
                let newExpPath = expr::expPath
                let vf = visitFunction newExpPath
                let va = visitArgs newExpPath
                if negated then
                    let newArgs = args |> map (va true)
                    match expr.Terminal, expr.FunctionName with
                    | Some _terminal, None ->
                        negate newExpPath expr
                    | None, Some(IsOpC fn) ->
                        let reverseFn =
                            match fn with
                            | "==" -> "!="
                            | "!=" | "<>" -> "=="
                            | ">" ->  "<="
                            | ">=" -> "<"
                            | "<" ->  ">="
                            | "<=" -> ">"
                            | _ -> failwith "ERROR"
                        createCustomFunctionExpression reverseFn newArgs
                    | None, Some("&&" | "||" as fn) ->
                        let reverseFn =
                            match fn with
                            | "&&" -> "||"
                            | "||" -> "&&"
                            | _ -> failwith "ERROR"
                        createCustomFunctionExpression reverseFn newArgs
                    | None, Some "!" ->
                        args.ExactlyOne() |> vf false
                    | None, Some(FunctionNameRising | FunctionNameFalling as fn) ->
                        createCustomFunctionExpression fn newArgs
                    | _ -> failwith "Invalid expression"
                else
                    match expr.Terminal, expr.FunctionName with
                    | Some _terminal, None -> expr
                    | None, Some "!" ->
                        args.ExactlyOne() |> va true
                    | None, Some _fn ->
                        let newArgs = args |> map (va false)
                        expr.WithNewFunctionArguments newArgs
                    | _ -> failwith "Invalid expression"

            visitFunction [] false self


        /// Expression 에 대해, 주어진 transformer 를 적용한 새로운 expression 을 반환한다.
        /// Expression 을 순환하면서, terminal 에 대해서는 TerminalHandler 를, function 에 대해서는 FunctionHandler 를 적용한다.
        member exp.Transform(tfs:ExpressionTransformers, resultStore:IStorage option) : IExpression =
            let {TerminalHandler = th; FunctionHandler = fh} = tfs

            let rec traverse (level:int) (exp:IExpression) (resultStore:IStorage option) : IExpression =
                match exp.Terminal, exp.FunctionName with
                | Some _terminal, None -> th (level, exp)
                | None, Some _fn ->
                    let args = exp.FunctionArguments
                    let newArgs = [for a in args do traverse (level + 1) a None]
                    let newFn =
                        let f = exp.WithNewFunctionArguments newArgs
                        fh (level, f, resultStore)
                    newFn
                | _ -> failwith "Invalid expression"
            traverse 0 exp resultStore


