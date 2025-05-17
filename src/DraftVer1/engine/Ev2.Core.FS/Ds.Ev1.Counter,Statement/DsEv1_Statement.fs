namespace Dual.Ev2

open System
open System.Reactive.Linq
open System.Reactive.Disposables

open Dual.Common.Core.FS
open Dual.Common.Base


[<AutoOpen>]
module rec StatementModule =


    type TimerStatement = {
        Timer:Timer
        RungInCondition: IExpression<bool> option
        ResetCondition:  IExpression<bool> option
        /// Timer 생성시의 function name
        FunctionName:string
    }


    type CounterStatement = {
        Counter        : Counter
        UpCondition    : IExpression<bool> option
        DownCondition  : IExpression<bool> option
        ResetCondition : IExpression<bool> option
        // XGI only
        LoadCondition  : IExpression<bool> option
        /// Counter 생성시의 function name
        FunctionName   : string
    }

    type CounterStatement with
        /// CounterStatement 의 UpCondition 또는 DownCondition 반환
        member x.GetUpOrDownCondition() : IExpression<bool> = [x.DownCondition; x.UpCondition] |> List.choose id |> List.exactlyOne




    type CopyUdtStatement = {
        Storages:Storages
        UdtDecl:UdtDecl
        Condition:IExpression<bool>
        Source:string
        Target:string
    }

    type ActionStatement =
        //| DuCopy of condition:IExpression<bool> * source:IExpression * target:IStorage
        | DuCopyUdt of CopyUdtStatement


    type FunctionParameters = {
        /// Enable bit
        Condition:IExpression<bool> option

        FunctionName:string
        Arguments:Arguments
        /// IExpression 으로 casting 이 필요한 경우를 위해 IExpression 저장
        /// - e.g "&&" Fuction 인 경우, Arguments 전체의 IExression 이 필요
        OriginalExpression:IExpression
        /// Function output store target
        Output:IStorage
    }


    /// e.g 가령 Person UDT 에서 "int age", 혹은 Lambda function 의 arg list;
    type TypeDecl = {
        Type:System.Type
        Name:string
    }

    type LambdaDecl = {
        Prototype:TypeDecl
        Arguments:TypeDecl list
        Body:IExpression
    }

    type LambdaApplication = {
        LambdaDecl:LambdaDecl
        Arguments:IExpression list
        Storages:Storages
    }
    // e.g struct Person { string name; int age; };
    type UdtDecl = {
        TypeName:string
        Members:TypeDecl list
    }

    // e.g Person people[10];
    type UdtDef = {
        TypeName:string
        VarName:string
        /// Array 가 아닌 경우, 1 의 값을 가짐.  array 인 경우 1보다 큰 값.  array index 는 0 부터 시작
        ArraySize:int
    }


    type ProcDecl = {
        ProcName:string
        Arguments:TypeDecl list
        Bodies:StatementContainer
    }

    type ProcCall = {
        ProcDecl:ProcDecl
        ActuralPrameters:Arguments
    }


    type Statement =
        /// 변수 선언.  e.g "int a = $pi + 3;"  초기값 처리에 주의
        ///
        /// XGI : 선언한 변수의 초기값으로 설정.  Rung 생성에는 관여되지 않음
        /// XGK : _1ON 을 조건으로 한 대입문으로 rung 생성.  (XGK 에서는 변수 초기값이 지원되지 않음)
        ///
        /// Ladder 생성 시점에는 DuVarDecl statement 는 존재하지 않는다.  변수 선언 혹은 assign 문으로 사전에 변환된다.
        | DuVarDecl of expression:IExpression * variable:ValueHolder

        /// User Defined Type (structure) 선언.  e.g "struct Person { string name; int age; };"
        | DuUdtDecl of UdtDecl

        | DuLambdaDecl of LambdaDecl
        | DuProcDecl of ProcDecl
        | DuProcCall of ProcCall

        /// UDT instances 정의.  e.g "Person peopole[10];"
        | DuUdtDef of UdtDef

        /// 대입문.  e.g "$a = $b + 3;"
        ///
        /// condition 조건이 만족할 경우, expression 을 target 에 대입.  condition None 인 경우, _ON 의 의미.
        | DuAssign of condition:IExpression<bool> option * expression:IExpression * target:IStorage

        | DuTimer   of TimerStatement
        | DuCounter of CounterStatement
        | DuAction  of ActionStatement

        /// PLC function (비교, 사칙연산, Move) 을 호출하는 statement
        ///
        /// - 주로 XGI 에서 사용.  XGK 에서는 zipAndExpression 와 Statement.ToStatements() 에서 사용.
        | DuPLCFunction of FunctionParameters
        with
            interface IStatement

    /// 추가 가능한 Statement container
    type StatementContainer = ResizeArray<Statement>

    type CommentedStatement =
        | CommentedStatement of comment:string * statement:Statement
        member x.Statement = match x with | CommentedStatement (_c, s) -> s
        member x.TargetName =
            match x.Statement with
            | DuAssign  (_, _expression, target) -> target.Name
            | DuVarDecl (_expression,variable)   -> variable.Name
            | DuTimer   (t:TimerStatement)       -> t.Timer.Name
            | DuCounter (c:CounterStatement)     -> c.Counter.Name
            | DuAction  (a:ActionStatement)      ->
                match a with
                //| DuCopy (_condition:IExpression<bool>, _source:IExpression,target:IStorage)-> target.Name
                | DuCopyUdt { Target = target } -> target
            | DuPLCFunction { FunctionName = fn } -> fn
            | (DuUdtDecl _ | DuUdtDef _) -> failwith "Unsupported.  Should not be called for these statements"
            | (DuLambdaDecl _ | DuProcDecl _ | DuProcCall _) ->
                failwith "ERROR: Not yet implemented"       // 추후 subroutine 사용시, 필요에 따라 세부 구현

        member x.TargetValue =
            match x.Statement with
            | DuAssign  (_, _expression, target) -> target.OValue
            | DuVarDecl (_expression,variable)   -> variable.OValue
            | DuTimer   (t:TimerStatement)       -> t.Timer.DN.OValue
            | DuCounter (c:CounterStatement)     -> c.Counter.DN.OValue
            | DuAction  (a:ActionStatement)      ->
                match a with
                //| DuCopy (_condition:IExpression<bool>, _source:IExpression,target:IStorage)-> target.BoxedValue
                | DuCopyUdt _ -> failwith "ERROR: Invalid value reference"
            | DuPLCFunction { OriginalExpression = exp } ->  exp.OValue
            | (DuUdtDecl _ | DuUdtDef _) -> failwith "Unsupported.  Should not be called for these statements"
            | (DuLambdaDecl _ | DuProcDecl _ | DuProcCall _) ->
                failwith "ERROR: Not yet implemented"       // 추후 subroutine 사용시, 필요에 따라 세부 구현


    /// UDT 구조체 멤버 값 복사.  source 및 target 이 string 으로 주어진다. (e.g "people[0]", "hong")
    /// PC 버젼에서는 UDT 변수 복사에 대한 실제 실행문.
    let copyUdt (storages:Storages) (decl:UdtDecl) (source:string) (target:string): unit =
        for m in decl.Members do
            let s, t = $"{source}.{m.Name}", $"{target}.{m.Name}"
            storages[t].OValue <- storages[s].OValue

    type IStatement with
        member x.Do() =
            let x = x :?> Statement
            match x with
            | DuAssign (condition, expr, target) ->
                if target.Type <> expr.Type then
                    failwith $"ERROR: {target.Name} Type mismatch in assignment statement"

                let isEvaluate = match condition with
                                 | None -> true
                                 | Some condi -> condi.TValue

                if isEvaluate then
                    target.OValue <- expr.OValue

            | DuVarDecl (expr, target) ->
                if target.Type <> expr.Type then
                    failwith $"ERROR: {target.Name} Type mismatch in assignment statement"
                target.OValue <- expr.OValue

            | DuTimer timerStatement ->
                for s in timerStatement.Timer.InputEvaluateStatements do
                    s.Do()

            | DuCounter counterStatement ->
                for s in counterStatement.Counter.InputEvaluateStatements do
                    s.Do()

            //| DuAction (DuCopy (condition, source, target)) ->
            //    if condition.EvaluatedValue then
            //        target.BoxedValue <- source.BoxedEvaluatedValue

            | DuAction (DuCopyUdt { Storages=storages; UdtDecl=udtDecl; Condition=condition; Source=source; Target=target}) ->
                if condition.TValue then
                    // 구조체 멤버 복사
                    copyUdt storages udtDecl source target

            | (DuUdtDecl _ | DuUdtDef _ | DuLambdaDecl _ | DuProcDecl _) -> ()  // OK: Noting todo

            | DuProcCall _ ->
                failwithlog "ERROR: Procedure call not yet implemented."

            | DuPLCFunction _ ->
                failwithlog "ERROR"


