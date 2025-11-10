namespace Ev2.Cpu.Runtime

open System
open System.Collections.Generic
open Ev2.Cpu.Core
open Ev2.Cpu.Core.Common

// ═════════════════════════════════════════════════════════════════════
// 메모리 시스템 상수
// ═════════════════════════════════════════════════════════════════════

module MemoryConstants =
    /// <summary>최적화된 메모리에서 지원하는 최대 변수 개수</summary>
    /// <remarks>DEPRECATED: Use RuntimeLimits.Current.MaxMemoryVariables instead (NEW-DEFECT-002 fix)</remarks>
    [<Literal>]
    let MaxMemoryVariables = 2000

    /// <summary>변수 값 변경 히스토리의 최대 크기 (메모리 관리를 위한 상한)</summary>
    /// <remarks>DEPRECATED: Use RuntimeLimits.Current.MaxHistorySize instead (NEW-DEFECT-002 fix)</remarks>
    [<Literal>]
    let MaxHistorySize = 10000

// ═════════════════════════════════════════════════════════════════════

/// <summary>메모리 영역 구분 (IEC 61131-3 표준)</summary>
/// <remarks>
/// PLC에서 사용하는 4가지 메모리 영역을 구분합니다.
/// - Input: 입력 변수 (I:) - 읽기 전용
/// - Output: 출력 변수 (O:) - 읽기/쓰기
/// - Local: 로컬 변수 (L:) - 읽기/쓰기
/// - Internal: 내부 변수 (V:) - 읽기/쓰기
/// </remarks>
type MemoryArea =
    /// 입력 영역 (I:) - 읽기 전용
    | Input
    /// 출력 영역 (O:) - 읽기/쓰기
    | Output
    /// 로컬 영역 (L:) - 읽기/쓰기
    | Local
    /// 내부 영역 (V:) - 읽기/쓰기
    | Internal

    /// <summary>영역 접두사 문자열</summary>
    /// <returns>I:, O:, L:, V: 중 하나</returns>
    member this.Prefix =
        match this with
        | Input -> "I:"
        | Output -> "O:"
        | Local -> "L:"
        | Internal -> "V:"

    /// <summary>쓰기 가능 여부</summary>
    /// <returns>Input 영역은 false, 나머지는 true</returns>
    member this.IsWritable =
        match this with
        | Input -> false
        | _ -> true

/// <summary>최적화된 메모리 슬롯 (struct로 메모리 효율성 향상)</summary>
/// <remarks>
/// struct 타입으로 선언하여 힙 할당을 줄이고 캐시 친화적입니다.
/// - Value: 현재 값
/// - LastValue: 이전 값 (변경 감지용)
/// - Changed: 변경 플래그
/// - DataType: 데이터 타입
/// - Area: 메모리 영역
/// 배열 인덱싱 방식으로 빠른 접근 제공
/// </remarks>
[<Struct>]
type OptimizedSlot = {
    /// 현재 변수 값
    mutable Value: obj
    /// 이전 값 (변경 감지용)
    mutable LastValue: obj
    /// 값 변경 플래그
    mutable Changed: bool
    /// 데이터 타입
    DataType: Type
    /// 메모리 영역
    Area: MemoryArea
}

/// <summary>메모리 슬롯 (레거시 호환성)</summary>
/// <remarks>
/// 변수 하나를 저장하는 슬롯입니다.
/// - Name: 변수 이름
/// - Area: 메모리 영역 (Input/Output/Local/Internal)
/// - DataType: 데이터 타입
/// - Value: 현재 값
/// - IsRetain: 전원 유지 변수 여부
/// </remarks>
type MemorySlot =
    { /// 변수 이름
      Name: string
      /// 메모리 영역 (mutable)
      mutable Area: MemoryArea
      /// 데이터 타입
      DataType: Type
      /// 현재 값 (mutable)
      mutable Value: obj
      /// 전원 유지 변수 여부
      IsRetain: bool }

    /// <summary>값을 기본값으로 리셋</summary>
    member slot.ResetDefault() =
        slot.Value <- TypeHelpers.getDefaultValue slot.DataType

