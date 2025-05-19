namespace Ev2.Core.FS

open Dual.Common.Base
open Dual.Common.Core.FS
open Newtonsoft.Json
open System

[<AutoOpen>]
module Interfaces =
    /// 기본 객체 인터페이스
    type IDsObject = interface end
    type IParameter = inherit IDsObject
    type IArrow = inherit IDsObject

    /// Guid, Name, DateTime
    type IUnique =
        inherit IDsObject


    type IDsSystem = inherit IDsObject
    type IDsFlow = inherit IDsObject
    type IDsWork = inherit IDsObject
    type IDsCall = inherit IDsObject

    let private toN = Option.toNullable
    let nullId = Nullable<int>()
    let nullGuid = Nullable<Guid>()
    let nullDateTime = Nullable<DateTime>()

    [<AbstractClass>]
    type Unique(name:string, ?id:int64, ?guid:Guid, ?dateTime:DateTime) =
        interface IUnique
        member val Id = id with get, set
        member val Guid = guid with get, set
        member val Name = name with get, set
        member val DateTime = dateTime with get, set

        override this.ToString() = this.Name
        override this.GetHashCode() = this.Guid.GetHashCode()
        override this.Equals(obj: obj) =
            match obj with
            | :? Unique as other -> this.Guid = other.Guid
            | _ -> false
    type DsSystem(name:string, ?id, ?guid:Guid, ?dateTime:DateTime) =
        inherit Unique(name, ?id=id, ?guid=guid, ?dateTime=dateTime)

        interface IDsSystem
        //member val Id = -1 with get, set
        member val FlowId = -1 with get, set
        member val FlowName = "" with get, set
        member val FlowGuid = Nullable() with get, set
        member val FlowDateTime = Nullable() with get, set
