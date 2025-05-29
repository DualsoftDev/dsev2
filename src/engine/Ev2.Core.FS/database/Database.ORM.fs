namespace Ev2.Core.FS

open System

open Dual.Common.Core.FS
open Dual.Common.Db.FS
open Dual.Common.Base

[<AutoOpen>]
module ORMTypesModule =

    type RowId = int
    type IORMUnique     = inherit IUnique inherit IORMRow
    type IORMProject    = inherit IORMUnique
    type IORMSystem     = inherit IORMUnique
    type IORMFlow       = inherit IORMUnique
    type IORMWork       = inherit IORMUnique
    type IORMCall       = inherit IORMUnique

    type IORMApiCall    = inherit IORMUnique
    type IORMApiDef     = inherit IORMUnique
    type IORMParamWork  = inherit IORMUnique
    type IORMParamCall  = inherit IORMUnique
    type IORMEnum       = inherit IORMUnique
    type IORMMeta       = inherit IORMUnique
    type IORMLog        = inherit IORMUnique

    [<AbstractClass>]
    type ORMUnique(name:string, guid:Guid, id:Nullable<Id>, dateTime:DateTime) =
        interface IORMUnique

        member val Id = id with get, set
        /// Parent Id
        member val ParentId = Nullable<Id>() with get, set
        member val Name = name with get, set

        member val Guid = guid2str guid with get, set

        member val DateTime = dateTime with get, set
        member val RawParent = Option<ORMUnique>.None with get, set

        new() = ORMUnique(nullString, emptyGuid, nullableId, minDate)

    /// ORMUnique 객체의 속성정보 (Id, Name, Guid, DateTime)를 Unique 객체에 저장
    let fromOrmUniqINGD (src:#ORMUnique) (dst:#Unique): #Unique =
        dst.Id <- n2o src.Id
        dst.Name <- src.Name
        dst.Guid <- s2guid src.Guid
        dst.DateTime <- src.DateTime
        dst

    /// Unique 객체의 속성정보 (Id, Name, Guid, DateTime)를 ORMUnique 객체에 저장
    let toOrmUniqINGDP (src:#Unique) (dst:#ORMUnique): #ORMUnique =
        dst.Id <- o2n src.Id
        dst.Name <- src.Name
        dst.Guid <- guid2str src.Guid
        dst.DateTime <- src.DateTime
        let pid = src.RawParent >>= _.Id
        dst.ParentId <- o2n pid
        dst



    [<AbstractClass>]
    type ORMArrowBase(srcId:int, tgtId:int, parentId:Id, arrowTypeId:Id) =
        inherit ORMUnique(ParentId=parentId)
        new() = ORMArrowBase(-1, -1, -1, -1)
        member val Source = srcId with get, set
        member val Target = tgtId with get, set
        member val TypeId = arrowTypeId with get, set

    /// Work 간 연결.  System 에 속함
    type ORMArrowWork(srcId:int, tgtId:int, systemId:int, arrowTypeId:Id) =
        inherit ORMArrowBase(srcId, tgtId, systemId, arrowTypeId)
        new() = ORMArrowWork(-1, -1, -1, -1)
        member val SystemId = systemId with get, set

    /// Call 간 연결.  Work 에 속함
    type ORMArrowCall(srcId:int, tgtId:int, workId:int, arrowTypeId:Id) =
        inherit ORMArrowBase(srcId, tgtId, workId, arrowTypeId)
        new() = ORMArrowCall(-1, -1, -1, -1)
        member val WorkId = workId with get, set

    /// Object Releation Mapper for Asset
    type ORMProject(author:string, version, (*langVersion, engineVersion,*) description) =
        inherit ORMUnique()

        new() = ORMProject(Environment.UserName, nullVersion, nullString)
        interface IORMProject
        member val Author = author with get, set
        member val Version       = version     with get, set
        member val Description   = description with get, set


    type ORMSystem(isPrototype:bool, originGuid:Nullable<Guid>, author:string, langVersion:Version, engineVersion:Version, description:string) =
        inherit ORMUnique()

        new() = ORMSystem(false, emptyGuid, nullString, nullVersion, nullVersion, nullString)
        interface IORMSystem
        member val Prototype     = isPrototype   with get, set
        member val Author        = author        with get, set
        member val EngineVersion = engineVersion with get, set
        member val LangVersion   = langVersion   with get, set
        member val Description   = description   with get, set

        member val OriginGuid = originGuid with get, set

    type ORMFlow(systemId:Id) =
        inherit ORMUnique(ParentId=systemId)

        new() = ORMFlow(-1)
        interface IORMFlow
        member x.SystemId with get() = x.ParentId and set v = x.ParentId <- v

    type ORMWork(systemId:Id, flowId:Nullable<Id>) =
        inherit ORMUnique(ParentId=systemId)

        new() = ORMWork(-1, nullableId)
        interface IORMWork
        member val FlowId = flowId with get, set
        member x.SystemId with get() = x.ParentId and set v = x.ParentId <- v

    type ORMCall(workId:Id, callTypeId:Nullable<int>, autoPre:string, safety:string, timeout:Nullable<int>) =
        inherit ORMUnique(ParentId=workId)

        new() = ORMCall(-1, DbCallType.Normal |> int |> Nullable, nullString, nullString, nullableInt)
        interface IORMCall
        member x.WorkId with get() = x.ParentId and set v = x.ParentId <- v
        member val CallTypeId = callTypeId with get, set
        member val AutoPre = autoPre with get, set
        member val Safety = safety with get, set
        member val Timeout = timeout with get, set



    type ORMMapProjectSystem(projectId:Id, systemId:Id, isActive:bool) =
        inherit ORMUnique()

        new() = ORMMapProjectSystem(-1, -1, false)
        member val ProjectId = projectId with get, set
        member val SystemId = systemId with get, set
        member val IsActive = isActive with get, set

    type ORMMapCall2ApiCall(callId:Id, apiCallId:Id) =
        inherit ORMUnique()

        new() = ORMMapCall2ApiCall(-1, -1)
        member val CallId = callId with get, set
        member val ApiCallId = apiCallId with get, set

    //type ORMApiCall(systemId:Id) =
    type ORMApiCall(systemId:Id, apiDefId:Id, inAddress:string, outAddress:string, inSymbol:string, outSymbol:string, valueTypeId:Id, value:string) =
        inherit ORMUnique(ParentId=systemId)

        new() = ORMApiCall(-1, -1, nullString, nullString, nullString, nullString, -1, nullString)
        interface IORMApiCall
        member val SystemId = systemId with get, set
        member val ApiDefId = apiDefId with get, set

        member val InAddress  = inAddress   with get, set
        member val OutAddress = outAddress  with get, set
        member val InSymbol   = inSymbol    with get, set
        member val OutSymbol  = outSymbol   with get, set
        member val ValueTypeId= valueTypeId with get, set
        member val Value      = value       with get, set


    type ORMApiDef(systemId:Id) =
        inherit ORMUnique(ParentId=systemId)

        new() = ORMApiDef(-1)
        interface IORMApiDef
        member x.SystemId with get() = x.ParentId and set v = x.ParentId <- v
        member val IsPush = false with get, set


    type ORMEnum(name, category, value) =
        interface IORMEnum
        new() = ORMEnum(nullString, nullString, invalidInt)
        member val Id = Nullable<Id>() with get, set
        member val Name = name with get, set
        member val Category = category with get, set
        member val Value = value with get, set

