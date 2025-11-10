namespace Ev2.Cpu.Core.UserDefined

open System
open Ev2.Cpu.Core

// ═════════════════════════════════════════════════════════════════════════════
// Runtime Update - 런타임 중 코드 수정 지원
// ═════════════════════════════════════════════════════════════════════════════
// PLC 실행 중 UserFC/FB, Program.Body, FB 인스턴스를 안전하게 수정하는 메커니즘
// 스캔 사이클 경계에서 검증 후 원자적으로 적용하며, 실패 시 자동 롤백
// ═════════════════════════════════════════════════════════════════════════════

/// 런타임 업데이트 요청 타입
[<RequireQualifiedAccess>]
type UpdateRequest =
    /// UserFC 업데이트 (함수 정의 수정)
    | UpdateUserFC of fc: UserFC * validate: bool

    /// UserFB 업데이트 (함수 블록 정의 수정)
    | UpdateUserFB of fb: UserFB * validate: bool

    /// FB 인스턴스 업데이트 (인스턴스 설정 또는 Static 값 수정)
    | UpdateFBInstance of instance: FBInstance * validate: bool

    /// Program.Body 업데이트 (메인 실행 로직 수정)
    | UpdateProgramBody of body: DsStmt list * validate: bool

    /// 메모리 변수 값 직접 수정 (간단 케이스, 검증 불필요)
    | UpdateMemoryValue of name: string * value: obj

    /// 여러 업데이트를 하나의 트랜잭션으로 처리
    | BatchUpdate of requests: UpdateRequest list

    /// 업데이트 설명 문자열
    member this.Description =
        match this with
        | UpdateUserFC (fc, _) -> sprintf "Update UserFC '%s'" fc.Name
        | UpdateUserFB (fb, _) -> sprintf "Update UserFB '%s'" fb.Name
        | UpdateFBInstance (inst, _) -> sprintf "Update FB Instance '%s'" inst.Name
        | UpdateProgramBody (stmts, _) -> sprintf "Update Program.Body (%d statements)" stmts.Length
        | UpdateMemoryValue (name, _) -> sprintf "Update Memory '%s'" name
        | BatchUpdate (reqs) -> sprintf "Batch Update (%d requests)" reqs.Length

/// 업데이트 결과
[<RequireQualifiedAccess>]
type UpdateResult =
    /// 성공
    | Success of message: string

    /// 검증 실패 (적용되지 않음)
    | ValidationFailed of errors: UserDefinitionError list

    /// 적용 실패 (런타임 오류, 검증 통과 후 발생)
    | ApplyFailed of error: string

    /// 롤백됨 (적용 후 오류 발생하여 복구)
    | RolledBack of reason: string * originalError: string

    /// 부분 성공 (배치 업데이트에서 일부만 성공)
    | PartialSuccess of succeeded: int * failed: int * errors: string list

    /// 결과 메시지 포맷팅
    member this.Format() =
        match this with
        | Success msg -> sprintf "[SUCCESS] %s" msg
        | ValidationFailed errors ->
            let errorMsgs = errors |> List.map (fun e -> e.Format())
            sprintf "[VALIDATION FAILED]\n%s" (String.concat "\n" errorMsgs)
        | ApplyFailed error ->
            sprintf "[APPLY FAILED] %s" error
        | RolledBack (reason, originalError) ->
            sprintf "[ROLLED BACK] %s\nOriginal Error: %s" reason originalError
        | PartialSuccess (succeeded, failed, errors) ->
            sprintf "[PARTIAL SUCCESS] %d succeeded, %d failed\nErrors:\n%s"
                succeeded failed (String.concat "\n" errors)

