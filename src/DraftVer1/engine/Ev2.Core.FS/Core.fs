namespace rec Dual.Ev2

open Newtonsoft.Json

open Dual.Common.Base
open Dual.Common.Core.FS
open System.Runtime.CompilerServices
open System


[<AutoOpen>]
module Core =
    /// DS system
    type DsSystem(name:string) =
        inherit DsItemWithGraph(name)
        interface ISystem
        [<JsonProperty(Order = 2)>] member val Flows = ResizeArray<DsFlow>() with get, set

        [<JsonProperty(Order = 3)>] member val Works = ResizeArray<DsWork>() with get, set

    /// DS flow
    type DsFlow(system:DsSystem, name:string) =
        inherit DsItem(name)
        interface IFlow

        [<JsonIgnore>] member val System = system with get, set

    /// DS work
    type DsWork(system:DsSystem, flow:DsFlow, name:string) =
        inherit DsItemWithGraph(name, container=system)
        interface IWork
        //new(name) = DsWork(getNull<DsFlow>(), name)
        //new() = DsWork(getNull<DsFlow>(), null)

        member val Flow = flow with get, set
        member val Actions = ResizeArray<DsAction>() with get, set


    /// DS coin.  base class for Ds{Action, AutoPre, Safety, Command, Operator}
    [<AbstractClass>]
    type DsCoin(name:string, ?work:DsWork) =
        inherit DsItem(name, ?container=work.Cast<DsItem>())

    /// DS action.  외부 device 호출
    type DsAction(name:string, ?work:DsWork) =
        inherit DsCoin(name, ?work=work)
        new(name) = DsAction(name, getNull<DsWork>())   // for C#
        new() = DsAction(null, getNull<DsWork>())   // for JSON
        member val IsDisabled = false with get, set
        member val IsPush = false with get, set
        [<JsonIgnore>] member x.Work = x.Container :?> DsWork

    /// DS auto-pre.  자동 운전시에만 참조하는 조건
    type DsAutoPre(name:string) =
        inherit DsCoin(name)

    /// DS safety.  안전 인과 조건
    type DsSafety(name:string, safeties:string []) =
        inherit DsCoin(name)
        new(name) = DsSafety(name, [||])
        member val Safeties = safeties with get, set

    /// DS command
    type DsCommand(name:string) =
        inherit DsCoin(name)

    /// DS operator
    type DsOperator(name:string) =
        inherit DsCoin(name)







