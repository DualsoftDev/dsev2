namespace Dual.Ev2

open Dual.Common.Base.FS
open Dual.Common.Core.FS
open Newtonsoft.Json
open System
open System.Runtime.InteropServices
open System.Reactive.Subjects



(*
 - Timer 설정을 위한 조건: expression 으로 받음.
 - Timer statement 는 expression 을 매 scan 마다 평가.  값이 변경되면(rising or falling) 해당 timer 에 반영
 - Timer 가 설정되고 나면, observable timer 에 의해서 counter 값이 하나씩 감소하고, 0 이 되면 target trigger
*)

module TimerModuleApi =

    // DllImport 바인딩을 정적 멤버로 정의합니다.
    [<DllImport("winmm.dll", SetLastError = true)>]
    extern uint timeBeginPeriod(uint uPeriod)

    [<DllImport("winmm.dll", SetLastError = true)>]
    extern uint timeEndPeriod(uint uPeriod)

[<AutoOpen>]
module DsType =     // from DsType.fs

    /// Describes the segment status with default being 'Homing'
    type Status4 =
        | Ready
        | Going
        | Finish
        | Homing


[<AutoOpen>]
module CpusEvent =  // from module CpusEvent @ DsEvent.fs
    // Represents the status parameters for a Vertex.
    type VertexStatusParam =
        | EventCPU of sys: ISystem * vertex: IVertex * status: Status4


[<AutoOpen>]
module DsRuntimeEnvironmentModule =

    type IValue with
        //[<Obsolete("Value.DsSystem 제거 OK??")>] : Subsystem 코드 생성시 필요?
        member x.DsSystem =
            (x:?>ValueHolder).DsSystem
            |> tee (fun s -> if isItNull s then failwith "ERROR")

    type EventBag() =
        member val StatusSubject = new Subject<VertexStatusParam>()
        interface IEventBag
        static member Create() = EventBag()

    type DsRuntimeEnvironment with
        member x.ValueBag = x.IValueBag :?> ValueBag
        member x.EventBag = x.IEventBag :?> EventBag
        member x.OptModelConfig = x.IOptModelConfig.Cast<ModelConfig>()
        member x.ValueChangedSubject = x.ValueBag.ValueChangedSubject
        member x.StatusChangedSubject = x.EventBag.StatusSubject
