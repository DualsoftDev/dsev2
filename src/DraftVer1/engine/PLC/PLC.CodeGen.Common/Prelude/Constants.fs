namespace PLC.CodeGen.Common
open Dual.Common.Core.FS
open System.Collections.Generic

/// Konstants (Constants) collection

module K =
    /// "ActionType"
    [<Literal>]
    let ActionType = "ActionType"

    /// "FullName Rule"
    [<Literal>]
    let FullNameRule = "FullName Rule"

    /// "SelfHold"
    [<Literal>]
    let SelfHold = "SelfHold"

    /// "Address"
    [<Literal>]
    let Address = "Address"

    /// "Address Prefix"
    [<Literal>]
    let AddrPrefix = "Address Prefix"

    /// "Tag Type"
    [<Literal>]
    let TagType = "Tag Type"

    /// "Work Type"
    [<Literal>]
    let ProcessType = "Work Type"

    /// "Name"
    [<Literal>]
    let Name = "Name"

    /// "InstanceId"
    [<Literal>]
    let InstanceId = "Instance Id"

    /// "LibraryId"
    [<Literal>]
    let LibraryId = "Library Id"

    /// "Device count"
    [<Literal>]
    let DeviceCount = "Device Count"

    /// "Device Type"
    [<Literal>]
    let DeviceType = "Device Type"

    /// "Job Bound Type"
    [<Literal>]
    let BoundType = "Bound Type"

    /// "Job Memory Type"
    [<Literal>]
    let MemoryType = "Memory Type"

    /// "Delay"
    [<Literal>]
    let Delay = "Delay"

    /// "FBInstance"
    [<Literal>]
    let FBInstance = "FBInstance"

    /// "Size"
    [<Literal>]
    let Size = "Size"



    /// "_MANUAL"
    let _Manual = "_MANUAL"
    /// "_ADV"
    let _Advance = "_ADV"
    /// "_RET"
    let _Return = "_RET"
    /// "MANUAL"
    let Manual = "MANUAL"
    /// "ADV"
    let Advance = "ADV"
    /// "RET"
    let Return = "RET"
    let On = "ON"
    let Off = "OFF"
    let Up = "UP"
    let Down = "DOWN"



    /// "I"
    let I = "I"
    /// "Q"
    let Q = "Q"
    /// "M"
    let M = "M"

    let X = "X"
    let B = "B"
    let W = "W"
    let D = "D"
    let L = "L"


    let ErrNoAddressAssigned = "No address assigned."
    let ErrNameIsNullOrEmpty = "Name is null or empty."
    let ErrAddressIsNullOrEmpty = "Address is null or empty."

    let logicalOperators = HashSet([|"&&"; "||"; "!"|])

    /// A: "+"|"-"|"*"|"/"
    let arithmaticOperators = HashSet([|"+"; "-"; "*"; "/"|])
    /// B: "&"|"&&&"| "|"|"|||"| "^"|"^^^"|  "~"|"~~~"| ">>"|">>>"| "<<"|"<<<"
    let bitwiseOperators    = HashSet([| "&";"&&&"; "|";"|||"; "^";"^^^";  "~";"~~~"; ">>";">>>"; "<<";"<<<" |])
    /// C: ">"|">="|"<"|"<="|"=="|"!="|"<>"
    let comparisonOperators = HashSet([|">";">=";"<";"<=";"==";"!=";"<>";|])

    /// AB: ("+"|"-"|"*"|"/")  |  ("&"|"&&&"| "|"|"|||"| "^"|"^^^"|  "~"|"~~~"| ">>"|">>>"| "<<"|"<<<")
    let arithmaticOrBitwiseOperators = HashSet(arithmaticOperators @ bitwiseOperators)

    /// AC: ("+"|"-"|"*"|"/")  |  (">"|">="|"<"|"<="|"=="|"!="|"<>")
    let arithmaticOrComparisionOperators = HashSet(arithmaticOperators @ comparisonOperators)

    /// ABC: ("+"|"-"|"*"|"/")  |  ("&"|"&&&"| "|"|"|||"| "^"|"^^^"|  "~"|"~~~"| ">>"|">>>"| "<<"|"<<<")  |  (">"|">="|"<"|"<="|"=="|"!="|"<>")
    let arithmaticOrBitwiseOrComparisionOperators = HashSet(arithmaticOperators @ bitwiseOperators @ comparisonOperators)

[<AutoOpen>]
module OperatorActivePatterns =
    let private contains (op:string) (set:HashSet<string>) = if set.Contains op then Some op else None
    /// IsArithmeticOperator: "+"|"-"|"*"|"/"
    let (|IsOpA|_|) (op:string) = contains op K.arithmaticOperators
    /// IsBitwiseOperator: "&";"&&&"; "|";"|||"; "^";"^^^";  "~";"~~~"; ">>";">>>"; "<<";"<<<"
    let (|IsOpB|_|) (op:string) = contains op K.bitwiseOperators
    /// IsComparisonOperator: ">"|">="|"<"|"<="|"=="|"!="|"<>"
    let (|IsOpC|_|) (op:string) = contains op K.comparisonOperators

    /// IsArithmeticOrBitwiseOperator: ("+"|"-"|"*"|"/")  |  ("&"|"&&&"| "|"|"|||"| "^"|"^^^"|  "~"|"~~~"| ">>"|">>>"| "<<"|"<<<")
    let (|IsOpAB|_|) (op:string) = contains op K.arithmaticOrBitwiseOperators
    /// IsArithmeticOrBitwiseOperator: ("+"|"-"|"*"|"/")  |  (">"|">="|"<"|"<="|"=="|"!="|"<>")
    let (|IsOpAC|_|) (op:string) = contains op K.arithmaticOrComparisionOperators
    /// IsArithmeticOrBitwiseOrComparisonOperator: ("+"|"-"|"*"|"/")  |  ("&"|"&&&"| "|"|"|||"| "^"|"^^^"|  "~"|"~~~"| ">>"|">>>"| "<<"|"<<<")  |  (">"|">="|"<"|"<="|"=="|"!="|"<>")
    let (|IsOpABC|_|) (op:string) = contains op K.arithmaticOrBitwiseOrComparisionOperators

    /// isLogicalOperator
    let (|IsOpL|_|) (op:string) = contains op K.logicalOperators

    /// IsArithmeticOperator: "+"|"-"|"*"|"/"
    let isOpA   op = (|IsOpA|_|)   op |> Option.isSome
    /// IsBitwiseOperator: "&";"&&&"; "|";"|||"; "^";"^^^";  "~";"~~~"; ">>";">>>"; "<<";"<<<"
    let isOpB   op = (|IsOpB|_|)   op |> Option.isSome
    /// IsComparisonOperator: ">"|">="|"<"|"<="|"=="|"!="|"<>"
    let isOpC   op = (|IsOpC|_|)   op |> Option.isSome
    /// is Arithmatic or Bitwise operator
    let isOpAB  op = (|IsOpAB|_|)  op |> Option.isSome
    /// is Arithmatic or Comparison operator
    let isOpAC  op = (|IsOpAC|_|)  op |> Option.isSome
    /// is Arithmatic, Bitwise or Comparison operator
    let isOpABC op = (|IsOpABC|_|) op |> Option.isSome
    /// isLogicalOperator
    let isOpL op = (|IsOpL|_|) op |> Option.isSome
