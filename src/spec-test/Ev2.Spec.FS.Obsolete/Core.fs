namespace Dual.EV2.Core

open System
open Dual.EV2.CoreParameter.Params
open Newtonsoft.Json

type IUnique =
    abstract Id: int option
    abstract Guid: Guid
    abstract Name: string
    abstract DateTime: DateTime

type IArrow = IUnique * IUnique

[<AbstractClass>]
type Unique(name: string, guid: Guid, dateTime: DateTime, ?id: int option, ?parent: Unique option) =
    // 기본 생성자: 내부에서만 사용
    internal new() = Unique("", Guid.Empty, DateTime.MinValue, None, None)

    // 속성들 정의
    member val Id: int option = defaultArg id None with get, set
    member val Name: string = name with get, set
    member val Guid: Guid = guid with get, set
    member val DateTime: DateTime = dateTime with get, set
    member val RawParent: Unique option = defaultArg parent None with get, set

    /// Parent Guid (RawParent의 Guid)
    member this.PGuid: Guid option = 
        match this.RawParent with
        | Some p -> Some p.Guid
        | None -> None

    interface IUnique with
        member this.Id = this.Id
        member this.Name = this.Name
        member this.Guid = this.Guid
        member this.DateTime = this.DateTime

type Vertex(name: string) =
    inherit Unique(name, Guid.NewGuid(), DateTime.Now)

type Call(name: string, parent: Work) =
    inherit Vertex(name)
    member val Param: CallParam = defaultCallParam with get, set
    member val Work = parent with get, set
    member val ApiCalls = ResizeArray<ApiCall>() with get, set

and Work(name: string, parent: System, flow:Flow) =
    inherit Vertex(name)
    member val Param: WorkParam = defaultWorkParam with get, set
    [<JsonIgnore>]
    member val System = parent with get, set
    member val Flow = flow with get, set
    member val Calls = ResizeArray<Call>() with get, set
    member val CallGraph = ResizeArray<(Call * Call)>() with get, set

and ApiDef(name: string, parent: System) =
    inherit Unique(name, Guid.NewGuid(), DateTime.Now, parent = Some parent)
    member val System = parent with get, set

and ApiCall(name: string, parent: Call, target: ApiDef) =
    inherit Unique(name, Guid.NewGuid(), DateTime.Now, parent = Some parent)
    member val Call = parent with get, set
    member val TargetApiDef = target with get, set
    member val Param: ApiCallParam = defaultApiCallParam with get, set

and ApiCallUsage(name: string, parent: Call, apiCall: ApiCall) =
    inherit Unique("ApiCallUsage", Guid.NewGuid(), DateTime.Now, parent = Some parent)
    member val Parent = parent with get, set
    member val ApiCall = apiCall with get, set

and Flow(name: string, parent: System) =
    inherit Unique(name, Guid.NewGuid(), DateTime.Now, parent = Some parent)
    member val Param: FlowParam = defaultFlowParam with get, set
    member val System = parent with get, set

and System(name: string, parent: Project) =
    inherit Unique(name, Guid.NewGuid(), DateTime.Now, parent = Some parent)
    member val Works = ResizeArray<Work>() with get, set
    member val Flows = ResizeArray<Flow>() with get, set
    member val ApiDefs = ResizeArray<ApiDef>() with get, set
    member val WorkGraph = ResizeArray<IArrow>() with get, set
    member val WorkArrows = ResizeArray<(Work * Work)>() with get, set
    member val Project = parent with get, set

and Project(name: string) =
    inherit Unique(name, Guid.NewGuid(), DateTime.Now)
    member val Systems = ResizeArray<System>() with get, set
    member val TargetSystemIds = ResizeArray<string>() with get, set
    member val SystemUsages = ResizeArray<ProjectSystemUsage>() with get, set

and ProjectSystemUsage(project: Project, system: System, active: bool) =
    inherit Unique("ProjectSystemUsage", Guid.NewGuid(), DateTime.Now, parent = Some project)
    member val Project = project with get, set
    member val TargetSystem = system with get, set
    member val Active = active with get, set