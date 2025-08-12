namespace rec Dual.Ev2.Aas

(* Core 를 AAS Json/Xml 로 변환하기 위한 실제 코드 *)


open Dual.Common.Core.FS
open Dual.Common.Base
open Ev2.Core.FS

[<AutoOpen>]
module CoreToAas =
    type NjUnique with
        member x.tryCollectPropertiesNjUnique(): JObj option seq =
            seq {
                JObj().TrySetProperty(x.Name,      "Name")
                JObj().TrySetProperty(x.Parameter, "Parameter")
                JObj().TrySetProperty(x.Guid,      "Guid")
                if x.Id.IsSome then
                    JObj().TrySetProperty(x.Id.Value, "Id")
            }

        /// 확장 타입별 특수 속성 수집 (AAS용)
        /// Generic reflection 기반으로 확장 속성을 동적으로 수집
        member x.tryCollectExtensionProperties(): JObj option seq =
            seq {
                // 1. 먼저 가상 메서드 CollectExtensionProperties 호출 (C# 확장 타입 지원)
                try
                    let extensionProps = x.CollectExtensionProperties()


                    for prop in extensionProps do
                        match prop with
                        | :? Newtonsoft.Json.Linq.JProperty as jprop ->
                            // JProperty의 값을 적절한 타입으로 변환하여 저장
                            let value =
                                match jprop.Value.Type with
                                | Newtonsoft.Json.Linq.JTokenType.String -> jprop.Value.ToString() :> obj
                                | Newtonsoft.Json.Linq.JTokenType.Integer -> jprop.Value.ToObject<int>() :> obj
                                | Newtonsoft.Json.Linq.JTokenType.Float -> jprop.Value.ToObject<float>() :> obj
                                | Newtonsoft.Json.Linq.JTokenType.Boolean -> jprop.Value.ToObject<bool>() :> obj
                                | _ -> jprop.Value.ToString() :> obj

                            yield JObj().TrySetProperty(value, jprop.Name)
                        | _ -> ()
                with
                | ex ->
                    eprintfn "[tryCollectExtensionProperties] Error calling CollectExtensionProperties: %s" ex.Message

                // 2. 기존 reflection 기반 로직도 유지 (F# 확장 타입 지원)
                let objType = x.GetType()
                let baseTypeName = objType.BaseType.Name

                // 기본 NjXXX 타입이 아닌 확장 타입만 처리
                if baseTypeName = "NjProject" || baseTypeName = "NjSystem" then
                    let baseType = objType.BaseType
                    let allProps = objType.GetProperties()
                    let basePropNames = baseType.GetProperties() |> Array.map (fun p -> p.Name) |> Set.ofArray

                    // 확장 타입에서만 정의된 속성들 찾기
                    for prop in allProps do
                        if not (basePropNames.Contains(prop.Name)) && prop.CanRead then
                            try
                                let value = prop.GetValue(x)
                                if value <> null then
                                    // 문자열: 빈 값이 아닌 경우만
                                    if prop.PropertyType = typeof<string> &&
                                       not (System.String.IsNullOrEmpty(value :?> string)) then
                                        yield JObj().TrySetProperty(value, prop.Name)
                                    // 숫자: 0이 아닌 경우만 (int)
                                    elif prop.PropertyType = typeof<int> && (value :?> int) <> 0 then
                                        yield JObj().TrySetProperty(value, prop.Name)
                                    // 기타 값 타입들에 대해서도 기본값이 아닌 경우
                                    elif prop.PropertyType.IsValueType &&
                                         not (value.Equals(System.Activator.CreateInstance(prop.PropertyType))) then
                                        yield JObj().TrySetProperty(value, prop.Name)
                            with
                            | ex ->
                                eprintfn "[tryCollectExtensionProperties] Error collecting property '%s' from type '%s': %s"
                                    prop.Name objType.Name ex.Message
            }

        member x.CollectProperties(): JNode[] =
            seq {
                yield! x.tryCollectPropertiesNjUnique()

                // 확장점: 확장 타입별 특별 처리
                yield! x.tryCollectExtensionProperties()

                match x with
                | :? NjProject as prj ->
                    if isItNotNull prj.Database then
                        JObj().TrySetProperty(prj.Database.ToString(), "Database")
                    JObj().TrySetProperty(prj.Description,         "Description")
                    JObj().TrySetProperty(prj.Author,              "Author")
                    JObj().TrySetProperty(prj.Version.ToString(),  "Version")
                    JObj().TrySetProperty(prj.DateTime,            "DateTime")

                | :? NjSystem as sys ->
                    JObj().TrySetProperty(sys.IRI,                      "IRI")
                    JObj().TrySetProperty(sys.Author,                   "Author")
                    JObj().TrySetProperty(sys.EngineVersion.ToString(), "EngineVersion")
                    JObj().TrySetProperty(sys.LangVersion.ToString(),   "LangVersion")
                    JObj().TrySetProperty(sys.Description,              "Description")
                    JObj().TrySetProperty(sys.DateTime,                 "DateTime")

                | :? NjApiCall as apiCall ->
                    JObj().TrySetProperty(apiCall.ApiDef,     "ApiDef")       // Guid
                    JObj().TrySetProperty(apiCall.InAddress,  "InAddress")
                    JObj().TrySetProperty(apiCall.OutAddress, "OutAddress")
                    JObj().TrySetProperty(apiCall.InSymbol,   "InSymbol")
                    JObj().TrySetProperty(apiCall.OutSymbol,  "OutSymbol")
                    JObj().TrySetProperty(apiCall.ValueSpec,  "ValueSpec")

                | :? NjCall as call ->
                    JObj().TrySetProperty(call.IsDisabled,       "IsDisabled")
                    JObj().TrySetProperty(call.CommonConditions, "CommonConditions")
                    JObj().TrySetProperty(call.AutoConditions,   "AutoConditions")
                    if call.Timeout.IsSome then
                        JObj().TrySetProperty(call.Timeout.Value,     "Timeout")
                    JObj().TrySetProperty(call.CallType.ToString(),   "CallType")
                    JObj().TrySetProperty(sprintf "%A" call.ApiCalls, "ApiCalls")      // Guid[] type
                    if call.Status.NonNullAny() then
                        JObj().TrySetProperty(call.Status, "Status")

                | :? NjArrow as arrow ->
                    JObj().TrySetProperty(arrow.Source, "Source")
                    JObj().TrySetProperty(arrow.Target, "Target")
                    JObj().TrySetProperty(arrow.Type,   "Type")

                | :? NjWork as work ->
                    JObj().TrySetProperty(work.FlowGuid,   "FlowGuid")
                    JObj().TrySetProperty(work.Motion,     "Motion")
                    JObj().TrySetProperty(work.Script,     "Script")
                    JObj().TrySetProperty(work.IsFinished, "IsFinished")
                    JObj().TrySetProperty(work.NumRepeat,  "NumRepeat")
                    JObj().TrySetProperty(work.Period,     "Period")
                    JObj().TrySetProperty(work.Delay,      "Delay")
                    if work.Status.NonNullAny() then
                        JObj().TrySetProperty(work.Status, "Status")

                | :? NjApiDef as apiDef ->
                    JObj().TrySetProperty(apiDef.IsPush,   "IsPush")
                    match apiDef.TopicIndex, apiDef.IsTopicOrigin with
                    | Some topicIndex, isOrigin ->
                        JObj().TrySetProperty(topicIndex, "TopicIndex")
                        JObj().TrySetProperty(isOrigin,   "IsTopicOrigin")
                    | None, None -> ()
                    | _ -> failwith "ERROR: TopicIndex and IsTopicOrigin must be both None or both Some"

                | (:? NjButton) | (:? NjLamp) | (:? NjCondition) | (:? NjAction) ->
                    ()
                | (:? NjFlow) ->
                    ()
                | xxx ->
                    failwith "ERROR"



            } |> choose id |> Seq.cast<JNode> |> toArray

    type NjProject with // ToSjSMC, ToSjSubmodel
        /// To [S]ystem [J]son [S]ub[M]odel element [C]llection (SMEC) 형태로 변환
        member x.ToSjSMC(): JNode =
            let me = x
            let actives = x.ActiveSystems |-> _.ToSjSMC()
            let activeSystems =
                JObj().AddProperties(
                    modelType = A.smc
                    , values = actives
                    , semanticKey = "ActiveSystems"
                )


            let passives = x.PassiveSystems |-> _.ToSjSMC()
            let passiveSystems =
                JObj().AddProperties(
                    modelType = A.smc
                    , values = passives
                    , semanticKey = "PassiveSystems"
                )

            let project =
                JObj().ToSjSMC("Project", x.CollectProperties())
                |> _.AddValues([| activeSystems; passiveSystems |])
            project


        /// 확장 속성용 semantic URL 생성
        static member CreateExtensionSemanticUrl(typeName: string, propName: string): string =
            let lowerTypeName = 
                typeName.Split('.')
                |> Array.last 
                |> (fun name -> name.ToLowerInvariant())
            let lowerPropName = propName.ToLowerInvariant()
            sprintf "https://dualsoft.com/aas/extension/%s/%s" lowerTypeName lowerPropName

        /// To [S]ystem [J]son Submodel (SM) 형태로 변환
        member prj.ToSjSubmodel(): JNode =
            // 확장 타입 정보 생성 - AASX에서 타입 복원을 위해 저장
            let extensionTypeInfo =
                JObj().AddProperties(
                    semanticKey = "ExtensionTypeInfo"
                    , value = prj.GetType().FullName  // 확장 타입 이름 저장
                    , modelType = ModelType.Property
                )

            // 확장 속성들을 별도의 Property로 수집
            let extensionProperties =
                try
                    let extensionProps = prj.CollectExtensionProperties()
                    extensionProps
                    |> Array.choose (fun token ->
                        match token with
                        | :? Newtonsoft.Json.Linq.JProperty as jprop ->
                            let propertyNode =
                                JObj()
                                    .Set(N.IdShort, jprop.Name)
                                    .Set(N.ModelType, ModelType.Property.ToString())
                                    .Set(N.ValueType, "xs:string")
                                    .Set(N.Value, jprop.Value.ToString())
                                    .SetSemantic(NjProject.CreateExtensionSemanticUrl(prj.GetType().FullName, jprop.Name))

                            Some(propertyNode :> JNode)
                        | _ ->
                            None
                    )
                with
                | ex ->
                    eprintfn "[ToSjSubmodel] Error collecting extension properties: %s" ex.Message
                    [||]

            // 모든 SubmodelElements 결합
            let allElements =
                [|
                    yield prj.ToSjSMC()
                    yield extensionTypeInfo
                    yield! extensionProperties
                |]


            let sm =
                JObj().AddProperties(
                    category = Category.CONSTANT
                    , modelType = ModelType.Submodel
                    , id = guid2str prj.Guid
                    , idShort = SubmodelIdShort
                    , kind = KindType.Instance
                    , semanticKey = "Submodel"
                    , smel = allElements
                )
            sm



    type NjSystem with
        /// To [S]ystem [J]son [S]ub[M]odel element [C]llection (SMEC) 형태로 변환
        member x.ToSjSMC(): JNode =
            let fs = x.Flows |-> _.ToSjSMC()
            let flows =
                JObj().AddProperties(
                    modelType = A.smc
                    , values = fs
                    , semanticKey = "Flows"
                )

            let ws = x.Works |-> _.ToSjSMC()
            let works =
                JObj().AddProperties(
                    modelType = A.smc
                    , values = ws
                    , semanticKey = "Works"
                )

            let arrs = x.Arrows |-> _.ToSjSMC()
            let arrows =
                JObj().AddProperties(
                    modelType = A.smc
                    , values = arrs
                    , semanticKey = "Arrows"
                )


            let ads = x.ApiDefs |-> _.ToSjSMC()
            let apiDefs =
                JObj().AddProperties(
                    modelType = A.smc
                    , values = ads
                    , semanticKey = "ApiDefs"
                )

            let acs = x.ApiCalls |-> _.ToSjSMC()
            let apiCalls =
                JObj().AddProperties(
                    modelType = A.smc
                    , values = acs
                    , semanticKey = "ApiCalls"
                )


            JObj().ToSjSMC("System", x.CollectProperties())
            |> _.AddValues([| apiDefs; apiCalls; flows; arrows; works |])

        //// To [S]ystem [J]son Submodel element (SME) 형태로 변환
        //[<Obsolete("안씀")>]
        //member sys.ToSjSubmodel(): JNode =
        //    let sm =
        //        JObj().AddProperties(
        //            category = Category.CONSTANT,
        //            modelType = ModelType.Submodel,
        //            id = guid2str sys.Guid,
        //            kind = KindType.Instance,
        //            semanticKey = "FakeSystemSubmodel",
        //            smel = [| sys.ToSjSMC() |]
        //        )
        //    sm


    type NjApiDef with
        /// To [S]ystem [J]son [S]ub[M]odel element [C]llection (SMEC) 형태로 변환
        member x.ToSjSMC(): JNode = JObj().ToSjSMC("ApiDef", x.CollectProperties())

    type NjApiCall with
        /// To [S]ystem [J]son [S]ub[M]odel element [C]llection (SMEC) 형태로 변환
        member x.ToSjSMC(): JNode = JObj().ToSjSMC("ApiCall", x.CollectProperties())


    type NjButton with
        member x.ToSjSMC(): JNode = JObj().ToSjSMC("Button", x.CollectProperties())

    type NjLamp with
        member x.ToSjSMC(): JNode =
            let props = x.CollectProperties()
            JObj().ToSjSMC("Lamp", props)

    type NjCondition with
        member x.ToSjSMC(): JNode =
            let props = x.CollectProperties()
            JObj().ToSjSMC("Condition", props)

    type NjAction with
        /// To [S]ystem [J]son [S]ub[M]odel element [C]llection (SMEC) 형태로 변환
        member x.ToSjSMC(): JNode =
            let props = x.CollectProperties()
            JObj().ToSjSMC("Action", props)

    type NjFlow with    // ToSjSMC
        /// To [S]ystem [J]son [S]ub[M]odel element [C]llection (SMEC) 형태로 변환
        member x.ToSjSMC(): JNode =
            let buttons    = x.Buttons    |-> _.ToSjSMC() |> toSjSMC "Buttons"
            let lamps      = x.Lamps      |-> _.ToSjSMC() |> toSjSMC "Lamps"
            let conditions = x.Conditions |-> _.ToSjSMC() |> toSjSMC "Conditions"
            let actions    = x.Actions    |-> _.ToSjSMC() |> toSjSMC "Actions"

            let props = x.CollectProperties()
            JObj().ToSjSMC("Flow", props)
            |> _.AddValues([|buttons; lamps; conditions; actions|] |> choose id)

    type NjCall with
        /// To [S]ystem [J]son [S]ub[M]odel element [C]llection (SMEC) 형태로 변환
        member x.ToSjSMC(): JNode =
            let me = x
            let props = x.CollectProperties()
            JObj().ToSjSMC("Call", props)

    type NjArrow with
        /// Convert arrow to Aas Jons of SubmodelElementCollection
        member x.ToSjSMC(): JNode =
            let props = x.CollectProperties()
            JObj().ToSjSMC("Arrow", props)

    let toSjSMC (semanticKey:string) (values:JNode[]) =
        match values with
        | [||] -> None
        | _ ->
            JObj()
                .SetSemantic(semanticKey)
                .Set(N.ModelType, ModelType.SubmodelElementCollection.ToString())
                |> _.AddValues(values)
                |> Some

    type NjWork with    // ToSjSMC
        /// To [S]ystem [J]son [S]ub[M]odel element [C]llection (SMEC) 형태로 변환
        member x.ToSjSMC(): JNode =
            let arrows = x.Arrows |-> _.ToSjSMC() |> toSjSMC "Arrows"
            let calls  = x.Calls  |-> _.ToSjSMC() |> toSjSMC "Calls"
            let props = x.CollectProperties()
            JObj().ToSjSMC("Work", props)
            |> _.AddValues([|arrows; calls|] |> choose id)
