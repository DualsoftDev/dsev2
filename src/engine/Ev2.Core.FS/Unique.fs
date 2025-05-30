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

    (* Project > System > Work > Call > ApiCall > ApiDef *)

    type IDsProject = inherit IDsObject
    type IDsSystem  = inherit IDsObject
    type IDsFlow    = inherit IDsObject
    type IDsWork    = inherit IDsObject
    type IDsCall    = inherit IDsObject
    type IDsApiCall = inherit IDsObject
    type IDsApiDef  = inherit IDsObject


    let internal minDate      = DateTime.MinValue
    let internal nullableId   = Nullable<Id>()
    let internal nullVersion  = null:Version
    let internal nullString   = null:string
    let internal nullableInt  = Nullable<int>()
    let internal nullableGuid = Nullable<Guid>()
    let internal noneGuid     = Option<Guid>.None
    let internal emptyGuid    = Guid.Empty
    let internal newGuid()    = Guid.NewGuid()
    let internal s2guid (s:string) = Guid.Parse s
    let internal guid2str (g:Guid) = g.ToString("D")

    let internal now() =
        let x = AppSettings.TheAppSettings
        let y = x.UseUtcTime
        if AppSettings.TheAppSettings.UseUtcTime then DateTime.UtcNow else DateTime.Now

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
module internal UniqueHelpers =
    let uniqReplicate (src:#Unique) (dst:#Unique) : #Unique =
        dst.Id <- src.Id
        dst.Name <- src.Name
        dst.Guid <- src.Guid
        dst.DateTime <- src.DateTime
        dst.RawParent <- src.RawParent
        dst

    (*
        Chaining 해서 사용할 수 있는 Uniq 속성 수정 helper 함수들.  예제
            dsProject
            |> uniqDateTime (now())
            |> uniqGuid (newGuid())
            |> uniqId (Some 3)
            |> uniqName "KKKKKKKKKKKKK"
    *)

    let uniqId       id       (dst:#Unique) = dst.Id        <- id;       dst
    let uniqName     name     (dst:#Unique) = dst.Name      <- name;     dst
    let uniqGuid     guid     (dst:#Unique) = dst.Guid      <- guid;     dst
    let uniqDateTime dateTime (dst:#Unique) = dst.DateTime  <- dateTime; dst
    let uniqParent   (parent:#Unique option) (dst:#Unique) = dst.RawParent <- parent >>= tryCast<Unique>; dst

    let uniqGD       guid dateTime                (dst:#Unique) = dst |> uniqGuid guid |> uniqDateTime dateTime
    let uniqNGD      name guid dateTime           (dst:#Unique) = dst |> uniqName name |> uniqGuid guid |> uniqDateTime dateTime
    /// src unique 속성 (Id, Name, Guid, DateTime) 들을 dst 에 복사
    let uniqINGD     id name guid dateTime        (dst:#Unique) = dst |> uniqId id     |> uniqNGD name guid dateTime
    /// src unique 속성 (Id, Name, Guid, DateTime, RawParent) 들을 dst 에 복사
    let uniqINGDP    id name guid dateTime parent (dst:#Unique) = dst |> uniqId id     |> uniqNGD name guid dateTime |> uniqParent parent
    let uniqAll = uniqINGDP

    let uniqINGD_fromObj (src:#Unique) (dst:#Unique): #Unique =
        dst.Id <- src.Id
        dst.Name <- src.Name
        dst.Guid <- src.Guid
        dst.DateTime <- src.DateTime
        dst

    let uniqRenew (dst:#Unique): #Unique =
        dst.Id <- None
        dst.Guid <- newGuid()
        dst.DateTime <- now()
        dst


    type Unique with
        member this.Renew<'T when 'T :> Unique> () : 'T =
            // 'this'는 Unique 타입이라 강제로 캐스팅 필요
            let x = this :?> 'T
            x.Id <- None
            x.Guid <- System.Guid.NewGuid()
            x.DateTime <- System.DateTime.Now
            x


