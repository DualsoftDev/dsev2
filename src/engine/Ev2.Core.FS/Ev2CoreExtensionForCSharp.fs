namespace Ev2.Core.FS

open Dual.Common.Base
open Dual.Common.Core.FS
open System
open System.Data
open System.Runtime.CompilerServices
open System.IO
open Newtonsoft.Json
open System.Runtime.InteropServices


[<AutoOpen>]
module ExtCopyModule =
    type Project with // CopyTo
        member x.CopyTo(nj:NjProject) =
            replicateProperties x nj |> ignore


type CopyExtensionForCSharp = // CsCopyTo
    [<Extension>]
    static member CsCopyTo(src:Unique, dst:Unique) =
        match src, dst with
        | (:? NjProject), (:? Project)
        | (:? NjSystem),  (:? DsSystem)
        | (:? NjFlow),    (:? Flow)
        | (:? NjWork),    (:? Work)
        | (:? NjCall),    (:? Call)
        | (:? NjButton),  (:? DsButton)
        | (:? NjLamp),    (:? Lamp)
        | (:? NjCondition), (:? DsCondition)
        | (:? NjAction),  (:? DsAction)
        | (:? NjApiDef),  (:? ApiDef)
        | (:? NjApiCall),  (:? ApiCall)


        | (:? Project),  (:? NjProject)
        | (:? DsSystem), (:? NjSystem)
        | (:? Flow),     (:? NjFlow)
        | (:? Work),     (:? NjWork)
        | (:? Call),     (:? NjCall)
        | (:? DsButton), (:? NjButton)
        | (:? Lamp),     (:? NjLamp)
        | (:? DsCondition), (:? NjCondition)
        | (:? DsAction), (:? NjAction)
        | (:? ApiDef),  (:? NjApiDef)
        | (:? ApiCall),  (:? NjApiCall)
            -> replicateProperties src dst |> ignore
        | _
            -> failwith "ERROR"



type Ev2CoreExtensionForCSharp = // CsExportToJson, CsToJson

    // Project 확장 메서드 - C# 전용
    [<Extension>]
    static member CsToJson(project:Project): string =
        // NjProject.ToJson()을 사용하여 일관된 DateFormatString 적용
        let njProject = project.ToNjObj() :?> NjProject
        njProject.ToJson()

    [<Extension>]
    static member CsToJson(project:Project, filePath:string): string =
        let njProject = project.ToNjObj() :?> NjProject
        let json = njProject.ToJson()
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

    [<Extension>]
    static member CsEnumerateRtObjects(rtObj:RtUnique, [<Optional; DefaultParameterValue(true)>] includeMe:bool): RtUnique[] =
        rtObj.EnumerateRtObjects(includeMe) |> toArray


// Project 타입에 대한 정적 메서드 (C#에서 ProjectExtensions.CsFromJson() 형태로 사용)
type ProjectExtensions = // CsCheckoutFromDB, CsCommitToDB, CsRemoveFromDB
    static let commitResultToString (result: DbCommitResult) : string =
        match result with
        | Ok result -> result.Stringify()
        | Error errorMsg -> failwith errorMsg


    /// C# exception 기반 CommitToDB - 실패시 예외 발생
    [<Extension>]
    static member CsCommitToDB(project:Project, dbApi:AppDbApi) : string =
        project.RTryCommitToDB(dbApi) |> commitResultToString

    /// C# exception 기반 CommitToDB - 실패시 예외 발생
    [<Extension>]
    static member CsCommitToDB(system:DsSystem, dbApi:AppDbApi) : string =
        system.RTryCommitToDB(dbApi) |> commitResultToString

    /// C# exception 기반 RemoveFromDB - 실패시 예외 발생
    [<Extension>]
    static member CsRemoveFromDB(project:Project, dbApi:AppDbApi) : string =
        project.RTryRemoveFromDB(dbApi) |> commitResultToString

    /// C# exception 기반 CheckoutFromDB by Id - 실패시 예외 발생
    [<Extension>]
    static member CsCheckoutFromDB(projectId:int64, dbApi:AppDbApi) : Project =
        Project.RTryCheckoutFromDB(projectId, dbApi) |> Result.defaultWith failwith

    /// C# exception 기반 CheckoutFromDB by name - 실패시 예외 발생
    [<Extension>]
    static member CsCheckoutFromDB(projectName:string, dbApi:AppDbApi) : Project =
        Project.RTryCheckoutFromDB(projectName, dbApi) |> Result.defaultWith failwith



// DsSystem 타입에 대한 정적 메서드
type DsSystemExtensions = // CsFromJson, CsImportFromJson
    static member CsImportFromJson(json:string): DsSystem =
        json
        |> NjSystem.ImportFromJson
        |> getRuntimeObject<DsSystem>
        |> validateRuntime

    static member CsFromJson(json:string): DsSystem =
        DsSystemExtensions.CsImportFromJson(json)

type DbApiExtensions = // CsWith, CsWith<'T>, CsWithConn, CsWithConn<'T>, CsWithNew, CsWithNew<'T>
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
type DsSystemExtensions with // CsCheckoutFromDB, CsTryCheckoutFromDB
    /// C# exception 기반 CheckoutFromDB by Id - 실패시 예외 발생
    [<Extension>]
    static member CsCheckoutFromDB(systemId:int64, dbApi:AppDbApi) : DsSystem =
        match DsSystem.RTryCheckoutFromDB(systemId, dbApi) with
        | Ok system -> system
        | Error errorMsg -> failwith errorMsg


    /// C# 친화적인 TryCheckoutFromDB - (성공여부, DsSystem객체, 에러메시지) 반환
    [<Extension>]
    static member CsTryCheckoutFromDB(systemId:int64, dbApi:AppDbApi) : bool * DsSystem * string =
        match DsSystem.RTryCheckoutFromDB(systemId, dbApi) with
        | Ok system -> (true, system, "")
        | Error errorMsg -> (false, Unchecked.defaultof<DsSystem>, errorMsg)

// AppDbApi C# 호환 확장 메서드들
type AppDbApiCsExtensions = // CsFindEnumValueId, CsFindEnumValue, CsTryFindEnumValueId, CsTryFindEnumValue
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