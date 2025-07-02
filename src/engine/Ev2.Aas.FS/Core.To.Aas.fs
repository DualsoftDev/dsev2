namespace rec Dual.Ev2.Aas

(* Core 를 AAS Json/Xml 로 변환하기 위한 실제 코드 *)

open Dual.Common.Core.FS
open Dual.Ev2
open System
open Dual.Common.Base
open Ev2.Core.FS

[<AutoOpen>]
module CoreGraphToAas =
    type NjArrow with
        /// Convert arrow to submodelElementCollection
        member x.ToSMC(): JObj =
            let source = J.CreateProp( idShort = "Source", value = x.Source )
            let target = J.CreateProp( idShort = "Target", value = x.Target )
            let et     = J.CreateProp( idShort = "EdgeType", value = x.Type.ToString() )

            let edge   = J.CreateJObj( idShort = "Edge", modelType = A.smc, values = [| et; source; target; |] )
            edge

//    type VertexDTO with
//        /// Convert EdgeDTO to submodelElementCollection
//        member x.ToSMC(): JObj =
//            let xx = x
//            J.CreateJObj(
//                idShort = "Vertex",
//                modelType = A.smc,
//                values = [|
//                    J.CreateProp("Guid", x.Guid.ToString())
//                    J.CreateProp("ContentGuid", x.ContentGuid.ToString())
//                |]
//            )

//    (*
//		<category></category>
//		<idShort>Document01</idShort>
//		<semanticId>
//			<type>ExternalReference</type>
//			<keys>
//				<key>
//					<type>ConceptDescription</type>
//					<value>0173-1#02-ABI500#001/0173-1#01-AHF579#001*01</value>
//				</key>
//			</keys>
//		</semanticId>
//    *)


//    type GuidVertex with    // ToSMC()
//        /// Convert GridVertex to submodelElementCollection
//        member x.ToSMC(): JObj =
//            assert(false)
//            null

//    type DsItemWithGraph with
//        /// IGraph.ToSMC() -> JNode
//        member x.GraphToSMC(): JObj =
//            match x with
//            | :? DsSystem as y -> y.WriteJsonProlog()
//            | :? DsWork as y -> y.WriteJsonProlog()
//            | _ -> failwith "ERROR"

//            let vs = x.VertexDTOs |> map _.ToSMC() |> Seq.cast<JNode>
//            let vs =
//                J.CreateJObj(
//                    idShort = "Vertices",
//                    modelType = A.smc,
//                    values = vs
//                )

//            let es = x.EdgeDTOs  |> map _.ToSMC()  |> Seq.cast<JNode>
//            let es =
//                J.CreateJObj(
//                    idShort = "Edges",
//                    modelType = A.smc,
//                    values = es
//                )

//            let graph =
//                J.CreateJObj(
//                    idShort = "Graph",
//                    modelType = A.smc,
//                    values = [|vs; es|]
//                )
//            graph




[<AutoOpen>]
module CoreToAas =

    type NjSystem with
        member private x.collectChildren(): JObj[] =
            let fs = x.Flows |> map _.ToSMC() |> Seq.cast<JNode>
            let flows =
                J.CreateJObj(
                    idShort = "Flows",
                    modelType = A.smc,
                    values = fs
                )

            //let jGraph = x.GraphToSMC()
            let ws = x.Works |> map _.ToSMC() //|> Seq.cast<JNode>
            let works =
                J.CreateJObj(
                    idShort = "Works",
                    modelType = A.smc,
                    values = (ws |> Seq.cast<JNode>)
                )

            let arrs = x.Arrows |-> _.ToSMC() |> Seq.cast<JNode>
            let arrows =
                J.CreateJObj(
                    idShort = "Arrows",
                    modelType = A.smc,
                    values = arrs
                )

            //[|flows; arrows; works|]


            J.CreateJObj(
                //modelType = A.sml,
                smec = [|flows; arrows; works|] ) |> Array.singleton


        ///// DsSystem -> JNode(SMC: Submodel Element Collection)
        //member x.ToSMC(): JObj =
        //    x.DsNamedObjectToSMC("System")
        //        .AddValues(x.collectChildren())

        member sys.ToSM(): JObj =
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
        member x.ToSMC(): JObj =
            x.DsNamedObjectToSMC("Flow")

    type NjWork with    // ToSMC
        /// DsWork -> JNode
        member x.ToSMC(): JObj =
            let arrowNodes = x.Arrows |-> _.ToSMC() |> Seq.cast<JNode> |> toArray
            let arrows =
                J.CreateJObj(
                    idShort = "Arrows",
                    modelType = A.smc,
                    values = arrowNodes
                )

            let callNodes = x.Calls |> map _.ToSMC() |> Seq.cast<JNode> |> toArray
            let calls =
                J.CreateJObj(
                    idShort = "Calls",
                    modelType = A.smc,
                    values = callNodes
                )

            x.DsNamedObjectToSMC("Work")
                .AddValues([|arrows; calls|])

    type NjCall with
        /// DsAction -> JNode
        member x.ToSMC(): JObj =
            x.DsNamedObjectToSMC("Call")
                .AddProperties(values=[
                    J.CreateJObj(
                        idShort = "IsDisable"
                        , modelType = ModelType.Property
                        , typedValue = x.IsDisabled
                    )
                ])


    //type IRtArrow with  // RtArrowBetweenCalls, RtArrowBetweenWorks
    //    /// DsAction -> JNode
    //    member x.ToSMC(): JObj =
    //        x.DsNamedObjectToSMC("Arrow")
    //            .AddProperties(values=[
    //                J.CreateJObj(
    //                    idShort = "IsDisable"
    //                    , modelType = ModelType.Property
    //                    //, typedValue = x.IsDisabled
    //                )
    //            ])


//    type DsAutoPre with
//        /// DsAutoPre -> JNode
//        member x.ToSMC(): JObj = x.DsNamedObjectToSMC("AutoPre")

//    type DsSafety with
//        /// DsSafety -> JNode
//        member x.ToSMC(): JObj = x.DsNamedObjectToSMC("Safety")

//    type DsCommand with
//        /// DsCommand -> JNode
//        member x.ToSMC(): JObj = x.DsNamedObjectToSMC("Command")

//    type DsOperator with
//        /// DsOperator -> JNode
//        member x.ToSMC(): JObj = x.DsNamedObjectToSMC("Operator")


    //type IWithName with
    type INjUnique with
        member internal x.DsNamedObjectToSMC(typeName:string, ?modelType:ModelType): JObj =
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

