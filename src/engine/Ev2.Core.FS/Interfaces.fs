namespace Ev2.Core.FS

open Dual.Common.Base
open Dual.Common.Core.FS
open Newtonsoft.Json
open System

[<AutoOpen>]
module Interfaces =
    /// 기본 객체 인터페이스
    type IDsObject = interface end
    type IUnique =
        inherit IDsObject



