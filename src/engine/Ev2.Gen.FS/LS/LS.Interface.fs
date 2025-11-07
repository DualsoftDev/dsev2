namespace Ev2.Gen

open System

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
[<AllowNullLiteral>] type IScanProgram    = inherit IProgram
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


(*
 * 글로벌/직접 변수: VAR_GLOBAL, VAR_GLOBAL_CONST
 * Program 로컬변수: VAR, VAR_CONST, VAR_EXTERNAL, VAR_EXTERNAL_CONST
 * FB 로컬변수: VAR, VAR_CONSTANT, VAR_INPUT, VAR_OUTPUT, VAR_IN_OUT, VAR_EXTERNAL, VAR_EXTERNAL_CONSTANT
 * Func 로컬변수: VAR, VAR_INPUT, VAR_OUTPUT, VAR_IN_OUT, VAR_RETURN
 *)

/// 변수 종류
type VarType =
    | VarUndefined
    | Var
    | VarConstant
    | VarInput
    | VarOutput
    | VarInOut
    | VarReturn         // Function 의 return.  Function 정의의 local 변수
    | VarExternal
    | VarExternalConstant
    | VarGlobal
    | VarGlobalConstant

[<AllowNullLiteral>]
type IVariable =
    inherit ITerminal
    abstract Name     : string
    abstract VarType  : VarType

[<AllowNullLiteral>] type ILiteral = inherit ITerminal


[<AllowNullLiteral>]
type TValue<'T> =
    /// Typed Value
    abstract TValue : 'T

[<AllowNullLiteral>]
type IExpression<'T> =
    inherit IExpression
    inherit TValue<'T>

[<AllowNullLiteral>] type ITerminal<'T> = inherit ITerminal inherit IExpression<'T>
[<AllowNullLiteral>] type IVariable<'T> = inherit IVariable inherit ITerminal<'T>
[<AllowNullLiteral>] type ILiteral<'T> = inherit ILiteral inherit ITerminal<'T>

[<AllowNullLiteral>] type IStorage = abstract IVariables : IVariable[]





[<AllowNullLiteral>] type IStatement = interface end

