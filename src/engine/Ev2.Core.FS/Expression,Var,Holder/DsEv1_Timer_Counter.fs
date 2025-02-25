namespace Dual.Ev2

open System
open System.Collections.Generic

open Dual.Common.Core.FS
open Dual.Common.Base.FS


[<AutoOpen>]
module rec TimerCounter =
    type TimerType = TON | TOF | TMR        // AB 에서 TMR 은 RTO 에 해당
    /// Timer / Counter 의 number data type
    type CountUnitType = uint32

    //제어 HW CPU 기기 타입
    type PlatformTarget =
        | WINDOWS
        | XGI
        | XGK
        | AB
        | MELSEC

    let xgkTimerCounterContactMarking = "$ON"

    type Storages() =
        inherit Dictionary<string, IStorage>(StringComparer.OrdinalIgnoreCase)
        new (existing:Storages) =
            Storages() then        // constructor chaning
            for kv in existing do
                base.Add(kv.Key, kv.Value)

    //type internal TickAccumulator(timerType:TimerType, timerStruct:TimerStruct) =
    //    let ts = timerStruct
    //    let tt = timerType

    //    let accumulateTON() =
    //        if ts.TT.TValue && not ts.DN.TValue && ts.ACC.TValue < ts.PRE.TValue then
    //            ts.ACC.TValue <- ts.ACC.TValue + MinTickInterval
    //            if ts.ACC.TValue >= ts.PRE.TValue then
    //                debugfn "Timer accumulator value reached"
    //                ts.TT.TValue <- false
    //                ts.DN.TValue <- true
    //                ts.EN.TValue <- true

    //    let accumulateTOF() =
    //        if ts.TT.TValue && ts.DN.TValue && not ts.EN.TValue && ts.ACC.TValue < ts.PRE.TValue then
    //            ts.ACC.TValue <- ts.ACC.TValue + MinTickInterval
    //            if ts.ACC.TValue >= ts.PRE.TValue then
    //                debugfn "Timer accumulator value reached"
    //                ts.TT.TValue <- false
    //                ts.DN.TValue <- false

    //    let accumulateRTO() =
    //        if ts.TT.TValue && not ts.DN.TValue && ts.EN.TValue && ts.ACC.TValue < ts.PRE.TValue then
    //            ts.ACC.TValue <- ts.ACC.TValue + MinTickInterval
    //            if ts.ACC.TValue >= ts.PRE.TValue then
    //                debugfn "Timer accumulator value reached"
    //                ts.TT.TValue <- false
    //                ts.EN.TValue <- false
    //                ts.DN.TValue <- true

    //    let accumulate() =
    //        //debugfn "Accumulating from %A" ts.ACC.Value
    //        match tt with
    //        | TON -> accumulateTON()
    //        | TOF -> accumulateTOF()
    //        | TMR -> accumulateRTO()

    //    let timerCallback (_: obj) = accumulate()

    //    let disposables = new CompositeDisposable()

    //    do
    //        ts.ResetStruct()

    //        debugfn "Timer subscribing to tick event"
    //        //theMinTickTimer.Subscribe(fun _ -> accumulate()) |> disposables.Add

    //        TimerModuleApi.timeBeginPeriod(TimerResolution) |> ignore
    //        new Timer(TimerCallback(timerCallback), null, TimeSpan.Zero, TimeSpan.FromMilliseconds(float MinTickInterval)) |> disposables.Add

    //        CpusEvent.ValueSubject.Where(fun (system, _storage, _value) -> system = (timerStruct:>IStorage).DsSystem)
    //            .Where(fun (_system, storage, _newValue) -> storage = timerStruct.EN)
    //            .Subscribe(fun (_system, _storage, newValue) ->
    //                if ts.ACC.Value < 0u || ts.PRE.Value < 0u then failwithlog "ERROR"
    //                let rungInCondition = newValue :?> bool
    //                //debugfn "%A rung-condition-in=%b with DN=%b" tt rungInCondition ts.DN.Value
    //                match tt, rungInCondition with
    //                | TON, true ->
    //                    ts.TT.Value <- not ts.DN.Value
    //                | TON, false -> ts.ResetStruct()

    //                | TOF, false ->
    //                    ts.EN.Value <- false
    //                    ts.TT.Value <- true
    //                    if not ts.DN.Value then
    //                        ts.ClearBits()

    //                | TOF, true ->
    //                    ts.EN.Value <- true
    //                    ts.TT.Value <- false     // spec 상충함 : // https://edisciplinas.usp.br/pluginfile.php/184942/mod_resource/content/1/Logix5000%20-%20Manual%20de%20Referencias.pdf 와 https://edisciplinas.usp.br/pluginfile.php/184942/mod_resource/content/1/Logix5000%20-%20Manual%20de%20Referencias.pdf 설명이 다름
    //                    ts.DN.Value <- true
    //                    ts.ACC.Value <- 0u

    //                | TMR, true ->
    //                    if ts.DN.Value then
    //                        ts.TT.Value <- false
    //                    else
    //                        ts.EN.Value <- true
    //                        ts.TT.Value <- true
    //                | TMR, false ->
    //                    ts.EN.Value <- false
    //                    ts.TT.Value <- false
    //            ) |> disposables.Add

    //        CpusEvent.ValueSubject.Where(fun (system, _storage, _value) -> system = (timerStruct:>IStorage).DsSystem)
    //            .Where(fun (_system, storage, _newValue) -> storage = ts.RES)
    //            .Subscribe(fun (_system, _storage, newValue) ->
    //                let resetCondition = newValue :?> bool
    //                if resetCondition then
    //                    ts.ACC.Value <- 0u
    //                    ts.DN.Value <- false
    //            ) |> disposables.Add

    //    interface IDisposable with
    //        member this.Dispose() =
    //            TimerModuleApi.timeEndPeriod(TimerResolution) |> ignore

    //            for d in disposables do
    //                d.Dispose()
    //            disposables.Clear()


    //let private clearBool(b:TValue<bool>) =
    //    if b |> isItNull |> not then
    //        b.Value <- false

    //[<AbstractClass>]
    //type TimerCounterBaseStruct (isTimer:bool option, name, dn, pre, acc, res, sys) =
    //    interface IStorage

    //    member private x.This = x
    //    member _.Name:string = name
    //    /// Done bit
    //    member _.DN:TValue<bool> = dn
    //    /// Preset value
    //    member _.PRE:TValue<CountUnitType> = pre
    //    member _.ACC:TValue<CountUnitType> = acc
    //    /// Reset bit.
    //    member _.RES:TValue<bool> = res
    //    /// XGI load
    //    member _.LD:TValue<bool> = res
    //    abstract member ResetStruct:unit -> unit
    //    default x.ResetStruct() =
    //        // -- preset 은 clear 대상이 아님: x.PRE,     reset 도 clear 해야 하는가? -- ???  x.RES
    //        clearBool x.DN
    //        clearBool x.LD

    //        if x.ACC |> isItNull |> not then
    //            x.ACC.Value <- 0u
    //    /// XGK 에서 할당한 counter/timer 변수 이름 임시 저장 공간.  e.g "C0001"
    //    member x.XgkStructVariableName =
    //        match isTimer with
    //        | Some true -> sprintf "T%04d" x.XgkStructVariableDevicePos
    //        | Some false -> sprintf "C%04d" x.XgkStructVariableDevicePos
    //        | _ -> failwith "ERROR"

    //    member val XgkStructVariableDevicePos = -1 with get, set

    //let createMemberVariable<'T>(name:string, value:'T) =
    //    TValue<'T>(value, Name=name, IsMemberVariable=true)

    //type TimerStruct private(typ:TimerType, name, en, tt, dn, pre, acc, res, sys) =
    //    inherit TimerCounterBaseStruct(Some true, name, dn, pre, acc, res, sys)

    //    /// Enable
    //    member _.EN:TValue<bool> = en
    //    /// Timing
    //    member _.TT:TValue<bool> = tt
    //    member _.Type = typ

    //    static member Create(typ:TimerType, storages:Storages, name, preset:CountUnitType, accum:CountUnitType, sys, target:PlatformTarget) =
    //        let suffixes  =
    //            match target with
    //            | XGK -> [".IN"; ".TT"; xgkTimerCounterContactMarking; ".PT"; ".ET"; ".RST"] // XGK 이름에 . 있으면 걸러짐 storagesToXgxSymbol
    //            | XGI | WINDOWS -> [".IN"; "._TT"; ".Q"; ".PT"; ".ET"; ".RST"]
    //            | AB -> [".EN"; ".TT"; ".DN"; ".PRE"; ".ACC"; ".RES"]
    //            | _ -> failwith "NOT yet supported"

    //        let en, tt, dn, pre, acc, res =
    //            let names = suffixes |> Seq.map (fun suffix -> $"{name}{suffix}") |> Seq.toList
    //            match names with
    //            | [en; tt; dn; pre; acc; res] -> en, tt, dn, pre, acc, res
    //            | _ -> failwith "Unexpected number of suffixes"

    //        let en  = createMemberVariable<bool>($"{en }", false)
    //        let tt  = createMemberVariable<bool>($"{tt }", false)
    //        let dn  = createMemberVariable<bool>($"{dn }", false).Tee(fun v -> v.TagKind <- VariableTag.PcSysVariable|>uint64)
    //        let pre = createMemberVariable<UInt32>(pre, preset)
    //        let acc = createMemberVariable<UInt32>(acc, accum)
    //        let res = createMemberVariable<bool>(res, false)

    //        storages.Add(en.Name, en)
    //        storages.Add(tt.Name, tt)
    //        storages.Add(dn.Name, dn)
    //        storages.Add(pre.Name, pre)
    //        storages.Add(acc.Name, acc)
    //        storages.Add(res.Name, res)

    //        let ts = new TimerStruct(typ, name, en, tt, dn, pre, acc, res, sys)
    //        storages.Add(name, ts)
    //        ts

    //    /// Clear EN, TT, DN bits
    //    member x.ClearBits() =
    //        [x.EN; x.TT; x.DN;].Iter clearBool

    //    override x.ResetStruct() =
    //        base.ResetStruct()
    //        x.ClearBits()
    //        x.ACC.Value <- 0u
    //        // x.PRE.Value <- 0us       // preset 도 clear 해야 하는가?
    //        ()

    //type Timer internal(typ:TimerType, timerStruct:TimerStruct) =

    //    let accumulator = new TickAccumulator(typ, timerStruct)

    //    member _.Type = typ
    //    member _.Name = timerStruct.Name
    //    member _.EN = timerStruct.EN
    //    member _.TT = timerStruct.TT
    //    member _.DN = timerStruct.DN
    //    member _.PRE = timerStruct.PRE
    //    member _.ACC = timerStruct.ACC
    //    member _.RES = timerStruct.RES
    //    member _.TimerStruct = timerStruct

    //    ///// XGK 에서는 사용하는 timer 의 timer resolution 을 곱해서 실제 preset 값을 계산해야 한다.
    //    //member val XgkTimerResolution = 1.0 with get, set
    //    ///// XGK 에서 사전 설정된 timer resolution 을 고려해서 실제 preset 값을 계산
    //    //member x.CalculateXgkTimerPreset() = int ( (float timerStruct.PRE.Value) / x.XgkTimerResolution)

    //    member val InputEvaluateStatements:Statement list = [] with get, set
    //    interface IDisposable with
    //        member _.Dispose() = (accumulator :> IDisposable).Dispose()

    //type Counter internal(typ:CounterType, counterStruct:CounterBaseStruct) =

    //    let accumulator = new CountAccumulator(typ, counterStruct)

    //    member _.Type = typ
    //    member _.CounterStruct = counterStruct
    //    member _.Name = counterStruct.Name
    //    /// Count up
    //    member _.CU = counterStruct.CU
    //    /// Count down
    //    member _.CD = counterStruct.CD
    //    /// Underflow
    //    member _.UN = counterStruct.UN
    //    /// Overflow
    //    member _.OV = counterStruct.OV
    //    /// Done bit
    //    member _.DN = counterStruct.DN
    //    /// Preset
    //    member _.PRE = counterStruct.PRE
    //    /// Accumulated
    //    member _.ACC = counterStruct.ACC
    //    /// Reset
    //    member _.RES = counterStruct.RES

    //    member val InputEvaluateStatements:Statement list = [] with get, set
    //    interface IDisposable with
    //        member this.Dispose() = (accumulator :> IDisposable).Dispose()




