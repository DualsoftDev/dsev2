namespace Dual.Ev2

open System
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Threading
open System.Reactive.Subjects
open System.Reactive.Linq
open System.Reactive.Disposables

open Dual.Common.Core.FS
open Dual.Common.Base


[<AutoOpen>]
module rec TimerModule =
    type TimerType = TON | TOF | TMR        // AB 에서 TMR 은 RTO 에 해당
    /// Timer / Counter 의 number data type
    type CountUnitType = uint32


    let xgkTimerCounterContactMarking = "$ON"
    let [<Literal>] MinTickInterval = 10u    //<ms>
    let [<Literal>] TimerResolution  = 1u    //<ms> windows timer resolution (1~ 1000000)

    type Storages() =
        inherit Dictionary<string, ValueHolder>(StringComparer.OrdinalIgnoreCase)
        new (existing:Storages) =
            Storages() then        // constructor chaning
            for kv in existing do
                base.Add(kv.Key, kv.Value)

    type internal TickAccumulator(timerType:TimerType, timerStruct:TimerStruct, dsRte:DsRuntimeEnvironment) =
        let ts = timerStruct
        let tt = timerType

        let accumulateTON() =
            if ts.TT.TValue && not ts.DN.TValue && ts.ACC.TValue < ts.PRE.TValue then
                ts.ACC.TValue <- ts.ACC.TValue + MinTickInterval
                if ts.ACC.TValue >= ts.PRE.TValue then
                    debugfn "Timer accumulator value reached"
                    ts.TT.TValue <- false
                    ts.DN.TValue <- true
                    ts.EN.TValue <- true

        let accumulateTOF() =
            if ts.TT.TValue && ts.DN.TValue && not ts.EN.TValue && ts.ACC.TValue < ts.PRE.TValue then
                ts.ACC.TValue <- ts.ACC.TValue + MinTickInterval
                if ts.ACC.TValue >= ts.PRE.TValue then
                    debugfn "Timer accumulator value reached"
                    ts.TT.TValue <- false
                    ts.DN.TValue <- false

        let accumulateRTO() =
            if ts.TT.TValue && not ts.DN.TValue && ts.EN.TValue && ts.ACC.TValue < ts.PRE.TValue then
                ts.ACC.TValue <- ts.ACC.TValue + MinTickInterval
                if ts.ACC.TValue >= ts.PRE.TValue then
                    debugfn "Timer accumulator value reached"
                    ts.TT.TValue <- false
                    ts.EN.TValue <- false
                    ts.DN.TValue <- true

        let accumulate() =
            //debugfn "Accumulating from %A" ts.ACC.Value
            match tt with
            | TON -> accumulateTON()
            | TOF -> accumulateTOF()
            | TMR -> accumulateRTO()

        let timerCallback (_: obj) = accumulate()

        let disposables = new CompositeDisposable()

        do
            ts.ResetStruct()

            debugfn "Timer subscribing to tick event"
            //theMinTickTimer.Subscribe(fun _ -> accumulate()) |> disposables.Add

            TimerModuleApi.timeBeginPeriod(TimerResolution) |> ignore
            new System.Threading.Timer(TimerCallback(timerCallback), null, TimeSpan.Zero, TimeSpan.FromMilliseconds(float MinTickInterval)) |> disposables.Add

            dsRte.ValueChangedSubject
                .Where(fun (storage, _value) -> storage.DsSystem = timerStruct.DsSystem)
                .Where(fun (storage, _newValue) -> storage = timerStruct.EN)
                .Subscribe(fun (_storage, newValue) ->
                    if ts.ACC.TValue < 0u || ts.PRE.TValue < 0u then failwithlog "ERROR"
                    let rungInCondition = newValue :?> bool
                    //debugfn "%A rung-condition-in=%b with DN=%b" tt rungInCondition ts.DN.Value
                    match tt, rungInCondition with
                    | TON, true ->
                        ts.TT.TValue <- not ts.DN.TValue
                    | TON, false -> ts.ResetStruct()

                    | TOF, false ->
                        ts.EN.TValue <- false
                        ts.TT.TValue <- true
                        if not ts.DN.TValue then
                            ts.ClearBits()

                    | TOF, true ->
                        ts.EN.TValue <- true
                        ts.TT.TValue <- false     // spec 상충함 : // https://edisciplinas.usp.br/pluginfile.php/184942/mod_resource/content/1/Logix5000%20-%20Manual%20de%20Referencias.pdf 와 https://edisciplinas.usp.br/pluginfile.php/184942/mod_resource/content/1/Logix5000%20-%20Manual%20de%20Referencias.pdf 설명이 다름
                        ts.DN.TValue <- true
                        ts.ACC.TValue <- 0u

                    | TMR, true ->
                        if ts.DN.TValue then
                            ts.TT.TValue <- false
                        else
                            ts.EN.TValue <- true
                            ts.TT.TValue <- true
                    | TMR, false ->
                        ts.EN.TValue <- false
                        ts.TT.TValue <- false
                ) |> disposables.Add

            dsRte.ValueChangedSubject
                .Where(fun (storage, _value) -> storage.DsSystem = timerStruct.DsSystem)
                .Where(fun (storage, _newValue) -> storage = ts.RES)
                .Subscribe(fun (_storage, newValue) ->
                    let resetCondition = newValue :?> bool
                    if resetCondition then
                        ts.ACC.TValue <- 0u
                        ts.DN.TValue <- false
                ) |> disposables.Add

        interface IDisposable with
            member this.Dispose() =
                TimerModuleApi.timeEndPeriod(TimerResolution) |> ignore

                for d in disposables do
                    d.Dispose()
                disposables.Clear()


    let internal clearBool(b:TValue<bool>) =
        if b |> isItNull |> not then
            b.OValue <- false


    type internal T =
        static member SysVarTag = int VariableTag.PcSysVariable
        static member CreateMemberVariable<'T>(valueBag:ValueBag, system:ISystem, name:string, value:'T, ?tagKind:int) =
            let v = TValue<'T>(value, valueBag=valueBag, Name=name, IsMemberVariable=true, DsSystem=system)
            tagKind.Iter(fun tk -> v.TagKind <- tk)
            v


    [<AbstractClass>]
    type TimerCounterBaseStruct (isTimer:bool, name, dn, pre, acc, res, sys) =
        inherit TValue<bool>(isTimer, Comment="Timer/Counter base", DsSystem=sys)

        member private x.This = x
        member _.Name:string = name
        /// Done bit
        member _.DN:TValue<bool> = dn
        /// Preset value
        member _.PRE:TValue<CountUnitType> = pre
        member _.ACC:TValue<CountUnitType> = acc
        /// Reset bit.
        member _.RES:TValue<bool> = res
        /// XGI load
        member _.LD:TValue<bool> = res
        abstract member ResetStruct:unit -> unit
        default x.ResetStruct() =
            // -- preset 은 clear 대상이 아님: x.PRE,     reset 도 clear 해야 하는가? -- ???  x.RES
            clearBool x.DN
            clearBool x.LD

            if x.ACC |> isItNull |> not then
                x.ACC.OValue <- 0u
        /// XGK 에서 할당한 counter/timer 변수 이름 임시 저장 공간.  e.g "C0001"
        member x.XgkStructVariableName =
            let prefix = isTimer ?= ("T", "C")
            sprintf "%s%04d" prefix x.XgkStructVariableDevicePos

        member val XgkStructVariableDevicePos = -1 with get, set


    type (*internal*) TimerCreateParams = {
        DsRuntimeEnvironment: DsRuntimeEnvironment
        Type: TimerType
        Name: string
        Preset: CountUnitType
        RungConditionIn: IExpression<bool> option
        ResetCondition: IExpression<bool> option
        FunctionName:string
    }



    type TimerStruct private(typ:TimerType, name, en, tt, dn, pre, acc, res, sys) =
        inherit TimerCounterBaseStruct(true, name, dn, pre, acc, res, sys)

        /// Enable
        member _.EN:TValue<bool> = en
        /// Timing
        member _.TT:TValue<bool> = tt
        member _.Type = typ

        static member Create(tParam:TimerCreateParams, storages:Storages, accum:CountUnitType) =
            let {Type=typ; Name=name; Preset=preset; DsRuntimeEnvironment=dsRte}:TimerCreateParams = tParam
            let target = dsRte.PlatformTarget
            let valueBag = dsRte.ValueBag
            let sys = dsRte.ISystem

            let suffixes  =
                match target with
                | XGK -> [".IN"; ".TT"; xgkTimerCounterContactMarking; ".PT"; ".ET"; ".RST"] // XGK 이름에 . 있으면 걸러짐 storagesToXgxSymbol
                | XGI | WINDOWS -> [".IN"; "._TT"; ".Q"; ".PT"; ".ET"; ".RST"]
                | AB -> [".EN"; ".TT"; ".DN"; ".PRE"; ".ACC"; ".RES"]
                | _ -> failwith "NOT yet supported"

            let en, tt, dn, pre, acc, res =
                let names = suffixes |> Seq.map (fun suffix -> $"{name}{suffix}") |> Seq.toList
                match names with
                | [en; tt; dn; pre; acc; res] -> en, tt, dn, pre, acc, res
                | _ -> failwith "Unexpected number of suffixes"

            let en  = T.CreateMemberVariable<bool>  (valueBag, sys, $"{en }", false)
            let tt  = T.CreateMemberVariable<bool>  (valueBag, sys, $"{tt }", false)
            let dn  = T.CreateMemberVariable<bool>  (valueBag, sys, $"{dn }", false, T.SysVarTag)
            let pre = T.CreateMemberVariable<UInt32>(valueBag, sys, pre, preset)
            let acc = T.CreateMemberVariable<UInt32>(valueBag, sys, acc, accum)
            let res = T.CreateMemberVariable<bool>  (valueBag, sys, res, false)

            storages.Add(en.Name, en)
            storages.Add(tt.Name, tt)
            storages.Add(dn.Name, dn)
            storages.Add(pre.Name, pre)
            storages.Add(acc.Name, acc)
            storages.Add(res.Name, res)

            let ts = new TimerStruct(typ, name, en, tt, dn, pre, acc, res, sys)
            storages.Add(name, ts)
            ts

        /// Clear EN, TT, DN bits
        member x.ClearBits() =
            [x.EN; x.TT; x.DN;].Iter clearBool

        override x.ResetStruct() =
            base.ResetStruct()
            x.ClearBits()
            x.ACC.OValue <- 0u
            // x.PRE.Value <- 0us       // preset 도 clear 해야 하는가?
            ()


    type IStatement = interface end


    type Timer internal(typ:TimerType, timerStruct:TimerStruct, dsRte:DsRuntimeEnvironment) =

        let accumulator = new TickAccumulator(typ, timerStruct, dsRte)

        member _.Type = typ
        member _.Name = timerStruct.Name
        member _.EN = timerStruct.EN
        member _.TT = timerStruct.TT
        member _.DN = timerStruct.DN
        member _.PRE = timerStruct.PRE
        member _.ACC = timerStruct.ACC
        member _.RES = timerStruct.RES
        member _.TimerStruct = timerStruct

        // todo : uncomment

        ///// XGK 에서는 사용하는 timer 의 timer resolution 을 곱해서 실제 preset 값을 계산해야 한다.
        //member val XgkTimerResolution = 1.0 with get, set
        ///// XGK 에서 사전 설정된 timer resolution 을 고려해서 실제 preset 값을 계산
        //member x.CalculateXgkTimerPreset() = int ( (float timerStruct.PRE.Value) / x.XgkTimerResolution)

        member val InputEvaluateStatements:IStatement list = [] with get, set
        interface IDisposable with
            member _.Dispose() = (accumulator :> IDisposable).Dispose()



