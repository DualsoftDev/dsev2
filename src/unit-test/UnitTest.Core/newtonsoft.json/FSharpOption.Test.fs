module FSharpOptionTest

open System
open Newtonsoft.Json
open NUnit.Framework

open Dual.Common.Base
open Dual.Common.UnitTest.FS
open Ev2.Core.FS

// see ..\submodules\nuget\UnitTest\UnitTest.Nuget.Common\Json\NsFsOption.Test.fs
type NullContainer (nullstr:string, nullableInt:Nullable<int>, optNullable:int option) = //
    member val String = nullString with get, set
    member val NullableInt = nullableInt with get, set
    member val OptNullable = optNullable with get, set


[<Test>]
let doTestNullable() =
    do
        let nullData = NullContainer(null, Nullable(), None)
        let json = EmJson.ToJson(nullData)
        json === "{}"
    do
        let nullData = NullContainer(null, Nullable(3), Some(3))
        let json = JsonConvert.SerializeObject(nullData, Formatting.Indented)
        let answer = """{
  "String": null,
  "NullableInt": 3,
  "OptNullable": {
    "Case": "Some",
    "Fields": [
      3
    ]
  }
}"""
        EmJson.IsJsonEquals(json, answer) === true

        let json2 = EmJson.ToJson(nullData)
        let answer = """{
  "NullableInt": 3,
  "OptNullable": 3
}"""
        EmJson.IsJsonEquals(json2, answer) === true
