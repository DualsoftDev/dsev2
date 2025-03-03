namespace Ev2.Parser.FS
open Ev2.Parser

open System
open System.IO
open System.Linq

open Antlr4.Runtime
open Antlr4.Runtime.Tree
open Dual.Common.Core.FS

//open Dual.Common.Core.FS
//open Dual.Common.Base.FS



open System.IO
open System.Runtime.InteropServices
open Antlr4.Runtime
open Dual.Common.Core.FS
open Dual.Common.Base.FS

type ParserErrorRecord(line: int, column: int, message: string, ambient: string) =
    member val Line = line
    member val Column = column
    member val Message = message
    member val Ambient = ambient


type ParserError(message: string) =
    inherit Exception(message)

    static member CreatePositionInfo (ctx: obj):string * string = // RuleContext or IErrorNode
        let getPosition (ctx: obj) =
            let fromToken (token: IToken) = $"[line:{token.Line}, column:{token.Column}]"

            let fromErrorNode (errNode: IErrorNode) =
                match errNode with
                | :? ErrorNodeImpl as impl -> fromToken (impl.Symbol)
                | _ -> failwithlog "ERROR"

            match ctx with
            | :? ParserRuleContext as prctx ->
                match prctx.Start with
                | :? CommonToken as start -> fromToken (start)
                | _ -> failwithlog "ERROR"
            | :? IErrorNode as errNode -> fromErrorNode (errNode)
            | _ -> failwithlog "ERROR"

        let getAmbient (ctx: obj) =
            match ctx with
            | :? IParseTree as pt -> pt.GetText()
            | _ -> failwithlog "ERROR"

        let posi = getPosition (ctx)
        let ambient = getAmbient (ctx)
        posi, ambient

    new(message: string, ctx: RuleContext) = let posi, ambi = ParserError.CreatePositionInfo(ctx) in ParserError($"{message} on \n\n\n{posi} near '{ambi}'")
    new(message: string, errorNode: IErrorNode) = let posi, ambi = ParserError.CreatePositionInfo(errorNode) in ParserError($"{message} on \n\n\n{posi} near '{ambi}'")
    new(message: string, line: int, column: int) = ParserError($"{message} \n\nCheck near\n\n [line:{line}, column:{column}]")

type ErrorListener<'Symbol>([<Optional; DefaultParameterValue(false)>] throwOnError) =
    inherit ConsoleErrorListener<'Symbol>()

    member val Errors = ResizeArray<ParserErrorRecord>()

    override x.SyntaxError
        (
            output: TextWriter,
            recognizer: IRecognizer,
            offendingSymbol: 'Symbol,
            line: int,
            col: int,
            msg: string,
            e: RecognitionException
        ) =
        let dsFile = recognizer.GrammarFileName

        match recognizer with
        | :? Parser as parser ->
            let ambient = parser.RuleContext.GetText()
            base.SyntaxError(output, recognizer, offendingSymbol, line, col, msg, e)
            tracefn ($"Parser error on [{line}:{col}]@{dsFile}: {msg}")
            x.Errors.Add(new ParserErrorRecord(line, col, msg, ambient))

            if throwOnError then
                ParserError($"{msg} near {ambient}", line, col) |> raise
        | :? Lexer ->
            tracefn ($"Lexer error on [{line}:{col}]@{dsFile}: {msg}")
            x.Errors.Add(new ParserErrorRecord(line, col, msg, ""))

            if throwOnError then
                ParserError($"Lexical error : {msg}", line, col) |> raise
        | _ -> failwithlog "ERROR"

