namespace Ev2.Cpu.Core

open System

/// 타입 검증 유틸리티
module TypeValidation =
    
    /// null 체크
    let checkNull (value: obj) (typeName: string) : obj =
        if isNull value then
            raise (ArgumentException(sprintf "null value for %s" typeName))
        else
            value
    
    /// 타입 일치 확인 (DsDataType 버전)
    let checkType (expectedType: DsDataType) (value: obj) : obj =
        if isNull value then
            raise (ArgumentException(sprintf "null value for %s" (expectedType.ToString())))
        else
            let actualType = value.GetType()
            let expectedDotNetType = expectedType.DotNetType
            if actualType = expectedDotNetType then
                value
            else
                raise (ArgumentException($"type mismatch: expected {expectedDotNetType.Name}, got {actualType.Name}"))
    
    /// 범위 검증 (숫자 타입용)
    let checkRange (t: DsDataType) (value: obj) : obj =
        match t with
        | TInt ->
            let i = unbox<int> value
            if i >= Int32.MinValue && i <= Int32.MaxValue then value
            else raise (ArgumentOutOfRangeException("value", "Int32 out of range"))
        | TDouble ->
            let d = unbox<double> value
            if Double.IsNaN(d) || Double.IsInfinity(d) then
                raise (ArgumentException("Invalid double value (NaN or Infinity)"))
            else value
        | _ -> value
    
    /// 문자열 검증
    let validateString (value: obj) (allowEmpty: bool) : obj =
        if isNull value then
            if allowEmpty then box String.Empty
            else raise (ArgumentNullException("value", "null string value"))
        else
            match value with
            | :? string as s ->
                if not allowEmpty && String.IsNullOrWhiteSpace(s) then
                    raise (ArgumentException("Empty string not allowed"))
                else
                    value
            | _ ->
                raise (ArgumentException($"type mismatch: expected String, got {value.GetType().Name}"))

    /// 타입 검증 하위 모듈
    module TypeValidator =
        
        /// 스코프 패스 검증
        let validateScopePath (path: string) : unit =
            if String.IsNullOrWhiteSpace(path) then
                raise (ArgumentException("Scope path cannot be null or empty"))
            
            // 유효한 식별자 패턴 검증 (문자로 시작, 문자/숫자/밑줄/점/대괄호 허용)
            let isValidChar c = 
                Char.IsLetterOrDigit(c) || c = '_' || c = '.' || c = '[' || c = ']'
            
            let isValidStart c = 
                Char.IsLetter(c) || c = '_'
            
            if not (isValidStart path.[0]) then
                raise (ArgumentException("Scope path must start with a letter or underscore"))
            
            if not (path |> Seq.forall isValidChar) then
                raise (ArgumentException("Scope path contains invalid characters"))