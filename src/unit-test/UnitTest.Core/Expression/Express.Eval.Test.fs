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
        let t1 = TTerminal(3)
        let t2 = TTerminal(4)

        let iExpAdd:IExpression = createCustomFunctionExpression "+" [t1 :> IExpression; t2]
        let tExpAdd = iExpAdd :?> IExpression<int>
        let ntAdd = iExpAdd :?> TNonTerminal<int>

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

            //let opAdd = TNonTerminal<int>(Op.OpArithmetic "+", [t1 :> IExpression; t2])
            //let nValue:int = opAdd.TEvaluate()

