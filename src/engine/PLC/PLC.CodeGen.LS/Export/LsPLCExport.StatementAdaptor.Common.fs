namespace PLC.CodeGen.LS

open System.Security

open Engine.Core
open Dual.Common.Core.FS
open PLC.CodeGen.Common

(*
    - 사칙연산 함수(Add, ..) 의 입력은 XGI 에서 전원선으로부터 연결이 불가능한 반면,
      ds 문법으로 작성시에는 expression 을 이용하기 때문에 직접 호환이 불가능하다.
      * add(<expr1>, <expr2>) 를
            tmp1 = <expr1> 및 tmp2 = <expr2> 와 같이
            . 임시 변수를 생성해서 assign rung 을 만들고
            . 임시 변수를 add 함수의 입력 argument 에 tag 로 작성하도록 한다.

    - 임의의 rung 에 사칙연산 함수가 포함되면, 해당 부분만 잘라서 임시 변수에 저장하고 그 값을 이용해야 한다.
      * e.g $result = ($t1 + $t2) > 3
            . $tmp = $t1 + $t2
            . $result = $tmp > 3

    - Timer 나 Counter 의 Rung In Condition 은 복수개이더라도 전원선 연결이 가능하다.
      * 임시 변수 없이 expression 을 그대로 전원선 연결해서 그리면 된다.

    - XGI 임시 변수는 XgiLocalVar<'T> type 으로 생성된다.

    - XGI rung 생성시에 Engine.Core 에서 생성된 Statement 를 직접 사용할 수 없는 이유이다.
    - Statement 를 XgiStatement 로 변환한 후, 이를 XGI 생성 모듈에서 사용한다.
    - 변환 시, 추가적으로 생성되는 요소
       * 조건 식을 임시 변수로 저장하기 위한 추가 statement
         - 기존 조건식 갖는 statement 대신 임시 변수를 가지는 statement 로 변환
       * 생성된 임시 변수의 tag 등록

    - [Statement] -> [temporary tag], [XgiStatment] : # XgiStatment >= # Statement
*)


