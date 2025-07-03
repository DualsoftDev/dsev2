namespace rec Dual.Ev2.Aas

(* AAS Json/Xml 로부터 Core 를 생성하기 위한 코드 *)

open Dual.Common.Core.FS
open Dual.Ev2
open System.Linq
open System
open Dual.Common.Base
open Ev2.Core.FS
open AasCore.Aas3_0


[<AutoOpen>]
module PropModule =
    /// SemanticId 키 매칭 유틸
    let private hasSemanticKey (semanticKey: string) (semantic: IHasSemantics) =
        let xxx =
            if semantic.SemanticId <> null then
                semantic.SemanticId.Keys
            else
                null

        semantic.SemanticId <> null &&
        semantic.SemanticId.Keys
        |> Seq.exists (fun k -> k.Value = AasSemantics.map[semanticKey])

    type SubmodelElementCollection with
        member smc.TryGetPropValueByCategory (category:string): string option =
            smc.Value
            |> Seq.tryPick (function
                | :? Property as p when p.Category = category -> Some p.Value
                | _ -> None)

        member smc.TryGetPropValueBySemanticKey (semanticKey:string): string option =
            let semanticId = AasSemantics.map[semanticKey]
            smc.Value
            |> Seq.tryPick (function
                | :? Property as p when hasSemanticKey semanticKey p -> Some p.Value
                | _ -> None)

        member smc.GetPropValueBySemanticKey semanticKey =
            smc.TryGetPropValueBySemanticKey semanticKey |> Option.get

        member smc.EnumerateChildrenSMC(semanticKey: string): SubmodelElementCollection [] =
            let semanticId = AasSemantics.map[semanticKey]
            smc.Value
            >>= (function
                | :? SubmodelElementCollection as child when hasSemanticKey semanticKey child -> [child]
                | _ -> [])
            |> toArray

        member smc.TryFindChildSMC(semanticKey: string): SubmodelElementCollection option =
            smc.EnumerateChildrenSMC semanticKey |> tryHead


[<AutoOpen>]
module CoreFromAas =
    type Environment = AasCore.Aas3_0.Environment
    type ISubmodel = AasCore.Aas3_0.ISubmodel

    type NjSystem with
        static member FromAasJsonENV(json:string): NjSystem =
            let env = J.CreateIClassFromJson<Environment>(json)
            let sm = env.Submodels.First()
            NjSystem.FromISubmodel(sm)

        static member FromAasXmlENV(xml:string): NjSystem =
            let sm = J.CreateIClassFromXml<Environment>(xml).Submodels.First()
            NjSystem.FromISubmodel(sm)

        static member FromISubmodel(submodel:ISubmodel): NjSystem =
            assert(submodel.IdShort.IsOneOf("Identification", "System"))

            let getSMC semanticId =
                submodel.SubmodelElements
                |> Seq.tryFind (fun sm ->
                    match sm.SemanticId with
                    | null -> false
                    | sid ->
                        match sid.Keys |> Seq.tryHead with
                        | Some key -> key.Value = AasSemantics.map[semanticId]
                        | None -> false)
                >>= (fun sm ->
                    match sm with
                    | :? SubmodelElementCollection as smc -> Some (smc.Value.OfType<SubmodelElementCollection>().ToArray())
                    | _ -> None)
                |? [||]


            let works  = getSMC "Works"  |-> NjWork.FromSMC
            let flows  = getSMC "Flows"  |-> NjFlow.FromSMC
            let arrows = getSMC "Arrows" |-> NjArrow.FromSMC

            NjSystem(
                Guid = Guid.NewGuid()
                , Name = submodel.IdShort
                , Flows = flows
                , Works = works
                , Arrows = arrows
            )



    type NjArrow with
        static member FromSMC(smc: SubmodelElementCollection): NjArrow =
            let src = smc.GetPropValueBySemanticKey "Source"
            let tgt = smc.GetPropValueBySemanticKey "Target"
            let typ = smc.GetPropValueBySemanticKey "Type"
            NjArrow( Source=src, Target=tgt, Type=typ)

    type NjFlow with
        static member FromSMC(smc: SubmodelElementCollection): NjFlow =
            let name = smc.GetPropValueBySemanticKey "Name"
            let guid = smc.GetPropValueBySemanticKey "Guid" |> Guid.Parse

            NjFlow( Name = name, Guid = guid)


    type NjWork with
        static member FromSMC(smc: SubmodelElementCollection): NjWork =
            let name   = smc.GetPropValueBySemanticKey "Name"
            let guid   = smc.GetPropValueBySemanticKey "Guid" |> Guid.Parse

            (* AAS 구조상 Work/Calls/Call[], Work/Arrows/Arrow[] 형태로 존재 *)
            let calls  = smc.TryFindChildSMC "Calls"  |-> (fun smc2 -> smc2.EnumerateChildrenSMC "Call")  |? [||] |-> NjCall.FromSMC
            let arrows = smc.TryFindChildSMC "Arrows" |-> (fun smc2 -> smc2.EnumerateChildrenSMC "Arrow") |? [||] |-> NjArrow.FromSMC

            NjWork( Name = name, Guid = guid, Calls = calls, Arrows = arrows)

    type NjCall with
        static member FromSMC(smc: SubmodelElementCollection): NjCall =
            let isDisabled =
                smc.TryGetPropValueBySemanticKey "IsDisabled"
                >>= Parse.TryBool
                |? false

            NjCall(IsDisabled = isDisabled) //, Name = smc.IdShort, Guid = Guid.NewGuid())
