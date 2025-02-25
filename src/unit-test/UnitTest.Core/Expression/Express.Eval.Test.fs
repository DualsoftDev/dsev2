namespace T

open System.Reflection
open NUnit.Framework

open Dual.Common.Core.FS
open Dual.Common.UnitTest.FS
open Dual.Ev2
open System



[<AutoOpen>]
module ExpressionTestModule =

    [<TestFixture>]
    type ExpressionTest() =
        let t1 = TValue(3)
        let t2 = TValue(4)
        let args:Args = [t1 :> IExpression; t2]

        [<Test>]
        member _.Function() =
            let funcCustomAdd = cf<int> "+"
            funcCustomAdd args === 7

            (fAbs<int> [TValue(-20)]) === 20
            fAbs<double> [TValue(-3.14)] === 3.14
            fBitwiseNot<int> [TValue(0)] === ~~~0
            fBitwiseNot<uint64> [TValue(32UL)] === ~~~32UL
            fBitwiseAnd<uint64> [TValue(29UL); TValue(252UL)] === (29UL&&&252UL)

            (fShiftLeft<uint64> [TValue(1UL); TValue(2)]) === (1UL<<<2)

            [1..10] |> map TValue<int> |> List.cast<IExpression> |> fAdd<int> === 55


            fConcat [TValue("Hello"); TValue("World")] === "HelloWorld"

        [<Test>]
        member _.Invalid() =
            (fun () -> cf "!"      [TValue(32); ]                  |> ignore) |> ShouldFailWithSubstringT "Unable to cast object"
            (fun () -> cf<int> "+" [TValue(2); TValue(3.0)]     |> ignore) |> ShouldFailWithSubstringT "Unable to cast object"
            (fun () -> cf<int> "+" [TValue(2); ]                   |> ignore) |> ShouldFailWithSubstringT "Wrong number of arguments"

        [<Test>]
        member _.SpecialFunctions() =
            cf "sin" [TValue(3.14/4.0); ] === Math.Sin(3.14/4.0)
            cf "sin" [TValue(3.14f/4.0f); ] === single (Math.Sin(3.14/4.0))
            (cf "sin" [TValue(3.14f/4.0f); ]).GetType() === typeof<single>



        [<Test>]
        member _.PredefinedFunctions() =
            cf "!" [TValue(false); ] === true

            // Binary fuction 에 한해, function type 지정안하면, argument 의 type 으로 결정됨.
            cf "+" [TValue(2); TValue(3)] === 5
            cf "*" [TValue(2); TValue(3)] === 6
            cf "&" [TValue(33); TValue(64)] === (33 &&& 64)

            cf<int> "+" [TValue(2); TValue(3)] === 5
            cf<int> "*" [TValue(2); TValue(3)] === 6
            cf<int> "&" [TValue(33); TValue(64)] === (33 &&& 64)

            let t = TValue(true)
            let f = TValue(false)
            cf<bool> "!" [t] === false
            cf<bool> "!" [f] === true

            cf<bool> "&&" [t; t] === true
            cf<bool> "&&" [t; f] === false
            cf<bool> "&&" [f; t] === false
            cf<bool> "&&" [f; f] === false

            cf<bool> "||" [t; t] === true
            cf<bool> "||" [t; f] === true
            cf<bool> "||" [f; t] === true
            cf<bool> "||" [f; f] === false

            cf<bool> "&&" [t; t; t; t] === true
            cf<bool> "&&" [t; t; t; f] === false
            cf<bool> "||" [f; f; f; t] === true
            cf<bool> "||" [f; f; f; f] === false

            // PC 환경에서는 아직 evaluation 안됨.
            //createCustomFunction<bool> "rising" [f] === false



            cf<bool> "bool" [TValue(2)] === true
            cf<bool> "bool" [TValue(0)] === false
            cf<bool> "int" [TValue(3.14)] === 3
            cf<bool> "double" [TValue(3)] === 3.0
            cf<double> "sin" [TValue(3.14)] === Math.Sin(3.14)
            cf<bool> ">=" [TValue(3); TValue(1)] === true
            cf<bool> "<=" [TValue(3); TValue(1)] === false

            cf<bool> "==" [TValue("Hello"); TValue("Hello")] === true
            cf<bool> "==" [TValue("Hello"); TValue("World")] === false
            cf<bool> "<>" [TValue("Hello"); TValue("Hello")] === false
            cf<bool> "<>" [TValue("Hello"); TValue("World")] === true

            cf<bool> "==" [TValue(1); TValue(1)] === true
            cf<bool> "==" [TValue(1); TValue(2)] === false
            cf<bool> "<>" [TValue(1); TValue(1)] === false
            cf<bool> "<>" [TValue(1); TValue(2)] === true


            let gt = cf ">="
            gt [TValue(3); TValue(1)] === true


        [<Test>]
        member _.NonTerminal() =
            let t1 = TValue(3)
            let t2 = TValue(4)
            let t3 = TValue(5)
            let args:Args = [t1 :> IExpression; t2; t3]

            let nt = TFunction<int>.Create(Op.PredefinedOperator "+", args)
            nt.Value === 12

            // expression tree 상의 값 수정 해도, invalidate() 수행 전까지는 동일 cache 값 반환
            t1.Value <- 5
            nt.Value === 12
            nt.Invalidate()
            nt.Value === 14



