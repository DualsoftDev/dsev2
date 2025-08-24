namespace rec Dual.Ev2.Aas

(* AAS Json/Xml 로부터 Core 를 생성하기 위한 코드 *)

open System

open AasCore.Aas3_0

open Dual.Common.Core.FS
open Dual.Common.Db.FS
open Dual.Common.Base

open Ev2.Core.FS

[<AutoOpen>]
module CoreFromAas =

    ///// 확장 속성 semantic URL 파싱
    //let tryParseExtensionSemanticUrl (semanticUrl: string) =
    //    let pattern = @"https://dualsoft\.com/aas/extension/([^/]+)/([^/]+)"
    //    let regex = System.Text.RegularExpressions.Regex(pattern)
    //    let match' = regex.Match(semanticUrl)
    //    if match'.Success then
    //        Some (match'.Groups.[1].Value, match'.Groups.[2].Value) // (typeName, propName)
    //    else None

    ///// 확장 속성 여부 확인
    //let isExtensionProperty (semanticUrl: string) =
    //    semanticUrl.Contains("/extension/")

    ///// 확장 속성 수집
    //let collectExtensionProperties (submodel: ISubmodel) =
    //    submodel.SubmodelElements
    //    |> Seq.choose (fun elem ->
    //        match elem with
    //        | :? Property as prop ->
    //            match prop.SemanticId with
    //            | null -> None
    //            | semanticId when semanticId.Keys.Count > 0 ->
    //                let keyValue = semanticId.Keys.[0].Value
    //                tryParseExtensionSemanticUrl keyValue
    //                |> Option.map (fun (typeName, propName) -> (propName, prop.Value))
    //            | _ -> None
    //        | _ -> None)
    //    |> Seq.toArray

    ///// 확장 속성을 객체에 적용
    //let applyExtensionProperties (project: NjProject) (extensionProps: (string * string)[]) =
    //    for (propName, propValue) in extensionProps do
    //        let propInfo = project.GetType().GetProperty(propName)
    //        if propInfo <> null && propInfo.CanWrite then
    //            // 타입에 따른 변환 처리
    //            let convertedValue =
    //                if propInfo.PropertyType = typeof<string> then
    //                    propValue :> obj
    //                elif propInfo.PropertyType = typeof<int> then
    //                    System.Int32.Parse(propValue) :> obj
    //                elif propInfo.PropertyType = typeof<bool> then
    //                    System.Boolean.Parse(propValue) :> obj
    //                elif propInfo.PropertyType = typeof<float> then
    //                    System.Double.Parse(propValue) :> obj
    //                else
    //                    propValue :> obj // 기본적으로 문자열로 처리

    //            propInfo.SetValue(project, convertedValue)

    ///// AASX에서 확장 타입 정보 추출 (ExtensionTypeInfo semantic으로 저장된 타입 이름)
    //let tryGetExtensionTypeInfo (submodel: ISubmodel): string option =
    //    submodel.SubmodelElements
    //    |> Seq.tryFind (fun elem ->
    //        match elem with
    //        | :? Property as prop ->
    //            match prop.SemanticId with
    //            | null -> false
    //            | semanticId when semanticId.Keys.Count > 0 ->
    //                let keyValue = semanticId.Keys.[0].Value
    //                keyValue = AasSemantics.map.["ExtensionTypeInfo"]
    //            | _ -> false
    //        | _ -> false)
    //    |> Option.bind (fun elem ->
    //        match elem with
    //        | :? Property as prop -> Some prop.Value
    //        | _ -> None)

    // 공통 FromSMC 헬퍼 함수 - UniqueInfo만 필요한 단순한 객체들을 위함
    let internal createSimpleFromSMC<'T when 'T :> Unique> (constructor: unit -> 'T)
                                                           (smc: SubmodelElementCollection) : 'T =
        let { Name=name; Guid=guid; Parameter=parameter; Id=id } = smc.ReadUniqueInfo()
        let obj = constructor()
        obj.Name <- name
        obj.Guid <- guid
        obj.Id <- id
        obj.Parameter <- parameter
        obj

    let internal readAasExtensionProperties (smc:SubmodelElementCollection) (njObj:INjUnique)  =
        getTypeFactory() |-> (fun factory -> factory.ReadAasExtensionProperties(njObj, smc))

    type NjProject with // FromAasxFile, FromISubmodel
        static member FromAasxFile(aasxPath: string): NjProject =
            let aasFileInfo = AasXModule.readEnvironmentFromAasx aasxPath
            let env = aasFileInfo.Environment

            let projectSubmodel =
                env.Submodels
                |> Seq.tryFind (fun sm -> sm.IdShort = PreludeModule.SubmodelIdShort)
                |> function
                    | Some sm -> sm
                    | None -> failwith $"Project Submodel with IdShort '{PreludeModule.SubmodelIdShort}' not found in AASX file: {aasxPath}"

            // 확장 타입 정보를 사용하여 적절한 NjProject 생성
            let project = NjProject.FromISubmodel(projectSubmodel)
                //match tryGetExtensionTypeInfo projectSubmodel, getTypeFactory() with
                //| Some typeName, Some factory ->
                //    // 확장 타입 정보가 있고 TypeFactory가 등록된 경우
                //    match factory.FindNjTypeByName(typeName) with
                //    | null ->
                //        // 확장 타입을 찾을 수 없으면 기본 타입 사용
                //        NjProject.FromISubmodel(projectSubmodel)
                //    | extType ->
                //        // 확장 타입 인스턴스 생성 및 데이터 로드
                //        let extInstance = System.Activator.CreateInstance(extType) :?> NjProject
                //        let baseProject = NjProject.FromISubmodel(projectSubmodel)
                //        // 기본 프로젝트 데이터를 확장 인스턴스에 복사
                //        extInstance.Name <- baseProject.Name
                //        extInstance.Guid <- baseProject.Guid
                //        extInstance.Id <- baseProject.Id
                //        extInstance.Parameter <- baseProject.Parameter
                //        extInstance.DateTime <- baseProject.DateTime
                //        extInstance.Database <- baseProject.Database
                //        extInstance.Author <- baseProject.Author
                //        extInstance.Version <- baseProject.Version
                //        extInstance.Description <- baseProject.Description
                //        extInstance.ActiveSystems <- baseProject.ActiveSystems
                //        extInstance.PassiveSystems <- baseProject.PassiveSystems

                //        // 확장 속성 복원
                //        let extensionProps = collectExtensionProperties projectSubmodel
                //        applyExtensionProperties extInstance extensionProps

                //        extInstance
                //| _, _ ->
                //    // 확장 타입 정보가 없거나 TypeFactory가 없으면 기본 동작
                //    let baseProject = NjProject.FromISubmodel(projectSubmodel)
                //    // 기본 타입의 경우에도 확장 속성이 있으면 복원 시도
                //    let extensionProps = collectExtensionProperties projectSubmodel
                //    if extensionProps.Length > 0 then
                //        applyExtensionProperties baseProject extensionProps
                //    baseProject

            project

        static member FromISubmodel(submodel:ISubmodel): NjProject =
            let project = submodel.GetSMCWithSemanticKey "Project" |> head
            let { Name=name; Guid=guid; Parameter=parameter; Id=id } = project.ReadUniqueInfo()

            let database    = project.TryGetPropValue "Database"    >>= DU.tryParse<DbProvider> |? Prelude.getNull<DbProvider>()
            let dateTime    = project.GetPropValue    "DateTime"    |> DateTime.Parse
            let author      = project.TryGetPropValue "Author"      |? null
            let description = project.TryGetPropValue "Description" |? null
            let version     = project.TryGetPropValue "Version"     |-> Version.Parse |? Version(0, 0)

            let activeSystems   = project.GetSMC "ActiveSystems"  >>= (_.GetSMC("System")) |-> NjSystem.FromSMC
            let passiveSystems  = project.GetSMC "PassiveSystems" >>= (_.GetSMC("System")) |-> NjSystem.FromSMC

            NjProject.Create(
                Name=name, Guid=guid, Id=id, Parameter=parameter

                , DateTime       = dateTime
                , Database       = database
                , Author         = author
                , Version        = version
                , Description    = description
                , ActiveSystems  = activeSystems
                , PassiveSystems = passiveSystems)
            |> tee (readAasExtensionProperties project)



    type NjSystem with // FromSMC
        static member FromSMC(smc: SubmodelElementCollection): NjSystem =
            let { Name=name; Guid=guid; Parameter=parameter; Id=id } = smc.ReadUniqueInfo()
            let dateTime      = smc.GetPropValue "DateTime"  |> DateTime.Parse
            let iri           = smc.TryGetPropValue "IRI" |?? (fun () -> logWarn $"No IRI on system {name}"; null)
            let engineVersion = smc.TryGetPropValue "EngineVersion" |-> Version.Parse |? Version(0, 0)
            let langVersion   = smc.TryGetPropValue "LangVersion"   |-> Version.Parse |? Version(0, 0)
            let author        = smc.TryGetPropValue "Author" |? null
            let description   = smc.TryGetPropValue "Description" |? null


            let apiDefs  = smc.GetSMC "ApiDefs"  >>= (_.GetSMC("ApiDef"))  |-> NjApiDef.FromSMC
            let apiCalls = smc.GetSMC "ApiCalls" >>= (_.GetSMC("ApiCall")) |-> NjApiCall.FromSMC
            let works    = smc.GetSMC "Works"    >>= (_.GetSMC("Work"))    |-> NjWork.FromSMC
            let flows    = smc.GetSMC "Flows"    >>= (_.GetSMC("Flow"))    |-> NjFlow.FromSMC
            let arrows   = smc.GetSMC "Arrows"   >>= (_.GetSMC("Arrow"))   |-> NjArrow.FromSMC

            NjSystem.Create(
                Name=name, Guid=guid, Id=id, Parameter=parameter

                , DateTime = dateTime
                , IRI = iri
                , Author = author
                , EngineVersion = engineVersion
                , LangVersion = langVersion
                , Description = description

                , Flows = flows
                , Works = works
                , Arrows = arrows
                , ApiDefs = apiDefs
                , ApiCalls = apiCalls)
            |> tee (readAasExtensionProperties smc)

    type NjArrow with // FromSMC
        static member FromSMC(smc: SubmodelElementCollection): NjArrow =
            let { Name=name; Guid=guid; Parameter=parameter; Id=id } = smc.ReadUniqueInfo()
            let src = smc.GetPropValue "Source"
            let tgt = smc.GetPropValue "Target"
            let typ = smc.GetPropValue "Type"
            NjArrow.Create(Name=name, Guid=guid, Id=id, Parameter=parameter, Source=src, Target=tgt, Type=typ)
            |> tee (readAasExtensionProperties smc)


    type NjButton with // FromSMC
        static member FromSMC(smc: SubmodelElementCollection): NjButton =
            createSimpleFromSMC (fun () -> NjButton.Create()) smc
            |> tee (readAasExtensionProperties smc)

    type NjLamp with // FromSMC
        static member FromSMC(smc: SubmodelElementCollection): NjLamp =
            createSimpleFromSMC (fun () -> NjLamp.Create()) smc
            |> tee (readAasExtensionProperties smc)

    type NjCondition with // FromSMC
        static member FromSMC(smc: SubmodelElementCollection): NjCondition =
            createSimpleFromSMC (fun () -> NjCondition.Create()) smc
            |> tee (readAasExtensionProperties smc)

    type NjAction with // FromSMC
        static member FromSMC(smc: SubmodelElementCollection): NjAction =
            createSimpleFromSMC (fun () -> NjAction.Create()) smc
            |> tee (readAasExtensionProperties smc)

    type NjFlow with // FromSMC
        static member FromSMC(smc: SubmodelElementCollection): NjFlow =
            let { Name=name; Guid=guid; Parameter=parameter; Id=id } = smc.ReadUniqueInfo()

            let buttons     = smc.TryFindChildSMC "Buttons"     |-> (fun smc2 -> smc2.CollectChildrenSMCWithSemanticKey "Button")     |? [||] |-> NjButton.FromSMC
            let lamps       = smc.TryFindChildSMC "Lamps"       |-> (fun smc2 -> smc2.CollectChildrenSMCWithSemanticKey "Lamp")       |? [||] |-> NjLamp.FromSMC
            let conditions  = smc.TryFindChildSMC "Conditions"  |-> (fun smc2 -> smc2.CollectChildrenSMCWithSemanticKey "Condition")  |? [||] |-> NjCondition.FromSMC
            let actions     = smc.TryFindChildSMC "Actions"     |-> (fun smc2 -> smc2.CollectChildrenSMCWithSemanticKey "Action")     |? [||] |-> NjAction.FromSMC

            NjFlow.Create( Name=name, Guid=guid, Id=id, Parameter=parameter, Buttons = buttons, Lamps = lamps, Conditions = conditions, Actions = actions)
            |> tee (readAasExtensionProperties smc)


    type NjWork with // FromSMC
        static member FromSMC(smc: SubmodelElementCollection): NjWork =
            let { Name=name; Guid=guid; Parameter=parameter; Id=id } = smc.ReadUniqueInfo()

            let flowGuid   = smc.TryGetPropValue       "FlowGuid"   |? null
            let motion     = smc.TryGetPropValue       "Motion"     |? null
            let script     = smc.TryGetPropValue       "Script"     |? null
            let isFinished = smc.TryGetPropValue<bool> "IsFinished" |? false
            let numRepeat  = smc.TryGetPropValue<int>  "NumRepeat"  |? 0
            let period     = smc.TryGetPropValue<int>  "Period"     |? 0
            let delay      = smc.TryGetPropValue<int>  "Delay"      |? 0
            let status4    = smc.TryGetPropValue<string> "Status"   >>= (Enum.TryParse<DbStatus4> >> tryParseToOption)

            (* AAS 구조상 Work/Calls/Call[], Work/Arrows/Arrow[] 형태로 존재 *)
            let calls  = smc.TryFindChildSMC "Calls"  |-> (fun smc2 -> smc2.CollectChildrenSMCWithSemanticKey "Call")  |? [||] |-> NjCall.FromSMC
            let arrows = smc.TryFindChildSMC "Arrows" |-> (fun smc2 -> smc2.CollectChildrenSMCWithSemanticKey "Arrow") |? [||] |-> NjArrow.FromSMC

            NjWork.Create(Name=name, Guid=guid, Id=id, Parameter=parameter
                , FlowGuid = flowGuid
                , Motion = motion
                , Script = script
                , IsFinished = isFinished
                , NumRepeat = numRepeat
                , Period = period
                , Delay = delay
                , Calls = calls
                , Status4 = status4
                , Arrows = arrows)
            |> tee (readAasExtensionProperties smc)

    type NjCall with // FromSMC
        static member FromSMC(smc: SubmodelElementCollection): NjCall =
            let { Name=name; Guid=guid; Parameter=parameter; Id=id } = smc.ReadUniqueInfo()
            let isDisabled       = smc.TryGetPropValue<bool> "IsDisabled"       |? false
            let commonConditions = smc.TryGetPropValue       "CommonConditions" |? null
            let autoConditions   = smc.TryGetPropValue       "AutoConditions"   |? null
            let timeout          = smc.TryGetPropValue<int>  "Timeout"
            let callType         = smc.TryGetPropValue       "CallType"         |? null
            let status4          = smc.TryGetPropValue<string> "Status"   >>= (Enum.TryParse<DbStatus4> >> tryParseToOption)


            let apiCalls =
                match smc.TryGetPropValue "ApiCalls" with
                | Some guids ->
                    let inner = guids.Trim().TrimStart('[', '|').TrimEnd('|', ']').Trim()
                    if String.IsNullOrEmpty(inner) then [||]
                    else inner.Split(';') |-> Guid.Parse
                | None -> [||]


            // Status4 는 저장 안함.  DB 전용

            NjCall.Create(Name=name, Guid=guid, Id=id, Parameter=parameter
                , IsDisabled = isDisabled
                , CommonConditions = commonConditions
                , AutoConditions = autoConditions
                , Timeout = timeout
                , CallType = callType
                , Status4 = status4
                , ApiCalls = apiCalls)     // Guid[] type
            |> tee (readAasExtensionProperties smc)



    type NjApiDef with // FromSMC
        static member FromSMC(smc: SubmodelElementCollection): NjApiDef =
            let { Name=name; Guid=guid; Parameter=parameter; Id=id } = smc.ReadUniqueInfo()
            let isPush = smc.TryGetPropValue<bool> "IsPush" |? false
            NjApiDef.Create(Name=name, Guid=guid, Id=id, Parameter=parameter, IsPush = isPush)
            |> tee (readAasExtensionProperties smc)

    type NjApiCall with // FromSMC
        static member FromSMC(smc: SubmodelElementCollection): NjApiCall =
            let { Name=name; Guid=guid; Parameter=parameter; Id=id } = smc.ReadUniqueInfo()

            let apiDef     = smc.GetPropValue    "ApiDef"     |> Guid.Parse
            let inAddress  = smc.TryGetPropValue "InAddress"  |? null
            let outAddress = smc.TryGetPropValue "OutAddress" |? null
            let inSymbol   = smc.TryGetPropValue "InSymbol"   |? null
            let outSymbol  = smc.TryGetPropValue "OutSymbol"  |? null
            let valueSpec  = smc.TryGetPropValue "ValueSpec"  |? null

            NjApiCall.Create(Name=name, Guid=guid, Id=id, Parameter=parameter
                , ApiDef = apiDef
                , InAddress = inAddress
                , OutAddress = outAddress
                , InSymbol = inSymbol
                , OutSymbol = outSymbol
                , ValueSpec = valueSpec)
            |> tee (readAasExtensionProperties smc)
