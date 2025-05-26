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


type GuidContainer private(guid:Guid, strGuid:string, optGuid:Guid option, nullableGuid:Nullable<Guid>) =

    new() = GuidContainer(Guid.Empty, null, None, Nullable())
    //new() = GuidContainer()
    member val Guid = guid with get, set
    member val StrGuid = strGuid with get, set

    [<JsonConverter(typeof<OptionGuidConverter>)>]
    member val OptGuid = optGuid with get, set
    member val NullableGuid = nullableGuid with get, set
    static member Create(?guid, ?strGuid, ?optGuid, ?nullableGuid) =
        let guid = guid |? Guid.Empty
        let strGuid = strGuid |? null:string
        let optGuid = optGuid |? Some Guid.Empty
        let nullableGuid = nullableGuid |? Nullable<Guid>()
        GuidContainer(guid, strGuid, optGuid, nullableGuid)

[<Test>]
let doTestGuid() =
    DcLogger.EnableTrace <- true
    let guid = GuidContainer()
    let jsonOriginal = JsonConvert.SerializeObject(guid, Formatting.Indented)
    tracefn $"{jsonOriginal}"
    jsonOriginal === """{
  "Guid": "00000000-0000-0000-0000-000000000000",
  "StrGuid": null,
  "OptGuid": null,
  "NullableGuid": null
}"""
    let json1 = EmJson.ToJson(guid)
    json1 === """{
  "Guid": "00000000-0000-0000-0000-000000000000"
}"""
    tracefn $"{json1}"



    do
        let guid = GuidContainer.Create(
            Guid.Parse("10000000-0000-0000-0000-000000000000"),
            "20000000-0000-0000-0000-000000000000",
            Some(Guid.Parse("30000000-0000-0000-0000-000000000000")),
            Nullable<Guid>(Guid.Parse("40000000-0000-0000-0000-000000000000")))
        let json = EmJson.ToJson(guid)
        tracefn $"{json}"
        let jsonAnswer = """{
  "Guid": "10000000-0000-0000-0000-000000000000",
  "StrGuid": "20000000-0000-0000-0000-000000000000",
  "OptGuid": "30000000-0000-0000-0000-000000000000",
  "NullableGuid": "40000000-0000-0000-0000-000000000000"
}"""
        json === jsonAnswer
        let guid2 = EmJson.FromJson<GuidContainer>(json)
        let json2 = EmJson.ToJson(guid2)
        json2 === jsonAnswer

    ()


type NullContainer (nullstr:string, nullableInt:Nullable<int>, optNullable:int option) =
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
        let json = EmJson.ToJson(nullData)
        json === """{
  "NullableInt": 3
}"""
        ()
    ()
