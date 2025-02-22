namespace T

open NUnit.Framework

open Dual.Common.Core.FS
open Dual.Common.Base.FS
open Dual.Common.UnitTest.FS
open Dual.Ev2
open Dual.Common.Base.FS.SampleDataTypes


open Newtonsoft.Json
open System.Collections.Generic

[<AutoOpen>]
module ExpressionTestModule =

    [<TestFixture>]
    type ExpressionTest() =
        let t1 = TTerminal(123)
        let t2 = TTerminal(123)

        let iExpAdd:IExpression = createCustomFunctionExpression "+" [t1 :> IExpression; t2]
        let tExpAdd = iExpAdd :?> IExpression<int>
        let ntAdd = iExpAdd :?> TNonTerminal<int>

        let opAdd = TNonTerminal<int>(Op.OpArithmetic "+", [t1 :> IExpression; t2])
        [<Test>]
        member _.Minimal() =
            let nValue:int = opAdd.TEvaluate()
            let oValue:obj = iExpAdd.Evaluate()
            let nValue2:int = ntAdd.TEvaluate()
            let oValue2:obj = ntAdd.Evaluate()
            noop()


