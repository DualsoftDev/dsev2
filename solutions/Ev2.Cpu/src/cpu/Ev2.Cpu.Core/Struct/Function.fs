namespace Ev2.Cpu.Core

open System

/// 함수 아리티 (인자 개수)
type Arity = 
    | Fixed of int
    | Variable of min: int * max: int option
    | Any

/// 함수 시그니처
type FunctionSignature = {
    Name: string
    Arity: Arity
    Infer: DsDataType list -> DsDataType
}

/// 함수 검증
module Validation =
    let validateArity (arity: Arity) (argc: int) =
        match arity with
        | Fixed n when n = argc -> ()
        | Fixed n -> raise (ArgumentException($"Expected {n} arguments, got {argc}"))
        | Variable(min, Some max) when argc >= min && argc <= max -> ()
        | Variable(min, None) when argc >= min -> ()
        | Variable(min, Some max) ->
            raise (ArgumentException($"Expected {min} to {max} arguments, got {argc}"))
        | Variable(min, None) ->
            raise (ArgumentException($"Expected at least {min} arguments, got {argc}"))
        | Any -> ()

/// 함수 레지스트리
module Registry =
    
    let private functions = 
        [
            // 산술 연산
            { Name = "ADD"; Arity = Variable(2, None); 
              Infer = fun types ->
                if types |> List.exists ((=) TString) then TString
                elif types |> List.exists ((=) TDouble) then TDouble
                else TInt }
            
            { Name = "SUB"; Arity = Fixed 2;
              Infer = fun types ->
                if types |> List.exists ((=) TDouble) then TDouble
                else TInt }
            
            { Name = "MUL"; Arity = Variable(2, None);
              Infer = fun types ->
                if types |> List.exists ((=) TDouble) then TDouble
                else TInt }
            
            { Name = "DIV"; Arity = Fixed 2; Infer = fun _ -> TDouble }
            { Name = "MOD"; Arity = Fixed 2; Infer = fun _ -> TInt }
            { Name = "POW"; Arity = Fixed 2; Infer = fun _ -> TDouble }
            
            // 수학 함수
            { Name = "ABS"; Arity = Fixed 1;
              Infer = function | [TInt] -> TInt | _ -> TDouble }
            
            { Name = "SQRT"; Arity = Fixed 1; Infer = fun _ -> TDouble }
            { Name = "SIN"; Arity = Fixed 1; Infer = fun _ -> TDouble }
            { Name = "COS"; Arity = Fixed 1; Infer = fun _ -> TDouble }
            { Name = "TAN"; Arity = Fixed 1; Infer = fun _ -> TDouble }
            { Name = "LOG"; Arity = Fixed 1; Infer = fun _ -> TDouble }
            { Name = "EXP"; Arity = Fixed 1; Infer = fun _ -> TDouble }
            
            // 반올림 (1-2 arguments: value, optional decimal places)
            { Name = "ROUND"; Arity = Variable(1, Some 2); Infer = fun _ -> TDouble }
            { Name = "FLOOR"; Arity = Fixed 1; Infer = fun _ -> TDouble }
            { Name = "CEIL"; Arity = Fixed 1; Infer = fun _ -> TDouble }
            
            // 비교
            { Name = "MIN"; Arity = Variable(2, None);
              Infer = fun types ->
                if types |> List.exists ((=) TDouble) then TDouble
                else TInt }
            
            { Name = "MAX"; Arity = Variable(2, None);
              Infer = fun types ->
                if types |> List.exists ((=) TDouble) then TDouble
                else TInt }
            
            // 논리 연산
            { Name = "AND"; Arity = Variable(2, None); Infer = fun _ -> TBool }
            { Name = "OR"; Arity = Variable(2, None); Infer = fun _ -> TBool }
            { Name = "XOR"; Arity = Fixed 2; Infer = fun _ -> TBool }
            { Name = "NOT"; Arity = Fixed 1; Infer = fun _ -> TBool }
            
            // 비교 연산
            { Name = "EQ"; Arity = Fixed 2; Infer = fun _ -> TBool }
            { Name = "NE"; Arity = Fixed 2; Infer = fun _ -> TBool }
            { Name = "GT"; Arity = Fixed 2; Infer = fun _ -> TBool }
            { Name = "GE"; Arity = Fixed 2; Infer = fun _ -> TBool }
            { Name = "LT"; Arity = Fixed 2; Infer = fun _ -> TBool }
            { Name = "LE"; Arity = Fixed 2; Infer = fun _ -> TBool }
            
            // 조건문
            { Name = "IF"; Arity = Fixed 3;
              Infer = function
                | [TBool; t1; t2] when t1 = t2 -> t1
                | [TBool; t1; t2] when t1.IsNumeric && t2.IsNumeric -> TDouble
                | [TBool; _; _] -> TString
                | _ -> raise (ArgumentException("IF requires (Bool, any, any)")) }
            
            // 타입 변환
            { Name = "BOOL"; Arity = Fixed 1; Infer = fun _ -> TBool }
            { Name = "INT"; Arity = Fixed 1; Infer = fun _ -> TInt }
            { Name = "DOUBLE"; Arity = Fixed 1; Infer = fun _ -> TDouble }
            { Name = "STRING"; Arity = Fixed 1; Infer = fun _ -> TString }
            
            // 문자열 함수
            { Name = "CONCAT"; Arity = Variable(2, None); Infer = fun _ -> TString }
            { Name = "SUBSTRING"; Arity = Variable(2, Some 3); Infer = fun _ -> TString }
            { Name = "TRIM"; Arity = Fixed 1; Infer = fun _ -> TString }
            { Name = "UPPER"; Arity = Fixed 1; Infer = fun _ -> TString }
            { Name = "LOWER"; Arity = Fixed 1; Infer = fun _ -> TString }
            { Name = "LEN"; Arity = Fixed 1; Infer = fun _ -> TInt }
            { Name = "LENGTH"; Arity = Fixed 1; Infer = fun _ -> TInt }
            
            // PLC 타이머/카운터
            { Name = "TON"; Arity = Fixed 3; Infer = fun _ -> TBool }  // [enable, name, preset] - 2-arg form deprecated
            { Name = "TOF"; Arity = Variable(2, Some 3); Infer = fun _ -> TBool }  // [name, preset] or [enable, name, preset]
            { Name = "TP"; Arity = Variable(2, Some 3); Infer = fun _ -> TBool }   // [name, preset] or [trigger, name, preset]
            { Name = "CTU"; Arity = Variable(2, Some 3); Infer = fun _ -> TInt }   // [name, preset] or [name, enable, preset]
            { Name = "CTD"; Arity = Variable(2, Some 4); Infer = fun _ -> TInt }   // [name, preset] or [name, down, load, preset]
            { Name = "CTUD"; Arity = Fixed 5; Infer = fun _ -> TInt }              // [name, countUp, countDown, reset, preset]
            
            // 엣지 검출
            { Name = "RISING"; Arity = Fixed 1; Infer = fun _ -> TBool }
            { Name = "FALLING"; Arity = Fixed 1; Infer = fun _ -> TBool }
            
            // MOV 명령
            { Name = "MOV"; Arity = Fixed 2;
              Infer = function | [_; t] -> t | _ -> raise (ArgumentException("MOV requires 2 arguments")) }
        ]
        |> List.map (fun f -> f.Name.ToUpper(), f)
        |> Map.ofList
    
    let tryFind (name: string) =
        Map.tryFind (name.ToUpper()) functions

/// 공개 함수 API
module Functions =
    
    /// 함수 찾기
    let tryFind (name: string) = 
        Registry.tryFind name
    
    /// 인자 개수 검증
    let validateArity arity argc =
        Validation.validateArity arity argc
    
    /// 반환 타입 추론
    let inferReturn (name: string) (argTypes: DsDataType list) : DsDataType =
        match tryFind name with
        | Some sig1 ->
            validateArity sig1.Arity argTypes.Length
            sig1.Infer argTypes
        | None -> raise (ArgumentException($"Unknown function: {name}"))
       