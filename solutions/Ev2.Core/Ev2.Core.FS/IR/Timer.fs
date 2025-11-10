namespace Ev2.Gen

open System
open Ev2.Core.FS.IR

[<AutoOpen>]
module TimerModule =
    type TimerType = Undefined | TON | TOF | TMR        // AB 에서 TMR 은 RTO 에 해당

    type TimerStruct internal(
        typ:TimerType, name
        , en:IExpression<bool>, tt:IVariable<bool>
        , dn:IVariable<bool>, pre:IVariable, acc:IVariable, res:IExpression<bool>, sys
    ) =
        member _.Name:string = name
        /// Enable
        member _.EN = en
        /// Done bit
        member _.DN = dn
        /// Preset value
        member _.PRE = pre
        member _.ACC = acc
        /// Reset bit.
        member _.RES = res
        /// XGI load
        member _.LD = res
        /// Timing
        member _.TT = tt
        member _.Type = typ


    type TimerCall(timerType:TimerType, rungIn: IExpression<bool>, reset:IExpression<bool>, preset:IVariable<bool>) =
        let ts = TimerStruct(timerType, nullString, rungIn, null, null, preset, null, reset, null)
        interface IFBCall
        new() = TimerCall(TimerType.Undefined, null, null, null)        // for serialization
        member val TimerType = timerType with get, set
        member val RungIn = rungIn with get, set
        member val Preset = preset with get, set
        member val Reset = reset with get, set

