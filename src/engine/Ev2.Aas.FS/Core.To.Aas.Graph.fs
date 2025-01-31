namespace rec Dual.Ev2.Aas

(* Core 를 AAS Json/Xml 로 변환하기 위한 실제 코드 *)

open Dual.Common.Core.FS
open Dual.Ev2
open System.Linq

[<AutoOpen>]
module CoreToAas =
    let private semanticType = SemanticIdType.ExternalReference
    let private keyType = KeyType.ConceptDescription
    let private modelType = ModelType.SubmodelElementCollection

    type EdgeDTO with
        /// Convert EdgeDTO to submodelElementCollection
        member x.ToSMEC(?wrap:bool):JNode =
            let wrap = wrap |? false

            let source =
                J.CreateProperties(
                    category = Category.CONSTANT,
                    idShort = "Source",
                    modelType = modelType,
                    semantic = J.CreateSemantic(semanticType, keyType, x.Source)
                )

            let target =
                J.CreateProperties(
                    category = Category.CONSTANT,
                    idShort = "Target",
                    modelType = modelType,
                    semantic = J.CreateSemantic(semanticType, keyType, x.Target)
                )

            let edge =
                J.CreateProperties(
                    idShort = "Edge",
                    modelType = modelType,
                    semantic = J.CreateSemantic(semanticType, keyType, x.EdgeType.ToString())
                ).SetValues([| source; target; |])

            if wrap then
                edge |> wrapWith N.SubmodelElementCollection
            else
                edge


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


    type IGraph with
        /// IGraph.ToProperties() -> JNode
        member x.GraphToProperties(): JNode =
            match x with
            | :? DsFlow as y -> y.PrepareToJson()
            | :? DsWork as y -> y.PrepareToJson()
            | _ -> failwith "ERROR"

            //let vs = x.GetVertexDetails() |> map _.ToProperties()
            //let es = x.GetEdgeDTOs()  |> map _.ToSMEC()

            let vs = x.GetVertexDetails() |> map _.ToProperties()
            let vs =
                J.CreateProperties(
                    idShort = "Vertices",
                    modelType = modelType,
                    semantic = J.CreateSemantic(semanticType, keyType, "Vertices")
                ).SetValues(vs)

            let es = x.GetEdgeDTOs()  |> map _.ToSMEC()
            let es =
                J.CreateProperties(
                    idShort = "Edges",
                    modelType = modelType,
                    semantic = J.CreateSemantic(semanticType, keyType, "Edges")
                ).SetValues(es)

            let graph =
                J.CreateProperties(
                    idShort = "Graph",
                    modelType = modelType,
                    semantic = J.CreateSemantic(semanticType, keyType, "Graph")
                ).SetValues([|vs; es|])
            graph







    type DsSystem with
        /// DsFlow -> JNode
        member x.ToProperties(): JNode = x.DsNamedObjectToProperties("System")


    type DsFlow with
        /// DsFlow -> JNode
        member x.ToProperties(): JNode =
            let jGraph = x.GraphToProperties()
            x.DsNamedObjectToProperties("Flow")
                .SetValues([|jGraph|])

    type DsWork with
        /// DsWork -> JNode
        member x.ToProperties(): JNode =
            let jGraph = x.GraphToProperties()
            x.DsNamedObjectToProperties("Work")
                .SetValues([|jGraph|])

    type DsAction with
        /// DsAction -> JNode
        member x.ToProperties(): JNode = x.DsNamedObjectToProperties("Action")


    type DsAutoPre with
        /// DsAutoPre -> JNode
        member x.ToProperties(): JNode = x.DsNamedObjectToProperties("AutoPre")

    type DsSafety with
        /// DsSafety -> JNode
        member x.ToProperties(): JNode = x.DsNamedObjectToProperties("Safety")

    type DsCommand with
        /// DsCommand -> JNode
        member x.ToProperties(): JNode = x.DsNamedObjectToProperties("Command")

    type DsOperator with
        /// DsOperator -> JNode
        member x.ToProperties(): JNode = x.DsNamedObjectToProperties("Operator")


    type DsNamedObject with
        member internal x.DsNamedObjectToProperties(typeName:string): JNode =
            let semantic = J.CreateSemantic(semanticType, keyType, x.Name)
            J.CreateProperties(idShort = typeName, modelType = modelType, semantic = semantic)

    type VertexDetail with
        member x.ToProperties(): JNode =
            match x with
            | Work     y -> y.ToProperties()
            | Action   y -> y.ToProperties()
            | AutoPre  y -> y.ToProperties()
            | Safety   y -> y.ToProperties()
            | Command  y -> y.ToProperties()
            | Operator y -> y.ToProperties()


