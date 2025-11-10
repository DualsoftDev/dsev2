namespace Ev2.Gen

open System
open System.Collections.Generic
open Dual.Common.Base
open Ev2.Core.FS.IR

[<AutoOpen>]
module CounterModule =

    type CounterType =
        | Undefined
        | CTU
        | CTD
        | CTUD
        | CTR

    [<AllowNullLiteral>] type ICounterInstance = inherit IFBCall
    [<AllowNullLiteral>]
    type ICounterInstance<'T when 'T : struct and 'T :> IConvertible and 'T : comparison> =
        inherit ICounterInstance

    type CounterStruct<'T when 'T : struct and 'T :> IConvertible and 'T : comparison>
        internal (counterType:CounterType, name:string, preset:'T, globalStorage:Storage, ?inputMapping:InputMapping, ?outputMapping:OutputMapping) =
        do if isNull (globalStorage :> obj) then invalidArg "globalStorage" "Storage is null"

        let toUInt (value:'T) = Convert.ToUInt32 value
        let ofUInt (value:uint32) : 'T = Convert.ChangeType(value, typeof<'T>) :?> 'T

        let dn     = Variable<bool>("DN",     Value=false)
        let dnDown = Variable<bool>("DNDown", Value=false)
        let ov     = Variable<bool>("OV",     Value=false)
        let un     = Variable<bool>("UN",     Value=false)
        let cu     = Variable<bool>("CU",     Value=false)
        let cd     = Variable<bool>("CD",     Value=false)
        let ld     = Variable<bool>("LD",     Value=false)
        let res    = Variable<bool>("RES",    Value=false)
        let pre    = Variable<'T>  ("PRE",    Value=preset)
        let initialAcc =
            match counterType with
            | CTD
            | CTR -> toUInt preset
            | _ -> 0u
        let acc = Variable<'T>("ACC", Value = ofUInt initialAcc)

        let inputs = Dictionary<string, IExpression>(StringComparer.OrdinalIgnoreCase)
        let outputs = Dictionary<string, IVariable>(StringComparer.OrdinalIgnoreCase)

        do
            inputs["CU"]    <-  cu
            inputs["CD"]    <-  cd
            inputs["LD"]    <-  ld
            inputs["RES"]   <- res
            inputs["PRE"]   <- pre
            inputs["ACC"]   <- acc
            outputs["ACC"]  <- acc
            outputs["DN"]   <- dn
            outputs["DNDown"] <- dnDown
            outputs["OV"]   <- ov
            outputs["UN"]   <- un

            inputMapping |> iter (fun dic ->
                for kvp in dic do
                    inputs[kvp.Key] <- kvp.Value )

            outputMapping |> iter (fun dic ->
                for kvp in dic do
                    outputs[kvp.Key] <- kvp.Value )


        let exposedVariables : IVariable[] = [| acc; pre; dn; dnDown; ov; un; cu; cd; ld; res |]
        let containerStruct = Struct(name, exposedVariables, VarType=VarType.VarGlobal)

        do
            globalStorage.Add(name, containerStruct)

        let mutable accumulator = initialAcc
        let mutable doneUp = false
        let mutable doneDown = false
        let mutable overflowFlag = false
        let mutable underflowFlag = false
        let mutable prevCountUp = false
        let mutable prevCountDown = false

        let resolveInput key (fallbackVar:IVariable<'U>) =
            let value =
                match inputs.TryGetValue key with
                | true, expr when not (isNull expr) ->
                    match expr with
                    | :? IExpression<'U> as typed -> typed.TValue
                    | _ ->
                        let raw = expr.Value
                        if isNull raw then fallbackVar.TValue else Convert.ChangeType(raw, typeof<'U>) :?> 'U
                | _ -> fallbackVar.TValue
            fallbackVar.Value <- box value
            value

        let propagateOutput key (defaultVar:IVariable<'U>) (value:'U) =
            let boxed = box value
            defaultVar.Value <- boxed
            match outputs.TryGetValue key with
            | true, (:? IVariable<'U> as mapped) when not (obj.ReferenceEquals(mapped, defaultVar)) ->
                mapped.Value <- boxed
            | _ -> ()

        let applyFlags doneUpVal doneDownVal overflowVal underflowUpdate =
            doneUp <- doneUpVal
            doneDown <- doneDownVal
            overflowFlag <- overflowVal
            underflowUpdate |> Option.iter (fun v -> underflowFlag <- v)

        let rec updateFlags () =
            let presetVal = toUInt pre.Value
            match counterType with
            | CTU ->
                let doneUpVal = (presetVal = 0u && accumulator > 0u) || (presetVal <> 0u && accumulator >= presetVal)
                let overflowVal = presetVal <> 0u && accumulator > presetVal
                applyFlags doneUpVal false overflowVal (Some false)
            | CTD ->
                let doneUpVal = accumulator = 0u
                applyFlags doneUpVal doneUpVal false None
            | CTUD ->
                let doneUpVal = presetVal <> 0u && accumulator >= presetVal
                let doneDownVal = accumulator = 0u
                let overflowVal = presetVal <> 0u && accumulator > presetVal
                applyFlags doneUpVal doneDownVal overflowVal None
            | CTR
            | Undefined ->
                applyFlags false false false None
            propagateOutput "DN" dn doneUp
            propagateOutput "DNDown" dnDown doneDown
            propagateOutput "OV" ov overflowFlag
            propagateOutput "UN" un underflowFlag
            propagateOutput "ACC" acc (ofUInt accumulator)

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
        member _.InternalVariables : IVariable[] = exposedVariables
        member _.Container = containerStruct

        member _.Evaluate() =
            let countUpSignal = resolveInput "CU" cu
            let countDownSignal = resolveInput "CD" cd
            let resetSignal = resolveInput "RES" res
            let loadSignal = resolveInput "LD" ld
            let requestedPreset = resolveInput "PRE" pre
            let requestedLoadValue = toUInt requestedPreset

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
        member _.Inputs = inputs
        member _.Outputs = outputs


    type CounterInstance<'T when 'T : struct and 'T :> IConvertible and 'T : comparison> (
        counterType:CounterType, name:string, preset:'T, globalStorage:Storage
        , ?inputMapping:InputMapping, ?outputMapping:OutputMapping
    ) =
        let cs = CounterStruct(counterType, name, preset, globalStorage, ?inputMapping=inputMapping, ?outputMapping=outputMapping)
        interface IFBCall
        interface ICounterInstance<'T>
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
        member _.Inputs = cs.Inputs
        member _.Outputs = cs.Outputs
        member _.InternalVariables = cs.InternalVariables
        member _.Container = cs.Container

    let inline private createCounter<'T when 'T : struct and 'T :> IConvertible and 'T : comparison>
        counterType name preset globalStorage = CounterInstance<'T>(counterType, name, preset, globalStorage)

    let inline createCTU<'T when 'T : struct and 'T :> IConvertible and 'T : comparison> name preset globalStorage =
        createCounter<'T> CTU name preset globalStorage

    let inline createCTD<'T when 'T : struct and 'T :> IConvertible and 'T : comparison> name preset globalStorage =
        createCounter<'T> CTD name preset globalStorage

    let inline createCTUD<'T when 'T : struct and 'T :> IConvertible and 'T : comparison> name preset globalStorage =
        createCounter<'T> CTUD name preset globalStorage
