namespace Ev2.Core.FS

open Dual.Common.Base
open Dual.Common.Core.FS
open Dual.Common.Db.FS
open Newtonsoft.Json
open System
open System.Data
open System.Runtime.CompilerServices
open System.IO

[<Extension>]
type Ev2CoreExtensionForCSharp =

    // Project 확장 메서드 - C# 전용
    [<Extension>]
    static member CsToJson(project:Project): string =
        // EmJson을 사용하여 $type 정보 포함
        let njProject = project.ToNjObj() :?> NjProject
        EmJson.ToJson(njProject)

    [<Extension>]
    static member CsToJson(project:Project, filePath:string): string =
        let njProject = project.ToNjObj() :?> NjProject
        let json = EmJson.ToJson(njProject)
        File.WriteAllText(filePath, json)
        json

    // DsSystem 확장 메서드 - C# 전용
    [<Extension>]
    static member CsExportToJson(system:DsSystem): string =
        let njSystem = system.ToNj<NjSystem>()
        njSystem.ExportToJson()

    [<Extension>]
    static member CsExportToJson(system:DsSystem, filePath:string): string =
        let njSystem = system.ToNj<NjSystem>()
        njSystem.ExportToJsonFile(filePath)

    // 범용 RtUnique 확장 메서드
    [<Extension>]
    static member CsToJson(rtObj:RtUnique): string =
        match rtObj with
        | :? Project as p ->
            let njProject = p.ToNjObj() :?> NjProject
            njProject.ToJson()
        | :? DsSystem as s ->
            let njSystem = s.ToNj<NjSystem>()
            njSystem.ExportToJson()
        | _ ->
            // 다른 타입들에 대한 기본 JSON 직렬화
            let njObj = rtObj.ToNjObj()
            EmJson.ToJson(njObj)

