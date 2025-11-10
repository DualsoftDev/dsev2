namespace Ev2.Cpu.Core

open System
open Ev2.Cpu.Core

/// 연산자 타입 검증 모듈
module OperatorValidation =
    
    /// 단항 연산자 유효성 검증
    let validateUnary (op: DsOp) (operandType: Type) : Type =
        match op with
        | Not ->
            if operandType = typeof<bool> then typeof<bool>
            else raise (ArgumentException($"NOT operator requires boolean operand, got {operandType}"))

        | BitNot ->
            if operandType = typeof<int> then typeof<int>
            else raise (ArgumentException($"Bitwise NOT operator requires integer operand, got {operandType}"))

        | Rising | Falling | Edge ->
            if operandType = typeof<bool> then typeof<bool>
            else raise (ArgumentException($"Edge detection operator '{op.ToString()}' requires boolean operand, got {operandType}"))

        | _ ->
            raise (ArgumentException($"'{op.ToString()}' is not a unary operator"))
    
    /// 이항 연산자 유효성 검증
    let validateBinary (op: DsOp) (leftType: Type) (rightType: Type) : Type =
        match op with
        // 논리 연산자
        | And | Or | Xor | Nand | Nor ->
            if leftType = typeof<bool> && rightType = typeof<bool> then
                typeof<bool>
            else
                raise (ArgumentException($"Logical operator '{op.ToString()}' requires boolean operands, got {leftType} and {rightType}"))

        // 비교 연산자
        | Eq | Ne ->
            // 모든 타입 간 비교 가능
            typeof<bool>

        | Gt | Ge | Lt | Le ->
            // 숫자 또는 문자열 비교
            if (TypeHelpers.isNumericType leftType && TypeHelpers.isNumericType rightType) ||
               (leftType = typeof<string> && rightType = typeof<string>) then
                typeof<bool>
            else
                raise (ArgumentException($"Comparison operator '{op.ToString()}' requires numeric or string operands of same type, got {leftType} and {rightType}"))

        // 산술 연산자
        | Add ->
            if leftType = typeof<string> || rightType = typeof<string> then
                typeof<string>  // 문자열 결합
            elif leftType = typeof<double> || rightType = typeof<double> then
                typeof<double>  // 실수 승격
            elif leftType = typeof<int> && rightType = typeof<int> then
                typeof<int>
            else
                raise (ArgumentException($"ADD operator cannot be applied to operands of type {leftType} and {rightType}"))

        | Sub | Mul | Div | Pow ->
            if TypeHelpers.isNumericType leftType && TypeHelpers.isNumericType rightType then
                if leftType = typeof<double> || rightType = typeof<double> then
                    typeof<double>
                else
                    typeof<int>
            else
                raise (ArgumentException($"Arithmetic operator '{op.ToString()}' requires numeric operands, got {leftType} and {rightType}"))

        | Mod ->
            if leftType = typeof<int> && rightType = typeof<int> then
                typeof<int>
            else
                raise (ArgumentException($"MOD operator requires integer operands, got {leftType} and {rightType}"))

        // 비트 연산자
        | BitAnd | BitOr | BitXor | ShiftLeft | ShiftRight ->
            if leftType = typeof<int> && rightType = typeof<int> then
                typeof<int>
            else
                raise (ArgumentException($"Bitwise operator '{op.ToString()}' requires integer operands, got {leftType} and {rightType}"))

        // 특수 연산자
        | Assign | Move ->
            if leftType = rightType then
                rightType
            elif leftType = typeof<int> && rightType = typeof<double> then
                typeof<double>
            else
                raise (ArgumentException($"Assignment operator '{op.ToString()}' requires compatible operands, got {leftType} and {rightType}"))

        | Coalesce ->
            leftType

        | _ ->
            raise (ArgumentException($"'{op.ToString()}' is not a binary operator"))

    /// 피연산자 개수에 따라 자동 분기
    let validate (op: DsOp) (operands: Type list) : Type =
        match op.IsUnary, operands with
        | true, [ operand ] -> validateUnary op operand
        | false, [ left; right ] -> validateBinary op left right
        | true, _ ->
            raise (ArgumentException($"Operator '{op.ToString()}' expects one operand, received {operands.Length}"))
        | false, _ ->
            raise (ArgumentException($"Operator '{op.ToString()}' expects two operands, received {operands.Length}"))
