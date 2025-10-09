namespace rec Dual.Ev2.Aas

(* AAS Json/Xml 로부터 Core 를 생성하기 위한 코드 *)

open System

open AasCore.Aas3_0

open Dual.Common.Core.FS
open Dual.Common.Db.FS
open Dual.Common.Base

open Ev2.Core.FS
open Newtonsoft.Json
open Newtonsoft.Json.Linq

#nowarn FS0044 // obsolete 사용 허용


[<AutoOpen>]
module CoreFromAas =
    //// 공통 FromSMC 헬퍼 함수 - UniqueInfo만 필요한 단순한 객체들을 위함
    //let internal createSimpleFromSMC<'T when 'T :> Unique>
    //    (constructor: unit -> 'T)
    //    (smc: SubmodelElementCollection) : 'T
    //  =
    //    let { Name=name; Guid=guid; Parameter=parameter; Id=id } = smc.ReadUniqueInfo()
    //    let obj = constructor()
    //    obj.Name <- name
    //    obj.Guid <- guid
    //    obj.Id <- id
    //    obj.Parameter <- parameter
    //    obj

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
            project |> tee(fun z -> z.Properties.AasxPath <- aasxPath)

        static member FromISubmodel(submodel:ISubmodel): NjProject =
            let project = submodel.GetSMCWithSemanticKey "Project" |> head
            let { Name=name; Guid=guid; Parameter=parameter; Id=id } = project.ReadUniqueInfo()

            let database    = project.TryGetPropValue "Database"    >>= DU.tryParse<DbProvider>
            let dateTime    = project.TryGetPropValue "DateTime"    |-> DateTime.Parse
            let author      = project.TryGetPropValue "Author"
            let description = project.TryGetPropValue "Description"
            let version     = project.TryGetPropValue "Version"     |-> Version.Parse
            let propertiesJsonOpt = project.TryGetPropValue "Properties"

            let activeSystems   = project.GetSMC "ActiveSystems"  >>= (_.GetSMC("System")) |-> NjSystem.FromSMC
            let passiveSystems  = project.GetSMC "PassiveSystems" >>= (_.GetSMC("System")) |-> NjSystem.FromSMC

            NjProject.Create(
                Name=name, Guid=guid, Id=id, Parameter=parameter
                , ActiveSystems  = activeSystems
                , PassiveSystems = passiveSystems)
            |> tee (fun njp ->
                let props =
                    propertiesJsonOpt
                    |-> JsonPolymorphic.FromJson<ProjectProperties>
                    |?? ProjectProperties.Create
                props.RawParent <- Some njp
                database |> iter (fun db -> props.Database <- db)
                author |> iter (fun v -> props.Author <- v)
                version |> iter (fun v -> props.Version <- v)
                description |> iter (fun v -> props.Description <- v)
                dateTime |> iter (fun v -> props.DateTime <- v)
                njp.Properties <- props)
            |> tee (readAasExtensionProperties project)



    type NjSystem with // FromSMC
        static member FromSMC(smc: SubmodelElementCollection): NjSystem =
            let { Name=name; Guid=guid; Parameter=parameter; Id=id} = smc.ReadUniqueInfo()
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
                , IRI = iri

                , Flows = flows
                , Works = works
                , Arrows = arrows
                , ApiDefs = apiDefs
                , ApiCalls = apiCalls
            )
            |> tee (fun system ->
                readAasExtensionProperties smc system |> ignore
                // UI 요소들 읽기
                let entitiesJson = smc.TryGetPropValue "Entities" |? null
                if entitiesJson.NonNullAny() then
                    let arr = JArray.Parse(entitiesJson)
                    system.PolymorphicJsonEntities.SerializedItems <- arr
                    system.PolymorphicJsonEntities.SyncToValues()

                let propertiesJson = smc.TryGetPropValue "Properties" |? null
                if propertiesJson.NonNullAny() then
                    let props = JsonPolymorphic.FromJson<DsSystemProperties>(propertiesJson)
                    props.RawParent <- Some (system :> Unique)
                    system.Properties <- props

                let sp = system.Properties
                author |> Option.ofObj |> iter (fun v -> sp.Author <- v)
                sp.EngineVersion <- engineVersion
                sp.LangVersion <- langVersion
                description |> Option.ofObj |> iter (fun v -> sp.Description <- v)
                sp.DateTime <- dateTime)

    type NjArrow with // FromSMC
        static member FromSMC(smc: SubmodelElementCollection): NjArrow =
            let { Name=name; Guid=guid; Parameter=parameter; Id=id} = smc.ReadUniqueInfo()
            let src = smc.GetPropValue "Source"
            let tgt = smc.GetPropValue "Target"
            let typ = smc.GetPropValue "Type"
            NjArrow.Create(Name=name, Guid=guid, Id=id, Parameter=parameter, Source=src, Target=tgt, Type=typ)
            |> tee (readAasExtensionProperties smc)


    type NjFlow with // FromSMC
        static member FromSMC(smc: SubmodelElementCollection): NjFlow =
            let { Name=name; Guid=guid; Parameter=parameter; Id=id } = smc.ReadUniqueInfo()

            // Flow는 이제 UI 요소를 직접 소유하지 않음

            NjFlow.Create( Name=name, Guid=guid, Id=id, Parameter=parameter)
            |> tee (fun flow ->
                let propertiesJson = smc.TryGetPropValue "Properties" |? null
                if propertiesJson.NonNullAny() then
                    let props = JsonPolymorphic.FromJson<FlowProperties>(propertiesJson)
                    props.RawParent <- Some flow
                    flow.Properties <- props)
            |> tee (readAasExtensionProperties smc)


    type NjWork with // FromSMC
        static member FromSMC(smc: SubmodelElementCollection): NjWork =
            let { Name=name; Guid=guid; Parameter=parameter; Id=id } = smc.ReadUniqueInfo()

            let flowGuid      = smc.TryGetPropValue       "FlowGuid"      |? null
            let motion        = smc.TryGetPropValue       "Motion"        |? null
            let script        = smc.TryGetPropValue       "Script"        |? null
            let externalStart = smc.TryGetPropValue       "ExternalStart" |? null
            let isFinished    = smc.TryGetPropValue<bool> "IsFinished"    |? false
            let numRepeat     = smc.TryGetPropValue<int>  "NumRepeat"     |? 0
            let period        = smc.TryGetPropValue<int>  "Period"        |? 0
            let delay         = smc.TryGetPropValue<int>  "Delay"         |? 0
            let status4       = smc.TryGetPropValue<string> "Status"   >>= (Enum.TryParse<DbStatus4> >> tryParseToOption)
            let propertiesJson = smc.TryGetPropValue "Properties" |? null

            (* AAS 구조상 Work/Calls/Call[], Work/Arrows/Arrow[] 형태로 존재 *)
            let calls  = smc.TryFindChildSMC "Calls"  |-> (fun smc2 -> smc2.CollectChildrenSMCWithSemanticKey "Call")  |? [||] |-> NjCall.FromSMC
            let arrows = smc.TryFindChildSMC "Arrows" |-> (fun smc2 -> smc2.CollectChildrenSMCWithSemanticKey "Arrow") |? [||] |-> NjArrow.FromSMC

            NjWork.Create(Name=name, Guid=guid, Id=id, Parameter=parameter)
            |> tee (fun work ->
                work.FlowGuid <- flowGuid
                work.Status4 <- status4
                work.Calls <- calls
                work.Arrows <- arrows

                if propertiesJson.NonNullAny() then
                    let props = JsonPolymorphic.FromJson<WorkProperties>(propertiesJson)
                    props.RawParent <- Some (work :> Unique)
                    work.Properties <- props
                else
                    let props = work.Properties
                    props.Motion <- motion
                    props.Script <- script
                    props.ExternalStart <- externalStart
                    props.IsFinished <- isFinished
                    props.NumRepeat <- numRepeat
                    props.Period <- period
                    props.Delay <- delay
                    props.RawParent <- Some (work :> Unique))
            |> tee (readAasExtensionProperties smc)

    type NjCall with // FromSMC
        static member FromSMC(smc: SubmodelElementCollection): NjCall =
            let { Name=name; Guid=guid; Parameter=parameter; Id=id } = smc.ReadUniqueInfo()
            let isDisabled       = smc.TryGetPropValue<bool> "IsDisabled"       |? false
            // JSON 문자열로 저장된 조건들을 ApiCallValueSpecs로 변환
            let commonConditionsStr = smc.TryGetPropValue "CommonConditions" |? null
            let autoConditionsStr   = smc.TryGetPropValue "AutoConditions"   |? null
            let timeout  = smc.TryGetPropValue<int>    "Timeout"
            let callType = smc.TryGetPropValue         "CallType" |? null
            let status4  = smc.TryGetPropValue<string> "Status"   >>= (Enum.TryParse<DbStatus4> >> tryParseToOption)
            let commonConditions =
                if commonConditionsStr.IsNullOrEmpty() then ApiCallValueSpecs()
                else ApiCallValueSpecs.FromJson(commonConditionsStr)
            let autoConditions =
                if autoConditionsStr.IsNullOrEmpty() then ApiCallValueSpecs()
                else ApiCallValueSpecs.FromJson(autoConditionsStr)


            let apiCalls =
                match smc.TryGetPropValue "ApiCalls" with
                | Some guids ->
                    let inner = guids.Trim().TrimStart('[', '|').TrimEnd('|', ']').Trim()
                    if String.IsNullOrEmpty(inner) then [||]
                    else inner.Split(';') |-> Guid.Parse
                | None -> [||]


            // Status4 는 저장 안함.  DB 전용

            let njCall = NjCall.Create(Name=name, Guid=guid, Id=id, Parameter=parameter
                , IsDisabled = isDisabled
                , Timeout = timeout
                , CallType = callType
                , Status4 = status4
                , ApiCalls = apiCalls)     // Guid[] type
            // object properties 설정
            njCall.AutoConditionsObj <- autoConditions
            njCall.CommonConditionsObj <- commonConditions
            let propertiesJson = smc.TryGetPropValue "Properties" |? null
            if propertiesJson.NonNullAny() then
                let props = JsonPolymorphic.FromJson<CallProperties>(propertiesJson)
                props.RawParent <- Some njCall
                njCall.Properties <- props
            njCall
            |> tee (readAasExtensionProperties smc)



    type NjApiDef with // FromSMC
        static member FromSMC(smc: SubmodelElementCollection): NjApiDef =
            let { Name=name; Guid=guid; Parameter=parameter; Id=id} = smc.ReadUniqueInfo()

            NjApiDef.Create(Name=name, Guid=guid, Id=id, Parameter=parameter)
            |> tee (fun apiDef ->
                let propertiesJson = smc.TryGetPropValue "Properties" |? null
                if propertiesJson.NonNullAny() then
                    let props = JsonPolymorphic.FromJson<ApiDefProperties>(propertiesJson)
                    props.RawParent <- Some apiDef
                    apiDef.Properties <- props)
            |> tee (readAasExtensionProperties smc)

    type NjApiCall with // FromSMC
        static member FromSMC(smc: SubmodelElementCollection): NjApiCall =
            let { Name=name; Guid=guid; Parameter=parameter; Id=id} = smc.ReadUniqueInfo()

            let apiDef     = smc.GetPropValue    "ApiDef"     |> Guid.Parse
            let inAddress  = smc.TryGetPropValue "InAddress"  |? null
            let outAddress = smc.TryGetPropValue "OutAddress" |? null
            let inSymbol   = smc.TryGetPropValue "InSymbol"   |? null
            let outSymbol  = smc.TryGetPropValue "OutSymbol"  |? null
            let valueSpec  = smc.TryGetPropValue "ValueSpec"  |? null
            // IOTags 역직렬화
            let ioTagsStr = smc.TryGetPropValue "IOTags" |? null

            let apiCall = NjApiCall.Create(Name=name, Guid=guid, Id=id, Parameter=parameter)
            apiCall.Properties.ApiDefGuid <- apiDef
            apiCall.Properties.InAddress <- inAddress
            apiCall.Properties.OutAddress <- outAddress
            apiCall.Properties.InSymbol <- inSymbol
            apiCall.Properties.OutSymbol <- outSymbol
            apiCall.ValueSpec <- valueSpec
            if not (System.String.IsNullOrEmpty(ioTagsStr)) then
                apiCall.IOTags <- JsonConvert.DeserializeObject<IOTagsWithSpec>(ioTagsStr)
            apiCall
            |> tee (fun apiCall ->
                let propertiesJson = smc.TryGetPropValue "Properties" |? null
                if propertiesJson.NonNullAny() then
                    let props = JsonPolymorphic.FromJson<ApiCallProperties>(propertiesJson)
                    props.RawParent <- Some apiCall
                    apiCall.Properties <- props)
            |> tee (readAasExtensionProperties smc)