/// 스냅샷 - 롤백을 위한 상태 저장
[<StructuralEquality; NoComparison>]
type RuntimeSnapshot = {
    /// 스냅샷 생성 시각
    Timestamp: DateTime

    /// 스냅샷 설명
    Description: string option

    /// UserFC 목록
    UserFCs: Map<string, UserFC>

    /// UserFB 목록
    UserFBs: Map<string, UserFB>

    /// FB 인스턴스 목록
    FBInstances: Map<string, FBInstance>

    /// Program.Body (문장 리스트)
    ProgramBody: DsStmt list option
} with
    static member Empty = {
        Timestamp = DateTime.UtcNow
        Description = None
        UserFCs = Map.empty
        UserFBs = Map.empty
        FBInstances = Map.empty
        ProgramBody = None
    }

    /// 스냅샷 요약 정보
    member this.Summary() =
        sprintf "Snapshot [%s] - FCs: %d, FBs: %d, Instances: %d, Body: %s"
            (this.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"))
            this.UserFCs.Count
            this.UserFBs.Count
            this.FBInstances.Count
            (match this.ProgramBody with Some b -> sprintf "%d stmts" b.Length | None -> "N/A")

/// 업데이트 설정
type UpdateConfig = {
    /// 검증 강제 여부 (기본: true)
    ForceValidation: bool

    /// 자동 롤백 활성화 (기본: true)
    AutoRollback: bool

    /// 스냅샷 히스토리 최대 개수 (기본: 10)
    MaxSnapshotHistory: int

    // MAJOR FIX (DEFECT-017-4): Removed unused UpdateTimeoutMs field
    // Timeout configuration already exists per-relay via RegisterWorkRelay/RegisterCallRelay
    // and UpdateWorkRelay/UpdateCallRelay in RelayStateManager
    // No clear use case for global update timeout vs per-relay timeouts
} with
    static member Default = {
        ForceValidation = true
        AutoRollback = true
        MaxSnapshotHistory = 10
    }

/// 업데이트 통계
type UpdateStatistics = {
    /// 총 업데이트 요청 수
    TotalRequests: int

    /// 성공한 업데이트 수
    SuccessCount: int

    /// 실패한 업데이트 수
    FailedCount: int

    /// 롤백된 업데이트 수
    RolledBackCount: int

    /// 마지막 업데이트 시각
    LastUpdateTime: DateTime option
} with
    static member Empty = {
        TotalRequests = 0
        SuccessCount = 0
        FailedCount = 0
        RolledBackCount = 0
        LastUpdateTime = None
    }

    member this.SuccessRate =
        if this.TotalRequests = 0 then 0.0
        else float this.SuccessCount / float this.TotalRequests * 100.0

/// 업데이트 이벤트
[<RequireQualifiedAccess>]
type UpdateEvent =
    /// 업데이트 요청됨
    | Requested of request: UpdateRequest * timestamp: DateTime

    /// 검증 시작
    | ValidationStarted of request: UpdateRequest * timestamp: DateTime

    /// 검증 완료
    | ValidationCompleted of request: UpdateRequest * result: Result<unit, UserDefinitionError list> * timestamp: DateTime

    /// 적용 시작
    | ApplyStarted of request: UpdateRequest * timestamp: DateTime

    /// 적용 완료
    | ApplyCompleted of request: UpdateRequest * result: UpdateResult * timestamp: DateTime

    /// 롤백 시작
    | RollbackStarted of reason: string * timestamp: DateTime

    /// 롤백 완료
    | RollbackCompleted of success: bool * timestamp: DateTime

    /// 이벤트 설명
    member this.Description =
        match this with
        | Requested (req, ts) -> sprintf "[%s] Requested: %s" (ts.ToString("HH:mm:ss.fff")) req.Description
        | ValidationStarted (req, ts) -> sprintf "[%s] Validation Started: %s" (ts.ToString("HH:mm:ss.fff")) req.Description
        | ValidationCompleted (req, result, ts) ->
            let status = match result with Ok _ -> "OK" | Error _ -> "FAILED"
            sprintf "[%s] Validation %s: %s" (ts.ToString("HH:mm:ss.fff")) status req.Description
        | ApplyStarted (req, ts) -> sprintf "[%s] Apply Started: %s" (ts.ToString("HH:mm:ss.fff")) req.Description
        | ApplyCompleted (req, result, ts) ->
            sprintf "[%s] Apply Completed: %s - %s" (ts.ToString("HH:mm:ss.fff")) req.Description (result.Format())
        | RollbackStarted (reason, ts) -> sprintf "[%s] Rollback Started: %s" (ts.ToString("HH:mm:ss.fff")) reason
        | RollbackCompleted (success, ts) -> sprintf "[%s] Rollback %s" (ts.ToString("HH:mm:ss.fff")) (if success then "Succeeded" else "Failed")

/// 업데이트 요청 헬퍼
module UpdateRequest =

    /// UserFC 업데이트 요청 생성
    let updateFC fc = UpdateRequest.UpdateUserFC(fc, true)

    /// UserFB 업데이트 요청 생성
    let updateFB fb = UpdateRequest.UpdateUserFB(fb, true)

    /// FB 인스턴스 업데이트 요청 생성
    let updateInstance inst = UpdateRequest.UpdateFBInstance(inst, true)

    /// Program.Body 업데이트 요청 생성
    let updateBody body = UpdateRequest.UpdateProgramBody(body, true)

    /// 메모리 값 업데이트 요청 생성
    let updateMemory name value = UpdateRequest.UpdateMemoryValue(name, value)

    /// 배치 업데이트 요청 생성
    let batch requests = UpdateRequest.BatchUpdate(requests)

/// 업데이트 결과 헬퍼
module UpdateResult =

    /// 성공 결과 생성
    let success msg = UpdateResult.Success(msg)

    /// 검증 실패 결과 생성
    let validationFailed errors = UpdateResult.ValidationFailed(errors)

    /// 롤백 결과 생성
    let rolledBack reason originalError = UpdateResult.RolledBack(reason, originalError)

    /// 부분 성공 결과 생성
    let partialSuccess succeeded failed errors = UpdateResult.PartialSuccess(succeeded, failed, errors)

    /// 여러 결과를 하나로 결합
    let combine (results: UpdateResult list) : UpdateResult =
        let isSuccess r =
            match r with
            | UpdateResult.Success _ -> true
            | _ -> false

        let successes = results |> List.filter isSuccess |> List.length
        let failures = results.Length - successes

        if failures = 0 then
            UpdateResult.Success(sprintf "All %d updates succeeded" results.Length)
        elif successes = 0 then
            let errors =
                results
                |> List.choose (fun r ->
                    match r with
                    | UpdateResult.ValidationFailed errs -> Some (errs |> List.map (fun e -> e.Format()))
                    | UpdateResult.RolledBack (reason, _) -> Some [reason]
                    | _ -> None)
                |> List.concat
            UpdateResult.ValidationFailed(
                errors |> List.map (fun msg ->
                    UserDefinitionError.create "Update.Combined" msg []))
        else
            let errorMsgs =
                results
                |> List.choose (fun r -> if isSuccess r then None else Some (r.Format()))
            UpdateResult.PartialSuccess(successes, failures, errorMsgs)
