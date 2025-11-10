namespace Ev2.Cpu.Parsing

open System
open System.Globalization
open Ev2.Cpu.Core

// ─────────────────────────────────────────────────────────────────────
// 렉서 (Lexer)
// ─────────────────────────────────────────────────────────────────────
// 텍스트 입력을 토큰 목록으로 변환
// ─────────────────────────────────────────────────────────────────────

/// Lexer for tokenizing expressions
module Lexer =

    let private isIdentifierStart (c: char) =
        Char.IsLetter(c) || c = '_' || c = '$'

    let private isIdentifierPart (c: char) =
        Char.IsLetterOrDigit(c) || c = '_' || c = '$' || c = '.' || c = '[' || c = ']'

    let private readWhile (text: string) (startPos: int) (predicate: char -> bool) =
        let mutable pos = startPos
        while pos < text.Length && predicate text.[pos] do
            pos <- pos + 1
        pos

    let private tryParseNumber (lexeme: string) : TokenType option =
        if lexeme.Contains('.') then
            match Double.TryParse(lexeme, NumberStyles.Float, CultureInfo.InvariantCulture) with
            | true, value -> Some (DoubleLiteral value)
            | false, _ -> None
        else
            match Int32.TryParse(lexeme, NumberStyles.Integer, CultureInfo.InvariantCulture) with
            | true, value -> Some (IntegerLiteral value)
            | false, _ -> None

    let private classifyIdentifier (text: string) : TokenType =
        let upper = text.ToUpperInvariant()
        match upper with
        | "TRUE" -> BooleanLiteral true
        | "FALSE" -> BooleanLiteral false
        | _ ->
            match Operators.tryParse upper with
            | Some op -> Operator op
            | None -> Identifier text

    let private tryParseDoubleCharOperator (c1: char) (c2: char) : DsOp option =
        match c1, c2 with
        | '=', '=' -> Some Eq
        | '!', '=' -> Some Ne
        | '<', '>' -> Some Ne
        | '>', '=' -> Some Ge
        | '<', '=' -> Some Le
        | '&', '&' -> Some And
        | '|', '|' -> Some Or
        | _ -> None

    let private tryParseSingleCharOperator (c: char) : DsOp option =
        match c with
        | '+' -> Some Add | '-' -> Some Sub | '*' -> Some Mul
        | '/' -> Some Div | '%' -> Some Mod
        | '=' -> Some Eq | '>' -> Some Gt | '<' -> Some Lt
        | '&' -> Some And | '|' -> Some Or
        | '!' -> Some Not | '~' -> Some BitNot
        | _ -> None

    // ─────────────────────────────────────────────────────────────────────
    // Tokenize 헬퍼 함수들 (코드 중복 제거 및 가독성 향상)
    // ─────────────────────────────────────────────────────────────────────

    /// 문자열 리터럴 파싱 (이스케이프 처리 포함)
    let private parseStringLiteral (input: string) (startPos: int) : Result<string * int, ParseError> =
        let length = input.Length
        let mutable endPos = startPos + 1
        let buffer = System.Text.StringBuilder()
        let mutable closed = false

        while endPos < length && not closed do
            let ch = input.[endPos]
            if ch = '\\' && endPos + 1 < length then
                let next = input.[endPos + 1]
                let escaped =
                    match next with
                    | '\\' -> '\\'
                    | '"' -> '"'
                    | 'n' -> '\n'
                    | 'r' -> '\r'
                    | 't' -> '\t'
                    | _ -> next
                buffer.Append(escaped) |> ignore
                endPos <- endPos + 2
            elif ch = '"' then
                closed <- true
                endPos <- endPos + 1
            else
                buffer.Append(ch) |> ignore
                endPos <- endPos + 1

        if not closed then
            Error (UnterminatedString startPos)
        else
            Ok (buffer.ToString(), endPos)

    /// 식별자 파싱 (변수명, 함수명, 키워드 등)
    let private parseIdentifier (input: string) (startPos: int) : TokenType * int =
        let endPos = readWhile input (startPos + 1) isIdentifierPart
        let lexeme = input.Substring(startPos, endPos - startPos)
        (classifyIdentifier lexeme, endPos)

    /// 숫자 리터럴 파싱 (정수 또는 실수)
    let private parseNumberLiteral (input: string) (startPos: int) : Result<TokenType * int, ParseError> =
        let endPos = readWhile input startPos (fun ch -> Char.IsDigit(ch) || ch = '.')
        let lexeme = input.Substring(startPos, endPos - startPos)
        match tryParseNumber lexeme with
        | Some tokenType -> Ok (tokenType, endPos)
        | None -> Error (InvalidNumberLiteral (lexeme, startPos))

    /// 연산자 파싱 (1문자 또는 2문자 연산자)
    let private parseOperatorToken (input: string) (pos: int) : Result<DsOp * int, ParseError> =
        let c = input.[pos]
        let length = input.Length

        // 2문자 연산자 먼저 시도
        if pos + 1 < length then
            match tryParseDoubleCharOperator c input.[pos + 1] with
            | Some op -> Ok (op, pos + 2)
            | None ->
                match tryParseSingleCharOperator c with
                | Some op -> Ok (op, pos + 1)
                | None -> Error (UnexpectedCharacter (c, pos))
        else
            match tryParseSingleCharOperator c with
            | Some op -> Ok (op, pos + 1)
            | None -> Error (UnexpectedCharacter (c, pos))

    /// Tokenize input text into tokens
    let tokenize (text: string) : ParseResult<Token list> =
        try
            let input = if isNull text then "" else text
            let length = input.Length
            let tokens = System.Collections.Generic.List<Token>()

            let addToken tokenType pos endPos =
                tokens.Add({
                    Type = tokenType
                    Position = pos
                    Lexeme = input.Substring(pos, endPos - pos)
                })

            let mutable pos = 0
            let mutable error = None

            while pos < length && error.IsNone do
                let c = input.[pos]

                if Char.IsWhiteSpace(c) then
                    pos <- pos + 1
                elif c = '(' then
                    addToken LeftParen pos (pos + 1)
                    pos <- pos + 1
                elif c = ')' then
                    addToken RightParen pos (pos + 1)
                    pos <- pos + 1
                elif c = ',' then
                    addToken Comma pos (pos + 1)
                    pos <- pos + 1
                elif c = '"' then
                    // 문자열 리터럴 (헬퍼 함수 사용)
                    match parseStringLiteral input pos with
                    | Ok (str, endPos) ->
                        addToken (StringLiteral str) pos endPos
                        pos <- endPos
                    | Error e ->
                        error <- Some e
                elif c = '↑' then
                    addToken (Operator Rising) pos (pos + 1)
                    pos <- pos + 1
                elif c = '↓' then
                    addToken (Operator Falling) pos (pos + 1)
                    pos <- pos + 1
                elif isIdentifierStart c then
                    // 식별자 (헬퍼 함수 사용)
                    let (tokenType, endPos) = parseIdentifier input pos
                    addToken tokenType pos endPos
                    pos <- endPos
                elif Char.IsDigit(c) then
                    // 숫자 리터럴 (헬퍼 함수 사용)
                    match parseNumberLiteral input pos with
                    | Ok (tokenType, endPos) ->
                        addToken tokenType pos endPos
                        pos <- endPos
                    | Error e ->
                        error <- Some e
                else
                    // 연산자 (헬퍼 함수 사용)
                    match parseOperatorToken input pos with
                    | Ok (op, endPos) ->
                        addToken (Operator op) pos endPos
                        pos <- endPos
                    | Error e ->
                        error <- Some e

            match error with
            | Some e -> Error e
            | None ->
                addToken EndOfFile length length
                Ok (tokens |> Seq.toList)
        with
        | ex -> Error (GeneralError ex.Message)
