namespace Ev2.Gen

open System

[<AutoOpen>]
module LSLadder =
    type SystemFB() =
        member val Name: string = "" with get, set
        member val RungInCondition: IExpression = null with get, set
    type TimerFB() =
        inherit SystemFB()
        member val Parameters: (string * string) list = [] with get, set


    /// Ladder instruction
    type Instruction() =
        member val OpCode: string = "" with get, set
        member val Operands: string list = [] with get, set
    /// Ladder rung
    type Rung() =
        member val Instructions: Instruction list = [] with get, set
    type Ladder() =
        member val Rungs: Rung list = [] with get, set