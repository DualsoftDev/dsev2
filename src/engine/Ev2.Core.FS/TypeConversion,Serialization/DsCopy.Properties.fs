namespace Ev2.Core.FS

open Dual.Common.Core.FS
open Dual.Common.Base
open Dual.Common.Db.FS

[<AutoOpen>]
module internal DsCopyModule =


    /// fwdDuplicate <- duplicateUnique
    let duplicateUnique (source:IUnique): IUnique =
        match source with
        | :? RtSystem  as rs -> rs.Duplicate()
        | :? RtProject as rp -> rp.Duplicate($"CC_{rp.Name}")
        | _ -> failwithf "Unsupported type for duplication: %A" (source.GetType())


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

        dst.Id <- src.Id
        dst.Name <- src.Name
        dst.Guid <- src.Guid
        dst.Parameter <- src.Parameter
        dst.RawParent <- src.RawParent

        let sbx, dbx = box src, box dst

        match sbx with
        | (:? RtProject) | (:? NjProject) | (:? ORMProject) ->
            let s =
                match sbx with
                | :? RtProject  as s -> {| Author=s.Author; Version=s.Version; Description=s.Description; Database=s.Database; DateTime=s.DateTime |}
                | :? NjProject  as s -> {| Author=s.Author; Version=s.Version; Description=s.Description; Database=s.Database; DateTime=s.DateTime |}
                | :? ORMProject as s -> {| Author=s.Author; Version=s.Version; Description=s.Description; Database=getNull<DbProvider>(); DateTime=s.DateTime |}
                | _ -> failwith "ERROR"
            match dbx with
            | :? RtProject  as d -> d.Author<-s.Author; d.Version<-s.Version; d.Description<-s.Description; d.Database<-s.Database; d.DateTime<-s.DateTime
            | :? NjProject  as d -> d.Author<-s.Author; d.Version<-s.Version; d.Description<-s.Description; d.Database<-s.Database; d.DateTime<-s.DateTime
            | :? ORMProject as d -> d.Author<-s.Author; d.Version<-s.Version; d.Description<-s.Description; (*d.Database<-s.Database;*) d.DateTime<-s.DateTime
            | _ -> failwith "ERROR"

        | (:? RtSystem) | (:? NjSystem) | (:? ORMSystem) ->
            let s =
                match sbx with
                | :? RtSystem  as s -> {| IRI=s.IRI; OriginGuid=s.OriginGuid; Author=s.Author; EngineVersion=s.EngineVersion; LangVersion=s.LangVersion; Description=s.Description; DateTime=s.DateTime |}
                | :? NjSystem  as s -> {| IRI=s.IRI; OriginGuid=s.OriginGuid; Author=s.Author; EngineVersion=s.EngineVersion; LangVersion=s.LangVersion; Description=s.Description; DateTime=s.DateTime |}
                | :? ORMSystem as s -> {| IRI=s.IRI; OriginGuid=s.OriginGuid; Author=s.Author; EngineVersion=s.EngineVersion; LangVersion=s.LangVersion; Description=s.Description; DateTime=s.DateTime |}
                | _ -> failwith "ERROR"

            match dbx with
            | :? RtSystem  as d -> d.IRI<-s.IRI; d.OriginGuid<-s.OriginGuid; d.Author<-s.Author; d.EngineVersion<-s.EngineVersion; d.LangVersion<-s.LangVersion; d.Description<-s.Description; d.DateTime<-s.DateTime
            | :? NjSystem  as d -> d.IRI<-s.IRI; d.OriginGuid<-s.OriginGuid; d.Author<-s.Author; d.EngineVersion<-s.EngineVersion; d.LangVersion<-s.LangVersion; d.Description<-s.Description; d.DateTime<-s.DateTime
            | :? ORMSystem as d -> d.IRI<-s.IRI; d.OriginGuid<-s.OriginGuid; d.Author<-s.Author; d.EngineVersion<-s.EngineVersion; d.LangVersion<-s.LangVersion; d.Description<-s.Description; d.DateTime<-s.DateTime
            | _ -> failwith "ERROR"


        | (:? RtFlow) | (:? NjFlow) | (:? ORMFlow) ->
            // 특별히 복사할 것 없음.
            ()


        | (:? RtWork) | (:? NjWork) | (:? ORMWork) ->
            let s =
                match sbx with
                | :? RtWork  as s -> {| Motion=s.Motion; Script=s.Script; IsFinished=s.IsFinished; NumRepeat=s.NumRepeat; Period=s.Period; Delay=s.Delay; (*Status4=s.Status4*) |}
                | :? NjWork  as s -> {| Motion=s.Motion; Script=s.Script; IsFinished=s.IsFinished; NumRepeat=s.NumRepeat; Period=s.Period; Delay=s.Delay; (*Status4=s.Status4*) |}
                | :? ORMWork as s -> {| Motion=s.Motion; Script=s.Script; IsFinished=s.IsFinished; NumRepeat=s.NumRepeat; Period=s.Period; Delay=s.Delay; (*Status4=s.Status4*) |}
                | _ -> failwith "ERROR"

            match dbx with
            | :? RtWork  as d -> d.Motion<-s.Motion; d.Script<-s.Script; d.IsFinished<-s.IsFinished; d.NumRepeat<-s.NumRepeat; d.Period<-s.Period; d.Delay<-s.Delay;
            | :? NjWork  as d -> d.Motion<-s.Motion; d.Script<-s.Script; d.IsFinished<-s.IsFinished; d.NumRepeat<-s.NumRepeat; d.Period<-s.Period; d.Delay<-s.Delay;
            | :? ORMWork as d -> d.Motion<-s.Motion; d.Script<-s.Script; d.IsFinished<-s.IsFinished; d.NumRepeat<-s.NumRepeat; d.Period<-s.Period; d.Delay<-s.Delay;
            | _ -> failwith "ERROR"


        | (:? RtCall) | (:? NjCall) | (:? ORMCall) ->   // 미처리 : ApiCalls, Status4
            let fj s = EmJson.FromJson<ResizeArray<string>> s
            let tj s = EmJson.ToJson s
            let s =
                match sbx with
                | :? RtCall  as s -> {| IsDisabled=s.IsDisabled; Timeout=s.Timeout; AutoConditions=s.AutoConditions|>tj; CommonConditions=s.CommonConditions|>tj; (*ApiCall=s.ApiCall; Status4*) |}
                | :? NjCall  as s -> {| IsDisabled=s.IsDisabled; Timeout=s.Timeout; AutoConditions=s.AutoConditions;     CommonConditions=s.CommonConditions;     (*ApiCall=s.ApiCall; Status4*) |}
                | :? ORMCall as s -> {| IsDisabled=s.IsDisabled; Timeout=s.Timeout; AutoConditions=s.AutoConditions;     CommonConditions=s.CommonConditions;     (*ApiCall=s.ApiCall; Status4*) |}
                | _ -> failwith "ERROR"

            match dbx with
            | :? RtCall  as d -> d.IsDisabled<-s.IsDisabled; d.Timeout<-s.Timeout; d.AutoConditions<-s.AutoConditions|>fj; d.CommonConditions<-s.CommonConditions|>fj;
            | :? NjCall  as d -> d.IsDisabled<-s.IsDisabled; d.Timeout<-s.Timeout; d.AutoConditions<-s.AutoConditions;     d.CommonConditions<-s.CommonConditions;
            | :? ORMCall as d -> d.IsDisabled<-s.IsDisabled; d.Timeout<-s.Timeout; d.AutoConditions<-s.AutoConditions;     d.CommonConditions<-s.CommonConditions;
            | _ -> failwith "ERROR"




        | (:? RtApiCall) | (:? NjApiCall) | (:? ORMApiCall) ->   // 미처리 : ApiDefGuid, Status4
            let s =
                match sbx with
                | :? RtApiCall  as s -> {| InAddress=s.InAddress; OutAddress=s.OutAddress; InSymbol=s.InSymbol; OutSymbol=s.OutSymbol; ValueSpec=s.ValueSpec |-> _.Jsonize() |? null; (*ApiDef;*) |}
                | :? NjApiCall  as s -> {| InAddress=s.InAddress; OutAddress=s.OutAddress; InSymbol=s.InSymbol; OutSymbol=s.OutSymbol; ValueSpec=s.ValueSpec; (*ApiDef;*) |}
                | :? ORMApiCall as s -> {| InAddress=s.InAddress; OutAddress=s.OutAddress; InSymbol=s.InSymbol; OutSymbol=s.OutSymbol; ValueSpec=s.ValueSpec; (*ApiDef;*) |}
                | _ -> failwith "ERROR"

            match dbx with
            | :? RtApiCall  as d -> d.InAddress<-s.InAddress; d.OutAddress<-s.OutAddress; d.InSymbol<-s.InSymbol; d.OutSymbol<-s.OutSymbol; d.ValueSpec<-s.ValueSpec |> Option.ofObj |-> deserializeWithType
            | :? NjApiCall  as d -> d.InAddress<-s.InAddress; d.OutAddress<-s.OutAddress; d.InSymbol<-s.InSymbol; d.OutSymbol<-s.OutSymbol; d.ValueSpec<-s.ValueSpec
            | :? ORMApiCall as d -> d.InAddress<-s.InAddress; d.OutAddress<-s.OutAddress; d.InSymbol<-s.InSymbol; d.OutSymbol<-s.OutSymbol; d.ValueSpec<-s.ValueSpec
            | _ -> failwith "ERROR"


        | (:? RtApiDef) | (:? NjApiDef) | (:? ORMApiDef) ->   // 미처리 : ApiApiDefs, Status4
            let s =
                match sbx with
                | :? RtApiDef  as s -> {| IsPush=s.IsPush |}
                | :? NjApiDef  as s -> {| IsPush=s.IsPush |}
                | :? ORMApiDef as s -> {| IsPush=s.IsPush |}
                | _ -> failwith "ERROR"

            match dbx with
            | :? RtApiDef  as d -> d.IsPush<-s.IsPush
            | :? NjApiDef  as d -> d.IsPush<-s.IsPush
            | :? ORMApiDef as d -> d.IsPush<-s.IsPush
            | _ -> failwith "ERROR"






        | (:? RtButton) | (:? NjButton) | (:? ORMButton) ->  ()
        | (:? RtLamp) | (:? NjLamp) | (:? ORMLamp) ->  ()
        | (:? RtCondition) | (:? NjCondition) | (:? ORMCondition) -> ()
        | (:? RtAction) | (:? NjAction) | (:? ORMAction) ->  ()

        | ( :? RtArrowBetweenCalls | :? RtArrowBetweenWorks
          | :? ORMArrowCall | :? ORMArrowWork
          | :? NjArrow ) -> ()

        | _ -> failwith "ERROR"


        dst
