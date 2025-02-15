namespace rec Dual.Ev2

open Newtonsoft.Json
open Newtonsoft.Json.Linq

open Dual.Common.Base.FS
open Dual.Common.Core.FS
open System.Runtime.CompilerServices
open System
open Dual.Common.Base.FS

[<AutoOpen>]

module DsConfigModule =
    type DsConfig(?connectionString:string) =
        /// DS 설정용 database connection string.
        /// 기존 table 구조: storage, model, log, token, error, tagKind
        /// 추가 table 구조: item, itemType(차종type), process(공정), process_item,
        member val ConnectionString = connectionString |? "" with get, set

