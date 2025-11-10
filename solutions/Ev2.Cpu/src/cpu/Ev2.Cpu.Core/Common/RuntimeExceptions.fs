namespace Ev2.Cpu.Core.Common

open System

// ═════════════════════════════════════════════════════════════════════════════
// Runtime Exceptions
// ═════════════════════════════════════════════════════════════════════════════
// Critical 오류를 위한 커스텀 예외 타입들
// failwith/failwithf를 명확한 예외 타입으로 전환하여 오류 추적 및 처리 개선
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// 메모리 한계 초과 예외
/// </summary>
/// <remarks>
/// 최대 변수 개수(2000개)를 초과하여 변수를 선언하려고 할 때 발생합니다.
/// - OptimizedMemory: 2000개 한계
/// - MemoryPool: 설정된 한계
/// </remarks>
type MemoryLimitExceededException(message: string, ?innerException: Exception) =
    inherit Exception(message, Option.toObj innerException)

    new(maxVariables: int) =
        MemoryLimitExceededException($"Memory limit exceeded: maximum {maxVariables} variables allowed")

    new(maxVariables: int, currentCount: int) =
        MemoryLimitExceededException($"Memory limit exceeded: attempted to exceed {maxVariables} variables (current: {currentCount})")

/// <summary>
/// 0으로 나누기 예외 (정수)
/// </summary>
/// <remarks>
/// 정수 나눗셈에서 분모가 0일 때 발생합니다.
/// </remarks>
type IntegerDivisionByZeroException(message: string, ?innerException: Exception) =
    inherit Exception(message, Option.toObj innerException)

    new() =
        IntegerDivisionByZeroException("Division by zero: integer division with zero divisor")

    new(numerator: int) =
        IntegerDivisionByZeroException($"Division by zero: {numerator} / 0")

/// <summary>
/// 0으로 나누기 예외 (실수)
/// </summary>
/// <remarks>
/// 실수 나눗셈에서 분모가 0 또는 epsilon 이하일 때 발생합니다.
/// </remarks>
type FloatingPointDivisionByZeroException(message: string, ?innerException: Exception) =
    inherit Exception(message, Option.toObj innerException)

    new() =
        FloatingPointDivisionByZeroException("Division by zero: floating-point division with zero divisor")

    new(numerator: float) =
        FloatingPointDivisionByZeroException($"Division by zero: {numerator} / 0.0")

    new(numerator: float, epsilon: float) =
        FloatingPointDivisionByZeroException($"Division by zero: {numerator} / divisor (divisor <= {epsilon})")

/// <summary>
/// Modulo 연산에서 0으로 나누기 예외
/// </summary>
/// <remarks>
/// Modulo 연산(%)에서 분모가 0일 때 발생합니다.
/// </remarks>
type ModuloByZeroException(message: string, ?innerException: Exception) =
    inherit Exception(message, Option.toObj innerException)

    new() =
        ModuloByZeroException("Modulo by zero: modulo operation with zero divisor")

    new(numerator: int) =
        ModuloByZeroException($"Modulo by zero: {numerator} %% 0")

/// <summary>
/// 런타임 예외 헬퍼 함수들
/// </summary>
module RuntimeExceptions =

    /// <summary>메모리 한계 초과 예외 발생</summary>
    let raiseMemoryLimit maxVariables =
        raise (MemoryLimitExceededException(maxVariables))

    /// <summary>메모리 한계 초과 예외 발생 (현재 개수 포함)</summary>
    let raiseMemoryLimitWithCount (maxVariables: int) (currentCount: int) =
        raise (MemoryLimitExceededException(maxVariables, currentCount))

    /// <summary>정수 0으로 나누기 예외 발생</summary>
    let raiseIntDivisionByZero () =
        raise (IntegerDivisionByZeroException())

    /// <summary>정수 0으로 나누기 예외 발생 (분자 포함)</summary>
    let raiseIntDivisionByZeroWith numerator =
        raise (IntegerDivisionByZeroException(numerator))

    /// <summary>실수 0으로 나누기 예외 발생</summary>
    let raiseFloatDivisionByZero () =
        raise (FloatingPointDivisionByZeroException())

    /// <summary>실수 0으로 나누기 예외 발생 (분자 포함)</summary>
    let raiseFloatDivisionByZeroWith numerator =
        raise (FloatingPointDivisionByZeroException(numerator))

    /// <summary>실수 0으로 나누기 예외 발생 (epsilon 포함)</summary>
    let raiseFloatDivisionByZeroWithEpsilon (numerator: float) (epsilon: float) =
        raise (FloatingPointDivisionByZeroException(numerator, epsilon))

    /// <summary>Modulo 0 예외 발생</summary>
    let raiseModuloByZero () =
        raise (ModuloByZeroException())

    /// <summary>Modulo 0 예외 발생 (분자 포함)</summary>
    let raiseModuloByZeroWith numerator =
        raise (ModuloByZeroException(numerator))

    // ═════════════════════════════════════════════════════════════════════════════
    // 메모리 관리 오류 헬퍼 (표준 예외 사용)
    // ═════════════════════════════════════════════════════════════════════════════

    /// <summary>변수 중복 선언 예외 발생</summary>
    let raiseVariableAlreadyDeclared (name: string) (existingInfo: string) (requestedInfo: string) =
        raise (InvalidOperationException($"Variable '{name}' already declared as {existingInfo} but requested {requestedInfo}"))

    /// <summary>변수 중복 선언 예외 발생 (영역 불일치)</summary>
    let raiseVariableAlreadyDeclaredInArea (name: string) (existingArea: string) (requestedArea: string) =
        raise (InvalidOperationException($"Variable '{name}' already declared in {existingArea} area. Cannot re-declare as {requestedArea}."))

    /// <summary>선언되지 않은 변수 접근 예외 발생</summary>
    let raiseVariableNotDeclared (name: string) =
        raise (InvalidOperationException($"Cannot set undeclared variable '{name}'. Use Declare to create variables explicitly."))

    /// <summary>쓰기 불가능한 변수 예외 발생</summary>
    let raiseCannotWriteToVariable (name: string) =
        raise (InvalidOperationException($"Cannot write to {name}"))

    /// <summary>입력 변수 쓰기 시도 예외 발생</summary>
    let raiseCannotWriteToInput (name: string) =
        raise (InvalidOperationException($"Cannot write to input: {name}"))

    // ═════════════════════════════════════════════════════════════════════════════
    // 루프 관련 오류 헬퍼
    // ═════════════════════════════════════════════════════════════════════════════

    /// <summary>루프 스택 오버플로우 예외 발생</summary>
    let raiseLoopStackOverflow (currentDepth: int) (maxDepth: int) =
        raise (InvalidOperationException($"Loop stack overflow: nesting depth {currentDepth} exceeds maximum {maxDepth}"))

    /// <summary>루프 반복 한계 초과 예외 발생</summary>
    let raiseLoopIterationLimit (currentIteration: int) (maxIterations: int) =
        raise (InvalidOperationException($"Loop iteration limit exceeded: {currentIteration} iterations exceeds maximum {maxIterations}"))

    /// <summary>루프 타임아웃 예외 발생</summary>
    let raiseLoopTimeout (elapsedMs: int) (timeoutMs: int) =
        raise (TimeoutException($"Loop execution timeout: {elapsedMs}ms exceeds limit {timeoutMs}ms"))
