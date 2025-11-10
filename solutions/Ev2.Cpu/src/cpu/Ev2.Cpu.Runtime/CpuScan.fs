namespace Ev2.Cpu.Runtime

open System
open System.Threading
open System.Threading.Tasks
open Ev2.Cpu.Core
open Ev2.Cpu.Core.UserDefined
open Ev2.Cpu.Runtime.DependencyAnalyzer

// ============================================================================
// 스캔 설정
// ============================================================================

type ScanConfig =
    {
        /// 스캔 주기(ms). None이면 ctx.CycleTime 사용
        CycleTimeMs  : int option
        /// 스캔 오버런 경고 임계값(ms). None이면 경고 비활성
        WarnIfOverMs : int option
        /// 변경 감지 기반 선택적 스캔 활성화
        SelectiveMode: bool
        /// 런타임 이벤트 싱크 (텔레메트리 및 진단용)
        EventSink    : IRuntimeEventSink option
        /// 스테이지별 데드라인 임계값 (성능 모니터링용)
        StageThresholds : StageThresholds option
    }
    static member Default =
        // 스테이지 임계값은 생성자에서 사이클 타임 기반으로 계산됨
        { CycleTimeMs = None; WarnIfOverMs = Some 5_000; SelectiveMode = false; EventSink = None; StageThresholds = None }

// ============================================================================
// CPU 스캔 엔진 - IEC 61131-3 표준 4단계 스캔 사이클
// ============================================================================
// 아키텍처: IEC 61131-3 준수 스캔 사이클 + 런타임 업데이트 지원
// 스레드 모델: 단일 스캔 루프 스레드, WaitHandle 기반 예외 없는 취소
// 상태 머신: Running → Paused/Breakpoint/Stopped/Error (상태 보존 방식)
// ============================================================================

