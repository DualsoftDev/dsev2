namespace rec Dual.Ev2.Aas

(* Core 를 AAS Json/Xml 로 변환하기 위한 실제 코드 *)

open Dual.Common.Core.FS
open System.Text.Json

type JObj = System.Text.Json.Nodes.JsonObject
type JArr = System.Text.Json.Nodes.JsonArray
type JNode = System.Text.Json.Nodes.JsonNode


module Aas =
    open AasCore.Aas3_0
    type Jsonization = AasCore.Aas3_0.Jsonization
    type Environment = AasCore.Aas3_0.Environment
    type AssetAdministrationShell = AasCore.Aas3_0.AssetAdministrationShell
    type SubmodelElementCollection = AasCore.Aas3_0.SubmodelElementCollection
    type Xmlization = AasCore.Aas3_0.Xmlization
    type IClass = AasCore.Aas3_0.IClass


[<AutoOpen>]
module JsonExtensionModule =
    type Category =
        | PARAMETER
        | CONSTANT
        | VARIABLE

    type SemanticIdType =
        | ExternalReference
        | GlobalReference


    type KeyType =
        | ConceptDescription
        | GlobalReference

    type ModelType =
        | SubmodelElementCollection

    /// Json/Xml node type.  속성 이름 혹은 node 이름
    type N =
        | AssetKind
        | AssetInformation
        | AssetAdministrationShells
        | Category
        | GlobalAssetId
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
            let s = sprintf "%A" x
            s[0..0].ToLower() + s[1..]  // 첫 글자만 소문자로 변환


    let wrapWith(nodeType:N) (child:JNode): JNode = JObj().Set(nodeType, child)


    type System.Text.Json.Nodes.JsonNode with
        member x.Set(key:N, value:string): JNode = x |> tee(fun x -> if value.NonNullAny() then x[key.ToString()] <- value)
        member x.Set(key:N, ja:JArr):      JNode = x |> tee(fun x -> if ja.NonNullAny()    then x[key.ToString()] <- ja)
        member x.Set(key:N, jn:JNode):     JNode = x |> tee(fun x -> if isItNotNull jn     then x[key.ToString()] <- jn)
        member x.Set(key:N, jns:JNode seq):JNode = x |> tee(fun x -> if jns.NonNullAny()   then x[key.ToString()] <- JArr (jns.ToArray()))

        member x.SetValues(jns:JNode seq) = x.Set(N.Value, jns)
        (*
          <valueType>xs:integer</valueType>
          <value></value>
        *)
        member x.SetTypedValue(value:string) = x.Set(N.ValueType, "xs:string") .Set(N.Value, value)
        member x.SetTypedValue(value:int)    = x.Set(N.ValueType, "xs:integer").Set(N.Value, value.ToString())
        member x.SetTypedValue(value:double) = x.Set(N.ValueType, "xs:double") .Set(N.Value, value.ToString())
        member x.SetModelType(modelType:ModelType) = x.Set(N.ModelType, modelType.ToString())

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
            x.Set(N.Keys, JArr [| JObj().Set(N.Type, keyType.ToString()).Set(N.Value, keyValue) |]  )
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
            x.Set(N.SemanticId,
                JObj()
                    .Set(N.Type, semanticIdType.ToString())
                    .SetKeys(keyType, keyValue))

        /// JsonNode(=> JNode) 를 Json string 으로 변환
        member x.Stringify(?settings:JsonSerializerOptions):string =
                let settings = settings |? JsonSerializerOptions() |> tee(fun s -> s.WriteIndented <- true)
                x.ToJsonString(settings)


    type AasCore.Aas3_0.IClass with
        member x.ToXml() =
            let outputBuilder = System.Text.StringBuilder()
            let settings = System.Xml.XmlWriterSettings(Encoding = System.Text.Encoding.UTF8, OmitXmlDeclaration = true, Indent = true)
            use writer = System.Xml.XmlWriter.Create(outputBuilder, settings)
            AasCore.Aas3_0.Xmlization.Serialize.To(x, writer)
            writer.Flush()
            outputBuilder.ToString()




    [<AbstractClass; Sealed>]
    type J() =
        static member WrapWith(nodeType:N, child:JNode): JNode = wrapWith nodeType child

        /// "semanticId" 에 할당하기 위힌 노드를 생성
        static member CreateSemantic(semanticIdType:SemanticIdType, keyType:KeyType, keyValue:string): JObj =
            JObj()
                .Set(N.Type, semanticIdType.ToString())
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
                category .Iter(fun y -> j.Set(N.Category,  y.ToString()) |> ignore)
                modelType.Iter(fun y -> j.Set(N.ModelType, y.ToString()) |> ignore)
                idShort  .Iter(fun y -> j.Set(N.IdShort,   y)            |> ignore)
                id       .Iter(fun y -> j.Set(N.Id,        y)            |> ignore)
                semantic .Iter(fun y -> j.Set(N.SemanticId,y)            |> ignore)
            )

        //static member CreatePrimitiveProperty<'T when 'T: struct> (data:'T) =
        //    match data.GetType().Name with
        //    | "Int32"   -> JObj().SetTypedValue(data :?> int)

        /// Json string 을 aas core 의 IClass subtype 객체로 변환
        static member CreateIClass<'T when 'T :> Aas.IClass>(json: string) : 'T = J.createIClass<'T> json :?> 'T

        static member private createIClass<'T>(json:string): Aas.IClass =
            let jnode = JNode.Parse(json)
            match typeof<'T>.Name with
            | "IHasSemantics"                       -> Aas.Jsonization.Deserialize.IHasSemanticsFrom                      (jnode)
            | "IHasExtensions"                      -> Aas.Jsonization.Deserialize.IHasExtensionsFrom                     (jnode)
            | "IReferable"                          -> Aas.Jsonization.Deserialize.IReferableFrom                         (jnode)
            | "IIdentifiable"                       -> Aas.Jsonization.Deserialize.IIdentifiableFrom                      (jnode)
            | "IHasKind"                            -> Aas.Jsonization.Deserialize.IHasKindFrom                           (jnode)
            | "IHasDataSpecification"               -> Aas.Jsonization.Deserialize.IHasDataSpecificationFrom              (jnode)
            | "IQualifiable"                        -> Aas.Jsonization.Deserialize.IQualifiableFrom                       (jnode)
            | "ISubmodelElement"                    -> Aas.Jsonization.Deserialize.ISubmodelElementFrom                   (jnode)
            | "IRelationshipElement"                -> Aas.Jsonization.Deserialize.IRelationshipElementFrom               (jnode)
            | "IDataElement"                        -> Aas.Jsonization.Deserialize.IDataElementFrom                       (jnode)
            | "IEventElement"                       -> Aas.Jsonization.Deserialize.IEventElementFrom                      (jnode)
            | "IAbstractLangString"                 -> Aas.Jsonization.Deserialize.IAbstractLangStringFrom                (jnode)
            | "IDataSpecificationContent"           -> Aas.Jsonization.Deserialize.IDataSpecificationContentFrom          (jnode)
            | "Extension"                           -> Aas.Jsonization.Deserialize.ExtensionFrom                          (jnode)
            | "AdministrativeInformation"           -> Aas.Jsonization.Deserialize.AdministrativeInformationFrom          (jnode)
            | "Qualifier"                           -> Aas.Jsonization.Deserialize.QualifierFrom                          (jnode)
            | "AssetAdministrationShell"            -> Aas.Jsonization.Deserialize.AssetAdministrationShellFrom           (jnode)
            | "AssetInformation"                    -> Aas.Jsonization.Deserialize.AssetInformationFrom                   (jnode)
            | "Resource"                            -> Aas.Jsonization.Deserialize.ResourceFrom                           (jnode)
            | "SpecificAssetId"                     -> Aas.Jsonization.Deserialize.SpecificAssetIdFrom                    (jnode)
            | "Submodel"                            -> Aas.Jsonization.Deserialize.SubmodelFrom                           (jnode)
            | "RelationshipElement"                 -> Aas.Jsonization.Deserialize.RelationshipElementFrom                (jnode)
            | "SubmodelElementList"                 -> Aas.Jsonization.Deserialize.SubmodelElementListFrom                (jnode)
            | "SubmodelElementCollection"           -> Aas.Jsonization.Deserialize.SubmodelElementCollectionFrom          (jnode)
            | "Property"                            -> Aas.Jsonization.Deserialize.PropertyFrom                           (jnode)
            | "MultiLanguageProperty"               -> Aas.Jsonization.Deserialize.MultiLanguagePropertyFrom              (jnode)
            | "Range"                               -> Aas.Jsonization.Deserialize.RangeFrom                              (jnode)
            | "ReferenceElement"                    -> Aas.Jsonization.Deserialize.ReferenceElementFrom                   (jnode)
            | "Blob"                                -> Aas.Jsonization.Deserialize.BlobFrom                               (jnode)
            | "File"                                -> Aas.Jsonization.Deserialize.FileFrom                               (jnode)
            | "AnnotatedRelationshipElement"        -> Aas.Jsonization.Deserialize.AnnotatedRelationshipElementFrom       (jnode)
            | "Entity"                              -> Aas.Jsonization.Deserialize.EntityFrom                             (jnode)
            | "EventPayload"                        -> Aas.Jsonization.Deserialize.EventPayloadFrom                       (jnode)
            | "BasicEventElement"                   -> Aas.Jsonization.Deserialize.BasicEventElementFrom                  (jnode)
            | "Operation"                           -> Aas.Jsonization.Deserialize.OperationFrom                          (jnode)
            | "OperationVariable"                   -> Aas.Jsonization.Deserialize.OperationVariableFrom                  (jnode)
            | "Capability"                          -> Aas.Jsonization.Deserialize.CapabilityFrom                         (jnode)
            | "ConceptDescription"                  -> Aas.Jsonization.Deserialize.ConceptDescriptionFrom                 (jnode)
            | "Reference"                           -> Aas.Jsonization.Deserialize.ReferenceFrom                          (jnode)
            | "Key"                                 -> Aas.Jsonization.Deserialize.KeyFrom                                (jnode)
            | "LangStringNameType"                  -> Aas.Jsonization.Deserialize.LangStringNameTypeFrom                 (jnode)
            | "LangStringTextType"                  -> Aas.Jsonization.Deserialize.LangStringTextTypeFrom                 (jnode)
            | "Environment"                         -> Aas.Jsonization.Deserialize.EnvironmentFrom                        (jnode)
            | "EmbeddedDataSpecification"           -> Aas.Jsonization.Deserialize.EmbeddedDataSpecificationFrom          (jnode)
            | "LevelType"                           -> Aas.Jsonization.Deserialize.LevelTypeFrom                          (jnode)
            | "ValueReferencePair"                  -> Aas.Jsonization.Deserialize.ValueReferencePairFrom                 (jnode)
            | "ValueList"                           -> Aas.Jsonization.Deserialize.ValueListFrom                          (jnode)
            | "LangStringPreferredNameTypeIec61360" -> Aas.Jsonization.Deserialize.LangStringPreferredNameTypeIec61360From(jnode)
            | "LangStringShortNameTypeIec61360"     -> Aas.Jsonization.Deserialize.LangStringShortNameTypeIec61360From    (jnode)
            | "LangStringDefinitionTypeIec61360"    -> Aas.Jsonization.Deserialize.LangStringDefinitionTypeIec61360From   (jnode)
            | "DataSpecificationIec61360"           -> Aas.Jsonization.Deserialize.DataSpecificationIec61360From          (jnode)

            | _ -> failwithf "Not supported type: %A" typeof<'T>.Name


            (* Aas.IClass subclass 가 아닌 case *)

            //| "ModellingKind" -> Aas.Jsonization.Deserialize.ModellingKindFrom(jnode)
            //| "QualifierKind" -> Aas.Jsonization.Deserialize.QualifierKindFrom(jnode)
            //| "AssetKind" -> Aas.Jsonization.Deserialize.AssetKindFrom(jnode)
            //| "AasSubmodelElements" -> Aas.Jsonization.Deserialize.AasSubmodelElementsFrom(jnode)
            //| "EntityType" -> Aas.Jsonization.Deserialize.EntityTypeFrom(jnode)
            //| "Direction" -> Aas.Jsonization.Deserialize.DirectionFrom(jnode)
            //| "StateOfEvent" -> Aas.Jsonization.Deserialize.StateOfEventFrom(jnode)
            //| "ReferenceTypes" -> Aas.Jsonization.Deserialize.ReferenceTypesFrom(jnode)
            //| "KeyTypes" -> Aas.Jsonization.Deserialize.KeyTypesFrom(jnode)
            //| "DataTypeDefXsd" -> Aas.Jsonization.Deserialize.DataTypeDefXsdFrom(jnode)
            //| "DataTypeIec61360" -> Aas.Jsonization.Deserialize.DataTypeIec61360From(jnode)