/// <summary>최적화된 메모리 저장소 (배열 기반 인덱싱)</summary>
/// <remarks>
/// 고성능 메모리 관리를 위한 최적화 구현입니다.
/// - 배열 기반 인덱싱으로 O(1) 접근 성능
/// - struct 슬롯으로 메모리 효율성 극대화
/// - RuntimeConfiguration을 통한 변수 개수 제한 (NEW-DEFECT-002 fix)
/// - 변경 플래그를 통한 선택적 스캔 지원
/// - 의존성 맵으로 변경 전파 추적
/// </remarks>
type OptimizedMemory() =
    let maxVariables = RuntimeLimits.Current.MaxMemoryVariables
    let varIndex = Dictionary<string, int>(StringComparer.Ordinal)
    let slots = Array.zeroCreate<OptimizedSlot> maxVariables
    let mutable nextIndex = 0
    let dependencyMap = Dictionary<string, Set<string>>(StringComparer.Ordinal)

    /// <summary>변수 이름에서 인덱스 조회</summary>
    /// <param name="name">변수 이름</param>
    /// <returns>Some index (존재하면) 또는 None</returns>
    member _.GetIndex(name: string) =
        match varIndex.TryGetValue(name) with
        | true, index -> Some index
        | false, _ -> None

    /// <summary>새 변수 선언 및 인덱스 할당</summary>
    /// <param name="name">변수 이름</param>
    /// <param name="dataType">데이터 타입</param>
    /// <param name="area">메모리 영역</param>
    /// <returns>할당된 인덱스</returns>
    /// <exception cref="System.Exception">최대 변수 개수 초과 시</exception>
    member this.DeclareVariable(name: string, dataType: Type, area: MemoryArea) =
        // Per spec: reject re-declaration instead of silently mutating Area
        if varIndex.ContainsKey(name) then
            let existingIndex = varIndex.[name]
            let existingArea = slots.[existingIndex].Area
            RuntimeExceptions.raiseVariableAlreadyDeclaredInArea name (string existingArea) (string area)
        if nextIndex >= maxVariables then
            RuntimeExceptions.raiseMemoryLimit maxVariables
        let index = nextIndex
        nextIndex <- nextIndex + 1
        varIndex.[name] <- index
        slots.[index] <- {
            Value = TypeHelpers.getDefaultValue dataType
            LastValue = null
            Changed = false
            DataType = dataType
            Area = area
        }
        index

    /// <summary>변수 값 조회</summary>
    /// <param name="name">변수 이름</param>
    /// <returns>변수 값 (없으면 null)</returns>
    member this.Get(name: string) =
        match this.GetIndex(name) with
        | Some index -> slots.[index].Value
        | None -> null

    /// <summary>변수 값 설정</summary>
    /// <param name="name">변수 이름</param>
    /// <param name="value">설정할 값</param>
    /// <exception cref="System.Exception">쓰기 불가 영역일 때 또는 변수가 선언되지 않았을 때</exception>
    /// <remarks>변수가 없으면 예외 발생 (명시적 선언 필요)</remarks>
    member this.Set(name: string, value: obj) =
        match this.GetIndex(name) with
        | Some index ->
            let slot = &slots.[index]
            if not slot.Area.IsWritable then RuntimeExceptions.raiseCannotWriteToVariable name
            slot.LastValue <- slot.Value
            slot.Value <- value
            slot.Changed <- not (Object.Equals(slot.LastValue, value))
        | None ->
            // Per spec: only Declare may create a symbol, Set must throw if variable doesn't exist
            RuntimeExceptions.raiseVariableNotDeclared name

    /// <summary>변수 값 강제 설정 (쓰기 보호 무시, Internal 자동 선언)</summary>
    /// <param name="name">변수 이름</param>
    /// <param name="value">설정할 값</param>
    /// <remarks>
    /// CRITICAL FIX (DEFECT-021-2/3): Auto-declare Internal variables for edge flags
    /// Previous SetForced required pre-declaration, causing TP/CTUD to crash on first use
    /// Now auto-declares typeof<bool> Internal variables when not found (non-retain by default)
    /// MAJOR FIX: Runtime updates need to drive input variables for simulation/diagnostics
    /// This bypasses the writability check to allow setting I: (input) domain variables
    /// Only use for runtime updates/edge flags - normal program logic should respect writability
    /// </remarks>
    member this.SetForced(name: string, value: obj) =
        match this.GetIndex(name) with
        | Some index ->
            let slot = &slots.[index]
            // Record change before updating value
            let changed = not (Object.Equals(slot.Value, value))
            slot.LastValue <- slot.Value
            slot.Value <- value
            slot.Changed <- changed
            // Mark dependents if value actually changed
            if changed then
                dependencyMap
                |> Seq.filter (fun kvp -> Set.contains name kvp.Value)
                |> Seq.iter (fun kvp -> slots.[varIndex.[kvp.Key]].Changed <- true)
        | None ->
            // CRITICAL FIX (DEFECT-021-2/3): Auto-declare Internal variables for edge flags
            // Previous SetForced required pre-declaration, causing TP/CTUD to crash on first use
            // Auto-declare as Internal (non-retain) for edge tracking flags
            if nextIndex >= maxVariables then
                RuntimeExceptions.raiseMemoryLimit maxVariables
            let dtype = if isNull value then typeof<bool> else value.GetType()
            varIndex.[name] <- nextIndex
            // OptimizedSlot doesn't store Name/IsRetain - those are tracked via varIndex and Area
            slots.[nextIndex] <- {
                Value = value
                LastValue = null
                Changed = true
                DataType = dtype
                Area = MemoryArea.Internal
            }
            nextIndex <- nextIndex + 1

    /// <summary>변수 값 변경 여부 확인</summary>
    /// <param name="name">변수 이름</param>
    /// <returns>마지막 스캔 이후 변경되었으면 true</returns>
    member _.HasChanged(name: string) =
        match varIndex.TryGetValue(name) with
        | true, index -> slots.[index].Changed
        | false, _ -> false

    /// <summary>모든 변경 플래그 초기화</summary>
    /// <remarks>스캔 종료 시 호출하여 다음 스캔을 위해 준비</remarks>
    member _.ClearChangeFlags() =
        for i = 0 to nextIndex - 1 do
            slots.[i].Changed <- false

    /// <summary>현재 메모리 상태 스냅샷 생성</summary>
    /// <returns>모든 변수와 값의 맵 (영역 접두사 포함)</returns>
    member _.Snapshot() =
        seq {
            for KeyValue(name, index) in varIndex do
                let slot = slots.[index]
                let key = slot.Area.Prefix + name
                yield (key, slot.Value)
        } |> Map.ofSeq

// ═════════════════════════════════════════════════════════════════════
// 트랜잭션 스냅샷 (GAP-002: Rollback Support)
// ═════════════════════════════════════════════════════════════════════

/// <summary>타이머 상태 스냅샷 (불변)</summary>
/// <remarks>트랜잭션 롤백을 위한 타이머 상태 복사본</remarks>
type TimerStateSnapshot = {
    Preset: int
    Accumulated: int
    Done: bool
    Timing: bool
    LastTimestamp: int64
}

/// <summary>카운터 상태 스냅샷 (불변)</summary>
/// <remarks>트랜잭션 롤백을 위한 카운터 상태 복사본</remarks>
type CounterStateSnapshot = {
    Preset: int
    Count: int
    Done: bool
    Up: bool
    LastCountInput: bool
}

