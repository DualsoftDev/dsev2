namespace Ev2.Cpu.Generation.Make

open System
open Ev2.Cpu.Core
open Ev2.Cpu.Core.Expression
open Ev2.Cpu.Core.Statement
open Ev2.Cpu.Generation.Core

/// 표현식 코드 생성
module ExpressionGen =

    /// 기본 표현식 생성자들
    let boolExpr value = Const(box value, DsDataType.TBool)
    let intExpr value = Const(box value, DsDataType.TInt)
    let doubleExpr value = Const(box value, DsDataType.TDouble)
    let stringExpr value = Const(box value, DsDataType.TString)

    /// 변수 참조 생성
    let varRef name dataType = Terminal(DsTag.Create(name, dataType))
    let boolVar name = varRef name DsDataType.TBool
    let intVar name = varRef name DsDataType.TInt
    let doubleVar name = varRef name DsDataType.TDouble
    let stringVar name = varRef name DsDataType.TString

    /// 이진 연산자
    let add left right = Binary(DsOp.Add, left, right)
    let sub left right = Binary(DsOp.Sub, left, right)
    let mul left right = Binary(DsOp.Mul, left, right)
    let div left right = Binary(DsOp.Div, left, right)
    let eq left right = Binary(DsOp.Eq, left, right)
    let ne left right = Binary(DsOp.Ne, left, right)
    let gt left right = Binary(DsOp.Gt, left, right)
    let ge left right = Binary(DsOp.Ge, left, right)
    let lt left right = Binary(DsOp.Lt, left, right)
    let le left right = Binary(DsOp.Le, left, right)
    let and' left right = Binary(DsOp.And, left, right)
    let or' left right = Binary(DsOp.Or, left, right)

    /// 단항 연산자
    let not' expr = Unary(DsOp.Not, expr)
    let rising expr = Unary(DsOp.Rising, expr)
    let falling expr = Unary(DsOp.Falling, expr)

    /// 함수 호출
    let call name args = Function(name, args)

    /// 타이머 함수들
    let ton name enable preset = call "TON" [enable; stringExpr name; intExpr preset]
    let tof name enable preset = call "TOF" [enable; stringExpr name; intExpr preset]
    let tp name trigger preset = call "TP" [trigger; stringExpr name; intExpr preset]

    /// 카운터 함수들
    // MAJOR FIX (DEFECT-022-6): CTU requires 4-arg form [name; countUp; reset; preset]
    // Previous 3-arg form omitted reset parameter, breaking state machines
    let ctu name enable reset preset = call "CTU" [stringExpr name; enable; reset; intExpr preset]
    let ctd name down load preset = call "CTD" [stringExpr name; down; load; intExpr preset]
    let ctud name countUp countDown reset preset = call "CTUD" [stringExpr name; countUp; countDown; reset; intExpr preset]

    /// 비교 함수들
    let limit minVal value maxVal = call "LIMIT" [minVal; value; maxVal]
    let max' left right = call "MAX" [left; right]
    let min' left right = call "MIN" [left; right]

    /// 수학 함수들
    let abs' expr = call "ABS" [expr]
    let sqrt' expr = call "SQRT" [expr]
    let sin' expr = call "SIN" [expr]
    let cos' expr = call "COS" [expr]
    let tan' expr = call "TAN" [expr]

    /// 문자열 함수들
    let concat left right = call "CONCAT" [left; right]
    let len expr = call "LEN" [expr]
    let mid expr start length = call "MID" [expr; start; length]