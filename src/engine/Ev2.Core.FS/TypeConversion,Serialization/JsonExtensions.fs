namespace Ev2.Core.FS

open System.Runtime.CompilerServices
open Ev2.Core.FS
open Ev2.Core.FS.NewtonsoftJsonObjects

///// C#에서 사용 가능한 Extension Methods
//[<Extension>]
//type JsonExtensions() =

//    /// Project를 NjProject로 변환하는 extension method
//    [<Extension>]
//    static member ToNjObj(project: Project) : NjProject =
//        NjProject.fromRuntime(project)

//    /// DsSystem을 NjSystem으로 변환하는 extension method
//    [<Extension>]
//    static member ToNjObj(system: DsSystem) : NjSystem =
//        NjSystem.fromRuntime(system)

//    /// Flow를 NjFlow로 변환하는 extension method
//    [<Extension>]
//    static member ToNjObj(flow: Flow) : NjFlow =
//        NjFlow.fromRuntime(flow)

//    /// Work를 NjWork로 변환하는 extension method
//    [<Extension>]
//    static member ToNjObj(work: Work) : NjWork =
//        NjWork.fromRuntime(work)

//    /// Call을 NjCall로 변환하는 extension method
//    [<Extension>]
//    static member ToNjObj(call: Call) : NjCall =
//        NjCall.fromRuntime(call)