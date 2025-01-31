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

    type ModelType =
        | SubmodelElementCollection

    /// Json/Xml node type.  속성 이름 혹은 node 이름
    type NodeType =
        | Category
        | SemanticId
        | Type
        | Keys
        | Key
        | Value
        | ValueType
        | ModelType
        | Description
        | Id
        | IdShort
        | SubmodelElements
        | SubmodelElementCollection
        override x.ToString() =
            let s = string x
            s[0..0].ToLower() + s[1..]  // 첫 글자만 소문자로 변환


    let wrapWith(nodeType:NodeType) (child:JNode): JNode = JObj().Set(nodeType.ToString(), child)


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
        member x.SetModelType(modelType:ModelType) = x.Set("modelType", modelType.ToString())

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
        member x.SetSemantic(semanticIdType:SemanticIdType, keyType:KeyType, keyValue:string): JNode =
            x.Set("semanticId",
                JObj()
                    .Set("type", semanticIdType.ToString())
                    .SetKeys(keyType, keyValue))

        //static member WrapWith(nodeType:NodeType, child:JNode): JNode = wrapWith (nodeType.ToString()) child

        member x.Stringify(?settings:JsonSerializerOptions):string =
                let settings = settings |? JsonSerializerOptions() |> tee(fun s -> s.WriteIndented <- true)
                x.ToJsonString(settings)

    [<AbstractClass; Sealed>]
    type J() =
        static member CreateSemantic(semanticIdType:SemanticIdType, keyType:KeyType, keyValue:string): JObj =
            JObj()
                .Set("type", semanticIdType.ToString())
                .SetKeys(keyType, keyValue) :?> JObj


        /// category, idShort, id, modelType, semanticId 등의 속성을 가진 JObj 를 생성
        static member CreateProperties(
            ?category:Category,
            ?idShort:string,
            ?id:string,
            ?modelType:ModelType,
            ?semantic:JObj
        ): JObj =
            JObj() |> tee(fun j ->
                category .Iter(fun y -> j.Set("category",  y.ToString()) |> ignore)
                modelType.Iter(fun y -> j.Set("modelType", y.ToString()) |> ignore)
                idShort  .Iter(fun y -> j.Set("idShort",   y)            |> ignore)
                id       .Iter(fun y -> j.Set("id",        y)            |> ignore)
                semantic .Iter(fun y -> j.Set("semanticId",y)            |> ignore)
            )


