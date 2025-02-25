namespace Dual.Ev2

open Dual.Common.Core.FS
open System
open System.Collections.Generic


[<AutoOpen>]
module TimerCounter =
    type TimerType = TON | TOF | TMR        // AB 에서 TMR 은 RTO 에 해당

    //[<AbstractClass>]
    //type TimerCounterBaseStruct (isTimer:bool option, name, dn, pre, acc, res, sys) =
    //    member private x.This = x
    //    member _.Name:string = name
    //    /// Done bit
    //    member _.DN:VariableBase<bool> = dn
    //    /// Preset value
    //    member _.PRE:VariableBase<CountUnitType> = pre
    //    member _.ACC:VariableBase<CountUnitType> = acc
    //    /// Reset bit.
    //    member _.RES:VariableBase<bool> = res
    //    /// XGI load
    //    member _.LD:VariableBase<bool> = res
    //    abstract member ResetStruct:unit -> unit
    //    default x.ResetStruct() =
    //        let clearBool(b:VariableBase<bool>) =
    //            if b |> isItNull |> not then
    //                b.Value <- false
    //        // -- preset 은 clear 대상이 아님: x.PRE,     reset 도 clear 해야 하는가? -- ???  x.RES
    //        clearVarBoolsOnDemand( [x.DN; x.LD;] )
    //        clearBool(x.LD)
    //        if x.ACC |> isItNull |> not then
    //            x.ACC.Value <- 0u
    //    /// XGK 에서 할당한 counter/timer 변수 이름 임시 저장 공간.  e.g "C0001"
    //    member x.XgkStructVariableName =
    //        match isTimer with
    //        | Some true -> sprintf "T%04d" x.XgkStructVariableDevicePos
    //        | Some false -> sprintf "C%04d" x.XgkStructVariableDevicePos
    //        | _ -> failwith "ERROR"

    //    member val XgkStructVariableDevicePos = -1 with get, set

    //type TimerStruct private(typ:TimerType, name, en, tt, dn, pre, acc, res, sys) =
    //    inherit TimerCounterBaseStruct(Some true, name, dn, pre, acc, res, sys)

    //    /// Enable
    //    member _.EN:VariableBase<bool> = en
    //    /// Timing
    //    member _.TT:VariableBase<bool> = tt
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

    //        let en  = createBool              $"{en }" false
    //        let tt  = createBool              $"{tt }" false
    //        let dn  = createBoolWithTagKind   $"{dn }" false (VariableTag.PcSysVariable|>int) // Done
    //        let pre = createUInt32            $"{pre}" preset
    //        let acc = createUInt32            $"{acc}" accum
    //        let res = createBool              $"{res}" false

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
    //        clearVarBoolsOnDemand( [x.EN; x.TT; x.DN;] )

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




