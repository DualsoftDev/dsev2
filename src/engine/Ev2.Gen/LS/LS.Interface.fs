namespace Ev2.Gen

open System
open System.Collections.Generic

[<AllowNullLiteral>] type IProject        = interface end
[<AllowNullLiteral>] type IProgram        = interface end
[<AllowNullLiteral>] type IValue          = interface end
[<AllowNullLiteral>] type ICommand        = interface end      // copy, move
[<AllowNullLiteral>] type IStruct         = interface end
[<AllowNullLiteral>] type IExpression     = interface end
[<AllowNullLiteral>] type IFunctionCall   = inherit IExpression
[<AllowNullLiteral>] type IFBCall         = interface end

type Range = int * int

[<AllowNullLiteral>]
type IArray =
    abstract DataType : System.Type
    abstract Dimensions : Range[]

[<AllowNullLiteral>]
type ITerminal =
    inherit IExpression
    abstract DataType : System.Type

[<AllowNullLiteral>]
type IVariable =
    inherit ITerminal
    abstract Name     : string

[<AllowNullLiteral>] type ILiteral = inherit ITerminal

[<AllowNullLiteral>] type IVariable<'T>   = inherit IVariable
[<AllowNullLiteral>] type ILiteral<'T>    = inherit ILiteral
[<AllowNullLiteral>] type IExpression<'T> = inherit IExpression
//[<AllowNullLiteral>] type IStorage      = interface end