type CpuScanEngine
    (
        program : Statement.Program,
        ctx     : Ev2.Cpu.Runtime.ExecutionContext,
        config  : ScanConfig option,
        updateManager : RuntimeUpdateManager option,
        retainStorage : IRetainStorage option
    ) =

    let mutable cfg = defaultArg config ScanConfig.Default
    let mutable loopTask : Task option = None
    let mutable cts      : CancellationTokenSource option = None
    let mutable isFirstScan = true
    let mutable currentProgramBody = program.Body

    // RETAIN 저장 Task의 스레드 간 가시성을 위한 volatile 필드
    // 스캔 루프(쓰기) ↔ StopAsync(읽기) 간 바쁜 대기 없이 동기화
    [<VolatileField>]
    let mutable pendingRetainSave : Task option = None
    let retainSaveLock = obj()

    do
        // 사이클 타임 및 스테이지 임계값 초기화
        let cycleTimeMs = defaultArg cfg.CycleTimeMs ctx.CycleTime
        cfg.CycleTimeMs |> Option.iter (fun customCycleTime -> ctx.CycleTime <- customCycleTime)
        cfg <- match cfg.StageThresholds with
               | None -> { cfg with StageThresholds = Some (StageThresholds.defaultForCycleTime cycleTimeMs) }
               | Some _ -> cfg

        // 선택적 스캔을 위한 의존성 그래프 구축
        let dependencies = DependencyAnalyzer.buildDependencyMap currentProgramBody
        ctx.Memory.SetDependencyMap dependencies
        if cfg.SelectiveMode then
            ctx.Memory.MarkAllChanged()

        // 런타임 업데이트 매니저 초기화
        updateManager |> Option.iter (fun mgr -> mgr.SetProgramBody(currentProgramBody))

        // 릴레이 상태 관리자 생성 (호스트가 제공하지 않은 경우)
        // 사전 등록된 릴레이나 커스텀 이벤트 싱크를 가진 관리자 허용
        match ctx.RelayStateManager with
        | None -> ctx.RelayStateManager <- Some (new RelayStateManager(ctx.TimeProvider, cfg.EventSink))
        | Some _ -> ()

        // ────────────────────────────────────────────────────────────────
        // RETAIN 데이터 자동 복원 (엔진 시작 시)
        // ────────────────────────────────────────────────────────────────
        retainStorage |> Option.iter (fun storage ->
            match storage.Load() with
            | Ok (Some snapshot) ->
                ctx.Memory.RestoreFromSnapshot(snapshot)
                let varCount = snapshot.Variables.Length
                let fbStaticCount = snapshot.FBStaticData |> List.sumBy (fun fb -> fb.Variables.Length)
                let totalCount = varCount + fbStaticCount
                Context.trace ctx (sprintf "[RETAIN] 복원 완료: %d개 변수 (%s)"
                    totalCount (snapshot.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")))
                RuntimeTelemetry.retainLoaded totalCount
                cfg.EventSink |> Option.iter (fun sink ->
                    sink.Publish(RuntimeEvent.RetainLoaded(totalCount, DateTime.UtcNow)))
            | Ok None ->
                Context.trace ctx "[RETAIN] 저장된 데이터 없음 (첫 실행 또는 파일 삭제됨)"
            | Error err ->
                Context.warning ctx (sprintf "[RETAIN] 로드 실패: %s" err))

    /// 단일 스캔 사이클 실행 후 경과 시간(ms) 반환
    member this.ScanOnce() : int =
        // 실행 상태 확인 - 오류 시 중단, 디버그 상태 보존
        match ctx.State with
        | ExecutionState.Error _ -> 0  // Fatal 오류 - 손상된 컨텍스트에서 실행 방지
        | ExecutionState.Paused | ExecutionState.Breakpoint _ -> 0  // 디버그 상태 보존
        | ExecutionState.Stopped ->
            // 수동 스캔: 임시로 Running 전환 후 Stopped 복원
            let priorState = ctx.State
            ctx.State <- ExecutionState.Running
            let result = this.executeScan()
            match ctx.State with
            | ExecutionState.Error _ | ExecutionState.Breakpoint _ -> ()  // 진단 상태 유지
            | _ -> ctx.State <- priorState
            result
        | ExecutionState.Running ->
            this.executeScan()

    member private this.executeScan() : int =
        // 이벤트 순서를 위해 실행 전 스캔 인덱스 증가
        ctx.Memory.IncrementScan()
        let scanIndex = ctx.ScanIndex
        let scanStartTime = DateTime.UtcNow
        let sw = System.Diagnostics.Stopwatch.StartNew()

        // 릴레이 스캔 인덱스를 실행 전 업데이트 (전환 시 현재 인덱스 필요)
        ctx.RelayStateManager |> Option.iter (fun manager ->
            manager.UpdateScanIndex(scanIndex))

        cfg.EventSink |> Option.iter (fun sink ->
            sink.Publish(RuntimeEvent.ScanStarted(scanIndex, scanStartTime)))

        try
            // ════════════════════════════════════════════════════════════
            // Stage 1: PrepareInputs - 런타임 업데이트, 의존성 재구축
            // ════════════════════════════════════════════════════════════
            let swPrepare = System.Diagnostics.Stopwatch.StartNew()

            match updateManager with
            | Some mgr ->
                let updateResults = mgr.ProcessPendingUpdates()

                // 업데이트 결과 로깅 및 텔레메트리 발행
                updateResults |> List.iter (fun result ->
                    match result with
                    | UpdateResult.Success msg ->
                        Context.trace ctx (sprintf "[업데이트 성공] %s" msg)
                        RuntimeTelemetry.runtimeUpdateApplied msg
                        cfg.EventSink |> Option.iter (fun sink ->
                            sink.Publish(RuntimeEvent.RuntimeUpdateApplied(msg, DateTime.UtcNow)))
                    | UpdateResult.ValidationFailed errors ->
                        let errorMsgs = errors |> List.map (fun e -> e.Format()) |> String.concat "; "
                        Context.warning ctx (sprintf "[업데이트 검증 실패] %s" errorMsgs)
                    | UpdateResult.ApplyFailed error ->
                        Context.warning ctx (sprintf "[업데이트 적용 실패] %s" error)
                    | UpdateResult.RolledBack (reason, originalError) ->
                        Context.warning ctx (sprintf "[업데이트 롤백] %s (원인: %s)" reason originalError)
                    | UpdateResult.PartialSuccess (succeeded, failed, errors) ->
                        Context.trace ctx (sprintf "[업데이트 부분 성공] %d개 성공, %d개 실패" succeeded failed)
                        let msg = $"부분 성공: {succeeded}개 성공, {failed}개 실패"
                        RuntimeTelemetry.runtimeUpdateApplied msg
                        cfg.EventSink |> Option.iter (fun sink ->
                            sink.Publish(RuntimeEvent.RuntimeUpdateApplied(msg, DateTime.UtcNow))))

                // Program.Body 업데이트 적용 및 의존성 그래프 재구축
                match mgr.GetProgramBody() with
                | Some newBody ->
                    currentProgramBody <- newBody
                    let newDependencies = DependencyAnalyzer.buildDependencyMap currentProgramBody
                    ctx.Memory.SetDependencyMap newDependencies
                    if cfg.SelectiveMode then
                        ctx.Memory.MarkAllChanged()
                    Context.trace ctx "[업데이트] Program.Body 업데이트 완료"
                    RuntimeTelemetry.runtimeUpdateApplied "Program.Body"
                    cfg.EventSink |> Option.iter (fun sink ->
                        sink.Publish(RuntimeEvent.RuntimeUpdateApplied("Program.Body", DateTime.UtcNow)))
                | None -> ()
            | None -> ()

            swPrepare.Stop()
            let prepareInputsDuration = swPrepare.Elapsed

            // ════════════════════════════════════════════════════════════
            // Stage 2: Execution - 프로그램 본문 실행
            // ════════════════════════════════════════════════════════════
            let swExec = System.Diagnostics.Stopwatch.StartNew()
            let useSelective = cfg.SelectiveMode && not isFirstScan

            if useSelective then
                StmtEvaluator.execScanSelective ctx currentProgramBody
            else
                StmtEvaluator.execScan ctx currentProgramBody

            if isFirstScan then
                isFirstScan <- false

            swExec.Stop()
            let executionDuration = swExec.Elapsed

            // ════════════════════════════════════════════════════════════
            // Stage 3: FlushOutputs - RETAIN 데이터 저장, 변경 플래그 초기화
            // ════════════════════════════════════════════════════════════
            let swFlush = System.Diagnostics.Stopwatch.StartNew()

            // 이전 RETAIN 저장 완료 대기 (경쟁 조건 방지를 위해 동기식)
            lock retainSaveLock (fun () ->
                match pendingRetainSave with
                | Some task ->
                    try
                        if not task.IsCompleted then
                            task.Wait(100) |> ignore
                    with _ -> ()
                    pendingRetainSave <- None
                | None -> ()
            )

            // Fatal 오류 발생 시 RETAIN 저장 생략 (손상된 상태 저장 방지)
            // RuntimeSpec.md §6: Fatal 오류는 즉시 실행 중단
            if not ctx.ErrorLog.HasFatalErrors then
                match retainStorage with
                | Some storage ->
                    let snapshot = ctx.Memory.CreateRetainSnapshot()
                    match storage.Save(snapshot) with
                    | Ok () ->
                        let varCount = snapshot.Variables.Length
                        let fbStaticCount = snapshot.FBStaticData |> List.sumBy (fun fb -> fb.Variables.Length)
                        let totalCount = varCount + fbStaticCount
                        RuntimeTelemetry.retainPersisted totalCount
                        cfg.EventSink |> Option.iter (fun sink ->
                            sink.Publish(RuntimeEvent.RetainPersisted(totalCount, DateTime.UtcNow)))
                    | Error err ->
                        ctx.LogWarning($"RETAIN 저장 실패: {err}")
                | None -> ()
            else
                ctx.LogWarning("스캔 중 Fatal 오류로 인해 RETAIN 저장 생략")

            if cfg.SelectiveMode then
                ctx.Memory.ClearChangeFlags()

            swFlush.Stop()
            let flushDuration = swFlush.Elapsed

            // ════════════════════════════════════════════════════════════
            // Stage 4: Finalize - 오류 확인, 텔레메트리, 릴레이 처리
            // ════════════════════════════════════════════════════════════
            let swFinalize = System.Diagnostics.Stopwatch.StartNew()

            // 릴레이 상태 전환 처리
            ctx.RelayStateManager |> Option.iter (fun manager ->
                manager.ProcessStateChanges())

            // 오래된 경고 주기적 정리 (무제한 메모리 증가 방지)
            if scanIndex % (int64 RuntimeLimits.Current.WarningCleanupIntervalScans) = 0L then
                ctx.ErrorLog.ClearOldWarnings()

            // Fatal 오류 처리 - ScanFailed 발행 및 실행 중단
            if ctx.ErrorLog.HasFatalErrors then
                let fatalErrors = ctx.ErrorLog.GetErrorsBySeverity(RuntimeErrorSeverity.Fatal)
                match fatalErrors |> List.tryHead with
                | Some error ->
                    RuntimeTelemetry.scanFailed scanIndex error.Message
                    cfg.EventSink |> Option.iter (fun sink ->
                        sink.Publish(RuntimeEvent.ScanFailed(scanIndex, error, DateTime.UtcNow)))
                    ctx.State <- ExecutionState.Error error.Message
                    Context.trace ctx (sprintf "[FATAL] 치명적 오류로 엔진 중지: %s" error.Message)
                | None -> ()

            // 스테이지별 메트릭 발행
            RuntimeTelemetry.stageCompleted "PrepareInputs" (int prepareInputsDuration.TotalMilliseconds) scanIndex
            RuntimeTelemetry.stageCompleted "Execution" (int executionDuration.TotalMilliseconds) scanIndex
            RuntimeTelemetry.stageCompleted "FlushOutputs" (int flushDuration.TotalMilliseconds) scanIndex

            swFinalize.Stop()
            let finalizeDuration = swFinalize.Elapsed

            RuntimeTelemetry.stageCompleted "Finalize" (int finalizeDuration.TotalMilliseconds) scanIndex

            // 전체 스캔 소요 시간 계산 (실제 경과 시간, 스테이지 합계 아님)
            sw.Stop()
            let totalDuration = sw.Elapsed
            let totalMs = int totalDuration.TotalMilliseconds

            let timeline = {
                PrepareInputsDuration = prepareInputsDuration
                ExecutionDuration = executionDuration
                FlushDuration = flushDuration
                FinalizeDuration = finalizeDuration
                TotalDuration = totalDuration
                ScanIndex = scanIndex
            }

            // 스테이지별 데드라인 위반 확인 (텔레메트리만, 런타임 영향 없음)
            // RuntimeSpec §2.2: 실시간 사이클 타임 업데이트 반영
            let currentCycleTimeMs = ctx.CycleTime
            let currentThresholds = match cfg.StageThresholds with
                                    | Some thresholds ->
                                        // 사이클 타임 변경 시 커스텀 스테이지 비율 스케일링
                                        match thresholds.Total with
                                        | Some total when total <> currentCycleTimeMs ->
                                            let scaleFactor = float currentCycleTimeMs / float total
                                            let scaleThreshold (optMs: int option) =
                                                optMs |> Option.map (fun ms -> max 1 (int (float ms * scaleFactor)))
                                            Some {
                                                PrepareInputs = scaleThreshold thresholds.PrepareInputs
                                                Execution = scaleThreshold thresholds.Execution
                                                FlushOutputs = scaleThreshold thresholds.FlushOutputs
                                                Finalize = scaleThreshold thresholds.Finalize
                                                Total = Some currentCycleTimeMs
                                            }
                                        | None -> Some thresholds
                                        | Some _ -> Some thresholds
                                    | None -> Some (StageThresholds.defaultForCycleTime currentCycleTimeMs)

            currentThresholds |> Option.iter (fun thresholds ->
                StageDeadlineEnforcer.checkAllStages timeline thresholds cfg.EventSink |> ignore)

            // 스캔 완료 이벤트 발행
            if not ctx.ErrorLog.HasFatalErrors then
                RuntimeTelemetry.scanCompleted scanIndex totalMs currentCycleTimeMs
                cfg.EventSink |> Option.iter (fun sink ->
                    sink.Publish(RuntimeEvent.ScanCompleted(scanIndex, timeline, DateTime.UtcNow)))

            // 전체 스캔 데드라인 확인
            if totalMs > currentCycleTimeMs then
                let deadline = TimeSpan.FromMilliseconds(float currentCycleTimeMs)
                RuntimeTelemetry.scanDeadlineMissed scanIndex totalMs currentCycleTimeMs
                cfg.EventSink |> Option.iter (fun sink ->
                    sink.Publish(RuntimeEvent.ScanDeadlineMissed(scanIndex, totalDuration, deadline, DateTime.UtcNow)))

            // 심각한 오버런에 대한 콘솔 경고
            match cfg.WarnIfOverMs with
            | Some warnThresholdMs when totalMs > warnThresholdMs ->
                Context.warning ctx $"스캔 오버런: {totalMs}ms > {warnThresholdMs}ms (경고 임계값)"
            | _ -> ()

            // 성능 프로파일링을 위한 스캔 메트릭 기록
            PerformanceProfiler.recordScanTime (int64 totalMs) currentCycleTimeMs

            totalMs

        with ex ->
            let error = RuntimeError.fatal $"스캔 실패: {ex.Message}"
                        |> RuntimeError.withScanIndex scanIndex
                        |> RuntimeError.withException ex
            ctx.ErrorLog.Log(error)
            cfg.EventSink |> Option.iter (fun sink ->
                sink.Publish(RuntimeEvent.ScanFailed(scanIndex, error, DateTime.UtcNow)))
            ctx.State <- ExecutionState.Error error.Message
            reraise()

    member private this.RunLoopAsync(token: CancellationToken) : Task =
        // 스캔 루프 전용 스레드 (RuntimeSpec.md:30)
        Task.Factory.StartNew(
            (fun () ->
                try
                    ctx.State <- ExecutionState.Running

                    // Task.Delay 오버헤드로 인한 시간 드리프트 방지를 위한 고정점 타이밍
                    let frequency = System.Diagnostics.Stopwatch.Frequency
                    let ticksPerMs = frequency / 1000L
                    let mutable nextScanTime = ctx.TimeProvider.GetTimestamp()

                    // 모든 비종료 상태에서 루프 유지 (Running/Paused/Breakpoint)
                    let isLoopAlive () =
                        match ctx.State with
                        | ExecutionState.Running | ExecutionState.Paused | ExecutionState.Breakpoint _ -> true
                        | ExecutionState.Stopped | ExecutionState.Error _ -> false

                    while not token.IsCancellationRequested && isLoopAlive() do
                        match ctx.State with
                        | ExecutionState.Paused | ExecutionState.Breakpoint _ ->
                            // 일시정지/중단점에서 CPU 양보 (바쁜 대기 없음)
                            // WaitHandle은 취소 시 예외 없이 즉시 반환
                            token.WaitHandle.WaitOne(10) |> ignore
                        | ExecutionState.Running ->
                            let scanStartTime = ctx.TimeProvider.GetTimestamp()
                            let _ = this.ScanOnce()

                            // 실시간 업데이트를 위해 매 반복마다 사이클 타임 재평가
                            let cycleTimeMs = ctx.CycleTime

                            // 고정밀 틱 계산 (나누기 전에 곱하기)
                            // 0으로 나누기 방지를 위한 1ms 최소값
                            let cycleTimeTicks =
                                if cycleTimeMs <= 0 then
                                    eprintfn "[RUNTIME] 경고: CycleTime이 %dms입니다. 1ms 최소값 사용" cycleTimeMs
                                    frequency / 1000L
                                else
                                    (int64 cycleTimeMs * frequency) / 1000L

                            // 고정점 스케줄링: 다음 스캔 시간 계산
                            nextScanTime <- nextScanTime + cycleTimeTicks
                            let now = ctx.TimeProvider.GetTimestamp()
                            let waitTicks = nextScanTime - now

                            if waitTicks > 0L then
                                let waitMs = int (waitTicks / ticksPerMs)
                                if waitMs > 0 then
                                    // 다음 스캔 또는 취소 대기 (예외 없음)
                                    token.WaitHandle.WaitOne(waitMs) |> ignore
                                else
                                    // 밀리초 미만 여유: 바쁜 스핀 방지를 위해 CPU 양보
                                    token.WaitHandle.WaitOne(0) |> ignore
                            else
                                // 오버런: 다음 타임 슬롯으로 건너뛰기
                                if cycleTimeTicks > 0L then
                                    let missedSlots = (-waitTicks) / cycleTimeTicks + 1L
                                    nextScanTime <- nextScanTime + (missedSlots * cycleTimeTicks)
                                else
                                    nextScanTime <- nextScanTime + (frequency / 1000L)
                        | _ -> ()
                finally
                    // 루프 종료 시 진단 상태 보존 (Error/Paused/Breakpoint)
                    match ctx.State with
                    | ExecutionState.Error _ | ExecutionState.Paused | ExecutionState.Breakpoint _ -> ()
                    | _ -> ctx.State <- ExecutionState.Stopped),
            token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default)

    /// 연속 스캔 루프 시작 (외부 취소 토큰 옵션)
    member this.StartAsync(?externalToken: CancellationToken) : Task =
        match loopTask with
        | Some t when not t.IsCompleted -> Task.CompletedTask
        | _ ->
            let linkedCts =
                match externalToken with
                | Some t -> CancellationTokenSource.CreateLinkedTokenSource t
                | None   -> new CancellationTokenSource()
            cts <- Some linkedCts
            let t = this.RunLoopAsync(linkedCts.Token)
            loopTask <- Some t
            Task.CompletedTask

    /// 스캔 루프 중지 (정상 종료 + 타임아웃 옵션)
    member this.StopAsync(?timeoutMs:int) : Task =
        task {
            // 진단을 위한 오류 상태 보존
            match ctx.State with
            | ExecutionState.Error _ -> ()
            | _ -> ctx.State <- ExecutionState.Stopped
            cts |> Option.iter (fun s -> s.Cancel())

            // 타임아웃을 이용한 루프 종료 대기
            match loopTask with
            | Some t when not t.IsCompleted ->
                let timeout = defaultArg timeoutMs RuntimeLimits.Current.StopTimeoutMs
                let! completed = Task.WhenAny(t, Task.Delay(timeout))
                if obj.ReferenceEquals(completed, t) then
                    try do! t with _ -> ()
                else
                    Context.warning ctx $"StopAsync 타임아웃 ({timeout}ms) 만료, 강제 종료"
            | _ -> ()

            // ────────────────────────────────────────────────────────────
            // RETAIN 데이터 자동 저장 (엔진 종료 시)
            // ────────────────────────────────────────────────────────────
            // Fatal 오류 발생 시 저장 생략 (손상된 상태 저장 방지)
            if not ctx.ErrorLog.HasFatalErrors then
                retainStorage |> Option.iter (fun storage ->
                    let snapshot = ctx.Memory.CreateRetainSnapshot()
                    let varCount = snapshot.Variables.Length
                    let fbStaticCount = snapshot.FBStaticData |> List.sumBy (fun fb -> fb.Variables.Length)
                    let totalCount = varCount + fbStaticCount
                    if totalCount > 0 then
                        match storage.Save(snapshot) with
                        | Ok () ->
                            Context.trace ctx (sprintf "[RETAIN] 저장 완료: %d개 변수, %d개 FB 정적 변수" varCount fbStaticCount)
                        | Error err ->
                            Context.warning ctx (sprintf "[RETAIN] 저장 실패: %s" err)
                    else
                        Context.trace ctx "[RETAIN] 저장할 데이터 없음")
            else
                Context.warning ctx "[RETAIN] Fatal 오류로 인해 종료 시 저장 생략"

            // 대기 중인 RETAIN 저장 완료 대기
            lock retainSaveLock (fun () ->
                match pendingRetainSave with
                | Some task ->
                    try
                        if not (task.Wait(5000)) then
                            eprintfn "[RETAIN] 경고: 비동기 저장이 5초 내에 완료되지 않음"
                    with ex ->
                        eprintfn "[RETAIN] 비동기 저장 대기 오류: %s" ex.Message
                    pendingRetainSave <- None
                | None -> ()
            )

            // 리소스 정리
            cts |> Option.iter (fun s -> s.Dispose())
            cts      <- None
            loopTask <- None
        }

// ============================================================================
// 모듈 API - 편의 래퍼 함수
// ============================================================================

module CpuScan =

    /// 전체 설정으로 스캔 엔진 생성
    let create (program: Statement.Program,
                ctxOpt   : Ev2.Cpu.Runtime.ExecutionContext option,
                cfgOpt   : ScanConfig option,
                updateMgrOpt : RuntimeUpdateManager option,
                retainStorageOpt : IRetainStorage option) =
        new CpuScanEngine(program,
                          defaultArg ctxOpt (Context.create()),
                          cfgOpt,
                          updateMgrOpt,
                          retainStorageOpt)

    /// 기본 설정으로 스캔 엔진 생성
    let createDefault (program: Statement.Program) =
        create (program, None, None, None, None)

    /// 단일 스캔 사이클 실행
    let scanOnce (engine: CpuScanEngine) = engine.ScanOnce()

    /// 취소 토큰 옵션으로 연속 스캔 루프 시작
    let start (engine: CpuScanEngine, tokenOpt: CancellationToken option) =
        match tokenOpt with
        | Some t -> engine.StartAsync(externalToken = t)
        | None   -> engine.StartAsync()

    /// 취소 토큰 없이 연속 스캔 루프 시작
    let startNoToken (engine: CpuScanEngine) =
        engine.StartAsync()

    /// 스캔 루프 정상 종료
    let stopAsync (engine: CpuScanEngine) =
        engine.StopAsync()
