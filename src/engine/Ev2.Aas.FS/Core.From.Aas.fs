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

        [<Obsolete("TODO: ISubmodel 에서 NjSystem 구축 코드 작성")>]
        static member FromISubmodel(submodel:ISubmodel): NjSystem =
            assert(submodel.IdShort.IsOneOf("Identification", "System"))
            //submodel.SubmodelElements[0].
            //let name = submodel.IdShort
            //getNull<NjSystem>()

            let getSMC idShort =
                submodel.SubmodelElements
                |> Seq.tryFind (fun sm -> sm.IdShort = idShort)
                >>= (fun sm ->
                    match sm with
                    | :? SubmodelElementCollection as smc -> Some <| smc.Value.OfType<SubmodelElementCollection>().ToArray()
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
            let props =
                smc.Value |> Seq.choose (fun sm ->
                    match sm with
                    | :? AasCore.Aas3_0.Property as p -> Some(p.IdShort, p.Value)
                    | _ -> None
                ) |> Map.ofSeq

            NjArrow(
                Source   = props["Source"]
                , Target = props["Target"]
                , Type   = props["EdgeType"] )

    type NjFlow with
        static member FromSMC(smc: SubmodelElementCollection): NjFlow =
            let name =
                smc.Value
                |> Seq.tryPick (function
                    | :? AasCore.Aas3_0.Property as p when p.IdShort = "Name" -> Some p.Value
                    | _ -> None)
                |? "Unnamed"

            let guid =
                smc.Value
                |> Seq.tryPick (function
                    | :? AasCore.Aas3_0.Property as p when p.IdShort = "Guid" -> Some(Guid.Parse(p.Value))
                    | _ -> None)
                |? Guid.NewGuid()

            NjFlow( Name = name, Guid = guid)


    type NjWork with
        static member FromSMC(smc: SubmodelElementCollection): NjWork =
            let name =
                smc.Value
                |> Seq.tryPick (function
                    | :? AasCore.Aas3_0.Property as p when p.IdShort = "Name" -> Some p.Value
                    | _ -> None)
                |? "Unnamed"

            let guid =
                smc.Value
                |> Seq.tryPick (function
                    | :? AasCore.Aas3_0.Property as p when p.IdShort = "Guid" -> Some(Guid.Parse(p.Value))
                    | _ -> None)
                |? Guid.NewGuid()

            let calls =
                smc.Value
                |> Seq.tryPick (function
                    | :? SubmodelElementCollection as col when col.IdShort = "Calls" ->
                        Some (
                            col.Value
                            |> Seq.choose (fun elem ->
                                match elem with
                                | :? SubmodelElementCollection as call -> Some(NjCall.FromSMC(call))
                                | _ -> None
                            )
                            |> toArray
                        )
                    | _ -> None
                ) |? [||]

            let arrows =
                smc.Value
                |> Seq.tryPick (function
                    | :? SubmodelElementCollection as col when col.IdShort = "Arrows" ->
                        Some (
                            col.Value
                            |> Seq.choose (fun elem ->
                                match elem with
                                | :? SubmodelElementCollection as arrow -> Some(NjArrow.FromSMC(arrow))
                                | _ -> None
                            )
                            |> toArray
                        )
                    | _ -> None
                ) |? [||]

            NjWork( Name = name, Guid = guid, Calls = calls, Arrows = arrows)

    type NjCall with
        static member FromSMC(smc: SubmodelElementCollection): NjCall =
            let isDisabled =
                smc.Value
                |> Seq.tryPick (function
                    | :? AasCore.Aas3_0.Property as p when p.IdShort = "IsDisable" ->
                        match Boolean.TryParse(p.Value) with
                        | true, v -> Some v
                        | _ -> None
                    | _ -> None)
                |? false

            NjCall(IsDisabled = isDisabled) //, Name = smc.IdShort, Guid = Guid.NewGuid())
