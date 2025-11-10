namespace Ev2.Cpu.Runtime

open System
open Ev2.Cpu.Core
open System.Collections.Generic
open System.Threading

// ─────────────────────────────────────────────────────────────────────
// Function Common Utilities
// ─────────────────────────────────────────────────────────────────────
// 모든 내장 함수에서 공통으로 사용하는 유틸리티
// ─────────────────────────────────────────────────────────────────────

[<AutoOpen>]
module FunctionCommon =

    /// Epsilon for floating point comparisons
    let eps = 1e-10

    /// Thread-local random number generator (thread-safe)
    let private rngThreadLocal = new ThreadLocal<Random>(fun () -> Random())

    /// Get thread-local random number generator (NEW-DEFECT-001 fix)
    /// Returns the Random instance for the current thread
    let getRng() = rngThreadLocal.Value

    /// String caching for performance optimization
    let private stringCache = Dictionary<int, struct (obj * string)>(128)
    let private cacheLock = obj()

    let cachedToString (value: obj) =
        if isNull value then ""
        else
            let hash = value.GetHashCode()
            lock cacheLock (fun () ->
                match stringCache.TryGetValue(hash) with
                | true, struct(cachedObj, cachedStr) when Object.Equals(cachedObj, value) ->
                    cachedStr
                | _ ->
                    let str = TypeHelpers.toString value
                    if stringCache.Count >= RuntimeLimits.Current.StringCacheSize then  // NEW-DEFECT-002 fix: configurable
                        stringCache.Clear()
                    stringCache.[hash] <- struct(value, str)
                    str)

    /// Check if object is null
    let inline isNull (o: obj) = obj.ReferenceEquals(o, null)

    // ─────────────────────────────────────────────────────────────────────
    // Exception-based validation (deprecated - use Result versions)
    // ─────────────────────────────────────────────────────────────────────

    /// Validate argument count (throws exception)
    let validateArgCount expected actual funcName =
        if actual <> expected then
            failwithf "%s requires %d argument%s" funcName expected (if expected = 1 then "" else "s")

    /// Validate argument count range (throws exception)
    let validateArgCountRange minArgs maxArgs actual funcName =
        if actual < minArgs || actual > maxArgs then
            failwithf "%s requires %d-%d arguments" funcName minArgs maxArgs

    // ─────────────────────────────────────────────────────────────────────
    // Result-based validation (preferred)
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 인자 개수 검증 (Result 반환) - 안전한 오류 처리
    /// </summary>
    /// <param name="expected">예상 인자 개수</param>
    /// <param name="actual">실제 인자 개수</param>
    /// <param name="funcName">함수 이름</param>
    /// <returns>검증 성공 시 Ok (), 실패 시 Error 메시지</returns>
    let validateArgCountResult (expected: int) (actual: int) (funcName: string) : Result<unit, string> =
        if actual <> expected then
            Error (sprintf "%s requires %d argument%s, got %d" funcName expected (if expected = 1 then "" else "s") actual)
        else
            Ok ()

    /// <summary>
    /// 인자 개수 범위 검증 (Result 반환) - 안전한 오류 처리
    /// </summary>
    /// <param name="minArgs">최소 인자 개수</param>
    /// <param name="maxArgs">최대 인자 개수</param>
    /// <param name="actual">실제 인자 개수</param>
    /// <param name="funcName">함수 이름</param>
    /// <returns>검증 성공 시 Ok (), 실패 시 Error 메시지</returns>
    let validateArgCountRangeResult (minArgs: int) (maxArgs: int) (actual: int) (funcName: string) : Result<unit, string> =
        if actual < minArgs || actual > maxArgs then
            Error (sprintf "%s requires %d-%d arguments, got %d" funcName minArgs maxArgs actual)
        else
            Ok ()

    /// <summary>
    /// 정확히 N개의 인자 추출 헬퍼
    /// </summary>
    let extractArgs (count: int) (args: 'a list) (funcName: string) : Result<'a list, string> =
        if args.Length <> count then
            Error (sprintf "%s requires %d argument%s, got %d" funcName count (if count = 1 then "" else "s") args.Length)
        else
            Ok args

    /// <summary>
    /// 1개의 인자 추출 헬퍼
    /// </summary>
    let extractArg1 (args: 'a list) (funcName: string) : Result<'a, string> =
        match args with
        | [a] -> Ok a
        | _ -> Error (sprintf "%s requires 1 argument, got %d" funcName args.Length)

    /// <summary>
    /// 2개의 인자 추출 헬퍼
    /// </summary>
    let extractArgs2 (args: 'a list) (funcName: string) : Result<'a * 'a, string> =
        match args with
        | [a; b] -> Ok (a, b)
        | _ -> Error (sprintf "%s requires 2 arguments, got %d" funcName args.Length)

    /// <summary>
    /// 3개의 인자 추출 헬퍼
    /// </summary>
    let extractArgs3 (args: 'a list) (funcName: string) : Result<'a * 'a * 'a, string> =
        match args with
        | [a; b; c] -> Ok (a, b, c)
        | _ -> Error (sprintf "%s requires 3 arguments, got %d" funcName args.Length)
