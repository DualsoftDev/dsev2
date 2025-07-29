namespace rec Dual.Ev2.Aas

(* Core 를 AAS Json/Xml 로 변환하기 위한 실제 코드 *)

open Dual.Common.Core.FS
open System.Text.Json
open System
open Dual.Common.Base
open System.Collections.Generic
open System.Diagnostics
open System.Globalization
open Newtonsoft.Json.Linq
open System.Text.Json.Nodes


/// System.Text.Json.Nodes.JsonNode 의 축약
type JNode = System.Text.Json.Nodes.JsonNode

/// System.Text.Json.Nodes.JsonObject 의 축약.  JsonNode(=>JNode) 를 상속 받음
type JObj = System.Text.Json.Nodes.JsonObject

/// System.Text.Json.Nodes.JsonArray 의 축약.  JsonNode(=>JNode) 를 상속 받음
type JArr = System.Text.Json.Nodes.JsonArray

module AasSemantics =
    let map : Map<string, string> =
        Map [
            "Submodel",         "https://dualsoft.com/aas/submodel"
            "Project",          "https://dualsoft.com/aas/project"
            "System",           "https://dualsoft.com/aas/system"
            "FakeSystemSubmodel", "https://dualsoft.com/aas/fakeSystemSubmodel"
            "Name",             "https://dualsoft.com/aas/unique/name"
            "Guid",             "https://dualsoft.com/aas/unique/guid"
            "Id",               "https://dualsoft.com/aas/unique/id"
            "Parameter",        "https://dualsoft.com/aas/unique/parameter"

            "IRI",              "https://dualsoft.com/aas/system/iri"
            "Author",           "https://dualsoft.com/aas/system/author"
            "EngineVersion",    "https://dualsoft.com/aas/system/engineVersion"
            "LangVersion",      "https://dualsoft.com/aas/system/langVersion"
            "Description",      "https://dualsoft.com/aas/system/description"
            "DateTime",         "https://dualsoft.com/aas/system/dateTime"


            "ApiDefs",          "https://dualsoft.com/aas/plural/apiDefs"
            "ApiCalls",         "https://dualsoft.com/aas/plural/apiCalls"
            "Works",            "https://dualsoft.com/aas/plural/works"
            "Arrows",           "https://dualsoft.com/aas/plural/arrows"
            "Calls",            "https://dualsoft.com/aas/plural/calls"
            "Flows",            "https://dualsoft.com/aas/plural/flows"
            "Buttons",          "https://dualsoft.com/aas/plural/buttons"
            "Lamps",            "https://dualsoft.com/aas/plural/lamps"
            "Conditions",       "https://dualsoft.com/aas/plural/conditions"
            "Actions",          "https://dualsoft.com/aas/plural/actions"
            "Details",          "https://dualsoft.com/aas/singular/details"

            "ApiDef",           "https://dualsoft.com/aas/singular/apiDef"
            "ApiCall",          "https://dualsoft.com/aas/singular/apiCall"
            "Work",             "https://dualsoft.com/aas/singular/work"
            "Arrow",            "https://dualsoft.com/aas/singular/arrow"
            "Call",             "https://dualsoft.com/aas/singular/call"
            "Flow",             "https://dualsoft.com/aas/singular/flow"
            "Button",           "https://dualsoft.com/aas/singular/button"
            "Lamp",             "https://dualsoft.com/aas/singular/lamp"
            "Condition",        "https://dualsoft.com/aas/singular/condition"
            "Action",           "https://dualsoft.com/aas/singular/action"


            "Version",          "https://dualsoft.com/aas/project/version"
            "Database",         "https://dualsoft.com/aas/project/database"
            "ActiveSystems",    "https://dualsoft.com/aas/project/activeSystems"
            "PassiveSystem",    "https://dualsoft.com/aas/project/passiveSystem"
            "PassiveSystems",   "https://dualsoft.com/aas/project/passiveSystems"
            "MyPrototypeSystems",   "https://dualsoft.com/aas/project/myPrototypeSystems"
            "ImportedPrototypeSystems",   "https://dualsoft.com/aas/project/importedPrototypeSystems"

            "IsPush",           "https://dualsoft.com/aas/apiDef/isPush"
            "TopicIndex",       "https://dualsoft.com/aas/apiDef/topicIndex"
            "IsTopicOrigin",    "https://dualsoft.com/aas/apiDef/isTopicOrigin"
            "FlowGuid",         "https://dualsoft.com/aas/work/flowGuid"
            "Motion",           "https://dualsoft.com/aas/work/motion"
            "Script",           "https://dualsoft.com/aas/work/script"
            "IsFinished",       "https://dualsoft.com/aas/work/isFinished"
            "NumRepeat",        "https://dualsoft.com/aas/work/numRepeat"
            "Period",           "https://dualsoft.com/aas/work/period"
            "Delay",            "https://dualsoft.com/aas/work/delay"

            "Type",             "https://dualsoft.com/aas/arrow/type"
            "Source",           "https://dualsoft.com/aas/arrow/source"
            "Target",           "https://dualsoft.com/aas/arrow/target"
            "Call",             "https://dualsoft.com/aas/call/call"
            "CallType",         "https://dualsoft.com/aas/call/callType"
            //"ApiCall",          "https://dualsoft.com/aas/call/apiCall"
            "Timeout",          "https://dualsoft.com/aas/call/timeout"
            "IsDisabled",       "https://dualsoft.com/aas/call/isDisabled"
            "CommonConditions", "https://dualsoft.com/aas/call/commonConditions"
            "AutoConditions",   "https://dualsoft.com/aas/call/autoConditions"
            "Status4",          "https://dualsoft.com/aas/common/status4"     // work or call
            "Status",           "https://dualsoft.com/aas/common/status"     // work or call


            "InAddress",        "https://dualsoft.com/aas/apiCall/inAddress"
            "OutAddress",       "https://dualsoft.com/aas/apiCall/outAddress"
            "InSymbol",         "https://dualsoft.com/aas/apiCall/inSymbol"
            "OutSymbol",        "https://dualsoft.com/aas/apiCall/outSymbol"
            "ValueSpec",        "https://dualsoft.com/aas/apiCall/valueSpec"


            "LocalDefinition",  "https://dualsoft.com/aas/passiveSystemLoadType/localDefinition"
            "InstanceName",     "https://dualsoft.com/aas/passiveSystemLoadType/instanceName"
            "PrototypeGuid",    "https://dualsoft.com/aas/passiveSystemLoadType/prototypeGuid"
            "InstanceGuid",     "https://dualsoft.com/aas/passiveSystemLoadType/instanceGuid"



            "__RidIdentification",   "https://www.hsu-hh.de/aut/aas/identification"
        ]

