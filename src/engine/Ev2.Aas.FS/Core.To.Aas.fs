namespace rec Dual.Ev2.Aas

(* Core 를 AAS Json/Xml 로 변환하기 위한 실제 코드 *)

open Dual.Common.Core.FS
open Dual.Ev2
open System.Linq

[<AutoOpen>]
module CoreToAas =
    type EdgeDTO with
        /// Convert EdgeDTO to submodelElementCollection
        member x.ToSMEC(?wrap:bool):JNode =
            let wrap = wrap |? false

            let semanticType, keyType, modelType = SemanticIdType.ExternalReference, KeyType.ConceptDescription, ModelType.SubmodelElementCollection
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
                ).SetValue([| source; target; |])

            if wrap then
                edge |> wrapWith N.SubmodelElementCollection
            else
                edge

    type VertexDetail with
        member x.ToProperties(): JNode =
            let semanticType, keyType, modelType = SemanticIdType.ExternalReference, KeyType.ConceptDescription, ModelType.SubmodelElementCollection
            let v = x.AsVertex()
            let semantic = J.CreateSemantic(semanticType, keyType, v.Name)
            J.CreateProperties(idShort = x.Case, modelType = modelType, semantic = semantic)

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

    type DsSystem with
        member x.ToSubmodel():JNode =
            let sm = JObj()
            sm["category"] <- "CONSTANT"
            sm["idShort"] <- "Identification"

            let arr = JArr (x.Flows.Map(_.ToSMEC()).ToArray())
            sm["submodelElements"] <- arr
            sm


    type DsFlow with
        /// Convert DsFlow to submodelElementCollection
        member x.ToSMEC():JNode =
            let sm = JObj()
            sm["idShort"] <- "Flow"
            let vs = JArr (x.Vertices.Map(_.ToSMEC()).ToArray())
            let es = JArr (x.Edges.Map(_.ToSMEC()).ToArray())
            sm["vertices"] <- vs
            sm["edges"] <- es
            sm


    type DsWork with
        /// Convert DsWork to submodelElementCollection
        member x.ToSMEC():JNode =
            let jo = JObj()
            jo["idShort"] <- "Work"
            let vs = JArr (x.Vertices.Map(_.ToSMEC()).ToArray())
            let es = JArr (x.Edges.Map(_.ToSMEC()).ToArray())
            jo["vertices"] <- vs
            jo["edges"] <- es
            jo

    type DsAction with
        /// Convert EdgeDTO to submodelElementCollection
        member x.ToSMEC():JNode =
            let jo = JObj()
            jo["type"] <- "Action"
            jo


    type DsAutoPre with
        /// Convert DsAutoPre to submodelElementCollection
        member x.ToSMEC():JNode =
            let jo = JObj()
            jo["type"] <- "AutoPre"
            jo

    type DsSafety with
        /// Convert DsSafety to submodelElementCollection
        member x.ToSMEC():JNode =
            let jo = JObj()
            jo["type"] <- "Safety"
            jo

    type DsCommand with
        /// Convert DsCommand to submodelElementCollection
        member x.ToSMEC():JNode =
            let jo = JObj()
            jo["type"] <- "Command"
            jo

    type DsOperator with
        /// Convert DsOperator to submodelElementCollection
        member x.ToSMEC():JNode =
            let jo = JObj()
            jo["type"] <- "Operator"
            jo


    type VertexDetail with
        /// Convert VertexDetail to submodelElementCollection
        /// VertexDetail to AAS json
        member x.ToSMEC() =
            match x with
            | Work     y -> y.ToSMEC()
            | Action   y -> y.ToSMEC()
            | AutoPre  y -> y.ToSMEC()
            | Safety   y -> y.ToSMEC()
            | Command  y -> y.ToSMEC()
            | Operator y -> y.ToSMEC()



