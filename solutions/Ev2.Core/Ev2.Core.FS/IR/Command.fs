namespace Ev2.Core.FS.IR
open System
open System.Linq
open Dual.Common.Base
open Ev2.Core.FS.IR

[<AutoOpen>]
module CommandModule =
    type Command(name:string, arguments:Arguments, ?executor: (Arguments -> unit)) =
        interface ICommand
        member x.Name = name
        member x.Arguments = arguments
        member val Executor = executor |? fun _ -> failwithMessage "Should be re-implemented" with get, set
        member x.Do() = x.Executor x.Arguments
