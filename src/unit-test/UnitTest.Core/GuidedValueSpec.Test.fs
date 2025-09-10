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