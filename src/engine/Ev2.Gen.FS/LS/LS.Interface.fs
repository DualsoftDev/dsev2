namespace Ev2.Gen

open System
open System.Collections.Generic

[<AllowNullLiteral>]
type IWithType =
    abstract DataType : System.Type

[<AllowNullLiteral>]
type IWithValue =
    /// Opaque Value
    abstract Value : obj with get, set

[<AllowNullLiteral>] type IProject        = interface end
[<AllowNullLiteral>] type IProgram        = interface end
[<AllowNullLiteral>] type IFunctionProgram = inherit IProgram inherit IWithType
[<AllowNullLiteral>] type IFBProgram      = inherit IProgram
[<AllowNullLiteral>] type IValue          = interface end
[<AllowNullLiteral>] type ICommand        = interface end      // copy, move
[<AllowNullLiteral>] type IStruct         = interface end
[<AllowNullLiteral>] type IExpression     = inherit IWithType inherit IWithValue
[<AllowNullLiteral>] type IFunctionCall   = interface end   //inherit IExpression
[<AllowNullLiteral>] type IFBCall         = interface end

type Range = int * int

[<AllowNullLiteral>]
type IArray =
    inherit IWithType
    abstract Dimensions : Range[]

[<AllowNullLiteral>]
type ITerminal =
    inherit IExpression
    //abstract Value    : obj

[<AllowNullLiteral>]
type IVariable =
    inherit ITerminal
    abstract Name     : string

[<AllowNullLiteral>] type ILiteral = inherit ITerminal


[<AllowNullLiteral>]
type TValue<'T> =
    /// Typed Value
    abstract TValue : 'T

[<AllowNullLiteral>]
type IExpression<'T> =
    inherit IExpression
    inherit TValue<'T>

[<AllowNullLiteral>]
type IVariable<'T> =
    inherit IVariable
    inherit IExpression<'T>

[<AllowNullLiteral>] type ILiteral<'T>    = inherit ILiteral  inherit IExpression<'T>
[<AllowNullLiteral>] type ITerminal<'T>   = inherit ITerminal inherit IExpression<'T>

[<AllowNullLiteral>]
type IStorage =
    abstract IVariables : IVariable[]





[<AllowNullLiteral>] type IStatement      = interface end

