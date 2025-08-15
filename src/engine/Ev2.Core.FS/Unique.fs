namespace Ev2.Core.FS

open System

open Dual.Common.Base
open Dual.Common.Core.FS
open Newtonsoft.Json
open Dual.Common.Db.FS


[<AutoOpen>]
module Interfaces =

    [<AbstractClass>]
    type Unique(name:string, guid:Guid, parameter:string, ?id:Id, ?parent:Unique) =     // CopyUniqueProperties, isDuplicated
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

        abstract member CopyUniqueProperties : Unique -> unit
        default x.CopyUniqueProperties (dst:Unique) : unit =
            dst.Id        <- x.Id
            dst.Name      <- x.Name
            dst.Parameter <- x.Parameter
            dst.Guid      <- x.Guid
            dst.RawParent <- x.RawParent

        // { 내부 구현 전용.  serialize 대상에서 제외됨
        member val internal ORMObject = Option<IORMUnique>.None with get, set
        member val internal NjObject  = Option<INjUnique> .None with get, set
        member val internal RtObject  = Option<IRtUnique> .None with get, set
        member val internal DDic      = DynamicDictionary()
        // } 내부 구현 전용.  serialize 대상에서 제외됨

        static member isDuplicated (x:Unique) (y:Unique) =
            let dup =
                x.Name.NonNullAny() && (x.Name = y.Name)
                || x.Guid = y.Guid
                || x.Id.IsSome && (x.Id = y.Id)
            if dup then
                noop()
            dup


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

