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
        TBool.DotNetType |> should equal typeof<bool>
        TInt.DotNetType |> should equal typeof<int>
        TDouble.DotNetType |> should equal typeof<double>
        TString.DotNetType |> should equal typeof<string>
    
    [<Fact>]
    member _.``DsDataType_DefaultValue_정확성_테스트``() =
        TBool.DefaultValue |> should equal (box false)
        TInt.DefaultValue |> should equal (box 0)
        TDouble.DefaultValue |> should equal (box 0.0)
        TString.DefaultValue |> should equal (box "")
    
    [<Fact>]
    member _.``DsDataType_IsNumeric_분류_테스트``() =
        TBool.IsNumeric |> should be False
        TInt.IsNumeric |> should be True
        TDouble.IsNumeric |> should be True
        TString.IsNumeric |> should be False
    
    [<Fact>]
    member _.``DsDataType_IsCompatibleWith_호환성_테스트``() =
        // 동일 타입 호환성
        TBool.IsCompatibleWith(TBool) |> should be True
        TInt.IsCompatibleWith(TInt) |> should be True
        TDouble.IsCompatibleWith(TDouble) |> should be True
        TString.IsCompatibleWith(TString) |> should be True
        
        // Int → Double 승격 허용 (Double 변수에 Int 값 할당 가능)
        TDouble.IsCompatibleWith(TInt) |> should be True

        // 기타 변환 금지
        TBool.IsCompatibleWith(TInt) |> should be False
        TInt.IsCompatibleWith(TDouble) |> should be False  // Double → Int 축소 금지
        TString.IsCompatibleWith(TBool) |> should be False
    
    [<Fact>]
    member _.``DsDataType_ToString_가독성_테스트``() =
        TBool.ToString() |> should equal "Bool"
        TInt.ToString() |> should equal "Int"
        TDouble.ToString() |> should equal "Double"
        TString.ToString() |> should equal "String"
    
    [<Fact>]
    member _.``DsDataType_OfType_정확한_변환``() =
        DsDataType.OfType(typeof<bool>) |> should equal TBool
        DsDataType.OfType(typeof<int>) |> should equal TInt
        DsDataType.OfType(typeof<double>) |> should equal TDouble
        DsDataType.OfType(typeof<float>) |> should equal TDouble  // float도 Double로 매핑
        DsDataType.OfType(typeof<string>) |> should equal TString
    
    [<Fact>]
    member _.``DsDataType_OfType_지원하지_않는_타입_예외``() =
        (fun () -> DsDataType.OfType(typeof<DateTime>) |> ignore) |> should throw typeof<ArgumentException>
    
    [<Fact>]
    member _.``DsDataType_OfType_null_타입_예외``() =
        (fun () -> DsDataType.OfType(null) |> ignore) |> should throw typeof<ArgumentException>
    
    [<Fact>]
    member _.``DsDataType_TryParse_성공_케이스``() =
        DsDataType.TryParse("bool") |> should equal (Some TBool)
        DsDataType.TryParse("BOOLEAN") |> should equal (Some TBool)
        DsDataType.TryParse("int") |> should equal (Some TInt)
        DsDataType.TryParse("INT32") |> should equal (Some TInt)
        DsDataType.TryParse("double") |> should equal (Some TDouble)
        DsDataType.TryParse("REAL") |> should equal (Some TDouble)
        DsDataType.TryParse("string") |> should equal (Some TString)
        DsDataType.TryParse("TEXT") |> should equal (Some TString)
    
    [<Fact>]
    member _.``DsDataType_TryParse_실패_케이스``() =
        DsDataType.TryParse("invalid") |> should equal None
        DsDataType.TryParse("") |> should equal None
        DsDataType.TryParse(null) |> should equal None
        DsDataType.TryParse("   ") |> should equal None
    
    // ═════════════════════════════════════════════════════════════════
    // TypeConverter 모듈 포괄적 테스트
    // ═════════════════════════════════════════════════════════════════
    
    [<Fact>]
    member _.``TypeConversion_toBool_정상_변환``() =
        // bool → bool
        TypeConverter.toBool(box true) |> should equal true
        TypeConverter.toBool(box false) |> should equal false
        
        // int → bool
        TypeConverter.toBool(box 1) |> should equal true
        TypeConverter.toBool(box -1) |> should equal true
        TypeConverter.toBool(box 0) |> should equal false
        
        // double → bool
        TypeConverter.toBool(box 1.0) |> should equal true
        TypeConverter.toBool(box -0.1) |> should equal true
        TypeConverter.toBool(box 0.0) |> should equal false
        TypeConverter.toBool(box Double.NaN) |> should equal false
        
        // string → bool
        TypeConverter.toBool(box "true") |> should equal true
        TypeConverter.toBool(box "false") |> should equal false
        TypeConverter.toBool(box "non-empty") |> should equal true
        TypeConverter.toBool(box "") |> should equal false
        TypeConverter.toBool(box "   ") |> should equal false
        
        // null → bool
        TypeConverter.toBool(null) |> should equal false
    
    [<Fact>]
    member _.``TypeConversion_toBool_지원하지_않는_타입_예외``() =
        (fun () -> TypeConverter.toBool(box DateTime.Now) |> ignore) |> should throw typeof<Exception>
    
    [<Fact>]
    member _.``TypeConversion_toInt_정상_변환``() =
        // int → int
        TypeConverter.toInt(box 42) |> should equal 42
        TypeConverter.toInt(box -100) |> should equal -100
        
        // bool → int
        TypeConverter.toInt(box true) |> should equal 1
        TypeConverter.toInt(box false) |> should equal 0
        
        // double → int (IEC 61131-3 truncation toward zero)
        TypeConverter.toInt(box 42.0) |> should equal 42
        TypeConverter.toInt(box 42.4) |> should equal 42
        TypeConverter.toInt(box 42.6) |> should equal 42  // Truncate, not round
        TypeConverter.toInt(box -42.4) |> should equal -42
        TypeConverter.toInt(box -42.6) |> should equal -42  // Truncate toward zero
        
        // string → int
        TypeConverter.toInt(box "123") |> should equal 123
        TypeConverter.toInt(box "-456") |> should equal -456
        
        // null → int
        TypeConverter.toInt(null) |> should equal 0
    
    [<Fact>]
    member _.``TypeConversion_toInt_NaN_예외``() =
        (fun () -> TypeConverter.toInt(box Double.NaN) |> ignore) |> should throw typeof<Exception>
    
    [<Fact>]
    member _.``TypeConversion_toInt_Infinity_예외``() =
        (fun () -> TypeConverter.toInt(box Double.PositiveInfinity) |> ignore) |> should throw typeof<Exception>
    
    [<Fact>]
    member _.``TypeConversion_toInt_잘못된_문자열_예외``() =
        (fun () -> TypeConverter.toInt(box "not_a_number") |> ignore) |> should throw typeof<Exception>
    
    [<Fact>]
    member _.``TypeConversion_toDouble_정상_변환``() =
        // double → double
        TypeConverter.toDouble(box 42.5) |> should equal 42.5
        TypeConverter.toDouble(box -100.25) |> should equal -100.25
        
        // int → double
        TypeConverter.toDouble(box 42) |> should equal 42.0
        TypeConverter.toDouble(box -100) |> should equal -100.0
        
        // bool → double
        TypeConverter.toDouble(box true) |> should equal 1.0
        TypeConverter.toDouble(box false) |> should equal 0.0
        
        // string → double
        TypeConverter.toDouble(box "123.45") |> should equal 123.45
        TypeConverter.toDouble(box "-456.78") |> should equal -456.78
        
        // null → double
        TypeConverter.toDouble(null) |> should equal 0.0
    
    [<Fact>]
    member _.``TypeConversion_toDouble_잘못된_문자열_예외``() =
        (fun () -> TypeConverter.toDouble(box "not_a_number") |> ignore) |> should throw typeof<Exception>
    
    [<Fact>]
    member _.``TypeConversion_toString_정상_변환``() =
        // string → string
        TypeConverter.toString(box "hello") |> should equal "hello"
        
        // int → string
        TypeConverter.toString(box 42) |> should equal "42"
        
        // double → string
        TypeConverter.toString(box 42.5) |> should equal "42.5"
        
        // bool → string
        TypeConverter.toString(box true) |> should equal "True"
        TypeConverter.toString(box false) |> should equal "False"
        
        // null → string
        TypeConverter.toString(null) |> should equal ""
    
    [<Fact>]
    member _.``TypeConversion_convert_모든_타입_조합``() =
        // Bool 변환
        TypeConverter.convert TBool (box 1) |> should equal (box true)
        TypeConverter.convert TBool (box 0) |> should equal (box false)
        
        // Int 변환 (IEC 61131-3 truncation toward zero)
        TypeConverter.convert TInt (box 42.7) |> should equal (box 42)
        TypeConverter.convert TInt (box true) |> should equal (box 1)
        
        // Double 변환
        TypeConverter.convert TDouble (box 42) |> should equal (box 42.0)
        TypeConverter.convert TDouble (box true) |> should equal (box 1.0)
        
        // String 변환
        TypeConverter.convert TString (box 42) |> should equal (box "42")
        TypeConverter.convert TString (box true) |> should equal (box "True")
    
    [<Fact>]
    member _.``TypeConversion_tryConvert_성공_실패_케이스``() =
        // 성공 케이스
        let result1 = TypeConverter.tryConvert TInt (box "123")
        result1.IsSome |> should be True
        result1.Value |> should equal (box 123)
        
        // 실패 케이스 (잘못된 문자열)
        let result2 = TypeConverter.tryConvert TInt (box "not_a_number")
        result2.IsNone |> should be True
    
    // ═════════════════════════════════════════════════════════════════
    // TypeValidation 모듈 테스트
    // ═════════════════════════════════════════════════════════════════
    
    [<Fact>]
    member _.``TypeValidation_checkNull_정상_케이스``() =
        let result = TypeValidation.checkNull (box 42) "test_context"
        result |> should equal (box 42)
    
    [<Fact>]
    member _.``TypeValidation_checkNull_null_예외``() =
        (fun () -> TypeValidation.checkNull null "test_context" |> ignore) |> should throw typeof<ArgumentException>
    
    [<Fact>]
    member _.``TypeValidation_checkType_정상_호환``() =
        let result = TypeValidation.checkType TInt (box 42)
        result |> should equal (box 42)
    
    [<Fact>]
    member _.``TypeValidation_checkType_null_예외``() =
        (fun () -> TypeValidation.checkType TInt null |> ignore) |> should throw typeof<ArgumentException>
    
    [<Fact>]
    member _.``TypeValidation_checkRange_Int_정상``() =
        let result = TypeValidation.checkRange TInt (box 42)
        result |> should equal (box 42)
    
    [<Fact>]
    member _.``TypeValidation_checkRange_Double_정상``() =
        let result = TypeValidation.checkRange TDouble (box 42.5)
        result |> should equal (box 42.5)
    
    [<Fact>]
    member _.``TypeValidation_checkRange_Double_NaN_예외``() =
        (fun () -> TypeValidation.checkRange TDouble (box Double.NaN) |> ignore) |> should throw typeof<Exception>
    
    [<Fact>]
    member _.``TypeValidation_checkRange_Double_Infinity_예외``() =
        (fun () -> TypeValidation.checkRange TDouble (box Double.PositiveInfinity) |> ignore) |> should throw typeof<Exception>
    
    [<Fact>]
    member _.``TypeValidation_validateScopePath_정상_케이스``() =
        // 유효한 스코프 패스들
        TypeValidation.TypeValidator.validateScopePath "System"
        TypeValidation.TypeValidator.validateScopePath "Motor.Control"
        TypeValidation.TypeValidator.validateScopePath "Tank_Level.Sensor[Input1]"
        TypeValidation.TypeValidator.validateScopePath "A.B.C.D.E"
    
    [<Fact>]
    member _.``TypeValidation_validateScopePath_빈_문자열_예외``() =
        (fun () -> TypeValidation.TypeValidator.validateScopePath "" |> ignore) |> should throw typeof<ArgumentException>
    
    [<Fact>]
    member _.``TypeValidation_validateScopePath_null_예외``() =
        (fun () -> TypeValidation.TypeValidator.validateScopePath null |> ignore) |> should throw typeof<ArgumentException>
    
    [<Fact>]
    member _.``TypeValidation_validateScopePath_잘못된_형식_예외``() =
        (fun () -> TypeValidation.TypeValidator.validateScopePath "123InvalidStart" |> ignore) |> should throw typeof<ArgumentException>
    
    [<Fact>]
    member _.``TypeValidation_validateScopePath_특수문자_예외``() =
        (fun () -> TypeValidation.TypeValidator.validateScopePath "Invalid@Path" |> ignore) |> should throw typeof<ArgumentException>