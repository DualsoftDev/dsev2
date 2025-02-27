namespace T

open Dual.Common.Core.FS
open NUnit.Framework
open Newtonsoft.Json
open Dual.Common.Base.FS
open Dual.Common.UnitTest.FS
open JsonSubTypes
open Dual.Common.Base.CS
open Dual.Ev2
open Dual.Common.Base.FS.SampleDataTypes



[<AutoOpen>]
module ValueHolderTestModule =

    let h0 = ValueHolder.Create(typeof<int>)
    let h1 = ValueHolder.Create(typeof<int>, 123)
    let h2 = THolder.Create(3.14)
    let h3 = ValueHolder.Create(typeof<string>, "Hello, World!")
    let h4 = ValueHolder.Create(typeof<uint64>, 9999UL)

    /// ObjectHolder 를 포함하는 클래스
    type ContainerClass() =
        member val Holder0 = h0 with get, set
        member val Holder1 = h1 with get, set
        member val Holder2 = h2 with get, set
        member val Holder3 = h3 with get, set
        member val Holder4 = h4 with get, set

        member val Holders:ValueHolder[] = [||] with get, set
        member val Num = 1234 with get

    type CC(cc:ContainerClass) =
        member val CC = cc with get, set
        member val Num = "Hello" with get


    [<TestFixture>]
    type ValueHolderTest() =
        [<Test>]
        member _.Minimal() =
            let json = EmJson.ToJson(h4)
            let jsonAnswer = """{
  "ObjectHolder": {
    "ValueTypeName": "System.UInt64",
    "Value": 9999
  }
}"""
            json === jsonAnswer
            let hh0 = EmJson.FromJson<ValueHolder>(json)
            let json2 = EmJson.ToJson(hh0)
            json2 === jsonAnswer

        [<Test>]
        member _.WithDynamicProperties() =
            let typ = typeof<double>
            let h = ValueHolder.Create(typ, 3.14)
            h.Type === typ
            (h :> IWithType).Type === typ

            // ValueHolder.Name 직접 접근
            h.Name === null

            // IWithName.Name -> 동적 property fetch "Name" -> ValueHolder.Name 접근
            (h :> IWithName).Name === null

            h.Name === null
            (h :> IWithName).Name === null
            h.Name <- "PI"
            (h :> IWithName).Name === h.Name

            let address = "%IX7.2"
            h.Address === null
            h.Address <- address
            (h :> IWithAddress).Address === address

            let json = EmJson.ToJson(h)
            writeClipboard(json)
            let jsonAnswer = """{
  "ObjectHolder": {
    "ValueTypeName": "System.Double",
    "Value": 3.14
  },
  "PropertiesDto": {
    "Name": "PI",
    "Address": "%IX7.2"
  }
}"""
            json === jsonAnswer
            let hh0 = EmJson.FromJson<ValueHolder>(json)
            let json2 = EmJson.ToJson(hh0)
            json2 === jsonAnswer


        [<Test>]
        member _.WithDD() =
            let h = ValueHolder.Create(typeof<double>, 3.14)
            h.DD.Add("name", "PI")
            h.DD.Add("tolerarnce", 0.001)
            h.DD.Add("student", Student("John", 16))
            let json = EmJson.ToJson(h)
            writeClipboard(json)
            let jsonAnswer = """{
  "ObjectHolder": {
    "ValueTypeName": "System.Double",
    "Value": 3.14
  },
  "PropertiesDto": {
    "name": "PI",
    "tolerarnce": 0.001,
    "student": {
      "$type": "Dual.Common.Base.FS.SampleDataTypes+Student, Dual.Common.Base.FS",
      "Name": "John",
      "Age": 16
    }
  }
}"""
            json === jsonAnswer
            let hh0 = EmJson.FromJson<ValueHolder>(json)
            let json2 = EmJson.ToJson(hh0)
            json2 === jsonAnswer


        [<Test>]
        member _.DefaultSerializeTest() =
            let container = ContainerClass()
            container.Holders <- [| h0; h3 |]
            let json1 = EmJson.ToJson(container)
            writeClipboard(json1)

            let json1Answer = """{
  "Holder0": {
    "ObjectHolder": {
      "ValueTypeName": "System.Int32"
    }
  },
  "Holder1": {
    "ObjectHolder": {
      "ValueTypeName": "System.Int32",
      "Value": 123
    }
  },
  "Holder2": {
    "ObjectHolder": {
      "ValueTypeName": "System.Double",
      "Value": 3.14
    }
  },
  "Holder3": {
    "ObjectHolder": {
      "ValueTypeName": "System.String",
      "Value": "Hello, World!"
    }
  },
  "Holder4": {
    "ObjectHolder": {
      "ValueTypeName": "System.UInt64",
      "Value": 9999
    }
  },
  "Holders": [
    {
      "ObjectHolder": {
        "ValueTypeName": "System.Int32"
      }
    },
    {
      "ObjectHolder": {
        "ValueTypeName": "System.String",
        "Value": "Hello, World!"
      }
    }
  ],
  "Num": 1234
}"""
            json1 === json1Answer
            let hh0 = EmJson.FromJson<ContainerClass>(json1Answer)
            let json2 = EmJson.ToJson(hh0)
            json2 === json1Answer

            let cc = CC(container)
            let json2 = EmJson.ToJson(cc)

            json2 === """{
  "CC": {
    "Holder0": {
      "ObjectHolder": {
        "ValueTypeName": "System.Int32"
      }
    },
    "Holder1": {
      "ObjectHolder": {
        "ValueTypeName": "System.Int32",
        "Value": 123
      }
    },
    "Holder2": {
      "ObjectHolder": {
        "ValueTypeName": "System.Double",
        "Value": 3.14
      }
    },
    "Holder3": {
      "ObjectHolder": {
        "ValueTypeName": "System.String",
        "Value": "Hello, World!"
      }
    },
    "Holder4": {
      "ObjectHolder": {
        "ValueTypeName": "System.UInt64",
        "Value": 9999
      }
    },
    "Holders": [
      {
        "ObjectHolder": {
          "ValueTypeName": "System.Int32"
        }
      },
      {
        "ObjectHolder": {
          "ValueTypeName": "System.String",
          "Value": "Hello, World!"
        }
      }
    ],
    "Num": 1234
  },
  "Num": "Hello"
}"""
            tracefn "Serialized JSON: %s" json2
            let cc2 = EmJson.FromJson<CC>(json2)
            let json3 = EmJson.ToJson(cc2)
            writeClipboard(json3)
            json2 === json3

            //let deserializedContainer = ContainerClass.FromJson(json)
            let deserializedContainer = JsonConvert.DeserializeObject<ContainerClass>(json1)
            tracefn "Deserialized Holder1: Type = %s, Value = %O" deserializedContainer.Holder1.ValueType.Name deserializedContainer.Holder1.OValue
            tracefn "Deserialized Holder2: Type = %s, Value = %O" deserializedContainer.Holder2.ValueType.Name deserializedContainer.Holder2.Value
            tracefn "Deserialized Holder3: Type = %s, Value = %O" deserializedContainer.Holder3.ValueType.Name deserializedContainer.Holder3.OValue

            tracefn "Deserialized Holder1: Type = %s" deserializedContainer.Holder1.Type.Name