module Aas =
    open AasCore.Aas3_0
    type Jsonization = AasCore.Aas3_0.Jsonization
    type Xmlization = AasCore.Aas3_0.Xmlization
    type Reference = AasCore.Aas3_0.Reference

    type Environment = AasCore.Aas3_0.Environment
    type AssetAdministrationShell = AasCore.Aas3_0.AssetAdministrationShell
    type Submodel = AasCore.Aas3_0.Submodel
    type SubmodelElementCollection = AasCore.Aas3_0.SubmodelElementCollection
    type SubmodelElementList = AasCore.Aas3_0.SubmodelElementList
    type IClass = AasCore.Aas3_0.IClass


/// AAS extension module
module A =
    /// ModelType.SubmodelElementCollection.   heterogeneous.  struct
    let internal smc = ModelType.SubmodelElementCollection
    /// ModelType.SubmodelElementList.   homogenious.  list, set
    let internal sml = ModelType.SubmodelElementList
    /// ModelType.Submodel
    let internal sm = ModelType.Submodel
    let internal ridIdentification = "__RidIdentification"


[<AutoOpen>]
module JsonExtensionModule =
    type Category =
        | PARAMETER
        | CONSTANT
        | VARIABLE

    type SemanticIdType =
        | ExternalReference
        | GlobalReference
        | ModelReference


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
        | SubmodelElement
        /// heterogeneous.  struct
        | SubmodelElementCollection
        /// homogenious.  list, set
        | SubmodelElementList

    type KindType =
        | Template
        | Instance
        | TemplateQualifier


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
        | Kind
        | Value
        | ValueType
        | ModelType
        | Description
        | Id
        | IdShort
        | Submodel
        | SubmodelElement
        | SubmodelElements
        /// heterogeneous.  struct
        | SubmodelElementCollection
        /// homogenious.  list, set
        | SubmodelElementList
        /// Json node 이름으로 변경을 위해서, camelCase 로 변환 필요
        override x.ToString() =
            let s = sprintf "%A" x
            s[0..0].ToLower() + s[1..]  // 첫 글자만 소문자로 변환


    type AasCore.Aas3_0.IClass with
        member x.ToJson(): string =
            let jsonObject = Aas.Jsonization.Serialize.ToJsonObject(x);
            jsonObject.Stringify()

        /// AasCore.IClass 객체를 XML 문자열로 변환
        member x.ToXml(): string =
            let outputBuilder = System.Text.StringBuilder()
            let settings = System.Xml.XmlWriterSettings(Encoding = System.Text.Encoding.UTF8, OmitXmlDeclaration = true, Indent = true)
            use writer = System.Xml.XmlWriter.Create(outputBuilder, settings)
            AasCore.Aas3_0.Xmlization.Serialize.To(x, writer)
            writer.Flush()
            outputBuilder.ToString()

        // see J.CreateIClassFromJson<'T>(), J.CreateIClassFromXml<'T>() for FromJson(), FromXml() methods


    type PropertyCounter = Dictionary<string, int>
    let thePropertyCounter = PropertyCounter() // 전역으로 사용되는 property counter.  이름별로 몇 개의 property 가 있는지 카운트

    let getCountedName2(name:string) (counter:PropertyCounter option) =
        let counter = counter |? thePropertyCounter // 전역 property counter 사용
        let count =
            if counter.ContainsKey(name) then
                let current = counter.[name]
                counter.[name] <- current + 1
                current + 1
            else
                counter.[name] <- 0
                0
        $"""{name}{if count = 0 then "" else count.ToString()}""" // name + count.  count 가 0 이면 name 만 사용
    let getCountedName name = getCountedName2 name (Some thePropertyCounter)


    let cloneJsonNode (node: JsonNode) : JsonNode =
        let json = node.ToJsonString()
        JsonNode.Parse(json)

    type System.Text.Json.Nodes.JsonObject with
        member x.Set(key:N, value:string):  JObj = x |> tee(fun x -> if value.NonNullAny() then x[key.ToString()] <- value)
        member x.Set(key:N, ja:JArr):       JObj = x |> tee(fun x -> if ja.NonNullAny()    then x[key.ToString()] <- ja)
        member x.Set(key:N, jn:JNode):      JObj = x |> tee(fun x -> if isItNotNull jn     then x[key.ToString()] <- jn)
        member x.Set(key:N, jns:JNode seq): JObj = x |> tee(fun x -> if jns.NonNullAny()   then x[key.ToString()] <- JArr (jns |-> cloneJsonNode |> toArray))

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
        member x.SetTypedValue<'T>(value:'T) : JObj option =
            if box value = null then
                None
            else
                match box value with
                | :? string   as v -> x.Set(N.ValueType, "xs:string")  .Set(N.Value, v)
                | :? int      as v -> x.Set(N.ValueType, "xs:integer") .Set(N.Value, v.ToString())
                | :? int64    as v -> x.Set(N.ValueType, "xs:long")    .Set(N.Value, v.ToString())
                | :? double   as v -> x.Set(N.ValueType, "xs:double")  .Set(N.Value, v.ToString())
                | :? single   as v -> x.Set(N.ValueType, "xs:float")   .Set(N.Value, v.ToString())
                | :? bool     as v -> x.Set(N.ValueType, "xs:boolean") .Set(N.Value, v.ToString())
                | :? Guid     as v -> x.Set(N.ValueType, "xs:string")  .Set(N.Value, v.ToString())
                | :? DateTime as v -> x.Set(N.ValueType, "xs:dateTime").Set(N.Value, v.ToString(CultureInfo("en-US")))

                //// --- Array 처리 ---
                //| :? (Guid[]) as arr ->
                //    let jarr = arr |> Array.map (fun g -> JObj().Set(N.Value)(string g)) |> JArr
                //    x.Set(N.ValueType, "xs:Guid[]").Set(N.Value, jarr)

                //| :? (string[]) as arr ->
                //    let jarr = arr |> Array.map (fun s -> JValue(s)) |> JArr
                //    Some(jobj.Set(N.ValueType, "xs:string[]").Set(N.Value, jarr))

                //| :? (int[]) as arr ->
                //    let jarr = arr |> Array.map (fun i -> JValue(string i) :> JNode) |> JArray
                //    Some(jobj.Set(N.ValueType, "xs:integer[]").Set(N.Value, jarr))


                | _ ->
                    failwithf "Not supported type: %A" typeof<'T>.Name

                |> Some


        member x.SetModelType(modelType:ModelType) = x.Set(N.ModelType, modelType.ToString())

        member this.SetSemantic(semanticKey:string): JObj =
            match this.TryGetPropertyValue(N.IdShort.ToString()) with
            | true, _ -> ()
            | _ ->
                let idShort = getCountedName semanticKey
                this.Set(N.IdShort, idShort) |> ignore // idShort 가 없으면 semanticKey 를 idShort 로 사용

            match AasSemantics.map |> Map.tryFind semanticKey with
            | Some semanticId -> this.SetSemantic(SemanticIdType.ExternalReference, KeyType.ConceptDescription, semanticId)
            | None -> failwithf "Not supported semantic name: %s" semanticKey

        /// value 와 name 만 넘기면 자동으로 idShort, semanticId, modelType 설정
        member this.TrySetProperty<'T>(value:'T, name:string, ?counters: PropertyCounter): JObj option =
            match this.SetTypedValue(value) with
            | Some jobj ->
                let idShort = getCountedName2 name counters

                jobj.SetSemantic(name)
                    .Set(N.IdShort, idShort)
                    .Set(N.ModelType, ModelType.Property.ToString())
                |> Some

            | None ->
                Trace.WriteLine $"Failed to set property [{name}] with value = [{value}]"
                //if not <| name.IsOneOf("Parameter", "Name") then
                //    Debugger.Break() // value 가 null 이면 None 반환
                None


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
            let keys =
                JObj()
                    .Set(N.Type, keyType.ToString())
                    .Set(N.Value, keyValue) :> JNode
            x.Set(N.Keys, JArr [| keys |]  )
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
        member x.SetSemantic(semantic:JObj): JObj = x.Set(N.SemanticId, semantic)
        member x.SetSemantic(semanticIdType:SemanticIdType, keyType:KeyType, keyValue:string): JObj =
            JObj()
                .Set(N.Type, semanticIdType.ToString())
                .SetKeys(keyType, keyValue)
            |> x.SetSemantic

        /// category, idShort, id, modelType, semanticId 등의 속성을 가진 JObj 를 생성
        ///
        /// value 와 values 는 양립할 수 없다.
        /// value : single typed value
        /// values : multiple values

        member x.AddProperties<'T>(
            ?category:Category,
            ?semanticKey:string,
            ?id:string,
            ?idShort:string,
            ?modelType:ModelType,
            ?value:'T,
            ?values:JNode seq,
            ?kind:KindType,
            ?smec:JNode seq,
            ?smel:JNode seq
        ): JObj =
            assert(value.IsNone || values.IsNone)
            x |> tee(fun j ->
                category   .Iter(fun y  -> j.Set(N.Category,  y.ToString()) |> ignore)
                modelType  .Iter(fun y  -> j.Set(N.ModelType, y.ToString()) |> ignore)
                semanticKey.Iter(fun y  -> j.SetSemantic(y)                 |> ignore)
                idShort    .Iter(fun y  -> j.Set(N.IdShort,   y)            |> ignore)
                id         .Iter(fun y  -> j.Set(N.Id,        y)            |> ignore)
                value      .Iter(fun y  -> j.SetTypedValue(y)               |> ignore)
                values     .Iter(fun ys -> j.AddValues(ys)                  |> ignore)
                kind       .Iter(fun y ->  j.Set(N.Kind, y.ToString())      |> ignore)
                smec       .Iter(fun ys -> j.Set(N.SubmodelElementCollection, J.CreateJArr ys) |> ignore)
                smel       .Iter(fun ys -> j.Set(N.SubmodelElements, J.CreateJArr ys)     |> ignore)
            )

        /// To [S]ystem [J]son [S]ub[M]odel element [C]llection (SMEC) 형태로 변환
        member x.ToSjSMC(semanticKey:string, values:JNode seq): JObj =
            x.AddProperties(
                semanticKey = semanticKey
                , modelType = A.smc
                , values = values
            )

    type System.Text.Json.Nodes.JsonNode with
        /// JsonNode(=> JNode) 를 Json string 으로 변환
        member x.Stringify(?settings:JsonSerializerOptions):string =
                let settings = settings |? JsonSerializerOptions() |> tee(fun s -> s.WriteIndented <- true)
                x.ToJsonString(settings)


    // Json 관련 static method 들을 모아놓은 static class
    [<AbstractClass; Sealed>]
    type J() =
        /// JObj[] -> JArr 변환
        static member CreateJArr(jns:JNode seq): JArr = jns |> toArray |> JArr

        /// Json string 을 aas core 의 IClass subtype 객체로 변환
        static member CreateIClassFromJson<'T when 'T :> Aas.IClass>(json: string) : 'T = J.createIClassFromJsonHelper<'T> json :?> 'T

        static member private createIClassFromJsonHelper<'T>(json:string): Aas.IClass =
            let jnode = JNode.Parse(json)
            match typeof<'T>.Name with
            | "AdministrativeInformation"           -> Aas.Jsonization.Deserialize.AdministrativeInformationFrom          (jnode)
            | "AnnotatedRelationshipElement"        -> Aas.Jsonization.Deserialize.AnnotatedRelationshipElementFrom       (jnode)
            | "AssetAdministrationShell"            -> Aas.Jsonization.Deserialize.AssetAdministrationShellFrom           (jnode)
            | "AssetInformation"                    -> Aas.Jsonization.Deserialize.AssetInformationFrom                   (jnode)
            | "BasicEventElement"                   -> Aas.Jsonization.Deserialize.BasicEventElementFrom                  (jnode)
            | "Blob"                                -> Aas.Jsonization.Deserialize.BlobFrom                               (jnode)
            | "Capability"                          -> Aas.Jsonization.Deserialize.CapabilityFrom                         (jnode)
            | "ConceptDescription"                  -> Aas.Jsonization.Deserialize.ConceptDescriptionFrom                 (jnode)
            | "DataSpecificationIec61360"           -> Aas.Jsonization.Deserialize.DataSpecificationIec61360From          (jnode)
            | "EmbeddedDataSpecification"           -> Aas.Jsonization.Deserialize.EmbeddedDataSpecificationFrom          (jnode)
            | "Entity"                              -> Aas.Jsonization.Deserialize.EntityFrom                             (jnode)
            | "Environment"                         -> Aas.Jsonization.Deserialize.EnvironmentFrom                        (jnode)
            | "EventPayload"                        -> Aas.Jsonization.Deserialize.EventPayloadFrom                       (jnode)
            | "Extension"                           -> Aas.Jsonization.Deserialize.ExtensionFrom                          (jnode)
            | "File"                                -> Aas.Jsonization.Deserialize.FileFrom                               (jnode)
            | "IAbstractLangString"                 -> Aas.Jsonization.Deserialize.IAbstractLangStringFrom                (jnode)
            | "IDataElement"                        -> Aas.Jsonization.Deserialize.IDataElementFrom                       (jnode)
            | "IDataSpecificationContent"           -> Aas.Jsonization.Deserialize.IDataSpecificationContentFrom          (jnode)
            | "IEventElement"                       -> Aas.Jsonization.Deserialize.IEventElementFrom                      (jnode)
            | "IHasDataSpecification"               -> Aas.Jsonization.Deserialize.IHasDataSpecificationFrom              (jnode)
            | "IHasExtensions"                      -> Aas.Jsonization.Deserialize.IHasExtensionsFrom                     (jnode)
            | "IHasKind"                            -> Aas.Jsonization.Deserialize.IHasKindFrom                           (jnode)
            | "IHasSemantics"                       -> Aas.Jsonization.Deserialize.IHasSemanticsFrom                      (jnode)
            | "IIdentifiable"                       -> Aas.Jsonization.Deserialize.IIdentifiableFrom                      (jnode)
            | "IQualifiable"                        -> Aas.Jsonization.Deserialize.IQualifiableFrom                       (jnode)
            | "IReferable"                          -> Aas.Jsonization.Deserialize.IReferableFrom                         (jnode)
            | "IRelationshipElement"                -> Aas.Jsonization.Deserialize.IRelationshipElementFrom               (jnode)
            | "ISubmodelElement"                    -> Aas.Jsonization.Deserialize.ISubmodelElementFrom                   (jnode)
            | "Key"                                 -> Aas.Jsonization.Deserialize.KeyFrom                                (jnode)
            | "LangStringDefinitionTypeIec61360"    -> Aas.Jsonization.Deserialize.LangStringDefinitionTypeIec61360From   (jnode)
            | "LangStringNameType"                  -> Aas.Jsonization.Deserialize.LangStringNameTypeFrom                 (jnode)
            | "LangStringPreferredNameTypeIec61360" -> Aas.Jsonization.Deserialize.LangStringPreferredNameTypeIec61360From(jnode)
            | "LangStringShortNameTypeIec61360"     -> Aas.Jsonization.Deserialize.LangStringShortNameTypeIec61360From    (jnode)
            | "LangStringTextType"                  -> Aas.Jsonization.Deserialize.LangStringTextTypeFrom                 (jnode)
            | "LevelType"                           -> Aas.Jsonization.Deserialize.LevelTypeFrom                          (jnode)
            | "MultiLanguageProperty"               -> Aas.Jsonization.Deserialize.MultiLanguagePropertyFrom              (jnode)
            | "Operation"                           -> Aas.Jsonization.Deserialize.OperationFrom                          (jnode)
            | "OperationVariable"                   -> Aas.Jsonization.Deserialize.OperationVariableFrom                  (jnode)
            | "Property"                            -> Aas.Jsonization.Deserialize.PropertyFrom                           (jnode)
            | "Qualifier"                           -> Aas.Jsonization.Deserialize.QualifierFrom                          (jnode)
            | "Range"                               -> Aas.Jsonization.Deserialize.RangeFrom                              (jnode)
            | "Reference"                           -> Aas.Jsonization.Deserialize.ReferenceFrom                          (jnode)
            | "ReferenceElement"                    -> Aas.Jsonization.Deserialize.ReferenceElementFrom                   (jnode)
            | "RelationshipElement"                 -> Aas.Jsonization.Deserialize.RelationshipElementFrom                (jnode)
            | "Resource"                            -> Aas.Jsonization.Deserialize.ResourceFrom                           (jnode)
            | "SpecificAssetId"                     -> Aas.Jsonization.Deserialize.SpecificAssetIdFrom                    (jnode)
            | "Submodel"                            -> Aas.Jsonization.Deserialize.SubmodelFrom                           (jnode)
            | "SubmodelElementCollection"           -> Aas.Jsonization.Deserialize.SubmodelElementCollectionFrom          (jnode)
            | "SubmodelElementList"                 -> Aas.Jsonization.Deserialize.SubmodelElementListFrom                (jnode)
            | "ValueList"                           -> Aas.Jsonization.Deserialize.ValueListFrom                          (jnode)
            | "ValueReferencePair"                  -> Aas.Jsonization.Deserialize.ValueReferencePairFrom                 (jnode)

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



        /// Json string 을 aas core 의 IClass subtype 객체로 변환
        static member CreateIClassFromXml<'T when 'T :> Aas.IClass>(xml:string) : 'T = J.createIClassFromXml<'T> xml :?> 'T

        static member private createIClassFromXml<'T>(xml:string): Aas.IClass =
            use stringReader = new System.IO.StringReader(xml)
            use xmlReader = System.Xml.XmlReader.Create(stringReader);
            // This step is necessary to skip the non-content. Otherwise,
            // the deserialization would have thrown an exception.
            xmlReader.MoveToContent() |> ignore

            match typeof<'T>.Name with
            | "AdministrativeInformation"           -> Aas.Xmlization.Deserialize.AdministrativeInformationFrom          (xmlReader)
            | "AnnotatedRelationshipElement"        -> Aas.Xmlization.Deserialize.AnnotatedRelationshipElementFrom       (xmlReader)
            | "AssetAdministrationShell"            -> Aas.Xmlization.Deserialize.AssetAdministrationShellFrom           (xmlReader)
            | "AssetInformation"                    -> Aas.Xmlization.Deserialize.AssetInformationFrom                   (xmlReader)
            | "BasicEventElement"                   -> Aas.Xmlization.Deserialize.BasicEventElementFrom                  (xmlReader)
            | "Blob"                                -> Aas.Xmlization.Deserialize.BlobFrom                               (xmlReader)
            | "Capability"                          -> Aas.Xmlization.Deserialize.CapabilityFrom                         (xmlReader)
            | "ConceptDescription"                  -> Aas.Xmlization.Deserialize.ConceptDescriptionFrom                 (xmlReader)
            | "DataSpecificationIec61360"           -> Aas.Xmlization.Deserialize.DataSpecificationIec61360From          (xmlReader)
            | "EmbeddedDataSpecification"           -> Aas.Xmlization.Deserialize.EmbeddedDataSpecificationFrom          (xmlReader)
            | "Entity"                              -> Aas.Xmlization.Deserialize.EntityFrom                             (xmlReader)
            | "Environment"                         -> Aas.Xmlization.Deserialize.EnvironmentFrom                        (xmlReader)
            | "EventPayload"                        -> Aas.Xmlization.Deserialize.EventPayloadFrom                       (xmlReader)
            | "Extension"                           -> Aas.Xmlization.Deserialize.ExtensionFrom                          (xmlReader)
            | "File"                                -> Aas.Xmlization.Deserialize.FileFrom                               (xmlReader)
            | "IAbstractLangString"                 -> Aas.Xmlization.Deserialize.IAbstractLangStringFrom                (xmlReader)
            | "IDataElement"                        -> Aas.Xmlization.Deserialize.IDataElementFrom                       (xmlReader)
            | "IDataSpecificationContent"           -> Aas.Xmlization.Deserialize.IDataSpecificationContentFrom          (xmlReader)
            | "IEventElement"                       -> Aas.Xmlization.Deserialize.IEventElementFrom                      (xmlReader)
            | "IHasDataSpecification"               -> Aas.Xmlization.Deserialize.IHasDataSpecificationFrom              (xmlReader)
            | "IHasExtensions"                      -> Aas.Xmlization.Deserialize.IHasExtensionsFrom                     (xmlReader)
            | "IHasKind"                            -> Aas.Xmlization.Deserialize.IHasKindFrom                           (xmlReader)
            | "IHasSemantics"                       -> Aas.Xmlization.Deserialize.IHasSemanticsFrom                      (xmlReader)
            | "IIdentifiable"                       -> Aas.Xmlization.Deserialize.IIdentifiableFrom                      (xmlReader)
            | "IQualifiable"                        -> Aas.Xmlization.Deserialize.IQualifiableFrom                       (xmlReader)
            | "IReferable"                          -> Aas.Xmlization.Deserialize.IReferableFrom                         (xmlReader)
            | "IRelationshipElement"                -> Aas.Xmlization.Deserialize.IRelationshipElementFrom               (xmlReader)
            | "ISubmodelElement"                    -> Aas.Xmlization.Deserialize.ISubmodelElementFrom                   (xmlReader)
            | "Key"                                 -> Aas.Xmlization.Deserialize.KeyFrom                                (xmlReader)
            | "LangStringDefinitionTypeIec61360"    -> Aas.Xmlization.Deserialize.LangStringDefinitionTypeIec61360From   (xmlReader)
            | "LangStringNameType"                  -> Aas.Xmlization.Deserialize.LangStringNameTypeFrom                 (xmlReader)
            | "LangStringPreferredNameTypeIec61360" -> Aas.Xmlization.Deserialize.LangStringPreferredNameTypeIec61360From(xmlReader)
            | "LangStringShortNameTypeIec61360"     -> Aas.Xmlization.Deserialize.LangStringShortNameTypeIec61360From    (xmlReader)
            | "LangStringTextType"                  -> Aas.Xmlization.Deserialize.LangStringTextTypeFrom                 (xmlReader)
            | "LevelType"                           -> Aas.Xmlization.Deserialize.LevelTypeFrom                          (xmlReader)
            | "MultiLanguageProperty"               -> Aas.Xmlization.Deserialize.MultiLanguagePropertyFrom              (xmlReader)
            | "Operation"                           -> Aas.Xmlization.Deserialize.OperationFrom                          (xmlReader)
            | "OperationVariable"                   -> Aas.Xmlization.Deserialize.OperationVariableFrom                  (xmlReader)
            | "Property"                            -> Aas.Xmlization.Deserialize.PropertyFrom                           (xmlReader)
            | "Qualifier"                           -> Aas.Xmlization.Deserialize.QualifierFrom                          (xmlReader)
            | "Range"                               -> Aas.Xmlization.Deserialize.RangeFrom                              (xmlReader)
            | "Reference"                           -> Aas.Xmlization.Deserialize.ReferenceFrom                          (xmlReader)
            | "ReferenceElement"                    -> Aas.Xmlization.Deserialize.ReferenceElementFrom                   (xmlReader)
            | "RelationshipElement"                 -> Aas.Xmlization.Deserialize.RelationshipElementFrom                (xmlReader)
            | "Resource"                            -> Aas.Xmlization.Deserialize.ResourceFrom                           (xmlReader)
            | "SpecificAssetId"                     -> Aas.Xmlization.Deserialize.SpecificAssetIdFrom                    (xmlReader)
            | "Submodel"                            -> Aas.Xmlization.Deserialize.SubmodelFrom                           (xmlReader)
            | "SubmodelElementCollection"           -> Aas.Xmlization.Deserialize.SubmodelElementCollectionFrom          (xmlReader)
            | "SubmodelElementList"                 -> Aas.Xmlization.Deserialize.SubmodelElementListFrom                (xmlReader)
            | "ValueList"                           -> Aas.Xmlization.Deserialize.ValueListFrom                          (xmlReader)
            | "ValueReferencePair"                  -> Aas.Xmlization.Deserialize.ValueReferencePairFrom                 (xmlReader)

            | _ -> failwithf "Not supported type: %A" typeof<'T>.Name

