namespace T

open NUnit.Framework
open System
open Ev2.Core.FS
open Newtonsoft.Json
open Dual.Common.UnitTest.FS
open Dual.Common.Base
open System.Security.Cryptography

[<AutoOpen>]
module GuidedValueSpecTestModule =
    [<Test>]
    let ``GuidedValueSpec can be created with Single value`` () =
        let guid = Guid.NewGuid()
        let valueSpec = Single 42
        let guidedSpec = GuidedValueSpec(guid, valueSpec)  // 타입 추론으로 <int> 생략

        guidedSpec.Guid === guid
        guidedSpec.ValueSpec === valueSpec

    [<Test>]
    let ``GuidedValueSpec can be created with Multiple values`` () =
        let guid = Guid.NewGuid()
        let valueSpec = Multiple [1; 2; 3]
        let guidedSpec = GuidedValueSpec(guid, valueSpec)  // 타입 추론으로 <int> 생략

        guidedSpec.Guid === guid
        guidedSpec.ValueSpec === valueSpec

    [<Test>]
    let ``GuidedValueSpec with Single value serializes and deserializes correctly`` () =
        let guid = Guid.NewGuid()
        let valueSpec = Single 3.14
        let guidedSpec = GuidedValueSpec(guid, valueSpec)  // 타입 추론으로 <float> 생략

        // GuidedValueSpec 전체 직렬화
        let json = guidedSpec.ToJson()
        //let json = EmJson.ToJson guidedSpec
        let deserialized = GuidedValueSpec<float>.FromJson(json)

        deserialized.Guid === guid
        (deserialized.ValueSpec :> IValueSpec).Stringify() === (valueSpec :> IValueSpec).Stringify()

    [<Test>]
    let ``GuidedValueSpec with Multiple values serializes and deserializes correctly`` () =
        let guid = Guid.NewGuid()
        let valueSpec = Multiple [true; false; true]
        let guidedSpec = GuidedValueSpec(guid, valueSpec)  // 타입 추론으로 <bool> 생략

        // GuidedValueSpec 전체 직렬화
        let json = guidedSpec.ToJson()
        let deserialized = GuidedValueSpec<bool>.FromJson(json)

        deserialized.Guid === guid
        (deserialized.ValueSpec :> IValueSpec).Stringify() === (valueSpec :> IValueSpec).Stringify()

    [<Test>]
    let ``GuidedValueSpec with Ranges serializes and deserializes correctly`` () =
        let guid = Guid.NewGuid()
        let valueSpec =
            Ranges [
                { Lower = Some (3.0, Open); Upper = Some (7.0, Closed) }
            ]
        let guidedSpec = GuidedValueSpec(guid, valueSpec)  // 타입 추론으로 <float> 생략

        // ValueSpec 부분만 따로 직렬화
        let valueJson = (valueSpec :> IValueSpec).Jsonize()
        let deserializedValue = IValueSpec.Deserialize(valueJson)

        (guidedSpec.ValueSpec :> IValueSpec).Stringify() === (valueSpec :> IValueSpec).Stringify()
        deserializedValue.Stringify() === (valueSpec :> IValueSpec).Stringify()

    [<Test>]
    let ``ValueSpec JSON serialization round trip`` () =
        let valueSpec = Single 42.0 :> IValueSpec
        let json = valueSpec.Jsonize()
        let deserialized = IValueSpec.Deserialize(json)

        deserialized.Stringify() === valueSpec.Stringify()

    [<Test>]
    let ``ValueSpec string parsing round trip`` () =
        let text = "x ∈ {1, 2, 3}"
        let valueSpec = IValueSpec.Parse(text)
        let stringified = valueSpec.Stringify()

        stringified === text

    [<Test>]
    let ``TagWithSpec serializes and deserializes correctly`` () =
        let tag = TagWithSpec<int>("Temperature", "DB100.DBW10", Single 25)
        tag.Value.Value <- 42

        let json = JsonConvert.SerializeObject(tag)
        let deserialized = JsonConvert.DeserializeObject<TagWithSpec<int>>(json)

        // Access through Tag property since Name/Address/Value are now JsonIgnore
        deserialized.Tag.Name === "Temperature"
        deserialized.Tag.Address === "DB100.DBW10"
        deserialized.Tag.Value.Value === 42
        (deserialized.ValueSpec :> IValueSpec).Stringify() === "x = 25"

    [<Test>]
    let ``TagWithSpec with range spec works correctly`` () =
        let spec = Ranges [{ Lower = Some (0.0, Closed); Upper = Some (100.0, Closed) }]
        let tag = TagWithSpec<float>("Pressure", "DB200.DBD20", spec)
        tag.Value.Value <- 50.0

        let json = JsonConvert.SerializeObject(tag)
        let deserialized = JsonConvert.DeserializeObject<TagWithSpec<float>>(json)

        // Access through Tag property since Value is now JsonIgnore
        deserialized.Tag.Value.Value === 50.0
        (deserialized.ValueSpec :> IValueSpec).Stringify() === "0.0 <= x <= 100.0"

    [<Test>]
    let ``ITagWithSpec interface works correctly`` () =
        let tag = TagWithSpec<int>("Sensor", "DB300.DBW30", Single 42)
        tag.Value.Value <- 100

        // Access through interface
        let iTag = tag :> ITagWithSpec

        // Test property access
        iTag.Name === "Sensor"
        iTag.Address === "DB300.DBW30"
        (iTag.Value :?> int) === 100
        iTag.ValueType === typeof<int>
        (iTag.ValueSpec.Stringify()) === "x = 42"

        // Test property modification through interface
        iTag.Name <- "NewSensor"
        iTag.Address <- "DB400.DBW40"
        iTag.Value <- box 200

        // Verify changes
        tag.Name === "NewSensor"
        tag.Address === "DB400.DBW40"
        tag.Value.Value === 200

    [<Test>]
    let ``ITagWithSpec serialization and deserialization works`` () =
        // Create tags with different types
        let intTag = TagWithSpec<int>("IntSensor", "DB100.DBW10", Single 42)
        intTag.Value.Value <- 100

        let floatTag = TagWithSpec<float>("FloatSensor", "DB200.DBD20", Ranges [{ Lower = Some (0.0, Closed); Upper = Some (100.0, Closed) }])
        floatTag.Value.Value <- 75.5

        // Store as ITagWithSpec interface
        let iTags: ITagWithSpec list = [intTag :> ITagWithSpec; floatTag :> ITagWithSpec]

        // Serialize each tag with type information
        let serialized =
            iTags
            |> List.map (fun iTag ->
                let typeName = iTag.ValueType.FullName
                let json = JsonConvert.SerializeObject(iTag.Tag)
                let valueSpecJson = JsonConvert.SerializeObject(iTag.ValueSpec)
                {| TypeName = typeName; TagJson = json; ValueSpecJson = valueSpecJson |})
            |> JsonConvert.SerializeObject

        // Deserialize back
        let deserializedData = JsonConvert.DeserializeObject<{| TypeName: string; TagJson: string; ValueSpecJson: string |} list>(serialized)

        // Verify first tag (int)
        let firstData = deserializedData.[0]
        firstData.TypeName === typeof<int>.FullName

        // For actual usage, you would use reflection to create the correct generic type
        // Here we know it's int, so we can deserialize directly
        let deserializedIntTag =
            let deserializedTag = JsonConvert.DeserializeObject<PlcTag<int>>(firstData.TagJson)
            let valueSpec = JsonConvert.DeserializeObject<ValueSpec<int>>(firstData.ValueSpecJson)
            let newTag = TagWithSpec<int>(deserializedTag.Name, deserializedTag.Address, valueSpec)
            newTag.Tag.Value <- deserializedTag.Value
            newTag

        deserializedIntTag.Name === "IntSensor"
        deserializedIntTag.Value.Value === 100
        (deserializedIntTag.ValueSpec :> IValueSpec).Stringify() === "x = 42"

    [<Test>]
    let ``ITagWithSpec collection with mixed types`` () =
        // Create a container that holds different types of tags
        let tags = ResizeArray<ITagWithSpec>()

        // Add different types
        let t1 = TagWithSpec<int>("Tag1", "DB1", Single 10) :> ITagWithSpec
        let t2 = TagWithSpec<float>("Tag2", "DB2", Multiple [1.0; 2.0; 3.0]) :> ITagWithSpec
        let t3 = TagWithSpec<bool>("Tag3", "DB3", Single true) :> ITagWithSpec

        tags.AddRange([t1; t2; t3])

        let j1 = EmJson.ToJson t1
        let j2 = EmJson.ToJson t2
        let j3 = EmJson.ToJson t3

        // Access through interface
        tags.Count === 3
        tags.[0].Name === "Tag1"
        tags.[1].Name === "Tag2"
        tags.[2].Name === "Tag3"

        // Check types
        tags.[0].ValueType === typeof<int>
        tags.[1].ValueType === typeof<float>
        tags.[2].ValueType === typeof<bool>

        // Check ValueSpec through interface
        tags.[0].ValueSpec.Stringify() === "x = 10"
        tags.[1].ValueSpec.Stringify() === "x ∈ {1.0, 2.0, 3.0}"
        tags.[2].ValueSpec.Stringify() === "x = true"

    [<Test>]
    let ``TagWithSpec with Undefined ValueSpec serializes correctly`` () =
        let tag = TagWithSpec<int>("TestTag", "DB100.DBW0", ValueSpec.Undefined)
        tag.Value.Value <- 123

        let json1 = JsonConvert.SerializeObject(tag)
        let deserialized = JsonConvert.DeserializeObject<TagWithSpec<int>>(json1)
        let json2 = JsonConvert.SerializeObject(deserialized)

        deserialized.Tag.Name === "TestTag"
        deserialized.Tag.Address === "DB100.DBW0"
        deserialized.Tag.Value.Value === 123
        EmJson.IsJsonEquals(json1, json2) === true

    [<Test>]
    let ``TagWithSpec with Multiple ValueSpec serializes correctly`` () =
        let spec = Multiple ["Option1"; "Option2"; "Option3"]
        let tag = TagWithSpec<string>("StringTag", "DB200.DBW0", spec)
        tag.Value.Value <- "Option2"

        let json1 = JsonConvert.SerializeObject(tag)
        let deserialized = JsonConvert.DeserializeObject<TagWithSpec<string>>(json1)
        let json2 = JsonConvert.SerializeObject(deserialized)

        deserialized.Tag.Name === "StringTag"
        deserialized.Tag.Address === "DB200.DBW0"
        deserialized.Tag.Value.Value === "Option2"
        EmJson.IsJsonEquals(json1, json2) === true

    [<Test>]
    let ``TagWithSpec with complex Ranges serializes correctly`` () =
        let spec = Ranges [
            { Lower = Some (10.0, Open); Upper = Some (20.0, Closed) }
            { Lower = Some (30.0, Closed); Upper = Some (40.0, Open) }
        ]
        let tag = TagWithSpec<float>("RangeTag", "DB300.DBD0", spec)
        tag.Value.Value <- 15.5

        let json1 = JsonConvert.SerializeObject(tag)
        let deserialized = JsonConvert.DeserializeObject<TagWithSpec<float>>(json1)
        let json2 = JsonConvert.SerializeObject(deserialized)

        deserialized.Tag.Name === "RangeTag"
        deserialized.Tag.Address === "DB300.DBD0"
        deserialized.Tag.Value.Value === 15.5
        EmJson.IsJsonEquals(json1, json2) === true

    [<Test>]
    let ``TagWithSpec JSON structure validation`` () =
        let spec = Single 100
        let tag = TagWithSpec<int>("JsonTest", "DB400.DBW0", spec)
        tag.Value.Value <- 200

        let json = JsonConvert.SerializeObject(tag)
        let jsonObj = Newtonsoft.Json.Linq.JObject.Parse(json)

        // Verify JSON structure - only Tag and ValueSpec should be serialized
        jsonObj.ContainsKey("Tag") === true
        jsonObj.ContainsKey("ValueSpec") === true
        jsonObj.ContainsKey("Name") === false  // Should be JsonIgnore
        jsonObj.ContainsKey("Address") === false  // Should be JsonIgnore
        jsonObj.ContainsKey("Value") === false  // Should be JsonIgnore

        // Verify nested Tag structure (Tag is a JSON string)
        let tagJsonStr = jsonObj.["Tag"].ToString()
        let tagObj = Newtonsoft.Json.Linq.JObject.Parse(tagJsonStr)
        tagObj.ContainsKey("Name") === true
        tagObj.ContainsKey("Address") === true
        tagObj.ContainsKey("Value") === true
        tagObj.ContainsKey("$type") === true  // Type information should be included

    [<Test>]
    let ``TagWithSpec with null values handles correctly`` () =
        let spec = Single 0
        let tag = TagWithSpec<int>(null, null, spec)
        tag.Value.Value <- 0

        let json1 = JsonConvert.SerializeObject(tag)
        let deserialized = JsonConvert.DeserializeObject<TagWithSpec<int>>(json1)
        let json2 = JsonConvert.SerializeObject(deserialized)

        deserialized.Tag.Name === null
        deserialized.Tag.Address === null
        deserialized.Tag.Value.Value === 0
        EmJson.IsJsonEquals(json1, json2) === true

    [<Test>]
    let ``TagWithSpec collection with different types serializes correctly`` () =
        let tags = [
            TagWithSpec<int>("IntTag", "DB1", Single 42) :> ITagWithSpec
            TagWithSpec<float>("FloatTag", "DB2", Multiple [1.1; 2.2; 3.3]) :> ITagWithSpec
            TagWithSpec<string>("StringTag", "DB3", Single "Hello") :> ITagWithSpec
            TagWithSpec<bool>("BoolTag", "DB4", Single false) :> ITagWithSpec
        ]

        // Set values
        (tags.[0] :> ITagWithSpec).Value <- box 100
        (tags.[1] :> ITagWithSpec).Value <- box 2.2
        (tags.[2] :> ITagWithSpec).Value <- box "World"
        (tags.[3] :> ITagWithSpec).Value <- box true

        // Serialize each with type info
        let serializedData =
            tags
            |> List.map (fun tag ->
                {|
                    TypeName = tag.ValueType.FullName
                    Json = JsonConvert.SerializeObject(tag.Tag)
                    ValueSpecJson = (tag.ValueSpec :> IValueSpec).Jsonize()
                |})

        // Verify each can be deserialized
        serializedData.Length === 4
        serializedData.[0].TypeName === typeof<int>.FullName
        serializedData.[1].TypeName === typeof<float>.FullName
        serializedData.[2].TypeName === typeof<string>.FullName
        serializedData.[3].TypeName === typeof<bool>.FullName

    [<Test>]
    let ``TagWithSpec preserves value after multiple serialization cycles`` () =
        let spec = Multiple [10; 20; 30; 40; 50]
        let originalTag = TagWithSpec<int>("CycleTest", "DB500.DBW0", spec)
        originalTag.Value.Value <- 30

        // First cycle
        let json1 = JsonConvert.SerializeObject(originalTag)
        let tag1 = JsonConvert.DeserializeObject<TagWithSpec<int>>(json1)

        // Second cycle
        let json2 = JsonConvert.SerializeObject(tag1)
        let tag2 = JsonConvert.DeserializeObject<TagWithSpec<int>>(json2)

        // Third cycle
        let json3 = JsonConvert.SerializeObject(tag2)
        let tag3 = JsonConvert.DeserializeObject<TagWithSpec<int>>(json3)

        // All should be equal
        tag3.Tag.Name === "CycleTest"
        tag3.Tag.Address === "DB500.DBW0"
        tag3.Tag.Value.Value === 30
        (tag3.ValueSpec :> IValueSpec).Stringify() === "x ∈ {10, 20, 30, 40, 50}"

        // JSON should remain stable
        json1 === json2
        json2 === json3

    [<Test>]
    let ``TagWithSpec with overlapping ranges serializes correctly`` () =
        let spec = Ranges [
            { Lower = Some (0.0, Closed); Upper = Some (50.0, Closed) }
            { Lower = Some (40.0, Open); Upper = Some (100.0, Closed) }
        ]
        let tag = TagWithSpec<float>("OverlapTest", "DB600.DBD0", spec)
        tag.Value.Value <- 45.0

        let json1 = JsonConvert.SerializeObject(tag)
        let deserialized = JsonConvert.DeserializeObject<TagWithSpec<float>>(json1)
        let json2 = JsonConvert.SerializeObject(deserialized)

        deserialized.Tag.Name === "OverlapTest"
        deserialized.Tag.Address === "DB600.DBD0"
        deserialized.Tag.Value.Value === 45.0
        EmJson.IsJsonEquals(json1, json2) === true

    [<Test>]
    let ``TagWithSpec with DateTime type serializes correctly`` () =
        let now = DateTime.Now
        let spec = Single now
        let tag = TagWithSpec<DateTime>("TimeTag", "DB700.DBW0", spec)
        tag.Value.Value <- now.AddHours(1.0)

        let json = JsonConvert.SerializeObject(tag)
        let deserialized = JsonConvert.DeserializeObject<TagWithSpec<DateTime>>(json)

        deserialized.Tag.Name === "TimeTag"
        deserialized.Tag.Address === "DB700.DBW0"
        // Compare with millisecond precision due to serialization
        Math.Abs((deserialized.Tag.Value.Value - now.AddHours(1.0)).TotalMilliseconds) < 1.0 === true

    [<Test>]
    let ``TagWithSpec with large Multiple values handles correctly`` () =
        let largeList = [1..1000] // 1000 items
        let spec = Multiple largeList
        let tag = TagWithSpec<int>("LargeTag", "DB800.DBW0", spec)
        tag.Value.Value <- 500

        let json = JsonConvert.SerializeObject(tag)
        let deserialized = JsonConvert.DeserializeObject<TagWithSpec<int>>(json)

        deserialized.Tag.Name === "LargeTag"
        deserialized.Tag.Value.Value === 500
        match deserialized.ValueSpec with
        | Multiple values -> values.Length === 1000
        | _ -> failwith "Expected Multiple ValueSpec"

    [<Test>]
    let ``TagWithSpec array serialization and deserialization`` () =
        let tags = [|
            TagWithSpec<int>("Tag1", "DB1", Single 10)
            TagWithSpec<int>("Tag2", "DB2", Multiple [20; 30])
            TagWithSpec<int>("Tag3", "DB3", Ranges [{ Lower = Some (0, Closed); Upper = Some (100, Closed) }])
        |]

        let json = JsonConvert.SerializeObject(tags)
        let deserialized = JsonConvert.DeserializeObject<TagWithSpec<int>[]>(json)

        deserialized.Length === 3
        deserialized.[0].Tag.Name === "Tag1"
        deserialized.[1].Tag.Name === "Tag2"
        deserialized.[2].Tag.Name === "Tag3"

    [<Test>]
    let ``TagWithSpec with empty string values`` () =
        let spec = Single ""
        let tag = TagWithSpec<string>("EmptyTag", "DB900.DBW0", spec)
        tag.Value.Value <- ""

        let json = JsonConvert.SerializeObject(tag)
        let deserialized = JsonConvert.DeserializeObject<TagWithSpec<string>>(json)

        deserialized.Tag.Name === "EmptyTag"
        deserialized.Tag.Value.Value === ""
        (deserialized.ValueSpec :> IValueSpec).Stringify() === "x = \"\""

    [<Test>]
    let ``TagWithSpec modification after deserialization works correctly`` () =
        let spec = Single 100
        let tag = TagWithSpec<int>("ModifyTest", "DB1000.DBW0", spec)
        tag.Value.Value <- 100

        let json = JsonConvert.SerializeObject(tag)
        let deserialized = JsonConvert.DeserializeObject<TagWithSpec<int>>(json)

        // Modify deserialized object
        deserialized.Name <- "ModifiedName"
        deserialized.Address <- "DB2000.DBW0"
        deserialized.Value.Value <- 200
        deserialized.ValueSpec <- Single 300

        // Verify modifications
        deserialized.Tag.Name === "ModifiedName"
        deserialized.Tag.Address === "DB2000.DBW0"
        deserialized.Tag.Value.Value === 200
        (deserialized.ValueSpec :> IValueSpec).Stringify() === "x = 300"

        // Re-serialize and verify
        let json2 = JsonConvert.SerializeObject(deserialized)
        let deserialized2 = JsonConvert.DeserializeObject<TagWithSpec<int>>(json2)

        deserialized2.Tag.Name === "ModifiedName"
        deserialized2.Tag.Address === "DB2000.DBW0"
        deserialized2.Tag.Value.Value === 200

    [<Test>]
    let ``TagWithSpec with nested JSON in tag name`` () =
        let spec = Single 42
        let tag = TagWithSpec<int>("{\"nested\":\"json\"}", "DB1100.DBW0", spec)
        tag.Value.Value <- 100

        let json = JsonConvert.SerializeObject(tag)
        let deserialized = JsonConvert.DeserializeObject<TagWithSpec<int>>(json)

        deserialized.Tag.Name === "{\"nested\":\"json\"}"
        deserialized.Tag.Value.Value === 100

    [<Test>]
    let ``TagWithSpec with special characters in address`` () =
        let spec = Single "test"
        let tag = TagWithSpec<string>("SpecialTag", "DB100.DBW[0].Value<1>", spec)
        tag.Value.Value <- "special"

        let json = JsonConvert.SerializeObject(tag)
        let deserialized = JsonConvert.DeserializeObject<TagWithSpec<string>>(json)

        deserialized.Tag.Address === "DB100.DBW[0].Value<1>"
        deserialized.Tag.Value.Value === "special"

    [<Test>]
    let ``TagWithSpec dictionary serialization`` () =
        let dict = System.Collections.Generic.Dictionary<string, ITagWithSpec>()

        dict.["int_tag"] <- TagWithSpec<int>("IntTag", "DB1", Single 42) :> ITagWithSpec
        dict.["float_tag"] <- TagWithSpec<float>("FloatTag", "DB2", Multiple [1.0; 2.0]) :> ITagWithSpec
        dict.["bool_tag"] <- TagWithSpec<bool>("BoolTag", "DB3", Single true) :> ITagWithSpec

        // Serialize as list of tags
        let tagList = dict |> Seq.map (fun kvp -> kvp.Value.Tag) |> Seq.toList
        let json = JsonConvert.SerializeObject(tagList)

        // Verify JSON can be parsed
        let parsed = Newtonsoft.Json.Linq.JArray.Parse(json)
        parsed.Count === 3

    [<Test>]
    let ``TagWithSpec JSON format detailed validation`` () =
        let spec = Multiple [10; 20; 30]
        let tag = TagWithSpec<int>("DetailedTest", "DB1200.DBW0", spec)
        tag.Value.Value <- 20

        let json = JsonConvert.SerializeObject(tag, Formatting.Indented)
        let jsonObj = Newtonsoft.Json.Linq.JObject.Parse(json)

        // Root level validation
        jsonObj.Properties() |> Seq.map (fun p -> p.Name) |> Seq.toList |> List.sort === ["Tag"; "ValueSpec"]

        // Tag structure validation (Tag is a JSON string)
        let tagJsonStr = jsonObj.["Tag"].ToString()
        let tagObj = Newtonsoft.Json.Linq.JObject.Parse(tagJsonStr)
        tagObj.["Name"].ToString() === "DetailedTest"
        tagObj.["Address"].ToString() === "DB1200.DBW0"

        // ValueSpec structure validation
        jsonObj.ContainsKey("ValueSpec") === true

    [<Test>]
    let ``TagWithSpec with extreme values`` () =
        let spec = Ranges [{ Lower = Some (System.Double.MinValue, Closed); Upper = Some (System.Double.MaxValue, Closed) }]
        let tag = TagWithSpec<float>("ExtremeTag", "DB1300.DBD0", spec)
        tag.Value.Value <- System.Double.MaxValue

        let json = JsonConvert.SerializeObject(tag)
        let deserialized = JsonConvert.DeserializeObject<TagWithSpec<float>>(json)

        deserialized.Tag.Value.Value === System.Double.MaxValue

    [<Test>]
    let ``TagWithSpec ToString method output`` () =
        let spec = Single 42
        let tag = TagWithSpec<int>("ToStringTest", "DB1400.DBW0", spec)

        let str = tag.ToString()
        str === "ToStringTest (DB1400.DBW0) [x = 42]"

    [<Test>]
    let ``TagWithSpec with unicode characters`` () =
        let spec = Multiple ["한글"; "中文"; "العربية"; "🚀"]
        let tag = TagWithSpec<string>("UnicodeTag", "DB1500.DBW0", spec)
        tag.Value.Value <- "한글"

        let json = JsonConvert.SerializeObject(tag)
        let deserialized = JsonConvert.DeserializeObject<TagWithSpec<string>>(json)

        deserialized.Tag.Name === "UnicodeTag"
        deserialized.Tag.Value.Value === "한글"
        match deserialized.ValueSpec with
        | Multiple values -> values |> List.contains "한글" === true
        | _ -> failwith "Expected Multiple ValueSpec"

    [<Test>]
    let ``TagWithSpec comparison after serialization`` () =
        let spec1 = Single 100
        let tag1 = TagWithSpec<int>("CompareTag", "DB1600.DBW0", spec1)
        tag1.Value.Value <- 200

        let json = JsonConvert.SerializeObject(tag1)
        let tag2 = JsonConvert.DeserializeObject<TagWithSpec<int>>(json)

        // Compare properties
        tag2.Name === tag1.Name
        tag2.Address === tag1.Address
        tag2.Value.Value === tag1.Value.Value
        (tag2.ValueSpec :> IValueSpec).Stringify() === (tag1.ValueSpec :> IValueSpec).Stringify()

    [<Test>]
    let ``TagWithSpec with custom JSON settings`` () =
        let settings = JsonSerializerSettings()
        settings.NullValueHandling <- NullValueHandling.Include
        settings.Formatting <- Formatting.None
        settings.DateFormatHandling <- DateFormatHandling.IsoDateFormat

        let spec = Single 42
        let tag = TagWithSpec<int>("SettingsTest", "DB1700.DBW0", spec)
        tag.Value.Value <- 100

        let json = JsonConvert.SerializeObject(tag, settings)
        let deserialized = JsonConvert.DeserializeObject<TagWithSpec<int>>(json, settings)

        deserialized.Tag.Name === "SettingsTest"
        deserialized.Tag.Value.Value === 100

    [<Test>]
    let ``TagWithSpec partial JSON deserialization`` () =
        // Create a complete tag first
        let originalTag = TagWithSpec<int>("PartialTag", "DB1800.DBW0", Single 50)
        originalTag.Value.Value <- 50

        let json1 = JsonConvert.SerializeObject(originalTag)
        let deserialized = JsonConvert.DeserializeObject<TagWithSpec<int>>(json1)
        let json2 = JsonConvert.SerializeObject(deserialized)

        deserialized.Tag.Name === "PartialTag"
        deserialized.Tag.Address === "DB1800.DBW0"
        deserialized.Tag.Value.Value === 50
        EmJson.IsJsonEquals(json1, json2) === true

    [<Test>]
    let ``TagWithSpec with negative values in ranges`` () =
        let spec = Ranges [{ Lower = Some (-100.0, Closed); Upper = Some (-10.0, Open) }]
        let tag = TagWithSpec<float>("NegativeTag", "DB1900.DBD0", spec)
        tag.Value.Value <- -50.0

        let json1 = JsonConvert.SerializeObject(tag)
        let deserialized = JsonConvert.DeserializeObject<TagWithSpec<float>>(json1)
        let json2 = JsonConvert.SerializeObject(deserialized)

        deserialized.Tag.Value.Value === -50.0
        EmJson.IsJsonEquals(json1, json2) === true

    [<Test>]
    let ``TagWithSpec.FromJson deserializes int correctly`` () =
        let originalTag = TagWithSpec<int>("IntTag", "DB100.DBW0", Single 42)
        originalTag.Value.Value <- 100

        let json = EmJson.ToJson(originalTag)
        let deserialized = TagWithSpec.FromJson(json)

        deserialized.Name === "IntTag"
        deserialized.Address === "DB100.DBW0"
        (deserialized.Value :?> int) === 100
        deserialized.ValueType === typeof<int>

    [<Test>]
    let ``TagWithSpec.FromJson deserializes float correctly`` () =
        let originalTag = TagWithSpec<float>("FloatTag", "DB200.DBD0", Multiple [1.0; 2.0; 3.0])
        originalTag.Value.Value <- 2.5

        let json = EmJson.ToJson(originalTag)
        let deserialized = TagWithSpec.FromJson(json)

        deserialized.Name === "FloatTag"
        deserialized.Address === "DB200.DBD0"
        (deserialized.Value :?> float) === 2.5
        deserialized.ValueType === typeof<float>

    [<Test>]
    let ``TagWithSpec.FromJson deserializes string correctly`` () =
        let originalTag = TagWithSpec<string>("StringTag", "DB300.DBW0", Single "test")
        originalTag.Value.Value <- "hello world"

        let json = EmJson.ToJson(originalTag)
        let deserialized = TagWithSpec.FromJson(json)

        deserialized.Name === "StringTag"
        deserialized.Address === "DB300.DBW0"
        (deserialized.Value :?> string) === "hello world"
        deserialized.ValueType === typeof<string>

    [<Test>]
    let ``TagWithSpec.FromJson deserializes bool correctly`` () =
        let originalTag = TagWithSpec<bool>("BoolTag", "DB400.DBX0.0", Single false)
        originalTag.Value.Value <- true

        let json = EmJson.ToJson(originalTag)
        let deserialized = TagWithSpec.FromJson(json)

        deserialized.Name === "BoolTag"
        deserialized.Address === "DB400.DBX0.0"
        (deserialized.Value :?> bool) === true
        deserialized.ValueType === typeof<bool>

    [<Test>]
    let ``TagWithSpec.FromJson preserves ValueSpec correctly`` () =
        let spec = Ranges [
            { Lower = Some (10.0, Closed); Upper = Some (50.0, Open) }
        ]
        let originalTag = TagWithSpec<float>("RangeTag", "DB500.DBD0", spec)
        originalTag.Value.Value <- 25.0

        let json = EmJson.ToJson(originalTag)
        let deserialized = TagWithSpec.FromJson(json)

        // Re-serialize and compare
        let json2 = JsonConvert.SerializeObject(deserialized.Tag)
        EmJson.IsJsonEquals(json, json2) === false // Because deserialized.Tag is just the PlcTag part

        // Check the values
        deserialized.Name === "RangeTag"
        (deserialized.Value :?> float) === 25.0

    [<Test>]
    let ``TagWithSpec.FromJson handles DateTime correctly`` () =
        let now = DateTime.Now.TruncateToSecond()
        let originalTag = TagWithSpec<DateTime>("TimeTag", "DB600.DBW0", Single now)
        originalTag.Value.Value <- now.AddHours(1.0)

        let json = EmJson.ToJson(originalTag)
        let deserialized = TagWithSpec.FromJson(json)

        deserialized.Name === "TimeTag"
        deserialized.ValueType === typeof<DateTime>
        // Compare with second precision
        let deserializedTime = deserialized.Value :?> DateTime
        Math.Abs((deserializedTime - now.AddHours(1.0)).TotalSeconds) < 1.0 === true

    [<Test>]
    let ``TagWithSpec.FromJson round trip test`` () =
        let originalTag = TagWithSpec<int>("RoundTripTag", "DB700.DBW0", Multiple [10; 20; 30])
        originalTag.Value.Value <- 20

        let json1 = EmJson.ToJson(originalTag)
        let deserialized = TagWithSpec.FromJson(json1)

        // Cast back to specific type
        let tagWithSpec = deserialized :?> TagWithSpec<int>
        tagWithSpec.Name === "RoundTripTag"
        tagWithSpec.Value.Value === 20

    [<Test>]
    let ``TagWithSpec.FromJson handles mixed type collection`` () =
        let tags = [
            TagWithSpec<int>("Tag1", "DB1", Single 10) :> ITagWithSpec
            TagWithSpec<float>("Tag2", "DB2", Multiple [1.0; 2.0]) :> ITagWithSpec
            TagWithSpec<string>("Tag3", "DB3", Single "test") :> ITagWithSpec
        ]

        let jsons = tags |> List.map EmJson.ToJson
        let deserialized = jsons |> List.map TagWithSpec.FromJson

        deserialized.Length === 3
        deserialized.[0].ValueType === typeof<int>
        deserialized.[1].ValueType === typeof<float>
        deserialized.[2].ValueType === typeof<string>