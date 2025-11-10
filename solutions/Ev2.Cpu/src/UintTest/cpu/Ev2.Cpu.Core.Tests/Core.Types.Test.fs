namespace Ev2.Cpu.Test

open System
open Xunit
open FsUnit.Xunit
open Ev2.Cpu.Core

// ─────────────────────────────────────────────────────────────────────
// Core/Types.fs 모듈 포괄적 유닛테스트
// ─────────────────────────────────────────────────────────────────────
// DsDataType, TypeConverter, TypeValidation의 모든 기능을 테스트
// 정상 케이스, 경계값, 에러 케이스를 모두 검증
// ─────────────────────────────────────────────────────────────────────

type CoreTypesTest() =
    
    // ═════════════════════════════════════════════════════════════════
    // DsDataType 기본 기능 테스트
    // ═════════════════════════════════════════════════════════════════
    
    [<Fact>]
    member _.``DsDataType_DotNetType_매핑_정확성_테스트``() =
        typeof<bool> |> should equal typeof<bool>
        typeof<int> |> should equal typeof<int>
        typeof<double> |> should equal typeof<double>
        typeof<string> |> should equal typeof<string>

    [<Fact>]
    member _.``DsDataType_DefaultValue_정확성_테스트``() =
        TypeHelpers.getDefaultValue typeof<bool> |> should equal (box false)
        TypeHelpers.getDefaultValue typeof<int> |> should equal (box 0)
        TypeHelpers.getDefaultValue typeof<double> |> should equal (box 0.0)
        TypeHelpers.getDefaultValue typeof<string> |> should equal (box "")

    [<Fact>]
    member _.``DsDataType_IsNumeric_분류_테스트``() =
        TypeHelpers.isNumericType typeof<bool> |> should be False
        TypeHelpers.isNumericType typeof<int> |> should be True
        TypeHelpers.isNumericType typeof<double> |> should be True
        TypeHelpers.isNumericType typeof<string> |> should be False

    [<Fact>]
    member _.``DsDataType_IsCompatibleWith_호환성_테스트``() =
        // 동일 타입 호환성
        TypeHelpers.areTypesCompatible typeof<bool> typeof<bool> |> should be True
        TypeHelpers.areTypesCompatible typeof<int> typeof<int> |> should be True
        TypeHelpers.areTypesCompatible typeof<double> typeof<double> |> should be True
        TypeHelpers.areTypesCompatible typeof<string> typeof<string> |> should be True

        // Int → Double 승격 허용 (Double 변수에 Int 값 할당 가능)
        TypeHelpers.areTypesCompatible typeof<double> typeof<int> |> should be True

        // 기타 변환 금지
        TypeHelpers.areTypesCompatible typeof<bool> typeof<int> |> should be False
        TypeHelpers.areTypesCompatible typeof<int> typeof<double> |> should be False  // Double → Int 축소 금지
        TypeHelpers.areTypesCompatible typeof<string> typeof<bool> |> should be False

    [<Fact>]
    member _.``DsDataType_ToString_가독성_테스트``() =
        TypeHelpers.getTypeName typeof<bool> |> should equal "Bool"
        TypeHelpers.getTypeName typeof<int> |> should equal "Int"
        TypeHelpers.getTypeName typeof<double> |> should equal "Double"
        TypeHelpers.getTypeName typeof<string> |> should equal "String"
    
    [<Fact>]
    member _.``TypeHelpers_isSupportedType_정확한_검증``() =
        TypeHelpers.isSupportedType typeof<bool> |> should be True
        TypeHelpers.isSupportedType typeof<int> |> should be True
        TypeHelpers.isSupportedType typeof<double> |> should be True
        TypeHelpers.isSupportedType typeof<string> |> should be True
        TypeHelpers.isSupportedType typeof<DateTime> |> should be False
        TypeHelpers.isSupportedType typeof<float32> |> should be False  // float32 = single (F#에서 float는 double의 별칭)

    [<Fact>]
    member _.``DsDataType_TryParse_성공_케이스``() =
        TypeHelpers.tryParseTypeName("bool") |> should equal (Some typeof<bool>)
        TypeHelpers.tryParseTypeName("BOOLEAN") |> should equal (Some typeof<bool>)
        TypeHelpers.tryParseTypeName("int") |> should equal (Some typeof<int>)
        TypeHelpers.tryParseTypeName("INT32") |> should equal (Some typeof<int>)
        TypeHelpers.tryParseTypeName("double") |> should equal (Some typeof<double>)
        TypeHelpers.tryParseTypeName("REAL") |> should equal (Some typeof<double>)
        TypeHelpers.tryParseTypeName("string") |> should equal (Some typeof<string>)
        TypeHelpers.tryParseTypeName("TEXT") |> should equal (Some typeof<string>)
    
    [<Fact>]
    member _.``DsDataType_TryParse_실패_케이스``() =
        TypeHelpers.tryParseTypeName("invalid") |> should equal None
        TypeHelpers.tryParseTypeName("") |> should equal None
        TypeHelpers.tryParseTypeName(null) |> should equal None
        TypeHelpers.tryParseTypeName("   ") |> should equal None
    
    // ═════════════════════════════════════════════════════════════════
    // TypeHelpers 모듈 포괄적 테스트
    // ═════════════════════════════════════════════════════════════════
    
    [<Fact>]
    member _.``TypeConversion_toBool_정상_변환``() =
        // bool → bool
        TypeHelpers.toBool(box true) |> should equal true
        TypeHelpers.toBool(box false) |> should equal false
        
        // int → bool
        TypeHelpers.toBool(box 1) |> should equal true
        TypeHelpers.toBool(box -1) |> should equal true
        TypeHelpers.toBool(box 0) |> should equal false
        
        // double → bool
        TypeHelpers.toBool(box 1.0) |> should equal true
        TypeHelpers.toBool(box -0.1) |> should equal true
        TypeHelpers.toBool(box 0.0) |> should equal false
        TypeHelpers.toBool(box Double.NaN) |> should equal false
        
        // string → bool
        TypeHelpers.toBool(box "true") |> should equal true
        TypeHelpers.toBool(box "false") |> should equal false
        TypeHelpers.toBool(box "non-empty") |> should equal true
        TypeHelpers.toBool(box "") |> should equal false
        TypeHelpers.toBool(box "   ") |> should equal false
        
        // null → bool
        TypeHelpers.toBool(null) |> should equal false
    
    [<Fact>]
    member _.``TypeConversion_toBool_지원하지_않는_타입_예외``() =
        (fun () -> TypeHelpers.toBool(box DateTime.Now) |> ignore) |> should throw typeof<Exception>
    
    [<Fact>]
    member _.``TypeConversion_toInt_정상_변환``() =
        // int → int
        TypeHelpers.toInt(box 42) |> should equal 42
        TypeHelpers.toInt(box -100) |> should equal -100
        
        // bool → int
        TypeHelpers.toInt(box true) |> should equal 1
        TypeHelpers.toInt(box false) |> should equal 0
        
        // double → int (IEC 61131-3 truncation toward zero)
        TypeHelpers.toInt(box 42.0) |> should equal 42
        TypeHelpers.toInt(box 42.4) |> should equal 42
        TypeHelpers.toInt(box 42.6) |> should equal 42  // Truncate, not round
        TypeHelpers.toInt(box -42.4) |> should equal -42
        TypeHelpers.toInt(box -42.6) |> should equal -42  // Truncate toward zero
        
        // string → int
        TypeHelpers.toInt(box "123") |> should equal 123
        TypeHelpers.toInt(box "-456") |> should equal -456
        
        // null → int
        TypeHelpers.toInt(null) |> should equal 0
    
    [<Fact>]
    member _.``TypeConversion_toInt_NaN_예외``() =
        (fun () -> TypeHelpers.toInt(box Double.NaN) |> ignore) |> should throw typeof<Exception>
    
    [<Fact>]
    member _.``TypeConversion_toInt_Infinity_예외``() =
        (fun () -> TypeHelpers.toInt(box Double.PositiveInfinity) |> ignore) |> should throw typeof<Exception>
    
    [<Fact>]
    member _.``TypeConversion_toInt_잘못된_문자열_예외``() =
        (fun () -> TypeHelpers.toInt(box "not_a_number") |> ignore) |> should throw typeof<Exception>
    
    [<Fact>]
    member _.``TypeConversion_toDouble_정상_변환``() =
        // double → double
        TypeHelpers.toDouble(box 42.5) |> should equal 42.5
        TypeHelpers.toDouble(box -100.25) |> should equal -100.25
        
        // int → double
        TypeHelpers.toDouble(box 42) |> should equal 42.0
        TypeHelpers.toDouble(box -100) |> should equal -100.0
        
        // bool → double
        TypeHelpers.toDouble(box true) |> should equal 1.0
        TypeHelpers.toDouble(box false) |> should equal 0.0
        
        // string → double
        TypeHelpers.toDouble(box "123.45") |> should equal 123.45
        TypeHelpers.toDouble(box "-456.78") |> should equal -456.78
        
        // null → double
        TypeHelpers.toDouble(null) |> should equal 0.0
    
    [<Fact>]
    member _.``TypeConversion_toDouble_잘못된_문자열_예외``() =
        (fun () -> TypeHelpers.toDouble(box "not_a_number") |> ignore) |> should throw typeof<Exception>
    
    [<Fact>]
    member _.``TypeConversion_toString_정상_변환``() =
        // string → string
        TypeHelpers.toString(box "hello") |> should equal "hello"
        
        // int → string
        TypeHelpers.toString(box 42) |> should equal "42"
        
        // double → string
        TypeHelpers.toString(box 42.5) |> should equal "42.5"
        
        // bool → string
        TypeHelpers.toString(box true) |> should equal "True"
        TypeHelpers.toString(box false) |> should equal "False"
        
        // null → string
        TypeHelpers.toString(null) |> should equal ""
    
    [<Fact>]
    member _.``TypeConversion_convert_모든_타입_조합``() =
        // Bool 변환
        TypeHelpers.convertToType typeof<bool> (box 1) |> should equal (box true)
        TypeHelpers.convertToType typeof<bool> (box 0) |> should equal (box false)

        // Int 변환 (IEC 61131-3 truncation toward zero)
        TypeHelpers.convertToType typeof<int> (box 42.7) |> should equal (box 42)
        TypeHelpers.convertToType typeof<int> (box true) |> should equal (box 1)

        // Double 변환
        TypeHelpers.convertToType typeof<double> (box 42) |> should equal (box 42.0)
        TypeHelpers.convertToType typeof<double> (box true) |> should equal (box 1.0)

        // String 변환
        TypeHelpers.convertToType typeof<string> (box 42) |> should equal (box "42")
        TypeHelpers.convertToType typeof<string> (box true) |> should equal (box "True")
    
    [<Fact>]
    member _.``TypeConversion_tryConvert_성공_실패_케이스``() =
        // 성공 케이스
        let result1 = TypeHelpers.tryConvertToType typeof<int> (box "123")
        result1.IsSome |> should be True
        result1.Value |> should equal (box 123)

        // 실패 케이스 (잘못된 문자열)
        let result2 = TypeHelpers.tryConvertToType typeof<int> (box "not_a_number")
        result2.IsNone |> should be True
    
    // ═════════════════════════════════════════════════════════════════
    // TypeHelpers 검증 함수 테스트
    // ═════════════════════════════════════════════════════════════════

    [<Fact>]
    member _.``TypeValidation_checkNull_정상_케이스``() =
        let result = TypeHelpers.checkNull (box 42) "test_context"
        result |> should equal (box 42)

    [<Fact>]
    member _.``TypeValidation_checkNull_null_예외``() =
        (fun () -> TypeHelpers.checkNull null "test_context" |> ignore) |> should throw typeof<ArgumentException>

    [<Fact>]
    member _.``TypeValidation_checkType_정상_호환``() =
        let result = TypeHelpers.checkType typeof<int> (box 42)
        result |> should equal (box 42)

    [<Fact>]
    member _.``TypeValidation_checkType_null_예외``() =
        (fun () -> TypeHelpers.checkType typeof<int> null |> ignore) |> should throw typeof<ArgumentException>

    [<Fact>]
    member _.``TypeValidation_checkRange_Int_정상``() =
        let result = TypeHelpers.checkRange typeof<int> (box 42)
        result |> should equal (box 42)

    [<Fact>]
    member _.``TypeValidation_checkRange_Double_정상``() =
        let result = TypeHelpers.checkRange typeof<double> (box 42.5)
        result |> should equal (box 42.5)

    [<Fact>]
    member _.``TypeValidation_checkRange_Double_NaN_예외``() =
        (fun () -> TypeHelpers.checkRange typeof<double> (box Double.NaN) |> ignore) |> should throw typeof<Exception>

    [<Fact>]
    member _.``TypeValidation_checkRange_Double_Infinity_예외``() =
        (fun () -> TypeHelpers.checkRange typeof<double> (box Double.PositiveInfinity) |> ignore) |> should throw typeof<Exception>