namespace Ev2.Gen

open System
open System.Collections.Generic

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

        let inputDict = Dictionary<string, IExpression>(StringComparer.OrdinalIgnoreCase)
        let inputs : InputMapping = upcast inputDict
        let outputDict = Dictionary<string, IVariable>(StringComparer.OrdinalIgnoreCase)
        let outputs : OutputMapping = upcast outputDict

        let initInput key (expr:IExpression) = inputDict[key] <- expr
        let initOutput key (variable:IVariable) = outputDict[key] <- variable

        do
            initInput "CU" (cu :> IExpression)
            initInput "CD" (cd :> IExpression)
            initInput "LD" (ld :> IExpression)
            initInput "RES" (res :> IExpression)
            initInput "PRE" (pre :> IExpression)
            initInput "ACC" (acc :> IExpression)
            initOutput "ACC" (acc :> IVariable)
            initOutput "DN" (dn :> IVariable)
            initOutput "DNDown" (dnDown :> IVariable)
            initOutput "OV" (ov :> IVariable)
            initOutput "UN" (un :> IVariable)
            match inputMapping with
            | Some overrides when not (isNull (overrides :> obj)) ->
                for kvp in overrides do
                    inputDict[kvp.Key] <- kvp.Value
            | _ -> ()
            match outputMapping with
            | Some overrides when not (isNull (overrides :> obj)) ->
                for kvp in overrides do
                    outputDict[kvp.Key] <- kvp.Value
            | _ -> ()

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

        let tryGetBoolInput key =
            match inputDict.TryGetValue key with
            | true, expr when not (isNull expr) ->
                match expr with
                | :? IExpression<bool> as typed -> Some typed.TValue
                | _ ->
                    let raw = expr.Value
                    if isNull raw then None else Some(Convert.ToBoolean raw)
            | _ -> None

        let tryGetTypedInput key =
            match inputDict.TryGetValue key with
            | true, expr when not (isNull expr) ->
                match expr with
                | :? IExpression<'T> as typed -> Some typed.TValue
                | _ ->
                    let raw = expr.Value
                    if isNull raw then None else Some(Convert.ChangeType(raw, typeof<'T>) :?> 'T)
            | _ -> None

        let propagateBoolOutput key (defaultVar:IVariable<bool>) (value:bool) =
            let boxed = box value
            defaultVar.Value <- boxed
            match outputDict.TryGetValue key with
            | true, (:? IVariable<bool> as mapped) when not (obj.ReferenceEquals(mapped, defaultVar)) ->
                mapped.Value <- boxed
            | _ -> ()

        let propagateTypedOutput key (defaultVar:IVariable<'T>) (value:'T) =
            let boxed = box value
            defaultVar.Value <- boxed
            match outputDict.TryGetValue key with
            | true, (:? IVariable<'T> as mapped) when not (obj.ReferenceEquals(mapped, defaultVar)) ->
                mapped.Value <- boxed
            | _ -> ()

        let resolveBoolInput key (fallbackVar:IVariable<bool>) =
            let value =
                match tryGetBoolInput key with
                | Some v -> v
                | None -> fallbackVar.TValue
            fallbackVar.Value <- box value
            value

        let resolveTypedInput key (fallbackVar:IVariable<'T>) =
            let value =
                match tryGetTypedInput key with
                | Some v -> v
                | None -> fallbackVar.TValue
            fallbackVar.Value <- box value
            value

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
            propagateBoolOutput "DN" dn doneUp
            propagateBoolOutput "DNDown" dnDown doneDown
            propagateBoolOutput "OV" ov overflowFlag
            propagateBoolOutput "UN" un underflowFlag
            propagateTypedOutput "ACC" acc (ofUInt accumulator)

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
            let countUpSignal = resolveBoolInput "CU" cu
            let countDownSignal = resolveBoolInput "CD" cd
            let resetSignal = resolveBoolInput "RES" res
            let loadSignal = resolveBoolInput "LD" ld
            let requestedPreset = resolveTypedInput "PRE" pre
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
