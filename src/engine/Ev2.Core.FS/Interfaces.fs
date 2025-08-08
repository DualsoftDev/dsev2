namespace Ev2.Core.FS

open System
open System.Linq

open Dual.Common.Base
open Dual.Common.Core.FS
open System.Collections.Generic
open Dual.Common.Db.FS
open Newtonsoft.Json

[<AutoOpen>]
module InterfaceModule =
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


    type IRtParameter = inherit IRtUnique inherit IParameter
    type IRtParameterContainer = inherit IRtUnique inherit IParameterContainer

    type IRtArrow     = inherit IRtUnique inherit IArrow

    type IRtProject = inherit IRtUnique inherit IWithDateTime
    type IRtSystem  = inherit IRtUnique inherit IWithDateTime
    type IRtFlow    = inherit IRtUnique inherit IDsFlow
    type IRtWork    = inherit IRtUnique inherit IDsWork
    type IRtCall    = inherit IRtUnique inherit IDsCall
    type IRtApiCall = inherit IRtUnique inherit IDsApiCall
    type IRtApiDef  = inherit IRtUnique inherit IDsApiDef

    type IRtButton    = inherit IRtUnique inherit IDsButton
    type IRtLamp      = inherit IRtUnique inherit IDsLamp
    type IRtCondition = inherit IRtUnique inherit IDsCondition
    type IRtAction    = inherit IRtUnique inherit IDsAction


    let internal minDate      = DateTime.MinValue
    let internal nullableId   = Nullable<Id>()
    let internal nullVersion  = null:Version
    let internal nullString   = null:string
    let internal nullableInt  = Nullable<int>()
    let internal nullableGuid = Nullable<Guid>()
    let internal noneGuid     = Option<Guid>.None
    let internal emptyGuid    = Guid.Empty
    let internal newGuid()    = Guid.NewGuid()
    let s2guid (s:string) = Guid.Parse s
    let guid2str (g:Guid) = g.ToString("D")
    let formatDateTime(dt:DateTime) = dt.ToString("yyyy-MM-ddTHH:mm:ss")

    let internal now() =
        let x = AppSettings.TheAppSettings
        let y = x.UseUtcTime
        if AppSettings.TheAppSettings.UseUtcTime then DateTime.UtcNow else DateTime.Now


    let mutable internal fwdDuplicate:  IUnique->IUnique = let dummy (src:IUnique) = failwithlog "Should be reimplemented." in dummy
    let mutable internal fwdReplicate:  IUnique->IUnique = let dummy (src:IUnique) = failwithlog "Should be reimplemented." in dummy
