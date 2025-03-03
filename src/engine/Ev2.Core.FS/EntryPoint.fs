namespace Dual.Ev2

module ModuleInitializer =
    let private isLiteralizable (value:IValue) =
        let isLiteral (v:obj) =
            match v with
            | :? ValueHolder as vh -> vh.IsLiteral
            | _ -> true
        value.EnumerateValueObjects(true) |> Seq.forall isLiteral

    let mutable private initailized = false
    let Initialize() =
        if not initailized then
            initailized <- true
            //CpusEvent.initialize()
            //fwdEnumerateValueObjects <- evalueateValueObjects
            fwdIsLiteralizable <- isLiteralizable
            ()
