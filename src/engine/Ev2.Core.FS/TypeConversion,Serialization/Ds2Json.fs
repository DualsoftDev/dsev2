namespace Ev2.Core.FS

open Dual.Common.Base
open Dual.Common.Core.FS
open Newtonsoft.Json
open System

[<AutoOpen>]
module Ds2JsonModule =
    type DsSystem with
        member x.ToJson():string =
            EmJson.ToJson(x)

