namespace Ev2.Cpu.Parsing

open System
open Ev2.Cpu.Core
open Ev2.Cpu.Ast

/// Options for parsing expressions
type ParsingOptions = {
    VariableResolver: (string -> DsDataType option) option
    TreatAsTerminal: (string -> bool) option
    AllowMetadata: bool
}
with
    static member Default = {
        VariableResolver = None
        TreatAsTerminal = None
        AllowMetadata = false
    }

/// Parser for expressions using Pratt parsing
module Parser =

    /// Token cursor for parsing
    type private Cursor(tokens: Token list) =
        let tokenArray = tokens |> List.toArray
        let mutable index = 0

        member _.Current =
            if index < tokenArray.Length then tokenArray.[index]
            else { Type = EndOfFile; Position = -1; Lexeme = "" }

        member _.Advance() =
            if index < tokenArray.Length - 1 then index <- index + 1

        member _.Expect(expectedType: TokenType) : ParseResult<Token> =
            let current = tokenArray.[index]
            match current.Type with
            | t when t = expectedType ->
                index <- index + 1
                Ok current
            | EndOfFile ->
                Error (UnexpectedEndOfInput (sprintf "%A" expectedType))
            | _ ->
                Error (UnexpectedToken (sprintf "%A" expectedType, current.Lexeme, current.Position))

        member _.IsAtEnd =
            match tokenArray.[index].Type with
            | EndOfFile -> true
            | _ -> false

    let private resolveVariableType (options: ParsingOptions) (name: string) =
        match options.VariableResolver with
        | Some resolver -> resolver name |> Option.defaultValue TString
        | None -> TString

    let private isTerminal (options: ParsingOptions) (name: string) =
        match options.TreatAsTerminal with
        | Some predicate -> predicate name
        | None -> false

    /// Parse expression with Pratt precedence climbing
    let rec private parseExpression (cursor: Cursor) (options: ParsingOptions) (minPrecedence: int) : ParseResult<DsExpr> =
        match parseUnary cursor options with
        | Error e -> Error e
        | Ok left ->
            let mutable result = Ok left
            let mutable shouldContinue = true

            while shouldContinue && not cursor.IsAtEnd do
                match cursor.Current.Type with
                | Operator op when not op.IsUnary && op.Priority >= minPrecedence ->
                    cursor.Advance()
                    // Right-associative operators: use op.Priority for recursion
                    // Left-associative operators: use op.Priority + 1 for recursion
                    let nextPrecedence = if op.IsRightAssociative then op.Priority else op.Priority + 1
                    match parseExpression cursor options nextPrecedence with
                    | Error e ->
                        shouldContinue <- false
                        result <- Error e
                    | Ok right ->
                        match result with
                        | Ok leftExpr -> result <- Ok (EBinary(op, leftExpr, right))
                        | Error _ -> shouldContinue <- false
                | _ -> shouldContinue <- false

            result

    /// Parse unary expressions
    and private parseUnary (cursor: Cursor) (options: ParsingOptions) : ParseResult<DsExpr> =
        match cursor.Current.Type with
        | Operator op when op.IsUnary ->
            cursor.Advance()
            match parseUnary cursor options with
            | Error e -> Error e
            | Ok expr -> Ok (EUnary(op, expr))
        | _ -> parsePrimary cursor options

    /// Parse primary expressions (literals, variables, function calls, parentheses)
    and private parsePrimary (cursor: Cursor) (options: ParsingOptions) : ParseResult<DsExpr> =
        match cursor.Current.Type with
        | IntegerLiteral i ->
            cursor.Advance()
            Ok (EConst(box i, TInt))

        | DoubleLiteral d ->
            cursor.Advance()
            Ok (EConst(box d, TDouble))

        | StringLiteral s ->
            cursor.Advance()
            Ok (EConst(box s, TString))

        | BooleanLiteral b ->
            cursor.Advance()
            Ok (EConst(box b, TBool))

        | Identifier name ->
            cursor.Advance()
            match cursor.Current.Type with
            | LeftParen ->
                // Function call
                cursor.Advance()

                if cursor.Current.Type = RightParen then
                    // Empty argument list
                    cursor.Advance()
                    Ok (ECall(name, []))
                else
                    // Parse arguments
                    let rec parseArgs acc =
                        match parseExpression cursor options 0 with
                        | Error e -> Error e
                        | Ok arg ->
                            let newAcc = arg :: acc
                            match cursor.Current.Type with
                            | Comma ->
                                cursor.Advance()
                                parseArgs newAcc
                            | RightParen ->
                                cursor.Advance()
                                Ok (List.rev newAcc)
                            | EndOfFile ->
                                Error (UnexpectedEndOfInput "')' or ','")
                            | _ ->
                                Error (UnexpectedToken ("')' or ','", cursor.Current.Lexeme, cursor.Current.Position))

                    match parseArgs [] with
                    | Error e -> Error e
                    | Ok args -> Ok (ECall(name, args))

            | _ ->
                // Variable or terminal
                let varType = resolveVariableType options name
                if isTerminal options name then
                    Ok (ETerminal(name, varType))
                else
                    Ok (EVar(name, varType))

        | LeftParen ->
            cursor.Advance()
            match parseExpression cursor options 0 with
            | Error e -> Error e
            | Ok expr ->
                match cursor.Expect(RightParen) with
                | Error e -> Error e
                | Ok _ -> Ok expr

        | EndOfFile ->
            Error (UnexpectedEndOfInput "expression")

        | t ->
            Error (UnexpectedToken ("expression", sprintf "%A" t, cursor.Current.Position))

    /// Parse expression from text
    let parseExpressionText (text: string) (options: ParsingOptions) : ParseResult<DsExpr> =
        match Lexer.tokenize text with
        | Error e -> Error e
        | Ok tokens ->
            let cursor = Cursor(tokens)
            match parseExpression cursor options 0 with
            | Error e -> Error e
            | Ok expr ->
                if not cursor.IsAtEnd then
                    match cursor.Current.Type with
                    | EndOfFile -> Ok expr
                    | _ -> Error (TrailingTokens cursor.Current.Position)
                else
                    Ok expr

    /// Parse expression with variable type table
    let parseWithTypeTable (text: string) (typeTable: System.Collections.Generic.IDictionary<string, DsDataType>) : ParseResult<DsExpr> =
        let resolver name =
            match typeTable.TryGetValue name with
            | true, typ -> Some typ
            | false, _ -> None

        let options = { ParsingOptions.Default with VariableResolver = Some resolver }
        parseExpressionText text options

    /// Parse expression with simple variable resolution
    let parseSimple (text: string) : ParseResult<DsExpr> =
        parseExpressionText text ParsingOptions.Default

