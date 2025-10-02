namespace Ev2.Core.FS

open log4net

open Dual.Common.Base
open Dual.Common.Db.FS
open System
open System.Linq
open System.IO
open Dapper

module ModuleInitializer =
    let private createProjectProperties(json:string) =
        json
        |> String.toOption
        |-> JsonPolymorphic.FromJson<ProjectProperties>
        |?? ProjectProperties.Create
    let private createSystemProperties(json:string) =
        json
        |> String.toOption
        |-> JsonPolymorphic.FromJson<DsSystemProperties>
        |?? DsSystemProperties.Create

    let mutable private initailized = false
    let Initialize(logger: ILog) =
        if not initailized then
            initailized <- true

            Dual.Common.Base.FS.ModuleInitializer.Initialize(logger)

            fwdOnNsJsonSerializing <- onNsJsonSerializing
            fwdOnNsJsonDeserialized <- onNsJsonDeserialized
            fwdDuplicate <- duplicateUnique
            fwdReplicate <- replicateUnique

            fwdRtObj2NjObj <- rtObj2NjObj

            fwdReplicateProperties <- replicatePropertiesImpl

            fwdProjectFromJson <-
                fun json ->
                    let proj = Project.fromJson json
                    proj.Systems |> iter (fun sys -> sys.Entities |> iter (fun entity -> entity.RawParent <- Some sys ))
                    proj
            fwdEnumerateRtObjects <- fun (rtObj: IRtUnique) -> (rtObj :?> RtUnique).EnumerateRtObjects().Cast<IRtUnique>()

            fwdValueSpecFromString <- fun text -> ValueRangeModule.parseValueSpec text
            fwdValueSpecFromJson <- fun json -> ValueRangeModule.deserializeWithType json

            fwdSetDateTime <-
                fun (obj: IWithDateTime) (dt: DateTime) ->
                    match obj with
                    | :? Project    as z -> z.Properties.DateTime <- dt
                    | :? DsSystem   as z -> z.Properties.DateTime <- dt
                    | :? NjProject  as z -> z.Properties.DateTime <- dt
                    | :? NjSystem   as z -> z.Properties.DateTime <- dt
                    | :? ORMProject as z -> let props = createProjectProperties z.PropertiesJson in props.DateTime <- dt; z.PropertiesJson <- props.ToJson()
                    | :? ORMSystem  as z -> let props = createSystemProperties  z.PropertiesJson in props.DateTime <- dt; z.PropertiesJson <- props.ToJson()
                    | _ -> fail()

            fwdGetDateTime <-
                fun (obj: IWithDateTime) ->
                    match obj with
                    | :? Project    as z -> z.Properties.DateTime
                    | :? DsSystem   as z -> z.Properties.DateTime
                    | :? NjProject  as z -> z.Properties.DateTime
                    | :? NjSystem   as z -> z.Properties.DateTime
                    | :? ORMProject as z -> (createProjectProperties z.PropertiesJson).DateTime
                    | :? ORMSystem  as z -> (createSystemProperties z.PropertiesJson).DateTime
                    | _ -> fail()


            let appSettings =
                let json =
                    let baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    Path.Combine(baseDir, "appsettings.json")
                    |> File.ReadAllText
                EmJson.FromJson<AppSettings>(json)
                |> tee (fun settings ->
                    if isItNull settings.DbProvider || settings.DbProvider.ConnectionString.IsNullOrEmpty() then
                        failwith "Database setting is missing or incorrect in appSettings.json")


            setDapperTypeMapping<Project>()
            setDapperTypeMapping<DsSystem>()
            setDapperTypeMapping<Flow>()
            setDapperTypeMapping<Work>()
            setDapperTypeMapping<Call>()
            setDapperTypeMapping<ApiCall>()
            setDapperTypeMapping<ApiDef>()
            setDapperTypeMapping<ArrowBetweenCalls>()
            setDapperTypeMapping<ArrowBetweenWorks>()

            let dbApi = AppDbApi(appSettings.DbProvider)
            dbApi.With(fun (conn, tr) ->
                conn.Execute($"DELETE FROM {Tn.TableHistory}", null, tr) |> ignore
                conn.ResetSequence(Tn.TableHistory, tr)
            )

            ()
