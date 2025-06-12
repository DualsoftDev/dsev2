namespace Ev2.Core.FS

open System
open System.Data
open System.Linq
open Dapper

open Dual.Common.Base
open Dual.Common.Core.FS
open Dual.Common.Db.FS
open System.Diagnostics


[<AutoOpen>]
module internal DbInsertImpl =
    /// IUnique 를 상속하는 객체에 대한 db insert/update 시, 메모리 객체의 Id 를 db Id 로 업데이트
    let idUpdator (targets:IUnique seq) (id:int)=
        for t in targets do
            match t with
            | :? Unique  as a -> a.Id <- Some id
            | _ -> failwith $"Unknown type {t.GetType()} in idUpdator"


    let rTryInsertSystemToDBHelper (dbApi:AppDbApi) (s:RtSystem): DbCommitResult =
        let helper(conn:IDbConnection, tr:IDbTransaction) =
            let ormSystem = s.ToORM<ORMSystem>(dbApi)

            let sysId = conn.Insert($"""INSERT INTO {Tn.System}
                                    (guid, parameter,                     dateTime,  name,  iri, author,     langVersion, engineVersion, description, originGuid, prototypeId)
                            VALUES (@Guid, @Parameter{dbApi.DapperJsonB}, @DateTime, @Name, @IRI, @Author, @LangVersion, @EngineVersion, @Description, @OriginGuid, @PrototypeId);""",
                            ormSystem, tr)

            s.Id <- Some sysId
            let guidDicDebug = dbApi.DDic.Get<Guid2UniqDic>()
            guidDicDebug[s.Guid].Id <- Some sysId

            match s.Project with
            | Some rtp ->
                // project 하부에 연결된 system 을 DB 에 저장

                // s.PrototypeSystemGuid 에 따라 다른 처리???
                //assert(not s.PrototypeSystemGuid.Is)

                // update projectSystemMap table
                let isActive = rtp.ActiveSystems |> Seq.contains s
                let isPassive = rtp.PassiveSystems |> Seq.contains s
                let projId = rtp.Id.Value

                // prototype 이 아니라면, active 나 passive 중 하나만 true 여야 한다.
                assert(isActive <> isPassive || s.PrototypeSystemGuid.IsNone)   // XOR

                match conn.TryQuerySingle<ORMMapProjectSystem>(
                                $"""SELECT * FROM {Tn.MapProject2System}
                                    WHERE projectId = {projId} AND systemId = {sysId}""", transaction=tr) with
                | Some row ->
                    if row.IsActive = isActive then
                        ()
                    else
                        conn.Execute($"""UPDATE {Tn.MapProject2System}
                                         SET active = {isActive}
                                         WHERE id = {row.Id}""", transaction=tr) |> ignore
                | None ->
                    let loadedName = s.PrototypeSystemGuid |-> (fun _ -> s.Name) |? null     // RtSystem.Name 은 loaded name 을 의미한다.
                    let affectedRows = conn.Execute(
                            $"""INSERT INTO {Tn.MapProject2System}
                                        (projectId, systemId, loadedName, isActive, guid)
                                 VALUES (@ProjectId, @SystemId, @LoadedName, @IsActive, @Guid)""",
                            {|  ProjectId = projId; SystemId = sysId; LoadedName=loadedName; IsActive = isActive;
                                Guid=Guid.NewGuid() |}, tr)
                    ()
            | None ->
                // project 와 무관한 system 을 DB 에 저장
                failwith "Not yet implemented: insertSystemToDBHelper without project context"
                ()

            // flows 삽입
            s.Flows |> iter _.InsertToDB(dbApi)

            // system 의 apiDefs 를 삽입
            s.ApiDefs |> iter _.InsertToDB(dbApi)

            // system 의 apiCalls 를 삽입
            s.ApiCalls |> iter _.InsertToDB(dbApi)

            // works 및 하부의 calls 삽입
            s.Works |> iter _.InsertToDB(dbApi)

            // system 의 arrows 를 삽입 (works 간 연결)
            s.Arrows |> iter _.InsertToDB(dbApi)

            Inserted

        try
            Ok (dbApi.With helper)
        with ex ->
            Error <| sprintf "Failed to insert system to DB: %s" ex.Message




    let rTryCommitSystemToDBHelper (dbApi:AppDbApi) (s:RtSystem): DbCommitResult =
        match s.Id with
        | Some id ->             // 이미 DB 에 저장된 system 이므로 update
            rTryCheckoutSystemFromDB id dbApi
            |-> (fun system ->
                let criteria = Cc(parentGuid=false)
                let diffs = system.ComputeDiff(s, criteria) |> toArray

                match diffs with
                | [||] ->   // DB 에 저장된 system 과 동일하므로 변경 없음
                    NoChange
                | _ ->   // DB 에 저장된 system 과 다르므로 update
                    Updated diffs
                )
        | None ->   // DB 에 저장되지 않은 system 이므로 insert
            rTryInsertSystemToDBHelper dbApi s



    /// DsProject 을 database (sqlite or pgsql) 에 저장
    let rTryInsertProjectToDB (proj:RtProject) (dbApi:AppDbApi): DbCommitResult =
        let helper(conn:IDbConnection, tr:IDbTransaction): DbCommitResult =

            Guid2UniqDic() |> dbApi.DDic.Set
            let ormProject = proj.ToORM(dbApi)
            assert (dbApi.DDic.Get<Guid2UniqDic>().Any())

            let projId =
                conn.Insert($"""INSERT INTO {Tn.Project}
                            (guid,   parameter,                     dateTime,  name,  author,  version,  description)
                    VALUES (@Guid, @Parameter{dbApi.DapperJsonB}, @DateTime, @Name, @Author, @Version, @Description);""", ormProject, tr)

            proj.Id <- Some projId

            let firstError =
                proj.Systems
                |-> rTryCommitSystemToDBHelper dbApi
                |> tryPick (function
                    | Error e -> Some e
                    | Ok _ -> None)

            match firstError with
            | Some e ->
                Error e
            | None ->
                //proj.Id <- Some projId
                ormProject.Id <- Some projId
                proj.Database <- dbApi.DbProvider

                Ok Inserted

        try
            dbApi.With helper
        with ex ->
            proj.Id <- None
            Error <| sprintf "Failed to insert project to DB: %s" ex.Message


    let rTryCommitSystemToDB (x:RtSystem) (dbApi:AppDbApi): DbCommitResult =
        dbApi.With(fun (conn, tr) ->
            Guid2UniqDic() |> dbApi.DDic.Set
            let ormSystem = x.ToORM(dbApi)
            assert (dbApi.DDic.Get<Guid2UniqDic>().Any())

            rTryCommitSystemToDBHelper dbApi x)



