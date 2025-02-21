namespace Dual.Ev2

open Dual.Common.Base.FS
open Dual.Common.Core.FS

[<AutoOpen>]
module TNonTerminalModule =

    //type Op with
    //    member x.ToEvaluator() =
    //        match x with
    //        | OpUnit -> (fun _ -> failwith "OpUnit")
    //        | And -> (fun args -> if args.Length = 2 then args.[0] && args.[1] else failwith "And")
    //        | Or -> (fun args -> if args.Length = 2 then args.[0] || args.[1] else failwith "Or")
    //        | Neg -> (fun args -> if args.Length = 1 then not args.[0] else failwith "Neg")
    //        | RisingAfter -> (fun args -> if args.Length = 2 then args.[0] > args.[1] else failwith "RisingAfter")
    //        | FallingAfter -> (fun args -> if args.Length = 2 then args.[0] < args.[1] else failwith "FallingAfter")
    //        //| OpCompare op -> (fun args -> if args.Length = 2 then args.[0] :?> IComparable).CompareTo(args.[1]) else failwith "OpCompare"
    //        //| OpArithmetic op -> (fun args -> if args.Length = 2 then args.[0] :?> IArithmetic).Arithmetic(args.[1]) else failwith "OpArithmetic"
    //        | OpOutOfService evaluator -> evaluator
    //        | _ -> failwith "OpOutOfService"


    ()
