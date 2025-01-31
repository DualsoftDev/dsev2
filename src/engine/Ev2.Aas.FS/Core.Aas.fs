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

    //let [<Literal>] ExternalReference = "ExternalReference"
    //let [<Literal>] GlobalReference = "GlobalReference"
    type SemanticIdType =
        | ExternalReference
        | GlobalReference


    type KeyType =
        | ConceptDescription
        | GlobalReference


    let wrapWith(newContainerName:string) (child:JNode): JNode = JObj().Set(newContainerName, child)


    type System.Text.Json.Nodes.JsonNode with
        member x.Set(key:string, value:string): JNode = x |> tee(fun x -> if value.NonNullAny() then x[key] <- value)
        member x.Set(key:string, arr:JArr):     JNode = x |> tee(fun x -> if arr.NonNullAny()   then x[key] <- arr)
        member x.Set(key:string, jobj:JNode):   JNode = x |> tee(fun x -> if isItNotNull jobj   then x[key] <- jobj)
        member x.Set(key:string, arr:JNode[]):  JNode = x |> tee(fun x -> if arr.NonNullAny()   then x[key] <- JArr arr)

        member x.SetValue(arr:JNode[]) = x.Set("value", arr)
        (*
          <valueType>xs:integer</valueType>
          <value></value>
        *)
        member x.SetTypedValue(value:string) = x.Set("valueType", "xs:string") .Set("value", value)
        member x.SetTypedValue(value:int)    = x.Set("valueType", "xs:integer").Set("value", value.ToString())
        member x.SetTypedValue(value:double) = x.Set("valueType", "xs:double") .Set("value", value.ToString())

        (*
            <keys>
              <key>
                <type>ConceptDescription</type>
                <value>0173-1#02-ABI500#001/0173-1#01-AHF579#001*01</value>
              </key>
            </keys>


            "keys": [
              {
                "type": "GlobalReference",
                "value": "urn:something00:f4547d0c"
              }
            ]
        *)
        member x.SetKeys(keyType:KeyType, keyValue:string) =
            x.Set("keys", JArr [| JObj().Set("type", keyType.ToString()).Set("value", keyValue) |]  )
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
        member x.SetSemanticId(semanticIdType:SemanticIdType, keyType:KeyType, keyValue:string): JNode =
            x.Set("semanticId",
                JObj()
                    .Set("type", semanticIdType.ToString())
                    .SetKeys(keyType, keyValue))

        member x.SetCatId(cat:Category, idShort:string, ?id:string): JNode =
            let id = id |? null
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
        member x.ToSMEC(?wrap:bool):JNode =
            let wrap = wrap |? false
            let source =
                JObj()
                    .SetCatId(CONSTANT, "Source")
                    .Set("modelType", "SubmodelElementCollection")
                    .SetSemanticId(SemanticIdType.ExternalReference, KeyType.ConceptDescription, x.Source)

            let target =
                JObj()
                    .SetCatId(CONSTANT, "Target")
                    .Set("modelType", "SubmodelElementCollection")
                    .SetSemanticId(SemanticIdType.ExternalReference, KeyType.ConceptDescription, x.Target)
                    //.Set("kind", "some-kind")
                    //.SetKeys("Submodel", "0173-1#01-AHF578#001")
                    //|> wrapWith "property"
                    //|> wrapWith "submodelElements"

            let edge =
                JObj()
                    .SetCatId(CONSTANT, "EdgeType")
                    .Set("modelType", "SubmodelElementCollection")
                    .SetSemanticId(SemanticIdType.ExternalReference, KeyType.ConceptDescription, x.EdgeType.ToString())
                    //|> wrapWith "property"

            let smec =
                JObj()
                    //.SetCatId(CONSTANT, "Edge")
                    .Set("modelType", "SubmodelElementCollection")
                    .SetSemanticId(SemanticIdType.ExternalReference, KeyType.ConceptDescription, "keyValue")
                    .Set("value", JArr [| source; target; edge |])

            if wrap then
                smec |> wrapWith "submodelElementCollection"
            else
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



