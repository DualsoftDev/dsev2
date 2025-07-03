namespace rec Dual.Ev2.Aas

(* AAS Json/Xml 로부터 Core 를 생성하기 위한 코드 *)

open Dual.Common.Core.FS
open Dual.Ev2
open System.Linq
open System
open Dual.Common.Base
open Ev2.Core.FS
open AasCore.Aas3_0
open System.Reflection.Metadata
open System.Security.Cryptography


[<AutoOpen>]
module PropModule =
    /// SemanticId 키 매칭 유틸
    let internal hasSemanticKey (semanticKey: string) (semantic: IHasSemantics) =
        semantic.SemanticId <> null &&
        semantic.SemanticId.Keys
        |> Seq.exists (fun k -> k.Value = AasSemantics.map[semanticKey])

    type UniqueInfo = { Name: string; Guid: Guid; Parameter: string; Id: Id option }

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

        member smc.ReadUniqueInfo() =
            let name = smc.TryGetPropValueBySemanticKey "Name" |? null
            let guid = smc.GetPropValueBySemanticKey "Guid" |> Guid.Parse
            let parameter = smc.TryGetPropValueBySemanticKey "Parameter" |? null
            let id = smc.TryGetPropValueBySemanticKey "Id" |-> Id.Parse
            { Name=name; Guid=guid; Parameter=parameter; Id=id }



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

            let getSMC semanticKey =
                submodel.SubmodelElements
                |> Seq.tryFind (fun sm -> PropModule.hasSemanticKey semanticKey sm)
                >>= (fun sm ->
                    match sm with
                    | :? SubmodelElementCollection as smc -> Some (smc.Value.OfType<SubmodelElementCollection>().ToArray())
                    | _ -> None)
                |? [||]

            //let xxx = submodel.SubmodelElements.OfType<SubmodelElementCollection>().ToArray()

            let details =
                submodel.SubmodelElements
                    .OfType<SubmodelElementCollection>()
                    .FirstOrDefault(fun sm -> PropModule.hasSemanticKey "Detail" sm)
            let { Name=name; Guid=guid; Parameter=parameter; Id=id } = details.ReadUniqueInfo()
            let dateTime      = details.GetPropValueBySemanticKey "DateTime"  |> DateTime.Parse
            let iri           = details.GetPropValueBySemanticKey "IRI"
            let engineVersion = details.TryGetPropValueBySemanticKey "EngineVersion" |-> Version.Parse |? Version(0, 0)
            let langVersion   = details.TryGetPropValueBySemanticKey "LangVersion"   |-> Version.Parse |? Version(0, 0)
            let author        = details.TryGetPropValueBySemanticKey "Author" |? null
            let description   = details.TryGetPropValueBySemanticKey "Description" |? null


            let works  = getSMC "Works"  |-> NjWork.FromSMC
            let flows  = getSMC "Flows"  |-> NjFlow.FromSMC
            let arrows = getSMC "Arrows" |-> NjArrow.FromSMC

            NjSystem(
                Name=name, Guid=guid, Id=id, Parameter=parameter

                , DateTime = dateTime
                , IRI = iri
                , Author = author
                , EngineVersion = engineVersion
                , LangVersion = langVersion
                , Description = description

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


    type NjButton with
        static member FromSMC(smc: SubmodelElementCollection): NjButton =
            let { Name=name; Guid=guid; Parameter=parameter; Id=id } = smc.ReadUniqueInfo()
            NjButton(Name=name, Guid=guid, Id=id, Parameter=parameter)

    type NjLamp with
        static member FromSMC(smc: SubmodelElementCollection): NjLamp =
            let { Name=name; Guid=guid; Parameter=parameter; Id=id } = smc.ReadUniqueInfo()
            NjLamp(Name=name, Guid=guid, Id=id, Parameter=parameter)

    type NjCondition with
        static member FromSMC(smc: SubmodelElementCollection): NjCondition =
            let { Name=name; Guid=guid; Parameter=parameter; Id=id } = smc.ReadUniqueInfo()
            NjCondition(Name=name, Guid=guid, Id=id, Parameter=parameter)

    type NjAction with
        static member FromSMC(smc: SubmodelElementCollection): NjAction =
            let { Name=name; Guid=guid; Parameter=parameter; Id=id } = smc.ReadUniqueInfo()
            NjAction(Name=name, Guid=guid, Id=id, Parameter=parameter)

    type NjFlow with
        static member FromSMC(smc: SubmodelElementCollection): NjFlow =
            let { Name=name; Guid=guid; Parameter=parameter; Id=id } = smc.ReadUniqueInfo()

            let buttons     = smc.TryFindChildSMC "Buttons"     |-> (fun smc2 -> smc2.EnumerateChildrenSMC "Button")     |? [||] |-> NjButton.FromSMC
            let lamps       = smc.TryFindChildSMC "Lamps"       |-> (fun smc2 -> smc2.EnumerateChildrenSMC "Lamp")       |? [||] |-> NjLamp.FromSMC
            let conditions  = smc.TryFindChildSMC "Conditions"  |-> (fun smc2 -> smc2.EnumerateChildrenSMC "Condition")  |? [||] |-> NjCondition.FromSMC
            let actions     = smc.TryFindChildSMC "Actions"     |-> (fun smc2 -> smc2.EnumerateChildrenSMC "Action")     |? [||] |-> NjAction.FromSMC

            NjFlow( Name=name, Guid=guid, Id=id, Parameter=parameter, Buttons = buttons, Lamps = lamps, Conditions = conditions, Actions = actions)


    type NjWork with
        static member FromSMC(smc: SubmodelElementCollection): NjWork =
            let { Name=name; Guid=guid; Parameter=parameter; Id=id } = smc.ReadUniqueInfo()

            (* AAS 구조상 Work/Calls/Call[], Work/Arrows/Arrow[] 형태로 존재 *)
            let calls  = smc.TryFindChildSMC "Calls"  |-> (fun smc2 -> smc2.EnumerateChildrenSMC "Call")  |? [||] |-> NjCall.FromSMC
            let arrows = smc.TryFindChildSMC "Arrows" |-> (fun smc2 -> smc2.EnumerateChildrenSMC "Arrow") |? [||] |-> NjArrow.FromSMC

            NjWork(Name=name, Guid=guid, Id=id, Parameter=parameter, Calls = calls, Arrows = arrows)

    type NjCall with
        static member FromSMC(smc: SubmodelElementCollection): NjCall =
            let { Name=name; Guid=guid; Parameter=parameter; Id=id } = smc.ReadUniqueInfo()
            let isDisabled =
                smc.TryGetPropValueBySemanticKey "IsDisabled"
                >>= Parse.TryBool
                |? false

            NjCall(Name=name, Guid=guid, Id=id, Parameter=parameter, IsDisabled = isDisabled)
