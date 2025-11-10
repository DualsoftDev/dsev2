namespace Ev2.Cpu.Runtime

open System
open System.Collections.Concurrent
open Ev2.Cpu.Core

// ─────────────────────────────────────────────────────────────────────
// Execution Context Types & Management
// ─────────────────────────────────────────────────────────────────────

/// <summary>PLC 프로그램 실행 상태</summary>
/// <remarks>
/// 실행 컨텍스트의 현재 상태를 나타냅니다.
/// - Running: 정상 실행 중
/// - Paused: 일시 정지 상태
/// - Stopped: 중단 상태
/// - Error: 오류 발생으로 중단
/// - Breakpoint: 브레이크포인트에서 중단
/// </remarks>
[<RequireQualifiedAccess>]
type ExecutionState =
    /// 정상 실행 중
    | Running
    /// 일시 정지 상태
    | Paused
    /// 중단 상태
    | Stopped
    /// 오류 발생으로 중단 (오류 메시지 포함)
    | Error of message: string
    /// 브레이크포인트에서 중단 (위치 정보 포함)
    | Breakpoint of location: string

/// <summary>PLC 프로그램 실행 컨텍스트 (스레드 안전)</summary>
/// <remarks>
/// PLC 런타임 실행에 필요한 모든 상태를 관리합니다.
/// - Memory: 변수 저장소
/// - Timers/Counters: TON/TOF/CTU/CTD 상태
/// - State: 실행 상태 (Running/Paused/Stopped/Error/Breakpoint)
/// - CycleTime: 스캔 주기 (밀리초)
/// - TimeProvider: 시간 추상화 (GAP-007)
/// - Trace: 디버그 로그 큐
/// - Breakpoints/Watchlist: 디버깅 지원
/// - ErrorCount/WarningCount: 오류/경고 통계
/// 모든 컬렉션은 ConcurrentDictionary/Queue를 사용하여 스레드 안전성 보장
/// </remarks>
type ExecutionContext = {
    /// 변수 메모리 (thread-safe)
    Memory       : Memory
    /// 타이머 상태 저장소 (TON/TOF)
    Timers       : ConcurrentDictionary<string, TimerState>
    /// 카운터 상태 저장소 (CTU/CTD)
    Counters     : ConcurrentDictionary<string, CounterState>

    // CRITICAL FIX (DEFECT-CRIT-8): Thread-safe mutable state access
    // Note: F# record types don't support [<VolatileField>] attribute
    // Thread safety achieved through:
    // 1. State writes only from scan loop thread (single writer)
    // 2. Readers (diagnostics, UI) accept eventual consistency
    // 3. Critical sections use explicit locking where atomic updates needed
    // For full volatile semantics, use System.Threading.Volatile.Read/Write at call sites

    /// 현재 실행 상태 (mutable, single-writer from scan loop)
    mutable State      : ExecutionState
    /// 스캔 주기 (밀리초, mutable, updated from scan loop)
    mutable CycleTime  : int
    /// 마지막 스캔 시각 (UTC, 모니터링용, updated from scan loop)
    mutable LastCycle  : DateTime
    /// 마지막 스캔 타임스탬프 (고해상도 ticks, updated from scan loop)
    mutable LastCycleTicks : int64

    /// 시간 제공자 (GAP-007: 테스트 가능성 및 시뮬레이션 지원)
    TimeProvider : ITimeProvider
    /// 트레이스 로그 큐 (thread-safe)
    Trace        : ConcurrentQueue<string>
    /// 브레이크포인트 목록 (location -> marker)
    Breakpoints  : ConcurrentDictionary<string, byte>
    /// 워치 변수 목록 (name -> marker)
    Watchlist    : ConcurrentDictionary<string, byte>
    /// 누적 오류 개수 (legacy - use ErrorLog instead)
    mutable ErrorCount   : int
    /// 누적 경고 개수 (legacy - use ErrorLog instead)
    mutable WarningCount : int
    /// 구조화된 오류 로그 (RuntimeSpec.md:89 - GAP-001 fix)
    ErrorLog     : RuntimeErrorLog
    /// Relay lifecycle manager (GAP-009 - RuntimeSpec.md:41,56)
    mutable RelayStateManager : RelayStateManager option
    /// Loop context manager (MEDIUM FIX: per-context instead of global singleton)
    LoopContext : LoopContext
}

