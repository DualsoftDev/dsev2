namespace Ev2.Core.FS

open Dual.Common.Base
open Dual.Common.Core.FS
open Newtonsoft.Json
open System

[<AutoOpen>]
module Interfaces =
    /// 기본 객체 인터페이스
    type IDsObject = interface end

    /// Guid, Name, DateTime
    type IUnique =
        inherit IDsObject


    type Unique(guid:Guid, name:string, dateTime:DateTime) =
        member x.Guid = guid
        member val Name = name with get, set
        member val DateTime = dateTime with get, set
        interface IUnique

        override this.ToString() = this.Name
        override this.GetHashCode() = this.Guid.GetHashCode()
        override this.Equals(obj: obj) =
            match obj with
            | :? Unique as other -> this.Guid = other.Guid
            | _ -> false
