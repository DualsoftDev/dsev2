namespace Ev2.Gen

open System
[<AutoOpen>]
module CounterModule =

    type CountUnitType = uint32
    type CounterType =
        Undefined
        /// UP Counter
        | CTU
        /// DOWN Counter
        | CTD
        /// UP/DOWN Counter
        | CTUD
        /// Ring Counter
        | CTR


    type CounterStruct internal(counterType:CounterType, name:string, preset:CountUnitType) =
        let dn = Variable<bool>($"{name}.DN", Value=false)
        let dnDown = Variable<bool>($"{name}.DNDown", Value=false)
        let ov = Variable<bool>($"{name}.OV", Value=false)
        let un = Variable<bool>($"{name}.UN", Value=false)
        let cu = Variable<bool>($"{name}.CU", Value=false)
        let cd = Variable<bool>($"{name}.CD", Value=false)
        let ld = Variable<bool>($"{name}.LD", Value=false)      // XGI load
        let res = Variable<bool>($"{name}.R", Value=false)
        let pre = Variable<CountUnitType>($"{name}.PRE", Value=preset)
        let initialAcc = match counterType with | CTD | CTR -> preset | _ -> 0u
        let acc = Variable<CountUnitType>($"{name}.ACC", Value=initialAcc)

        let mutable accumulator = initialAcc
        let mutable doneUp = false
        let mutable doneDown = false
        let mutable overflowFlag = false
        let mutable underflowFlag = false
        let mutable prevCountUp = false
        let mutable prevCountDown = false

        let risingEdge prev current = current && not prev

        let rec updateFlags () =
            let presetVal = pre.Value
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
            acc.Value <- accumulator

        let resetAccumulator () =
            accumulator <- match counterType with | CTD | CTR -> pre.Value | _ -> 0u
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
        member _.ACC : IVariable<CountUnitType> = acc
        member _.PRE : IVariable<CountUnitType> = pre
        member _.DN : IVariable<bool> = dn
        member _.DNDown : IVariable<bool> = dnDown
        member _.OV : IVariable<bool> = ov
        member _.UN : IVariable<bool> = un
        member _.CU : IVariable<bool> = cu
        member _.CD : IVariable<bool> = cd
        member _.LD : IVariable<bool> = ld
        member _.RES : IVariable<bool> = res
        member _.AccumulatorValue = accumulator

        member _.Evaluate() =
            let countUpSignal = cu.Value
            let countDownSignal = cd.Value
            let resetSignal = res.Value
            let loadSignal = ld.Value
            let requestedLoadValue = pre.Value

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


    type CounterCall(counterType:CounterType, name:string, preset:CountUnitType) =
        let cs = CounterStruct(counterType, name, preset)
        interface IFBCall
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

    let private createCounter counterType name preset = CounterCall(counterType, name, preset)
    let createCTU name preset = createCounter CTU name preset
    let createCTD name preset = createCounter CTD name preset
    let createCTUD name preset = createCounter CTUD name preset
