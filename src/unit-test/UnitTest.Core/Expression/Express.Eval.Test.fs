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
            let funcCustomAdd = createCustomFunction<int> "+"
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
        member _.PredefinedFunction() =
            createCustomFunction<int> "+" [TTerminal(2); TTerminal(3)] === 5
            createCustomFunction<int> "*" [TTerminal(2); TTerminal(3)] === 6
            createCustomFunction<int> "&" [TTerminal(33); TTerminal(64)] === (33 &&& 64)

            let t = TTerminal(true)
            let f = TTerminal(false)
            createCustomFunction<bool> "!" [t] === false
            createCustomFunction<bool> "!" [f] === true

            createCustomFunction<bool> "&&" [t; t] === true
            createCustomFunction<bool> "&&" [t; f] === false
            createCustomFunction<bool> "&&" [f; t] === false
            createCustomFunction<bool> "&&" [f; f] === false

            createCustomFunction<bool> "||" [t; t] === true
            createCustomFunction<bool> "||" [t; f] === true
            createCustomFunction<bool> "||" [f; t] === true
            createCustomFunction<bool> "||" [f; f] === false

            createCustomFunction<bool> "&&" [t; t; t; t] === true
            createCustomFunction<bool> "&&" [t; t; t; f] === false
            createCustomFunction<bool> "||" [f; f; f; t] === true
            createCustomFunction<bool> "||" [f; f; f; f] === false

            // PC 환경에서는 아직 evaluation 안됨.
            //createCustomFunction<bool> "rising" [f] === false



            createCustomFunction<bool> "bool" [TTerminal(2)] === true
            createCustomFunction<bool> "bool" [TTerminal(0)] === false
            createCustomFunction<bool> "int" [TTerminal(3.14)] === 3
            createCustomFunction<bool> "double" [TTerminal(3)] === 3.0
            createCustomFunction<double> "sin" [TTerminal(3.14)] === Math.Sin(3.14)
            createCustomFunction<bool> ">=" [TTerminal(3); TTerminal(1)] === true
            createCustomFunction<bool> "<=" [TTerminal(3); TTerminal(1)] === false


            let gt = createCustomFunction ">="
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



