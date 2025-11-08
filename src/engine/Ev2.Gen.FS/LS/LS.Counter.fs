namespace Ev2.Gen

open System

[<AutoOpen>]
module CounterModule =

    type CounterType =
        | Undefined
        | CTU
        | CTD
        | CTUD
        | CTR

    [<AllowNullLiteral>]
    type ICounterCall =
        inherit IFBCall
        abstract CounterType : CounterType
        abstract Name : string
        abstract ACC : IVariable
        abstract PRE : IVariable
        abstract DN : IVariable<bool>
        abstract DNDown : IVariable<bool>
        abstract OV : IVariable<bool>
        abstract UN : IVariable<bool>
        abstract CU : IVariable<bool>
        abstract CD : IVariable<bool>
        abstract LD : IVariable<bool>
        abstract RES : IVariable<bool>
        abstract Evaluate : unit -> unit
        abstract Reset : unit -> unit

    [<AllowNullLiteral>]
    type ICounterCall<'T when 'T : struct and 'T :> IConvertible and 'T : comparison> =
        inherit ICounterCall
        abstract TypedACC : IVariable<'T>
        abstract TypedPRE : IVariable<'T>

    type CounterStruct<'T when 'T : struct and 'T :> IConvertible and 'T : comparison>
        internal (counterType:CounterType, name:string, preset:'T) =

        let toUInt (value:'T) = Convert.ToUInt32 value
        let ofUInt (value:uint32) : 'T = Convert.ChangeType(value, typeof<'T>) :?> 'T

        let dn = Variable<bool>($"{name}.DN", Value=false)
        let dnDown = Variable<bool>($"{name}.DNDown", Value=false)
        let ov = Variable<bool>($"{name}.OV", Value=false)
        let un = Variable<bool>($"{name}.UN", Value=false)
        let cu = Variable<bool>($"{name}.CU", Value=false)
        let cd = Variable<bool>($"{name}.CD", Value=false)
        let ld = Variable<bool>($"{name}.LD", Value=false)
        let res = Variable<bool>($"{name}.R", Value=false)
        let pre = Variable<'T>($"{name}.PRE", Value=preset)
        let initialAcc =
            match counterType with
            | CTD
            | CTR -> toUInt preset
            | _ -> 0u
        let acc = Variable<'T>($"{name}.ACC", Value = ofUInt initialAcc)

        let mutable accumulator = initialAcc
        let mutable doneUp = false
        let mutable doneDown = false
        let mutable overflowFlag = false
        let mutable underflowFlag = false
        let mutable prevCountUp = false
        let mutable prevCountDown = false

        let rec updateFlags () =
            let presetVal = toUInt pre.Value
            match counterType with
            | CTU ->
                doneUp <- (presetVal = 0u && accumulator > 0u) || (presetVal <> 0u && accumulator >= presetVal)
                doneDown <- false
                overflowFlag <- presetVal <> 0u && accumulator > presetVal
                underflowFlag <- false
            | CTD ->
                doneUp <- accumulator = 0u
                doneDown <- doneUp
                overflowFlag <- false
            | CTUD ->
                doneUp <- presetVal <> 0u && accumulator >= presetVal
                doneDown <- accumulator = 0u
                overflowFlag <- presetVal <> 0u && accumulator > presetVal
            | CTR
            | Undefined ->
                doneUp <- false
                doneDown <- false
                overflowFlag <- false
            dn.Value <- doneUp
            dnDown.Value <- doneDown
            ov.Value <- overflowFlag
            un.Value <- underflowFlag
            acc.Value <- ofUInt accumulator

        let risingEdge prev current = current && not prev

        let resetAccumulator () =
            let presetVal = toUInt pre.Value
            accumulator <- match counterType with | CTD | CTR -> presetVal | _ -> 0u
            overflowFlag <- false
            underflowFlag <- false
            doneDown <- false
            prevCountUp <- false
            prevCountDown <- false
            cu.Value <- false
            cd.Value <- false
            ld.Value <- false
            res.Value <- false
            updateFlags()

        do resetAccumulator()

        member _.Type = counterType
        member _.Name = name
        member _.ACC : IVariable<'T> = acc
        member _.PRE : IVariable<'T> = pre
        member _.DN : IVariable<bool> = dn
        member _.DNDown : IVariable<bool> = dnDown
        member _.OV : IVariable<bool> = ov
        member _.UN : IVariable<bool> = un
        member _.CU : IVariable<bool> = cu
        member _.CD : IVariable<bool> = cd
        member _.LD : IVariable<bool> = ld
        member _.RES : IVariable<bool> = res
        member _.AccumulatorValue = acc.Value

        member _.Evaluate() =
            let countUpSignal = cu.Value
            let countDownSignal = cd.Value
            let resetSignal = res.Value
            let loadSignal = ld.Value
            let requestedLoadValue = toUInt pre.Value

            if resetSignal then
                resetAccumulator()
            elif loadSignal then
                accumulator <- requestedLoadValue
                overflowFlag <- false
                underflowFlag <- false
                doneDown <- false
                updateFlags()
            else
                match counterType with
                | CTU when risingEdge prevCountUp countUpSignal ->
                    accumulator <- accumulator + 1u
                    updateFlags()
                | CTD when risingEdge prevCountDown countDownSignal ->
                    if accumulator = 0u then
                        underflowFlag <- true
                    else
                        accumulator <- accumulator - 1u
                    updateFlags()
                | CTUD ->
                    let mutable changed = false
                    if risingEdge prevCountUp countUpSignal then
                        accumulator <- accumulator + 1u
                        changed <- true
                    if risingEdge prevCountDown countDownSignal then
                        if accumulator = 0u then
                            underflowFlag <- true
                        else
                            accumulator <- accumulator - 1u
                        changed <- true
                    if changed then updateFlags()
                | CTR -> ()
                | _ -> ()

            prevCountUp <- countUpSignal
            prevCountDown <- countDownSignal

        member _.Reset() = resetAccumulator()


    type CounterCall<'T when 'T : struct and 'T :> IConvertible and 'T : comparison>
        (counterType:CounterType, name:string, preset:'T) =
        let cs = CounterStruct(counterType, name, preset)
        interface IFBCall
        interface ICounterCall with
            member _.CounterType = counterType
            member _.Name = cs.Name
            member _.ACC = cs.ACC :> IVariable
            member _.PRE = cs.PRE :> IVariable
            member _.DN = cs.DN
            member _.DNDown = cs.DNDown
            member _.OV = cs.OV
            member _.UN = cs.UN
            member _.CU = cs.CU
            member _.CD = cs.CD
            member _.LD = cs.LD
            member _.RES = cs.RES
            member _.Evaluate() = cs.Evaluate()
            member _.Reset() = cs.Reset()
        interface ICounterCall<'T> with
            member _.TypedACC = cs.ACC
            member _.TypedPRE = cs.PRE
        member _.CounterStruct = cs
        member _.Type = counterType
        member _.Name   = cs.Name
        member _.ACC    = cs.ACC
        member _.PRE    = cs.PRE
        member _.DN     = cs.DN
        member _.DNDown = cs.DNDown
        member _.OV     = cs.OV
        member _.UN     = cs.UN
        member _.CU     = cs.CU
        member _.CD     = cs.CD
        member _.LD     = cs.LD
        member _.RES    = cs.RES
        member _.AccumulatorValue = cs.AccumulatorValue
        member _.Evaluate() = cs.Evaluate()
        member _.Reset() = cs.Reset()

    let inline private createCounter<'T when 'T : struct and 'T :> IConvertible and 'T : comparison>
        counterType name preset = CounterCall<'T>(counterType, name, preset)

    let inline createCTU<'T when 'T : struct and 'T :> IConvertible and 'T : comparison> name preset =
        createCounter<'T> CTU name preset

    let inline createCTD<'T when 'T : struct and 'T :> IConvertible and 'T : comparison> name preset =
        createCounter<'T> CTD name preset

    let inline createCTUD<'T when 'T : struct and 'T :> IConvertible and 'T : comparison> name preset =
        createCounter<'T> CTUD name preset
