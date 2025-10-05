namespace Ev2.Core.FS

open System

open Dual.Common.Base
open Dual.Common.Db.FS

[<AutoOpen>]
module ORMTypesModule =
    [<AbstractClass>]
    type ORMUnique(name:string, guid:Guid, id:Id option, parameter:string) =
        inherit Unique(name, guid, parameter, ?id=id, ?parent=None)
        interface IORMUnique

        new() = new ORMUnique(nullString, emptyGuid, None, nullString)
        /// Parent Id
        member val ParentId = Option<Id>.None with get, set


    /// System 하부 entities: {ORMWork, ORMApiCall, OMRApiDef}
    [<AbstractClass>]
    type ORMSystemEntity(systemId:Id) =
        inherit ORMUnique(ParentId=Some systemId)
        interface ISystemEntity
        member x.SystemId with get() = x.ParentId and set v = x.ParentId <- v

    /// Json 직렬화되는 System 하부 entities: {Buttons, Lamps, Conditions, Actions}
    [<CLIMutable>]
    type ORMJsonSystemEntity = {
        Id        : Option<Id>
        Guid      : Guid
        SystemId  : Id
        Type      : string
        Json      : string
    }

    /// Work 하부 entities: {ORMFlow, ORMCall}
    [<AbstractClass>]
    type ORMWorkEntity(workId:Id) =
        inherit ORMUnique(ParentId=Some workId)
        member x.WorkId with get() = x.ParentId and set v = x.ParentId <- v


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
    type ORMProject(propertiesJson:string) = // Initialize
        inherit ORMUnique()

        new() = new ORMProject(nullString)

        member val PropertiesJson = propertiesJson with get, set
        member x.PropertiesJsonB = x.PropertiesJson |> JsonbString

        interface IORMProject

        member x.Initialize(runtime:Project) =
            runtime.CopyUniqueProperties(x)
            x.PropertiesJson <- runtime.PropertiesJson
            x


    type ORMSystem(ownerProjectId:Id option, iri:string, propertiesJson:string) = // Initialize
        inherit ORMUnique(ParentId=ownerProjectId)

        interface IORMSystem

        member x.ProjectId with get() = x.ParentId and set v = x.ParentId <- v

        new() = new ORMSystem(None, nullString, nullString)

        member val PropertiesJson = propertiesJson with get, set
        member x.PropertiesJsonB = x.PropertiesJson |> JsonbString

        member val PolymorphicJsonEntities = PolymorphicJsonCollection<JsonPolymorphic>() with get, set
        member val OwnerProjectId = ownerProjectId with get, set
        member val IRI = iri with get, set

        member x.Initialize(runtime:DsSystem) =
            runtime.CopyUniqueProperties(x)
            x.IRI <- runtime.IRI
            x.OwnerProjectId <- runtime.OwnerProjectId
            x.PropertiesJson <- runtime.PropertiesJson
            x

    type ORMFlow(systemId:Id, propertiesJson:string) = // Initialize
        inherit ORMWorkEntity(systemId)

        new() = new ORMFlow(-1, nullString)
        interface IORMFlow
        member x.SystemId with get() = x.ParentId and set v = x.ParentId <- v
        member val Properties = propertiesJson with get, set
        member x.PropertiesJsonB = x.PropertiesJson |> JsonbString
        member x.PropertiesJson
            with get() = x.Properties
            and set value = x.Properties <- value

        member x.Initialize(runtime:Flow) =
            runtime.CopyUniqueProperties(x)
            x.SystemId <- runtime.System |-> _.Id |? None
            x.Properties <- runtime.PropertiesJson
            x

    type ORMWork(systemId:Id, status4Id:Id option, flowId:Id option, flowGuid:Guid option, propertiesJson:string) = // Initialize
        inherit ORMSystemEntity(systemId)

        new() = new ORMWork(-1, None, None, noneGuid, nullString)
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
        member val Properties = propertiesJson with get, set
        member x.PropertiesJsonB = x.PropertiesJson |> JsonbString
        member x.PropertiesJson
            with get() = x.Properties
            and set value = x.Properties <- value

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
            x.Properties <- runtime.PropertiesJson
            x

    type ORMCall(workId:Id, status4Id:Id option // Initialize
        , callTypeId:Id option, autoConditions: string, commonConditions: string, isDisabled:bool, timeout:int option, propertiesJson:string
    ) =
        inherit ORMWorkEntity(workId)

        new() = new ORMCall(-1, None, None, null, null, false, None, nullString)
        interface IORMCall
        member x.WorkId with get() = x.ParentId and set v = x.ParentId <- v
        member val Status4Id  = status4Id  with get, set
        member val CallTypeId = callTypeId with get, set
        member val IsDisabled = isDisabled with get, set
        member val Timeout    = timeout    with get, set
        member val AutoConditions   = autoConditions   with get, set
        member val CommonConditions = commonConditions with get, set
        member val Properties = propertiesJson with get, set
        member x.PropertiesJsonB = x.PropertiesJson |> JsonbString
        member x.PropertiesJson
            with get() = x.Properties
            and set value = x.Properties <- value

        member x.Initialize(runtime:Call) =
            runtime.CopyUniqueProperties(x)
            (* 다음의 enum 에 대한 id 설정은 dbApi 가 있을 때 해결되어야 한다. *)
            //x.CallTypeId <- runtime.CallType |> int64 |> Some
            //x.Status4Id <- runtime.Status4 |-> int64

            x.IsDisabled <- runtime.IsDisabled
            x.Timeout <- runtime.Timeout
            // ApiCallValueSpecs를 JSON 문자열로 변환
            x.AutoConditions   <- runtime.AutoConditions.ToJson()
            x.CommonConditions <- runtime.CommonConditions.ToJson()
            x.Properties <- runtime.PropertiesJson
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
        , valueSpec:string, ioTagsJson:string, propertiesJson:string
    ) =
        inherit ORMSystemEntity(systemId)

        new() = new ORMApiCall(-1, -1, nullString, nullString, nullString, nullString, nullString, nullString, nullString)
        interface IORMApiCall
        member val ApiDefId = apiDefId with get, set

        member val InAddress   = inAddress   with get, set
        member val OutAddress  = outAddress  with get, set
        member val InSymbol    = inSymbol    with get, set
        member val OutSymbol   = outSymbol   with get, set
        member val ValueSpec   = valueSpec with get, set
        member val IOTagsJson  = ioTagsJson with get, set
        member val Properties  = propertiesJson with get, set
        member x.PropertiesJsonB = x.PropertiesJson |> JsonbString
        member x.PropertiesJson
            with get() = x.Properties
            and set value = x.Properties <- value

        /// View only.  ValueSpec 에 대한 user friendly 표현.  e.g "3 <= x < 5".   TODO: ValueSpec 값 수정 시, tableHistory 를 통해 모니터링 하다가 update 해야 함.
        member x.ValueSpecHint =
            if x.ValueSpec.IsNullOrEmpty() then null
            else x.ValueSpec |> IValueSpec.Deserialize |> _.ToString()


    type ORMApiDef(systemId:Id, propertiesJson:string) =
        inherit ORMSystemEntity(systemId)

        new() = new ORMApiDef(-1, nullString)
        interface IORMApiDef
        member val Properties = propertiesJson with get, set
        member x.PropertiesJsonB = x.PropertiesJson |> JsonbString
        member x.PropertiesJson
            with get() = x.Properties
            and set value = x.Properties <- value

    type ORMEnum(name, category, value) =
        interface IORMEnum

        new() = new ORMEnum(nullString, nullString, -1)
        interface IORMUnique
        member val Id       = Option<Id>.None with get, set
        member val Name     = name            with get, set
        member val Category = category        with get, set
        member val Value    = value           with get, set