/// <summary>메모리 스냅샷 (트랜잭션 복구용)</summary>
/// <remarks>
/// ExecutionContext.Rollback() 지원을 위한 메모리 상태 스냅샷입니다.
/// - 모든 변수의 이름과 현재 값을 Map으로 저장
/// - 타이머 및 카운터 상태 포함 (HIGH FIX: RuntimeSpec.md:113-118)
/// - Recoverable 에러 발생 시 이 스냅샷으로 복원 가능
/// - 스캔 시작 시 생성하여 안전한 복원 지점 제공
/// </remarks>
type MemorySnapshot = {
    /// 스냅샷 생성 시각
    Timestamp: DateTime
    /// 모든 변수의 이름과 값 (얕은 복사)
    Variables: Map<string, obj>
    /// 스냅샷 시점의 변수 키 목록 (MAJOR FIX: Rollback 시 추가 변수 삭제용)
    VariableKeys: Set<string>
    /// 스캔 카운터 (MEDIUM FIX: RuntimeSpec.md:118)
    ScanCount: int64
    /// 타이머 상태 스냅샷 (HIGH FIX: RuntimeSpec.md:113-118)
    Timers: Map<string, TimerStateSnapshot>
    /// 카운터 상태 스냅샷 (HIGH FIX: RuntimeSpec.md:113-118)
    Counters: Map<string, CounterStateSnapshot>
    /// HIGH FIX (DEFECT-019-7): Store Area/IsRetain metadata for proper rollback
    /// Previous snapshots lost domain/retain info, so rollback created wrong slots
    VariableAreas: Map<string, MemoryArea>
    VariableRetainFlags: Map<string, bool>
}

