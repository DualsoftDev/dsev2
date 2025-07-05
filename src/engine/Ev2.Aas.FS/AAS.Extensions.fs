namespace rec Dual.Ev2.Aas

(* AAS Json/Xml 로부터 Core 를 생성하기 위한 코드 *)

open System.Linq
open System

open AasCore.Aas3_0

open Dual.Common.Core.FS
open Dual.Common.Base.FS
open Ev2.Core.FS
open System.Globalization


[<AutoOpen>]
module AasExtensions =
    /// SemanticId 키 매칭 유틸
    type IHasSemantics with
        member internal semantic.hasSemanticKey (semanticKey: string) =
            semantic.SemanticId <> null &&
            semantic.SemanticId.Keys
            |> Seq.exists (fun k -> k.Value = AasSemantics.map[semanticKey])

    type UniqueInfo = { Name: string; Guid: Guid; Parameter: string; Id: Id option }

    type SubmodelElementCollection with
        member smc.ValuesOfType<'T when 'T :> ISubmodelElement>() = smc.Value.OfType<'T>()

        member smc.TryGetPropValueBySemanticKey (semanticKey:string): string option =
            smc.ValuesOfType<Property>()
            |> tryPick (function
                | p when p.hasSemanticKey semanticKey -> Some p.Value
                | _ -> None)

        member smc.CollectChildrenSMEWithSemanticKey(semanticKey: string): ISubmodelElement [] =
            smc.Value
            |> filter (fun sme -> sme.hasSemanticKey semanticKey)
            |> toArray
        member smc.CollectChildrenSMCWithSemanticKey(semanticKey: string): SubmodelElementCollection [] =
            smc.CollectChildrenSMEWithSemanticKey semanticKey |> Seq.cast<SubmodelElementCollection> |> toArray

        member smc.TryGetPropValue (propName:string) = smc.TryGetPropValueBySemanticKey propName

        member smc.TryGetPropValue<'T> (propName: string): 'T option =
            smc.TryGetPropValue propName
            >>= (fun str ->
                try
                    let value =
                        match typeof<'T> with
                        | _ when typeof<'T> = typeof<string> ->
                            box str
                        | _ when typeof<'T> = typeof<Guid> ->
                            str |> Guid.Parse |> box
                        | _ when typeof<'T> = typeof<int> ->
                            str |> Int32.Parse |> box
                        | _ when typeof<'T> = typeof<float> ->
                            str |> Double.Parse |> box
                        | _ when typeof<'T> = typeof<bool> ->
                            str |> Boolean.Parse |> box
                        | _ ->
                            // 일반적인 Convert.ChangeType 사용
                            Convert.ChangeType(str, typeof<'T>, CultureInfo.InvariantCulture)
                    Some (value :?> 'T)
                with _ -> None)

        member smc.GetPropValue propName =
            smc.TryGetPropValue propName |> Option.get

        member smc.TryFindChildSME(semanticKey: string): ISubmodelElement option =
            smc.CollectChildrenSMEWithSemanticKey semanticKey |> tryHead

        member smc.TryFindChildSMC(semanticKey: string): SubmodelElementCollection option =
            (smc.TryFindChildSME semanticKey).Cast<SubmodelElementCollection>()


        member smc.TryGetPropValueByCategory (category:string): string option =
            smc.Value
            |> Seq.tryPick (function
                | :? Property as p when p.Category = category -> Some p.Value
                | _ -> None)

        member smc.ReadUniqueInfo() =
            let name      = smc.TryGetPropValue "Name"      |? null
            let guid      = smc.GetPropValue    "Guid"      |> Guid.Parse
            let parameter = smc.TryGetPropValue "Parameter" |? null
            let id        = smc.TryGetPropValue "Id"        |-> Id.Parse
            { Name=name; Guid=guid; Parameter=parameter; Id=id }