/// <summary>타이머 상태 정보 (읽기 전용 스냅샷)</summary>
/// <remarks>
/// 타이머의 현재 상태를 나타내는 불변 정보입니다.
/// - Preset: 설정 시간 (밀리초)
/// - Accumulated: 경과 시간 (밀리초)
/// - Done: 타이머 완료 플래그
/// - Timing: 타이머 동작 중 플래그
/// </remarks>
type TimerInfo = {
    /// 설정 시간 (밀리초)
    Preset: int
    /// 현재 경과 시간 (밀리초)
    Accumulated: int
    /// 타이머 완료 여부 (DN 비트)
    Done: bool
    /// 타이머 동작 중 여부 (TT 비트)
    Timing: bool
}

/// <summary>카운터 상태 정보 (읽기 전용 스냅샷)</summary>
/// <remarks>
/// 카운터의 현재 상태를 나타내는 불변 정보입니다.
/// - Preset: 목표 카운트
/// - Count: 현재 카운트 값
/// - Done: 카운터 완료 플래그
/// - Up: 증가/감소 방향 (true: CTU, false: CTD)
/// </remarks>
type CounterInfo = {
    /// 목표 카운트 값
    Preset: int
    /// 현재 카운트 값
    Count: int
    /// 카운터 완료 여부 (DN 비트)
    Done: bool
    /// 증가 카운터(true) / 감소 카운터(false)
    Up: bool
}