// Project 타입에 대한 정적 메서드 (C#에서 ProjectExtensions.CsFromJson() 형태로 사용)
type ProjectExtensions =
    static member CsFromJson(json:string): Project =
        // JSON을 JObject로 파싱하여 RuntimeType 확인
        let jObj = Newtonsoft.Json.Linq.JObject.Parse(json)
        let runtimeTypeName =
            match jObj.["RuntimeType"] with
            | null -> "NjProject"
            | token -> token.ToString()

        // TypeFactory를 통해 RuntimeType에 맞는 JSON 타입 찾기
        let njProject =
            match getTypeFactory() with
            | Some factory ->
                // RuntimeType 문자열로 JSON 타입 찾기
                match factory.FindNjTypeByName(runtimeTypeName) with
                | null ->
                    // 타입을 찾지 못하면 기본 NjProject로 역직렬화
                    EmJson.FromJson<NjProject>(json)
                | jsonType ->
                    // 찾은 타입으로 동적 역직렬화
                    let genericMethod = typeof<EmJson>.GetMethod("FromJson", [| typeof<string> |]).MakeGenericMethod([|jsonType|])
                    genericMethod.Invoke(null, [|json|]) :?> NjProject
            | None ->
                // TypeFactory가 없으면 기본 NjProject로 역직렬화
                EmJson.FromJson<NjProject>(json)

        let runtimeProject = njProject
                            |> NewtonsoftJsonModules.getRuntimeObject<Project>
                            |> validateRuntime

        // 확장 속성 복사 수행
        match getTypeFactory() with
        | Some factory ->
            factory.CopyExtensionProperties(njProject, runtimeProject)
        | None -> ()

        runtimeProject

    // =====================
    // CsXXX 메서드들 - exception 기반 패턴 (C# 친화적)
    // =====================

    /// C# exception 기반 CommitToDB - 실패시 예외 발생
    [<Extension>]
    static member CsCommitToDB(project:Project, dbApi:AppDbApi) : string =
        match project.RTryCommitToDB(dbApi) with
        | Ok result ->
            match result with
            | NoChange -> "NoChange"
            | Inserted -> "Inserted"
            | Updated diffs -> $"Updated ({diffs.Length} changes)"
            | Deleted -> "Deleted"
        | Error errorMsg -> failwith errorMsg

    /// C# exception 기반 RemoveFromDB - 실패시 예외 발생
    [<Extension>]
    static member CsRemoveFromDB(project:Project, dbApi:AppDbApi) : string =
        match project.RTryRemoveFromDB(dbApi) with
        | Ok result ->
            match result with
            | NoChange -> "NoChange"
            | Inserted -> "Inserted"
            | Updated diffs -> $"Updated ({diffs.Length} changes)"
            | Deleted -> "Deleted"
        | Error errorMsg -> failwith errorMsg

    /// C# exception 기반 CheckoutFromDB by Id - 실패시 예외 발생
    [<Extension>]
    static member CsCheckoutFromDB(projectId:int64, dbApi:AppDbApi) : Project =
        match Project.RTryCheckoutFromDB(projectId, dbApi) with
        | Ok project -> project
        | Error errorMsg -> failwith errorMsg

    /// C# exception 기반 CheckoutFromDB by name - 실패시 예외 발생
    [<Extension>]
    static member CsCheckoutFromDB(projectName:string, dbApi:AppDbApi) : Project =
        match Project.RTryCheckoutFromDB(projectName, dbApi) with
        | Ok project -> project
        | Error errorMsg -> failwith errorMsg

    // =====================
    // CsTryXXX 메서드들 - bool * result * error 패턴
    // =====================
    /// C# 친화적인 TryCommitToDB - (성공여부, 결과메시지, 에러메시지) 반환
    [<Extension>]
    static member CsTryCommitToDB(project:Project, dbApi:AppDbApi) : bool * string * string =
        match project.RTryCommitToDB(dbApi) with
        | Ok result ->
            let resultMessage =
                match result with
                | NoChange -> "NoChange"
                | Inserted -> "Inserted"
                | Updated diffs -> $"Updated ({diffs.Length} changes)"
                | Deleted -> "Deleted"
            (true, resultMessage, "")
        | Error errorMsg -> (false, "", errorMsg)

    /// C# 친화적인 TryRemoveFromDB - (성공여부, 결과메시지, 에러메시지) 반환
    [<Extension>]
    static member CsTryRemoveFromDB(project:Project, dbApi:AppDbApi) : bool * string * string =
        match project.RTryRemoveFromDB(dbApi) with
        | Ok result ->
            let resultMessage =
                match result with
                | NoChange -> "NoChange"
                | Inserted -> "Inserted"
                | Updated diffs -> $"Updated ({diffs.Length} changes)"
                | Deleted -> "Deleted"
            (true, resultMessage, "")
        | Error errorMsg -> (false, "", errorMsg)

    /// C# 친화적인 TryCheckoutFromDB - (성공여부, Project객체, 에러메시지) 반환
    [<Extension>]
    static member CsTryCheckoutFromDB(projectId:int64, dbApi:AppDbApi) : bool * Project * string =
        match Project.RTryCheckoutFromDB(projectId, dbApi) with
        | Ok project -> (true, project, "")
        | Error errorMsg -> (false, Unchecked.defaultof<Project>, errorMsg)

    /// C# 친화적인 TryCheckoutFromDB by name - (성공여부, Project객체, 에러메시지) 반환
    [<Extension>]
    static member CsTryCheckoutFromDB(projectName:string, dbApi:AppDbApi) : bool * Project * string =
        match Project.RTryCheckoutFromDB(projectName, dbApi) with
        | Ok project -> (true, project, "")
        | Error errorMsg -> (false, Unchecked.defaultof<Project>, errorMsg)

// DsSystem 타입에 대한 정적 메서드
type DsSystemExtensions =
    static member CsImportFromJson(json:string): DsSystem =
        json
        |> NjSystem.ImportFromJson
        |> NewtonsoftJsonModules.getRuntimeObject<DsSystem>
        |> validateRuntime

    static member CsFromJson(json:string): DsSystem =
        DsSystemExtensions.CsImportFromJson(json)

