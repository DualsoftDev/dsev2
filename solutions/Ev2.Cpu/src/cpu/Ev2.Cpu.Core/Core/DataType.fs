namespace Ev2.Cpu.Core

open System
open System.Text.RegularExpressions
open System.Globalization
open System.Collections.Generic

// ─────────────────────────────────────────────────────────────────────
// 통합 CPU 데이터 타입 시스템 (Unified CPU Data Type System)
// ─────────────────────────────────────────────────────────────────────
// PLC/DCS 시스템에서 사용되는 기본 데이터 타입들을 정의합니다.
// 모든 변수, 상수, 연산 결과는 이 4가지 타입 중 하나여야 합니다.
// ─────────────────────────────────────────────────────────────────────

/// CPU 데이터 타입 정의 (4개 기본 타입)
/// - TBool: 논리값 (TRUE/FALSE, 디지털 I/O, 릴레이 상태)
/// - TInt: 32비트 정수 (카운터, 타이머 프리셋, 아날로그 값)  
/// - TDouble: 64비트 실수 (계산 결과, 공학 단위 변환)
/// - TString: 문자열 (메시지, 경보 텍스트, 설정 정보)
[<StructuralEquality; NoComparison>]
type DsDataType =
    | TBool     // 논리값: 디지털 I/O, 릴레이, 플래그
    | TInt      // 정수: 카운터, 타이머, 아날로그 입력값
    | TDouble   // 실수: 계산 결과, 공학 단위, 스케일링
    | TString   // 문자열: 메시지, 경보, HMI 표시
    with
        /// .NET 런타임 타입으로 매핑 (CLR interop)
        /// PLC 타입을 .NET 타입으로 변환하여 메모리에서 저장/처리
        member this.DotNetType : Type =
            match this with
            | TBool   -> typeof<bool>    // System.Boolean
            | TInt    -> typeof<int>     // System.Int32 (-2^31 ~ 2^31-1)
            | TDouble -> typeof<double>  // System.Double (IEEE 754)
            | TString -> typeof<string>  // System.String (UTF-16)

        /// 타입별 기본값 반환 (초기화 시 사용)
        /// 변수 선언 시 명시적 값이 없으면 이 값으로 초기화
        member this.DefaultValue : obj =
            match this with
            | TBool   -> box false       // 논리값: 거짓
            | TInt    -> box 0           // 정수: 영
            | TDouble -> box 0.0         // 실수: 영점영
            | TString -> box ""          // 문자열: 빈 문자열

        /// 런타임 값의 타입 검증 (타입 안전성 보장)
        /// obj 값이 이 타입과 호환되는지 확인하고 검증된 값 반환
        member this.Validate(value: obj) : obj =
            if isNull value then
                invalidArg "value" "Value cannot be null"
            let actual = value.GetType()
            let expected = this.DotNetType
            if actual = expected then
                value
            // HIGH FIX: Use IsCompatibleWith for type promotion (Int → Double)
            elif actual = typeof<int> && this = TDouble then
                box (double (unbox<int> value))  // Promote int to double
            else
                failwithf "Type mismatch: expected %s but got %s" expected.FullName actual.FullName

        /// 타입 호환성 검사 (자동 타입 승격 규칙 포함)
        /// PLC에서 일반적으로 지원하는 타입 변환 규칙:
        /// - Int → Double: 정수를 실수로 자동 승격 (정밀도 유지)
        /// - 같은 타입끼리는 항상 호환 가능
        member this.IsCompatibleWith(other: DsDataType) =
            match this, other with
            | x, y when x = y -> true     // 동일 타입
            | TDouble, TInt -> true       // 정수→실수 승격 허용 (Int can be assigned to Double)
            | _ -> false                  // 기타 변환 금지

        /// 수치 타입 여부 확인 (산술 연산 가능성)
        /// 산술 연산자(+, -, *, /, MOD)가 적용 가능한 타입인지 검사
        member this.IsNumeric =
            match this with
            | TInt | TDouble -> true      // 정수, 실수: 산술 연산 가능
            | _ -> false                  // 논리값, 문자열: 산술 연산 불가

        /// 사람이 읽기 쉬운 타입 이름 반환 (디버깅/로깅 용도)
        override this.ToString() =
            match this with
            | TBool -> "Bool"       // 논리값
            | TInt -> "Int"         // 정수 
            | TDouble -> "Double"   // 실수
            | TString -> "String"   // 문자열

        /// .NET 타입에서 DsDataType 생성
        static member OfType(t: Type) =
            if isNull t then invalidArg "t" "Type cannot be null"
            elif t = typeof<bool>   then TBool
            elif t = typeof<int>    then TInt
            elif t = typeof<double> then TDouble
            elif t = typeof<float>  then TDouble
            elif t = typeof<string> then TString
            else invalidArg "t" (sprintf "Unsupported .NET type: %s" t.FullName)

        /// 문자열에서 타입 파싱 시도
        static member TryParse(name: string) =
            if String.IsNullOrWhiteSpace name then None
            else
                match name.Trim().ToLowerInvariant() with
                | "bool" | "boolean" | "system.boolean" -> Some TBool
                | "int"  | "int32"   | "system.int32"   -> Some TInt
                | "double" | "float" | "real" | "system.double" -> Some TDouble
                | "string" | "text" | "system.string" -> Some TString
                | _ -> None


