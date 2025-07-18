namespace rec Dual.Ev2.Aas

(* AAS Json/Xml 로부터 Core 를 생성하기 위한 코드 *)

open System.Linq
open System

open AasCore.Aas3_0

open Dual.Common.Core.FS
open Dual.Common.Base.FS
open Ev2.Core.FS
open System.Globalization
open System.Runtime.CompilerServices

open System.Text.Json
open System.Text.Json.Nodes

[<AutoOpen>]
module AasExtensions =
    /// SemanticId 키 매칭 유틸
    type IHasSemantics with
        member internal semantic.hasSemanticKey (semanticKey: string) =
            semantic.SemanticId <> null &&
            semantic.SemanticId.Keys
            |> Seq.exists (fun k -> k.Value = AasSemantics.map[semanticKey])

    type UniqueInfo = { Name: string; Guid: Guid; Parameter: string; Id: Id option }

    type SMEsExtension =
        [<Extension>]
        static member TryGetPropValueBySemanticKey(smc:ISubmodelElement seq, semanticKey:string): string option =
            smc.OfType<Property>()
            |> tryPick (function
                | p when p.hasSemanticKey semanticKey -> Some p.Value
                | _ -> None)

        [<Extension>]
        static member TryGetPropValueByCategory (smc:ISubmodelElement seq, category:string): string option =
            smc.OfType<Property>()
            |> tryPick (function
                | p when p.Category = category -> Some p.Value
                | _ -> None)

        [<Extension>]
        static member CollectChildrenSMEWithSemanticKey(smc:ISubmodelElement seq, semanticKey: string): ISubmodelElement [] =
            smc
            |> filter (fun sme -> sme.hasSemanticKey semanticKey)
            |> toArray

        [<Extension>]
        static member CollectChildrenSMCWithSemanticKey(smc:ISubmodelElement seq, semanticKey: string): SubmodelElementCollection [] =
            smc.CollectChildrenSMEWithSemanticKey semanticKey |> Seq.cast<SubmodelElementCollection> |> toArray

        [<Extension>]
        static member TryGetPropValue (smc:ISubmodelElement seq, propName:string) = smc.TryGetPropValueBySemanticKey propName

        [<Extension>]
        static member TryGetPropValue<'T> (smc:ISubmodelElement seq, propName: string): 'T option =
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

        [<Extension>]
        static member GetPropValue(smc:ISubmodelElement seq, propName) =
            smc.TryGetPropValue propName |> Option.get

        [<Extension>]
        static member TryFindChildSME(smc:ISubmodelElement seq, semanticKey: string): ISubmodelElement option =
            smc.CollectChildrenSMEWithSemanticKey semanticKey |> tryHead

        [<Extension>]
        static member TryFindChildSMC(smc:ISubmodelElement seq, semanticKey: string): SubmodelElementCollection option =
            (smc.TryFindChildSME semanticKey).Cast<SubmodelElementCollection>()

        [<Extension>]
        static member ReadUniqueInfo(smc:ISubmodelElement seq) =
            let name      = smc.TryGetPropValue "Name"      |? null
            let guid      = smc.GetPropValue    "Guid"      |> Guid.Parse
            let parameter = smc.TryGetPropValue "Parameter" |? null
            let id        = smc.TryGetPropValue "Id"        |-> Id.Parse
            { Name=name; Guid=guid; Parameter=parameter; Id=id }


    let private nonnullize(values:ResizeArray<ISubmodelElement>) = if values = null then ResizeArray<ISubmodelElement>() else values
    type SubmodelElementCollection with
        member smc.ReadUniqueInfo() = nonnullize(smc.Value).ReadUniqueInfo()
        member smc.ValuesOfType<'T when 'T :> ISubmodelElement>() = nonnullize(smc.Value).OfType<'T>()
        member smc.TryGetPropValueBySemanticKey (semanticKey:string): string option = nonnullize(smc.Value).TryGetPropValueBySemanticKey semanticKey
        member smc.TryGetPropValueByCategory (category:string): string option = nonnullize(smc.Value).TryGetPropValueByCategory category
        member smc.CollectChildrenSMEWithSemanticKey(semanticKey: string): ISubmodelElement [] = nonnullize(smc.Value).CollectChildrenSMEWithSemanticKey semanticKey
        member smc.CollectChildrenSMCWithSemanticKey(semanticKey: string): SubmodelElementCollection [] = nonnullize(smc.Value).CollectChildrenSMEWithSemanticKey semanticKey |> Seq.cast<SubmodelElementCollection> |> toArray
        member smc.TryGetPropValue (propName:string) = smc.TryGetPropValueBySemanticKey propName
        member smc.TryGetPropValue<'T> (propName: string): 'T option = nonnullize(smc.Value).TryGetPropValue<'T> propName
        member smc.GetPropValue (propName:string):string = nonnullize(smc.Value).GetPropValue propName
        member smc.TryFindChildSME(semanticKey: string): ISubmodelElement option = nonnullize(smc.Value).TryFindChildSME semanticKey
        member smc.TryFindChildSMC(semanticKey: string): SubmodelElementCollection option = nonnullize(smc.Value).TryFindChildSMC semanticKey

        member smc.GetSMC(semanticKey: string): SubmodelElementCollection [] =
                smc.CollectChildrenSMCWithSemanticKey semanticKey

    type ISubmodel with
        member sm.GetSMCWithSemanticKey(semanticKey:string): SubmodelElementCollection [] =
            sm.SubmodelElements
                .CollectChildrenSMCWithSemanticKey semanticKey

