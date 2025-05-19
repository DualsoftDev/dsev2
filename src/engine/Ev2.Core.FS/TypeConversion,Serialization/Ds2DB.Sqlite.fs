namespace Ev2.Core.FS

open Dual.Common.Base
open Dual.Common.Core.FS
open Newtonsoft.Json
open System

[<AutoOpen>]
module Ds2SqliteModule =
    type DsSystem with
        member x.ToSqlite3(connStr:string) =
            ()

        [<Obsolete("DB 에서 읽어 들이는 것은 금지!!!  Debugging 전용")>]
        static member FromSqlite3(connStr:string) =
            ()
