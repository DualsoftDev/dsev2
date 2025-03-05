namespace rec Dual.Ev2


open Dual.Common.Base.FS
open Dual.Common.Core.FS
open Dual.Common.Antlr.FS
open Ev2.Parser

[<AutoOpen>]

module FqdnParserModule =
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