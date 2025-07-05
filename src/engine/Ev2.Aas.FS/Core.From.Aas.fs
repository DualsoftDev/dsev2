namespace rec Dual.Ev2.Aas

(* AAS Json/Xml 로부터 Core 를 생성하기 위한 코드 *)

open System.Linq
open System

open AasCore.Aas3_0

open Dual.Common.Core.FS
open Ev2.Core.FS
open System.Globalization
open Dual.Common.Db.FS
open Dual.Common.Base

[<AutoOpen>]
module CoreFromAas =
    //type Environment = AasCore.Aas3_0.Environment
    //type ISubmodel = AasCore.Aas3_0.ISubmodel

    type NjProject with
        static member FromISubmodel(submodel:ISubmodel): NjProject =
            let details = submodel.GetSMCWithSemanticKey "Details" |> head
            let { Name=name; Guid=guid; Parameter=parameter; Id=id } = details.ReadUniqueInfo()

            let database    = details.TryGetPropValue "Database"    >>= DU.tryParse<DbProvider> |? Prelude.getNull<DbProvider>()
            let dateTime    = details.GetPropValue    "DateTime"    |> DateTime.Parse
            let author      = details.TryGetPropValue "Author"      |? null
            let description = details.TryGetPropValue "Description" |? null
            let version     = details.TryGetPropValue "Version"     |-> Version.Parse |? Version(0, 0)

            let myProtosSystems = submodel.GetSMCWithSemanticKey "MyPrototypeSystems"       >>= (_.GetSMC("System")) |-> NjSystem.FromSMC
            let importsSystems  = submodel.GetSMCWithSemanticKey "ImportedPrototypeSystems" >>= (_.GetSMC("System")) |-> NjSystem.FromSMC
            let activeSystems   = submodel.GetSMCWithSemanticKey "ActiveSystems"            >>= (_.GetSMC("System")) |-> NjSystem.FromSMC
            let protos = myProtosSystems @ importsSystems
            let passiveSystems  = submodel.GetSMCWithSemanticKey "PassiveSystems"           >>= (_.GetSMC("PassiveSystem")) |-> (fun smc -> NjSystemLoadType.FromSMC(smc, protos))

            NjProject(
                Name=name, Guid=guid, Id=id, Parameter=parameter

                , DateTime = dateTime
                , Database = database
                , Author = author
                , Version = version
                , Description = description

                , MyPrototypeSystems = myProtosSystems
                , ImportedPrototypeSystems = importsSystems
                , ActiveSystems = activeSystems
                , PassiveSystems = passiveSystems
            )

    type NjSystem with
        static member FromAasJsonStringENV(jsonEnv:string): NjSystem =
            let env = J.CreateIClassFromJson<Environment>(jsonEnv)
            let sm = env.Submodels.First()
            NjSystem.FromISubmodel(sm)

        static member FromAasXmlENV(xml:string): NjSystem =
            let sm = J.CreateIClassFromXml<Environment>(xml).Submodels.First()
            NjSystem.FromISubmodel(sm)

        [<Obsolete("아마 불필요..")>]
        static member FromISubmodel(submodel:ISubmodel): NjSystem =
            assert(submodel.IdShort.IsOneOf("Identification", "System"))

            let details = submodel.GetSMCWithSemanticKey "Details" |> head
            let { Name=name; Guid=guid; Parameter=parameter; Id=id } = details.ReadUniqueInfo()
            let dateTime      = details.GetPropValue "DateTime"  |> DateTime.Parse
            let iri           = details.GetPropValue "IRI"
            let engineVersion = details.TryGetPropValue "EngineVersion" |-> Version.Parse |? Version(0, 0)
            let langVersion   = details.TryGetPropValue "LangVersion"   |-> Version.Parse |? Version(0, 0)
            let author        = details.TryGetPropValue "Author" |? null
            let description   = details.TryGetPropValue "Description" |? null


            let apiDefs  = submodel.GetSMCWithSemanticKey "ApiDefs" |-> NjApiDef.FromSMC
            let apiCalls = submodel.GetSMCWithSemanticKey "ApiCalls"|-> NjApiCall.FromSMC
            let works    = submodel.GetSMCWithSemanticKey "Works"   |-> NjWork.FromSMC
            let flows    = submodel.GetSMCWithSemanticKey "Flows"   |-> NjFlow.FromSMC
            let arrows   = submodel.GetSMCWithSemanticKey "Arrows"  |-> NjArrow.FromSMC

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

        static member FromSMC(smc: SubmodelElementCollection): NjSystem =
            let getSmc (semanticKey: string): SubmodelElementCollection [] =
                smc.CollectChildrenSMCWithSemanticKey semanticKey

            let details = getSmc "Details" |> head
            let { Name=name; Guid=guid; Parameter=parameter; Id=id } = details.ReadUniqueInfo()
            let dateTime      = details.GetPropValue "DateTime"  |> DateTime.Parse
            let iri           = details.TryGetPropValue "IRI" |?? (fun () -> logWarn $"No IRI on system {name}"; null)
            let engineVersion = details.TryGetPropValue "EngineVersion" |-> Version.Parse |? Version(0, 0)
            let langVersion   = details.TryGetPropValue "LangVersion"   |-> Version.Parse |? Version(0, 0)
            let author        = details.TryGetPropValue "Author" |? null
            let description   = details.TryGetPropValue "Description" |? null


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
            let { Name=name; Guid=guid; Parameter=parameter; Id=id } = smc.ReadUniqueInfo()
            NjButton(Name=name, Guid=guid, Id=id, Parameter=parameter)

    type NjLamp with
        static member FromSMC(smc: SubmodelElementCollection): NjLamp =
            let { Name=name; Guid=guid; Parameter=parameter; Id=id } = smc.ReadUniqueInfo()
            NjLamp(Name=name, Guid=guid, Id=id, Parameter=parameter)

    type NjCondition with
        static member FromSMC(smc: SubmodelElementCollection): NjCondition =
            let { Name=name; Guid=guid; Parameter=parameter; Id=id } = smc.ReadUniqueInfo()
            NjCondition(Name=name, Guid=guid, Id=id, Parameter=parameter)

    type NjAction with
        static member FromSMC(smc: SubmodelElementCollection): NjAction =
            let { Name=name; Guid=guid; Parameter=parameter; Id=id } = smc.ReadUniqueInfo()
            NjAction(Name=name, Guid=guid, Id=id, Parameter=parameter)

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

            let flowGuid   = smc.TryGetPropValue "FlowGuid" |? null
            let motion     = smc.TryGetPropValue "Motion" |? null
            let script     = smc.TryGetPropValue "Script" |? null
            let isFinished = smc.TryGetPropValue<bool> "IsFinished" |? false
            let numRepeat  = smc.TryGetPropValue<int> "NumRepeat" |? 0
            let period     = smc.TryGetPropValue<int> "Period" |? 0
            let delay      = smc.TryGetPropValue<int> "Delay" |? 0

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
            NjApiDef(Name=name, Guid=guid, Id=id, Parameter=parameter
                , IsPush = isPush
            )

    type NjApiCall with
        static member FromSMC(smc: SubmodelElementCollection): NjApiCall =
            let { Name=name; Guid=guid; Parameter=parameter; Id=id } = smc.ReadUniqueInfo()

            let apiDef     = smc.GetPropValue "ApiDef" |> Guid.Parse
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

    type NjSystemLoadType with
        static member FromSMC(smc: SubmodelElementCollection, systemProtos:NjSystem seq): NjSystemLoadType =
            match smc.GetPropValue("Type") with
            | "LocalDefinition" ->
                let guid = smc.GetPropValue("Guid") |> s2guid
                let system = systemProtos |> find (fun sys -> sys.Guid = guid)
                LocalDefinition system
            | "Reference" ->
                let instanceName = smc.GetPropValue("InstanceName")
                let protoGuid = smc.GetPropValue("PrototypeGuid") |> s2guid
                let instanceGuid = smc.GetPropValue("InstanceGuid") |> s2guid
                {
                    InstanceName = instanceName
                    PrototypeGuid = protoGuid
                    InstanceGuid = instanceGuid
                } |> Reference

            | _ -> failwith "ERROR"
