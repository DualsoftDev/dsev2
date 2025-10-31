namespace T

open NUnit.Framework
open Dual.Common.UnitTest.FS
open Ev2.Gen


[<AutoOpen>]
type ArithmaticTest() =
    [<Test>]
    member _.``변수``() =
        let v1 = VarBindingFB<int>("Var1", initValue=10)
        v1.Name === "Var1"
        v1.InitValue === Some 10
        // 선언용 변수는 값에 접근 불가
        (fun () -> let v1v = v1 :> IVariable<int> in v1v.Value) |> ShouldFail


    [<Test>]
    member _.``Arithmatics``() =
        add_n16 [| literal 3s; literal 5s |]           |> _.TValue === 8s
        add_N16 [| literal 3us; literal 5us |]         |> _.TValue === 8us
        add_n32 [| literal 3; literal 5 |]             |> _.TValue === 8
        add_n32 [| literal 3; literal 4; literal 5 |]  |> _.TValue === 12
        add_f32 [| literal 3.14f; literal 3.14f |]     |> _.TValue === 6.28f
        add_f64 [| literal 3.14; literal 3.14 |]       |> _.TValue === 6.28

        let f =
            let hello = (coil<string> "First" "Hello") :> IExpression<string>
            let world = (coil<string> "Second" "World") :> IExpression<string>
            add_String [| hello; world |]
        f.TValue === "HelloWorld"


        // 1 + (3 * 5) = 16
        add_n32 [| literal 1; mul_n32 [| literal 3; literal 5 |] |] |> _.TValue === 16

    [<Test>]
    member _.``Comparisons``() =
        ge_n32 (literal 3) (literal 4) |> _.TValue === false
        ge_n32 (literal 4) (literal 3) |> _.TValue === true
        ()