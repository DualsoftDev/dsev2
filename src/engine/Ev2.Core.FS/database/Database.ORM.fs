namespace Ev2.Core.FS

open System

open Dual.Common.Core.FS
open Dual.Common.Db.FS
open Dual.Common.Base
open Newtonsoft.Json

[<AutoOpen>]
module ORMTypesModule =
    [<AbstractClass>]
    type ORMUnique(name:string, guid:Guid, id:Id option, parameter:string) =
        inherit Unique(name, guid, parameter, ?id=id, ?parent=None)
        interface IORMUnique

        new() = new ORMUnique(nullString, emptyGuid, None, nullString)
        /// Parent Id
        member val ParentId = Option<Id>.None with get, set

    [<AbstractClass>]
    type ORMProjectEntity(?projectId:Id) =
        inherit ORMUnique(ParentId=projectId)
        member x.ProjectId with get() = x.ParentId and set v = x.ParentId <- v

    [<AbstractClass>]
    type ORMSystemEntity(systemId:Id) =
        inherit ORMUnique(ParentId=Some systemId)
        interface ISystemEntity
        member x.SystemId with get() = x.ParentId and set v = x.ParentId <- v

    [<AbstractClass>]
    type ORMSystemEntityWithFlow(systemId:Id, flowId:Id option) =
        inherit ORMSystemEntity(systemId)
        interface ISystemEntityWithFlow
        member val FlowId = flowId with get, set

    [<AbstractClass>]
    type ORMFlowEntity(flowId:Id) =
        inherit ORMUnique(ParentId=Some flowId)
        member x.FlowId with get() = x.ParentId and set v = x.ParentId <- v

    [<AbstractClass>]
    type ORMWorkEntity(workId:Id) =
        inherit ORMUnique(ParentId=Some workId)
        member x.WorkId with get() = x.ParentId and set v = x.ParentId <- v

    [<AbstractClass>]
    type ORMCallEntity(callId:Id) =
        inherit ORMUnique(ParentId=Some callId)
        member x.CallId with get() = x.ParentId and set v = x.ParentId <- v











    [<AbstractClass>]
    type ORMArrowBase(srcId:Id, tgtId:Id, parentId:Id option, arrowTypeId:Id, srcGuid:Guid, tgtGuid:Guid) =
        inherit ORMUnique(ParentId=parentId)
        new() = new ORMArrowBase(-1, -1, None, -1, emptyGuid, emptyGuid)
        interface IORMArrow
        member val Source = srcId with get, set
        member val Target = tgtId with get, set
        member val TypeId = arrowTypeId with get, set

        //{ 실제 DB 에 저장되지 않는 data.  저장소 변환시 자료 복사 용도로만 사용
        member val XSourceGuid = srcGuid with get, set
        member val XTargetGuid = tgtGuid with get, set
        member val XType = DbArrowType.None with get, set
        //}


    /// Work 간 연결.  System 에 속함
    type ORMArrowWork(srcId:Id, tgtId:Id, systemId:Id, arrowTypeId:Id, srcGuid:Guid, tgtGuid:Guid) =
        inherit ORMArrowBase(srcId, tgtId, Some systemId, arrowTypeId, srcGuid, tgtGuid)
        new() = new ORMArrowWork(-1, -1, -1, -1, emptyGuid, emptyGuid)
        interface IORMArrowWork
        member val SystemId = systemId with get, set

    /// Call 간 연결.  Work 에 속함
    type ORMArrowCall(srcId:Id, tgtId:Id, workId:Id, arrowTypeId:Id, srcGuid:Guid, tgtGuid:Guid) =
        inherit ORMArrowBase(srcId, tgtId, Some workId, arrowTypeId, srcGuid, tgtGuid)
        new() = new ORMArrowCall(-1, -1, -1, -1, emptyGuid, emptyGuid)
        interface IORMArrowCall
        member val WorkId = workId with get, set

    /// Object Releation Mapper for Asset
    type ORMProject(author:string, version, (*langVersion, engineVersion,*) description, dateTime) = // Initialize
        inherit ORMUnique()

        new() = new ORMProject(Environment.UserName, nullVersion, nullString, minDate)
        interface IORMProject with
            member x.DateTime  with get() = x.DateTime and set v = x.DateTime <- v

        member val AasxPath    = nullString with get, set // AASX 파일 경로.
        member val Author      = author      with get, set
        member val Version     = version     with get, set
        member val Description = description with get, set
        member val DateTime    = dateTime    with get, set

        member x.Initialize(runtime:Project) =
            runtime.CopyUniqueProperties(x)
            x.DateTime <- runtime.DateTime
            x.Author <- runtime.Author
            x.Version <- runtime.Version
            x.Description <- runtime.Description
            x


    type ORMSystem(ownerProjectId:Id option // Initialize
        , iri:string, author:string, langVersion:Version, engineVersion:Version
        , description:string, dateTime
        , polys: PolymorphicJsonCollection<SystemEntityWithJsonPolymorphic>    // Button, Lamp, Condition, Action
    ) =
        inherit ORMProjectEntity()

        new() = new ORMSystem(None, nullString, nullString, nullVersion, nullVersion, nullString, minDate, PolymorphicJsonCollection<SystemEntityWithJsonPolymorphic>())
        interface IORMSystem with
            member x.DateTime  with get() = x.DateTime and set v = x.DateTime <- v

        member val PolymorphicJsonEntities = polys with get, set
        member val OwnerProjectId = ownerProjectId with get, set

        member val IRI           = iri           with get, set
        member val Author        = author        with get, set
        member val EngineVersion = engineVersion with get, set
        member val LangVersion   = langVersion   with get, set
        member val Description   = description   with get, set
        member val DateTime      = dateTime      with get, set

        member x.Initialize(runtime:DsSystem) =
            runtime.CopyUniqueProperties(x)
            x.DateTime <- runtime.DateTime
            x.IRI <- runtime.IRI
            x.Author <- runtime.Author
            x.EngineVersion <- runtime.EngineVersion
            x.LangVersion <- runtime.LangVersion
            x.Description <- runtime.Description
            x.OwnerProjectId <- runtime.OwnerProjectId
            x

    type ORMFlow(systemId:Id) = // Initialize
        inherit ORMWorkEntity(systemId)

        new() = new ORMFlow(-1)
        interface IORMFlow
        member x.SystemId with get() = x.ParentId and set v = x.ParentId <- v

        member x.Initialize(runtime:Flow) =
            runtime.CopyUniqueProperties(x)
            x.SystemId <- runtime.System |-> _.Id |? None
            x


    type ORMButton(systemId:Id, flowId:Id option, ioTagsJson:string) =
        inherit ORMSystemEntityWithFlow(systemId, flowId)

        new() = new ORMButton(-1, None, nullString)
        interface IORMButton
        member val IOTagsJson = ioTagsJson with get, set

    type ORMLamp(systemId:Id, flowId:Id option, ioTagsJson:string) =
        inherit ORMSystemEntityWithFlow(systemId, flowId)

        new() = new ORMLamp(-1, None, nullString)
        interface IORMLamp
        member val IOTagsJson = ioTagsJson with get, set

    type ORMCondition(systemId:Id, flowId:Id option, ioTagsJson:string) =
        inherit ORMSystemEntityWithFlow(systemId, flowId)

        new() = new ORMCondition(-1, None, nullString)
        interface IORMCondition
        member val IOTagsJson = ioTagsJson with get, set

    type ORMAction(systemId:Id, flowId:Id option, ioTagsJson:string) =
        inherit ORMSystemEntityWithFlow(systemId, flowId)

        new() = new ORMAction(-1, None, nullString)
        interface IORMAction
        member val IOTagsJson = ioTagsJson with get, set


    type ORMWork(systemId:Id, status4Id:Id option, flowId:Id option, flowGuid:Guid option) = // Initialize
        inherit ORMSystemEntity(systemId)

        new() = new ORMWork(-1, None, None, noneGuid)
        interface IORMWork

        member val FlowId     = flowId     with get, set
        member val FlowGuid   = flowGuid   with get, set
        member val Motion       = nullString with get, set
        member val Script       = nullString with get, set
        member val ExternalStart = nullString with get, set
        member val IsFinished   = false      with get, set
        member val NumRepeat  = 0          with get, set
        member val Period     = 0          with get, set
        member val Delay      = 0          with get, set
        member val Status4Id = status4Id with get, set

        member x.Initialize(runtime:Work) =
            runtime.CopyUniqueProperties(x)
            x.Motion <- runtime.Motion
            x.Script <- runtime.Script
            x.ExternalStart <- runtime.ExternalStart
            x.IsFinished <- runtime.IsFinished
            x.NumRepeat <- runtime.NumRepeat
            x.Period <- runtime.Period
            x.Delay <- runtime.Delay
            x.FlowId <- runtime.Flow |-> _.Id |? None
            x.Status4Id <- runtime.Status4 |-> int64
            x

    type ORMCall(workId:Id, status4Id:Id option // Initialize
        , callTypeId:Id option, autoConditions: string, commonConditions: string, isDisabled:bool, timeout:int option
    ) =
        inherit ORMWorkEntity(workId)

        new() = new ORMCall(-1, None, (DbCallType.Normal |> int64 |> Some), null, null, false, None)
        interface IORMCall
        member x.WorkId with get() = x.ParentId and set v = x.ParentId <- v
        member val Status4Id  = status4Id  with get, set
        member val CallTypeId = callTypeId with get, set
        member val IsDisabled = isDisabled with get, set
        member val Timeout    = timeout    with get, set
        member val AutoConditions   = autoConditions   with get, set
        member val CommonConditions = commonConditions with get, set

        member x.Initialize(runtime:Call) =
            runtime.CopyUniqueProperties(x)
            x.CallTypeId <- runtime.CallType |> int64 |> Some
            x.IsDisabled <- runtime.IsDisabled
            x.Timeout <- runtime.Timeout
            // ApiCallValueSpecs를 JSON 문자열로 변환
            x.AutoConditions <- if runtime.AutoConditions.Count = 0 then null else runtime.AutoConditions.ToJson()
            x.CommonConditions <- if runtime.CommonConditions.Count = 0 then null else runtime.CommonConditions.ToJson()
            x.Status4Id <- runtime.Status4 |-> int64
            x



    type ORMMapProjectSystem(projectId:Id, systemId:Id, isActiveSystem:bool) =
        inherit ORMUnique()

        new() = new ORMMapProjectSystem(-1, -1, false)
        member val ProjectId = projectId with get, set
        member val SystemId  = systemId  with get, set
        member val IsActiveSystem = isActiveSystem with get, set

    type ORMMapCall2ApiCall(callId:Id, apiCallId:Id) =
        inherit ORMUnique()

        new() = new ORMMapCall2ApiCall(-1, -1)
        member val CallId = callId with get, set
        member val ApiCallId = apiCallId with get, set

    //type ORMApiCall(systemId:Id) =
    type ORMApiCall(systemId:Id, apiDefId:Id // ValueSpecHint
        , inAddress:string, outAddress:string
        , inSymbol:string, outSymbol:string
        , valueSpec:string, ioTagsJson:string
    ) =
        inherit ORMSystemEntity(systemId)

        new() = new ORMApiCall(-1, -1, nullString, nullString, nullString, nullString, nullString, nullString)
        interface IORMApiCall
        member val ApiDefId = apiDefId with get, set

        member val InAddress   = inAddress   with get, set
        member val OutAddress  = outAddress  with get, set
        member val InSymbol    = inSymbol    with get, set
        member val OutSymbol   = outSymbol   with get, set
        member val ValueSpec   = valueSpec with get, set
        member val IOTagsJson  = ioTagsJson with get, set

        /// View only.  ValueSpec 에 대한 user friendly 표현.  e.g "3 <= x < 5".   TODO: ValueSpec 값 수정 시, tableHistory 를 통해 모니터링 하다가 update 해야 함.
        member x.ValueSpecHint =
            if x.ValueSpec.IsNullOrEmpty() then null
            else x.ValueSpec |> IValueSpec.Deserialize |> _.ToString()


    type ORMApiDef(systemId:Id) =
        inherit ORMSystemEntity(systemId)

        new() = new ORMApiDef(-1)
        interface IORMApiDef
        member val IsPush = false with get, set

        member val TxId = Option<Id>.None with get, set
        member val RxId = Option<Id>.None with get, set

        member val XTxGuid = emptyGuid with get, set
        member val XRxGuid = emptyGuid with get, set

    type ORMEnum(name, category, value) =
        interface IORMEnum

        new() = new ORMEnum(nullString, nullString, -1)
        interface IORMUnique
        member val Id       = Option<Id>.None with get, set
        member val Name     = name            with get, set
        member val Category = category        with get, set
        member val Value    = value           with get, set

