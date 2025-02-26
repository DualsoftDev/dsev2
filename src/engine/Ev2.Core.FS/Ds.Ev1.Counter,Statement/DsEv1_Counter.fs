namespace Dual.Ev2

open System
open System.Reactive.Linq
open System.Reactive.Disposables

open Dual.Common.Core.FS
open Dual.Common.Base.FS



[<AutoOpen>]
module CounterModule =
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
        Storages:Storages
        Name:string
        Preset: CountUnitType
        Accumulator: CountUnitType
        CU: TValue<bool>
        CD: TValue<bool>
        OV: TValue<bool>
        UN: TValue<bool>
        DN: TValue<bool>
        /// XGI load
        LD: TValue<bool>
        DNDown: TValue<bool>

        RES: TValue<bool>
        PRE: TValue<CountUnitType>
        ACC: TValue<CountUnitType>
    }

    let private CreateCounterParameters(typ:CounterType, storages:Storages, name, preset, accum:CountUnitType, target:PlatformTarget) =
        let nullB = getNull<TValue<bool>>()
        let mutable cu  = nullB  // Count up enable bit
        let mutable cd  = nullB  // Count down enable bit
        let mutable ov  = nullB  // Overflow
        let mutable un  = nullB  // Underflow
        let mutable ld  = nullB  // XGI: Load

        let mutable dn  = nullB
        let mutable dnDown  = nullB
        let mutable pre = getNull<TValue<CountUnitType>>()
        let mutable acc = getNull<TValue<CountUnitType>>()
        let mutable res = nullB
        let add =
            let addTagsToStorages (storages:Storages) (ts:ValueHolder seq) =
                for t in ts do
                    if not (isItNull t) then
                        storages.Add(t.Name, t)
            addTagsToStorages storages
        let dnName = if target = XGK
                     then $"{name}{xgkTimerCounterContactMarking}"
                     else
                        if typ = CTUD
                        then $"{name}.QU"
                        else $"{name}.Q"

        match target, typ with
        | (WINDOWS | XGI| XGK), CTU ->
            cu  <- T.CreateMemberVariable<bool>  (  $"{name}.CU",  false)  // Count up enable bit
            res <- T.CreateMemberVariable<bool>  (  $"{name}.R",   false)
            pre <- T.CreateMemberVariable<UInt32>(  $"{name}.PV",  preset)
            dn  <- T.CreateMemberVariable<bool>  (  dnName,        false, T.SysVarTag) // Done
            acc <- T.CreateMemberVariable<UInt32>(  $"{name}.CV",  accum)
            add [cu; res; pre; dn; acc]

        | (WINDOWS | XGI| XGK), CTD ->
            ()
            cd  <- T.CreateMemberVariable<bool>  (  $"{name}.CD", false)   // Count down enable bit
            ld  <- T.CreateMemberVariable<bool>  (  $"{name}.LD", false)   // Load
            pre <- T.CreateMemberVariable<UInt32>(  $"{name}.PV", preset)
            dn  <- T.CreateMemberVariable<bool>  (  dnName,       false, T.SysVarTag) // Done
            acc <- T.CreateMemberVariable<UInt32>(  $"{name}.CV", accum)
            add [cd; res; ld; pre; dn; acc]

        | (WINDOWS | XGI| XGK), CTUD ->
            cu  <- T.CreateMemberVariable<bool>    ( $"{name}.CU", false)  // Count up enable bit
            cd  <- T.CreateMemberVariable<bool>    ( $"{name}.CD", false)  // Count down enable bit
            res <- T.CreateMemberVariable<bool>    ( $"{name}.R" , false)
            ld  <- T.CreateMemberVariable<bool>    ( $"{name}.LD", false)  // Load
            pre <- T.CreateMemberVariable<UInt32>  ( $"{name}.PV", preset)
            dn  <- T.CreateMemberVariable<bool>    ( dnName,       false, T.SysVarTag) // Done
            dnDown  <- T.CreateMemberVariable<bool>( $"{name}.QD", false, T.SysVarTag) // Done
            acc <- T.CreateMemberVariable<UInt32>  ( $"{name}.CV", accum)
            add [cu; cd; res; ld; pre; dn; dnDown; acc]

        | (WINDOWS | XGI| XGK), CTR ->
            cd  <- T.CreateMemberVariable<bool>  (   $"{name}.CD",  false)   // Count down enable bit
            pre <- T.CreateMemberVariable<UInt32>(   $"{name}.PV",  preset)
            res <- T.CreateMemberVariable<bool>  (   $"{name}.RST", false)
            dn  <- T.CreateMemberVariable<bool>  (   dnName,        false, T.SysVarTag) // Done
            acc <- T.CreateMemberVariable<UInt32>(   $"{name}.CV",  accum)
            add [cd; pre; res; dn; acc]

        | _ ->
            match typ with
            | CTU ->
                cu  <- T.CreateMemberVariable<bool>( $"{name}.CU", false)  // Count up enable bit
                add [cu]
            | CTR | CTD ->
                cd  <- T.CreateMemberVariable<bool>( $"{name}.CD", false)  // Count down enable bit
                add [cd]
            | CTUD ->
                cu  <- T.CreateMemberVariable<bool>( $"{name}.CU", false) // Count up enable bit
                cd  <- T.CreateMemberVariable<bool>( $"{name}.CD", false) // Count down enable bit
                add [cu; cd]


            ov  <- T.CreateMemberVariable<bool>  (   $"{name}.OV", false)   // Overflow
            un  <- T.CreateMemberVariable<bool>  (   $"{name}.UN", false)   // Underflow
            ld  <- T.CreateMemberVariable<bool>  (   $"{name}.LD", false)   // XGI: Load
            dn  <- T.CreateMemberVariable<bool>  (   $"{name}.DN", false, T.SysVarTag) // Done
            pre <- T.CreateMemberVariable<UInt32>(   $"{name}.PRE", preset)
            acc <- T.CreateMemberVariable<UInt32>(   $"{name}.ACC", accum)
            res <- T.CreateMemberVariable<bool>  (   $"{name}.RES", false)
            add [ov; un; dn; pre; acc; res;]

        (* 내부 structure 가 AB 기반이므로, 메모리 자체는 생성하되, storage 에 등록하지는 않는다. *)
        if isItNull(ov) then
            ov  <- T.CreateMemberVariable<bool>  (   $"{name}.OV", false)
        if isItNull(un) then
            un  <- T.CreateMemberVariable<bool>  (   $"{name}.UN", false)
        if isItNull(cu) then
            cu  <- T.CreateMemberVariable<bool>  (   $"{name}.CU", false)
        if isItNull(cd) then
            cd  <- T.CreateMemberVariable<bool>  (   $"{name}.CD", false)
        if isItNull(cd) then
            res  <- T.CreateMemberVariable<bool> (   $"{name}.RES", false)

        {
            Type        = typ
            Storages    = storages
            Name        = name
            Preset      = preset
            Accumulator = accum
            CU          = cu
            CD          = cd
            OV          = ov
            UN          = un
            DN          = dn
            DNDown      = dnDown
            LD          = ld
            RES         = res
            PRE         = pre
            ACC         = acc
        }

    [<AbstractClass>]
    type CounterBaseStruct(cp:CounterParams, sys) =
        inherit TimerCounterBaseStruct(false, cp.Name, cp.DN, cp.PRE, cp.ACC, cp.RES, sys)

        member _.CU:TValue<bool> = cp.CU  // Count up enable bit
        member _.CD:TValue<bool> = cp.CD  // Count down enable bit
        member _.OV:TValue<bool> = cp.OV  // Overflow
        member _.UN:TValue<bool> = cp.UN  // Underflow
        member _.LD:TValue<bool> = cp.LD  // Load (XGI)
        member _.Type = cp.Type
        override x.ResetStruct() =
            base.ResetStruct()
            [x.OV; x.UN; x.CU; x.CD;].Iter clearBool


    type ICounter = interface end

    type ICTU =
        inherit ICounter
        abstract CU:TValue<bool>

    type ICTD =
        inherit ICounter
        abstract CD:TValue<bool>
        abstract LD:TValue<bool>

    type ICTUD =
        inherit ICTU
        inherit ICTD

    type ICTR =
        inherit ICounter
        abstract CD:TValue<bool>

    type CTUStruct private(counterParams:CounterParams, sys) =
        inherit CounterBaseStruct(counterParams, sys)
        member _.CU = base.CU
        interface ICTU with
            member x.CU = x.CU
        static member Create(typ:CounterType, storages, name, preset:CountUnitType, accum:CountUnitType, sys, target:PlatformTarget) =
            let counterParams = CreateCounterParameters(typ, storages, name, preset, accum, target)
            let cs = new CTUStruct(counterParams, sys)
            storages.Add(name, cs)
            cs

    type CTDStruct private(counterParams:CounterParams, sys) =
        inherit CounterBaseStruct(counterParams, sys)
        member _.CD = base.CD
        interface ICTD with
            member x.CD = x.CD
            member x.LD = x.LD
        static member Create(typ:CounterType, storages, name, preset:CountUnitType, accum:CountUnitType, sys, target:PlatformTarget) =
            let counterParams = CreateCounterParameters(typ, storages, name, preset, accum, target)
            let cs = new CTDStruct(counterParams, sys)
            storages.Add(name, cs)
            cs

    type CTUDStruct private(counterParams:CounterParams, sys) =
        inherit CounterBaseStruct(counterParams, sys)
        member _.CU = base.CU
        member _.CD = base.CD
        interface ICTUD with
            member x.CU = x.CU
            member x.CD = x.CD
            member x.LD = x.LD
        static member Create(typ:CounterType, storages, name, preset:CountUnitType, accum:CountUnitType, sys, target:PlatformTarget) =
            let counterParams = CreateCounterParameters(typ, storages, name, preset, accum, target)
            let cs = new CTUDStruct(counterParams, sys)
            storages.Add(name, cs)
            cs

    type CTRStruct(counterParams:CounterParams , sys) =
        inherit CounterBaseStruct(counterParams, sys)
        member _.RES = base.RES
        interface ICTR with
            member x.CD = x.CD
        static member Create(typ:CounterType, storages, name, preset:CountUnitType, accum:CountUnitType, sys, target:PlatformTarget) =
            let counterParams = CreateCounterParameters(typ, storages, name, preset, accum, target)
            let cs = new CTRStruct(counterParams, sys)
            storages.Add(name, cs)
            cs

    type internal CountAccumulator(counterType:CounterType, counterStruct:CounterBaseStruct)=
        let disposables = new CompositeDisposable()

        let cs = counterStruct
        let system = (counterStruct:>ValueHolder).DsSystem
        let registerLoad() =
            let csd = box cs :?> ICTD       // CTD or CTUD 둘다 적용
            CpusEvent.ValueSubject
                .Where(fun (sys, _storage, _value) -> sys = system)
                .Where(fun (_sys, storage, _newValue) -> storage = csd.LD && csd.LD.TValue)
                .Subscribe(fun (_sys, _storage, _newValue) ->
                    cs.ACC.TValue <- cs.PRE.TValue
            ) |> disposables.Add

        let registerCTU() =
            let csu = box cs :?> ICTU
            CpusEvent.ValueSubject
                .Where(fun (sys, _storage, _value) -> sys = system)
                .Where(fun (_sys, storage, _newValue) -> storage = csu.CU && csu.CU.TValue)
                .Subscribe(fun (_sys, _storage, _newValue) ->
                    if cs.ACC.TValue < 0u || cs.PRE.TValue < 0u then failwithlog "ERROR"
                    cs.ACC.TValue <- cs.ACC.TValue + 1u
                    if cs.ACC.TValue >= cs.PRE.TValue then
                        debugfn "Counter accumulator value reached"
                        cs.DN.TValue <- true
            ) |> disposables.Add
        let registerCTD() =
            let csd = box cs :?> ICTD
            registerLoad()
            CpusEvent.ValueSubject
                .Where(fun (sys, _storage, _value) -> sys = system)
                .Where(fun (_sys, storage, _newValue) -> storage = csd.CD && csd.CD.TValue)
                .Subscribe(fun (_sys, _storage, _newValue) ->
                    if cs.ACC.TValue < 0u || cs.PRE.TValue < 0u then failwithlog "ERROR"
                    cs.ACC.TValue <- cs.ACC.TValue - 1u
                    if cs.ACC.TValue <= cs.PRE.TValue then
                        debugfn "Counter accumulator value reached"
                        cs.DN.TValue <- true
            ) |> disposables.Add

        let registerCTR() =
            let csr = box cs :?> ICTR
            CpusEvent.ValueSubject
                .Where(fun (sys, _storage, _value) -> sys = system)
                .Where(fun (_sys, storage, _newValue) -> storage = csr.CD && csr.CD.TValue)
                .Subscribe(fun (_sys, _storage, _newValue) ->
                    if cs.ACC.TValue < 0u || cs.PRE.TValue < 0u then failwithlog "ERROR"
                    cs.ACC.TValue <- cs.ACC.TValue + 1u
                    if cs.ACC.TValue = cs.PRE.TValue then
                        debugfn "Counter accumulator value reached"
                        cs.DN.TValue <- true
                    if cs.ACC.TValue > cs.PRE.TValue then
                        cs.ACC.TValue <- 1u
                        cs.DN.TValue <- false
            ) |> disposables.Add


        let registerReset() =
            CpusEvent.ValueSubject
                .Where(fun (sys, _storage, _value) -> sys = (counterStruct:>ValueHolder).DsSystem)
                .Where(fun (_sys, storage, _newValue) -> storage = cs.RES && cs.RES.TValue)
                .Subscribe(fun (_sys, _storage, _newValue) ->
                    debugfn "Counter reset requested"
                    if cs.ACC.TValue < 0u || cs.PRE.TValue < 0u then
                        failwithlog "ERROR"
                    cs.ACC.TValue <- 0u
                    [cs.DN; cs.CU; cs.CD; cs.OV; cs.UN;].Iter clearBool
            ) |> disposables.Add

        do
            cs.ResetStruct()
            registerReset()
            match cs, counterType with
            | :? CTUStruct, CTU -> registerCTU()
            | :? CTRStruct, CTR -> registerCTR()
            | :? CTDStruct, CTD -> registerCTD()
            | :? CTUDStruct, CTUD -> registerCTU(); registerCTD();
            | _ -> failwithlog "ERROR"

        interface IDisposable with
            member this.Dispose() =
                for d in disposables do
                    d.Dispose()
                disposables.Clear()




    type Counter internal(typ:CounterType, counterStruct:CounterBaseStruct) =

        let accumulator = new CountAccumulator(typ, counterStruct)

        member _.Type = typ
        member _.CounterStruct = counterStruct
        member _.Name = counterStruct.Name
        /// Count up
        member _.CU = counterStruct.CU
        /// Count down
        member _.CD = counterStruct.CD
        /// Underflow
        member _.UN = counterStruct.UN
        /// Overflow
        member _.OV = counterStruct.OV
        /// Done bit
        member _.DN = counterStruct.DN
        /// Preset
        member _.PRE = counterStruct.PRE
        /// Accumulated
        member _.ACC = counterStruct.ACC
        /// Reset
        member _.RES = counterStruct.RES

        member val InputEvaluateStatements:IStatement list = [] with get, set


        interface IDisposable with
            member this.Dispose() = (accumulator :> IDisposable).Dispose()








