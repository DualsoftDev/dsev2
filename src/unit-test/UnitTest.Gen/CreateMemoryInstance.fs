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

    [<Test>]
    let ``Create Struct`` () =
        //let contactStruct =
        //    let fields:IVariable[] = [|
        //        new Var<string>("Address") :> IVariable
        //        new Var<int>("Postal")
        //        new Var<string>("Mobile")
        //    |]

        //    new Struct("Contact", fields)


        let fields:IVariable[] = [|
            new Var<string>("Name") :> IVariable
            new Var<int>("Age")
            new Array<string>("Favorites", [| Ev2.Gen.Range(0, 10) |])
        |]

        let person = new Struct("Person", fields)
        person.Name === "Person"
        person.Fields.Length === 3
        let name = person.GetField("Name")
        name.DataType === typeof<string>
        let age = person.GetField("Age")
        age.DataType === typeof<int>

        let fav = person.GetField("Favorites")
        fav.DataType === typeof<Array<string>>
        (fav :?> Array<string>).InnerDataType === typeof<string>
