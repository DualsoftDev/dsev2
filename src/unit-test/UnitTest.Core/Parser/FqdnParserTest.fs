namespace T


open System.Linq
open NUnit.Framework
open Newtonsoft.Json

open Dual.Common.UnitTest.FS
open Dual.Common.Base.CS
open Dual.Common.Base.FS
open Dual.Common.Core.FS

open Dual.Ev2
open Ev2.Parser.FS
module FqdnParserTest =
    /// Json Test
    type T() =
        [<Test>]
        member _.``Minimal`` () =
            match rTryParseFqdn "hello.world.nice.to.meet.you" with
            | Ok v -> v === [|"hello"; "world"; "nice"; "to"; "meet"; "you"|]
            | Error e -> failwith e
