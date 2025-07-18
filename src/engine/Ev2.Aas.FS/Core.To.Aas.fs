namespace rec Dual.Ev2.Aas

(* Core 를 AAS Json/Xml 로 변환하기 위한 실제 코드 *)

open System

open Dual.Common.Core.FS
open Ev2.Core.FS
open Dual.Common.Base
open AasCore.Aas3_0
open System.Text.Json

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


    type NjProject with
        member private x.collectChildren(): JNode[] =
            let me = x
            let details =
                let props = [|
                    yield! x.CollectProperties()
                    yield! seq {
                        if isItNotNull x.Database then
                            JObj().TrySetProperty(x.Database.ToString(), "Database")
                        JObj().TrySetProperty(x.Description,         "Description")
                        JObj().TrySetProperty(x.Author,              "Author")
                        JObj().TrySetProperty(x.Version.ToString(),  "Version")
                        JObj().TrySetProperty(x.DateTime,            "DateTime")
                    } |> choose id |> Seq.cast<JNode>
                |]
                JObj()
                    .ToSjSMC("Details", props)

            let actives = x.ActiveSystems |-> _.ToSjSMC()
            let activeSystems =
                JObj().AddProperties(
                    modelType = A.smc
                    , values = actives
                    , semanticKey = "ActiveSystems"
                )


            let passives = x.PassiveSystems |-> _.ToSjSMC()
            let passiveSystems =
                JObj().AddProperties(
                    modelType = A.smc
                    , values = passives
                    , semanticKey = "PassiveSystems"
                )
            //let passives = x.PassiveSystems |-> _.ToString() |-> (fun z -> JObj().AddProperties(modelType=ModelType.Property, value=z)) |> Seq.cast<JNode> |> J.CreateJArr
            //let passiveSystems =
            //    JObj().AddProperties(
            //        modelType = A.smc
            //        , values = passives
            //        , semanticKey = "PassiveSystems"
            //    )

            [| details; activeSystems; passiveSystems |]

        /// To [S]ystem [J]son Submodel element (SME) 형태로 변환
        member prj.ToSjSubmodel(): JNode =
            let sm =
                JObj().AddProperties(
                    category = Category.CONSTANT
                    , modelType = ModelType.Submodel
                    , id = guid2str prj.Guid
                    , idShort = SubmodelIdShort
                    , kind = KindType.Instance
                    , semanticKey = "Project"
                    , smel = prj.collectChildren()
                )
            sm



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
                JObj().ToSjSMC("Details", props)


            let fs = x.Flows |-> _.ToSjSMC()
            let flows =
                JObj().AddProperties(
                    modelType = A.smc
                    , values = fs
                    , semanticKey = "Flows"
                )

            let ws = x.Works |-> _.ToSjSMC()
            let works =
                JObj().AddProperties(
                    modelType = A.smc
                    , values = ws
                    , semanticKey = "Works"
                )

            let arrs = x.Arrows |-> _.ToSjSMC()
            let arrows =
                JObj().AddProperties(
                    modelType = A.smc
                    , values = arrs
                    , semanticKey = "Arrows"
                )


            let ads = x.ApiDefs |-> _.ToSjSMC()
            let apiDefs =
                JObj().AddProperties(
                    modelType = A.smc
                    , values = ads
                    , semanticKey = "ApiDefs"
                )

            let acs = x.ApiCalls |-> _.ToSjSMC()
            let apiCalls =
                JObj().AddProperties(
                    modelType = A.smc
                    , values = acs
                    , semanticKey = "ApiCalls"
                )


            [| details; apiDefs; apiCalls; flows; arrows; works |]

        /// To [S]ystem [J]son Submodel element (SME) 형태로 변환
        member sys.ToSjSubmodel(): JNode =
            let sm =
                JObj().AddProperties(
                    category = Category.CONSTANT,
                    modelType = ModelType.Submodel,
                    id = guid2str sys.Guid,
                    kind = KindType.Instance,
                    semanticKey = "System",
                    smel = sys.collectChildren()
                )
            sm

        /// To [S]ystem [J]son [S]ub[M]odel element [C]llection (SMEC) 형태로 변환
        member sys.ToSjSMC(): JNode =
            let props = sys.CollectProperties()
            JObj().ToSjSMC("System", props)
            |> _.AddValues(sys.collectChildren())

        [<Obsolete("TODO")>] member x.ToENV(): JObj = null
        [<Obsolete("TODO")>] member x.ToAasJsonENV(): string = null

    type NjApiDef with
        /// To [S]ystem [J]son [S]ub[M]odel element [C]llection (SMEC) 형태로 변환
        member x.ToSjSMC(): JNode =
            let props = [|
                yield! x.CollectProperties()
                yield! seq {
                    JObj().TrySetProperty(x.IsPush, "IsPush")
                } |> choose id |> Seq.cast<JNode>
            |]

            JObj().ToSjSMC("ApiDef", props)

    type NjApiCall with
        /// To [S]ystem [J]son [S]ub[M]odel element [C]llection (SMEC) 형태로 변환
        member x.ToSjSMC(): JNode =
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

            JObj().ToSjSMC("ApiCall", props)


    type NjButton with
        member x.ToSjSMC(): JNode =
            let props = x.CollectProperties()
            JObj().ToSjSMC("Button", props)

    type NjLamp with
        member x.ToSjSMC(): JNode =
            let props = x.CollectProperties()
            JObj().ToSjSMC("Lamp", props)

    type NjCondition with
        member x.ToSjSMC(): JNode =
            let props = x.CollectProperties()
            JObj().ToSjSMC("Condition", props)

    type NjAction with
        /// To [S]ystem [J]son [S]ub[M]odel element [C]llection (SMEC) 형태로 변환
        member x.ToSjSMC(): JNode =
            let props = x.CollectProperties()
            JObj().ToSjSMC("Action", props)

    type NjFlow with    // ToSjSMC
        /// To [S]ystem [J]son [S]ub[M]odel element [C]llection (SMEC) 형태로 변환
        member x.ToSjSMC(): JNode =
            let buttons    = x.Buttons    |-> _.ToSjSMC() |> toSjSMC "Buttons"
            let lamps      = x.Lamps      |-> _.ToSjSMC() |> toSjSMC "Lamps"
            let conditions = x.Conditions |-> _.ToSjSMC() |> toSjSMC "Conditions"
            let actions    = x.Actions    |-> _.ToSjSMC() |> toSjSMC "Actions"

            let props = x.CollectProperties()
            JObj().ToSjSMC("Flow", props)
            |> _.AddValues([|buttons; lamps; conditions; actions|] |> choose id)

    type NjCall with
        /// To [S]ystem [J]son [S]ub[M]odel element [C]llection (SMEC) 형태로 변환
        member x.ToSjSMC(): JNode =
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

            JObj().ToSjSMC("Call", props)

    type NjArrow with
        /// Convert arrow to Aas Jons of SubmodelElementCollection
        member x.ToSjSMC(): JNode =
            let props = [|
                yield! x.CollectProperties()
                yield! seq {
                    JObj().TrySetProperty(x.Source, "Source")
                    JObj().TrySetProperty(x.Target, "Target")
                    JObj().TrySetProperty(x.Type, "Type")
                } |> choose id |> Seq.cast<JNode>

            |]

            JObj().ToSjSMC("Arrow", props)

    let toSjSMC (semanticKey:string) (values:JNode[]) =
        match values with
        | [||] -> None
        | _ ->
            JObj()
                .SetSemantic(semanticKey)
                .Set(N.ModelType, ModelType.SubmodelElementCollection.ToString())
                |> _.AddValues(values)
                |> Some

    type NjWork with    // ToSjSMC
        /// To [S]ystem [J]son [S]ub[M]odel element [C]llection (SMEC) 형태로 변환
        member x.ToSjSMC(): JNode =
            let arrows = x.Arrows |-> _.ToSjSMC() |> toSjSMC "Arrows"
            let calls  = x.Calls  |-> _.ToSjSMC() |> toSjSMC "Calls"
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

            JObj().ToSjSMC("Work", props)
            |> _.AddValues([|arrows; calls|] |> choose id)
