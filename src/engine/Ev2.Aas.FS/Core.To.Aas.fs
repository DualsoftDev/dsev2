namespace rec Dual.Ev2.Aas

(* Core 를 AAS Json/Xml 로 변환하기 위한 실제 코드 *)

open System

open Dual.Common.Core.FS
open Ev2.Core.FS


[<AutoOpen>]
module CoreToAas =
    type NjUnique with
        member x.CollectProperties(): JNode[] =
            seq {
                JObj().TrySetProperty(x.Name,      "Name")
                JObj().TrySetProperty(x.Parameter, "Parameter")
                JObj().TrySetProperty(x.Guid,      "Guid")
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
                JObj().ToAjSMC("Detail", props)


            let fs = x.Flows |-> _.ToAjSMC()
            let flows =
                JObj().AddProperties(
                    modelType = A.smc
                    , values = fs
                    , semanticKey = "Flows"
                )

            let ws = x.Works |-> _.ToAjSMC()
            let works =
                JObj().AddProperties(
                    modelType = A.smc
                    , values = ws
                    , semanticKey = "Works"
                )

            let arrs = x.Arrows |-> _.ToAjSMC()
            let arrows =
                JObj().AddProperties(
                    modelType = A.smc
                    , values = arrs
                    , semanticKey = "Arrows"
                )


            let ads = x.ApiDefs |-> _.ToAjSMC()
            let apiDefs =
                JObj().AddProperties(
                    modelType = A.smc
                    , values = ads
                    , semanticKey = "ApiDefs"
                )

            let acs = x.ApiCalls |-> _.ToAjSMC()
            let apiCalls =
                JObj().AddProperties(
                    modelType = A.smc
                    , values = acs
                    , semanticKey = "ApiCalls"
                )


            [| details; apiDefs; apiCalls; flows; arrows; works |]

        member sys.ToAjSM(): JNode =
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

    type NjApiDef with
        member x.ToAjSMC(): JNode =
            let props = [|
                yield! x.CollectProperties()
                yield! seq {
                    JObj().TrySetProperty(x.IsPush, "IsPush")
                } |> choose id |> Seq.cast<JNode>
            |]

            JObj().ToAjSMC("ApiDef", props)

    type NjApiCall with
        member x.ToAjSMC(): JNode =
            let props = [|
                yield! x.CollectProperties()
                yield! seq {
                    JObj().TrySetProperty(x.ApiDef,     "ApiDef")       // Guid
                    JObj().TrySetProperty(x.InAddress,  "InAddress")
                    JObj().TrySetProperty(x.OutAddress, "OutAddress")
                    JObj().TrySetProperty(x.InSymbol,   "InSymbol")
                    JObj().TrySetProperty(x.OutSymbol,  "OutSymbol")
                    JObj().TrySetProperty(x.ValueSpec,  "ValueSpec")
                } |> choose id |> Seq.cast<JNode>
            |]

            JObj().ToAjSMC("ApiCall", props)


    type NjButton with
        member x.ToAjSMC(): JNode =
            let props = x.CollectProperties()
            JObj().ToAjSMC("Button", props)

    type NjLamp with
        member x.ToAjSMC(): JNode =
            let props = x.CollectProperties()
            JObj().ToAjSMC("Lamp", props)

    type NjCondition with
        member x.ToAjSMC(): JNode =
            let props = x.CollectProperties()
            JObj().ToAjSMC("Condition", props)

    type NjAction with
        member x.ToAjSMC(): JNode =
            let props = x.CollectProperties()
            JObj().ToAjSMC("Action", props)

    type NjFlow with    // ToAjSMC
        /// DsFlow -> JNode
        member x.ToAjSMC(): JNode =
            let buttons    = x.Buttons    |-> _.ToAjSMC() |> toAjSMC "Buttons"
            let lamps      = x.Lamps      |-> _.ToAjSMC() |> toAjSMC "Lamps"
            let conditions = x.Conditions |-> _.ToAjSMC() |> toAjSMC "Conditions"
            let actions    = x.Actions    |-> _.ToAjSMC() |> toAjSMC "Actions"

            let props = x.CollectProperties()
            JObj().ToAjSMC("Flow", props)
            |> _.AddValues([|buttons; lamps; conditions; actions|] |> choose id)

    type NjCall with
        /// DsAction -> JNode
        member x.ToAjSMC(): JNode =
            let me = x
            let props = [|
                yield! x.CollectProperties()
                yield! seq {
                    JObj().TrySetProperty(x.IsDisabled,       "IsDisabled")
                    JObj().TrySetProperty(x.CommonConditions, "CommonConditions")
                    JObj().TrySetProperty(x.AutoConditions,   "AutoConditions")
                    if x.Timeout.IsSome then
                        JObj().TrySetProperty(x.Timeout.Value,     "Timeout")
                    JObj().TrySetProperty(x.CallType.ToString(),   "CallType")

                    JObj().TrySetProperty(sprintf "%A" x.ApiCalls, "ApiCalls")      // Guid[] type
                    //JObj().TrySetProperty(x.ApiCalls |-> string |> box, "ApiCalls")
                } |> choose id |> Seq.cast<JNode>
            |]

            JObj().ToAjSMC("Call", props)

    type NjArrow with
        /// Convert arrow to Aas Jons of SubmodelElementCollection
        member x.ToAjSMC(): JNode =
            let props = [|
                yield! x.CollectProperties()
                yield! seq {
                    JObj().TrySetProperty(x.Source, "Source")
                    JObj().TrySetProperty(x.Target, "Target")
                    JObj().TrySetProperty(x.Type, "Type")
                } |> choose id |> Seq.cast<JNode>

            |]

            JObj().ToAjSMC("Arrow", props)

    let toAjSMC (semanticKey:string) (values:JNode[]) =
        match values with
        | [||] -> None
        | _ ->
            JObj()
                .SetSemantic(semanticKey)
                .Set(N.ModelType, ModelType.SubmodelElementCollection.ToString())
                |> _.AddValues(values)
                |> Some

    type NjWork with    // ToAjSMC
        /// DsWork -> JNode
        member x.ToAjSMC(): JNode =
            let arrows = x.Arrows |-> _.ToAjSMC() |> toAjSMC "Arrows"
            let calls  = x.Calls  |-> _.ToAjSMC() |> toAjSMC "Calls"
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

            JObj().ToAjSMC("Work", props)
            |> _.AddValues([|arrows; calls|] |> choose id)


