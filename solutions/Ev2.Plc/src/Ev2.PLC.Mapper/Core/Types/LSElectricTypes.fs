namespace Ev2.PLC.Mapper.Core.Types

/// LS Electric XG5000 Ladder Element Type
/// Based on XG5000 PLC programming tool element type definitions
type LSElementType =
    | LDElementMode_Start    = 0
    | LineType_Start         = 0
    | VertLineMode           = 0   // LineType_Start '|'
    | HorzLineMode           = 1   // '-'
    | MultiHorzLineMode      = 2   // '-->>'
    /// add only here additional line type device.
    | LineType_End           = 5

    | ContactType_Start      = 6
    | ContactMode            = 6   // ContactType_Start // '-| |-'
    | ClosedContactMode      = 7   // '-|/|-'
    | PulseContactMode       = 8   // '-|P|-'
    | NPulseContactMode      = 9   // '-|N|-'
    | ClosedPulseContactMode = 10  // '-|P/|-'
    | ClosedNPulseContactMode= 11  // '-|N/|-'
    /// add only here additional contact type device.
    | ContactType_End        = 13

    | CoilType_Start         = 14
    | CoilMode               = 14  // CoilType_Start // '-( )-'
    | ClosedCoilMode         = 15  // '-(/)-'
    | SetCoilMode            = 16  // '-(S)-'
    | ResetCoilMode          = 17  // '-(R)-'
    | PulseCoilMode          = 18  // '-(P)-'
    | NPulseCoilMode         = 19  // '-(N)-'
    /// add only here additional coil type device.
    | CoilType_End           = 30

    | FunctionType_Start     = 31
    | FuncMode               = 32
    | FBMode                 = 33  // '-[F]-'
    | FBHeaderMode           = 34  // '-[F]-' : Header
    | FBBodyMode             = 35  // '-[F]-' : Body
    | FBTailMode             = 36  // '-[F]-' : Tail
    | FBInputMode            = 37
    | FBOutputMode           = 38
    /// add only here additional function type device.
    | FunctionType_End       = 45

    | BranchType_Start       = 51
    | SCALLMode              = 52
    | JMPMode                = 53
    | RetMode                = 54
    | SubroutineMode         = 55
    | BreakMode              = 56
    | ForMode                = 57
    | NextMode               = 58
    /// add only here additional branch type device.
    | BranchType_End         = 60

    | CommentType_Start      = 61
    | InverterMode           = 62  // '-*-'
    | RungCommentMode        = 63  // 'rung comment'
    | OutputCommentMode      = 64  // 'output comment'
    | LabelMode              = 65
    | EndOfPrgMode           = 66
    | RowCompositeMode       = 67  // 'row'
    | ErrorComponentMode     = 68
    | NullType               = 69
    | VariableMode           = 70
    | CellActionMode         = 71
    | RisingContact          = 72  // add dual xg5000 4.52
    | FallingContact         = 73  // add dual xg5000 4.52
    /// add only here additional comment type device.
    | CommentType_End        = 90

    /// vertical function(function & function block) related
    | VertFunctionType_Start = 100
    | VertFuncMode           = 101
    | VertFBMode             = 102
    | VertFBHeaderMode       = 103
    | VertFBBodyMode         = 104
    | VertFBTailMode         = 105
    /// add additional vertical function type device here
    | VertFunctionType_End   = 109
    | LDElementMode_End      = 110

    | Misc_Start             = 120
    | ArrowMode              = 121
    | Misc_End               = 122

/// LS Electric ladder element
type LSLadderElement = {
    ElementType: LSElementType
    Variable: string option
    Value: string option
    Row: int
    Column: int
    Description: string option
}

