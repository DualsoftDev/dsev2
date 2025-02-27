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

    type (*internal*) CounterCreateParams = {
        DsRuntimeEnvironment: DsRuntimeEnvironment
        Type: CounterType
        Name: string
        Preset: CountUnitType
        CountUpCondition  : IExpression<bool> option
        CountDownCondition: IExpression<bool> option
        ResetCondition    : IExpression<bool> option
        LoadCondition     : IExpression<bool> option
        FunctionName:string
    }


    let private CreateCounterParameters(cParams:CounterCreateParams, storages:Storages, accum:CountUnitType) =
        let { DsRuntimeEnvironment=dsRte; Type=typ; Name=name; Preset=preset; }:CounterCreateParams = cParams
        let sys, target = dsRte.ISystem, dsRte.PlatformTarget
        let valueBag = dsRte.ValueBag

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
            cu  <- T.CreateMemberVariable<bool>  (  valueBag, sys, $"{name}.CU",  false)  // Count up enable bit
            res <- T.CreateMemberVariable<bool>  (  valueBag, sys, $"{name}.R",   false)
            pre <- T.CreateMemberVariable<UInt32>(  valueBag, sys, $"{name}.PV",  preset)
            dn  <- T.CreateMemberVariable<bool>  (  valueBag, sys, dnName,        false, T.SysVarTag) // Done
            acc <- T.CreateMemberVariable<UInt32>(  valueBag, sys, $"{name}.CV",  accum)
            add [cu; res; pre; dn; acc]

        | (WINDOWS | XGI| XGK), CTD ->
            ()
            cd  <- T.CreateMemberVariable<bool>  (  valueBag, sys, $"{name}.CD", false)   // Count down enable bit
            ld  <- T.CreateMemberVariable<bool>  (  valueBag, sys, $"{name}.LD", false)   // Load
            pre <- T.CreateMemberVariable<UInt32>(  valueBag, sys, $"{name}.PV", preset)
            dn  <- T.CreateMemberVariable<bool>  (  valueBag, sys, dnName,       false, T.SysVarTag) // Done
            acc <- T.CreateMemberVariable<UInt32>(  valueBag, sys, $"{name}.CV", accum)
            add [cd; res; ld; pre; dn; acc]

        | (WINDOWS | XGI| XGK), CTUD ->
            cu  <- T.CreateMemberVariable<bool>    ( valueBag, sys, $"{name}.CU", false)  // Count up enable bit
            cd  <- T.CreateMemberVariable<bool>    ( valueBag, sys, $"{name}.CD", false)  // Count down enable bit
            res <- T.CreateMemberVariable<bool>    ( valueBag, sys, $"{name}.R" , false)
            ld  <- T.CreateMemberVariable<bool>    ( valueBag, sys, $"{name}.LD", false)  // Load
            pre <- T.CreateMemberVariable<UInt32>  ( valueBag, sys, $"{name}.PV", preset)
            dn  <- T.CreateMemberVariable<bool>    ( valueBag, sys, dnName,       false, T.SysVarTag) // Done
            dnDown  <- T.CreateMemberVariable<bool>( valueBag, sys, $"{name}.QD", false, T.SysVarTag) // Done
            acc <- T.CreateMemberVariable<UInt32>  ( valueBag, sys, $"{name}.CV", accum)
            add [cu; cd; res; ld; pre; dn; dnDown; acc]

        | (WINDOWS | XGI| XGK), CTR ->
            cd  <- T.CreateMemberVariable<bool>  (   valueBag, sys, $"{name}.CD",  false)   // Count down enable bit
            pre <- T.CreateMemberVariable<UInt32>(   valueBag, sys, $"{name}.PV",  preset)
            res <- T.CreateMemberVariable<bool>  (   valueBag, sys, $"{name}.RST", false)
            dn  <- T.CreateMemberVariable<bool>  (   valueBag, sys, dnName,        false, T.SysVarTag) // Done
            acc <- T.CreateMemberVariable<UInt32>(   valueBag, sys, $"{name}.CV",  accum)
            add [cd; pre; res; dn; acc]

        | _ ->
            match typ with
            | CTU ->
                cu  <- T.CreateMemberVariable<bool>( valueBag, sys, $"{name}.CU", false)  // Count up enable bit
                add [cu]
            | CTR | CTD ->
                cd  <- T.CreateMemberVariable<bool>( valueBag, sys, $"{name}.CD", false)  // Count down enable bit
                add [cd]
            | CTUD ->
                cu  <- T.CreateMemberVariable<bool>( valueBag, sys, $"{name}.CU", false) // Count up enable bit
                cd  <- T.CreateMemberVariable<bool>( valueBag, sys, $"{name}.CD", false) // Count down enable bit
                add [cu; cd]


            ov  <- T.CreateMemberVariable<bool>  (   valueBag, sys, $"{name}.OV", false)   // Overflow
            un  <- T.CreateMemberVariable<bool>  (   valueBag, sys, $"{name}.UN", false)   // Underflow
            ld  <- T.CreateMemberVariable<bool>  (   valueBag, sys, $"{name}.LD", false)   // XGI: Load
            dn  <- T.CreateMemberVariable<bool>  (   valueBag, sys, $"{name}.DN", false, T.SysVarTag) // Done
            pre <- T.CreateMemberVariable<UInt32>(   valueBag, sys, $"{name}.PRE", preset)
            acc <- T.CreateMemberVariable<UInt32>(   valueBag, sys, $"{name}.ACC", accum)
            res <- T.CreateMemberVariable<bool>  (   valueBag, sys, $"{name}.RES", false)
            add [ov; un; dn; pre; acc; res;]

        (* 내부 structure 가 AB 기반이므로, 메모리 자체는 생성하되, storage 에 등록하지는 않는다. *)
        if isItNull(ov) then
            ov  <- T.CreateMemberVariable<bool>  (   valueBag, sys, $"{name}.OV", false)
        if isItNull(un) then
            un  <- T.CreateMemberVariable<bool>  (   valueBag, sys, $"{name}.UN", false)
        if isItNull(cu) then
            cu  <- T.CreateMemberVariable<bool>  (   valueBag, sys, $"{name}.CU", false)
        if isItNull(cd) then
            cd  <- T.CreateMemberVariable<bool>  (   valueBag, sys, $"{name}.CD", false)
        if isItNull(cd) then
            res  <- T.CreateMemberVariable<bool> (   valueBag, sys, $"{name}.RES", false)

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
        static member Create(cParams:CounterCreateParams, storages:Storages, accum:uint32) =
            let { DsRuntimeEnvironment=dsRte; Type=typ; Name=name; Preset=preset; }:CounterCreateParams = cParams
            let sys = dsRte.ISystem
            let counterParams = CreateCounterParameters(cParams, storages, accum)
            let cs = new CTUStruct(counterParams, sys)
            storages.Add(name, cs)
            cs

    type CTDStruct private(counterParams:CounterParams, sys) =
        inherit CounterBaseStruct(counterParams, sys)
        member _.CD = base.CD
        interface ICTD with
            member x.CD = x.CD
            member x.LD = x.LD

        static member Create(cParams:CounterCreateParams, storages:Storages, accum:uint32) =
            let { DsRuntimeEnvironment=dsRte; Type=typ; Name=name; Preset=preset; }:CounterCreateParams = cParams
            let sys = dsRte.ISystem
            let counterParams = CreateCounterParameters(cParams, storages, accum)

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

        static member Create(cParams:CounterCreateParams, storages:Storages, accum:uint32) =
            let { DsRuntimeEnvironment=dsRte; Type=typ; Name=name; Preset=preset; }:CounterCreateParams = cParams
            let sys = dsRte.ISystem
            let counterParams = CreateCounterParameters(cParams, storages, accum)
            let cs = new CTUDStruct(counterParams, sys)
            storages.Add(name, cs)
            cs

    type CTRStruct(counterParams:CounterParams , sys) =
        inherit CounterBaseStruct(counterParams, sys)
        member _.RES = base.RES
        interface ICTR with
            member x.CD = x.CD

        static member Create(cParams:CounterCreateParams, storages:Storages, accum:uint32) =
            let { DsRuntimeEnvironment=dsRte; Type=typ; Name=name; Preset=preset; }:CounterCreateParams = cParams
            let sys = dsRte.ISystem
            let counterParams = CreateCounterParameters(cParams, storages, accum)
            let cs = new CTRStruct(counterParams, sys)
            storages.Add(name, cs)
            cs



    type internal CountAccumulator(counterType: CounterType, counterStruct: CounterBaseStruct, dsRte: DsRuntimeEnvironment) =
        /// CompositeDisposable을 사용하여 구독한 이벤트를 관리
        let disposables = new CompositeDisposable()
        let cs = counterStruct
        let system = counterStruct.DsSystem

        /// ACC 및 PRE 값이 0 이상인지 확인하는 함수
        let validateAccPre (): unit =
            if cs.ACC.TValue < 0u || cs.PRE.TValue < 0u then failwithlog "ERROR"

        /// ACC 값을 변경하고 특정 조건이 충족되면 추가 동작을 수행하는 함수
        /// - updateFn: ACC 값을 변경하는 함수
        /// - comparisonFn: 현재 ACC 값과 PRE 값 비교 함수 (>=, <= 등)
        /// - onLimitReached: ACC 값이 특정 조건을 충족했을 때 실행할 함수
        let updateAccAndCheckLimit (updateFn: unit -> unit) (comparisonFn: uint32 -> uint32 -> bool) (onLimitReached: unit -> unit): unit =
            validateAccPre()
            updateFn ()
            if comparisonFn cs.ACC.TValue cs.PRE.TValue then
                debugfn "Counter accumulator value reached"
                onLimitReached()

        /// 특정 값이 변경될 때 특정 동작을 실행하는 구독 함수
        /// - storage: 감지할 값 (DsStorage)
        /// - condition: 조건을 검사하는 함수
        /// - action: 조건이 충족되었을 때 실행할 함수
        let subscribeToChange (storage:IStorage) (condition: unit -> bool) (action: unit -> unit): unit =
            dsRte.ValueChangedSubject
                .Where(fun (s, _) -> s.DsSystem = system)
                .Where(fun (s, _) -> s = storage && condition())
                .Subscribe(fun (_, _) -> action ())
            |> disposables.Add

        /// CTD 구조체의 LD(Load) 신호가 True일 때 ACC를 PRE 값으로 설정
        let registerLoad (csd: ICTD): unit =
            let condition = fun () -> csd.LD.TValue
            let action = fun () -> cs.ACC.TValue <- cs.PRE.TValue
            subscribeToChange csd.LD condition action

        /// CTU(Count Up) 타입의 카운터 증가를 등록
        let registerCTU (csu: ICTU): unit =
            let condition = fun () -> csu.CU.TValue
            let action =
                let update = fun () -> cs.ACC.TValue <- cs.ACC.TValue + 1u
                let onLimitReached = fun () -> cs.DN.TValue <- true
                fun () -> updateAccAndCheckLimit update (>=) onLimitReached
            subscribeToChange csu.CU condition action

        /// CTD(Count Down) 타입의 카운터 감소를 등록
        let registerCTD (csd: ICTD): unit =
            registerLoad csd
            let condition = fun () -> csd.CD.TValue
            let action =
                let update = fun () -> if cs.ACC.TValue > 0u then cs.ACC.TValue <- cs.ACC.TValue - 1u
                let onLimitReached = fun () -> cs.DN.TValue <- true
                fun () -> updateAccAndCheckLimit update (<=) onLimitReached
            subscribeToChange csd.CD condition action

        /// CTR(Count Reset) 타입의 카운터 동작을 등록
        let registerCTR (csr: ICTR): unit =
            let condition = fun () -> csr.CD.TValue
            let action =
                let update = fun () -> cs.ACC.TValue <- cs.ACC.TValue + 1u
                let onLimitReached = fun () ->
                    if cs.ACC.TValue = cs.PRE.TValue then
                        debugfn "Counter accumulator value reached"
                        cs.DN.TValue <- true
                    elif cs.ACC.TValue > cs.PRE.TValue then
                        cs.ACC.TValue <- 1u
                        cs.DN.TValue <- false
                fun () -> updateAccAndCheckLimit update (>=) onLimitReached
            subscribeToChange csr.CD condition action


        /// Reset 신호를 감지하여 카운터 값을 초기화
        let registerReset (): unit =
            let condition = fun () -> cs.RES.TValue
            let action = fun () ->
                validateAccPre()
                debugfn "Counter reset requested"
                cs.ACC.TValue <- 0u
                [cs.DN; cs.CU; cs.CD; cs.OV; cs.UN] |> List.iter clearBool
            subscribeToChange cs.RES condition action

        /// 초기화 및 카운터 타입별 등록 실행
        do
            cs.ResetStruct()
            registerReset()
            match cs, counterType with
            | :? CTUStruct as csu, CTU -> registerCTU csu
            | :? CTRStruct as csr, CTR -> registerCTR csr
            | :? CTDStruct as csd, CTD -> registerCTD csd
            | :? CTUDStruct as csud, CTUD -> registerCTU csud; registerCTD csud
            | _ -> failwithlog "ERROR"

        /// IDisposable 인터페이스 구현: 모든 구독 해제
        interface IDisposable with
            member this.Dispose(): unit =
                for d in disposables do d.Dispose()
                disposables.Clear()



    type Counter internal(typ:CounterType, counterStruct:CounterBaseStruct, dsRte:DsRuntimeEnvironment) =

        let accumulator = new CountAccumulator(typ, counterStruct, dsRte)

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








