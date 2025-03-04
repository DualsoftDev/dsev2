namespace PLC.CodeGen.Common

open Dual.Common.Core.FS
open Engine.Core

[<AutoOpen>]
module rec Command =

    //type VarKind =
    //    | None     = 0
    //    | Variable = 1
    //    | Constant = 2


    ///// Command 를 위한 Tag
    //type CommandTag(tag:string, size:Size, kind:VarKind) =
    //    interface INamedExpressionizableTerminal with
    //        member _.StorageName = tag
    //        member _.ToText() = tag
    //    member _.Size() = size
    //    member _.SizeString =
    //        match size with
    //        | IEC61131.Size.Bit   -> "BOOL"
    //        | IEC61131.Size.Byte  -> "BYTE"
    //        | IEC61131.Size.Word  -> "WORD"
    //        | IEC61131.Size.DWord -> "DWORD"
    //        |_-> failwithlog "Unknown tag Size"
    //    member _.ToText() = tag
    //    member _.VarKind() = kind


    type IFunctionCommand =
        abstract member TerminalEndTag: INamedExpressionizableTerminal with get

    ///CoilOutput은 단일 출력을 내보내는 형식
    type CoilOutputMode =
        | COMCoil of INamedExpressionizableTerminal
        | COMPulseCoil of INamedExpressionizableTerminal
        | COMNPulseCoil of INamedExpressionizableTerminal
        | COMClosedCoil of INamedExpressionizableTerminal
        | COMSetCoil of INamedExpressionizableTerminal
        | COMResetCoil of INamedExpressionizableTerminal

        interface IFunctionCommand with
            member this.TerminalEndTag: INamedExpressionizableTerminal =
                match this with
                | COMCoil(endTag) ->  endTag
                | COMPulseCoil(endTag) -> endTag
                | COMNPulseCoil(endTag) -> endTag
                | COMClosedCoil(endTag) -> endTag
                | COMSetCoil(endTag) -> endTag
                | COMResetCoil(endTag) -> endTag

    /// bool type 을 반환하는 pure function.   (instance 불필요)
    type Predicate =
        | Compare of name: string * output: INamedExpressionizableTerminal * arguments: IExpression list //endTag * FunctionName * Tag list

        interface IFunctionCommand with
            member this.TerminalEndTag: INamedExpressionizableTerminal =
                match this with
                | Compare(_, endTag, _) -> endTag

    /// non-boolean 값을 반환하는 pure function.  (instance 불필요)
    type Function =
        //| CopyMode  of INamedExpressionizableTerminal *  (CommandTag * CommandTag) //endTag * (fromA, toB)
        | Arithmetic of name: string * output: INamedExpressionizableTerminal * arguments: IExpression list //endTag * FunctionName * Tag list

        interface IFunctionCommand with
            member this.TerminalEndTag: INamedExpressionizableTerminal =
                match this with
                //| CopyMode  (endTag, _) -> endTag
                | Arithmetic(_, endTag, _) -> endTag

    type PLCAction = Move of condition: IExpression<bool> * source: IExpression * target: IStorage