module LSElementType =
    /// Check if element type is in a specific range
    let isInRange (elementType: LSElementType) (startType: LSElementType) (endType: LSElementType) =
        let value = int elementType
        let startValue = int startType
        let endValue = int endType
        value >= startValue && value <= endValue

    /// Check if element type is a line
    let isLine (elementType: LSElementType) =
        isInRange elementType LSElementType.LineType_Start LSElementType.LineType_End

    /// Check if element type is a contact
    let isContact (elementType: LSElementType) =
        match elementType with
        | LSElementType.ContactMode
        | LSElementType.ClosedContactMode
        | LSElementType.PulseContactMode
        | LSElementType.NPulseContactMode
        | LSElementType.ClosedPulseContactMode
        | LSElementType.ClosedNPulseContactMode
        | LSElementType.RisingContact
        | LSElementType.FallingContact -> true
        | _ -> false

    /// Check if element type is a coil
    let isCoil (elementType: LSElementType) =
        match elementType with
        | LSElementType.CoilMode
        | LSElementType.ClosedCoilMode
        | LSElementType.SetCoilMode
        | LSElementType.ResetCoilMode
        | LSElementType.PulseCoilMode
        | LSElementType.NPulseCoilMode -> true
        | _ -> false

    /// Check if element type is a function
    let isFunction (elementType: LSElementType) =
        match elementType with
        | LSElementType.FuncMode
        | LSElementType.FBMode
        | LSElementType.FBHeaderMode
        | LSElementType.FBBodyMode
        | LSElementType.FBTailMode
        | LSElementType.FBInputMode
        | LSElementType.FBOutputMode
        | LSElementType.VertFuncMode
        | LSElementType.VertFBMode
        | LSElementType.VertFBHeaderMode
        | LSElementType.VertFBBodyMode
        | LSElementType.VertFBTailMode -> true
        | _ -> false

    /// Check if element type is a vertical function
    let isVertFunction (elementType: LSElementType) =
        isInRange elementType LSElementType.VertFunctionType_Start LSElementType.VertFunctionType_End

    /// Check if element type is a branch
    let isBranch (elementType: LSElementType) =
        match elementType with
        | LSElementType.SCALLMode
        | LSElementType.JMPMode
        | LSElementType.RetMode
        | LSElementType.SubroutineMode
        | LSElementType.BreakMode
        | LSElementType.ForMode
        | LSElementType.NextMode -> true
        | _ -> false

    /// Check if element type is a comment
    let isComment (elementType: LSElementType) =
        match elementType with
        | LSElementType.InverterMode
        | LSElementType.RungCommentMode
        | LSElementType.OutputCommentMode
        | LSElementType.LabelMode
        | LSElementType.RowCompositeMode
        | LSElementType.VariableMode -> true
        | _ -> false

    /// Convert element type to condition operator
    let toConditionOperator (elementType: LSElementType) =
        match elementType with
        | LSElementType.ContactMode -> Some ConditionOperator.Equal
        | LSElementType.ClosedContactMode -> Some ConditionOperator.Not
        | LSElementType.PulseContactMode -> Some ConditionOperator.Rising
        | LSElementType.NPulseContactMode -> Some ConditionOperator.Rising
        | LSElementType.ClosedPulseContactMode -> Some ConditionOperator.Falling
        | LSElementType.ClosedNPulseContactMode -> Some ConditionOperator.Falling
        | LSElementType.RisingContact -> Some ConditionOperator.Rising
        | LSElementType.FallingContact -> Some ConditionOperator.Falling
        | _ -> None

    /// Convert element type to action operation
    let toActionOperation (elementType: LSElementType) =
        match elementType with
        | LSElementType.CoilMode -> Some ActionOperation.Assign
        | LSElementType.ClosedCoilMode -> Some ActionOperation.Reset
        | LSElementType.SetCoilMode -> Some ActionOperation.Set
        | LSElementType.ResetCoilMode -> Some ActionOperation.Reset
        | LSElementType.PulseCoilMode -> Some ActionOperation.Toggle
        | LSElementType.NPulseCoilMode -> Some ActionOperation.Toggle
        | LSElementType.SCALLMode -> Some ActionOperation.Call
        | LSElementType.JMPMode -> Some ActionOperation.Jump
        | LSElementType.RetMode -> Some ActionOperation.Jump
        | LSElementType.SubroutineMode -> Some ActionOperation.Call
        | _ -> None
