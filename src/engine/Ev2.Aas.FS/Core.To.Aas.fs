namespace rec Dual.Ev2.Aas

(* Core 를 AAS Json/Xml 로 변환하기 위한 실제 코드 *)

open Dual.Common.Core.FS
open Dual.Ev2
open System
open Dual.Common.Base

[<AutoOpen>]
module CoreGraphToAas =
    type EdgeDTO with
        /// Convert EdgeDTO to submodelElementCollection
        member x.ToSMC(): JObj =
            let source =
                J.CreateProp(
                    idShort = "Source",
                    value = x.Source
                )

            let target =
                J.CreateProp(
                    idShort = "Target",
                    value = x.Target
                )

            let et =
                J.CreateProp(
                    idShort = "EdgeType",
                    value = x.EdgeType.ToString()
                )

            let edge =
                J.CreateJObj(
                    idShort = "Edge",
                    modelType = A.smc,
                    values = [| et; source; target; |]
                )

            edge

    type VertexDTO with
        /// Convert EdgeDTO to submodelElementCollection
        member x.ToSMC(): JObj =
            let xx = x
            J.CreateJObj(
                idShort = "Vertex",
                modelType = A.smc,
                values = [|
                    J.CreateProp("Guid", x.Guid.ToString())
                    J.CreateProp("ContentGuid", x.ContentGuid.ToString())
                |]
            )

    (*
		<category></category>
		<idShort>Document01</idShort>
		<semanticId>
			<type>ExternalReference</type>
			<keys>
				<key>
					<type>ConceptDescription</type>
					<value>0173-1#02-ABI500#001/0173-1#01-AHF579#001*01</value>
				</key>
			</keys>
		</semanticId>
    *)


    type GuidVertex with    // ToSMC()
        /// Convert GridVertex to submodelElementCollection
        member x.ToSMC(): JObj =
            assert(false)
            null

    type DsItemWithGraph with
        /// IGraph.ToSMC() -> JNode
        member x.GraphToSMC(): JObj =
            match x with
            | :? DsSystem as y -> y.WriteJsonProlog()
            | :? DsWork as y -> y.WriteJsonProlog()
            | _ -> failwith "ERROR"

            let vs = x.VertexDTOs |> map _.ToSMC() |> Seq.cast<JNode>
            let vs =
                J.CreateJObj(
                    idShort = "Vertices",
                    modelType = A.smc,
                    values = vs
                )

            let es = x.EdgeDTOs  |> map _.ToSMC()  |> Seq.cast<JNode>
            let es =
                J.CreateJObj(
                    idShort = "Edges",
                    modelType = A.smc,
                    values = es
                )

            let graph =
                J.CreateJObj(
                    idShort = "Graph",
                    modelType = A.smc,
                    values = [|vs; es|]
                )
            graph




[<AutoOpen>]
module CoreToAas =

    type DsSystem with
        /// DsSystem -> JNode(SMC: Submodel Element Collection)
        member x.ToSMC(): JObj =
            let fs = x.Flows |> map _.ToSMC() |> Seq.cast<JNode>
            let flows =
                J.CreateJObj(
                    idShort = "Flows",
                    modelType = A.smc,
                    values = fs
                )

            let jGraph = x.GraphToSMC()
            let ws = x.Works |> map _.ToSMC() |> Seq.cast<JNode>
            let works =
                J.CreateJObj(
                    idShort = "Works",
                    modelType = A.smc,
                    values = ws
                )

            x.DsNamedObjectToSMC("System")
                .AddValues([|flows; works|])

        member x.ToSM(): JObj =
            let sml =
                let sysName = J.CreateProp("Name", x.Name)
                let flows = x.Flows.Map _.ToSMC()
                ([sysName] @ flows) //|> map (fun smc -> smc.WrapWith(modelType = ModelType.SubmodelElement))
            let sm =
                J.CreateJObj(
                    category = Category.CONSTANT,
                    modelType = ModelType.Submodel,
                    idShort = "Identification",
                    id = A.ridIdentification,
                    kind = KindType.Instance,
                    semantic = J.CreateSemantic(SemanticIdType.ModelReference, KeyType.Submodel, A.ridIdentification),
                    sml = sml
                )//.AddValues([|x.ToSMC()|])
            sm


        [<Obsolete("TODO")>] member x.ToENV(): JObj = null
        [<Obsolete("TODO")>] member x.ToAasJsonENV(): string = null


    type DsFlow with    // ToSMC
        /// DsFlow -> JNode
        member x.ToSMC(): JObj =
            x.DsNamedObjectToSMC("Flow")

    type DsWork with    // ToSMC
        /// DsWork -> JNode
        member x.ToSMC(): JObj =
            let jGraph = x.GraphToSMC()
            let acts = x.Actions |> map _.ToSMC() |> Seq.cast<JNode>
            let actions =
                J.CreateJObj(
                    idShort = "Works",
                    modelType = A.smc,
                    values = acts
                )

            x.DsNamedObjectToSMC("Work")
                .AddValues([|jGraph; actions|])

    type DsAction with
        /// DsAction -> JNode
        member x.ToSMC(): JObj =
            x.DsNamedObjectToSMC("Action")
                .AddProperties(values=[
                    J.CreateJObj(
                        idShort = "IsDisable",
                        modelType = ModelType.Property,
                        typedValue = x.IsDisabled
                    )
                ])


    type DsAutoPre with
        /// DsAutoPre -> JNode
        member x.ToSMC(): JObj = x.DsNamedObjectToSMC("AutoPre")

    type DsSafety with
        /// DsSafety -> JNode
        member x.ToSMC(): JObj = x.DsNamedObjectToSMC("Safety")

    type DsCommand with
        /// DsCommand -> JNode
        member x.ToSMC(): JObj = x.DsNamedObjectToSMC("Command")

    type DsOperator with
        /// DsOperator -> JNode
        member x.ToSMC(): JObj = x.DsNamedObjectToSMC("Operator")


    type IWithName with
        member internal x.DsNamedObjectToSMC(typeName:string, ?modelType:ModelType): JObj =
            let modelType = modelType |? A.smc
            let vals:JNode seq = [
                    J.CreateProp("Name", x.Name)
                    match x with
                    | :? IGuid as guid ->
                        J.CreateProp("Guid", guid.Guid.ToString())
                    | _ ->
                        ()
                ]
            J.CreateJObj( idShort = typeName, modelType = modelType, values=vals)

