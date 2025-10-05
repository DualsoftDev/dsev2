namespace Ev2.Core.FS

open System
open Dual.Common.Core.FS
open Dual.Common.Base
open Dual.Common.Db.FS
open Newtonsoft.Json

[<AutoOpen>]
module internal DsCopyModule =


    /// fwdDuplicate <- duplicateUnique
    let duplicateUnique (source:IUnique): IUnique =
        match source with
        | :? DsSystem  as rs -> rs.Duplicate()
        | :? Project as rp -> rp.Duplicate()
        | _ -> failwithf "Unsupported type for duplication: %A" (source.GetType())

    /// fwdReplicate <- replicateUnique
    let replicateUnique (source:IUnique): IUnique =
        match source with
        | :? DsSystem  as rs -> rs.Replicate()
        | :? Project as rp -> rp.Replicate()
        | _ -> failwithf "Unsupported type for replication: %A" (source.GetType())

    let private linkUniq (src:#Unique) (dst:#Unique): #Unique=
        match box src with
        | :? IRtUnique  as s -> dst.RtObject  <- Some s
        | :? INjUnique  as s -> dst.NjObject  <- Some s
        | :? IORMUnique as s -> dst.ORMObject <- Some s
        | _  -> fail()

        match box dst with
        | :? IRtUnique  as d -> src.RtObject  <- Some d
        | :? INjUnique  as d -> src.NjObject  <- Some d
        | :? IORMUnique as d -> src.ORMObject <- Some d
        | _  -> fail()

        dst


    /// src Unique 객체의 속성정보 (Id, Name, Guid, DateTime)를 복사해서 dst 의 Unique 객체에 저장
    // 시각적 구분을 위해서 길어지더라도 한줄로 표현 할 것.
    let replicatePropertiesImpl (src:#Unique) (dst:#Unique) : #Unique =
        let theDbApi = AppDbApi.TheAppDbApi

        linkUniq src dst |> ignore

        src.CopyUniqueProperties(dst)

        let sbx, dbx = box src, box dst

        match sbx with
        | :? IDsProject ->
            // Project, NjProject, ORMProject
            let propertiesJson =
                match sbx with
                | :? Project    as s -> s.PropertiesJson
                | :? NjProject  as s -> s.Properties.ToJson()
                | :? ORMProject as s -> s.PropertiesJson
                | _ -> fail()
            match dbx with
            | :? Project    as d -> d.PropertiesJson <- propertiesJson
            | :? NjProject  as d -> d.Properties <- JsonPolymorphic.FromJson<ProjectProperties>(propertiesJson)
            | :? ORMProject as d -> d.PropertiesJson <- propertiesJson
            | _ -> fail()

        | :? IDsSystem ->
            // System, NjSystem, ORMSystem
            let s =
                match sbx with
                | :? DsSystem  as s ->
                    {| IRI=s.IRI; Properties=s.PropertiesJson |}
                | :? NjSystem  as s ->
                    {| IRI=s.IRI; Properties=s.Properties.ToJson() |}
                | :? ORMSystem as s ->
                    {| IRI=s.IRI; Properties=s.PropertiesJson |}
                | _ -> fail()

            match dbx with
            | :? DsSystem  as d ->
                d.IRI <- s.IRI
                d.PropertiesJson <- s.Properties
            | :? NjSystem  as d ->
                d.IRI <- s.IRI
                let props = JsonPolymorphic.FromJson<DsSystemProperties>(s.Properties)
                props.RawParent <- Some (d :> Unique)
                d.Properties <- props
            | :? ORMSystem as d ->
                d.IRI <- s.IRI
                d.PropertiesJson <- s.Properties
            | _ -> fail()


        | :? IDsFlow  ->
            let propertiesJson =
                match sbx with
                | :? Flow    as f -> f.PropertiesJson
                | :? NjFlow  as f -> f.Properties.ToJson()
                | :? ORMFlow as f -> f.PropertiesJson
                | _ -> fail()

            match dbx with
            | :? Flow    as d -> d.PropertiesJson <- propertiesJson
            | :? NjFlow  as d -> d.Properties <- assignFromJson d FlowProperties.Create propertiesJson
            | :? ORMFlow as d -> d.PropertiesJson <- propertiesJson
            | _ -> fail()

        | :? IDsWork ->
            // Work, NjWork, ORMWork
            noop()
            let s =
                match sbx with
                | :? Work    as w -> {| Motion=w.Motion; Script=w.Script; ExternalStart=w.ExternalStart; IsFinished=w.IsFinished; NumRepeat=w.NumRepeat; Period=w.Period; Delay=w.Delay; FlowGuid=w.FlowGuid; Properties=w.PropertiesJson |}
                | :? NjWork  as w -> {| Motion=w.Motion; Script=w.Script; ExternalStart=w.ExternalStart; IsFinished=w.IsFinished; NumRepeat=w.NumRepeat; Period=w.Period; Delay=w.Delay; FlowGuid=w.FlowGuid |> Option.ofObj |-> s2guid; Properties=w.Properties.ToJson() |}
                | :? ORMWork as w -> {| Motion=w.Motion; Script=w.Script; ExternalStart=w.ExternalStart; IsFinished=w.IsFinished; NumRepeat=w.NumRepeat; Period=w.Period; Delay=w.Delay; FlowGuid=w.FlowGuid; Properties=w.PropertiesJson |}
                | _ -> fail()

            match dbx with
            | :? Work    as d -> d.Motion<-s.Motion; d.Script<-s.Script; d.ExternalStart<-s.ExternalStart; d.IsFinished<-s.IsFinished; d.NumRepeat<-s.NumRepeat; d.Period<-s.Period; d.Delay<-s.Delay; d.FlowGuid<-s.FlowGuid; d.PropertiesJson <- s.Properties
            | :? NjWork  as d -> d.Motion<-s.Motion; d.Script<-s.Script; d.ExternalStart<-s.ExternalStart; d.IsFinished<-s.IsFinished; d.NumRepeat<-s.NumRepeat; d.Period<-s.Period; d.Delay<-s.Delay; d.FlowGuid<-s.FlowGuid |-> guid2str |> Option.toObj; d.Properties <- assignFromJson d WorkProperties.Create s.Properties
            | :? ORMWork as d -> d.Motion<-s.Motion; d.Script<-s.Script; d.ExternalStart<-s.ExternalStart; d.IsFinished<-s.IsFinished; d.NumRepeat<-s.NumRepeat; d.Period<-s.Period; d.Delay<-s.Delay; d.FlowGuid<-s.FlowGuid; d.PropertiesJson <- s.Properties
            | _ -> fail()

        | :? IDsCall ->
            // sbx와 dbx 타입에 따라 속성 복사 및 타입 변환 처리
            //let getStatus4Id (s4:DbStatus4 option):Id option = s4 >>= theDbApi.TryFindEnumValueId<DbStatus4>
            let getStatus4Id (s4:DbStatus4 option):Id option = s4 >>= DbApi.TryGetEnumId<DbStatus4>
            let getStatus    (s4:Id option):DbStatus4 option = s4 >>= DbApi.TryGetEnumValue<DbStatus4>
            match sbx with
            | :? Call as s ->
                match dbx with
                | :? Call    as d -> d.IsDisabled<-s.IsDisabled; d.Timeout<-s.Timeout; d.AutoConditions   <-s.AutoConditions;          d.CommonConditions<-s.CommonConditions;          d.Status4<-s.Status4; d.PropertiesJson <- s.PropertiesJson
                | :? NjCall  as d -> d.IsDisabled<-s.IsDisabled; d.Timeout<-s.Timeout; d.AutoConditionsObj<-s.AutoConditions;          d.CommonConditionsObj<-s.CommonConditions;       d.Status4<-s.Status4; d.Properties <- assignFromJson d CallProperties.Create s.PropertiesJson
                | :? ORMCall as d -> d.IsDisabled<-s.IsDisabled; d.Timeout<-s.Timeout; d.AutoConditions   <-s.AutoConditions.ToJson(); d.CommonConditions<-s.CommonConditions.ToJson(); d.Status4Id<-getStatus4Id s.Status4; d.PropertiesJson <- s.PropertiesJson
                | _ -> fail()
            | :? NjCall as s ->
                match dbx with
                | :? Call    as d -> d.IsDisabled<-s.IsDisabled; d.Timeout<-s.Timeout; d.AutoConditions   <-s.AutoConditionsObj;          d.CommonConditions<-s.CommonConditionsObj;       d.Status4<-s.Status4; d.PropertiesJson <- s.Properties.ToJson()
                | :? NjCall  as d -> d.IsDisabled<-s.IsDisabled; d.Timeout<-s.Timeout; d.AutoConditionsObj<-s.AutoConditionsObj;          d.CommonConditionsObj<-s.CommonConditionsObj;    d.Status4<-s.Status4; d.Properties <- cloneProperties d s.Properties CallProperties.Create
                | :? ORMCall as d -> d.IsDisabled<-s.IsDisabled; d.Timeout<-s.Timeout; d.AutoConditions   <-s.AutoConditionsObj.ToJson(); d.CommonConditions<-s.CommonConditionsObj.ToJson(); d.Status4Id<-getStatus4Id s.Status4; d.PropertiesJson <- s.Properties.ToJson()
                | _ -> fail()
            | :? ORMCall as s ->
                let fj conditions = ApiCallValueSpecs.FromJson(conditions)
                match dbx with
                | :? Call    as d -> d.IsDisabled<-s.IsDisabled; d.Timeout<-s.Timeout; d.AutoConditions<-fj s.AutoConditions;    d.CommonConditions   <-fj s.CommonConditions;    d.Status4<-getStatus s.Status4Id; d.PropertiesJson <- s.PropertiesJson
                | :? NjCall  as d -> d.IsDisabled<-s.IsDisabled; d.Timeout<-s.Timeout; d.AutoConditionsObj<-fj s.AutoConditions; d.CommonConditionsObj<-fj s.CommonConditions;    d.Status4<-getStatus s.Status4Id; d.Properties <- assignFromJson d CallProperties.Create s.PropertiesJson
                | :? ORMCall as d -> d.IsDisabled<-s.IsDisabled; d.Timeout<-s.Timeout; d.AutoConditions<-s.AutoConditions;       d.CommonConditions   <-s.CommonConditions;       d.Status4Id<-s.Status4Id; d.PropertiesJson <- s.PropertiesJson
                | _ -> fail()
            | _ -> fail()




        | :? IDsApiCall ->
            // ApiCall, NjApiCall, ORMApiCall ->   // 미처리 : ApiDefGuid, Status4
            let s =
                match sbx with
                | :? ApiCall    as s -> {| InAddress=s.InAddress; OutAddress=s.OutAddress; InSymbol=s.InSymbol; OutSymbol=s.OutSymbol; ValueSpec=s.ValueSpec |-> _.Jsonize() |? null; IOTags=s.IOTags; (*ApiDef;*) |}
                | :? NjApiCall  as s -> {| InAddress=s.InAddress; OutAddress=s.OutAddress; InSymbol=s.InSymbol; OutSymbol=s.OutSymbol; ValueSpec=s.ValueSpec;                         IOTags=s.IOTags; (*ApiDef;*) |}
                | :? ORMApiCall as s -> {| InAddress=s.InAddress; OutAddress=s.OutAddress; InSymbol=s.InSymbol; OutSymbol=s.OutSymbol; ValueSpec=s.ValueSpec;                         IOTags=IOTagsWithSpec.FromJson s.IOTagsJson |}
                | _ -> fail()

            match dbx with
            | :? ApiCall    as d -> d.InAddress<-s.InAddress; d.OutAddress<-s.OutAddress; d.InSymbol<-s.InSymbol; d.OutSymbol<-s.OutSymbol; d.ValueSpec<-s.ValueSpec |> Option.ofObj |-> deserializeWithType; d.IOTags<-s.IOTags
            | :? NjApiCall  as d -> d.InAddress<-s.InAddress; d.OutAddress<-s.OutAddress; d.InSymbol<-s.InSymbol; d.OutSymbol<-s.OutSymbol; d.ValueSpec<-s.ValueSpec;                                         d.IOTags<-s.IOTags
            | :? ORMApiCall as d -> d.InAddress<-s.InAddress; d.OutAddress<-s.OutAddress; d.InSymbol<-s.InSymbol; d.OutSymbol<-s.OutSymbol; d.ValueSpec<-s.ValueSpec;                                         d.IOTagsJson<-IOTagsWithSpec.Jsonize s.IOTags
            | _ -> fail()

        | :? IDsApiDef ->
            // ApiDef, NjApiDef, ORMApiDef) ->   // 미처리 : ApiApiDefs, Status4
            let s =
                match sbx with
                | :? ApiDef    as s -> s.PropertiesJson
                | :? NjApiDef  as s -> s.Properties.ToJson()
                | :? ORMApiDef as s -> s.PropertiesJson
                | _ -> fail()

            match dbx with
            | :? ApiDef    as d -> d.PropertiesJson <- s
            | :? NjApiDef  as d -> d.Properties <- assignFromJson d ApiDefProperties.Create s
            | :? ORMApiDef as d -> d.PropertiesJson <- s
            | _ -> fail()

        | :? BLCABase as srcBlca ->
            match dbx with
            | :? BLCABase as dstBlca ->
                dstBlca.SetPropertiesJson(srcBlca.GetPropertiesJson())
            | _ -> fail()

        (*
            ArrowBetweenCalls, ArrowBetweenWorks
            ORMArrowCall, ORMArrowWork
            NjArrow,
        *)
        | :? IArrow ->
            let parseEnum (s:string) : DbArrowType =
                match Enum.TryParse<DbArrowType>(s) with
                | true, v -> v
                | _ -> failwith $"Cannot parse {s}"
            let getEnumIdFromString (s:string) = s |> parseEnum |> DbApi.GetEnumId

            let s =
                match sbx with
                | :? ArrowBetweenWorks as s -> {| SourceGuid=s.XSourceGuid;   TargetGuid=s.XTargetGuid;   Type=s.Type; TypeId=s.XTypeId |}
                | :? ArrowBetweenCalls as s -> {| SourceGuid=s.XSourceGuid;   TargetGuid=s.XTargetGuid;   Type=s.Type; TypeId=s.XTypeId |}
                | :? ORMArrowWork      as s -> {| SourceGuid=s.XSourceGuid;   TargetGuid=s.XTargetGuid;   Type=s.XType; TypeId=s.TypeId |}
                | :? ORMArrowCall      as s -> {| SourceGuid=s.XSourceGuid;   TargetGuid=s.XTargetGuid;   Type=s.XType; TypeId=s.TypeId |}
                | :? NjArrow           as s -> {| SourceGuid=s2guid s.Source; TargetGuid=s2guid s.Target; Type=parseEnum(s.Type); TypeId=getEnumIdFromString(s.Type) |}
                | _ -> fail()

            match dbx with
            | :? ArrowBetweenWorks as d -> d.XSourceGuid<-s.SourceGuid; d.XTargetGuid<-s.TargetGuid; d.Type<-s.Type; //d.TypeId<-s.TypeId
            | :? ArrowBetweenCalls as d -> d.XSourceGuid<-s.SourceGuid; d.XTargetGuid<-s.TargetGuid; d.Type<-s.Type; //d.TypeId<-s.TypeId
            | :? ORMArrowWork      as d -> d.XSourceGuid<-s.SourceGuid; d.XTargetGuid<-s.TargetGuid; d.XType<-s.Type; d.TypeId<-s.TypeId
            | :? ORMArrowCall      as d -> d.XSourceGuid<-s.SourceGuid; d.XTargetGuid<-s.TargetGuid; d.XType<-s.Type; d.TypeId<-s.TypeId
            | :? NjArrow           as d -> d.XSourceGuid<-s.SourceGuid; d.XTargetGuid<-s.TargetGuid; d.Type<-s.Type.ToString(); d.XTypeId<-s.TypeId
            | _ -> fail()

            ()

        | _ -> fail()


        (* 특별 case 처리 *)
        match sbx, dbx with
        | (:? ApiCall as s), (:?NjApiCall as d) ->
            d.ApiDef <- s.ApiDefGuid
        | (:? NjApiCall as s), (:? ApiCall as d) ->
            d.ApiDefGuid <- s.ApiDef

        | _ -> ()


        getTypeFactory()
        |> iter(fun factory -> factory.CopyProperties(src, dst))

        dst


    let getPropertyNameForDB(dbApi:DbApi, propertyName: string) : string =
        match propertyName with
        | "Parameter" | "ValueSpec" -> $"{propertyName}{dbApi.DapperJsonB}"
        | "properties" -> $"PropertiesJson{dbApi.DapperJsonB}"
        | _ -> propertyName