[<AutoOpen>]
module ConvertorPrologModule =
    let internal systemTypeToXgiTypeName (typ: System.Type) =
        // pp.60, https://sol.ls-electric.com/uploads/document/16572861196090/XGI%20%EC%B4%88%EA%B8%89_V21_.pdf
        match typ.Name with
        | BOOL -> "BOOL"
        | CHAR -> "BYTE"
        | INT8 -> "SINT"
        | INT16 -> "INT"
        | INT32 -> "DINT"
        | INT64 -> "LINT"
        | UINT8 -> "USINT"
        | UINT16 -> "UINT"
        | UINT32 -> "UDINT"
        | UINT64 -> "ULINT"
        | FLOAT32 -> "REAL"
        | FLOAT64 -> "LREAL"
        | STRING -> "STRING" // 32 byte
        | _ -> failwithlog "ERROR"

    let private systemTypeToXgkTypeName (typ: System.Type) =
        match typ.Name with
        | BOOL -> "BIT"
        | (INT8 | UINT8 | CHAR)
        | (INT16 | UINT16)
        | (INT32 | UINT32) -> "WORD"

        | (STRING | FLOAT32 | FLOAT64 | INT64 | UINT64) -> "WORD"     // xxx 이거 맞나?

        | _ -> failwithlog "ERROR"

    let systemTypeToXgiSizeTypeName (typ: System.Type) =
        match typ.Name with
        | BOOL -> "BOOL"
        | CHAR | INT8 | UINT8 -> "BYTE"
        | INT16 | UINT16 -> "WORD"
        | INT32 | UINT32 -> "DWORD"
        | INT64 | UINT64 -> "LWORD"
        | FLOAT32 -> "REAL"
        | FLOAT64 -> "LREAL"
        | STRING -> "STRING" // 32 byte
        | _ -> failwithlog "ERROR"

    let systemTypeToXgxTypeName (target:PlatformTarget) (typ: System.Type) =
        match target with
        | XGI -> systemTypeToXgiTypeName typ
        | XGK -> systemTypeToXgkTypeName typ
        | _ -> failwithlog "ERROR"

    type IXgxVar =
        inherit IVariable
        inherit INamedExpressionizableTerminal
        abstract SymbolInfo: SymbolInfo

    type IXgxVar<'T> =
        inherit IXgxVar
        inherit IVariable<'T>

    /// XGI/XGK 에서 사용하는 tag 주소를 갖지 않는 variable
    type XgxVar<'T when 'T: equality>(param: StorageCreationParams<'T>) =
        inherit VariableBase<'T>(param)

        let {   Name = name
                Value = initValue
                Comment = comment } =
            param

        let symbolInfo =
            let plcType = systemTypeToXgiTypeName typedefof<'T>
            let comment = comment |> map (fun cmt -> SecurityElement.Escape cmt) |? ""
            let initValueHolder: BoxedObjectHolder = { Object = initValue }
            let kind = int Variable.Kind.VAR
            fwdCreateSymbolInfo name comment plcType kind initValueHolder

        interface IXgxVar with
            member x.SymbolInfo = x.SymbolInfo

        interface INamedExpressionizableTerminal with
            member x.StorageName = name

        interface IText with
            member x.ToText() = name

        member x.SymbolInfo = symbolInfo

        override x.ToBoxedExpression() = var2expr x

    let getType (x: obj) : System.Type =
        match x with
        | :? IExpression as exp -> exp.DataType
        | :? IStorage as stg -> stg.DataType
        | :? IValue as value -> value.ObjValue.GetType()
        | _ -> failwithlog "ERROR"

    let createXgxVariable (name: string) (initValue: obj) comment : IXgxVar =
        (*
            "n0" is an incorrect variable.
            The folling characters are allowed:
            Only alphabet capital/small letters and '_' are allowed in the first letter.
            Only alphabet capital/small letters and '_' are allowed in the second letter.
            (e.g. variable1, _variable2, variableAB_3, SYMBOL, ...)
        *)
        match name with
        | "_1ON" | "_1OFF" -> ()
        | _ ->
            match name |> Seq.toList with
            | ch :: _ when isHangul ch -> ()
            | ch1 :: ch2 :: _ when isValidStart ch1 && isValidStart ch2 -> ()
            | _ -> failwith $"Invalid XGI variable name {name}.  Use longer name"

        match name with
        //| RegexMatches @"^ld(\d)+" -> failwith $"Invalid XGI variable name {name}."
        | RegexPattern @"^ld(\d)+" _ -> failwith $"Invalid XGI variable name {name}."
        | _ -> ()

        let createParam () =
            {   defaultStorageCreationParams (unbox initValue) (int VariableTag.PlcUserVariable) with
                    Name = name
                    Comment = Some comment }

        let typ = initValue.GetType()

        match typ.Name with
        | BOOL    -> XgxVar<bool>  (createParam ())
        | CHAR    -> XgxVar<char>  (createParam ())
        | FLOAT32 -> XgxVar<single>(createParam ())
        | FLOAT64 -> XgxVar<double>(createParam ())
        | INT16   -> XgxVar<int16> (createParam ())
        | INT32   -> XgxVar<int32> (createParam ())
        | INT64   -> XgxVar<int64> (createParam ())
        | INT8    -> XgxVar<int8>  (createParam ())
        | STRING  -> XgxVar<string>(createParam ())
        | UINT16  -> XgxVar<uint16>(createParam ())
        | UINT32  -> XgxVar<uint32>(createParam ())
        | UINT64  -> XgxVar<uint64>(createParam ())
        | UINT8   -> XgxVar<uint8> (createParam ())
        | "DuFunction" ->
            let defaultBool =
                {   defaultStorageCreationParams false (VariableTag.PlcUserVariable|>int) with
                        Name = name
                        Comment = Some comment }

            XgxVar<bool>(defaultBool)
        | _ -> failwithlog "ERROR"

[<AutoOpen>]
module rec TypeConvertorModule =
    type IXgiStatement =
        interface
        end

    type CommentedStatements = CommentedStatements of comment: string * statements: Statement list

    let (|CommentAndStatements|) = function | CommentedStatements(x, ys) -> x, ys

    let commentAndStatements = (|CommentAndStatements|)

    /// FunctionBlocks은 Timer와 같은 현재 측정 시간을 저장하는 Instance가 필요있는 Command 해당
    type FunctionBlock =
        | TimerMode of TimerStatement //endTag, time
        | CounterMode of CounterStatement // IExpressionizableTerminal *  CommandTag  * int  //endTag, countResetTag, count

        member x.GetInstanceText() =
            match x with
            | TimerMode timerStatement -> timerStatement.Timer.Name
            | CounterMode counterStatement -> counterStatement.Counter.Name

        interface IFunctionCommand with
            member this.TerminalEndTag: INamedExpressionizableTerminal =
                match this with
                | TimerMode timerStatement -> timerStatement.Timer.DN
                | CounterMode counterStatement -> counterStatement.Counter.DN


    /// Rung 의 Command 정의를 위한 type.
    ///Command = CoilCmd | PredicateCmd | FunctionCmd | ActionCmd | FunctionBlockCmd | XgkParamCmd
    /// 실행을 가지는 type
    type CommandTypes =
        /// 출력 코일
        | CoilCmd          of CoilOutputMode
        /// Predicate.  (boolean function).  비교 연산
        | PredicateCmd     of Predicate
        /// Non-boolean function.  사칙연산
        | FunctionCmd      of Function
        /// Action.  Move 등
        | ActionCmd        of PLCAction
        /// Timer, Counter 등
        | FunctionBlockCmd of FunctionBlock
        /// "Param="MOV,SRC,DST"" 와 같은 형태의 명령. int 는 명령의 길이.  대부분 3
        | XgkParamCmd      of string * int

//let createPLCCommandCopy(endTag, from, toTag) = FunctionPure.CopyMode(endTag, (from, toTag))

