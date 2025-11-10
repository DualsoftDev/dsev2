namespace T

open System
open NUnit.Framework
open Dual.Common.UnitTest.FS
open Ev2.Core.FS.IR
open Ev2.Core.FS.IR


[<AutoOpen>]
type ArithmaticTest() =
    //[<Test>]
    //member _.``변수``() =
    //    let v1 = VarBindingFB<int>("Var1", initValue=10)
    //    v1.Name === "Var1"
    //    v1.InitValue === Some 10
    //    // 선언용 변수는 값에 접근 불가
    //    (fun () -> let v1v = v1 :> IVariable<int> in v1v.Value) |> ShouldFail


    [<Test>]
    member _.``Arithmatics``() =
        add<int16>  [| literal 3s; literal 5s |]           |> _.TValue === 8s
        add<uint16> [| literal 3us; literal 5us |]         |> _.TValue === 8us
        add<int32>  [| literal 3; literal 5 |]             |> _.TValue === 8
        add<int32>  [| literal 3; literal 4; literal 5 |]  |> _.TValue === 12
        add<single> [| literal 3.14f; literal 3.14f |]     |> _.TValue === 6.28f
        add<double> [| literal 3.14; literal 3.14 |]       |> _.TValue === 6.28

        add         [| literal 3s; literal 5s |]           |> _.TValue === 8s
        add         [| literal 3us; literal 5us |]         |> _.TValue === 8us
        add         [| literal 3; literal 5 |]             |> _.TValue === 8
        add         [| literal 3; literal 4; literal 5 |]  |> _.TValue === 12
        add         [| literal 3.14f; literal 3.14f |]     |> _.TValue === 6.28f
        add         [| literal 3.14; literal 3.14 |]       |> _.TValue === 6.28


        let f =
            let hello = (coil<string> "First" "Hello") :> IExpression<string>
            let world = (coil<string> "Second" "World") :> IExpression<string>
            add<string> [| hello; world |]
        f.TValue === "HelloWorld"


        // 1 + (3 * 5) = 16
        add<int32> [| literal 1; mul<int32> [| literal 3; literal 5 |] |] |> _.TValue === 16

        pow<int32>(literal 2, literal 3) |> _.TValue === 8
        pow(literal 2s, literal 3) |> _.TValue === 8s

        modulo<int32> [| literal 2; literal 3 |] |> _.TValue === 2
        modulo<int32> [| literal 3; literal 2 |] |> _.TValue === 1

        cast<int16>(literal 3) |> _.TValue === 3s
        cast<uint16>(literal 3) |> _.TValue === 3us
        cast<int16>(Variable("MyVar", 3)) |> _.TValue === 3s

        cos(literal Math.PI) |> _.TValue === -1.0
        cos(literal (single Math.PI)) |> _.TValue === -1.0f

    [<Test>]
    member _.``Comparisons``() =
        ge<int32> (literal 3) (literal 4) |> _.TValue === false
        ge<int32> (literal 4) (literal 3) |> _.TValue === true
        ()