type DbApiExtensions =
    // =====================
    // CsWith 메서드들 - 기본 With 메서드의 C# 호환 버전
    // =====================

    /// 기본 CsWith - 값을 반환하는 함수용
    [<Extension>]
    static member CsWith<'T>(dbApi:AppDbApi, func:Func<IDbConnection, IDbTransaction, 'T>) =
        dbApi.With(fun (conn, tr) -> func.Invoke(conn, tr))

    /// 기본 CsWith - 값을 반환하는 함수용 (에러 핸들러 포함)
    [<Extension>]
    static member CsWith<'T>(dbApi:AppDbApi, func:Func<IDbConnection, IDbTransaction, 'T>, onError:Action<Exception>) =
        dbApi.With(func, onError)

    /// 기본 CsWith - void를 반환하는 액션용
    [<Extension>]
    static member CsWith(dbApi:AppDbApi, action:Action<IDbConnection, IDbTransaction>) =
        dbApi.With(action)

    /// 기본 CsWith - void를 반환하는 액션용 (에러 핸들러 포함)
    [<Extension>]
    static member CsWith(dbApi:AppDbApi, action:Action<IDbConnection, IDbTransaction>, onError:Action<Exception>) =
        dbApi.With(action, onError)

    // =====================
    // CsWithNew 메서드들 - 새로운 연결/트랜잭션 생성
    // =====================

    /// CsWithNew - 새 연결에서 값을 반환하는 함수용
    [<Extension>]
    static member CsWithNew<'T>(dbApi:AppDbApi, func:Func<IDbConnection, IDbTransaction, 'T>) =
        dbApi.WithNew(fun (conn, tr) -> func.Invoke(conn, tr))

    /// CsWithNew - 새 연결에서 값을 반환하는 함수용 (에러 핸들러 포함)
    [<Extension>]
    static member CsWithNew<'T>(dbApi:AppDbApi, func:Func<IDbConnection, IDbTransaction, 'T>, onError:Action<Exception>) =
        dbApi.WithNew((fun (conn, tr) -> func.Invoke(conn, tr)), (fun ex -> onError.Invoke(ex)))

    /// CsWithNew - 새 연결에서 void를 반환하는 액션용
    [<Extension>]
    static member CsWithNew(dbApi:AppDbApi, action:Action<IDbConnection, IDbTransaction>) =
        dbApi.WithNew(fun (conn, tr) -> action.Invoke(conn, tr))

    /// CsWithNew - 새 연결에서 void를 반환하는 액션용 (에러 핸들러 포함)
    [<Extension>]
    static member CsWithNew(dbApi:AppDbApi, action:Action<IDbConnection, IDbTransaction>, onError:Action<Exception>) =
        dbApi.WithNew((fun (conn, tr) -> action.Invoke(conn, tr)), (fun ex -> onError.Invoke(ex)))

    // =====================
    // CsWithConn 메서드들 - 연결만 사용 (트랜잭션 없음)
    // =====================

    /// CsWithConn - 트랜잭션 없이 값을 반환하는 함수용
    [<Extension>]
    static member CsWithConn<'T>(dbApi:AppDbApi, func:Func<IDbConnection, 'T>) =
        dbApi.WithConn(fun conn -> func.Invoke(conn))

    /// CsWithConn - 트랜잭션 없이 값을 반환하는 함수용 (에러 핸들러 포함)
    [<Extension>]
    static member CsWithConn<'T>(dbApi:AppDbApi, func:Func<IDbConnection, 'T>, onError:Action<Exception>) =
        dbApi.WithConn((fun conn -> func.Invoke(conn)), (fun ex -> onError.Invoke(ex)))

    /// CsWithConn - 트랜잭션 없이 void를 반환하는 액션용
    [<Extension>]
    static member CsWithConn(dbApi:AppDbApi, action:Action<IDbConnection>) =
        dbApi.WithConn(fun conn -> action.Invoke(conn))

    /// CsWithConn - 트랜잭션 없이 void를 반환하는 액션용 (에러 핸들러 포함)
    [<Extension>]
    static member CsWithConn(dbApi:AppDbApi, action:Action<IDbConnection>, onError:Action<Exception>) =
        dbApi.WithConn((fun conn -> action.Invoke(conn)), (fun ex -> onError.Invoke(ex)))

