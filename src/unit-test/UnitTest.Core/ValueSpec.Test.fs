namespace T


open NUnit.Framework

open Ev2.Core.FS
open Dual.Common.UnitTest.FS


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
  "valueType": "Double",
  "value": {
    "Case": "Single",
    "Fields": [
      3.14156952
    ]
  }
}"""
        singleDoubleValue.Jsonize() =~= jsonSingleDoubleValue
        jsonSingleDoubleValue |> IValueSpec.Deserialize === singleDoubleValue

        // bool 타입
        let jsonSingleBoolValue = """{
  "valueType": "Boolean",
  "value": {
    "Case": "Single",
    "Fields": [
      true
    ]
  }
}"""
        singleBoolValue.Jsonize() =~= jsonSingleBoolValue
        jsonSingleBoolValue |> IValueSpec.Deserialize === singleBoolValue


        // double 타입
        let jsonMultipleRange = """{
  "valueType": "Double",
  "value": {
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
        multipleRange.Jsonize() =~= jsonMultipleRange
        jsonMultipleRange |> IValueSpec.Deserialize === multipleRange


