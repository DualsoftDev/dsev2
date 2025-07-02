namespace rec Dual.Ev2.Aas

(* Core 를 AAS Json/Xml 로 변환하기 위한 실제 코드 *)

open Dual.Common.Core.FS
open Dual.Ev2
open System
open Dual.Common.Base
open Ev2.Core.FS


[<AutoOpen>]
module CoreToAas =
    type NjUnique with
        member x.CollectProperties(): JNode[] =
            seq {
                JObj().SetProperty(x.Name, "Name")
                JObj().SetProperty(x.Parameter, "Parameter")
                JObj().SetProperty(x.Guid, "Guid")
                if x.Id.IsSome then
                    JObj().SetProperty(x.Id.Value, "Id")
            } |> choose id |> Seq.cast<JNode> |> toArray

    type NjSystem with
        member private x.collectChildren(): JNode[] =
            let fs = x.Flows |-> _.ToSMC()
            let flows =
                JObj().AddProperties(
                    idShort = "Flows",
                    modelType = A.smc,
                    values = fs
                )

            let ws = x.Works |-> _.ToSMC()
            let works =
                JObj().AddProperties(
                    idShort = "Works",
                    modelType = A.smc,
                    values = ws
                )

            let arrs = x.Arrows |-> _.ToSMC()
            let arrows =
                JObj().AddProperties(
                    idShort = "Arrows",
                    modelType = A.smc,
                    values = arrs
                )

            [|flows; arrows; works|]

        member sys.ToSM(): JNode =
            let sm =
                JObj().AddProperties(
                    category = Category.CONSTANT,
                    modelType = ModelType.Submodel,
                    idShort = "Identification",
                    id = A.ridIdentification,
                    kind = KindType.Instance,
                    semantic = J.CreateSemantic(SemanticIdType.ModelReference, KeyType.Submodel, A.ridIdentification),
                    smel = sys.collectChildren()
                )
            sm


        [<Obsolete("TODO")>] member x.ToENV(): JObj = null
        [<Obsolete("TODO")>] member x.ToAasJsonENV(): string = null

    type NjButton with
        member x.ToSMC(): JNode =
            let props = [|
                yield! x.CollectProperties()
                //yield! seq {
                //} |> choose id |> Seq.cast<JNode>
            |]

            JObj().ToSMC("Button", props)

    type NjLamp with
        member x.ToSMC(): JNode =
            let props = [|
                yield! x.CollectProperties()
                //yield! seq {
                //} |> choose id |> Seq.cast<JNode>
            |]

            JObj().ToSMC("Lamp", props)
    type NjCondition with
        member x.ToSMC(): JNode =
            let props = [|
                yield! x.CollectProperties()
                //yield! seq {
                //} |> choose id |> Seq.cast<JNode>
            |]

            JObj().ToSMC("Condition", props)
    type NjAction with
        member x.ToSMC(): JNode =
            let props = [|
                yield! x.CollectProperties()
                //yield! seq {
                //} |> choose id |> Seq.cast<JNode>
            |]

            JObj().ToSMC("Action", props)

    type NjFlow with    // ToSMC
        /// DsFlow -> JNode
        member x.ToSMC(): JNode =
            let buttons    = x.Buttons    |-> _.ToSMC() |> toSMC "Buttons"
            let lamps      = x.Lamps      |-> _.ToSMC() |> toSMC "Lamps"
            let conditions = x.Conditions |-> _.ToSMC() |> toSMC "Conditions"
            let actions    = x.Actions    |-> _.ToSMC() |> toSMC "Actions"

            let props = [|
                yield! x.CollectProperties()
                //yield! seq {
                //} |> choose id |> Seq.cast<JNode>
            |]

            JObj().ToSMC("Flow", props)
            |> _.AddValues([|buttons; lamps; conditions; actions|] |> choose id)

    type NjCall with
        /// DsAction -> JNode
        member x.ToSMC(): JNode =
            let props = [|
                yield! x.CollectProperties()
                yield! seq {
                    JObj().SetProperty(x.IsDisabled, "IsDisabled")
                    JObj().SetProperty(x.CommonConditions, "CommonConditions")
                    JObj().SetProperty(x.AutoConditions, "AutoConditions")
                    if x.Timeout.IsSome then
                        JObj().SetProperty(x.Timeout.Value, "Timeout")
                } |> choose id |> Seq.cast<JNode>

            |]

            JObj().ToSMC("Call", props)

    type NjArrow with
        /// Convert arrow to submodelElementCollection
        member x.ToSMC(): JNode =
            let props = [|
                yield! x.CollectProperties()
                yield! seq {
                    JObj().SetProperty(x.Source, "Source")
                    JObj().SetProperty(x.Target, "Target")
                    JObj().SetProperty(x.Type, "Type")
                } |> choose id |> Seq.cast<JNode>

            |]

            JObj().ToSMC("Edge", props)

    let toSMC (idShort:string) (values:JNode[]) =
        match values with
        | [||] -> None
        | _ ->
            JObj()
                .Set(N.IdShort, idShort)
                .Set(N.ModelType, ModelType.SubmodelElementCollection.ToString())
                |> _.AddValues(values)
                |> Some

    type NjWork with    // ToSMC
        /// DsWork -> JNode
        member x.ToSMC(): JNode =
            let arrows = x.Arrows |-> _.ToSMC() |> toSMC "Arrows"
            let calls  = x.Calls  |-> _.ToSMC() |> toSMC "Calls"
            let props = [|
                yield! x.CollectProperties()
                yield! seq {
                    JObj().SetProperty(x.FlowGuid, "FlowGuid")
                    JObj().SetProperty(x.Motion, "Motion")
                    JObj().SetProperty(x.Script, "Script")
                    JObj().SetProperty(x.IsFinished, "IsFinished")
                    JObj().SetProperty(x.NumRepeat, "NumRepeat")
                    JObj().SetProperty(x.Period, "Period")
                    JObj().SetProperty(x.Delay, "Delay")
                } |> choose id |> Seq.cast<JNode>
            |]

            JObj().ToSMC("Work", props)
            |> _.AddValues([|arrows; calls|] |> choose id)


