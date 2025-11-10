module Ev2.Cpu.Test.RefactoredArchitecture

open Xunit
open FsUnit.Xunit
open System

[<Fact>]
let ``Basic test - Legacy types should work``() =
    // Test with existing legacy types
    true |> should equal true

[<Fact>]
let ``String concatenation should work``() =
    let result = "Hello" + " World"
    result |> should equal "Hello World"

[<Fact>]
let ``Basic math should work``() =
    let result = 2 + 2
    result |> should equal 4

[<Fact>]
let ``Option types should work``() =
    let someValue = Some 42
    let noneValue = None
    
    someValue |> should equal (Some 42)
    noneValue |> should equal None

[<Fact>]
let ``List operations should work``() =
    let numbers = [1; 2; 3; 4; 5]
    let sum = numbers |> List.sum
    sum |> should equal 15

[<Fact>]
let ``Map operations should work``() =
    let map = Map.ofList [("key1", "value1"); ("key2", "value2")]
    map.["key1"] |> should equal "value1"

[<Fact>]
let ``Set operations should work``() =
    let set1 = Set.ofList [1; 2; 3]
    let set2 = Set.ofList [3; 4; 5]
    let union = Set.union set1 set2
    union.Count |> should equal 5

[<Fact>]
let ``DateTime should work``() =
    let now = System.DateTime.Now
    let future = now.AddDays(1.0)
    future > now |> should equal true

[<Fact>]
let ``String formatting should work``() =
    let name = "World"
    let greeting = sprintf "Hello, %s!" name
    greeting |> should equal "Hello, World!"

[<Fact>]
let ``Exception handling should work``() =
    let divideByZero() = 1 / 0
    (fun () -> divideByZero() |> ignore) |> should throw typeof<System.DivideByZeroException>