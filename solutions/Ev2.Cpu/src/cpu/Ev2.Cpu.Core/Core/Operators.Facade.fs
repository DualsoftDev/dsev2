namespace Ev2.Cpu.Core

// ─────────────────────────────────────────────────────────────────────
// 연산자 파사드 (Operator Facade)
// ─────────────────────────────────────────────────────────────────────
// 하위 모듈들을 통합하여 하나의 인터페이스로 제공
// 이전 Operators 모듈과의 호환성 유지
// ─────────────────────────────────────────────────────────────────────

/// 연산자 헬퍼 유틸리티 (통합 모듈)
[<AutoOpen>]
module Operators =

    /// 문자열을 연산자로 파싱
    let parse = OperatorParser.parse

    /// 문자열을 연산자로 파싱 (Option 반환)
    let tryParse = OperatorParser.tryParse

    /// 대소문자 구분 파싱
    let parseCaseSensitive = OperatorParser.parseCaseSensitive

    /// 연산자를 문자열로 변환
    let format = OperatorFormatter.format

    /// 연산자를 심볼 형식으로 변환
    let formatAsSymbol = OperatorFormatter.formatAsSymbol

    /// 연산자를 단어 형식으로 변환
    let formatAsWord = OperatorFormatter.formatAsWord
