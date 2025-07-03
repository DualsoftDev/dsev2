namespace rec Dual.Ev2.Aas

(* AAS Json/Xml 로부터 Core 를 생성하기 위한 코드 *)

open System.Linq
open System

open AasCore.Aas3_0

open Dual.Common.Core.FS
open Ev2.Core.FS
open System.Globalization


[<AutoOpen>]
module PropModule =
    /// SemanticId 키 매칭 유틸
    let internal hasSemanticKey (semanticKey: string) (semantic: IHasSemantics) =
        semantic.SemanticId <> null &&
        semantic.SemanticId.Keys
        |> Seq.exists (fun k -> k.Value = AasSemantics.map[semanticKey])

    type UniqueInfo = { Name: string; Guid: Guid; Parameter: string; Id: Id option }

    type SubmodelElementCollection with
        member smc.TryGetPropValueByCategory (category:string): string option =
            smc.Value
            |> Seq.tryPick (function
                | :? Property as p when p.Category = category -> Some p.Value
                | _ -> None)

        member smc.TryGetPropValueBySemanticKey (semanticKey:string): string option =
            let semanticId = AasSemantics.map[semanticKey]
            smc.Value
            |> Seq.tryPick (function
                | :? Property as p when hasSemanticKey semanticKey p -> Some p.Value
                | _ -> None)

        member smc.TryGetPropValue (propName:string) = smc.TryGetPropValueBySemanticKey propName

        member smc.TryGetPropValue<'T> (propName: string): 'T option =
            smc.TryGetPropValue propName
            >>= (fun str ->
                try
                    let value =
                        match typeof<'T> with
                        | _ when typeof<'T> = typeof<string> ->
                            box str
                        | _ when typeof<'T> = typeof<Guid> ->
                            str |> Guid.Parse |> box
                        | _ when typeof<'T> = typeof<int> ->
                            str |> Int32.Parse |> box
                        | _ when typeof<'T> = typeof<float> ->
                            str |> Double.Parse |> box
                        | _ when typeof<'T> = typeof<bool> ->
                            str |> Boolean.Parse |> box
                        | _ ->
                            // 일반적인 Convert.ChangeType 사용
                            Convert.ChangeType(str, typeof<'T>, CultureInfo.InvariantCulture)
                    Some (value :?> 'T)
                with _ -> None)

        member smc.GetPropValue propName =
            smc.TryGetPropValue propName |> Option.get

        member smc.EnumerateChildrenSMC(semanticKey: string): SubmodelElementCollection [] =
            let semanticId = AasSemantics.map[semanticKey]
            smc.Value
            >>= (function
                | :? SubmodelElementCollection as child when hasSemanticKey semanticKey child -> [child]
                | _ -> [])
            |> toArray

        member smc.TryFindChildSMC(semanticKey: string): SubmodelElementCollection option =
            smc.EnumerateChildrenSMC semanticKey |> tryHead

        member smc.ReadUniqueInfo() =
            let name = smc.TryGetPropValue "Name" |? null
            let guid = smc.GetPropValue "Guid" |> Guid.Parse
            let parameter = smc.TryGetPropValue "Parameter" |? null
            let id = smc.TryGetPropValue "Id" |-> Id.Parse
            { Name=name; Guid=guid; Parameter=parameter; Id=id }



[<AutoOpen>]
module CoreFromAas =
    type Environment = AasCore.Aas3_0.Environment
    type ISubmodel = AasCore.Aas3_0.ISubmodel

    type NjProject with
        static member FromISubmodel(submodel:ISubmodel): NjProject =
            failwith "ERROR"

    type NjSystem with
        static member FromAasJsonENV(json:string): NjSystem =
            let env = J.CreateIClassFromJson<Environment>(json)
            let sm = env.Submodels.First()
            NjSystem.FromISubmodel(sm)

        static member FromAasXmlENV(xml:string): NjSystem =
            let sm = J.CreateIClassFromXml<Environment>(xml).Submodels.First()
            NjSystem.FromISubmodel(sm)

        static member FromISubmodel(submodel:ISubmodel): NjSystem =
            assert(submodel.IdShort.IsOneOf("Identification", "System"))

            let getSMC semanticKey =
                submodel.SubmodelElements
                |> Seq.tryFind (fun sm -> PropModule.hasSemanticKey semanticKey sm)
                >>= (fun sm ->
                    match sm with
                    | :? SubmodelElementCollection as smc -> Some (smc.Value.OfType<SubmodelElementCollection>().ToArray())
                    | _ -> None)
                |? [||]

            //let xxx = submodel.SubmodelElements.OfType<SubmodelElementCollection>().ToArray()

            let details =
                submodel.SubmodelElements
                    .OfType<SubmodelElementCollection>()
                    .FirstOrDefault(fun sm -> PropModule.hasSemanticKey "Detail" sm)
            let { Name=name; Guid=guid; Parameter=parameter; Id=id } = details.ReadUniqueInfo()
            let dateTime      = details.GetPropValue "DateTime"  |> DateTime.Parse
            let iri           = details.GetPropValue "IRI"
            let engineVersion = details.TryGetPropValue "EngineVersion" |-> Version.Parse |? Version(0, 0)
            let langVersion   = details.TryGetPropValue "LangVersion"   |-> Version.Parse |? Version(0, 0)
            let author        = details.TryGetPropValue "Author" |? null
            let description   = details.TryGetPropValue "Description" |? null


            let apiDefs  = getSMC "ApiDefs"|-> NjApiDef.FromSMC
            let apiCalls = getSMC "ApiCalls"|-> NjApiCall.FromSMC
            let works    = getSMC "Works"  |-> NjWork.FromSMC
            let flows    = getSMC "Flows"  |-> NjFlow.FromSMC
            let arrows   = getSMC "Arrows" |-> NjArrow.FromSMC

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

            let buttons     = smc.TryFindChildSMC "Buttons"     |-> (fun smc2 -> smc2.EnumerateChildrenSMC "Button")     |? [||] |-> NjButton.FromSMC
            let lamps       = smc.TryFindChildSMC "Lamps"       |-> (fun smc2 -> smc2.EnumerateChildrenSMC "Lamp")       |? [||] |-> NjLamp.FromSMC
            let conditions  = smc.TryFindChildSMC "Conditions"  |-> (fun smc2 -> smc2.EnumerateChildrenSMC "Condition")  |? [||] |-> NjCondition.FromSMC
            let actions     = smc.TryFindChildSMC "Actions"     |-> (fun smc2 -> smc2.EnumerateChildrenSMC "Action")     |? [||] |-> NjAction.FromSMC

            NjFlow( Name=name, Guid=guid, Id=id, Parameter=parameter, Buttons = buttons, Lamps = lamps, Conditions = conditions, Actions = actions)


    type NjWork with
        static member FromSMC(smc: SubmodelElementCollection): NjWork =
            let { Name=name; Guid=guid; Parameter=parameter; Id=id } = smc.ReadUniqueInfo()

            (* AAS 구조상 Work/Calls/Call[], Work/Arrows/Arrow[] 형태로 존재 *)
            let calls  = smc.TryFindChildSMC "Calls"  |-> (fun smc2 -> smc2.EnumerateChildrenSMC "Call")  |? [||] |-> NjCall.FromSMC
            let arrows = smc.TryFindChildSMC "Arrows" |-> (fun smc2 -> smc2.EnumerateChildrenSMC "Arrow") |? [||] |-> NjArrow.FromSMC

            NjWork(Name=name, Guid=guid, Id=id, Parameter=parameter, Calls = calls, Arrows = arrows)

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
