namespace Ev2.Core.FS

open Dual.Common.Core.FS
open Dual.Common.Base
open Dual.Common.Db.FS

[<AutoOpen>]
module internal DsCopyModule =


    /// fwdDuplicate <- duplicateUnique
    let duplicateUnique (source:IUnique): IUnique =
        match source with
        | :? DsSystem  as rs -> rs.Duplicate()
        | :? Project as rp -> rp.Duplicate($"CC_{rp.Name}")
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
        | _  -> failwith "ERROR"

        match box dst with
        | :? IRtUnique  as d -> src.RtObject  <- Some d
        | :? INjUnique  as d -> src.NjObject  <- Some d
        | :? IORMUnique as d -> src.ORMObject <- Some d
        | _  -> failwith "ERROR"

        dst

    /// src Unique 객체의 속성정보 (Id, Name, Guid, DateTime)를 복사해서 dst 의 Unique 객체에 저장
    let replicatePropertiesImpl (src:#Unique) (dst:#Unique) : #Unique =

        linkUniq src dst |> ignore

        src.CopyUniqueProperties(dst)

        let sbx, dbx = box src, box dst

        match sbx with
        | (:? Project) | (:? NjProject) | (:? ORMProject) ->
            let s =
                match sbx with
                | :? Project    as s -> {| Author=s.Author; Version=s.Version; Description=s.Description; Database=s.Database; DateTime=s.DateTime |}
                | :? NjProject  as s -> {| Author=s.Author; Version=s.Version; Description=s.Description; Database=s.Database; DateTime=s.DateTime |}
                | :? ORMProject as s -> {| Author=s.Author; Version=s.Version; Description=s.Description; Database=getNull<DbProvider>(); DateTime=s.DateTime |}
                | _ -> failwith "ERROR"
            match dbx with
            | :? Project    as d -> d.Author<-s.Author; d.Version<-s.Version; d.Description<-s.Description; d.Database<-s.Database; d.DateTime<-s.DateTime
            | :? NjProject  as d -> d.Author<-s.Author; d.Version<-s.Version; d.Description<-s.Description; d.Database<-s.Database; d.DateTime<-s.DateTime
            | :? ORMProject as d -> d.Author<-s.Author; d.Version<-s.Version; d.Description<-s.Description; (*d.Database<-s.Database;*) d.DateTime<-s.DateTime
            | _ -> failwith "ERROR"

        | (:? DsSystem) | (:? NjSystem) | (:? ORMSystem) ->
            let s =
                match sbx with
                | :? DsSystem  as s -> {| IRI=s.IRI; Author=s.Author; EngineVersion=s.EngineVersion; LangVersion=s.LangVersion; Description=s.Description; DateTime=s.DateTime |}
                | :? NjSystem  as s -> {| IRI=s.IRI; Author=s.Author; EngineVersion=s.EngineVersion; LangVersion=s.LangVersion; Description=s.Description; DateTime=s.DateTime |}
                | :? ORMSystem as s -> {| IRI=s.IRI; Author=s.Author; EngineVersion=s.EngineVersion; LangVersion=s.LangVersion; Description=s.Description; DateTime=s.DateTime |}
                | _ -> failwith "ERROR"

            match dbx with
            | :? DsSystem  as d -> d.IRI<-s.IRI; d.Author<-s.Author; d.EngineVersion<-s.EngineVersion; d.LangVersion<-s.LangVersion; d.Description<-s.Description; d.DateTime<-s.DateTime
            | :? NjSystem  as d -> d.IRI<-s.IRI; d.Author<-s.Author; d.EngineVersion<-s.EngineVersion; d.LangVersion<-s.LangVersion; d.Description<-s.Description; d.DateTime<-s.DateTime
            | :? ORMSystem as d -> d.IRI<-s.IRI; d.Author<-s.Author; d.EngineVersion<-s.EngineVersion; d.LangVersion<-s.LangVersion; d.Description<-s.Description; d.DateTime<-s.DateTime
            | _ -> failwith "ERROR"


        | (:? Flow) | (:? NjFlow) | (:? ORMFlow) ->
            // 특별히 복사할 것 없음.
            ()


        | (:? Work) | (:? NjWork) | (:? ORMWork) ->
            let s =
                match sbx with
                | :? Work    as s -> {| Motion=s.Motion; Script=s.Script; IsFinished=s.IsFinished; NumRepeat=s.NumRepeat; Period=s.Period; Delay=s.Delay; (*Status4=s.Status4*) |}
                | :? NjWork  as s -> {| Motion=s.Motion; Script=s.Script; IsFinished=s.IsFinished; NumRepeat=s.NumRepeat; Period=s.Period; Delay=s.Delay; (*Status4=s.Status4*) |}
                | :? ORMWork as s -> {| Motion=s.Motion; Script=s.Script; IsFinished=s.IsFinished; NumRepeat=s.NumRepeat; Period=s.Period; Delay=s.Delay; (*Status4=s.Status4*) |}
                | _ -> failwith "ERROR"

            match dbx with
            | :? Work    as d -> d.Motion<-s.Motion; d.Script<-s.Script; d.IsFinished<-s.IsFinished; d.NumRepeat<-s.NumRepeat; d.Period<-s.Period; d.Delay<-s.Delay;
            | :? NjWork  as d -> d.Motion<-s.Motion; d.Script<-s.Script; d.IsFinished<-s.IsFinished; d.NumRepeat<-s.NumRepeat; d.Period<-s.Period; d.Delay<-s.Delay;
            | :? ORMWork as d -> d.Motion<-s.Motion; d.Script<-s.Script; d.IsFinished<-s.IsFinished; d.NumRepeat<-s.NumRepeat; d.Period<-s.Period; d.Delay<-s.Delay;
            | _ -> failwith "ERROR"


        | (:? Call) | (:? NjCall) | (:? ORMCall) ->   // 미처리 : ApiCalls, Status4
            /// From Json
            let fj (s:string) = if s.IsNullOrEmpty() then ResizeArray() else EmJson.FromJson<ResizeArray<string>> s
            /// To Json
            let tj obj = EmJson.ToJson obj
            let s =
                match sbx with
                | :? Call    as s -> {| IsDisabled=s.IsDisabled; Timeout=s.Timeout; AutoConditions=s.AutoConditions|>tj; CommonConditions=s.CommonConditions|>tj; Status4=s.Status4;     (*ApiCall=s.ApiCall; Status4*) |}
                | :? NjCall  as s -> {| IsDisabled=s.IsDisabled; Timeout=s.Timeout; AutoConditions=s.AutoConditions;     CommonConditions=s.CommonConditions;     Status4=s.Status4;     (*ApiCall=s.ApiCall; Status4*) |}
                | :? ORMCall as s -> {| IsDisabled=s.IsDisabled; Timeout=s.Timeout; AutoConditions=s.AutoConditions;     CommonConditions=s.CommonConditions;     Status4=None           (*ApiCall=s.ApiCall; Status4*) |}
                | _ -> failwith "ERROR"

            match dbx with
            | :? Call    as d -> d.IsDisabled<-s.IsDisabled; d.Timeout<-s.Timeout; d.AutoConditions<-s.AutoConditions|>fj; d.CommonConditions<-s.CommonConditions|>fj;  d.Status4<-s.Status4
            | :? NjCall  as d -> d.IsDisabled<-s.IsDisabled; d.Timeout<-s.Timeout; d.AutoConditions<-s.AutoConditions;     d.CommonConditions<-s.CommonConditions;      d.Status4<-s.Status4
            | :? ORMCall as d -> d.IsDisabled<-s.IsDisabled; d.Timeout<-s.Timeout; d.AutoConditions<-s.AutoConditions;     d.CommonConditions<-s.CommonConditions;      (*No status handling*)
            | _ -> failwith "ERROR"




        | (:? ApiCall) | (:? NjApiCall) | (:? ORMApiCall) ->   // 미처리 : ApiDefGuid, Status4
            let s =
                match sbx with
                | :? ApiCall    as s -> {| InAddress=s.InAddress; OutAddress=s.OutAddress; InSymbol=s.InSymbol; OutSymbol=s.OutSymbol; ValueSpec=s.ValueSpec |-> _.Jsonize() |? null; (*ApiDef;*) |}
                | :? NjApiCall  as s -> {| InAddress=s.InAddress; OutAddress=s.OutAddress; InSymbol=s.InSymbol; OutSymbol=s.OutSymbol; ValueSpec=s.ValueSpec; (*ApiDef;*) |}
                | :? ORMApiCall as s -> {| InAddress=s.InAddress; OutAddress=s.OutAddress; InSymbol=s.InSymbol; OutSymbol=s.OutSymbol; ValueSpec=s.ValueSpec; (*ApiDef;*) |}
                | _ -> failwith "ERROR"

            match dbx with
            | :? ApiCall    as d -> d.InAddress<-s.InAddress; d.OutAddress<-s.OutAddress; d.InSymbol<-s.InSymbol; d.OutSymbol<-s.OutSymbol; d.ValueSpec<-s.ValueSpec |> Option.ofObj |-> deserializeWithType
            | :? NjApiCall  as d -> d.InAddress<-s.InAddress; d.OutAddress<-s.OutAddress; d.InSymbol<-s.InSymbol; d.OutSymbol<-s.OutSymbol; d.ValueSpec<-s.ValueSpec
            | :? ORMApiCall as d -> d.InAddress<-s.InAddress; d.OutAddress<-s.OutAddress; d.InSymbol<-s.InSymbol; d.OutSymbol<-s.OutSymbol; d.ValueSpec<-s.ValueSpec
            | _ -> failwith "ERROR"


        | (:? ApiDef) | (:? NjApiDef) | (:? ORMApiDef) ->   // 미처리 : ApiApiDefs, Status4
            let s =
                match sbx with
                | :? ApiDef    as s -> {| IsPush=s.IsPush |}
                | :? NjApiDef  as s -> {| IsPush=s.IsPush |}
                | :? ORMApiDef as s -> {| IsPush=s.IsPush |}
                | _ -> failwith "ERROR"

            match dbx with
            | :? ApiDef    as d -> d.IsPush<-s.IsPush
            | :? NjApiDef  as d -> d.IsPush<-s.IsPush
            | :? ORMApiDef as d -> d.IsPush<-s.IsPush
            | _ -> failwith "ERROR"






        | (:? DsButton) | (:? NjButton) | (:? ORMButton) ->  ()
        | (:? Lamp) | (:? NjLamp) | (:? ORMLamp) ->  ()
        | (:? DsCondition) | (:? NjCondition) | (:? ORMCondition) -> ()
        | (:? DsAction) | (:? NjAction) | (:? ORMAction) ->  ()

        | ( :? ArrowBetweenCalls | :? ArrowBetweenWorks
          | :? ORMArrowCall | :? ORMArrowWork
          | :? NjArrow ) -> ()

        | _ -> failwith "ERROR"


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

    let private allPropertyNames =
        [| "Id"; "Name"; "Guid"; "DateTime"; "Parameter"; "RawParent";
           "Author"; "Version"; "Description"; "Database";
           "IRI"; "OriginGuid"; "EngineVersion"; "LangVersion"; "OwnerProjectId"
           "Motion"; "Script"; "IsFinished"; "NumRepeat"; "Period"; "Delay"; "Status4"
           "InAddress"; "OutAddress"; "InSymbol"; "OutSymbol";
           "ValueSpec";
           // 확장 속성 추가
           "Location"; "Area" |]
        |> Set.ofArray

    let tryGetDBColumnName(dbApi:DbApi, propertyName: string) : string option =
        allPropertyNames.TryGet(propertyName)
        |-> (fun propertyName ->
                propertyName[0..0].ToLower() + propertyName[1..])    // 첫 문자만 소문자로 변경

    let getPropertyNameForDB(dbApi:DbApi, propertyName: string) : string =
        match propertyName with
        | "Parameter" | "ValueSpec" -> $"{propertyName}{dbApi.DapperJsonB}"
        | _ -> propertyName
