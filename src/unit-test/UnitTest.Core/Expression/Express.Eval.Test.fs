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
        let t1 = TTerminal(3)
        let t2 = TTerminal(4)
        let args:Args = [t1 :> IExpression; t2]

        [<Test>]
        member _.Function() =
            let funcCustomAdd = cf<int> "+"
            funcCustomAdd args === 7

            (fAbs<int> [TTerminal(-20)]) === 20
            fAbs<double> [TValueHolder(-3.14)] === 3.14
            fBitwiseNot<int> [TValueHolder(0)] === ~~~0
            fBitwiseNot<uint64> [TValueHolder(32UL)] === ~~~32UL
            fBitwiseAnd<uint64> [TValueHolder(29UL); TValueHolder(252UL)] === (29UL&&&252UL)

            (fShiftLeft<uint64> [TValueHolder(1UL); TValueHolder(2)]) === (1UL<<<2)

            [1..10] |> map TValueHolder<int> |> List.cast<IExpression> |> fAdd<int> === 55


            fConcat [TValueHolder("Hello"); TValueHolder("World")] === "HelloWorld"

        [<Test>]
        member _.Invalid() =
            (fun () -> cf "!"      [TTerminal(32); ]                  |> ignore) |> ShouldFailWithSubstringT "Unable to cast object"
            (fun () -> cf<int> "+" [TTerminal(2); TTerminal(3.0)]     |> ignore) |> ShouldFailWithSubstringT "Unable to cast object"
            (fun () -> cf<int> "+" [TTerminal(2); ]                   |> ignore) |> ShouldFailWithSubstringT "Wrong number of arguments"

        [<Test>]
        member _.SpecialFunctions() =
            cf "sin" [TTerminal(3.14/4.0); ] === Math.Sin(3.14/4.0)
            cf "sin" [TTerminal(3.14f/4.0f); ] === single (Math.Sin(3.14/4.0))
            (cf "sin" [TTerminal(3.14f/4.0f); ]).GetType() === typeof<single>



        [<Test>]
        member _.PredefinedFunctions() =
            cf "!" [TTerminal(false); ] === true

            // Binary fuction 에 한해, function type 지정안하면, argument 의 type 으로 결정됨.
            cf "+" [TTerminal(2); TTerminal(3)] === 5
            cf "*" [TTerminal(2); TTerminal(3)] === 6
            cf "&" [TTerminal(33); TTerminal(64)] === (33 &&& 64)

            cf<int> "+" [TTerminal(2); TTerminal(3)] === 5
            cf<int> "*" [TTerminal(2); TTerminal(3)] === 6
            cf<int> "&" [TTerminal(33); TTerminal(64)] === (33 &&& 64)

            let t = TTerminal(true)
            let f = TTerminal(false)
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



            cf<bool> "bool" [TTerminal(2)] === true
            cf<bool> "bool" [TTerminal(0)] === false
            cf<bool> "int" [TTerminal(3.14)] === 3
            cf<bool> "double" [TTerminal(3)] === 3.0
            cf<double> "sin" [TTerminal(3.14)] === Math.Sin(3.14)
            cf<bool> ">=" [TTerminal(3); TTerminal(1)] === true
            cf<bool> "<=" [TTerminal(3); TTerminal(1)] === false

            cf<bool> "==" [TTerminal("Hello"); TTerminal("Hello")] === true
            cf<bool> "==" [TTerminal("Hello"); TTerminal("World")] === false
            cf<bool> "<>" [TTerminal("Hello"); TTerminal("Hello")] === false
            cf<bool> "<>" [TTerminal("Hello"); TTerminal("World")] === true

            cf<bool> "==" [TTerminal(1); TTerminal(1)] === true
            cf<bool> "==" [TTerminal(1); TTerminal(2)] === false
            cf<bool> "<>" [TTerminal(1); TTerminal(1)] === false
            cf<bool> "<>" [TTerminal(1); TTerminal(2)] === true


            let gt = cf ">="
            gt [TTerminal(3); TTerminal(1)] === true


        [<Test>]
        member _.NonTerminal() =
            let t1 = TTerminal(3)
            let t2 = TTerminal(4)
            let t3 = TTerminal(5)
            let args:Args = [t1 :> IExpression; t2; t3]

            let nt = TNonTerminal<int>.Create(Op.PredefinedOperator "+", args)
            nt.Value === 12

            // expression tree 상의 값 수정 해도, invalidate() 수행 전까지는 동일 cache 값 반환
            t1.Value <- 5
            nt.Value === 12
            nt.Invalidate()
            nt.Value === 14



