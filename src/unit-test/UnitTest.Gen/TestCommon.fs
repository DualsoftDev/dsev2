namespace T

open NUnit.Framework
open Dual.Common.UnitTest.FS
open Ev2.Gen
open Dual.Common.Base


[<AutoOpen>]
module internal PouTestHelperModule =
    let trueValue  = Literal<bool>(true)
    let falseValue = Literal<bool>(false)
    let boolContact name  = Variable<bool>(name) :> IVariable<bool>
    let coil<'T> name value  = Variable<'T>(name, Value=value) :> IVariable<'T>
    let literal<'T> (value:'T) = Literal<'T>(value) :> IExpression<'T>

    let inline createAdd2Function<'T when 'T : (static member (+) : 'T * 'T -> 'T)>(globalStorage:Storage, name:string option) =
        let name = name |? $"Add2_{typeof<'T>.Name}"
        let num1 = Variable<'T>("Num1", varType=VarType.VarInput)
        let num2 = Variable<'T>("Num2", varType=VarType.VarInput)
        let sum = Variable<'T>("Sum", varType=VarType.VarOutput)
        let returnVar = Variable<'T>(name, varType=VarType.VarReturn)
        let localStorage = Storage.Create([num1 :> IVariable; num2; sum])
        let stmt1 = AssignStatement( add<'T> [| num1; num2 |], sum) :> Statement
        let stmt2 = AssignStatement( sum, returnVar)
        FunctionProgram<'T>.Create(name, globalStorage, localStorage, [|stmt1; stmt2|], [||])

