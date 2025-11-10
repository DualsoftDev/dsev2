namespace Ev2.Cpu.Core

open System
open System.Globalization

[<AutoOpen>]
module ExpressionTagRegistry =
    let clearVariableRegistry () = DsTagRegistry.clear ()
    let getAllRegisteredVariables () = DsTagRegistry.all ()
    let getVariableByName name = DsTagRegistry.tryFind name

[<AutoOpen>]
module Expression =
    /// <summary>
    /// 표현식 (DsExpr) - PLC 제어 시스템의 모든 계산식과 논리식을 표현
    /// </summary>
    /// <remarks>
    /// DsExpr는 불변(immutable) 타입으로 모든 표현식을 재귀적 트리 구조로 표현합니다.
    /// 타입 안전성을 위해 InferType()으로 컴파일 시점에 타입 검증이 가능합니다.
    /// </remarks>
    /// <example>
    /// <code>
    /// // 상수 표현식
    /// let constant = Const(box 42, typeof<int>)
    ///
    /// // 변수 표현식
    /// let variable = Terminal(DsTag.Int("temperature"))
    ///
    /// // 산술 연산
    /// let sum = Binary(Add, Const(box 10, typeof<int>), Const(box 20, typeof<int>))
    ///
    /// // 함수 호출
    /// let abs = Function("ABS", [Const(box -5, typeof<int>)])
    /// </code>
    /// </example>
    [<StructuralEquality; NoComparison>]
    type DsExpr =
        /// <summary>상수 - 컴파일 시점에 값이 결정된 리터럴</summary>
        | Const     of obj * Type
        /// <summary>변수/IO - 런타임에 메모리에서 값을 읽는 태그</summary>
        | Terminal  of DsTag
        /// <summary>단항 연산 - NOT, Rising Edge, Falling Edge 등</summary>
        | Unary     of DsOp * DsExpr
        /// <summary>이항 연산 - +, -, *, /, AND, OR, ==, != 등</summary>
        | Binary    of DsOp * DsExpr * DsExpr
        /// <summary>함수 호출 - ADD, MAX, SUBSTR 등 내장 함수</summary>
        | Function  of string * DsExpr list
    
    let private quoteDsString (s: string) = 
        "\"" + s.Replace("\"", "\"\"") + "\""

    /// 표현식 확장
    type DsExpr with
        
        /// 타입 추론
        member e.InferType() : Type =
            match e with
            | Const(_, t) -> t
            | Terminal(tag) -> tag.StructType

            | Unary(op, x) ->
                let xt = x.InferType()
                // 단항 연산자 타입 검증 - validateForTypes 사용
                match DsOp.validateForTypes op (Some xt) None with
                | Some resultType -> resultType
                | None -> raise (ArgumentException($"{op} cannot operate on {xt}"))

            | Binary(op, l, r) ->
                let lt = l.InferType()
                let rt = r.InferType()

                // 특수 케이스: Add는 문자열 연결도 지원
                if op = Add && (lt = typeof<string> || rt = typeof<string>) then
                    typeof<string>
                else
                    // 모든 다른 연산자는 validateForTypes로 타입 검증 및 결과 타입 추론
                    match DsOp.validateForTypes op (Some lt) (Some rt) with
                    | Some resultType -> resultType
                    | None -> raise (ArgumentException($"{op} cannot operate on {lt} and {rt}"))

            | Function(name, args) ->
                // 각 인자 타입 수집
                let argTypes = args |> List.map (fun e -> e.InferType())
                // 규칙 기반 반환타입 추론 (예: DIV는 항상 typeof<double> 등)
                Functions.inferReturn name argTypes
        
        member e.Type = e.InferType()
        
        /// 타입 추론 (안전하게 시도)
        member e.TryInferType() : Type option =
            try
                Some(e.InferType())
            with
            | _ -> None
        
        /// 참조 변수 수집
        member e.Variables =
            let rec collect = function
                | Terminal(tag) -> Set.singleton tag.Name
                | Unary(_, x) -> collect x
                | Binary(_, l, r) -> Set.union (collect l) (collect r)
                | Function(_, args) -> args |> List.map collect |> Set.unionMany
                | Const _ -> Set.empty
            collect e
        
        /// 텍스트 변환
        member e.ToText() =
            let rec text = function
                | Const(v, _) ->
                    match v with
                    | :? string as s -> quoteDsString s
                    | :? double as d -> d.ToString(CultureInfo.InvariantCulture)
                    | v -> string v
                | Terminal(tag) -> tag.Name
                | Unary(op, x) -> sprintf "%O(%s)" op (text x)
                | Binary(op, l, r) -> sprintf "(%s %O %s)" (text l) op (text r)
                | Function(f, args) ->
                    sprintf "%s(%s)" f (args |> List.map text |> String.concat ", ")
            text e

        /// 상수 여부
        member e.IsConstant = 
            match e with Const _ -> true | _ -> false
        
        /// 복잡도 (노드 수)
        member e.Complexity =
            let rec count = function
                | Const _ | Terminal _ -> 1
                | Unary(_, x) -> 1 + count x
                | Binary(_, l, r) -> 1 + count l + count r
                | Function(_, args) -> 1 + List.sumBy count args
            count e
    
    // === 표현식 빌더 ===

    // 상수
    let num (n: int) = Const(box n, typeof<int>)
    let dbl (d: double) = Const(box d, typeof<double>)
    let str (s: string) = Const(box s, typeof<string>)
    let bool (b: bool) = Const(box b, typeof<bool>)
    
    // 변수 - 레지스트리를 통해 유일한 인스턴스 보장
    let var name typ = Terminal(DsTag.Create(name, typ))
    let intVar name = Terminal(DsTag.Int(name))
    let dblVar name = Terminal(DsTag.Double(name))
    let strVar name =
        match DsTagRegistry.tryFind name with
        | Some tag when tag.StructType = typeof<string> ->
            Terminal(tag)
        | Some _ ->
            // 이미 다른 타입으로 등록된 경우 문자열 상수로 취급
            Const(box name, typeof<string>)
        | None ->
            Terminal(DsTag.String(name))
    let boolVar name = Terminal(DsTag.Bool(name))
    
    // 변수 타입이 알려진 경우 기존 변수 재사용
    let existingVar name =
        match getVariableByName name with
        | Some tag -> Terminal(tag)
        | None -> raise (InvalidOperationException($"Variable '{name}' not found in registry. Declare it first."))
    
    // 함수 - 간결한 정의
    let fn name args = Function(name, args)
    let call name args = Function(name, args)  // 호환성을 위해 유지
    let add args = Function("ADD", args)
    let sub l r = Function("SUB", [l; r])
    let mul args = Function("MUL", args)
    let div l r = Function("DIV", [l; r])
    
    // Set/Reset 연산 빌더
    let set expr = expr
    let reset expr = Unary(Not, expr)
    let setReset s r = Binary(And, s, Unary(Not, r))
    let latch s r prev = Binary(Or, Binary(And, s, Unary(Not, r)), Binary(And, prev, Unary(Not, r)))
    
    // 조건 빌더
    let when' cond expr = Binary(And, cond, expr)
    let unless cond expr = Binary(And, Unary(Not, cond), expr)
    
    // 엣지 검출
    let rising expr = Unary(Rising, expr)
    let falling expr = Unary(Falling, expr)
    
    // 논리 연산 체인
    let all exprs = exprs |> List.reduce (fun a b -> Binary(And, a, b))
    let any exprs = exprs |> List.reduce (fun a b -> Binary(Or, a, b))
    let none exprs = exprs |> List.map (fun e -> Unary(Not, e)) |> all

    type DsTag with
        member x.Expr = Terminal(x)
        member x.Set expr = set expr
        member x.Reset expr = reset expr
        member x.SetReset s r = setReset s r    
