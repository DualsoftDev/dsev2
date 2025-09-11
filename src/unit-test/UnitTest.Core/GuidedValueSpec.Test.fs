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