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
                JObj().TrySetProperty(x.Name, "Name")
                JObj().TrySetProperty(x.Parameter, "Parameter")
                JObj().TrySetProperty(x.Guid, "Guid")
                if x.Id.IsSome then
                    JObj().TrySetProperty(x.Id.Value, "Id")
            } |> choose id |> Seq.cast<JNode> |> toArray

    type NjSystem with
        member private x.collectChildren(): JNode[] =
            let details =
                let props = [|
                    yield! x.CollectProperties()
                    yield! seq {
                        JObj().TrySetProperty(x.IRI,                      "IRI")
                        JObj().TrySetProperty(x.Author,                   "Author")
                        JObj().TrySetProperty(x.EngineVersion.ToString(), "EngineVersion")
                        JObj().TrySetProperty(x.LangVersion.ToString(),   "LangVersion")
                        JObj().TrySetProperty(x.Description,              "Description")
                        JObj().TrySetProperty(x.DateTime,                 "DateTime")
                    } |> choose id |> Seq.cast<JNode>
                |]
                JObj().ToSMC("Detail", props)


            let fs = x.Flows |-> _.ToSMC()
            let flows =
                JObj().AddProperties(
                    modelType = A.smc
                    , values = fs
                    , semanticKey = "Flows"
                )

            let ws = x.Works |-> _.ToSMC()
            let works =
                JObj().AddProperties(
                    modelType = A.smc
                    , values = ws
                    , semanticKey = "Works"
                )

            let arrs = x.Arrows |-> _.ToSMC()
            let arrows =
                JObj().AddProperties(
                    modelType = A.smc
                    , values = arrs
                    , semanticKey = "Arrows"
                )

            [| details; flows; arrows; works |]

        member sys.ToSM(): JNode =
            let sm =
                JObj().AddProperties(
                    category = Category.CONSTANT,
                    modelType = ModelType.Submodel,
                    idShort = "Identification",
                    id = A.ridIdentification,
                    kind = KindType.Instance,
                    semanticKey = A.ridIdentification,
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
            let me = x
            let props = [|
                yield! x.CollectProperties()
                yield! seq {
                    JObj().TrySetProperty(x.IsDisabled, "IsDisabled")
                    JObj().TrySetProperty(x.CommonConditions, "CommonConditions")
                    JObj().TrySetProperty(x.AutoConditions, "AutoConditions")
                    if x.Timeout.IsSome then
                        JObj().TrySetProperty(x.Timeout.Value, "Timeout")
                } |> choose id |> Seq.cast<JNode>

            |]

            JObj().ToSMC("Call", props)

    type NjArrow with
        /// Convert arrow to submodelElementCollection
        member x.ToSMC(): JNode =
            let props = [|
                yield! x.CollectProperties()
                yield! seq {
                    JObj().TrySetProperty(x.Source, "Source")
                    JObj().TrySetProperty(x.Target, "Target")
                    JObj().TrySetProperty(x.Type, "Type")
                } |> choose id |> Seq.cast<JNode>

            |]

            JObj().ToSMC("Arrow", props)

    let toSMC (semanticKey:string) (values:JNode[]) =
        match values with
        | [||] -> None
        | _ ->
            JObj()
                .SetSemantic(semanticKey)
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
                    JObj().TrySetProperty(x.FlowGuid,   "FlowGuid")
                    JObj().TrySetProperty(x.Motion,     "Motion")
                    JObj().TrySetProperty(x.Script,     "Script")
                    JObj().TrySetProperty(x.IsFinished, "IsFinished")
                    JObj().TrySetProperty(x.NumRepeat,  "NumRepeat")
                    JObj().TrySetProperty(x.Period,     "Period")
                    JObj().TrySetProperty(x.Delay,      "Delay")
                } |> choose id |> Seq.cast<JNode>
            |]

            JObj().ToSMC("Work", props)
            |> _.AddValues([|arrows; calls|] |> choose id)