/// <summary>PLC 변수 메모리 관리 클래스</summary>
/// <remarks>
/// PLC 런타임에서 사용하는 변수 저장소입니다.
/// - 4가지 메모리 영역 지원 (Input/Output/Local/Internal)
/// - 타입 안전 변수 저장
/// - 변수 값 변경 히스토리 관리
/// - 변경 감지 및 의존성 전파
/// - 리테인 메모리 (전원 유지 변수) 지원
/// - 스캔 카운터 및 통계 제공
/// 스레드 안전하지 않으므로 단일 스레드에서만 사용
/// </remarks>
type Memory() =

    let entries = Dictionary<string, MemorySlot>(StringComparer.Ordinal)
    let types   = Dictionary<string, Type>(StringComparer.Ordinal)

    let history = ResizeArray<string * obj * DateTime>()
    let mutable scanCounter = 0L

    let changeFlags = Dictionary<string, bool>(StringComparer.Ordinal)
    let lastValues  = Dictionary<string, obj>(StringComparer.Ordinal)
    let dependencyMap = Dictionary<string, Set<string>>(StringComparer.Ordinal)

    // 스키마 추적 (고성능 retain 저장소용)
    let mutable currentSchemaHash: string option = None
    let mutable isSchemaModified = false

    let tryFindSlot name =
        match entries.TryGetValue(name) with
        | true, slot -> Some slot
        | _ -> None

    let inferDataType (value: obj) =
        if isNull value then typeof<string>
        else value.GetType()

    let appendHistory name value =
        if history.Count >= RuntimeLimits.Current.MaxHistorySize then history.RemoveAt(0)
        history.Add(name, value, DateTime.Now)

    let markDependents name =
        let visited = HashSet<string>(StringComparer.Ordinal)
        let rec mark current =
            if visited.Add(current) then
                for KeyValue(target, inputs) in dependencyMap do
                    if Set.contains current inputs then
                        changeFlags.[target] <- true
                        mark target
        mark name

    let recordChange name newValue =
        let oldValue =
            match tryFindSlot name with
            | Some slot -> slot.Value
            | None -> null
        let changed = not (Object.Equals(oldValue, newValue))
        if changed then
            changeFlags.[name] <- true
            lastValues.[name] <- oldValue
            markDependents name
        changed

    let ensureDeclared area (name: string) (dtype: Type) (isRetain: bool) =
        if String.IsNullOrWhiteSpace name then invalidArg "name" "Name cannot be empty"
        match tryFindSlot name with
        | Some slot when slot.DataType <> dtype ->
            RuntimeExceptions.raiseVariableAlreadyDeclared name (TypeHelpers.getTypeName slot.DataType) (TypeHelpers.getTypeName dtype)
        | Some slot when slot.IsRetain <> isRetain ->
            // IsRetain flag changed - reject if Area also changed
            if slot.Area <> area then
                RuntimeExceptions.raiseVariableAlreadyDeclaredInArea name (string slot.Area) (string area)
            // IsRetain flag changed - recreate slot with new flag, preserving current value
            let newSlot =
                { Name = name
                  Area = area
                  DataType = dtype
                  Value = slot.Value  // Preserve current value
                  IsRetain = isRetain }
            entries.[name] <- newSlot
            types.[name] <- dtype
            newSlot
        | Some slot ->
            // Per spec: reject re-declaration with different Area instead of silently mutating
            if slot.Area <> area then
                RuntimeExceptions.raiseVariableAlreadyDeclaredInArea name (string slot.Area) (string area)
            // Slot already exists with same area - preserve current value and change tracking
            types.[name] <- dtype
            slot
        | None ->
            // MEDIUM FIX (DEFECT-018-8): Check memory limit before creating new variables
            // Without this check, runaway programs can exhaust process memory
            if entries.Count >= RuntimeLimits.Current.MaxMemoryVariables then
                RuntimeExceptions.raiseMemoryLimitWithCount RuntimeLimits.Current.MaxMemoryVariables entries.Count

            let slot =
                { Name = name
                  Area = area
                  DataType = dtype
                  Value = TypeHelpers.getDefaultValue dtype
                  IsRetain = isRetain }
            entries.Add(name, slot)
            types.[name] <- dtype
            slot

    let ensureInternalSlot name value =
        match tryFindSlot name with
        | Some slot -> slot
        | None ->
            let dtype = inferDataType value
            let slot =
                { Name = name
                  Area = MemoryArea.Internal
                  DataType = dtype
                  Value = TypeHelpers.getDefaultValue dtype
                  IsRetain = false }
            entries.Add(name, slot)
            slot

    let coerceValue (slot: MemorySlot) (value: obj) =
        if isNull value then TypeHelpers.getDefaultValue slot.DataType
        else TypeHelpers.validateType slot.DataType value

    let removeByArea predicate =
        entries
        |> Seq.filter (fun kv -> predicate kv.Value.Area)
        |> Seq.map (fun kv -> kv.Key)
        |> Seq.toArray
        |> Array.iter (fun name ->
            entries.Remove(name) |> ignore
            types.Remove(name) |> ignore
            changeFlags.Remove(name) |> ignore
            lastValues.Remove(name) |> ignore)

    // ─────────────────────────────────────────────
    // 조회 / 업데이트
    // ─────────────────────────────────────────────

    /// <summary>변수 값 조회</summary>
    /// <param name="name">변수 이름</param>
    /// <returns>변수 값 (변수가 없으면 null)</returns>
    member _.Get(name: string) : obj =
        match tryFindSlot name with
        | Some slot -> slot.Value
        | None -> null

    /// <summary>변수 값 설정 (Internal 영역)</summary>
    /// <param name="name">변수 이름</param>
    /// <param name="value">설정할 값</param>
    /// <exception cref="System.Exception">Input 영역에 쓰기 시도하거나 변수가 선언되지 않았을 때</exception>
    /// <remarks>
    /// - 변수가 없으면 예외 발생 (명시적 선언 필요)
    /// - 타입 검증 및 강제 변환 수행
    /// - 변경 감지 및 히스토리 기록
    /// - 의존 변수에 변경 플래그 전파
    /// </remarks>
    member this.Set(name: string, value: obj) =
        let slot =
            match tryFindSlot name with
            | Some s -> s
            | None -> RuntimeExceptions.raiseVariableNotDeclared name
        if not slot.Area.IsWritable then
            RuntimeExceptions.raiseCannotWriteToInput name
        let coerced = coerceValue slot value
        let changed = recordChange name coerced
        slot.Value <- coerced
        if changed then appendHistory name coerced

    /// <summary>변수 값 강제 설정 (쓰기 보호 무시, Internal 자동 선언)</summary>
    /// <param name="name">변수 이름</param>
    /// <param name="value">설정할 값</param>
    /// <remarks>
    /// CRITICAL FIX (DEFECT-022-1): Auto-declare Internal variables for edge flags
    /// Previous SetForced threw VariableNotDeclared, breaking TP/CTUD on first use
    /// Now auto-declares typeof<bool> Internal variables when not found (non-retain by default)
    /// MAJOR FIX: Runtime updates need to drive input variables for simulation/diagnostics
    /// This bypasses the writability check to allow setting I: (input) domain variables
    /// Only use for runtime updates/edge flags - normal program logic should respect writability
    /// </remarks>
    member this.SetForced(name: string, value: obj) =
        match tryFindSlot name with
        | Some slot ->
            let coerced = coerceValue slot value
            let changed = recordChange name coerced
            slot.Value <- coerced
            if changed then appendHistory name coerced
        | None ->
            // CRITICAL FIX (DEFECT-022-1): Auto-declare Internal variables for edge flags
            // Previous SetForced threw VariableNotDeclared, breaking TP/CTUD on first use
            // Auto-declare as Internal (non-retain) for edge tracking flags
            let dtype = if isNull value then typeof<bool> else value.GetType()
            let area = MemoryArea.Internal
            types.[name] <- dtype
            let slot = { Name = name; Value = value; IsRetain = false; Area = area; DataType = dtype }
            entries.[name] <- slot
            let changed = recordChange name value
            if changed then appendHistory name value

    /// <summary>입력 변수 값 설정 (Input 영역)</summary>
    /// <param name="name">입력 변수 이름</param>
    /// <param name="value">설정할 값</param>
    /// <remarks>
    /// PLC 입력을 시뮬레이션하거나 테스트할 때 사용합니다.
    /// - Input 영역에 변수 생성 또는 업데이트
    /// - 타입 검증 및 강제 변환 수행
    /// - 변경 감지 및 히스토리 기록
    /// </remarks>
    member this.SetInput(name: string, value: obj) =
        let declaredType =
            match types.TryGetValue(name) with
            | true, dtype -> dtype
            | _ ->
                let inferred = inferDataType value
                types.[name] <- inferred
                inferred
        let slot =
            match tryFindSlot name with
            | Some slot ->
                if slot.DataType <> declaredType then
                    RuntimeExceptions.raiseVariableAlreadyDeclared name (TypeHelpers.getTypeName slot.DataType) (TypeHelpers.getTypeName declaredType)
                // MAJOR FIX: Don't mutate area - if slot exists, it should already be Input
                // Changing area breaks runtime's ability to drive Local/Output variables
                // MEDIUM FIX (DEFECT-018-9): Use domain-specific exception instead of failwith
                // Generic failwith breaks structured logging and catch patterns
                if slot.Area <> MemoryArea.Input then
                    raise (InvalidOperationException($"Variable '{name}' already declared in {slot.Area.Prefix} area, cannot set as Input"))
                slot
            | None ->
                let slot =
                    { Name = name
                      Area = MemoryArea.Input
                      DataType = declaredType
                      Value = TypeHelpers.getDefaultValue declaredType
                      IsRetain = false }
                entries.Add(name, slot)
                slot
        let coerced = coerceValue slot value
        let changed = recordChange name coerced
        slot.Value <- coerced
        if changed then appendHistory name coerced

    /// <summary>출력 변수 값 조회 (Output 영역)</summary>
    /// <param name="name">출력 변수 이름</param>
    /// <returns>변수 값 (Output 영역이 아니면 null)</returns>
    member _.GetOutput(name: string) : obj =
        match tryFindSlot name with
        | Some slot when slot.Area = MemoryArea.Output -> slot.Value
        | _ -> null

    // ─────────────────────────────────────────────
    // 선언 / 초기화
    // ─────────────────────────────────────────────

    /// <summary>로컬 변수 선언 (Local 영역)</summary>
    /// <param name="name">변수 이름</param>
    /// <param name="dtype">데이터 타입</param>
    /// <param name="retain">전원 유지 여부 (기본값: false)</param>
    member _.DeclareLocal(name: string, dtype: Type, ?retain: bool) =
        let isRetain = defaultArg retain false
        // 스키마 변경 감지 (retain 변수가 새로 추가되는 경우)
        let isNewRetainVar = isRetain && not (entries.ContainsKey(name))
        let slot = ensureDeclared MemoryArea.Local name dtype isRetain
        if isNewRetainVar then
            isSchemaModified <- true
        slot |> ignore

    /// <summary>입력 변수 선언 (Input 영역)</summary>
    /// <param name="name">변수 이름</param>
    /// <param name="dtype">데이터 타입</param>
    member _.DeclareInput(name: string, dtype: Type) =
        ensureDeclared MemoryArea.Input name dtype false |> ignore

    /// <summary>출력 변수 선언 (Output 영역)</summary>
    /// <param name="name">변수 이름</param>
    /// <param name="dtype">데이터 타입</param>
    member _.DeclareOutput(name: string, dtype: Type) =
        ensureDeclared MemoryArea.Output name dtype false |> ignore

    /// <summary>내부 변수 선언 (Internal 영역)</summary>
    /// <param name="name">변수 이름</param>
    /// <param name="dtype">데이터 타입</param>
    /// <param name="retain">전원 유지 여부 (기본값: false)</param>
    member _.DeclareInternal(name: string, dtype: Type, ?retain: bool) =
        let isRetain = defaultArg retain false
        // 스키마 변경 감지 (retain 변수가 새로 추가되는 경우)
        let isNewRetainVar = isRetain && not (entries.ContainsKey(name))
        let slot = ensureDeclared MemoryArea.Internal name dtype isRetain
        if isNewRetainVar then
            isSchemaModified <- true
        slot |> ignore

    /// <summary>메모리 초기화 (Input 제외)</summary>
    /// <remarks>
    /// Output, Local, Internal 영역을 모두 삭제하고 초기화합니다.
    /// - Input 영역은 유지
    /// - 히스토리 및 변경 플래그 초기화
    /// </remarks>
    member _.Clear() =
        removeByArea (fun area -> area <> MemoryArea.Input)
        history.Clear()
        changeFlags.Clear()
        lastValues.Clear()

    /// <summary>입력 변수 모두 제거</summary>
    /// <remarks>
    /// Input 영역의 모든 변수를 삭제합니다.
    /// - 변경 플래그도 함께 초기화
    /// </remarks>
    member _.ClearInputs() =
        removeByArea (fun area -> area = MemoryArea.Input)
        changeFlags.Clear()
        lastValues.Clear()

    // ─────────────────────────────────────────────
    // 모니터링 / 통계
    // ─────────────────────────────────────────────

    /// <summary>스캔 카운터 증가</summary>
    /// <remarks>스캔 사이클이 실행될 때마다 호출</remarks>
    member _.IncrementScan() =
        scanCounter <- scanCounter + 1L

    /// <summary>현재 스캔 카운터 값</summary>
    member _.ScanCount = scanCounter

    /// <summary>스캔 카운터 복원 (트랜잭션 롤백용)</summary>
    /// <param name="count">복원할 스캔 카운터 값</param>
    /// <remarks>MEDIUM FIX: Rollback 시 스캔 카운터도 복원 (RuntimeSpec.md:118)</remarks>
    member _.RestoreScanCount(count: int64) =
        scanCounter <- count

    /// <summary>현재 메모리 상태 스냅샷 생성</summary>
    /// <returns>모든 변수와 값의 맵 (영역 접두사 포함)</returns>
    member _.Snapshot() : Map<string, obj> =
        // CRITICAL FIX (DEFECT-CRIT-4): Dictionary enumeration race condition
        // Previous code: entries.Values directly enumerated (not thread-safe)
        // Problem: If another thread modifies dictionary during enumeration, InvalidOperationException
        // Solution: Snapshot values using ToArray() (atomic copy in Dictionary.Values)
        let snapshot = entries.Values |> Seq.toArray
        snapshot
        |> Seq.map (fun slot -> slot.Area.Prefix + slot.Name, slot.Value)
        |> Map.ofSeq

    /// <summary>메모리 스냅샷을 텍스트로 변환</summary>
    /// <returns>"변수명=값" 형식의 쉼표 구분 문자열</returns>
    member this.SnapshotText() =
        this.Snapshot()
        |> Seq.map (fun (KeyValue(k, v)) ->
            let valueStr = if isNull v then "<null>" else v.ToString()
            $"{k}={valueStr}")
        |> String.concat ", "

    /// <summary>변수 값 변경 히스토리 조회</summary>
    /// <param name="name">변수 이름</param>
    /// <param name="count">조회할 개수 (기본값: 10)</param>
    /// <returns>(변수명, 값, 시각) 튜플 리스트 (최신순)</returns>
    member _.GetHistory(name: string, ?count: int) =
        let n = defaultArg count 10
        history
        |> Seq.filter (fun (n', _, _) -> n' = name)
        |> Seq.rev
        |> Seq.truncate n
        |> Seq.toList

    /// <summary>변수 존재 여부 확인</summary>
    /// <param name="name">변수 이름</param>
    /// <returns>변수가 존재하면 true</returns>
    member _.Exists(name: string) = entries.ContainsKey(name)

    /// <summary>변수 타입 조회</summary>
    /// <param name="name">변수 이름</param>
    /// <returns>Some Type (존재하면) 또는 None</returns>
    member _.GetType(name: string) : Type option =
        match types.TryGetValue(name) with
        | true, dtype -> Some dtype
        | _ ->
            match tryFindSlot name with
            | Some slot -> Some slot.DataType
            | None -> None

    /// <summary>메모리 통계 정보 조회</summary>
    /// <returns>익명 레코드로 영역별 변수 개수, 히스토리 크기, 스캔 카운터 반환</returns>
    member _.Stats() =
        let count area =
            entries.Values
            |> Seq.filter (fun slot -> slot.Area = area)
            |> Seq.length
        let inputs = count MemoryArea.Input
        let outputs = count MemoryArea.Output
        let locals = count MemoryArea.Local
        let variables = count MemoryArea.Internal
        {|
            Inputs = inputs
            Outputs = outputs
            Locals = locals
            Variables = variables
            Total = inputs + outputs + locals + variables
            HistorySize = history.Count
            ScanCount = scanCounter
        |}

    /// <summary>변수 값 변경 여부 확인</summary>
    /// <param name="name">변수 이름</param>
    /// <returns>마지막 스캔 이후 변경되었으면 true</returns>
    member _.HasChanged(name: string) =
        match changeFlags.TryGetValue(name) with
        | true, flag -> flag
        | _ -> false

    /// <summary>모든 변경 플래그 초기화</summary>
    /// <remarks>스캔 종료 시 호출하여 다음 스캔을 위해 준비</remarks>
    member _.ClearChangeFlags() =
        changeFlags.Clear()
        lastValues.Clear()

    /// <summary>변수 간 의존성 맵 설정</summary>
    /// <param name="dependencies">변수 의존성 맵 (대상 -> 입력 변수 집합)</param>
    /// <remarks>선택적 스캔 최적화를 위해 사용</remarks>
    member _.SetDependencyMap(dependencies: Map<string, Set<string>>) =
        dependencyMap.Clear()
        dependencies |> Map.iter (fun key deps -> dependencyMap.[key] <- deps)

    /// <summary>모든 변수를 변경됨으로 표시</summary>
    /// <remarks>
    /// 전체 스캔 강제 실행을 위해 모든 변수와 의존 타겟을 변경됨으로 표시합니다.
    /// </remarks>
    member _.MarkAllChanged() =
        changeFlags.Clear()
        for KeyValue(name, _) in entries do
            changeFlags.[name] <- true
        for KeyValue(target, _) in dependencyMap do
            changeFlags.[target] <- true

    // ─────────────────────────────────────────────
    // 트랜잭션 스냅샷 (GAP-002: Rollback Support)
    // ─────────────────────────────────────────────

    /// <summary>현재 메모리 상태의 스냅샷 생성 (RuntimeSpec.md:104)</summary>
    /// <returns>MemorySnapshot (모든 변수 값 포함)</returns>
    /// <remarks>
    /// 트랜잭션 복구를 위한 스냅샷을 생성합니다.
    /// - 모든 변수의 현재 값을 Map으로 복사
    /// - Recoverable 에러 발생 시 Rollback으로 복원 가능
    /// - 스캔 시작 시 호출하여 안전한 복원 지점 생성
    /// </remarks>
    member _.CreateSnapshot() : MemorySnapshot =
        let varMap =
            entries
            |> Seq.map (fun (KeyValue(name, slot)) -> (name, slot.Value))
            |> Map.ofSeq
        // MAJOR FIX: Store variable keys for complete rollback (delete variables created during transaction)
        let varKeys =
            entries.Keys
            |> Set.ofSeq
        // HIGH FIX (DEFECT-019-7): Store Area/IsRetain metadata for proper rollback
        let varAreas =
            entries
            |> Seq.map (fun (KeyValue(name, slot)) -> (name, slot.Area))
            |> Map.ofSeq
        let varRetainFlags =
            entries
            |> Seq.map (fun (KeyValue(name, slot)) -> (name, slot.IsRetain))
            |> Map.ofSeq
        {
            Timestamp = DateTime.UtcNow
            Variables = varMap
            VariableKeys = varKeys
            ScanCount = scanCounter
            // Note: Timers and Counters are populated by ExecutionContext.CreateSnapshot
            Timers = Map.empty
            Counters = Map.empty
            VariableAreas = varAreas
            VariableRetainFlags = varRetainFlags
        }

    /// <summary>스냅샷에서 메모리 상태 복원 (RuntimeSpec.md:104)</summary>
    /// <param name="snapshot">복원할 MemorySnapshot</param>
    /// <remarks>
    /// Recoverable 에러 발생 시 메모리를 스냅샷 시점으로 롤백합니다.
    /// - 모든 변수 값을 스냅샷에 저장된 값으로 복원
    /// - 변경 플래그 및 히스토리는 유지
    /// - 스캔 카운터는 복원하지 않음 (계속 증가)
    /// </remarks>
    member _.Rollback(snapshot: MemorySnapshot) =
        // 1. Restore variable values from snapshot
        for KeyValue(name, value) in snapshot.Variables do
            match tryFindSlot name with
            | Some slot ->
                slot.Value <- value
                // MAJOR FIX (DEFECT-016-3): Do NOT call recordChange/appendHistory on rollback
                // Rollback returns to start-of-scan state (RuntimeSpec.md:117)
                // Setting changeFlags causes selective mode to re-run unnecessarily
                // Only restore values, don't mark as changed
            | None ->
                // CRITICAL FIX (DEFECT-018-2): Recreate variables that existed in snapshot but were deleted
                // Without this, variables deleted during failed transaction vanish permanently
                // Violates transactional guarantees - rollback must restore ALL state

                // HIGH FIX (DEFECT-019-7): Use snapshot metadata to preserve Area/IsRetain
                // Previous code assumed Local/non-retain, losing domain and retain semantics
                let area = snapshot.VariableAreas.TryFind(name) |> Option.defaultValue MemoryArea.Local
                let isRetain = snapshot.VariableRetainFlags.TryFind(name) |> Option.defaultValue false

                // Infer type from value (best effort for all supported types)
                let dataType =
                    if isNull value then typeof<int>  // Default for null
                    else
                        match value with
                        | :? bool -> typeof<bool>
                        | :? sbyte -> typeof<sbyte>
                        | :? byte -> typeof<byte>
                        | :? int16 -> typeof<int16>
                        | :? uint16 -> typeof<uint16>
                        | :? int -> typeof<int>
                        | :? uint32 -> typeof<uint32>
                        | :? int64 -> typeof<int64>
                        | :? uint64 -> typeof<uint64>
                        | :? double -> typeof<double>
                        | :? string -> typeof<string>
                        | _ -> typeof<int>  // Default for unknown types

                // Recreate slot with correct Area/IsRetain from snapshot
                let slot = { Name = name; Area = area; DataType = dataType; Value = value; IsRetain = isRetain }
                entries.[name] <- slot
                types.[name] <- dataType

        // HIGH FIX (DEFECT-018-3): Clear change flags for all restored variables
        // Variables modified during the failed transaction have stale changeFlags/lastValues
        // Selective mode would treat them as dirty even though values were rolled back
        // Must clear flags AFTER value restoration to prevent false dirty marks
        for name in snapshot.VariableKeys do
            changeFlags.Remove(name) |> ignore
            lastValues.Remove(name) |> ignore

        // MAJOR FIX (DEFECT-016-4): Clear changeFlags/lastValues for deleted variables
        // Variables created during transaction are removed, but their history leaks
        // HasChanged() can return true for non-existent variables, confusing dependencies
        let currentKeys = entries.Keys |> Seq.toList
        for name in currentKeys do
            if not (snapshot.VariableKeys.Contains(name)) then
                entries.Remove(name) |> ignore
                types.Remove(name) |> ignore
                changeFlags.Remove(name) |> ignore
                lastValues.Remove(name) |> ignore

        // LOW FIX (DEFECT-018-10): Clear history entries added during failed transaction
        // History shows writes that never took effect, which is misleading for diagnostics
        // Remove entries added after snapshot timestamp to maintain accurate history
        let mutable i = history.Count - 1
        while i >= 0 do
            let (_, _, timestamp) = history.[i]
            if timestamp > snapshot.Timestamp then
                history.RemoveAt(i)
            i <- i - 1

    // ─────────────────────────────────────────────
    // 리테인 메모리 (Retain Memory)
    // ─────────────────────────────────────────────

    /// <summary>리테인 변수 스냅샷 생성</summary>
    /// <returns>RetainSnapshot (전원 유지 변수의 현재 값)</returns>
    /// <remarks>
    /// IsRetain = true인 모든 변수의 현재 값을 JSON으로 직렬화하여 저장합니다.
    /// 전원 재시작 후 RestoreFromSnapshot으로 복원 가능
    /// </remarks>
    member _.CreateRetainSnapshot() : RetainSnapshot =
        // 모든 retain 변수를 분류: 일반 변수 vs FB Static 변수
        let mutable regularVars = []
        // Store FB static with type metadata (name, dataType, valueBytes)
        let fbStaticMap = System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string * string * byte[]>>()

        for KeyValue(name, slot) in entries do
            if slot.IsRetain then
                // MAJOR FIX (DEFECT-022-7): Populate fbStaticMap for telemetry
                // Previous code only stored as regular variables, losing FB grouping metadata
                // Now: Store both as regular vars (for data safety) AND in fbStaticMap (for telemetry)
                //
                // Heuristic: FB instance names are CamelCase (e.g., "MyCounter", "Timer1")
                // Static variable names are UPPER_CASE or camelCase (e.g., "ALARM_STATE", "currentValue")
                // Look for first underscore where the part after it looks like a static var
                let findInstanceSplit (fullName: string) =
                    let mutable splitIndex = -1
                    let mutable i = 0
                    while i < fullName.Length - 1 && splitIndex = -1 do
                        if fullName.[i] = '_' then
                            let afterUnderscore = fullName.Substring(i + 1)
                            // Static var pattern: starts with uppercase (UPPER_CASE) or lowercase (camelCase)
                            // NOT uppercase followed by lowercase (that's CamelCase instance continuation)
                            let looksLikeStaticVar =
                                if afterUnderscore.Length > 1 && System.Char.IsUpper(afterUnderscore.[0]) then
                                    // If second char is also upper or underscore, it's UPPER_CASE static
                                    System.Char.IsUpper(afterUnderscore.[1]) || afterUnderscore.[1] = '_'
                                elif afterUnderscore.Length > 0 && System.Char.IsLower(afterUnderscore.[0]) then
                                    // Starts with lowercase, likely camelCase static
                                    true
                                else
                                    false
                            if looksLikeStaticVar then
                                splitIndex <- i
                        i <- i + 1
                    if splitIndex = -1 then fullName.LastIndexOf('_') else splitIndex

                let underscoreIndex = findInstanceSplit name
                let isFBStatic =
                    if underscoreIndex > 0 && underscoreIndex < name.Length - 1 && slot.Area = MemoryArea.Internal then
                        let prefix = name.Substring(0, underscoreIndex)
                        // Exclude system vars and plain globals
                        let isSystemVar = prefix.ToUpperInvariant() = prefix && prefix.Contains('_')
                        let isPlainGlobal = name.ToUpperInvariant() = name && name.Contains('_')
                        not isSystemVar && not isPlainGlobal
                    else
                        false

                if isFBStatic then
                    // FB Static variable - store in both places
                    let fbInstance = name.Substring(0, underscoreIndex)
                    let staticVarName = name.Substring(underscoreIndex + 1)
                    let valueBytes = RetainValueSerializer.serialize slot.Value slot.DataType
                    let dataType = TypeHelpers.getTypeName slot.DataType

                    if not (fbStaticMap.ContainsKey(fbInstance)) then
                        fbStaticMap.[fbInstance] <- System.Collections.Generic.List<string * string * byte[]>()
                    fbStaticMap.[fbInstance].Add((staticVarName, dataType, valueBytes))

                // Always store as regular variable (preserves all data)
                regularVars <- {
                    RetainVariable.Name = name
                    Area = slot.Area.Prefix.TrimEnd(':')
                    DataType = TypeHelpers.getTypeName slot.DataType
                    ValueBytes = RetainValueSerializer.serialize slot.Value slot.DataType
                } :: regularVars

        // FBStaticData 리스트 생성
        let fbStaticDataList =
            fbStaticMap
            |> Seq.map (fun (KeyValue(fbInstance, staticVars)) ->
                let varsList =
                    staticVars
                    |> Seq.map (fun (name, dataType, valueBytes) ->
                        {
                            FBStaticVariable.Name = name
                            DataType = dataType
                            ValueBytes = valueBytes
                        })
                    |> Seq.toList
                {
                    FBStaticData.InstanceName = fbInstance
                    Variables = varsList
                })
            |> Seq.toList

        {
            RetainSnapshot.Timestamp = DateTime.UtcNow
            Version = RetainDefaults.CurrentVersion
            Variables = List.rev regularVars  // reverse to maintain original order
            FBStaticData = fbStaticDataList
            Checksum = ""  // Will be computed by BinaryRetainStorage.Save()
        }

    /// <summary>리테인 스냅샷에서 변수 값 복원</summary>
    /// <param name="snapshot">복원할 RetainSnapshot</param>
    /// <remarks>
    /// 스냅샷에 저장된 리테인 변수 값을 메모리에 복원합니다.
    /// - 버전 불일치 시 경고하지만 복원 시도
    /// - 타입 일치하는 변수만 복원
    /// - 존재하지 않거나 Retain이 아닌 변수는 무시
    /// </remarks>
    member _.RestoreFromSnapshot(snapshot: RetainSnapshot) =
        // MEDIUM FIX: Version metadata check with warning (RuntimeSpec.md:80-83)
        if snapshot.Version <> RetainDefaults.CurrentVersion then
            eprintfn "[RETAIN] Version mismatch: snapshot v%d, runtime v%d - compatibility issues may occur"
                snapshot.Version RetainDefaults.CurrentVersion

        // 일반 retain 변수 복원
        for retainVar in snapshot.Variables do
            match tryFindSlot retainVar.Name with
            | Some slot when slot.IsRetain ->
                // CRITICAL FIX (DEFECT-015-3): Area comparison was wrong - snapshot.Area has no colon
                // slot.Area.Prefix = "I:", "O:", "L:", "V:" (with colon)
                // retainVar.Area = "I", "O", "L", "V" (without colon)
                let slotArea = slot.Area.Prefix.TrimEnd(':')
                if slotArea = retainVar.Area then
                    // 타입 확인
                    let expectedType = TypeHelpers.getTypeName slot.DataType
                    if expectedType = retainVar.DataType then
                        let restoredValue = RetainValueSerializer.deserialize retainVar.ValueBytes slot.DataType
                        // Record change and update history for proper tracking
                        if recordChange retainVar.Name restoredValue then
                            appendHistory retainVar.Name restoredValue
                        slot.Value <- restoredValue
            | _ -> ()  // 변수가 없거나, Retain이 아니면 무시

        // FB Static 변수 복원
        for fbStatic in snapshot.FBStaticData do
            for fbVar in fbStatic.Variables do
                // "FBInstanceName_staticVarName" 형식으로 변수 이름 구성
                let fullName = sprintf "%s_%s" fbStatic.InstanceName fbVar.Name
                match tryFindSlot fullName with
                | Some slot when slot.IsRetain ->
                    // 타입 검증으로 버전 호환성 확인
                    let expectedType = TypeHelpers.getTypeName slot.DataType
                    if expectedType = fbVar.DataType then
                        let restoredValue = RetainValueSerializer.deserialize fbVar.ValueBytes slot.DataType
                        if recordChange fullName restoredValue then
                            appendHistory fullName restoredValue
                        slot.Value <- restoredValue
                    // else: 타입 불일치 - 무시 (버전 변경 등)
                | _ -> ()  // 변수가 없거나 Retain이 아니면 무시

    // ─────────────────────────────────────────────
    // 스키마 기반 고성능 Retain 지원
    // ─────────────────────────────────────────────

    /// <summary>현재 retain 변수 구조로부터 스키마 빌드</summary>
    /// <returns>RetainSchema 객체</returns>
    member _.BuildSchema() : RetainSchema =
        let mutable offset = 0
        let mutable variables = []
        let mutable fbStatic = []

        // 일반 retain 변수들 수집 (정렬된 순서)
        let retainVars =
            entries.Values
            |> Seq.filter (fun slot -> slot.IsRetain)
            |> Seq.sortBy (fun slot -> slot.Name)
            |> Seq.toList

        // 변수 스키마 생성
        for slot in retainVars do
            // FB Static 변수는 별도 처리 ("InstanceName_VarName" 형식)
            if not (slot.Name.Contains("_")) then
                let size = TypeSizes.getSize slot.DataType
                let varSchema = {
                    VariableSchema.Name = slot.Name
                    Area = slot.Area.Prefix.TrimEnd(':')
                    DataType = TypeHelpers.getTypeName slot.DataType
                    Offset = offset
                    Size = size
                }
                variables <- varSchema :: variables
                offset <- offset + size

        // FB Static 변수들 수집
        let fbStaticVars =
            entries.Values
            |> Seq.filter (fun slot -> slot.IsRetain && slot.Name.Contains("_"))
            |> Seq.sortBy (fun slot -> slot.Name)
            |> Seq.toList

        // FB 인스턴스별로 그룹화
        let fbGroups =
            fbStaticVars
            |> List.groupBy (fun slot ->
                let parts = slot.Name.Split('_')
                if parts.Length >= 2 then parts.[0] else "Unknown")

        for (instanceName, vars) in fbGroups do
            let mutable fbVars = []
            for slot in vars do
                let varName = slot.Name.Substring(instanceName.Length + 1)
                let size = TypeSizes.getSize slot.DataType
                let fbVarSchema = {
                    FBStaticVariableSchema.Name = varName
                    DataType = TypeHelpers.getTypeName slot.DataType
                    Offset = offset
                    Size = size
                }
                fbVars <- fbVarSchema :: fbVars
                offset <- offset + size

            let fbSchema = {
                FBStaticInstanceSchema.InstanceName = instanceName
                Variables = List.rev fbVars
            }
            fbStatic <- fbSchema :: fbStatic

        // 스키마 생성
        let schema = {
            RetainSchema.Version = RetainDefaults.CurrentVersion
            SchemaHash = "" // 임시, 곧 계산됨
            Timestamp = DateTime.Now
            TotalSize = offset
            Variables = List.rev variables
            FBStatic = List.rev fbStatic
        }

        // 스키마 해시 계산
        let schemaHash = SchemaHasher.computeSchemaHash schema
        { schema with SchemaHash = schemaHash }

    /// <summary>스키마 변경 여부 확인</summary>
    /// <returns>true if schema was modified, false otherwise</returns>
    member _.IsSchemaModified() = isSchemaModified

    /// <summary>스키마 변경 플래그 리셋</summary>
    member _.ResetSchemaModifiedFlag() = isSchemaModified <- false

    /// <summary>현재 스키마 해시 설정</summary>
    /// <param name="hash">스키마 해시</param>
    member _.SetCurrentSchemaHash(hash: string) = currentSchemaHash <- Some hash

    /// <summary>현재 스키마 해시 가져오기</summary>
    /// <returns>현재 스키마 해시 (없으면 None)</returns>
    member _.GetCurrentSchemaHash() = currentSchemaHash