/// <summary>실행 컨텍스트 관리 모듈</summary>
/// <remarks>
/// ExecutionContext 생성, 타이머/카운터 업데이트, 디버깅 등을 제공합니다.
/// - create: 새 컨텍스트 생성
/// - updateTimerOn/Off: TON/TOF 타이머 업데이트
/// - updateCounterUp/Down: CTU/CTD 카운터 업데이트
/// - trace/warning/error: 로그 및 오류 기록
/// - checkBreakpoint: 브레이크포인트 확인
/// - reset: 컨텍스트 초기화
/// - getStatus: 현재 상태 조회
/// </remarks>
module Context =
    /// <summary>기본 PLC 사이클 타임 (밀리초)</summary>
    [<Literal>]
    let DefaultCycleTimeMs = 10

    /// <summary>트레이스 큐 크기 제한</summary>
    /// <param name="trace">트레이스 큐</param>
    /// <remarks>큐 크기가 RuntimeLimits.TraceCapacity를 초과하면 오래된 항목 제거</remarks>
    let private trimTrace (trace: ConcurrentQueue<string>) =
        // MINOR FIX: Use configurable RuntimeLimits.TraceCapacity instead of hardcoded 1000
        let capacity = RuntimeLimits.Current.TraceCapacity
        let mutable ignored = Unchecked.defaultof<string>
        while trace.Count > capacity && trace.TryDequeue(&ignored) do ()

    /// <summary>새 실행 컨텍스트 생성</summary>
    /// <returns>초기화된 ExecutionContext (기본 CycleTime = 10ms)</returns>
    /// <remarks>
    /// 모든 컬렉션이 빈 상태로 초기화되며, State는 Stopped입니다.
    /// RelayStateManager is auto-initialized (DEFECT-004 fix).
    /// </remarks>
    let create () : ExecutionContext =
        let timeProvider = SystemTimeProvider() :> ITimeProvider
        { Memory       = Memory()
          Timers       = ConcurrentDictionary<string, TimerState>(StringComparer.Ordinal)
          Counters     = ConcurrentDictionary<string, CounterState>(StringComparer.Ordinal)
          State        = ExecutionState.Stopped
          CycleTime    = DefaultCycleTimeMs
          LastCycle    = DateTime.UtcNow
          LastCycleTicks = timeProvider.GetTimestamp()
          TimeProvider = timeProvider
          Trace        = ConcurrentQueue<string>()
          Breakpoints  = ConcurrentDictionary<string, byte>(StringComparer.Ordinal)
          Watchlist    = ConcurrentDictionary<string, byte>(StringComparer.Ordinal)
          ErrorCount   = 0
          WarningCount = 0
          ErrorLog     = RuntimeErrorLog()
          // MAJOR FIX: Don't auto-initialize with None sink - let engine create with proper EventSink
          RelayStateManager = None
          LoopContext = LoopContext(timeProvider) }  // MAJOR FIX: Pass timeProvider for monotonic clock guarantee

    /// <summary>타이머 상태 가져오기 (없으면 생성)</summary>
    /// <param name="ctx">실행 컨텍스트</param>
    /// <param name="name">타이머 이름</param>
    /// <param name="presetMs">설정 시간 (밀리초)</param>
    /// <param name="nowTicks">현재 타임스탬프</param>
    /// <returns>TimerState (기존 또는 새로 생성)</returns>
    let private getTimer (ctx: ExecutionContext) (name: string) (presetMs: int) nowTicks : TimerState =
        ctx.Timers.GetOrAdd(name, fun _ -> TimerState.create presetMs nowTicks)

    /// <summary>TON 타이머 로직 코어 (스레드 안전하지 않음, lock 필요)</summary>
    /// <param name="timer">타이머 상태</param>
    /// <param name="enable">입력 신호 (RungIn)</param>
    /// <param name="presetMs">설정 시간 (밀리초)</param>
    /// <param name="nowTicks">현재 타임스탬프</param>
    /// <returns>Done 비트 (타이머 완료 여부)</returns>
    /// <remarks>
    /// On-Delay 타이머 로직:
    /// - enable이 true이면 시간 누적, Preset 도달 시 Done = true
    /// - enable이 false이면 즉시 리셋
    /// </remarks>
    let private updateTimerOnCore (timer: TimerState) (enable: bool) presetMs nowTicks =
        timer.Preset <- max 0 presetMs
        if enable then
            if not timer.Timing then
                timer.Timing <- true
                timer.Done <- (timer.Preset = 0)
                timer.Accumulated <- if timer.Done then timer.Preset else 0
                timer.LastTimestamp <- nowTicks
            else
                let elapsed = Timebase.elapsedMilliseconds timer.LastTimestamp nowTicks
                // Check overflow before adding, and check preset shrink regardless of elapsed
                if elapsed > 0 && not timer.Done then
                    // Prevent overflow: clamp addition to Int32.MaxValue
                    let newAccum =
                        if timer.Accumulated > Int32.MaxValue - elapsed then
                            timer.Preset  // Overflow would occur, just set to preset (done)
                        else
                            min timer.Preset (timer.Accumulated + elapsed)
                    timer.Accumulated <- newAccum
                    timer.LastTimestamp <- nowTicks
                // Always check if preset shrink makes timer done (even if elapsed = 0)
                timer.Done <- timer.Accumulated >= timer.Preset
        else
            timer.Timing <- false
            timer.Done <- false
            timer.Accumulated <- 0
            timer.LastTimestamp <- nowTicks
        timer.Done

    /// <summary>TON 타이머 업데이트 (타임스탬프 지정)</summary>
    /// <param name="ctx">실행 컨텍스트</param>
    /// <param name="name">타이머 이름</param>
    /// <param name="enable">입력 신호 (RungIn)</param>
    /// <param name="presetMs">설정 시간 (밀리초)</param>
    /// <param name="nowTicks">현재 타임스탬프 (ticks)</param>
    /// <returns>Done 비트 (타이머 완료 여부)</returns>
    let updateTimerOnWithTimestamp (ctx: ExecutionContext) (name: string) (enable: bool) (presetMs: int) (nowTicks: int64) : bool =
        let timer = getTimer ctx name presetMs nowTicks
        lock timer.Lock (fun () -> updateTimerOnCore timer enable presetMs nowTicks)

    /// <summary>TON (On-Delay) 타이머 업데이트</summary>
    /// <param name="ctx">실행 컨텍스트</param>
    /// <param name="name">타이머 이름</param>
    /// <param name="enable">입력 신호 (RungIn)</param>
    /// <param name="presetMs">설정 시간 (밀리초)</param>
    /// <returns>Done 비트 (타이머 완료 여부)</returns>
    /// <remarks>
    /// IEC 61131-3 표준 TON 타이머:
    /// - 입력이 true로 전환되면 시간 측정 시작
    /// - Preset 시간 경과 후 Done = true
    /// - 입력이 false가 되면 즉시 리셋
    /// </remarks>
    let updateTimerOn (ctx: ExecutionContext) (name: string) (enable: bool) (presetMs: int) : bool =
        updateTimerOnWithTimestamp ctx name enable presetMs (ctx.TimeProvider.GetTimestamp())

    /// <summary>TOF 타이머 로직 코어 (스레드 안전하지 않음, lock 필요)</summary>
    /// <param name="timer">타이머 상태</param>
    /// <param name="enable">입력 신호 (RungIn)</param>
    /// <param name="presetMs">설정 시간 (밀리초)</param>
    /// <param name="nowTicks">현재 타임스탬프</param>
    /// <returns>Done 비트</returns>
    /// <remarks>
    /// Off-Delay 타이머 로직:
    /// - enable이 true이면 즉시 Done = true
    /// - enable이 false로 전환되면 Preset 시간 후 Done = false
    /// </remarks>
    let private updateTimerOffCore (timer: TimerState) (enable: bool) presetMs nowTicks =
        timer.Preset <- max 0 presetMs
        if enable then
            timer.Done <- true
            timer.Timing <- false
            timer.Accumulated <- 0
            timer.LastTimestamp <- nowTicks
        else
            if timer.Done then
                if not timer.Timing then
                    timer.Timing <- true
                    timer.Accumulated <- 0
                    timer.LastTimestamp <- nowTicks
                else
                    let elapsed = Timebase.elapsedMilliseconds timer.LastTimestamp nowTicks
                    if elapsed > 0 then
                        // Prevent overflow: clamp addition to Int32.MaxValue
                        let newAccum =
                            if timer.Accumulated > Int32.MaxValue - elapsed then
                                timer.Preset  // Overflow would occur, just set to preset (done)
                            else
                                min timer.Preset (timer.Accumulated + elapsed)
                        timer.Accumulated <- newAccum
                        timer.LastTimestamp <- nowTicks
                        if timer.Accumulated >= timer.Preset then
                            timer.Done <- false
                            timer.Timing <- false
                            timer.Accumulated <- timer.Preset
        timer.Done

    /// <summary>TOF 타이머 업데이트 (타임스탬프 지정)</summary>
    /// <param name="ctx">실행 컨텍스트</param>
    /// <param name="name">타이머 이름</param>
    /// <param name="enable">입력 신호 (RungIn)</param>
    /// <param name="presetMs">설정 시간 (밀리초)</param>
    /// <param name="nowTicks">현재 타임스탬프 (ticks)</param>
    /// <returns>Done 비트</returns>
    let updateTimerOffWithTimestamp (ctx: ExecutionContext) (name: string) (enable: bool) (presetMs: int) (nowTicks: int64) : bool =
        let timer = getTimer ctx name presetMs nowTicks
        lock timer.Lock (fun () -> updateTimerOffCore timer enable presetMs nowTicks)

    /// <summary>TOF (Off-Delay) 타이머 업데이트</summary>
    /// <param name="ctx">실행 컨텍스트</param>
    /// <param name="name">타이머 이름</param>
    /// <param name="enable">입력 신호 (RungIn)</param>
    /// <param name="presetMs">설정 시간 (밀리초)</param>
    /// <returns>Done 비트</returns>
    /// <remarks>
    /// IEC 61131-3 표준 TOF 타이머:
    /// - 입력이 true이면 즉시 Done = true
    /// - 입력이 false로 전환되면 Preset 시간 후 Done = false
    /// - 출력 신호 지연 차단에 사용
    /// </remarks>
    let updateTimerOff (ctx: ExecutionContext) (name: string) (enable: bool) (presetMs: int) : bool =
        updateTimerOffWithTimestamp ctx name enable presetMs (ctx.TimeProvider.GetTimestamp())

    /// <summary>카운터 상태 가져오기 (없으면 생성)</summary>
    /// <param name="ctx">실행 컨텍스트</param>
    /// <param name="name">카운터 이름</param>
    /// <param name="preset">목표 카운트</param>
    /// <returns>CounterState (기존 또는 새로 생성)</returns>
    let private getCounter (ctx: ExecutionContext) (name: string) (preset: int) : CounterState =
        ctx.Counters.GetOrAdd(name, fun _ -> CounterState.create preset)

    /// <summary>CTU (Count Up) 카운터 업데이트</summary>
    /// <param name="ctx">실행 컨텍스트</param>
    /// <param name="name">카운터 이름</param>
    /// <param name="count">카운트 입력 신호 (rising edge 감지)</param>
    /// <param name="reset">리셋 신호</param>
    /// <param name="preset">목표 카운트</param>
    /// <returns>현재 카운트 값</returns>
    /// <remarks>
    /// IEC 61131-3 표준 CTU 카운터:
    /// - count가 false에서 true로 전환될 때마다 +1
    /// - Count가 Preset에 도달하면 Done = true
    /// - reset이 true이면 Count = 0으로 초기화
    /// </remarks>
    let updateCounterUp (ctx: ExecutionContext) (name: string) (count: bool) (reset: bool) (preset: int) : int =
        let counter = getCounter ctx name preset
        lock counter.Lock (fun () ->
            counter.Preset <- max 0 preset
            counter.Up <- true
            if reset then
                counter.Count <- 0
                counter.Done <- false
                counter.LastCountInput <- false
            else
                if count && not counter.LastCountInput then
                    counter.Count <- counter.Count + 1
                // IEC 61131-3: Preset = 0 should assert Done immediately
                counter.Done <- (counter.Count >= counter.Preset)
                counter.LastCountInput <- count
            counter.Count)

    /// <summary>CTD (Count Down) 카운터 업데이트</summary>
    /// <param name="ctx">실행 컨텍스트</param>
    /// <param name="name">카운터 이름</param>
    /// <param name="count">카운트 입력 신호 (rising edge 감지)</param>
    /// <param name="load">로드 신호 (Preset 값으로 초기화)</param>
    /// <param name="preset">초기 카운트 값</param>
    /// <returns>현재 카운트 값</returns>
    /// <remarks>
    /// IEC 61131-3 표준 CTD 카운터:
    /// - count가 false에서 true로 전환될 때마다 -1
    /// - Count가 0에 도달하면 Done = true
    /// - load가 true이면 Count = Preset으로 초기화
    /// </remarks>
    let updateCounterDown (ctx: ExecutionContext) (name: string) (count: bool) (load: bool) (preset: int) : int =
        let counter = getCounter ctx name preset
        lock counter.Lock (fun () ->
            counter.Preset <- max 0 preset
            counter.Up <- false
            if load then
                counter.Count <- counter.Preset
                counter.Done <- (counter.Count = 0)
                counter.LastCountInput <- false
            else
                if count && not counter.LastCountInput then
                    counter.Count <- max 0 (counter.Count - 1)
                    counter.Done <- counter.Count = 0
                counter.LastCountInput <- count
            counter.Count)

    /// <summary>트레이스 로그 추가</summary>
    /// <param name="ctx">실행 컨텍스트</param>
    /// <param name="msg">로그 메시지</param>
    /// <remarks>
    /// 타임스탬프와 함께 트레이스 큐에 메시지를 추가합니다.
    /// 큐 크기 제한(1000)을 초과하면 오래된 항목 자동 제거
    /// </remarks>
    let trace (ctx: ExecutionContext) (msg: string) =
        let entry = sprintf "[%s] %s" (DateTime.UtcNow.ToString("HH:mm:ss.fff")) msg
        ctx.Trace.Enqueue(entry)
        trimTrace ctx.Trace

    /// <summary>경고 메시지 기록</summary>
    /// <param name="ctx">실행 컨텍스트</param>
    /// <param name="msg">경고 메시지</param>
    /// <remarks>WarningCount를 증가시키고 트레이스에 "WARNING:" 접두사로 기록</remarks>
    let warning (ctx: ExecutionContext) (msg: string) =
        ctx.WarningCount <- ctx.WarningCount + 1
        trace ctx ("WARNING: " + msg)

    /// <summary>오류 메시지 기록 및 실행 중단</summary>
    /// <param name="ctx">실행 컨텍스트</param>
    /// <param name="msg">오류 메시지</param>
    /// <remarks>
    /// ErrorCount를 증가시키고, 트레이스에 기록하며, State를 Error로 변경합니다.
    /// </remarks>
    let error (ctx: ExecutionContext) (msg: string) =
        ctx.ErrorCount <- ctx.ErrorCount + 1
        trace ctx ("ERROR: " + msg)
        ctx.State <- ExecutionState.Error msg

    /// <summary>브레이크포인트 확인</summary>
    /// <param name="ctx">실행 컨텍스트</param>
    /// <param name="location">문장 위치</param>
    /// <returns>브레이크포인트가 있으면 true (State도 Breakpoint로 변경)</returns>
    /// <remarks>해당 위치에 브레이크포인트가 설정되어 있으면 실행을 중단합니다.</remarks>
    let checkBreakpoint (ctx: ExecutionContext) (location: string) =
        if ctx.Breakpoints.ContainsKey(location) then
            ctx.State <- ExecutionState.Breakpoint location
            true
        else false

    /// <summary>실행 컨텍스트 완전 초기화</summary>
    /// <param name="ctx">실행 컨텍스트</param>
    /// <remarks>
    /// 메모리, 타이머, 카운터, 트레이스를 모두 지우고 초기 상태로 복원합니다.
    /// - Memory, Timers, Counters, Trace 모두 Clear
    /// - State를 Stopped로 설정
    /// - ErrorCount/WarningCount를 0으로 리셋
    /// </remarks>
    let reset (ctx: ExecutionContext) =
        ctx.Memory.Clear()
        ctx.Timers.Clear()
        ctx.Counters.Clear()
        ctx.State        <- ExecutionState.Stopped
        ctx.Trace.Clear()
        ctx.ErrorCount   <- 0
        ctx.WarningCount <- 0
        ctx.LastCycle    <- DateTime.UtcNow
        ctx.LastCycleTicks <- ctx.TimeProvider.GetTimestamp()

    /// <summary>현재 실행 상태 조회</summary>
    /// <param name="ctx">실행 컨텍스트</param>
    /// <returns>익명 레코드로 모든 상태 정보 반환</returns>
    /// <remarks>
    /// 반환 정보:
    /// - State, CycleTime, ScanCount
    /// - Timers/Counters 개수
    /// - Errors/Warnings 개수
    /// - MemoryStats, TraceCount
    /// - LastCycle, UptimeSec
    /// </remarks>
    let getStatus (ctx: ExecutionContext) =
        {| State       = ctx.State
           CycleTime   = ctx.CycleTime
           ScanCount   = ctx.Memory.ScanCount
           Timers      = ctx.Timers.Count
           Counters    = ctx.Counters.Count
           Errors      = ctx.ErrorCount
           Warnings    = ctx.WarningCount
           MemoryStats = ctx.Memory.Stats()
           TraceCount  = ctx.Trace.Count
           LastCycle   = ctx.LastCycle
           UptimeSec   = (DateTime.UtcNow - ctx.LastCycle).TotalSeconds |}

    /// <summary>타이머 상태 정보 조회</summary>
    /// <param name="ctx">실행 컨텍스트</param>
    /// <param name="name">타이머 이름</param>
    /// <returns>Some TimerInfo (타이머가 존재하면) 또는 None</returns>
    /// <remarks>
    /// 스레드 안전하게 타이머 상태의 스냅샷을 반환합니다.
    /// Lock을 사용하여 일관성 보장
    /// </remarks>
    let tryGetTimerInfo (ctx: ExecutionContext) name : TimerInfo option =
        match ctx.Timers.TryGetValue name with
        | true, timer ->
            lock timer.Lock (fun () ->
                let info : TimerInfo =
                    { Preset = timer.Preset
                      Accumulated = timer.Accumulated
                      Done = timer.Done
                      Timing = timer.Timing }
                Some info)
        | _ -> None

    /// <summary>카운터 상태 정보 조회</summary>
    /// <param name="ctx">실행 컨텍스트</param>
    /// <param name="name">카운터 이름</param>
    /// <returns>Some CounterInfo (카운터가 존재하면) 또는 None</returns>
    /// <remarks>
    /// 스레드 안전하게 카운터 상태의 스냅샷을 반환합니다.
    /// Lock을 사용하여 일관성 보장
    /// </remarks>
    let tryGetCounterInfo (ctx: ExecutionContext) name : CounterInfo option =
        match ctx.Counters.TryGetValue name with
        | true, counter ->
            lock counter.Lock (fun () ->
                let info : CounterInfo =
                    { Preset = counter.Preset
                      Count = counter.Count
                      Done = counter.Done
                      Up = counter.Up }
                Some info)
        | _ -> None

