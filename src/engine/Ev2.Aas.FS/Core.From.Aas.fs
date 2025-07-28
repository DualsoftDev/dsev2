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
    //type Environment = AasCore.Aas3_0.Environment
    //type ISubmodel = AasCore.Aas3_0.ISubmodel

    type NjProject with // FromISubmodel, FromAasxFile
        static member FromAasxFile(aasxPath: string): NjProject =
            let aasFileInfo = AasXModule.readEnvironmentFromAasx aasxPath
            let env = aasFileInfo.Environment

            let projectSubmodel =
                env.Submodels
                |> Seq.tryFind (fun sm -> sm.IdShort = PreludeModule.SubmodelIdShort)
                |> function
                    | Some sm -> sm
                    | None -> failwith $"Project Submodel with IdShort '{PreludeModule.SubmodelIdShort}' not found in AASX file: {aasxPath}"

            let project = NjProject.FromISubmodel(projectSubmodel)
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

            NjProject(
                Name=name, Guid=guid, Id=id, Parameter=parameter

                , DateTime       = dateTime
                , Database       = database
                , Author         = author
                , Version        = version
                , Description    = description
                , ActiveSystems  = activeSystems
                , PassiveSystems = passiveSystems
            )

    type NjSystem with  // FromSMC
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

            NjSystem(
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
                , ApiCalls = apiCalls
            )

    type NjArrow with
        static member FromSMC(smc: SubmodelElementCollection): NjArrow =
            let { Name=name; Guid=guid; Parameter=parameter; Id=id } = smc.ReadUniqueInfo()
            let src = smc.GetPropValue "Source"
            let tgt = smc.GetPropValue "Target"
            let typ = smc.GetPropValue "Type"
            NjArrow(Name=name, Guid=guid, Id=id, Parameter=parameter
                    , Source=src, Target=tgt, Type=typ)


    type NjButton with
        static member FromSMC(smc: SubmodelElementCollection): NjButton =
            createSimpleFromSMC (fun () -> NjButton()) smc

    type NjLamp with
        static member FromSMC(smc: SubmodelElementCollection): NjLamp =
            createSimpleFromSMC (fun () -> NjLamp()) smc

    type NjCondition with
        static member FromSMC(smc: SubmodelElementCollection): NjCondition =
            createSimpleFromSMC (fun () -> NjCondition()) smc

    type NjAction with
        static member FromSMC(smc: SubmodelElementCollection): NjAction =
            createSimpleFromSMC (fun () -> NjAction()) smc

    type NjFlow with
        static member FromSMC(smc: SubmodelElementCollection): NjFlow =
            let { Name=name; Guid=guid; Parameter=parameter; Id=id } = smc.ReadUniqueInfo()

            let buttons     = smc.TryFindChildSMC "Buttons"     |-> (fun smc2 -> smc2.CollectChildrenSMCWithSemanticKey "Button")     |? [||] |-> NjButton.FromSMC
            let lamps       = smc.TryFindChildSMC "Lamps"       |-> (fun smc2 -> smc2.CollectChildrenSMCWithSemanticKey "Lamp")       |? [||] |-> NjLamp.FromSMC
            let conditions  = smc.TryFindChildSMC "Conditions"  |-> (fun smc2 -> smc2.CollectChildrenSMCWithSemanticKey "Condition")  |? [||] |-> NjCondition.FromSMC
            let actions     = smc.TryFindChildSMC "Actions"     |-> (fun smc2 -> smc2.CollectChildrenSMCWithSemanticKey "Action")     |? [||] |-> NjAction.FromSMC

            NjFlow( Name=name, Guid=guid, Id=id, Parameter=parameter, Buttons = buttons, Lamps = lamps, Conditions = conditions, Actions = actions)


    type NjWork with
        static member FromSMC(smc: SubmodelElementCollection): NjWork =
            let { Name=name; Guid=guid; Parameter=parameter; Id=id } = smc.ReadUniqueInfo()

            let flowGuid   = smc.TryGetPropValue       "FlowGuid"   |? null
            let motion     = smc.TryGetPropValue       "Motion"     |? null
            let script     = smc.TryGetPropValue       "Script"     |? null
            let isFinished = smc.TryGetPropValue<bool> "IsFinished" |? false
            let numRepeat  = smc.TryGetPropValue<int>  "NumRepeat"  |? 0
            let period     = smc.TryGetPropValue<int>  "Period"     |? 0
            let delay      = smc.TryGetPropValue<int>  "Delay"      |? 0

            (* AAS 구조상 Work/Calls/Call[], Work/Arrows/Arrow[] 형태로 존재 *)
            let calls  = smc.TryFindChildSMC "Calls"  |-> (fun smc2 -> smc2.CollectChildrenSMCWithSemanticKey "Call")  |? [||] |-> NjCall.FromSMC
            let arrows = smc.TryFindChildSMC "Arrows" |-> (fun smc2 -> smc2.CollectChildrenSMCWithSemanticKey "Arrow") |? [||] |-> NjArrow.FromSMC

            NjWork(Name=name, Guid=guid, Id=id, Parameter=parameter
                , FlowGuid = flowGuid
                , Motion = motion
                , Script = script
                , IsFinished = isFinished
                , NumRepeat = numRepeat
                , Period = period
                , Delay = delay
                , Calls = calls, Arrows = arrows)

    type NjCall with
        static member FromSMC(smc: SubmodelElementCollection): NjCall =
            let { Name=name; Guid=guid; Parameter=parameter; Id=id } = smc.ReadUniqueInfo()
            let isDisabled       = smc.TryGetPropValue<bool> "IsDisabled"       |? false
            let commonConditions = smc.TryGetPropValue       "CommonConditions" |? null
            let autoConditions   = smc.TryGetPropValue       "AutoConditions"   |? null
            let timeout          = smc.TryGetPropValue<int>  "Timeout"
            let callType         = smc.TryGetPropValue       "CallType"         |? null


            let apiCalls =
                match smc.TryGetPropValue "ApiCalls" with
                | Some guids ->
                    let inner = guids.Trim().TrimStart('[', '|').TrimEnd('|', ']').Trim()
                    if String.IsNullOrEmpty(inner) then [||]
                    else inner.Split(';') |-> Guid.Parse
                | None -> [||]


            // Status4 는 저장 안함.  DB 전용

            NjCall(Name=name, Guid=guid, Id=id, Parameter=parameter
                , IsDisabled = isDisabled
                , CommonConditions = commonConditions
                , AutoConditions = autoConditions
                , Timeout = timeout
                , CallType = callType
                , ApiCalls = apiCalls     // Guid[] type
                )


    type NjApiDef with
        static member FromSMC(smc: SubmodelElementCollection): NjApiDef =
            let { Name=name; Guid=guid; Parameter=parameter; Id=id } = smc.ReadUniqueInfo()
            let isPush = smc.TryGetPropValue<bool> "IsPush" |? false
            let topicIndex = smc.TryGetPropValue<int> "TopicIndex" |? 0
            NjApiDef(Name=name, Guid=guid, Id=id, Parameter=parameter
                , IsPush = isPush, TopicIndex = topicIndex
            )

    type NjApiCall with
        static member FromSMC(smc: SubmodelElementCollection): NjApiCall =
            let { Name=name; Guid=guid; Parameter=parameter; Id=id } = smc.ReadUniqueInfo()

            let apiDef     = smc.GetPropValue    "ApiDef"     |> Guid.Parse
            let inAddress  = smc.TryGetPropValue "InAddress"  |? null
            let outAddress = smc.TryGetPropValue "OutAddress" |? null
            let inSymbol   = smc.TryGetPropValue "InSymbol"   |? null
            let outSymbol  = smc.TryGetPropValue "OutSymbol"  |? null
            let valueSpec  = smc.TryGetPropValue "ValueSpec"  |? null

            NjApiCall(Name=name, Guid=guid, Id=id, Parameter=parameter
                , ApiDef = apiDef
                , InAddress = inAddress
                , OutAddress = outAddress
                , InSymbol = inSymbol
                , OutSymbol = outSymbol
                , ValueSpec = valueSpec
            )
