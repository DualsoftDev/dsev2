module FSharpOptionTest
open System
open Newtonsoft.Json
open System.Runtime.Serialization
open Dual.Common.Base



type Person() =
    [<JsonProperty("Name")>]
    member val Name = "" with get, set

    /// 내부적으로는 option<Guid>
    [<JsonProperty("UserId")>]
    member val UserIdBox = NsJsonOptionContainer<Guid>() with get, set

    [<JsonProperty("DOB")>]
    member val BirthdayBox = NsJsonOptionContainer<DateTime>() with get, set

    [<JsonProperty("Point")>]
    member val PointBox = NsJsonOptionContainer<System.Drawing.Point>() with get, set

    [<JsonIgnore>]
    member this.UserId
        with get () = this.UserIdBox.OptionValue
        and set v = this.UserIdBox.OptionValue <- v

    [<JsonIgnore>]
    member this.Birthday
        with get () = this.BirthdayBox.OptionValue
        and set v = this.BirthdayBox.OptionValue <- v

    [<JsonIgnore>]
    member this.Point
        with get () = this.PointBox.OptionValue
        and set v = this.PointBox.OptionValue <- v

open Newtonsoft.Json
open NUnit.Framework
open Dual.Common.Core.FS
open Dual.Common.Base
open System.Drawing
open Dual.Common.UnitTest.FS
open Ev2.Core.FS

[<Test>]
let doTest() =
    DcLogger.EnableTrace <- true
    let person = Person()
    person.Name <- "Alice"
    let json1 = JsonConvert.SerializeObject(person, Formatting.Indented)
    tracefn $"{json1}"
    person.UserId <- Some(Guid.Parse("b74c3a4a-bf0e-4ff4-b8a9-9d39c16dbe10"))
    let json2 = JsonConvert.SerializeObject(person, Formatting.Indented)
    tracefn $"{json2}"
    person.Birthday <- Some(DateTime.Parse("2025-05-20T16:26:29.381299+09:00"))
    let json3 = JsonConvert.SerializeObject(person, Formatting.Indented)
    tracefn $"{json3}"
    person.Point <- Some(Point(1, 3))
    let json4 = JsonConvert.SerializeObject(person, Formatting.Indented)
    tracefn $"{json4}"

    json1 === """{
  "Name": "Alice",
  "UserId": {},
  "DOB": {},
  "Point": {}
}"""
    json2 === """{
  "Name": "Alice",
  "UserId": {
    "Value": "b74c3a4a-bf0e-4ff4-b8a9-9d39c16dbe10"
  },
  "DOB": {},
  "Point": {}
}"""
    json3 === """{
  "Name": "Alice",
  "UserId": {
    "Value": "b74c3a4a-bf0e-4ff4-b8a9-9d39c16dbe10"
  },
  "DOB": {
    "Value": "2025-05-20T16:26:29.381299+09:00"
  },
  "Point": {}
}"""
    json4 === """{
  "Name": "Alice",
  "UserId": {
    "Value": "b74c3a4a-bf0e-4ff4-b8a9-9d39c16dbe10"
  },
  "DOB": {
    "Value": "2025-05-20T16:26:29.381299+09:00"
  },
  "Point": {
    "Value": "1, 3"
  }
}"""

    EmJson.FromJson<Person>(json4) |> EmJson.ToJson === json4

    printfn "%s" json3


[<Test>]
let arrowParse() =
    let json = """
        {
            "Guid": "6ee2fb3c-0db7-461e-a28f-9750dc7d36d3",
            "Source": "891067fc-9da0-4b48-980c-dcaf50a694ae",
            "Target": "cf22ac6b-403c-4547-bf82-846aa356b73e"
        }"""
    let arrow = EmJson.FromJson<DtoArrow>(json)
    ()