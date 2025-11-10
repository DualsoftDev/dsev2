namespace Ev2.Cpu.Parsing

/// Parse error with detailed position information
[<StructuralEquality; NoComparison>]
type ParseError =
    | UnexpectedCharacter of char * position:int
    | UnterminatedString of startPosition:int
    | InvalidNumberLiteral of lexeme:string * position:int
    | UnexpectedToken of expected:string * actual:string * position:int
    | UnexpectedEndOfInput of expected:string
    | TrailingTokens of position:int
    | ValidationError of message:string
    | GeneralError of message:string
    with
        member this.ToMessage() =
            match this with
            | UnexpectedCharacter (ch, pos) ->
                sprintf "Unexpected character '%c' at position %d" ch pos
            | UnterminatedString pos ->
                sprintf "Unterminated string literal at position %d" pos
            | InvalidNumberLiteral (lexeme, pos) ->
                sprintf "Invalid number literal '%s' at position %d" lexeme pos
            | UnexpectedToken (expected, actual, pos) ->
                sprintf "Expected %s but found '%s' at position %d" expected actual pos
            | UnexpectedEndOfInput expected ->
                sprintf "Unexpected end of input, expected %s" expected
            | TrailingTokens pos ->
                sprintf "Unexpected trailing tokens starting at position %d" pos
            | ValidationError msg ->
                sprintf "Validation failed: %s" msg
            | GeneralError msg -> msg

/// Result type for parsing operations
type ParseResult<'T> = Result<'T, ParseError>
