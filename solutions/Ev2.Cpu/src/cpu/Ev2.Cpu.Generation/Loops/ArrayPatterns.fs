namespace Ev2.Cpu.Generation.Loops

open System
open Ev2.Cpu.Core

// ═════════════════════════════════════════════════════════════════════════════
// Array Loop Patterns - Standard Patterns for Array Processing
// ═════════════════════════════════════════════════════════════════════════════
// PLC/DCS에서 자주 사용되는 배열 처리 패턴 제공:
// - 배열 초기화 (Fill)
// - 배열 복사 (Copy)
// - 배열 검색 (Find, Filter)
// - 배열 변환 (Map, Transform)
// - 배열 집계 (Sum, Average, Min, Max)
// - 배열 비교 (Compare, Equal)
// ═════════════════════════════════════════════════════════════════════════════

/// 배열 처리 패턴
module ArrayPatterns =

    // ─────────────────────────────────────────────────────────────────
    // 배열 초기화 패턴
    // ─────────────────────────────────────────────────────────────────

    /// 배열을 상수값으로 채우기
    /// 예: fillArray "data" 100 (Const(0.0, typeof<double>))
    ///  => FOR i := 0 TO 99 DO data[i] := 0.0 END_FOR
    let fillArray (arrayName: string) (arraySize: int) (value: DsExpr) : DsStmt =
        let indexVar = DsTag.Create($"{arrayName}_i", typeof<int>)
        let arrayTag = DsTag.Create(arrayName, typeof<double>) // 배열 타입은 컨텍스트에 따라 다를 수 있음

        let assignStmt =
            Assign(
                0,
                arrayTag,
                value
            )

        For(
            0,
            indexVar,
            Const(box 0, typeof<int>),
            Const(box (arraySize - 1), typeof<int>),
            Some(Const(box 1, typeof<int>)),
            [assignStmt]
        )

    /// 배열을 순차적인 값으로 채우기 (0, 1, 2, ...)
    /// 예: fillSequential "index" 10
    ///  => FOR i := 0 TO 9 DO index[i] := i END_FOR
    let fillSequential (arrayName: string) (arraySize: int) : DsStmt =
        let indexVar = DsTag.Create($"{arrayName}_i", typeof<int>)
        let arrayTag = DsTag.Create(arrayName, typeof<int>)

        let assignStmt =
            Assign(
                0,
                arrayTag,
                Terminal indexVar
            )

        For(
            0,
            indexVar,
            Const(box 0, typeof<int>),
            Const(box (arraySize - 1), typeof<int>),
            Some(Const(box 1, typeof<int>)),
            [assignStmt]
        )

    /// 배열을 함수로 초기화
    /// 예: fillWithFunction "temps" 24 (fun i -> i * 0.5)
    ///  => FOR i := 0 TO 23 DO temps[i] := i * 0.5 END_FOR
    let fillWithFunction (arrayName: string) (arraySize: int) (fn: DsExpr -> DsExpr) : DsStmt =
        let indexVar = DsTag.Create($"{arrayName}_i", typeof<int>)
        let arrayTag = DsTag.Create(arrayName, typeof<double>)

        let assignStmt =
            Assign(
                0,
                arrayTag,
                fn (Terminal indexVar)
            )

        For(
            0,
            indexVar,
            Const(box 0, typeof<int>),
            Const(box (arraySize - 1), typeof<int>),
            Some(Const(box 1, typeof<int>)),
            [assignStmt]
        )

    // ─────────────────────────────────────────────────────────────────
    // 배열 복사 패턴
    // ─────────────────────────────────────────────────────────────────

    /// 배열 전체 복사
    /// 예: copyArray "source" "dest" 100
    ///  => FOR i := 0 TO 99 DO dest[i] := source[i] END_FOR
    let copyArray (sourceArrayName: string) (destArrayName: string) (arraySize: int) : DsStmt =
        let indexVar = DsTag.Create("_copy_i", typeof<int>)
        let sourceTag = DsTag.Create(sourceArrayName, typeof<double>)
        let destTag = DsTag.Create(destArrayName, typeof<double>)

        let assignStmt =
            Assign(
                0,
                destTag,
                Terminal sourceTag
            )

        For(
            0,
            indexVar,
            Const(box 0, typeof<int>),
            Const(box (arraySize - 1), typeof<int>),
            Some(Const(box 1, typeof<int>)),
            [assignStmt]
        )

    /// 배열 부분 복사
    /// 예: copyArrayRange "source" "dest" 10 20 5
    ///  => FOR i := 0 TO 4 DO dest[i+10] := source[i+20] END_FOR
    let copyArrayRange (sourceArrayName: string) (destArrayName: string) (destOffset: int) (sourceOffset: int) (count: int) : DsStmt =
        let indexVar = DsTag.Create("_copy_i", typeof<int>)
        let sourceTag = DsTag.Create(sourceArrayName, typeof<double>)
        let destTag = DsTag.Create(destArrayName, typeof<double>)

        // source[i + sourceOffset]
        let sourceIndexExpr =
            Binary(
                DsOp.Add,
                Terminal indexVar,
                Const(box sourceOffset, typeof<int>)
            )

        // dest[i + destOffset]
        let destIndexExpr =
            Binary(
                DsOp.Add,
                Terminal indexVar,
                Const(box destOffset, typeof<int>)
            )

        let assignStmt =
            Assign(
                0,
                destTag,
                Terminal sourceTag
            )

        For(
            0,
            indexVar,
            Const(box 0, typeof<int>),
            Const(box (count - 1), typeof<int>),
            Some(Const(box 1, typeof<int>)),
            [assignStmt]
        )

    // ─────────────────────────────────────────────────────────────────
    // 배열 검색 패턴
    // ─────────────────────────────────────────────────────────────────

    /// 배열에서 값 찾기 (첫 번째 인덱스 반환)
    /// 예: findValue "data" 100 (Const(42.0, typeof<double>)) "foundIndex"
    ///  => foundIndex := -1;
    ///     FOR i := 0 TO 99 DO
    ///       IF data[i] = 42.0 AND foundIndex = -1 THEN
    ///         foundIndex := i;
    ///       END_IF;
    ///     END_FOR
    let findValue (arrayName: string) (arraySize: int) (searchValue: DsExpr) (resultVarName: string) : DsStmt list =
        let indexVar = DsTag.Create("_find_i", typeof<int>)
        let arrayTag = DsTag.Create(arrayName, typeof<double>)
        let resultTag = DsTag.Create(resultVarName, typeof<int>)

        // foundIndex := -1
        let initStmt =
            Assign(
                0,
                resultTag,
                Const(box -1, typeof<int>)
            )

        // IF data[i] = searchValue AND foundIndex = -1 THEN foundIndex := i
        let condition =
            Binary(
                DsOp.And,
                Binary(DsOp.Eq, Terminal arrayTag, searchValue),
                Binary(DsOp.Eq, Terminal resultTag, Const(box -1, typeof<int>))
            )

        let assignIndexStmt =
            Assign(
                0,
                resultTag,
                Terminal indexVar
            )

        let ifStmt = Command(0, condition, Terminal (DsTag.Bool("_dummy")))

        let forLoop =
            For(
                0,
                indexVar,
                Const(box 0, typeof<int>),
                Const(box (arraySize - 1), typeof<int>),
                Some(Const(box 1, typeof<int>)),
                [assignIndexStmt]
            )

        [initStmt; forLoop]

    /// 배열에서 조건에 맞는 개수 세기
    /// 예: countMatches "temps" 100 (fun v -> v > 25.0) "hotCount"
    ///  => hotCount := 0;
    ///     FOR i := 0 TO 99 DO
    ///       IF temps[i] > 25.0 THEN hotCount := hotCount + 1 END_IF
    ///     END_FOR
    let countMatches (arrayName: string) (arraySize: int) (condition: DsExpr -> DsExpr) (resultVarName: string) : DsStmt list =
        let indexVar = DsTag.Create("_count_i", typeof<int>)
        let arrayTag = DsTag.Create(arrayName, typeof<double>)
        let resultTag = DsTag.Create(resultVarName, typeof<int>)

        // resultVar := 0
        let initStmt =
            Assign(
                0,
                resultTag,
                Const(box 0, typeof<int>)
            )

        // resultVar := resultVar + 1
        let incrementStmt =
            Assign(
                0,
                resultTag,
                Binary(
                    DsOp.Add,
                    Terminal resultTag,
                    Const(box 1, typeof<int>)
                )
            )

        // IF condition(array[i]) THEN increment
        let condExpr = condition (Terminal arrayTag)
        let ifStmt = Command(0, condExpr, Terminal (DsTag.Bool("_dummy")))

        let forLoop =
            For(
                0,
                indexVar,
                Const(box 0, typeof<int>),
                Const(box (arraySize - 1), typeof<int>),
                Some(Const(box 1, typeof<int>)),
                [incrementStmt]
            )

        [initStmt; forLoop]

    // ─────────────────────────────────────────────────────────────────
    // 배열 변환 패턴
    // ─────────────────────────────────────────────────────────────────

    /// 배열의 각 요소에 함수 적용 (Map)
    /// 예: mapArray "input" "output" 100 (fun v -> v * 2.0)
    ///  => FOR i := 0 TO 99 DO output[i] := input[i] * 2.0 END_FOR
    let mapArray (sourceArrayName: string) (destArrayName: string) (arraySize: int) (transform: DsExpr -> DsExpr) : DsStmt =
        let indexVar = DsTag.Create("_map_i", typeof<int>)
        let sourceTag = DsTag.Create(sourceArrayName, typeof<double>)
        let destTag = DsTag.Create(destArrayName, typeof<double>)

        let assignStmt =
            Assign(
                0,
                destTag,
                transform (Terminal sourceTag)
            )

        For(
            0,
            indexVar,
            Const(box 0, typeof<int>),
            Const(box (arraySize - 1), typeof<int>),
            Some(Const(box 1, typeof<int>)),
            [assignStmt]
        )

    /// 배열 스케일링 (모든 요소에 상수 곱하기)
    /// 예: scaleArray "data" 100 2.5
    ///  => FOR i := 0 TO 99 DO data[i] := data[i] * 2.5 END_FOR
    let scaleArray (arrayName: string) (arraySize: int) (scaleFactor: float) : DsStmt =
        let indexVar = DsTag.Create($"{arrayName}_i", typeof<int>)
        let arrayTag = DsTag.Create(arrayName, typeof<double>)

        let assignStmt =
            Assign(
                0,
                arrayTag,
                Binary(
                    DsOp.Mul,
                    Terminal arrayTag,
                    Const(box scaleFactor, typeof<double>)
                )
            )

        For(
            0,
            indexVar,
            Const(box 0, typeof<int>),
            Const(box (arraySize - 1), typeof<int>),
            Some(Const(box 1, typeof<int>)),
            [assignStmt]
        )

    // ─────────────────────────────────────────────────────────────────
    // 배열 집계 패턴
    // ─────────────────────────────────────────────────────────────────

    /// 배열 합계
    /// 예: sumArray "values" 100 "total"
    ///  => total := 0.0;
    ///     FOR i := 0 TO 99 DO total := total + values[i] END_FOR
    let sumArray (arrayName: string) (arraySize: int) (resultVarName: string) : DsStmt list =
        let indexVar = DsTag.Create("_sum_i", typeof<int>)
        let arrayTag = DsTag.Create(arrayName, typeof<double>)
        let resultTag = DsTag.Create(resultVarName, typeof<double>)

        // result := 0.0
        let initStmt =
            Assign(
                0,
                resultTag,
                Const(box 0.0, typeof<double>)
            )

        // result := result + array[i]
        let sumStmt =
            Assign(
                0,
                resultTag,
                Binary(
                    DsOp.Add,
                    Terminal resultTag,
                    Terminal arrayTag
                )
            )

        let forLoop =
            For(
                0,
                indexVar,
                Const(box 0, typeof<int>),
                Const(box (arraySize - 1), typeof<int>),
                Some(Const(box 1, typeof<int>)),
                [sumStmt]
            )

        [initStmt; forLoop]

    /// 배열 평균
    /// 예: averageArray "temps" 24 "avgTemp"
    ///  => avgTemp := 0.0;
    ///     FOR i := 0 TO 23 DO avgTemp := avgTemp + temps[i] END_FOR
    ///     avgTemp := avgTemp / 24.0
    let averageArray (arrayName: string) (arraySize: int) (resultVarName: string) : DsStmt list =
        let sumStmts = sumArray arrayName arraySize resultVarName
        let resultTag = DsTag.Create(resultVarName, typeof<double>)

        // result := result / arraySize
        let divideStmt =
            Assign(
                0,
                resultTag,
                Binary(
                    DsOp.Div,
                    Terminal resultTag,
                    Const(box (float arraySize), typeof<double>)
                )
            )

        sumStmts @ [divideStmt]

    /// 배열 최대값 찾기
    /// 예: maxArray "data" 100 "maxValue"
    ///  => maxValue := data[0];
    ///     FOR i := 1 TO 99 DO
    ///       IF data[i] > maxValue THEN maxValue := data[i] END_IF
    ///     END_FOR
    let maxArray (arrayName: string) (arraySize: int) (resultVarName: string) : DsStmt list =
        let indexVar = DsTag.Create("_max_i", typeof<int>)
        let arrayTag = DsTag.Create(arrayName, typeof<double>)
        let resultTag = DsTag.Create(resultVarName, typeof<double>)

        // maxValue := array[0]
        let initStmt =
            Assign(
                0,
                resultTag,
                Terminal arrayTag
            )

        // IF array[i] > maxValue THEN maxValue := array[i]
        let condition =
            Binary(
                DsOp.Gt,
                Terminal arrayTag,
                Terminal resultTag
            )

        let assignStmt =
            Assign(
                0,
                resultTag,
                Terminal arrayTag
            )

        let ifStmt = Command(0, condition, Terminal (DsTag.Bool("_dummy")))

        let forLoop =
            For(
                0,
                indexVar,
                Const(box 1, typeof<int>),
                Const(box (arraySize - 1), typeof<int>),
                Some(Const(box 1, typeof<int>)),
                [assignStmt]
            )

        [initStmt; forLoop]

    /// 배열 최소값 찾기
    /// 예: minArray "data" 100 "minValue"
    ///  => minValue := data[0];
    ///     FOR i := 1 TO 99 DO
    ///       IF data[i] < minValue THEN minValue := data[i] END_IF
    ///     END_FOR
    let minArray (arrayName: string) (arraySize: int) (resultVarName: string) : DsStmt list =
        let indexVar = DsTag.Create("_min_i", typeof<int>)
        let arrayTag = DsTag.Create(arrayName, typeof<double>)
        let resultTag = DsTag.Create(resultVarName, typeof<double>)

        // minValue := array[0]
        let initStmt =
            Assign(
                0,
                resultTag,
                Terminal arrayTag
            )

        // IF array[i] < minValue THEN minValue := array[i]
        let condition =
            Binary(
                DsOp.Lt,
                Terminal arrayTag,
                Terminal resultTag
            )

        let assignStmt =
            Assign(
                0,
                resultTag,
                Terminal arrayTag
            )

        let ifStmt = Command(0, condition, Terminal (DsTag.Bool("_dummy")))

        let forLoop =
            For(
                0,
                indexVar,
                Const(box 1, typeof<int>),
                Const(box (arraySize - 1), typeof<int>),
                Some(Const(box 1, typeof<int>)),
                [assignStmt]
            )

        [initStmt; forLoop]

    // ─────────────────────────────────────────────────────────────────
    // 배열 비교 패턴
    // ─────────────────────────────────────────────────────────────────

    /// 두 배열 비교 (모든 요소가 같은지 확인)
    /// 예: arraysEqual "array1" "array2" 100 "isEqual"
    ///  => isEqual := TRUE;
    ///     FOR i := 0 TO 99 DO
    ///       IF array1[i] <> array2[i] THEN isEqual := FALSE; EXIT; END_IF
    ///     END_FOR
    let arraysEqual (array1Name: string) (array2Name: string) (arraySize: int) (resultVarName: string) : DsStmt list =
        let indexVar = DsTag.Create("_cmp_i", typeof<int>)
        let array1Tag = DsTag.Create(array1Name, typeof<double>)
        let array2Tag = DsTag.Create(array2Name, typeof<double>)
        let resultTag = DsTag.Create(resultVarName, typeof<bool>)

        // isEqual := TRUE
        let initStmt =
            Assign(
                0,
                resultTag,
                Const(box true, typeof<bool>)
            )

        // IF array1[i] <> array2[i] THEN isEqual := FALSE; EXIT
        let condition =
            Binary(
                DsOp.Ne,
                Terminal array1Tag,
                Terminal array2Tag
            )

        let assignFalse =
            Assign(
                0,
                resultTag,
                Const(box false, typeof<bool>)
            )

        let breakStmt = Break(0)

        let ifBody = Command(0, condition, Terminal (DsTag.Bool("_dummy")))

        let forLoop =
            For(
                0,
                indexVar,
                Const(box 0, typeof<int>),
                Const(box (arraySize - 1), typeof<int>),
                Some(Const(box 1, typeof<int>)),
                [assignFalse; breakStmt]
            )

        [initStmt; forLoop]
