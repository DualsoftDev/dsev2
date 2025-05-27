namespace Ev2.Core.FS

open System

open Dual.Common.Base
open Dual.Common.Core.FS


[<AutoOpen>]
module Interfaces =
    type Id = int   //int64
    /// 기본 객체 인터페이스
    type IDsObject  = interface end
    type IParameter = inherit IDsObject
    type IParameterContainer = inherit IDsObject
    type IArrow     = inherit IDsObject

    /// Guid, Name, DateTime
    type IUnique    = inherit IDsObject


    type IDsProject = inherit IDsObject
    type IDsSystem  = inherit IDsObject
    type IDsFlow    = inherit IDsObject
    type IDsWork    = inherit IDsObject
    type IDsCall    = inherit IDsObject
    type IDsApiCall = inherit IDsObject


    let internal minDate      = DateTime.MinValue
    let internal nullableId   = Nullable<Id>()
    let internal nullVersion  = null:Version
    let internal nullString   = null:string
    let internal nullableInt  = Nullable<int>()
    let internal invalidInt   = -1
    let internal nullableGuid = Nullable<Guid>()
    let internal emptyGuid    = Guid.Empty
    let internal newGuid()    = Guid.NewGuid()
    let internal s2guid (s:string) = Guid.Parse s
    let internal guid2str (g:Guid) = g.ToString("D")

    let internal now() = if AppSettings.TheAppSettings.UseUtcTime then DateTime.UtcNow else DateTime.Now

    [<AbstractClass>]
    type Unique(name:string, guid:Guid, dateTime:DateTime, ?id:Id, ?parent:Unique) =
        interface IUnique

        internal new() = Unique(nullString, newGuid(), now(), ?id=None, ?parent=None)

        /// DB 저장시의 primary key id.  DB read/write 수행한 경우에만 Non-null
        member val Id = id with get, set

        member val Name = name with get, set

        /// Guid: 메모리에 최초 객체 생성시 생성
        member val Guid:Guid = guid with get, set

        /// DateTime: 메모리에 최초 객체 생성시 생성
        member val DateTime = dateTime with get, set

        /// 자신의 container 에 해당하는 parent DS 객체.  e.g call -> work -> system -> project, flow -> system
        member val RawParent = parent with get, set

        /// Parent Guid : Json 저장시에는 container 의 parent 를 추적하면 되므로 json 에는 저장하지 않음
        member x.PGuid = x.RawParent |-> _.Guid



[<AutoOpen>]
module UniqueHelpers =
    let uniqReplicate (src:#Unique) (dst:#Unique) : #Unique =
        dst.Id <- src.Id
        dst.Name <- src.Name
        dst.Guid <- src.Guid
        dst.DateTime <- src.DateTime
        dst.RawParent <- src.RawParent
        dst

    let uniqDuplicate (src:#Unique) (dst:#Unique): #Unique =
        dst.Name <- src.Name
        dst.DateTime <- now()
        dst.RawParent <- src.RawParent
        dst

    let uniqRenew (dst:#Unique): #Unique =
        dst.Id <- None
        dst.Guid <- newGuid()
        dst.DateTime <- now()
        dst

    let uniqId       id       (dst:#Unique) = dst.Id        <- id;       dst
    let uniqName     name     (dst:#Unique) = dst.Name      <- name;     dst
    let uniqGuid     guid     (dst:#Unique) = dst.Guid      <- guid;     dst
    let uniqDateTime dateTime (dst:#Unique) = dst.DateTime  <- dateTime; dst
    let uniqParent   (parent:#Unique option)   (dst:#Unique) = dst.RawParent <- parent >>= tryCast<Unique>;   dst

    let uniqGuidDateTime     guid dateTime                (dst:#Unique) = dst |> uniqGuid guid |> uniqDateTime dateTime
    let uniqNameGuidDateTime name guid dateTime           (dst:#Unique) = dst |> uniqName name |> uniqGuid guid |> uniqDateTime dateTime
    let uniqINGD             id name guid dateTime        (dst:#Unique) = dst |> uniqId id     |> uniqNameGuidDateTime name guid dateTime
    let uniqINGDP            id name guid dateTime parent (dst:#Unique) = dst |> uniqId id     |> uniqNameGuidDateTime name guid dateTime |> uniqParent parent
    let uniqAll = uniqINGDP


    type Unique with
        member this.Renew<'T when 'T :> Unique> () : 'T =
            // 'this'는 Unique 타입이라 강제로 캐스팅 필요
            let x = this :?> 'T
            x.Id <- None
            x.Guid <- System.Guid.NewGuid()
            x.DateTime <- System.DateTime.Now
            x


