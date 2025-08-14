namespace Ev2.Core.FS.Extension

open System
open System.Data
open System.Collections.Generic
open System.Reflection
open Dapper
open Dual.Common.Base
open Dual.Common.Db.FS

/// ORM 확장 속성 정의
type OrmExtensionProperty = {
    PropertyName: string
    ColumnName: string
    SqlType: string
    IsNullable: bool
    DefaultValue: obj option
}

/// 데이터베이스 벤더 타입
type DatabaseVendor =
    | SQLite
    | PostgreSQL
    | SqlServer
    | MySQL

/// ORM 확장 모듈
module OrmExtension =

    /// 데이터베이스 벤더별 타입 매핑
    let private getSqlType (vendor: DatabaseVendor) (clrType: Type) =
        match vendor, clrType with
        // SQLite
        | SQLite, t when t = typeof<string> -> "TEXT"
        | SQLite, t when t = typeof<int> || t = typeof<int32> -> "INTEGER"
        | SQLite, t when t = typeof<int64> -> "INTEGER"
        | SQLite, t when t = typeof<float> || t = typeof<double> -> "REAL"
        | SQLite, t when t = typeof<bool> -> "INTEGER"
        | SQLite, t when t = typeof<DateTime> -> "TEXT"
        | SQLite, t when t = typeof<Guid> -> "TEXT"
        | SQLite, t when t = typeof<decimal> -> "REAL"

        // PostgreSQL
        | PostgreSQL, t when t = typeof<string> -> "TEXT"
        | PostgreSQL, t when t = typeof<int> || t = typeof<int32> -> "INTEGER"
        | PostgreSQL, t when t = typeof<int64> -> "BIGINT"
        | PostgreSQL, t when t = typeof<float> -> "REAL"
        | PostgreSQL, t when t = typeof<double> -> "DOUBLE PRECISION"
        | PostgreSQL, t when t = typeof<bool> -> "BOOLEAN"
        | PostgreSQL, t when t = typeof<DateTime> -> "TIMESTAMP"
        | PostgreSQL, t when t = typeof<Guid> -> "UUID"
        | PostgreSQL, t when t = typeof<decimal> -> "DECIMAL"

        // 기본값
        | _, _ -> "TEXT"


    /// ALTER TABLE 문 생성 (컬럼 추가)
    let generateAlterTableSql (vendor: DatabaseVendor) (tableName: string) (property: OrmExtensionProperty) =
        let quotedColumnName =
            match vendor with
            | PostgreSQL -> sprintf "\"%s\"" property.ColumnName
            | _ -> sprintf "\"%s\"" property.ColumnName

        let sqlType = getSqlType vendor (property.GetType())
        let nullable = if property.IsNullable then "" else " NOT NULL"
        let defaultValue =
            match property.DefaultValue with
            | Some v -> sprintf " DEFAULT %A" v
            | None -> ""

        sprintf "ALTER TABLE %s ADD COLUMN %s %s%s%s"
            tableName quotedColumnName sqlType nullable defaultValue

    /// 확장 속성 값 읽기
    let readExtensionProperties (conn: IDbConnection) (tr: IDbTransaction option)
                                (tableName: string) (idColumn: string) (id: int64)
                                (properties: OrmExtensionProperty[]) =
        if Array.isEmpty properties then
            Map.empty
        else
            let columns =
                properties
                |> Array.map (fun p -> sprintf "\"%s\"" p.ColumnName)
                |> String.concat ", "

            let sql = sprintf "SELECT %s FROM \"%s\" WHERE \"%s\" = @Id" columns tableName idColumn

            let parameters = dict ["Id", box id]

            let result =
                match tr with
                | Some transaction -> conn.QuerySingleOrDefault(sql, parameters, transaction)
                | None -> conn.QuerySingleOrDefault(sql, parameters)

            if isItNull result then
                Map.empty
            else
                properties
                |> Array.fold (fun acc prop ->
                    let value = (result :?> IDictionary<string, obj>).[prop.ColumnName]
                    Map.add prop.PropertyName value acc) Map.empty

    /// 확장 속성 값 쓰기
    let writeExtensionProperties (conn: IDbConnection) (tr: IDbTransaction option)
                                 (tableName: string) (idColumn: string) (id: int64)
                                 (properties: OrmExtensionProperty[]) (values: Map<string, obj>) =
        if Array.isEmpty properties || Map.isEmpty values then
            ()
        else
            let setClauses = ResizeArray<string>()
            let parameters = Dictionary<string, obj>()
            parameters.["Id"] <- box id

            for prop in properties do
                match Map.tryFind prop.PropertyName values with
                | Some value ->
                    setClauses.Add(sprintf "\"%s\" = @%s" prop.ColumnName prop.PropertyName)
                    parameters.[prop.PropertyName] <- value
                | None -> ()

            if setClauses.Count > 0 then
                let sql = sprintf "UPDATE \"%s\" SET %s WHERE \"%s\" = @Id"
                            tableName (String.Join(", ", setClauses)) idColumn

                match tr with
                | Some transaction -> conn.Execute(sql, parameters, transaction) |> ignore
                | None -> conn.Execute(sql, parameters) |> ignore

    /// 테이블에 확장 컬럼이 존재하는지 확인
    let checkColumnExists (conn: IDbConnection) (vendor: DatabaseVendor)
                         (tableName: string) (columnName: string) =
        let sql =
            match vendor with
            | SQLite ->
                sprintf "SELECT COUNT(*) FROM pragma_table_info('%s') WHERE name = @ColumnName" tableName
            | PostgreSQL ->
                sprintf """
                    SELECT COUNT(*)
                    FROM information_schema.columns
                    WHERE table_name = @TableName AND column_name = @ColumnName
                """
            | _ ->
                // 다른 DB는 일단 PostgreSQL과 동일하게
                sprintf """
                    SELECT COUNT(*)
                    FROM information_schema.columns
                    WHERE table_name = @TableName AND column_name = @ColumnName
                """

        let parameters =
            match vendor with
            | SQLite -> dict ["ColumnName", box columnName]
            | _ -> dict ["TableName", box tableName; "ColumnName", box columnName]

        let count = conn.QuerySingle<int>(sql, parameters)
        count > 0

    /// 자동으로 누락된 확장 컬럼 추가
    let ensureExtensionColumns (conn: IDbConnection) (vendor: DatabaseVendor)
                              (tableName: string) (properties: OrmExtensionProperty[]) =
        for prop in properties do
            if not (checkColumnExists conn vendor tableName prop.ColumnName) then
                let alterSql = generateAlterTableSql vendor tableName prop
                conn.Execute(alterSql) |> ignore
                logInfo $"Added extension column {prop.ColumnName} to table {tableName}"