// 기존 DsSystemExtensions에 추가
type DsSystemExtensions with
    // =====================
    // CsXXX 메서드들 - exception 기반 패턴 (C# 친화적)
    // =====================

    /// C# exception 기반 CommitToDB - 실패시 예외 발생
    [<Extension>]
    static member CsCommitToDB(system:DsSystem, dbApi:AppDbApi) : string =
        match system.RTryCommitToDB(dbApi) with
        | Ok result ->
            match result with
            | NoChange -> "NoChange"
            | Inserted -> "Inserted"
            | Updated diffs -> $"Updated ({diffs.Length} changes)"
            | Deleted -> "Deleted"
        | Error errorMsg -> failwith errorMsg

    /// C# exception 기반 CheckoutFromDB by Id - 실패시 예외 발생
    [<Extension>]
    static member CsCheckoutFromDB(systemId:int64, dbApi:AppDbApi) : DsSystem =
        match DsSystem.RTryCheckoutFromDB(systemId, dbApi) with
        | Ok system -> system
        | Error errorMsg -> failwith errorMsg

    // =====================
    // CsTryXXX 메서드들 - bool * result * error 패턴
    // =====================
    /// C# 친화적인 TryCommitToDB - (성공여부, 결과메시지, 에러메시지) 반환
    [<Extension>]
    static member CsTryCommitToDB(system:DsSystem, dbApi:AppDbApi) : bool * string * string =
        match system.RTryCommitToDB(dbApi) with
        | Ok result ->
            let resultMessage =
                match result with
                | NoChange -> "NoChange"
                | Inserted -> "Inserted"
                | Updated diffs -> $"Updated ({diffs.Length} changes)"
                | Deleted -> "Deleted"
            (true, resultMessage, "")
        | Error errorMsg -> (false, "", errorMsg)

    /// C# 친화적인 TryCheckoutFromDB - (성공여부, DsSystem객체, 에러메시지) 반환
    [<Extension>]
    static member CsTryCheckoutFromDB(systemId:int64, dbApi:AppDbApi) : bool * DsSystem * string =
        match DsSystem.RTryCheckoutFromDB(systemId, dbApi) with
        | Ok system -> (true, system, "")
        | Error errorMsg -> (false, Unchecked.defaultof<DsSystem>, errorMsg)

// AppDbApi C# 호환 확장 메서드들
type AppDbApiCsExtensions =
    // =====================
    // CsXXX 메서드들 - exception 기반 패턴 (C# 친화적)
    // =====================

    /// C# exception 기반 FindEnumValueId - 실패시 예외 발생
    [<Extension>]
    static member CsFindEnumValueId<'TEnum when 'TEnum : enum<int>>(dbApi:AppDbApi, enumValue:'TEnum) : int64 =
        match dbApi.TryFindEnumValueId<'TEnum>(enumValue) with
        | Some id -> id
        | None -> failwith $"Enum value {enumValue} not found in database"

    /// C# exception 기반 FindEnumValue - 실패시 예외 발생
    [<Extension>]
    static member CsFindEnumValue<'TEnum when 'TEnum : struct and 'TEnum : enum<int> and 'TEnum : (new : unit -> 'TEnum) and 'TEnum :> ValueType>
        (dbApi:AppDbApi, enumId:int64) : 'TEnum =
        match dbApi.TryFindEnumValue<'TEnum>(enumId) with
        | Some enumValue -> enumValue
        | None -> failwith $"Enum id {enumId} not found in database"

    // =====================
    // CsTryXXX 메서드들 - bool * result * error 패턴
    // =====================
    /// C# 친화적인 TryFindEnumValueId - (성공여부, Id값, 에러메시지) 반환
    [<Extension>]
    static member CsTryFindEnumValueId<'TEnum when 'TEnum : enum<int>>(dbApi:AppDbApi, enumValue:'TEnum) : bool * int64 * string =
        match dbApi.TryFindEnumValueId<'TEnum>(enumValue) with
        | Some id -> (true, id, "")
        | None -> (false, -1L, $"Enum value {enumValue} not found in database")

    /// C# 친화적인 TryFindEnumValue - (성공여부, enum값, 에러메시지) 반환
    [<Extension>]
    static member CsTryFindEnumValue<'TEnum when 'TEnum : struct and 'TEnum : enum<int> and 'TEnum : (new : unit -> 'TEnum) and 'TEnum :> ValueType>
        (dbApi:AppDbApi, enumId:int64) : bool * 'TEnum * string =
        match dbApi.TryFindEnumValue<'TEnum>(enumId) with
        | Some enumValue -> (true, enumValue, "")
        | None -> (false, new 'TEnum(), $"Enum id {enumId} not found in database")