// ═════════════════════════════════════════════════════════════════════════════
// ExecutionContext Extension Methods for Structured Error Handling (GAP-001)
// ═════════════════════════════════════════════════════════════════════════════

/// Extension methods for ExecutionContext to support structured errors and transactions
type ExecutionContext with
    /// Current scan cycle index (automatically reads from Memory.ScanCount)
    /// MEDIUM FIX: Changed to int64 to prevent overflow (RuntimeSpec.md:26)
    member this.ScanIndex : int64 = this.Memory.ScanCount

    /// Create memory snapshot for transaction rollback (RuntimeSpec.md:104 - GAP-002)
    /// HIGH FIX: Include timers/counters (RuntimeSpec.md:113-118)
    member this.CreateSnapshot() : MemorySnapshot =
        let memSnapshot = this.Memory.CreateSnapshot()
        // Capture timer states (thread-safe with locks)
        let timerStates =
            this.Timers
            |> Seq.map (fun (KeyValue(name, timer)) ->
                lock timer.Lock (fun () ->
                    let snapshot : TimerStateSnapshot = {
                        Preset = timer.Preset
                        Accumulated = timer.Accumulated
                        Done = timer.Done
                        Timing = timer.Timing
                        LastTimestamp = timer.LastTimestamp
                    }
                    (name, snapshot)))
            |> Map.ofSeq
        // Capture counter states (thread-safe with locks)
        let counterStates =
            this.Counters
            |> Seq.map (fun (KeyValue(name, counter)) ->
                lock counter.Lock (fun () ->
                    let snapshot : CounterStateSnapshot = {
                        Preset = counter.Preset
                        Count = counter.Count
                        Done = counter.Done
                        Up = counter.Up
                        LastCountInput = counter.LastCountInput
                    }
                    (name, snapshot)))
            |> Map.ofSeq
        {
            Timestamp = memSnapshot.Timestamp
            Variables = memSnapshot.Variables
            VariableKeys = memSnapshot.VariableKeys
            ScanCount = memSnapshot.ScanCount
            Timers = timerStates
            Counters = counterStates
            VariableAreas = memSnapshot.VariableAreas
            VariableRetainFlags = memSnapshot.VariableRetainFlags
        }

    /// Rollback to a previous memory snapshot (RuntimeSpec.md:104 - GAP-002)
    /// HIGH FIX: Restore timers/counters (RuntimeSpec.md:113-118)
    /// MEDIUM FIX: Restore scan counter (RuntimeSpec.md:118)
    /// CRITICAL FIX: Remove phantom timers/counters created during failed transaction
    member this.Rollback(snapshot: MemorySnapshot) : unit =
        this.Memory.Rollback(snapshot)
        // MEDIUM FIX: Restore scan counter
        this.Memory.RestoreScanCount(snapshot.ScanCount)

        // CRITICAL FIX: Remove timers created during transaction (not in snapshot)
        let timerNames = this.Timers.Keys |> Seq.toList
        for timerName in timerNames do
            if not (snapshot.Timers.ContainsKey(timerName)) then
                this.Timers.TryRemove(timerName) |> ignore

        // Restore timer states (thread-safe with locks)
        for KeyValue(name, timerSnapshot) in snapshot.Timers do
            match this.Timers.TryGetValue(name) with
            | true, timer ->
                lock timer.Lock (fun () ->
                    timer.Preset <- timerSnapshot.Preset
                    timer.Accumulated <- timerSnapshot.Accumulated
                    timer.Done <- timerSnapshot.Done
                    timer.Timing <- timerSnapshot.Timing
                    timer.LastTimestamp <- timerSnapshot.LastTimestamp)
            | false, _ ->
                // HIGH FIX (DEFECT-019-8): Recreate deleted timer from snapshot
                // Previous code skipped it, causing timer to vanish after rollback (RuntimeSpec.md:113-118)
                let recreatedTimer = {
                    Preset = timerSnapshot.Preset
                    Accumulated = timerSnapshot.Accumulated
                    Done = timerSnapshot.Done
                    Timing = timerSnapshot.Timing
                    LastTimestamp = timerSnapshot.LastTimestamp
                    Lock = obj ()
                }
                this.Timers.[name] <- recreatedTimer

        // CRITICAL FIX: Remove counters created during transaction (not in snapshot)
        let counterNames = this.Counters.Keys |> Seq.toList
        for counterName in counterNames do
            if not (snapshot.Counters.ContainsKey(counterName)) then
                this.Counters.TryRemove(counterName) |> ignore

        // Restore counter states (thread-safe with locks)
        for KeyValue(name, counterSnapshot) in snapshot.Counters do
            match this.Counters.TryGetValue(name) with
            | true, counter ->
                lock counter.Lock (fun () ->
                    counter.Preset <- counterSnapshot.Preset
                    counter.Count <- counterSnapshot.Count
                    counter.Done <- counterSnapshot.Done
                    counter.Up <- counterSnapshot.Up
                    counter.LastCountInput <- counterSnapshot.LastCountInput)
            | false, _ ->
                // HIGH FIX (DEFECT-019-9): Recreate deleted counter from snapshot
                // Previous code skipped it, causing counter to vanish after rollback (RuntimeSpec.md:113-118)
                let recreatedCounter = {
                    Preset = counterSnapshot.Preset
                    Count = counterSnapshot.Count
                    Done = counterSnapshot.Done
                    Up = counterSnapshot.Up
                    LastCountInput = counterSnapshot.LastCountInput
                    Lock = obj ()
                }
                this.Counters.[name] <- recreatedCounter

    /// Classify exception severity (RuntimeSpec.md:113-118 - CRITICAL FIX)
    member this.ClassifyException(ex: exn) : RuntimeErrorSeverity =
        match ex with
        // System-level fatal exceptions
        | :? System.OutOfMemoryException
        | :? System.StackOverflowException
        | :? System.AccessViolationException -> RuntimeErrorSeverity.Fatal
        // Recoverable runtime exceptions
        | :? System.ArgumentException
        | :? System.InvalidOperationException
        | :? System.DivideByZeroException
        | :? System.IndexOutOfRangeException
        | :? System.InvalidCastException
        | :? System.NullReferenceException
        | :? System.FormatException
        | :? System.OverflowException -> RuntimeErrorSeverity.Recoverable
        // Default to recoverable for unknown exceptions
        | _ -> RuntimeErrorSeverity.Recoverable

    /// Execute action with transaction semantics (RuntimeSpec.md:113-118 - CRITICAL FIX)
    member this.WithTransaction(action: unit -> unit) : Result<unit, RuntimeError> =
        let memSnapshot = this.CreateSnapshot()
        let errorLogSnapshot = this.ErrorLog.CreateSnapshot()
        try
            action()
            if this.ErrorLog.HasFatalErrors then
                // CRITICAL FIX: Capture fatal errors BEFORE restoring snapshot
                let fatalErrors = this.ErrorLog.GetErrorsBySeverity(RuntimeErrorSeverity.Fatal)
                // Fatal error - rollback memory and error log
                this.Rollback(memSnapshot)
                this.ErrorLog.RestoreSnapshot(errorLogSnapshot)
                // CRITICAL FIX (DEFECT-020-2): Re-log ALL fatal errors after rollback
                // Previous code only re-logged first error (List.tryHead), losing root-cause detail
                // Diagnostics need complete fatal error list for troubleshooting
                for error in fatalErrors do
                    this.ErrorLog.Log(error)  // Preserve all fatal entries for post-scan check
                // Return first error for Result type (others are in ErrorLog)
                match List.tryHead fatalErrors with
                | Some error -> Error error
                | None -> Ok ()
            else
                Ok ()
        with ex ->
            // CRITICAL FIX: Classify exception and apply severity-based policy
            let severity = this.ClassifyException(ex)
            this.Rollback(memSnapshot)
            this.ErrorLog.RestoreSnapshot(errorLogSnapshot)

            match severity with
            | RuntimeErrorSeverity.Fatal ->
                let error = RuntimeError.fatal $"Transaction failed: {ex.Message}"
                            |> RuntimeError.withScanIndex this.ScanIndex
                            |> RuntimeError.withException ex
                this.ErrorLog.Log(error)
                RuntimeTelemetry.fatalError error.Message
                // CRITICAL FIX: Set ExecutionState.Error instead of Stopped for fatal errors
                this.State <- ExecutionState.Error error.Message
                Error error
            | RuntimeErrorSeverity.Recoverable ->
                let error = RuntimeError.recoverable $"Transaction failed (recoverable): {ex.Message}"
                            |> RuntimeError.withScanIndex this.ScanIndex
                            |> RuntimeError.withException ex
                this.ErrorLog.Log(error)
                RuntimeTelemetry.recoverableError error.Message
                Error error
            | RuntimeErrorSeverity.Warning ->
                let error = RuntimeError.warning $"Transaction warning: {ex.Message}"
                            |> RuntimeError.withScanIndex this.ScanIndex
                            |> RuntimeError.withException ex
                this.ErrorLog.Log(error)
                Ok ()  // Continue scan

    /// Legacy error logging (for backward compatibility)
    member ctx.LogSimpleError(message: string) =
        let error = RuntimeError.fatal message
                    |> RuntimeError.withScanIndex ctx.ScanIndex
        ctx.ErrorLog.Log(error)

    /// Log a fatal error
    member ctx.LogFatal(message: string, ?fbInstance: string, ?astNode: DsStmt, ?ex: exn) =
        let error =
            RuntimeError.fatal message
            |> RuntimeError.withScanIndex ctx.ScanIndex
            |> (match fbInstance with Some fb -> RuntimeError.withFBInstance fb | None -> id)
            |> (match astNode with Some node -> RuntimeError.withAstNode node | None -> id)
            |> (match ex with Some e -> RuntimeError.withException e | None -> id)
        ctx.ErrorLog.Log(error)
        // MEDIUM FIX: Emit fatal error telemetry to ETW/APM
        RuntimeTelemetry.fatalError message

    /// Log a recoverable error
    member ctx.LogRecoverable(message: string, ?fbInstance: string, ?astNode: DsStmt, ?ex: exn) =
        let error =
            RuntimeError.recoverable message
            |> RuntimeError.withScanIndex ctx.ScanIndex
            |> (match fbInstance with Some fb -> RuntimeError.withFBInstance fb | None -> id)
            |> (match astNode with Some node -> RuntimeError.withAstNode node | None -> id)
            |> (match ex with Some e -> RuntimeError.withException e | None -> id)
        ctx.ErrorLog.Log(error)
        // MEDIUM FIX: Emit recoverable error telemetry to ETW/APM
        RuntimeTelemetry.recoverableError message

    /// Log a warning
    member ctx.LogWarning(message: string, ?fbInstance: string, ?astNode: DsStmt) =
        let error =
            RuntimeError.warning message
            |> RuntimeError.withScanIndex ctx.ScanIndex
            |> (match fbInstance with Some fb -> RuntimeError.withFBInstance fb | None -> id)
            |> (match astNode with Some node -> RuntimeError.withAstNode node | None -> id)
        ctx.ErrorLog.Log(error)

    /// Get all logged errors
    member ctx.GetErrors() : RuntimeError list =
        ctx.ErrorLog.GetErrors()

    /// Check if scan should stop due to fatal errors
    member ctx.ShouldStopScan() : bool =
        ctx.ErrorLog.HasFatalErrors
