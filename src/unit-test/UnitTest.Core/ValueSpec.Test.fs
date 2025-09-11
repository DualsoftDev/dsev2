namespace T


open NUnit.Framework

open Ev2.Core.FS
open Dual.Common.UnitTest.FS
open Dual.Common.Base


[<AutoOpen>]
module ValueSpecTestModule =
    [<Test>]
    let ``parse test`` () =
        let p = Multiple [1; 2; 3] :> IValueSpec
        p.ToString() |> IValueSpec.Parse === p
        p.ToString() |> IValueSpec.Parse |> _.ToString() === p.ToString()
        ()

    [<Test>]
    let ``toString test`` () =
        let singleDoubleValue   :IValueSpec = Single 3.14156952
        let singleBoolValue     :IValueSpec = Single true
        let singleIntValue      :IValueSpec = Single 99
        let multipleIntValues   :IValueSpec = Multiple [1; 2; 3]
        let multipleDoubleValues:IValueSpec = Multiple [1.1; 2.2; 3.3]
        let singleRange1        :IValueSpec = Ranges [ { Lower = None; Upper = Some (3.14, Open) } ]
        let singleRange2        :IValueSpec = Ranges [ { Lower = None; Upper = Some (3.14, Closed) } ]

        let multipleRange:IValueSpec = Ranges [
            { Lower = None; Upper = Some (3.14, Open) }
            { Lower = Some (5.0, Open); Upper = Some (6.0, Open) }
            { Lower = Some (7.1, Closed); Upper = None }]

        singleDoubleValue      .ToString() === "x = 3.14156952"
        singleBoolValue        .ToString() === "x = true"
        singleIntValue         .ToString() === "x = 99"
        multipleIntValues      .ToString() === "x ∈ {1, 2, 3}"
        multipleDoubleValues   .ToString() === "x ∈ {1.1, 2.2, 3.3}"
        singleRange1           .ToString() === "x < 3.14"
        singleRange2           .ToString() === "x <= 3.14"
        multipleRange          .ToString() === "x < 3.14 || 5.0 < x < 6.0 || 7.1 <= x"

        // double 타입
        let jsonSingleDoubleValue = """{
  "$type": "Double",
  "Value": {
    "Case": "Single",
    "Fields": [
      3.14156952
    ]
  }
}"""
        EmJson.IsJsonEquals(singleDoubleValue.Jsonize(), jsonSingleDoubleValue) === true
        jsonSingleDoubleValue |> IValueSpec.Deserialize === singleDoubleValue
        jsonSingleDoubleValue |> ValueSpec.FromString === singleDoubleValue

        // bool 타입
        let jsonSingleBoolValue = """{
  "$type": "Boolean",
  "Value": {
    "Case": "Single",
    "Fields": [
      true
    ]
  }
}"""
        EmJson.IsJsonEquals(singleBoolValue.Jsonize(), jsonSingleBoolValue) === true
        jsonSingleBoolValue |> IValueSpec.Deserialize === singleBoolValue


        // double 타입
        let jsonMultipleRange = """{
  "$type": "Double",
  "Value": {
    "Case": "Ranges",
    "Fields": [
      [
        {
          "Lower": null,
          "Upper": {
            "Case": "Some",
            "Fields": [
              {
                "Item1": 3.14,
                "Item2": {
                  "Case": "Open"
                }
              }
            ]
          }
        },
        {
          "Lower": {
            "Case": "Some",
            "Fields": [
              {
                "Item1": 5.0,
                "Item2": {
                  "Case": "Open"
                }
              }
            ]
          },
          "Upper": {
            "Case": "Some",
            "Fields": [
              {
                "Item1": 6.0,
                "Item2": {
                  "Case": "Open"
                }
              }
            ]
          }
        },
        {
          "Lower": {
            "Case": "Some",
            "Fields": [
              {
                "Item1": 7.1,
                "Item2": {
                  "Case": "Closed"
                }
              }
            ]
          },
          "Upper": null
        }
      ]
    ]
  }
}"""
        EmJson.IsJsonEquals(multipleRange.Jsonize(), jsonMultipleRange) === true
        let obj = jsonMultipleRange |> ValueSpec.FromString
        obj === multipleRange
        let str = obj.Stringify()
        jsonMultipleRange |> IValueSpec.Deserialize === multipleRange

    [<Test>]
    let ``Contains test`` () =
        // Single value 테스트
        let singleInt = Single 42
        singleInt.Contains(42) === true
        singleInt.Contains(43) === false
        singleInt.Contains("42") === false  // 타입 불일치

        // Multiple values 테스트
        let multipleInt = Multiple [1; 2; 3; 4; 5]
        multipleInt.Contains(3) === true
        multipleInt.Contains(6) === false
        multipleInt.Contains(1.0) === false  // 타입 불일치

        // Range 테스트 - Open bounds
        let openRange = Ranges [ { Lower = Some (10, Open); Upper = Some (20, Open) } ]
        openRange.Contains(15) === true
        openRange.Contains(10) === false  // Open lower bound
        openRange.Contains(20) === false  // Open upper bound
        openRange.Contains(9) === false
        openRange.Contains(21) === false

        // Range 테스트 - Closed bounds
        let closedRange = Ranges [ { Lower = Some (10, Closed); Upper = Some (20, Closed) } ]
        closedRange.Contains(10) === true  // Closed lower bound
        closedRange.Contains(20) === true  // Closed upper bound
        closedRange.Contains(15) === true
        closedRange.Contains(9) === false
        closedRange.Contains(21) === false

        // Range 테스트 - Mixed bounds
        let mixedRange = Ranges [ { Lower = Some (10.0, Open); Upper = Some (20.0, Closed) } ]
        mixedRange.Contains(10.0) === false  // Open lower
        mixedRange.Contains(20.0) === true   // Closed upper
        mixedRange.Contains(15.0) === true

        // Range 테스트 - No lower bound
        let noLowerRange = Ranges [ { Lower = None; Upper = Some (10, Open) } ]
        noLowerRange.Contains(5) === true
        noLowerRange.Contains(9) === true
        noLowerRange.Contains(10) === false
        noLowerRange.Contains(11) === false

        // Range 테스트 - No upper bound
        let noUpperRange = Ranges [ { Lower = Some (10, Closed); Upper = None } ]
        noUpperRange.Contains(9) === false
        noUpperRange.Contains(10) === true
        noUpperRange.Contains(100) === true
        noUpperRange.Contains(1000) === true

        // Multiple ranges 테스트
        let multiRange = Ranges [
            { Lower = Some (1, Closed); Upper = Some (5, Open) }
            { Lower = Some (10, Open); Upper = Some (15, Closed) }
        ]
        multiRange.Contains(3) === true   // 첫 번째 범위
        multiRange.Contains(5) === false  // 첫 번째 범위 상한 (Open)
        multiRange.Contains(7) === false  // 범위 밖
        multiRange.Contains(12) === true  // 두 번째 범위
        multiRange.Contains(10) === false // 두 번째 범위 하한 (Open)
        multiRange.Contains(15) === true  // 두 번째 범위 상한 (Closed)

        // Bool 타입 테스트
        let singleBool = Single true
        singleBool.Contains(true) === true
        singleBool.Contains(false) === false
        singleBool.Contains(1) === false  // 타입 불일치

        let multipleBool = Multiple [true; false]
        multipleBool.Contains(true) === true
        multipleBool.Contains(false) === true
        multipleBool.Contains("true") === false  // 타입 불일치


