namespace rec Dual.Ev2.Aas

(* Core 를 AAS Json/Xml 로 변환하기 위한 실제 코드 *)


open Dual.Common.Core.FS
open Dual.Common.Base
open Ev2.Core.FS
open Newtonsoft.Json

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
                    if isItNotNull prj.Database then
                        JObj().TrySetProperty(prj.Database.ToString(), nameof prj.Database)
                    JObj().TrySetProperty(prj.Description,         nameof prj.Description)
                    JObj().TrySetProperty(prj.Author,              nameof prj.Author)
                    JObj().TrySetProperty(prj.Version.ToString(),  nameof prj.Version)
                    JObj().TrySetProperty(prj.DateTime,            nameof prj.DateTime)

                | :? NjSystem as sys ->
                    JObj().TrySetProperty(sys.IRI,                      nameof sys.IRI)
                    JObj().TrySetProperty(sys.Author,                   nameof sys.Author)
                    JObj().TrySetProperty(sys.EngineVersion.ToString(), nameof sys.EngineVersion)
                    JObj().TrySetProperty(sys.LangVersion.ToString(),   nameof sys.LangVersion)
                    JObj().TrySetProperty(sys.Description,              nameof sys.Description)
                    JObj().TrySetProperty(sys.DateTime,                 nameof sys.DateTime)

                | :? NjApiCall as apiCall ->
                    JObj().TrySetProperty(apiCall.ApiDef,     nameof apiCall.ApiDef)       // Guid
                    JObj().TrySetProperty(apiCall.InAddress,  nameof apiCall.InAddress)
                    JObj().TrySetProperty(apiCall.OutAddress, nameof apiCall.OutAddress)
                    JObj().TrySetProperty(apiCall.InSymbol,   nameof apiCall.InSymbol)
                    JObj().TrySetProperty(apiCall.OutSymbol,  nameof apiCall.OutSymbol)
                    JObj().TrySetProperty(apiCall.ValueSpec,  nameof apiCall.ValueSpec)
                    // IOTags 직렬화
                    let ioTagsStr = if box apiCall.IOTags = null then null else JsonConvert.SerializeObject(apiCall.IOTags)
                    JObj().TrySetProperty(ioTagsStr, nameof apiCall.IOTags)

                | :? NjCall as call ->
                    JObj().TrySetProperty(call.IsDisabled,       nameof call.IsDisabled)
                    // JSON 문자열로 변환하여 AASX에 저장
                    let commonConditionsStr = if call.CommonConditionsObj.Count = 0 then null else call.CommonConditionsObj.ToJson()
                    let autoConditionsStr   = if call.AutoConditionsObj.Count = 0 then null else call.AutoConditionsObj.ToJson()
                    JObj().TrySetProperty(commonConditionsStr, nameof call.CommonConditions)
                    JObj().TrySetProperty(autoConditionsStr,   nameof call.AutoConditions)
                    if call.Timeout.IsSome then
                        JObj().TrySetProperty(call.Timeout.Value,     nameof call.Timeout)
                    JObj().TrySetProperty(call.CallType.ToString(),   nameof call.CallType)
                    JObj().TrySetProperty(sprintf "%A" call.ApiCalls, nameof call.ApiCalls)      // Guid[] type
                    if call.Status.NonNullAny() then
                        JObj().TrySetProperty(call.Status, nameof call.Status)

                | :? NjArrow as arrow ->
                    JObj().TrySetProperty(arrow.Source, nameof arrow.Source)
                    JObj().TrySetProperty(arrow.Target, nameof arrow.Target)
                    JObj().TrySetProperty(arrow.Type,   nameof arrow.Type)

                | :? NjWork as work ->
                    JObj().TrySetProperty(work.FlowGuid,     nameof work.FlowGuid)
                    JObj().TrySetProperty(work.Motion,       nameof work.Motion)
                    JObj().TrySetProperty(work.Script,       nameof work.Script)
                    JObj().TrySetProperty(work.ExternalStart, nameof work.ExternalStart)
                    JObj().TrySetProperty(work.IsFinished,   nameof work.IsFinished)
                    JObj().TrySetProperty(work.NumRepeat,  nameof work.NumRepeat)
                    JObj().TrySetProperty(work.Period,     nameof work.Period)
                    JObj().TrySetProperty(work.Delay,      nameof work.Delay)
                    if work.Status.NonNullAny() then
                        JObj().TrySetProperty(work.Status, nameof work.Status)

                | :? NjApiDef as apiDef ->
                    JObj().TrySetProperty(apiDef.IsPush,   nameof apiDef.IsPush)
                    JObj().TrySetProperty(apiDef.TxGuid,   nameof apiDef.TxGuid)
                    JObj().TrySetProperty(apiDef.RxGuid,   nameof apiDef.RxGuid)

                | (:? NjButton as btn) ->
                    JObj().TrySetProperty(btn.FlowGuid, nameof btn.FlowGuid)
                    // IOTags 직렬화
                    let ioTagsStr = if box btn.IOTags = null then null else JsonConvert.SerializeObject(btn.IOTags)
                    JObj().TrySetProperty(ioTagsStr, nameof btn.IOTags)
                | (:? NjLamp as lamp) ->
                    JObj().TrySetProperty(lamp.FlowGuid, nameof lamp.FlowGuid)
                    // IOTags 직렬화
                    let ioTagsStr = if box lamp.IOTags = null then null else JsonConvert.SerializeObject(lamp.IOTags)
                    JObj().TrySetProperty(ioTagsStr, nameof lamp.IOTags)
                | (:? NjCondition as cond) ->
                    JObj().TrySetProperty(cond.FlowGuid, nameof cond.FlowGuid)
                    // IOTags 직렬화
                    let ioTagsStr = if box cond.IOTags = null then null else JsonConvert.SerializeObject(cond.IOTags)
                    JObj().TrySetProperty(ioTagsStr, nameof cond.IOTags)
                | (:? NjAction as act) ->
                    JObj().TrySetProperty(act.FlowGuid, nameof act.FlowGuid)
                    // IOTags 직렬화
                    let ioTagsStr = if box act.IOTags = null then null else JsonConvert.SerializeObject(act.IOTags)
                    JObj().TrySetProperty(ioTagsStr, nameof act.IOTags)
                | (:? NjFlow) ->
                    ()
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

            // UI 요소들 추가
            let btns = x.Buttons |-> _.ToSjSMC()
            let buttons =
                JObj().AddProperties(
                    modelType = A.smc
                    , values = btns
                    , semanticKey = nameof x.Buttons
                )

            let lmps = x.Lamps |-> _.ToSjSMC()
            let lamps =
                JObj().AddProperties(
                    modelType = A.smc
                    , values = lmps
                    , semanticKey = nameof x.Lamps
                )

            let conds = x.Conditions |-> _.ToSjSMC()
            let conditions =
                JObj().AddProperties(
                    modelType = A.smc
                    , values = conds
                    , semanticKey = nameof x.Conditions
                )

            let acts = x.Actions |-> _.ToSjSMC()
            let actions =
                JObj().AddProperties(
                    modelType = A.smc
                    , values = acts
                    , semanticKey = nameof x.Actions
                )

            let me = x
            JObj().ToSjSMC("System", x.CollectProperties())
            |> _.AddValues([| apiDefs; apiCalls; flows; arrows; works; buttons; lamps; conditions; actions |])


    type NjApiDef with // ToSjSMC
        /// To [S]ystem [J]son [S]ub[M]odel element [C]llection (SMEC) 형태로 변환
        member x.ToSjSMC(): JNode = JObj().ToSjSMC("ApiDef", x.CollectProperties())

    type NjApiCall with // ToSjSMC
        /// To [S]ystem [J]son [S]ub[M]odel element [C]llection (SMEC) 형태로 변환
        member x.ToSjSMC(): JNode = JObj().ToSjSMC("ApiCall", x.CollectProperties())


    type NjButton with // ToSjSMC
        member x.ToSjSMC(): JNode = JObj().ToSjSMC("Button", x.CollectProperties())

    type NjLamp with // ToSjSMC
        member x.ToSjSMC(): JNode =
            let props = x.CollectProperties()
            JObj().ToSjSMC("Lamp", props)

    type NjCondition with // ToSjSMC
        member x.ToSjSMC(): JNode =
            let props = x.CollectProperties()
            JObj().ToSjSMC("Condition", props)

    type NjAction with // ToSjSMC
        /// To [S]ystem [J]son [S]ub[M]odel element [C]llection (SMEC) 형태로 변환
        member x.ToSjSMC(): JNode =
            let props = x.CollectProperties()
            JObj().ToSjSMC("Action", props)

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
