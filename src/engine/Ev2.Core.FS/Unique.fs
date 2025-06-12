namespace Ev2.Core.FS

open System

open Dual.Common.Base
open Dual.Common.Core.FS
open Newtonsoft.Json
open Dual.Common.Db.FS


[<AutoOpen>]
module Interfaces =
    type Id = int64
    /// 기본 객체 인터페이스
    type IDsObject  = interface end
    type IParameter = inherit IDsObject
    type IParameterContainer = inherit IDsObject
    type IArrow     = inherit IDsObject
    /// Guid, Name, DateTime
    type IUnique     = inherit IDsObject

    type IWithDateTime =
        inherit IDsObject
        /// DateTime 속성을 가지는 객체 인터페이스
        abstract member DateTime: DateTime with get, set

    type IDs1stClass = inherit IUnique inherit IWithDateTime
    type IDs2ndClass = inherit IUnique

    (* Project > System > Work > Call > ApiCall > ApiDef *)

    type IDsProject = inherit IDs1stClass
    type IDsSystem  = inherit IDs1stClass
    type IDsFlow    = inherit IDs2ndClass
    type IDsWork    = inherit IDs2ndClass
    type IDsCall    = inherit IDs2ndClass
    type IDsApiCall = inherit IDs2ndClass
    type IDsApiDef  = inherit IDs2ndClass

    type IDsButton    = inherit IDs2ndClass
    type IDsLamp      = inherit IDs2ndClass
    type IDsCondition = inherit IDs2ndClass
    type IDsAction    = inherit IDs2ndClass


    /// Runtime 객체 인터페이스
    type IRtObject  = inherit IUnique
    type IRtUnique  = inherit IRtObject

    /// Newtonsoft JSON 객체 인터페이스
    type INjObject  = inherit IUnique
    type INjUnique  = inherit INjObject

    /// ORM 객체 인터페이스
    type IORMObject  = inherit IUnique
    type IORMUnique  = inherit IORMObject inherit IORMRow

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


    let mutable internal fwdDuplicate:  IUnique->IUnique = let dummy (src:IUnique) = failwithlog "Should be reimplemented." in dummy

    [<AbstractClass>]
    type Unique(name:string, guid:Guid, parameter:string, ?id:Id, ?parent:Unique) =
        interface IUnique

        internal new() = Unique(nullString, newGuid(), nullString, ?id=None, ?parent=None)

        /// DB 저장시의 primary key id.  DB read/write 수행한 경우에만 Non-null
        [<JsonProperty(Order = -100)>] member val Id = id with get, set

        [<JsonProperty(Order = -100)>] member val Name = name with get, set
        [<JsonProperty(Order = -97)>]  member val Parameter = parameter with get, set

        /// Guid: 메모리에 최초 객체 생성시 생성
        [<JsonProperty(Order = -98)>]  member val Guid:Guid = guid with get, set

        /// 자신의 container 에 해당하는 parent DS 객체.  e.g call -> work -> system -> project, flow -> system
        [<JsonIgnore>] member val RawParent = parent with get, set

        // { 내부 구현 전용.  serialize 대상에서 제외됨
        member val internal ORMObject = Option<IORMUnique>.None with get, set
        member val internal NjObject  = Option<INjUnique> .None with get, set
        member val internal RtObject  = Option<IRtUnique> .None with get, set
        member val internal DDic      = DynamicDictionary()
        // } 내부 구현 전용.  serialize 대상에서 제외됨


    let mutable fwdReplicateProperties: Unique -> Unique -> Unique = let dummy (src:Unique) (dst:Unique) = failwith "Should be reimplemented" in dummy
    let replicateProperties (src:#Unique) (dst:#Unique): #Unique =
        fwdReplicateProperties src dst |> ignore
        dst

[<AutoOpen>]
module internal UniqueHelpers =
    /// Unique 객체의 RawParent 설정.  pipe 지원
    let setParent (parent:Unique) (x:#Unique) : #Unique =
        x.RawParent <- Some parent
        x

    /// Unique 객체의 RawParent 설정.  pipe 미지원. (unit 반환)
    let setParentI (parent:Unique) (x:#Unique): unit = x.RawParent <- Some parent

    /// Unique 객체의 RawParent 제거
    let clearParentI (x:#Unique): unit = x.RawParent <- None

    /// Unique 객체의 parent guid 부합 체크
    let isParentGuid (x:#Unique) (maybeParentGuid:Guid) = x.RawParent |-> _.Guid = Some maybeParentGuid

    (*
        Chaining 해서 사용할 수 있는 Uniq 속성 수정 helper 함수들.  예제
            dsProject
            |> uniqDateTime (now())
            |> uniqGuid (newGuid())
            |> uniqId (Some 3)
            |> uniqName "KKKKKKKKKKKKK"
    *)


    let private uniqParameter param    (dst:#Unique) = dst.Parameter <- param;    dst
    let uniqId        id       (dst:#Unique) = dst.Id        <- id;       dst
    let uniqDateTime  dateTime (dst:#Unique) = dst |> tryCast<IWithDateTime> |> iter (fun z -> z.DateTime <- dateTime); dst
    let uniqName      name     (dst:#Unique) = dst.Name      <- name;     dst
    let uniqGuid      guid     (dst:#Unique) = dst.Guid      <- guid;     dst
    let uniqParent    (parent:#Unique option) (dst:#Unique) = dst.RawParent <- parent >>= tryCast<Unique>; dst

    ////let uniqGD       guid dateTime                (dst:#Unique) = dst |> uniqGuid guid |> uniqDateTime dateTime
    //let uniqNGA      name guid args               (dst:#Unique) = dst |> uniqName name |> uniqGuid guid |> uniqParameter args
    //let uniqNGDA     name guid dateTime args      (dst:#Unique) = dst |> uniqNGA name guid args |> uniqDateTime dateTime
    ///// src unique 속성 (Id, Name, Guid, DateTime) 들을 dst 에 복사
    //let uniqINGA     id name guid args            (dst:#Unique) = dst |> uniqId id     |> uniqNGA name guid args
    ////let uniqINGDA    id name guid dateTime args   (dst:#Unique) = dst |> uniqId id     |> uniqNGDA name guid dateTime args

    //let private linkUniqNA (src:#Unique) (dst:#Unique): #Unique =
    //    dst
    //    |> linkUniq src
    //    |> uniqName src.Name
    //    |> uniqParameter src.Parameter

    //let private linkUniqINA (src:#Unique) (dst:#Unique): #Unique =
    //    dst
    //    |> linkUniqNA src
    //    |> uniqId src.Id


    //let private linkUniqNGA (src:#Unique) (dst:#Unique):#Unique =
    //    dst
    //    |> linkUniqNA src
    //    |> uniqGuid src.Guid



