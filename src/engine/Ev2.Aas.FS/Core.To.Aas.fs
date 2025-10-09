namespace rec Dual.Ev2.Aas

(* Core 를 AAS Json/Xml 로 변환하기 위한 실제 코드 *)


open Dual.Common.Base
open Ev2.Core.FS

#nowarn FS0044 // obsolete 사용 허용

[<AutoOpen>]
module CoreToAas =
    type NjUnique with // CollectProperties, tryCollectExtensionProperties, tryCollectPropertiesNjUnique
        member x.tryCollectPropertiesNjUnique(): JObj option seq =
            seq {
                JObj().TrySetProperty(x.Name, nameof x.Name)
                JObj().TrySetProperty(x.Guid, nameof x.Guid)

                if (x.Parameter.NonNullAny()) then
                    JObj().TrySetProperty(x.Parameter, nameof x.Parameter)
                if x.Id.IsSome then
                    JObj().TrySetProperty(x.Id.Value, nameof x.Id)
            }

        /// 확장 타입별 특수 속성 수집 (AAS용)
        /// Generic reflection 기반으로 확장 속성을 동적으로 수집
        member x.tryCollectExtensionProperties(): JObj option seq =
            getTypeFactory()
            |-> (fun factory -> factory.WriteAasExtensionProperties x |-> Some)
            |? Seq.empty


        member x.CollectProperties(): JNode[] =
            seq {
                yield! x.tryCollectPropertiesNjUnique()

                // 확장점: 확장 타입별 특별 처리
                yield! x.tryCollectExtensionProperties()

                match x with
                | :? NjProject as prj ->
                    JObj().TrySetProperty(prj.Properties.ToJson(), "Properties")

                | :? NjSystem as sys ->
                    let sp = sys.Properties
                    JObj().TrySetProperty(sys.IRI, nameof sys.IRI)
                    let entitiesJson =
                        let serialized = sys.PolymorphicJsonEntities.SerializedItems
                        if isNull serialized || serialized.Count = 0 then null else serialized.ToString()
                    JObj().TrySetProperty(entitiesJson, "Entities")
                    (* AASX 내에 Properties 에 대한 type 정보 $type 등이 없어도
                       JsonPolymorphic.FromJson<'T>가 직접 createExtended<'T>()를 호출해
                       실제 인스턴스를 먼저 만든 뒤 JsonConvert.PopulateObject로 값을 채우므로 확장 가능.
                     *)
                    JObj().TrySetProperty(sys.Properties.ToJson(), "Properties")

                | :? NjApiCall as apiCall ->
                    JObj().TrySetProperty(apiCall.ApiDef,     nameof apiCall.ApiDef)       // Guid
                    JObj().TrySetProperty(apiCall.Properties.InAddress,  nameof apiCall.Properties.InAddress)
                    JObj().TrySetProperty(apiCall.Properties.OutAddress, nameof apiCall.Properties.OutAddress)
                    JObj().TrySetProperty(apiCall.Properties.InSymbol,   nameof apiCall.Properties.InSymbol)
                    JObj().TrySetProperty(apiCall.Properties.OutSymbol,  nameof apiCall.Properties.OutSymbol)
                    JObj().TrySetProperty(apiCall.ValueSpec,  nameof apiCall.ValueSpec)
                    // IOTags 직렬화
                    let ioTagsStr = IOTagsWithSpec.Jsonize apiCall.IOTags
                    JObj().TrySetProperty(ioTagsStr, nameof apiCall.IOTags)
                    JObj().TrySetProperty(apiCall.Properties.ToJson(), "Properties")

                | :? NjCall as call ->
                    //JObj().TrySetProperty(call.IsDisabled,       nameof call.IsDisabled)
                    // JSON 문자열로 변환하여 AASX에 저장
                    let commonConditionsStr = if call.CommonConditionsObj.Count = 0 then null else call.CommonConditionsObj.ToJson()
                    let autoConditionsStr   = if call.AutoConditionsObj.Count = 0 then null else call.AutoConditionsObj.ToJson()
                    JObj().TrySetProperty(commonConditionsStr, nameof call.CommonConditions)
                    JObj().TrySetProperty(autoConditionsStr,   nameof call.AutoConditions)

                    //JObj().TrySetProperty(sprintf "%A" call.ApiCalls, nameof call.ApiCalls)      // Guid[] type
                    JObj().TrySetProperty(call.Status, nameof call.Status)

                    JObj().TrySetProperty(call.Properties.ToJson(), "Properties")

                | :? NjArrow as arrow ->
                    JObj().TrySetProperty(arrow.Source, nameof arrow.Source)
                    JObj().TrySetProperty(arrow.Target, nameof arrow.Target)
                    JObj().TrySetProperty(arrow.Type,   nameof arrow.Type)

                | :? NjWork as work ->
                    JObj().TrySetProperty(work.FlowGuid,     nameof work.FlowGuid)
                    JObj().TrySetProperty(work.Status, nameof work.Status)
                    JObj().TrySetProperty(work.Properties.ToJson(), "Properties")

                | :? NjApiDef as apiDef ->
                    JObj().TrySetProperty(apiDef.Properties.ToJson(), "Properties")

                | :? NjFlow as flow ->
                    JObj().TrySetProperty(flow.Properties.ToJson(), "Properties")
                | unknown ->
                    failwith $"ERROR: Unknown type {unknown.GetType().Name}"

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
                    , semanticKey = nameof x.ActiveSystems
                )


            let passives = x.PassiveSystems |-> _.ToSjSMC()
            let passiveSystems =
                JObj().AddProperties(
                    modelType = A.smc
                    , values = passives
                    , semanticKey = nameof x.PassiveSystems
                )

            let project =
                JObj().ToSjSMC("Project", x.CollectProperties())
                |> _.AddValues([| activeSystems; passiveSystems |])
            project

        /// To [S]ystem [J]son Submodel (SM) 형태로 변환
        member prj.ToSjSubmodel(): JNode =
            let sm =
                JObj().AddProperties(
                    category = Category.CONSTANT
                    , modelType = ModelType.Submodel
                    , id = guid2str prj.Guid
                    , idShort = SubmodelIdShort
                    , kind = KindType.Instance
                    , semanticKey = "Submodel"
                    , smel = [| prj.ToSjSMC() |]
                )
            sm



    type NjSystem with // ToSjSMC
        /// To [S]ystem [J]son [S]ub[M]odel element [C]llection (SMEC) 형태로 변환
        member x.ToSjSMC(): JNode =
            let fs = x.Flows |-> _.ToSjSMC()
            let flows =
                JObj().AddProperties(
                    modelType = A.smc
                    , values = fs
                    , semanticKey = nameof x.Flows
                )

            let ws = x.Works |-> _.ToSjSMC()
            let works =
                JObj().AddProperties(
                    modelType = A.smc
                    , values = ws
                    , semanticKey = nameof x.Works
                )

            let arrs = x.Arrows |-> _.ToSjSMC()
            let arrows =
                JObj().AddProperties(
                    modelType = A.smc
                    , values = arrs
                    , semanticKey = nameof x.Arrows
                )


            let ads = x.ApiDefs |-> _.ToSjSMC()
            let apiDefs =
                JObj().AddProperties(
                    modelType = A.smc
                    , values = ads
                    , semanticKey = nameof x.ApiDefs
                )

            let acs = x.ApiCalls |-> _.ToSjSMC()
            let apiCalls =
                JObj().AddProperties(
                    modelType = A.smc
                    , values = acs
                    , semanticKey = nameof x.ApiCalls
                )

            let me = x
            JObj().ToSjSMC("System", x.CollectProperties())
            |> _.AddValues([| apiDefs; apiCalls; flows; arrows; works; |])


    type NjApiDef with // ToSjSMC
        /// To [S]ystem [J]son [S]ub[M]odel element [C]llection (SMEC) 형태로 변환
        member x.ToSjSMC(): JNode = JObj().ToSjSMC("ApiDef", x.CollectProperties())

    type NjApiCall with // ToSjSMC
        /// To [S]ystem [J]son [S]ub[M]odel element [C]llection (SMEC) 형태로 변환
        member x.ToSjSMC(): JNode = JObj().ToSjSMC("ApiCall", x.CollectProperties())


    type NjFlow with // ToSjSMC
        /// To [S]ystem [J]son [S]ub[M]odel element [C]llection (SMEC) 형태로 변환
        member x.ToSjSMC(): JNode =
            // Flow는 이제 UI 요소를 직접 소유하지 않음
            let props = x.CollectProperties()
            JObj().ToSjSMC("Flow", props)

    type NjCall with // ToSjSMC
        /// To [S]ystem [J]son [S]ub[M]odel element [C]llection (SMEC) 형태로 변환
        member x.ToSjSMC(): JNode =
            let me = x
            let props = x.CollectProperties()
            JObj().ToSjSMC("Call", props)

    type NjArrow with // ToSjSMC
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

    type NjWork with // ToSjSMC
        /// To [S]ystem [J]son [S]ub[M]odel element [C]llection (SMEC) 형태로 변환
        member x.ToSjSMC(): JNode =
            let arrows = x.Arrows |-> _.ToSjSMC() |> toSjSMC (nameof x.Arrows)
            let calls  = x.Calls  |-> _.ToSjSMC() |> toSjSMC (nameof x.Calls)
            let props = x.CollectProperties()
            JObj().ToSjSMC("Work", props)
            |> _.AddValues([|arrows; calls|] |> choose id)
