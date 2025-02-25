namespace T

open NUnit.Framework

open Dual.Common.Core.FS
open Dual.Common.Base.FS
open Dual.Common.UnitTest.FS
open Dual.Ev2
open Dual.Common.Base.FS.SampleDataTypes


open Newtonsoft.Json
open System.Collections.Generic
open System

[<AutoOpen>]
module TerminalTestModule =

    [<TestFixture>]
    type TerminalTest() =
        let t = TValue(123)
        [<Test>]
        member _.Minimal() =
            let json = EmJson.ToJson(t)
            let jsonAnswer = """{
  "ObjectHolder": {
    "ValueTypeName": "System.Int32",
    "Value": 123
  }
}"""
            json === jsonAnswer
            let hh0 = EmJson.FromJson<TValue<int>>(json)
            let json2 = EmJson.ToJson(hh0)
            json2 === jsonAnswer


        [<Test>]
        member _.Properties() =
            let pi = TValue(3.14)
            pi.Name <- "PI"
            pi.Address <- "%IX3.1"
            pi.Comment <- "This is PI"
            pi.IsLiteral <- true
            let json = EmJson.ToJson(pi)
            let jsonAnswer = """{
  "ObjectHolder": {
    "ValueTypeName": "System.Double",
    "Value": 3.14
  },
  "PropertiesDto": {
    "Name": "PI",
    "Address": "%IX3.1",
    "Comment": "This is PI",
    "IsLiteral": true
  }
}"""
            json === jsonAnswer
            let hh0 = EmJson.FromJson<TValue<int>>(json)
            let json2 = EmJson.ToJson(hh0)
            json2 === jsonAnswer

            (fun () -> pi.Value <- 123.0) |> ShouldFailWithSubstringT "ERROR: PI is CONSTANT.  It's read-only"
            noop()



    [<TestFixture>]
    type TypeSimplifyTest() =
        [<Test>]
        member _.SimpleSerialization() =
            let settings = JsonSerializerSettings(TypeNameHandling = TypeNameHandling.All, NullValueHandling = NullValueHandling.Ignore)

            let t = KeyValuePair<int, string>(3, "hello")
            let json = EmJson.ToJson(t, settings)
            noop()

            let s = Student("John", 13)
            let json = EmJson.ToJson(s, settings)
            json === """{
  "$type": "Dual.Common.Base.FS.SampleDataTypes+Student, Dual.Common.Base.FS",
  "Name": "John",
  "Age": 13
}"""
            let t = TValue(3.14)
            let json = EmJson.ToJson(t, settings)
            json === """{
  "$type": "Dual.Ev2.TExpressionModule+TValue`1[[System.Double, System.Private.CoreLib]], Ev2.Core.FS",
  "ObjectHolder": {
    "$type": "Dual.Common.Base.FS.NsJsonS11nSafeObjectModule+NsJsonS11nSafeObject, Dual.Common.Base.FS",
    "ValueTypeName": "System.Double",
    "Value": 3.14
  }
}"""




        [<Test>]
        member _.TypeSerialization() =
            EmJson.GetType("Dual.Common.Base.FS.SampleDataTypes+Student, Dual.Common.Base.FS") === typeof<Student>
            EmJson.GetType("Dual.Ev2.TExpressionModule+TValue`1[System.Int32], Ev2.Core.FS") === typeof<TValue<int>>
            EmJson.GetType("System.Int32, System.Private.CoreLib") === typeof<int>

    [<TestFixture>]
    type NonTerminalTest() =
        [<Test>]
        member _.Minimal() =
            Dual.Ev2.ModuleInitializer.Initialize()

            let nt =
                let args = [| TValue(1.0) :> IExpression; TValue(3.14) |]
                TFunction<double>.Create(Op.PredefinedOperator "+", args)
            let json = EmJson.ToJson(nt)
            let jsonAnswer = """{
  "Operator": {
    "Case": "PredefinedOperator",
    "Fields": [
      "+"
    ]
  },
  "Arguments": [
    {
      "$type": "Dual.Ev2.TExpressionModule+TValue`1[[System.Double, System.Private.CoreLib]], Ev2.Core.FS",
      "ObjectHolder": {
        "ValueTypeName": "System.Double",
        "Value": 1.0
      }
    },
    {
      "$type": "Dual.Ev2.TExpressionModule+TValue`1[[System.Double, System.Private.CoreLib]], Ev2.Core.FS",
      "ObjectHolder": {
        "ValueTypeName": "System.Double",
        "Value": 3.14
      }
    }
  ]
}"""
            //let hh0 = EmJson.FromJson<TFunction<double>>(jsonAnswer)
            //let json = jsonAnswer


            json === jsonAnswer

            let hh0 = EmJson.FromJson<TFunction<double>>(json)
            let json2 = EmJson.ToJson(hh0)
            json2 === jsonAnswer


            Math.Abs(nt.TValue - 4.14) < 0.001 === true
            //nt.LazyValue
