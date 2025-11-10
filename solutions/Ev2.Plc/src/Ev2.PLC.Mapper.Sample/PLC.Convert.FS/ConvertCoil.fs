namespace PLC.Convert.FS

open System
open System.Collections.Generic
open PLC.Convert
open PLC.Convert.LSCore.Expression

module ConvertCoilModule =

    type ContentType =
            | Coil of string
            | ContactNega of string
            | ContactPosi of string
            | Other of string
            with override x.ToString() =
                    match x with
                    | Coil line -> line
                    | ContactNega line ->  line
                    | ContactPosi line ->  line
                    | Other line -> line

    type Network = {
            Title: string
            Content: ContentType array
        }

    let getCoils (networks: Network array) : seq<Terminal> =
        
        let isCoil = function
            | Coil _ -> true
            | _ -> false

        let isContactPosi = function
            | ContactPosi _ -> true
            | _ -> false

        let isContactNega = function
            | ContactNega _ -> true
            | _ -> false

        let filterNetwork (n: Network) =
            let hasCoil = n.Content |> Seq.exists isCoil
            let hasContact = n.Content |> Seq.exists (fun x -> isContactPosi x || isContactNega x)
            hasCoil && hasContact

        let dictRung = Dictionary<string, Terminal>()

        networks
        |> Array.filter filterNetwork
        |> Array.iter (fun n ->
            match n.Content |> Seq.tryFind isCoil with
            | Some (Coil coil) when not (dictRung.ContainsKey(coil)) ->
                let coilExpr = Terminal(LSCore.Symbol(coil))
                dictRung.Add(coil, coilExpr)
            | _ -> ()
        )

        let rungs =
            networks
            |> Array.filter filterNetwork
            |> Array.map (fun n ->
                match n.Content |> Seq.tryFind isCoil with
                | Some (Coil coil) ->
                    let contactAs = n.Content |> Seq.choose (function ContactPosi x -> Some x | _ -> None)
                    let contactBs = n.Content |> Seq.choose (function ContactNega x -> Some x | _ -> None)
                    Rung(coil, contactAs, contactBs, dictRung)
                | _ -> failwith "Invalid Network Structure"
            )

        rungs
        |> Array.collect (fun rung -> rung.RungExprs |> Seq.cast<Terminal> |> Seq.toArray )
        |> Seq.filter (fun terminal -> terminal.HasInnerExpr)
