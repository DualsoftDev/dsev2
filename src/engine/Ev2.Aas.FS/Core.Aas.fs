namespace rec Dual.Ev2.Aas

(* Core 를 AAS Json/Xml 로 변환하기 위한 실제 코드 *)

open Dual.Common.Core.FS
open Dual.Ev2
open System.Linq
open System.Text.Json

type JObj = System.Text.Json.Nodes.JsonObject
type JArr = System.Text.Json.Nodes.JsonArray
type JNode = System.Text.Json.Nodes.JsonNode

[<AutoOpen>]
module JsonExtensionModule =
    type Category =
        | PARAMETER
        | CONSTANT
        | VARIABLE


    let wrapWith(newContainerName:string) (child:JNode): JNode = JObj().Set(newContainerName, child)


    type System.Text.Json.Nodes.JsonNode with
        member x.Set(key:string, value:string): JNode = x |> tee(fun x -> if value.NonNullAny() then x[key] <- value)
        member x.Set(key:string, arr:JArr):     JNode = x |> tee(fun x -> if arr.NonNullAny()   then x[key] <- arr)
        member x.Set(key:string, jobj:JNode):   JNode = x |> tee(fun x -> if isItNotNull jobj   then x[key] <- jobj)
        member x.Set(key:string, arr:JNode[]):  JNode = x |> tee(fun x -> if arr.NonNullAny()   then x[key] <- JArr arr)

        (*
          <valueType>xs:integer</valueType>
          <value></value>
        *)
        member x.SetValue(value:string) =
            x.Set("valueType", "xs:string").Set("value", value)
        member x.SetValue(value:int) =
            x.Set("valueType", "xs:integer").Set("value", value)
        member x.SetValue(value:double) =
            x.Set("valueType", "xs:double").Set("value", value)

        (*
            <keys>
              <key>
                <type>ConceptDescription</type>
                <value>0173-1#02-ABI500#001/0173-1#01-AHF579#001*01</value>
              </key>
            </keys>
        *)
        member x.SetKeys(keyType:string, keyValue:string) =
            let key =
                JObj()
                    .Set("type", keyType)
                    .Set("value", keyValue)
            x.Set("keys", JArr [| JObj().Set("key", key) |]  )
        (*
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
        member x.SetSemanticId(semanticIdType:string, keyType:string, keyValue:string): JNode =
            x.Set("semanticId",
                JObj()
                    .Set("type", semanticIdType)
                    .SetKeys(keyType, keyValue))

        member x.SetCatId(cat:Category, id:string, idShort:string): JNode =
            x.Set("category", cat.ToString())
                .Set("idShort", idShort)
                .Set("id", id)

        //static member WrapWith(newContainerName:string, child:JNode): JNode = wrapWith newContainerName child

        member x.Stringify(?settings:JsonSerializerOptions):string =
                let settings = settings |? JsonSerializerOptions() |> tee(fun s -> s.WriteIndented <- true)
                x.ToJsonString(settings)

[<AutoOpen>]
module CoreToAas =
    type EdgeDTO with
        /// Convert EdgeDTO to submodelElementCollection
        member x.ToSMEC():JNode =
            let source =
                JObj()
                    .SetCatId(CONSTANT, "some-guid", "Source")
                    .Set("kind", "some-kind")
                    .SetSemanticId("semanticIdType", "keyType", "keyValue")
                    .SetKeys("kkt", "kkv")
                    |> wrapWith "property"
                    |> wrapWith "submodelElements"

            let target =
                JObj()
                    .SetCatId(CONSTANT, "some-guid", "Target")
                    .Set("kind", "some-kind")
                    .SetKeys("Submodel", "0173-1#01-AHF578#001")
                    |> wrapWith "property"
                    |> wrapWith "submodelElements"

            let smec =
                JObj()
                    .SetCatId(CONSTANT, "Edge", null)
                    .Set("source", source)
                    .Set("target", target)
                    .Set("type", x.EdgeType.ToString())
            smec

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