[<AutoOpen>]
module ParserCommonModule =
    type ParseTreePredicate = IParseTree -> bool

    type IParseTree with

        member x.Descendants<'T when 'T :> IParseTree>
          (
            ?includeMe: bool,
            ?predicate: ParseTreePredicate,
            ?exclude: ParseTreePredicate
          ) : 'T seq =

            let includeMe = includeMe |? false
            let predicate = predicate |? (isType<'T>)
            let exclude = exclude |? (fun _ -> false)

            let rec helper (frm: IParseTree, incMe: bool) =
                seq {
                    if not (exclude (frm)) then
                        if (incMe && predicate (frm)) then
                            yield forceCast<'T> (frm)

                        for index in [ 0 .. frm.ChildCount - 1 ] do
                            yield! helper (frm.GetChild(index), true)
                }

            helper (x, includeMe)

        member x.Ascendants<'T when 'T :> IParseTree>(?includeMe: bool, ?predicate: ParseTreePredicate) =

            let includeMe = includeMe |? false
            let predicate = predicate |? (isType<'T>)

            let rec helper (from: IParseTree, includeMe: bool) =
                [
                    if from <> null then
                        if (includeMe && predicate (from) && isType<'T> from) then
                            yield forceCast<'T> (from)

                        yield! helper (from.Parent, true)
                ]

            helper (x, includeMe)

        member x.TryFindFirstChild(predicate: ParseTreePredicate, ?includeMe: bool) =
            let includeMe = includeMe |? false
            x.Descendants<IParseTree>(includeMe) |> Seq.tryFind (predicate)

        member x.TryFindChildren<'T when 'T :> IParseTree>
          (
            ?includeMe: bool,
            ?predicate: ParseTreePredicate,
            ?exclude: ParseTreePredicate
          ) : 'T seq = // :'T

            let includeMe = includeMe |? false
            let predicate = predicate |? truthyfy
            let predicate x = isType<'T> x && predicate x
            let exclude = exclude |? falsify
            x.Descendants<'T>(includeMe, predicate, exclude)

        member x.TryFindFirstChild<'T when 'T :> IParseTree>
          (
            ?includeMe: bool,
            ?predicate: ParseTreePredicate,
            ?exclude: ParseTreePredicate
          ) : 'T option = // :'T
            x.TryFindChildren(includeMe |? false, predicate |? truthyfy, exclude |? falsify) |> Seq.tryHead


        member x.TryFindFirstAscendant(predicate: ParseTreePredicate, ?includeMe: bool) = //:IParseTree option=
            let includeMe = includeMe |? false
            x.Ascendants(includeMe) |> Seq.tryFind (predicate)


        member x.TryFindFirstAscendant<'T when 'T :> IParseTree>(?includeMe: bool) =
            let includeMe = includeMe |? false
            let pred = isType<'T>
            x.TryFindFirstAscendant(pred, includeMe) |> Option.map forceCast<'T>

        //member x.TryFindIdentifier1FromContext(?exclude: ParseTreePredicate) =
        //    let exclude = exclude |? falsify

        //    option {
        //        let! ctx = x.TryFindFirstChild<Identifier1Context>(false, exclude = exclude)
        //        return ctx.GetText()
        //    }

        //member x.TryFindNameComponentContext() : IParseTree option =
        //    let pred =
        //        fun (tree: IParseTree) ->
        //            tree :? Identifier1Context
        //            || tree :? Identifier2Context
        //            || tree :? Identifier3Context
        //            || tree :? Identifier4Context
        //            || tree :? Identifier5Context
        //            || tree :? IdentifierOpCmdContext

        //    x.TryFindFirstChild(pred, true)

        //member x.TryGetName() : string option =
        //    option {
        //        let! idCtx = x.TryFindNameComponentContext()
        //        let name = idCtx.GetText()
        //        return name.DeQuoteOnDemand()
        //    }

        //member x.TryCollectNameComponents() : string[] option = // :Fqdn
        //    option {
        //        let! idCtx = x.TryFindNameComponentContext()

        //        if  idCtx :? Identifier1Context then
        //            return [| idCtx.GetText() |]
        //        else
        //            let name = idCtx.GetText()
        //            return fwdParseFqdn(name).ToArray()
        //    }

        //member x.CollectNameComponents() : string[] =
        //    match x.TryCollectNameComponents() with
        //    | Some names -> names.Select(deQuoteOnDemand).ToArray()
        //    | None -> failWithLog "Failed to collect name components"

        //member x.TryGetSystemName() =
        //    option {
        //        let! ctx = x.TryFindFirstAscendant<SystemContext>(true)
        //        let! names = ctx.TryCollectNameComponents()
        //        return names.Combine()
        //    }

    type ParserRuleContext with

        member x.GetRange() =
            let s = x.Start.StartIndex
            let e = x.Stop.StopIndex
            s, e

        member x.GetOriginalText() =
            // https://stackoverflow.com/questions/16343288/how-do-i-get-the-original-text-that-an-antlr4-rule-matched
            x.Start.InputStream.GetText(x.GetRange() |> Antlr4.Runtime.Misc.Interval)

[<AutoOpen>]
module FqdnParserModule =
    let createParser<'P, 'L when 'P :> Parser and 'L :> Lexer>
        (text: string, lexerCtor: AntlrInputStream -> 'L, parserCtor: CommonTokenStream -> 'P) : 'P =

        let inputStream = AntlrInputStream(text)
        let lexer = lexerCtor(inputStream)
        let tokenStream = CommonTokenStream(lexer)
        let parser = parserCtor(tokenStream)

        // 에러 리스너 추가
        let listener_lexer = new ErrorListener<int>(true)
        let listener_parser = new ErrorListener<IToken>(true)
        lexer.AddErrorListener(listener_lexer)
        parser.AddErrorListener(listener_parser)

        parser


    let createFqdnParser (text: string) : fqdnParser =
        let l = fun inputStream -> fqdnLexer(inputStream)
        let p = fun tokenStream -> fqdnParser(tokenStream)
        createParser(text, l, p)

    let rTryParseFqdn (text: string) : Result<string[], string> =
        if text.IsNullOrEmpty() then
            Error "Empty name"
        else
            try
                let parser = createFqdnParser (text)
                let ctx = parser.fqdn ()
                let ncs = ctx.Descendants<fqdnParser.NameComponentContext>()
                Ok [| for nc in ncs -> nc.GetText() |]
            with
            | :? ParserError as _err ->
                logError $"Failed to parse FQDN: '{text}'" // Just warning.  하나의 이름에 '.' 을 포함하는 경우.  e.g "#seg.testMe!!!"
                Ok [| text |]   // !!!! Not ERROR !!!
                //Error _err.Message
            | exn ->
                Error $"ERROR: {exn}"


    let parseFqdn (text: string) = rTryParseFqdn(text).GetOkValue()
