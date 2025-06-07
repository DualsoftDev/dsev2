namespace Ev2.Core.FS

open System

open Dual.Common.Core.FS
open Dual.Common.Db.FS
open Dual.Common.Base

[<AutoOpen>]
module ORMTypesModule =

    type RowId = int
    type IORMProject    = inherit IORMUnique
    type IORMSystem     = inherit IORMUnique
    type IORMFlow       = inherit IORMUnique
    type IORMWork       = inherit IORMUnique
    type IORMCall       = inherit IORMUnique
    type IORMArrow      = inherit IORMUnique
    type IORMArrowWork  = inherit IORMArrow
    type IORMArrowCall  = inherit IORMArrow

    type IORMApiCall    = inherit IORMUnique
    type IORMApiDef     = inherit IORMUnique
    type IORMParamWork  = inherit IORMUnique
    type IORMParamCall  = inherit IORMUnique
    type IORMEnum       = inherit IORMUnique
    type IORMMeta       = inherit IORMUnique
    type IORMLog        = inherit IORMUnique

    [<AbstractClass>]
    type ORMUnique(name:string, guid:Guid, id:Id option, parameter:string, dateTime:DateTime) =
        inherit Unique()
        interface IORMUnique

        new() = ORMUnique(nullString, emptyGuid, None, nullString, minDate)
        /// Parent Id
        member val ParentId = Option<Id>.None with get, set

    /// Unique 객체의 속성정보 (Id, Name, Guid, DateTime)를 ORMUnique 객체에 저장
    let toOrmUniqINGDP (src:#Unique) (dst:#ORMUnique): #ORMUnique =
        dst |> fromUniqINGD src |> ignore

        dst.ParentId <- src.RawParent >>= _.Id
        dst.RtObject <- Some src
        src.ORMObject <- Some dst
        dst



    [<AbstractClass>]
    type ORMArrowBase(srcId:Id, tgtId:Id, parentId:Id option, arrowTypeId:Id) =
        inherit ORMUnique(ParentId=parentId)
        new() = ORMArrowBase(-1, -1, None, -1)
        interface IORMArrow
        member val Source = srcId with get, set
        member val Target = tgtId with get, set
        member val TypeId = arrowTypeId with get, set

    /// Work 간 연결.  System 에 속함
    type ORMArrowWork(srcId:Id, tgtId:Id, systemId:Id, arrowTypeId:Id) =
        inherit ORMArrowBase(srcId, tgtId, Some systemId, arrowTypeId)
        new() = ORMArrowWork(-1, -1, -1, -1)
        interface IORMArrowWork
        member val SystemId = systemId with get, set

    /// Call 간 연결.  Work 에 속함
    type ORMArrowCall(srcId:Id, tgtId:Id, workId:Id, arrowTypeId:Id) =
        inherit ORMArrowBase(srcId, tgtId, Some workId, arrowTypeId)
        new() = ORMArrowCall(-1, -1, -1, -1)
        interface IORMArrowCall
        member val WorkId = workId with get, set

    /// Object Releation Mapper for Asset
    type ORMProject(author:string, version, (*langVersion, engineVersion,*) description) =
        inherit ORMUnique()

        new() = ORMProject(Environment.UserName, nullVersion, nullString)
        interface IORMProject
        member val Author = author with get, set
        member val Version       = version     with get, set
        member val Description   = description with get, set


    type ORMSystem(prototypeId:Nullable<Id>, originGuid:Nullable<Guid>, iri:string, author:string, langVersion:Version, engineVersion:Version, description:string) =
        inherit ORMUnique()

        new() = ORMSystem(nullableId, emptyGuid, nullString, nullString, nullVersion, nullVersion, nullString)
        interface IORMSystem
        member val PrototypeId   = prototypeId   with get, set
        member val IRI           = iri           with get, set
        member val Author        = author        with get, set
        member val EngineVersion = engineVersion with get, set
        member val LangVersion   = langVersion   with get, set
        member val Description   = description   with get, set

        member val OriginGuid = originGuid with get, set

    type ORMFlow(systemId:Id) =
        inherit ORMUnique(ParentId=Some systemId)

        new() = ORMFlow(-1)
        interface IORMFlow
        member x.SystemId with get() = x.ParentId and set v = x.ParentId <- v

    type ORMWork(systemId:Id, status4Id:Nullable<Id>, flowId:Nullable<Id>) =
        inherit ORMUnique(ParentId=Some systemId)

        new() = ORMWork(-1, nullableId, nullableId)
        interface IORMWork

        member val FlowId     = flowId     with get, set
        member val Motion     = nullString with get, set
        member val Script     = nullString with get, set
        member val IsFinished = false      with get, set
        member val NumRepeat  = 0          with get, set
        member val Period     = 0          with get, set
        member val Delay      = 0          with get, set
        member val Status4Id = status4Id with get, set
        member x.SystemId with get() = x.ParentId and set v = x.ParentId <- v

    type ORMCall(workId:Id, status4Id:Nullable<Id>, callTypeId:Nullable<Id>, autoPre:string, safety:string, isDisabled:bool, timeout:Nullable<int>) =
        inherit ORMUnique(ParentId=Some workId)

        new() = ORMCall(-1, nullableId, (DbCallType.Normal |> int64 |> Nullable), nullString, nullString, false, nullableInt)
        interface IORMCall
        member x.WorkId with get() = x.ParentId and set v = x.ParentId <- v
        member val Status4Id  = status4Id  with get, set
        member val CallTypeId = callTypeId with get, set
        member val AutoPre    = autoPre    with get, set
        member val Safety     = safety     with get, set
        member val IsDisabled = isDisabled with get, set
        member val Timeout    = timeout    with get, set



    type ORMMapProjectSystem(projectId:Id, systemId:Id, isActive:bool) =
        inherit ORMUnique()

        new() = ORMMapProjectSystem(-1, -1, false)
        interface IORMRow
        member val ProjectId = projectId with get, set
        member val SystemId  = systemId  with get, set
        member val IsActive  = isActive  with get, set

    type ORMMapCall2ApiCall(callId:Id, apiCallId:Id) =
        inherit ORMUnique()

        new() = ORMMapCall2ApiCall(-1, -1)
        interface IORMRow
        member val CallId = callId with get, set
        member val ApiCallId = apiCallId with get, set

    //type ORMApiCall(systemId:Id) =
    type ORMApiCall(systemId:Id, apiDefId:Id, inAddress:string, outAddress:string, inSymbol:string, outSymbol:string,
        valueSpec:string
    ) =
        inherit ORMUnique(ParentId=Some systemId)

        new() = ORMApiCall(-1, -1, nullString, nullString, nullString, nullString, nullString)
        interface IORMApiCall
        member val SystemId = systemId with get, set
        member val ApiDefId = apiDefId with get, set

        member val InAddress   = inAddress   with get, set
        member val OutAddress  = outAddress  with get, set
        member val InSymbol    = inSymbol    with get, set
        member val OutSymbol   = outSymbol   with get, set
        member val ValueSpec   = valueSpec with get, set

        /// View only.  ValueSpec 에 대한 user friendly 표현.  e.g "3 <= x < 5".   TODO: ValueSpec 값 수정 시, tableHistory 를 통해 모니터링 하다가 update 해야 함.
        member x.ValueSpecHint =
            if x.ValueSpec.IsNullOrEmpty() then null
            else x.ValueSpec |> IValueSpec.Deserialize |> _.ToString()


    type ORMApiDef(systemId:Id) =
        inherit ORMUnique(ParentId=Some systemId)

        new() = ORMApiDef(-1)
        interface IORMApiDef
        member x.SystemId with get() = x.ParentId and set v = x.ParentId <- v
        member val IsPush = false with get, set


    type ORMEnum(name, category, value) =
        interface IORMEnum

        new() = ORMEnum(nullString, nullString, -1)
        interface IORMRow
        member val Id       = Nullable<Id>() with get, set
        member val Name     = name           with get, set
        member val Category = category       with get, set
        member val Value    = value          with get, set

