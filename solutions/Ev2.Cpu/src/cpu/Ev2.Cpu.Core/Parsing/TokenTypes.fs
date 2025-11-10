namespace Ev2.Cpu.Parsing

open Ev2.Cpu.Core

// ─────────────────────────────────────────────────────────────────────
// 토큰 타입 정의 (Token Types)
// ─────────────────────────────────────────────────────────────────────
// 렉서와 파서에서 사용하는 토큰 타입 정의
// ─────────────────────────────────────────────────────────────────────

/// Token types for lexical analysis
[<StructuralEquality; NoComparison>]
type TokenType =
    | Identifier of string
    | IntegerLiteral of int
    | DoubleLiteral of double
    | StringLiteral of string
    | BooleanLiteral of bool
    | Operator of DsOp
    | LeftParen
    | RightParen
    | Comma
    | EndOfFile

/// Token with position information
type Token = {
    Type: TokenType
    Position: int
    Lexeme: string
}
