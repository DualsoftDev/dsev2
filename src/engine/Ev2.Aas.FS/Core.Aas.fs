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
    type Submodel = AasCore.Aas3_0.Submodel
    type SubmodelElementCollection = AasCore.Aas3_0.SubmodelElementCollection
    type SubmodelElementList = AasCore.Aas3_0.SubmodelElementList
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


    (* 확인 필요 *)
    type KeyType =
        | ConceptDescription

        | AssetAdministrationShell
        | Blob
        | EventElement
        | ExternalReference
        | FragmentReference
        | GlobalReference
        //| ModelReference
        | Property
        | Submodel
        | SubmodelElementList


    type ModelType =
        | AnnotatedRelationshipElement
        | AssetAdministrationShell
        | BasicEventElement
        | Blob
        | Capability
        | ConceptDescription
        | DataSpecificationIec61360
        | Entity
        | File
        | MultiLanguageProperty
        | Operation
        | Property
        | Range
        | ReferenceElement
        | RelationshipElement
        | Submodel
        | SubmodelElementCollection
        | SubmodelElementList

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


    let wrapWith(nodeType:N) (child:JNode): JObj = JObj().Set(nodeType, child)


    type System.Text.Json.Nodes.JsonObject with
        member x.Set(key:N, value:string):  JObj = x |> tee(fun x -> if value.NonNullAny() then x[key.ToString()] <- value)
        member x.Set(key:N, ja:JArr):       JObj = x |> tee(fun x -> if ja.NonNullAny()    then x[key.ToString()] <- ja)
        member x.Set(key:N, jn:JNode):      JObj = x |> tee(fun x -> if isItNotNull jn     then x[key.ToString()] <- jn)
        member x.Set(key:N, jns:JNode seq): JObj = x |> tee(fun x -> if jns.NonNullAny()   then x[key.ToString()] <- JArr (jns.ToArray()))

        member x.SetValues(jns:JNode seq) = x.Set(N.Value, jns)

        /// JObj 의 value 속성에 JNode 를 append 추가.  SetValues 는 덮어쓰기 용
        member x.AddValues(jns:#JNode seq) =

            let key = N.Value.ToString()
            match x.TryGetPropertyValue(key) with
            | true, (:? JArr as ja) ->
                for jn in jns do ja.Add(jn) // 개별적으로 추가
                x
            | _ ->
                x.SetValues(jns |> Seq.cast<JNode>) // 새로운 JsonArray 생성

        (*
          <valueType>xs:integer</valueType>
          <value></value>
        *)
        member x.SetTypedValue<'T>(value:'T) =
            match box value with
            | :? string  as v -> x.Set(N.ValueType, "xs:string") .Set(N.Value, v)
            | :? int     as v -> x.Set(N.ValueType, "xs:integer").Set(N.Value, v.ToString())
            | :? double  as v -> x.Set(N.ValueType, "xs:double") .Set(N.Value, v.ToString())
            | :? single  as v -> x.Set(N.ValueType, "xs:float")  .Set(N.Value, v.ToString())
            | :? bool    as v -> x.Set(N.ValueType, "xs:boolean").Set(N.Value, v.ToString())

            | _ -> failwithf "Not supported type: %A" typeof<'T>.Name

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
            x.Set(N.Keys, JArr [| JObj().Set(N.Type, keyType.ToString()).Set(N.Value, keyValue) :> JNode |]  )
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

        /// category, idShort, id, modelType, semanticId 등의 속성을 가진 JObj 를 생성
        ///
        /// value 와 values 는 양립할 수 없다.
        /// value : single typed value
        /// values : multiple values
        // semantic = J.CreateSemantic(semanticType, keyType, "Vertices"),

        member x.AddProperties<'T>(
            ?category:Category,
            ?idShort:string,
            ?id:string,
            ?modelType:ModelType,
            ?semantic:JObj,
            ?value:'T,
            ?values:JNode seq
        ): JObj =
            assert(value.IsNone || values.IsNone)
            x |> tee(fun j ->
                category  .Iter(fun y  -> j.Set(N.Category,  y.ToString()) |> ignore)
                modelType .Iter(fun y  -> j.Set(N.ModelType, y.ToString()) |> ignore)
                idShort   .Iter(fun y  -> j.Set(N.IdShort,   y)            |> ignore)
                id        .Iter(fun y  -> j.Set(N.Id,        y)            |> ignore)
                semantic  .Iter(fun y  -> j.Set(N.SemanticId,y)            |> ignore)
                value     .Iter(fun y  -> j.SetTypedValue(y)               |> ignore)
                values    .Iter(fun ys -> j.AddValues(ys)                  |> ignore)
            )

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
        /// JNode[] -> JArr 변환
        static member CreateJArr(jns:#JNode seq): JArr = jns |> Seq.cast<JNode> |> toArray |> JArr

        static member WrapWith(nodeType:N, child:JNode): JNode = wrapWith nodeType child

        /// "semanticId" 에 할당하기 위힌 노드를 생성
        static member CreateSemantic(semanticIdType:SemanticIdType, keyType:KeyType, keyValue:string): JObj =
            JObj()
                .Set(N.Type, semanticIdType.ToString())
                .SetKeys(keyType, keyValue)


        /// category, idShort, id, modelType, semanticId 등의 속성을 가진 JObj 를 생성
        ///
        /// value 와 values 는 양립할 수 없다.
        /// value : single typed value
        /// values : multiple values
        static member CreateProperties<'T>(
            ?category:Category,
            ?idShort:string,
            ?id:string,
            ?modelType:ModelType,
            ?semantic:JObj,
            ?typedValue:'T,
            ?values:JNode seq
        ): JObj =
            JObj().AddProperties(
                ?category   = category,
                ?idShort    = idShort,
                ?id         = id,
                ?modelType  = modelType,
                ?semantic   = semantic,
                ?value = typedValue,
                ?values     = values)

        (* value 속성을 가진 <property> JObj 를 생성
        // <property>

          <idShort>something3fdd3eb4</idShort>
          <valueType>xs:double</valueType>
          <value>1234.01234</value>

        // </property>
        *)
        static member CreateValueProperty<'T>(idShort:string, value:'T): JObj =
            J.CreateProperties(idShort = idShort, typedValue = value, modelType = ModelType.Property)


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
