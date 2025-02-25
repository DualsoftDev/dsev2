namespace Dual.Ev2

open System
open System.Runtime.Serialization
open Newtonsoft.Json
open Newtonsoft.Json.Linq

open Dual.Common.Base.FS
open Dual.Common.Core.FS
open Dual.Common.Base.CS


//[<AutoOpen>]
//module T =

module ModuleInitializer =
    let Initialize() =
        ()
        //fwdEvaluate := (
        //    fun (operator: Op) (args: Args) ->
        //        evaluateT (operator, args) :> obj
        //        //failwithlog "Should be reimplemented."
