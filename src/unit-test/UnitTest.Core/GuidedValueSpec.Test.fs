namespace T

open NUnit.Framework
open System
open Ev2.Core.FS
open Newtonsoft.Json
open Dual.Common.UnitTest.FS
open Dual.Common.Base

[<AutoOpen>]
module GuidedValueSpecTestModule =
    [<Test>]
    let ``GuidedValueSpec can be created with Single value`` () =
        let guid = Guid.NewGuid()
        let valueSpec = Single 42 :> IValueSpec
        let guidedSpec = GuidedValueSpec(guid, valueSpec)

        guidedSpec.Guid === guid
        guidedSpec.ValueSpec === valueSpec

    [<Test>]
    let ``GuidedValueSpec can be created with Multiple values`` () =
        let guid = Guid.NewGuid()
        let valueSpec = Multiple [1; 2; 3] :> IValueSpec
        let guidedSpec = GuidedValueSpec(guid, valueSpec)

        guidedSpec.Guid === guid
        guidedSpec.ValueSpec === valueSpec

    [<Test>]
    let ``GuidedValueSpec with Single value serializes and deserializes correctly`` () =
        let guid = Guid.NewGuid()
        let valueSpec = Single 3.14 :> IValueSpec
        let guidedSpec = GuidedValueSpec(guid, valueSpec)

        // GuidedValueSpec 전체 직렬화
        let json = guidedSpec.ToJson()
        //let json = EmJson.ToJson guidedSpec
        let deserialized = GuidedValueSpec.FromJson(json)

        deserialized.Guid === guid
        deserialized.ValueSpec.Stringify() === valueSpec.Stringify()

    [<Test>]
    let ``GuidedValueSpec with Multiple values serializes and deserializes correctly`` () =
        let guid = Guid.NewGuid()
        let valueSpec = Multiple [true; false; true] :> IValueSpec
        let guidedSpec = GuidedValueSpec(guid, valueSpec)

        // GuidedValueSpec 전체 직렬화
        let json = guidedSpec.ToJson()
        let deserialized = GuidedValueSpec.FromJson(json)

        deserialized.Guid === guid
        deserialized.ValueSpec.Stringify() === valueSpec.Stringify()

    [<Test>]
    let ``GuidedValueSpec with Ranges serializes and deserializes correctly`` () =
        let guid = Guid.NewGuid()
        let valueSpec =
            Ranges [
                { Lower = Some (3.0, Open); Upper = Some (7.0, Closed) }
            ] :> IValueSpec
        let guidedSpec = GuidedValueSpec(guid, valueSpec)

        // ValueSpec 부분만 따로 직렬화
        let valueJson = valueSpec.Jsonize()
        let deserializedValue = IValueSpec.Deserialize(valueJson)

        guidedSpec.ValueSpec.Stringify() === valueSpec.Stringify()
        deserializedValue.Stringify() === valueSpec.Stringify()

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