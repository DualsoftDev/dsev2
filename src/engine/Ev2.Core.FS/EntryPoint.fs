namespace Dual.Ev2


open Dual.Common.Base.FS
open Dual.Common.Core.FS


// Json serialize 시의 clean namesapce 를 위해 module 로 선언
[<AutoOpen>]
module T =
    ()
module ModuleInitializer =
    let Initialize() =
        ()
        //fwdEvaluate := (
        //    fun (operator: Op) (args: Args) ->
        //        evaluateT (operator, args) :> obj
        //        //failwithlog "Should be reimplemented."
