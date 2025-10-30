namespace T

open NUnit.Framework
open Dual.Common.UnitTest.FS
open Ev2.Gen


[<AutoOpen>]
module internal PouTestHelperModule =
    let trueValue  = Literal<bool>(true)
    let falseValue = Literal<bool>(false)
    let boolContact name  = Var<bool>(name) :> IVariable<bool>
    let coil<'T> name value  = Variable<'T>(name, Value=value) :> IVariable<'T>
    let literal<'T> (value:'T) = Literal<'T>(value) :> IExpression<'T>

