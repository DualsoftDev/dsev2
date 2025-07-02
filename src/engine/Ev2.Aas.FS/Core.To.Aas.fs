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
                J.CreateJObj(
                    idShort = "Flows",
                    modelType = A.smc,
                    values = fs
                )

            let ws = x.Works |-> _.ToSMC()
            let works =
                J.CreateJObj(
                    idShort = "Works",
                    modelType = A.smc,
                    values = ws
                )

            let arrs = x.Arrows |-> _.ToSMC()
            let arrows =
                J.CreateJObj(
                    idShort = "Arrows",
                    modelType = A.smc,
                    values = arrs
                )

            [|flows; arrows; works|]


            //J.CreateJObj(
            //    //modelType = A.sml,
            //    smec = [|flows; arrows; works|] ) |> Array.singleton


        ///// DsSystem -> JNode(SMC: Submodel Element Collection)
        //member x.ToSMC(): JObj =
        //    x.DsNamedObjectToSMC("System")
        //        .AddValues(x.collectChildren())

        member sys.ToSM(): JNode =
            let sml =
                let sysName = J.CreateProp("Name", sys.Name)
                let flows = sys.Flows.Map _.ToSMC()
                ([sysName] @ flows) //|> map (fun smc -> smc.WrapWith(modelType = ModelType.SubmodelElement))
            let sm =
                J.CreateJObj(
                    category = Category.CONSTANT,
                    modelType = ModelType.Submodel,
                    idShort = "Identification",
                    id = A.ridIdentification,
                    kind = KindType.Instance,
                    semantic = J.CreateSemantic(SemanticIdType.ModelReference, KeyType.Submodel, A.ridIdentification),
                    //sml = sml,
                    smel = sys.collectChildren()
                )//.AddValues(x.collectChildren())
            sm


        [<Obsolete("TODO")>] member x.ToENV(): JObj = null
        [<Obsolete("TODO")>] member x.ToAasJsonENV(): string = null


    type NjFlow with    // ToSMC
        /// DsFlow -> JNode
        member x.ToSMC(): JNode =
            x.DsNamedObjectToSMC("Flow")

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


        //member x.ToSMC(): JNode =
        //    let source = J.CreateProp( idShort = "Source", value = x.Source )
        //    let target = J.CreateProp( idShort = "Target", value = x.Target )
        //    let et     = J.CreateProp( idShort = "EdgeType", value = x.Type.ToString() )

        //    let edge   = J.CreateJObj( idShort = "Edge", modelType = A.smc, values = [| et; source; target; |] )
        //    edge


    let toSMC (idShort:string) (values:JNode seq) =
        JObj()
            .Set(N.IdShort, idShort)
            .Set(N.ModelType, ModelType.SubmodelElementCollection.ToString())
            |> _.AddValues(values)

    type NjWork with    // ToSMC
        /// DsWork -> JNode
        member x.ToSMC(): JNode =
            let arrows = x.Arrows |-> _.ToSMC() |> toSMC "Arrows"
            let calls  = x.Calls  |-> _.ToSMC() |> toSMC "Calls"

            x.DsNamedObjectToSMC("Work")
                :?> JObj
                |> _.AddValues([|arrows; calls|])


            //let arrowNodes = x.Arrows |-> _.ToSMC()
            //let arrows =
            //    J.CreateJObj(
            //        idShort = "Arrows",
            //        modelType = A.smc,
            //        values = arrowNodes
            //    )

            //let callNodes = x.Calls |> map _.ToSMC()
            //let calls =
            //    J.CreateJObj(
            //        idShort = "Calls",
            //        modelType = A.smc,
            //        values = callNodes
            //    )

            //x.DsNamedObjectToSMC("Work")
            //    :?> JObj
            //    |> _.AddValues([|arrows; calls|])




    //type IWithName with
    type INjUnique with
        member internal x.DsNamedObjectToSMC(typeName:string, ?modelType:ModelType): JNode =
            let modelType = modelType |? A.smc
            let vals:JNode seq = [
                    match x with
                    | :? INamed as named ->
                        J.CreateProp("Name", named.Name)
                    | _ -> ()
                    match x with
                    | :? IGuid as guid ->
                        J.CreateProp("Guid", guid.Guid.ToString())
                    | _ -> ()
                ]
            J.CreateJObj( idShort = typeName, modelType = modelType, values=vals)

