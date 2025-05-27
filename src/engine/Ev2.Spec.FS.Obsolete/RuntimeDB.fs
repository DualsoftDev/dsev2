namespace Dual.EV2.RuntimeDB

open System
open System.Collections.Generic
open Microsoft.Data.Sqlite
open Dual.EV2.Core

module TableNames =
    [<Literal>] 
    let tbProject = "project"
    [<Literal>]
    let tbSystem = "system"
    [<Literal>]
    let tbProjectSystemMap = "projectSystemMap"
    [<Literal>]
    let tbWork = "work"
    [<Literal>]
    let tbCall = "call"
    [<Literal>]
    let tbApiCall = "apiCall"
    [<Literal>]
    let tbApiCallMap = "apiCallMap"
    [<Literal>]
    let tbApiDef = "apiDef"
    [<Literal>]
    let tbArrowCall = "arrowCall"
    [<Literal>]
    let tbArrowWork = "arrowWork"

module RuntimeDB =

    type RuntimeDb = {
        Projects: ResizeArray<Project>
        ProjectSystemMap: ResizeArray<ProjectSystemUsage>
        Systems: ResizeArray<System>
        Works: ResizeArray<Work>
        Calls: ResizeArray<Call>
        ApiCallMap: ResizeArray<ApiCallUsage>
        ApiCalls: ResizeArray<ApiCall>
        ApiDefs: ResizeArray<ApiDef>
        WorkArrows: ResizeArray<(Work * Work)>
        CallArrows: ResizeArray<(Call * Call)>
    }

    let initializeSchema (dbPath: string) =
        use conn = new SqliteConnection($"Data Source={dbPath}")
        conn.Open()

        let exec sql =
            use cmd = new SqliteCommand(sql, conn)
            cmd.ExecuteNonQuery() |> ignore

        [
            $"CREATE TABLE IF NOT EXISTS {TableNames.tbProject} (id TEXT PRIMARY KEY, name TEXT)";
            $"CREATE TABLE IF NOT EXISTS {TableNames.tbSystem} (id TEXT PRIMARY KEY, name TEXT)";
            $"CREATE TABLE IF NOT EXISTS {TableNames.tbProjectSystemMap} (id TEXT PRIMARY KEY, projectId TEXT, systemId TEXT, active BOOLEAN)";
            $"CREATE TABLE IF NOT EXISTS {TableNames.tbWork} (id TEXT PRIMARY KEY, name TEXT, systemId TEXT)";
            $"CREATE TABLE IF NOT EXISTS {TableNames.tbCall} (id TEXT PRIMARY KEY, name TEXT, workId TEXT)";
            $"CREATE TABLE IF NOT EXISTS {TableNames.tbApiCall} (id TEXT PRIMARY KEY, name TEXT, callId TEXT, apiDefId TEXT)";
            $"CREATE TABLE IF NOT EXISTS {TableNames.tbApiCallMap} (id TEXT PRIMARY KEY, callId TEXT)";
            $"CREATE TABLE IF NOT EXISTS {TableNames.tbApiDef} (id TEXT PRIMARY KEY, name TEXT, systemId TEXT)";
            $"CREATE TABLE IF NOT EXISTS {TableNames.tbArrowCall} (id TEXT PRIMARY KEY, type TEXT, source TEXT, target TEXT)";
            $"CREATE TABLE IF NOT EXISTS {TableNames.tbArrowWork} (id TEXT PRIMARY KEY, type TEXT, source TEXT, target TEXT)";
        ] |> List.iter exec

        conn.Close()

    let fromProject (proj: Project) : RuntimeDb =
        let pTable, psMapTable = ResizeArray(), ResizeArray()
        let sTable, wTable, cTable = ResizeArray(), ResizeArray(), ResizeArray()
        let acTable, adSet = ResizeArray(), HashSet<Guid>()
        let adTable, acMap = ResizeArray(), ResizeArray()
        let wcTable, ccTable = ResizeArray(), ResizeArray()

        pTable.Add(proj)

        for su in proj.SystemUsages do
            psMapTable.Add(su)
            let sys = su.TargetSystem
            sTable.Add(sys)

            for w in sys.Works do
                wTable.Add(w)
                for c in w.Calls do
                    cTable.Add(c)
                    for ac in c.ApiCalls do
                        acTable.Add(ac)
                        acMap.Add(ApiCallUsage($"{ac.Name}.Map", c)) // 매핑 엔트리 생성
                        if adSet.Add(ac.TargetApiDef.Guid) then
                            adTable.Add(ac.TargetApiDef)
                for s, t in w.CallGraph do ccTable.Add((s, t))

            for s, t in sys.WorkArrows do wcTable.Add((s, t))

        {
            Projects = pTable
            ProjectSystemMap = psMapTable
            Systems = sTable
            Works = wTable
            Calls = cTable
            ApiCallMap = acMap
            ApiCalls = acTable
            ApiDefs = adTable
            WorkArrows = wcTable
            CallArrows = ccTable
        }

    let saveToSqlite (db: RuntimeDb) (sqlitePath: string) =
        use conn = new SqliteConnection($"Data Source={sqlitePath}")
        conn.Open()

        let insertRow (tableName: string) (columns: string[]) (values: obj[]) =
            let sql =
                let cols = String.concat ", " columns
                let pars = columns |> Array.map (fun c -> "@" + c) |> String.concat ", "
                $"INSERT INTO {tableName} ({cols}) VALUES ({pars})"
            use cmd = new SqliteCommand(sql, conn)
            Array.iteri (fun i col -> cmd.Parameters.AddWithValue("@" + col, values[i]) |> ignore) columns
            cmd.ExecuteNonQuery() |> ignore

        for p in db.Projects do
            insertRow TableNames.tbProject [|"id"; "name"|] [|p.Guid.ToString(); p.Name|]

        for ps in db.ProjectSystemMap do
            insertRow TableNames.tbProjectSystemMap
                [|"id"; "projectId"; "systemId"; "active"|]
                [|ps.Guid.ToString(); ps.Project.Guid.ToString(); ps.TargetSystem.Guid.ToString(); ps.Active|]

        for s in db.Systems do
            insertRow TableNames.tbSystem [|"id"; "name"|] [|s.Guid.ToString(); s.Name|]

        for w in db.Works do
            insertRow TableNames.tbWork [|"id"; "name"; "systemId"|]
                [|w.Guid.ToString(); w.Name; w.System.Guid.ToString()|]

        for c in db.Calls do
            insertRow TableNames.tbCall [|"id"; "name"; "workId"|]
                [|c.Guid.ToString(); c.Name; c.Work.Guid.ToString()|]

        for ac in db.ApiCalls do
            insertRow TableNames.tbApiCall [|"id"; "name"; "callId"; "apiDefId"|]
                [|ac.Guid.ToString(); ac.Name; ac.Call.Guid.ToString(); ac.TargetApiDef.Guid.ToString()|]

        for map in db.ApiCallMap do
            insertRow TableNames.tbApiCallMap [|"id"; "callId"|]
                [|map.Guid.ToString(); map.Parent.Guid.ToString()|]

        for ad in db.ApiDefs do
            insertRow TableNames.tbApiDef [|"id"; "name"; "systemId"|]
                [|ad.Guid.ToString(); ad.Name; ad.System.Guid.ToString()|]

        for (src, tgt) in db.WorkArrows do
            insertRow TableNames.tbArrowWork [|"id"; "type"; "source"; "target"|]
                [|Guid.NewGuid().ToString(); "start"; src.Guid.ToString(); tgt.Guid.ToString()|]

        for (src, tgt) in db.CallArrows do
            insertRow TableNames.tbArrowCall [|"id"; "type"; "source"; "target"|]
                [|Guid.NewGuid().ToString(); "start"; src.Guid.ToString(); tgt.Guid.ToString()|]

        conn.Close()
