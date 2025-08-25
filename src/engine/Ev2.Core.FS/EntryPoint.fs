namespace Ev2.Core.FS

open log4net

open Dual.Common.Base
open Dual.Common.Db.FS
open System
open System.IO
open Dapper

module ModuleInitializer =
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

            fwdProjectFromJson <- fun json -> Project.fromJson json

            let appSettings =
                let json =
                    let baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    Path.Combine(baseDir, "appsettings.json")
                    |> File.ReadAllText
                EmJson.FromJson<AppSettings>(json)
                |> tee (fun settings ->
                    if isItNull settings.DbProvider || settings.DbProvider.ConnectionString.IsNullOrEmpty() then
                        failwith "Database setting is missing or incorrect in appSettings.json")

            let dbApi = AppDbApi(appSettings.DbProvider)
            dbApi.With(fun (conn, tr) ->
                conn.Execute($"DELETE FROM {Tn.TableHistory}", null, tr) |> ignore
                conn.ResetSequence(Tn.TableHistory, tr)
            )

            ()
