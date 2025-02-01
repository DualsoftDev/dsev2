namespace rec Dual.Ev2.Aas

(* Core 를 AAS Json/Xml 로 변환하기 위한 실제 코드 *)

open Dual.Common.Core.FS
open Dual.Ev2
open System.Linq

[<AutoOpen>]
module CoreToAas =
    /// ModelType.SubmodelElementCollection
    let private smc = ModelType.SubmodelElementCollection
    //let private sme = ModelType.SubmodelElement
    let private sml = ModelType.SubmodelElementList
    let private sm = ModelType.Submodel

    type EdgeDTO with
        /// Convert EdgeDTO to submodelElementCollection
        member x.ToProperties(): JObj =
            let source =
                J.CreateValueProperty(
                    idShort = "Source",
                    value = x.Source
                )

            let target =
                J.CreateValueProperty(
                    idShort = "Target",
                    value = x.Target
                )

            let et =
                J.CreateValueProperty(
                    idShort = "EdgeType",
                    value = x.EdgeType.ToString()
                )

            let edge =
                J.CreateProperties(
                    idShort = "Edge",
                    modelType = smc,
                    values = [| et; source; target; |]
                )

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
        member x.GraphToProperties(): JObj =
            match x with
            | :? DsFlow as y -> y.PrepareToJson()
            | :? DsWork as y -> y.PrepareToJson()
            | _ -> failwith "ERROR"

            let vs = x.GetVertexDetails() |> map _.ToProperties() |> Seq.cast<JNode>
            let vs =
                J.CreateProperties(
                    idShort = "Vertices",
                    modelType = smc,
                    values = vs
                )

            let es = x.GetEdgeDTOs()  |> map _.ToProperties()  |> Seq.cast<JNode>
            let es =
                J.CreateProperties(
                    idShort = "Edges",
                    modelType = smc,
                    values = es
                )

            let graph =
                J.CreateProperties(
                    idShort = "Graph",
                    modelType = smc,
                    values = [|vs; es|]
                )
            graph







    type DsSystem with
        /// DsFlow -> JNode
        member x.ToProperties(): JObj =
            let fs = x.Flows |> map _.ToProperties() |> Seq.cast<JNode>
            let value =
                J.CreateProperties(
                    idShort = "Flows",
                    modelType = smc,
                    values = fs
                )
            x.DsNamedObjectToProperties("System")
                .AddValues([|value|])


    type DsFlow with
        /// DsFlow -> JNode
        member x.ToProperties(): JObj =
            let jGraph = x.GraphToProperties()
            x.DsNamedObjectToProperties("Flow")
                .AddValues([|jGraph|])

    type DsWork with
        /// DsWork -> JNode
        member x.ToProperties(): JObj =
            let jGraph = x.GraphToProperties()
            x.DsNamedObjectToProperties("Work")
                .AddValues([|jGraph|])

    type DsAction with
        /// DsAction -> JNode
        member x.ToProperties(): JObj =
            x.DsNamedObjectToProperties("Action")
                .AddProperties(values=[
                    J.CreateProperties(
                        idShort = "IsDisable",
                        modelType = ModelType.Property,
                        typedValue = x.IsDisabled
                    )
                ])


    type DsAutoPre with
        /// DsAutoPre -> JNode
        member x.ToProperties(): JObj = x.DsNamedObjectToProperties("AutoPre")

    type DsSafety with
        /// DsSafety -> JNode
        member x.ToProperties(): JObj = x.DsNamedObjectToProperties("Safety")

    type DsCommand with
        /// DsCommand -> JNode
        member x.ToProperties(): JObj = x.DsNamedObjectToProperties("Command")

    type DsOperator with
        /// DsOperator -> JNode
        member x.ToProperties(): JObj = x.DsNamedObjectToProperties("Operator")


    type DsNamedObject with
        member internal x.DsNamedObjectToProperties(typeName:string, ?modelType:ModelType): JObj =
            let modelType = modelType |? smc
            J.CreateProperties(idShort = typeName, modelType = modelType, values=[J.CreateValueProperty("Name", x.Name)])

    type VertexDetail with
        member x.ToProperties(): JObj =
            match x with
            | Work     y -> y.ToProperties()
            | Action   y -> y.ToProperties()
            | AutoPre  y -> y.ToProperties()
            | Safety   y -> y.ToProperties()
            | Command  y -> y.ToProperties()
            | Operator y -> y.ToProperties()


