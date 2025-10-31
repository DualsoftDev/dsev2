namespace T

open NUnit.Framework
open System
open Ev2.Core.FS
open Newtonsoft.Json
open Dual.Common.UnitTest.FS
open Dual.Common.Base
open Ev2.Gen

[<AutoOpen>]
module InstanceCreationTestModule =
    [<Test>]
    let ``Create Var`` () =
        let tb1 = new Variable<bool>(Value=true)
        tb1.DataType === typeof<bool>
        tb1.Value === true
        ()

    [<Test>]
    let ``Create Literals`` () =
        let pi = new Literal<double>(3.14)
        pi.DataType === typeof<double>
        pi.Value === 3.14

type StructTest() =
    [<Test>]
    member _.``Create Struct``() =
        let person =
            new Struct(
                "Person",
                [|
                    Variable<string>("Name", Value="Kim") :> IVariable
                    Variable<int>("Age", Value=16)
                    new Array<string>(
                        "Favorites",
                        [| Ev2.Gen.Range(0, 10) |],
                        Value = [| "코딩"; "음악"; "여행" |]
                    )
                    new Struct(
                        "Contact",
                        [|
                            Variable<string>("Address", Value="서울") :> IVariable
                            Variable<int>("Postal", Value=12345)
                            Variable<string>("Mobile", Value="010-1234-5678")
                        |]
                    )
                |])
        person.Name === "Person"
        person.Fields.Length === 4
        let name = person.GetField("Name")
        name.DataType === typeof<string>
        name.Value === "Kim"
        let age = person.GetField("Age")
        age.DataType === typeof<int>
        age.Value === 16

        let fav = person.GetField("Favorites")
        fav.DataType === typeof<Array<string>>
        let favArray = fav :?> Array<string>
        favArray.ElementDataType === typeof<string>
        match favArray.Value with
        | null ->
            Assert.Fail("Favorites 배열의 초기값이 설정되지 않았습니다.")
        | value ->
            value.GetType() === typeof<string[]>
            let initValues = value :?> string[]
            initValues.Length === 3
            initValues[0] === "코딩"
            initValues[1] === "음악"
            initValues[2] === "여행"

        let contact = person.GetField("Contact") :?> Struct
        contact.DataType === typeof<Struct>
        contact.GetField("Address").DataType === typeof<string>
        contact.GetField("Postal").DataType === typeof<int>
        contact.GetField("Mobile").DataType === typeof<string>

