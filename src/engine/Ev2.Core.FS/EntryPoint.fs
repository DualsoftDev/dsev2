namespace Dual.Ev2


open Dual.Common.Base.FS
open Dual.Common.Core.FS

module ModuleInitializer =
    //let private evalueateValueObjects (includeMe:bool) (evaluator:obj -> bool) (value:IValue) =
    //    value.EnumerateValueObjects(includeMe, evaluator)
    let private isLiteralizable (value:IValue) =
        let isLiteral (v:obj) =
            match v with
            | :? ValueHolder as vh -> vh.IsLiteral
            | _ -> true
        value.EnumerateValueObjects(true) |> Seq.forall isLiteral

    let Initialize() =
        //CpusEvent.initialize()
        //fwdEnumerateValueObjects <- evalueateValueObjects
        fwdIsLiteralizable <- isLiteralizable
        ()