// ─────────────────────────────────────────────────────────────────────
// 타입 변환 유틸리티 (Type Conversion Utilities)
// ─────────────────────────────────────────────────────────────────────
// PLC/DCS 환경에서 필요한 안전한 타입 변환 기능을 제공합니다.
// 모든 변환은 PLC 표준 규칙을 따르며, 변환 실패 시 명확한 오류 메시지를 제공합니다.
// ─────────────────────────────────────────────────────────────────────

/// 타입 변환 유틸리티 모듈
/// PLC 시스템에서 사용되는 표준적인 타입 변환 규칙을 구현
module TypeConverter =
    let private EPS = 1e-10

    /// .NET 타입에서 DsDataType으로 변환
    let fromDotNetType (typ: Type) : DsDataType = 
        DsDataType.OfType typ

    /// 문자열을 타입으로 파싱
    let parse (typeName: string) : DsDataType =
        match DsDataType.TryParse typeName with
        | Some t -> t
        | None -> failwithf "Unsupported type: %s" typeName

    /// 문자열을 타입으로 파싱 시도
    let tryParse (typeName: string) : DsDataType option = 
        DsDataType.TryParse typeName

    /// 객체를 bool로 변환
    /// MODERATE FIX (DEFECT-022-9): IEC 61131-3 TO_BOOL semantics
    /// Previous code treated |value| <= 1e-10 as false, misclassifying small magnitudes
    /// PLC standard: ANY non-zero value (no matter how small) coerces to TRUE
    let toBool (value: obj) : bool =
        if isNull value then false
        else
            match value with
            | :? bool as b -> b
            | :? int as i -> i <> 0
            | :? double as d -> not (Double.IsNaN d) && d <> 0.0
            | :? single as f -> not (Single.IsNaN f) && f <> 0.0f
            | :? string as s ->
                match Boolean.TryParse(s) with
                | true, b -> b
                | _ ->
                    // Handle common PLC false values explicitly
                    let normalized = s.Trim().ToLower()
                    match normalized with
                    | "0" | "off" | "no" | "false" | "" -> false
                    | "1" | "on" | "yes" | "true" -> true
                    | _ -> not (String.IsNullOrWhiteSpace s)
            | _ -> failwithf "Cannot convert %s to bool" (value.GetType().Name)

    /// 객체를 bool로 변환 시도
    let tryToBool (value: obj) : bool option =
        try Some (toBool value) with _ -> None

    /// 객체를 int로 변환
    /// MODERATE FIX (DEFECT-022-10): IEC 61131-3 TO_INT semantics
    /// Previous code used Math.Round (1.6 → 2), diverging from PLC standard
    /// IEC 61131 TO_INT truncates toward zero: 1.6 → 1, -1.6 → -1
    let toInt (value: obj) : int =
        if isNull value then 0
        else
            match value with
            | :? int as i -> i
            | :? bool as b -> if b then 1 else 0
            | :? double as d ->
                if Double.IsNaN d || Double.IsInfinity d then
                    failwith "Cannot convert NaN or Infinity to Int32"
                elif d > float Int32.MaxValue || d < float Int32.MinValue then
                    failwith "Double value exceeds Int32 range"
                else int d  // Truncate toward zero (IEC 61131-3 TO_INT)
            | :? single as f ->
                let d = float f
                if Double.IsNaN d || Double.IsInfinity d then
                    failwith "Cannot convert NaN or Infinity to Int32"
                elif d > float Int32.MaxValue || d < float Int32.MinValue then
                    failwith "Single value exceeds Int32 range"
                else int d  // Truncate toward zero (IEC 61131-3 TO_INT)
            | :? string as s ->
                match Int32.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture) with
                | true, i -> i
                | _ -> failwithf "Invalid integer: %s" s
            | _ -> failwithf "Cannot convert %s to int" (value.GetType().Name)

    /// 객체를 int로 변환 시도
    let tryToInt (value: obj) : int option =
        try Some (toInt value) with _ -> None

    /// 객체를 double로 변환
    let toDouble (value: obj) : double =
        if isNull value then 0.0
        else
            match value with
            | :? double as d -> d
            | :? single as f -> float f
            | :? int as i -> float i
            | :? bool as b -> if b then 1.0 else 0.0
            | :? string as s ->
                match Double.TryParse(s, NumberStyles.Float ||| NumberStyles.AllowThousands, CultureInfo.InvariantCulture) with
                | true, d -> d
                | _ -> failwithf "Invalid double: %s" s
            | _ -> failwithf "Cannot convert %s to double" (value.GetType().Name)

    /// 객체를 double로 변환 시도
    let tryToDouble (value: obj) : double option =
        try Some (toDouble value) with _ -> None

    /// 객체를 float로 변환 (double의 별칭)
    let toFloat (value: obj) : float =
        value |> toDouble |> float

    /// 객체를 string으로 변환
    let toString (value: obj) : string =
        if isNull value then String.Empty
        else
            match value with
            | :? string as s -> s
            | :? bool as b -> if b then "True" else "False"
            | :? int as i -> string i
            | :? double as d -> d.ToString(CultureInfo.InvariantCulture)
            | :? single as f -> (float f).ToString(CultureInfo.InvariantCulture)
            | _ -> value.ToString()

    /// 객체를 string으로 변환 시도
    let tryToString (value: obj) : string option =
        try Some (toString value) with _ -> None

    /// 목표 타입으로 변환
    let convert (targetType: DsDataType) (value: obj) : obj =
        match targetType with
        | TBool -> box (toBool value)
        | TInt -> box (toInt value)
        | TDouble -> box (toDouble value)
        | TString -> box (toString value)

    /// 목표 타입으로 변환 시도
    let tryConvert (targetType: DsDataType) (value: obj) : obj option =
        try
            Some (convert targetType value)
        with
        | _ -> None


