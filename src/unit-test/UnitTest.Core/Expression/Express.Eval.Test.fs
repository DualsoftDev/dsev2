namespace T

open System.Reflection
open NUnit.Framework

open Dual.Common.Core.FS
open Dual.Common.UnitTest.FS
open Dual.Ev2



[<AutoOpen>]
module ExpressionTestModule =

    [<TestFixture>]
    type ExpressionTest() =
        let t1 = TTerminal(3)
        let t2 = TTerminal(4)

        let args = [t1 :> IExpression; t2]
        let iExpAdd:IExpression = createCustomFunctionExpression<int> "+" args
        let tExpAdd = iExpAdd :?> IExpression<int>
        let ntAdd = iExpAdd :?> TNonTerminal<int>

        let xxx = tryConvert<double>(-3.14)
        let yyy = xxx

        [<Test>]
        member _.Minimal() =
            iExpAdd.Evaluate()  === 7
            ntAdd  .Evaluate()  === 7
            tExpAdd.Evaluate()  === 7
            ntAdd  .TEvaluate() === 7
            tExpAdd.TEvaluate() === 7
            let oValue:obj = iExpAdd.Evaluate()
            let nValue2:int = ntAdd.TEvaluate()
            let oValue2:obj = ntAdd.Evaluate()
            noop()

            (fAbs<int> [TTerminal(-20)]).Evaluate() === 20
            (fAbs<double> [TValueHolder(-3.14)]).Evaluate() === 3.14
            (fBitwiseNot<int> [TValueHolder(0)]).Evaluate() === ~~~0
            (fBitwiseNot<uint64> [TValueHolder(32UL)]).Evaluate() === ~~~32UL
            (fBitwiseAnd<uint64> [TValueHolder(29UL); TValueHolder(252UL)]).Evaluate() === (29UL&&&252UL)

            (fShiftLeft<uint64> [TValueHolder(1UL); TValueHolder(2)]).Evaluate() === (1UL<<<2)

            [1..10] |> map TValueHolder<int> |> List.cast<IExpression> |> fAdd<int> |> _.Evaluate()  === 55

            //let opAdd = TNonTerminal<int>(Op.OpArithmetic "+", [t1 :> IExpression; t2])
            //let nValue:int = opAdd.TEvaluate()

            noop()

