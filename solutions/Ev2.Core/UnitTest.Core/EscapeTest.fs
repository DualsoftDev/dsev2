namespace T

open NUnit.Framework
open System
open Ev2.Core.FS
open Newtonsoft.Json
open Dual.Common.UnitTest.FS
open Dual.Common.Base

[<AutoOpen>]
module EscapeTestModule =
    [<Test>]
    let ``Test Call AutoConditions JSON escape`` () =
        // ApiCallValueSpec 생성
        let guid1 = Guid.Parse("87950ebc-a3b7-4004-b35c-5f6efdca383a")
        let spec1 = ApiCallValueSpec<int32>(guid1, Single 1)
        let spec2 = ApiCallValueSpec<double>(guid1, Multiple [1.1; 2.2; 3.3])

        let specs = ApiCallValueSpecs()
        specs.Add(spec1)
        specs.Add(spec2)

        // Call 객체 생성
        let call = NjCall.Create()
        call.AutoConditionsObj <- specs

        // JSON 직렬화
        let callJson = JsonConvert.SerializeObject(call, Formatting.Indented)
        logDebug "NjCall JSON 결과:\n%s\n" callJson

        // AutoConditions 필드 확인
        let jObj = Newtonsoft.Json.Linq.JObject.Parse(callJson)
        let autoConditionsValue = jObj.["AutoConditions"]

        if autoConditionsValue <> null then
            let autoConditionsStr = autoConditionsValue.ToString()
            logDebug "AutoConditions 필드값:\n%s\n" autoConditionsStr

            // 이중 escape 확인 - 문자열에 \\ 가 포함되어서는 안됨
            let hasDoubleEscape = autoConditionsStr.Contains("\\\"")
            logDebug "이중 escape 발생 여부: %b\n" hasDoubleEscape
            Assert.That(hasDoubleEscape, Is.False, "이중 escape가 발생했습니다.")
        else
            Assert.Fail("AutoConditions가 null입니다.")