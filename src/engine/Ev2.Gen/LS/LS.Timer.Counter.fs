namespace Ev2.Gen

[<AutoOpen>]
module TimerCounterModule =
    type TimerType = Undefined | TON | TOF | TMR        // AB 에서 TMR 은 RTO 에 해당

    [<AbstractClass>]
    type TimerCounterStruct (isTimer:bool, name, en:IExpression<bool>, dn:IVariable<bool>, pre:IVariable<bool>, acc:IVariable<bool>, res:IExpression<bool>, sys) =
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
    type TimerStruct internal(typ:TimerType, name, en:IExpression<bool>, tt:IVariable<bool>, dn, pre, acc, res, sys) =
        inherit TimerCounterStruct(true, name, en, dn, pre, acc, res, sys)
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

    type CountUnitType = uint32
    type CounterType =
        /// UP Counter
        CTU
        /// DOWN Counter
        | CTD
        /// UP/DOWN Counter
        | CTUD
        /// Ring Counter
        | CTR

    type CounterParams = {
        Type: CounterType
        //Storage:Storage
        Name:string
        Preset: CountUnitType
        Accumulator: CountUnitType
        CU: IVariable<bool>
        CD: IVariable<bool>
        OV: IVariable<bool>
        UN: IVariable<bool>
        DN: IVariable<bool>
        /// XGI load
        LD: IVariable<bool>
        DNDown: IVariable<bool>

        RES: IVariable<bool>
        PRE: IVariable<CountUnitType>
        ACC: IVariable<CountUnitType>
    }
    type CounterStruct internal(typ:TimerType, name, en:IExpression<bool>, tt:IVariable<bool>, dn, pre, acc, res, sys) =
        inherit TimerCounterStruct(true, name, en, dn, pre, acc, res, sys)
        /// Timing
        member _.TT = tt
        member _.Type = typ


    type CounterCall(timerType:TimerType, rungIn: IExpression<bool>, reset:IExpression<bool>, preset:IVariable<bool>) =
        let ts = CounterStruct(timerType, nullString, rungIn, null, null, preset, null, reset, null)
        interface IFBCall
        new() = CounterCall(TimerType.Undefined, null, null, null)        // for serialization
        member val TimerType = timerType with get, set
        member val RungIn = rungIn with get, set
        member val Preset = preset with get, set
        member val Reset = reset with get, set


