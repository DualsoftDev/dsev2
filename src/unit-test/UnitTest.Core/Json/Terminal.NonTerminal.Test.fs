namespace T

open NUnit.Framework

open Dual.Common.Core.FS
open Dual.Common.Base.FS
open Dual.Common.UnitTest.FS
open Dual.Ev2
open Dual.Common.Base.FS.SampleDataTypes


open Newtonsoft.Json
open System.Collections.Generic

[<AutoOpen>]
module TerminalTestModule =

    [<TestFixture>]
    type TerminalTest() =
        let t = TTerminal(123)
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
            let hh0 = EmJson.FromJson<TTerminal<int>>(json)
            let json2 = EmJson.ToJson(hh0)
            json2 === jsonAnswer


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
            let t = TTerminal(3.14)
            let json = EmJson.ToJson(t, settings)
            json === """{
  "$type": "Dual.Ev2.TTerminalModule+TTerminal`1[[System.Double, System.Private.CoreLib]], Ev2.Core.FS",
  "ObjectHolder": {
    "ValueTypeName": "System.Double",
    "Value": 3.14
  }
}"""




            noop()
        [<Test>]
        member _.TypeSerialization() =
            EmJson.GetType("Dual.Common.Base.FS.SampleDataTypes+Student, Dual.Common.Base.FS") === typeof<Student>
            EmJson.GetType("Dual.Ev2.TTerminalModule+TTerminal`1[System.Int32], Ev2.Core.FS") === typeof<TTerminal<int>>
            EmJson.GetType("System.Int32, System.Private.CoreLib") === typeof<int>

    [<TestFixture>]
    type NonTerminalTest() =
        [<Test>]
        member _.Minimal() =
            let nt = TNonTerminal(123)
            nt.Arguments <- [| TTerminal(1) :> IExpression; TNonTerminal(3.14) |]
            let json = EmJson.ToJson(nt)
            let jsonAnswer = """{
  "Operator": {
    "Case": "OpUnit"
  },
  "Arguments": [
    {
      "$type": "Dual.Ev2.TTerminalModule+TTerminal`1[[System.Int32, System.Private.CoreLib]], Ev2.Core.FS",
      "ObjectHolder": {
        "ValueTypeName": "System.Int32",
        "Value": 1
      }
    },
    {
      "$type": "Dual.Ev2.TTerminalModule+TNonTerminal`1[[System.Double, System.Private.CoreLib]], Ev2.Core.FS",
      "Operator": {
        "Case": "OpUnit"
      },
      "Arguments": [],
      "ObjectHolder": {
        "ValueTypeName": "System.Double",
        "Value": 3.14
      }
    }
  ],
  "ObjectHolder": {
    "ValueTypeName": "System.Int32",
    "Value": 123
  }
}"""
            json === jsonAnswer
            let hh0 = EmJson.FromJson<TNonTerminal<int>>(json)
            let json2 = EmJson.ToJson(hh0)
            json2 === jsonAnswer