/// 타입 검증 유틸리티
module TypeValidator =
    
    /// null 값 검증
    let checkNull (value: obj) (context: string) : obj =
        if isNull value then
            invalidArg "value" (sprintf "Null value in context: %s" context)
        else value

    /// 타입 일치 검증
    let checkType (expectedType: DsDataType) (value: obj) : obj =
        if isNull value then
            invalidArg "value" (sprintf "Null value for type %O" expectedType)
        else
            let actualType = DsDataType.OfType(value.GetType())
            if expectedType.IsCompatibleWith(actualType) then value
            else failwithf "Type mismatch: expected %O but got %O" expectedType actualType

    /// 숫자 범위 검증
    let checkRange (typ: DsDataType) (value: obj) : obj =
        match typ with
        | TInt ->
            let i = TypeConverter.toInt value
            if i >= Int32.MinValue && i <= Int32.MaxValue then box i
            else failwith "Int32 out of range"
        | TDouble ->
            let d = TypeConverter.toDouble value
            if Double.IsNaN(d) || Double.IsInfinity(d) then
                failwith "Invalid double value (NaN or Infinity)"
            else box d
        | _ -> value

    /// 문자열 검증
    let validateString (value: obj) (allowEmpty: bool) : obj =
        if isNull value then
            if allowEmpty then box String.Empty
            else failwith "null string value"
        else
            match value with
            | :? string as s ->
                if not allowEmpty && String.IsNullOrWhiteSpace(s) then
                    failwith "Empty string not allowed"
                else value
            | _ ->
                failwithf "type mismatch: expected String, got %s" (value.GetType().Name)

    /// 스코프 경로 형식 검증
    let validateScopePath (path: string) =
        if String.IsNullOrWhiteSpace path then
            invalidArg "path" "Scope path cannot be empty"
        let pattern = @"^[A-Za-z][A-Za-z0-9_]*(\.[A-Za-z][A-Za-z0-9_]*(\[[A-Za-z0-9_]+\])?)*$"
        if not (Regex.IsMatch(path, pattern)) then
            invalidArg "path" (sprintf "Invalid scope path format: %s" path)