/// High-level parsing utilities
module ParsingHelpers =

    /// Create a type table from variable definitions
    let createTypeTable (variables: (string * DsDataType) seq) =
        let dict = System.Collections.Generic.Dictionary<string, DsDataType>()
        for (name, typ) in variables do
            dict.[name] <- typ
        dict

    /// Parse and validate expression
    let parseAndValidate (text: string) (typeTable: System.Collections.Generic.IDictionary<string, DsDataType>) : ParseResult<DsExpr> =
        match Parser.parseWithTypeTable text typeTable with
        | Error e -> Error e
        | Ok expr ->
            match expr.Validate() with
            | Error e -> Error (ValidationError e)
            | Ok () -> Ok expr

    /// Parse expression with automatic type inference
    let parseWithInference (text: string) (knownVariables: Set<string>) : Result<DsExpr * Set<string>, ParseError> =
        match Parser.parseSimple text with
        | Error e -> Error e
        | Ok expr ->
            let usedVariables = expr.GetVariables()
            let unknownVariables = Set.difference usedVariables knownVariables
            Ok (expr, unknownVariables)

    /// Batch parse multiple expressions
    let parseMultiple (expressions: string list) (typeTable: System.Collections.Generic.IDictionary<string, DsDataType>) : ParseResult<DsExpr list> =
        let rec parseAll remaining acc =
            match remaining with
            | [] -> Ok (List.rev acc)
            | text :: rest ->
                match Parser.parseWithTypeTable text typeTable with
                | Error e -> Error e
                | Ok expr -> parseAll rest (expr :: acc)

        parseAll expressions []
