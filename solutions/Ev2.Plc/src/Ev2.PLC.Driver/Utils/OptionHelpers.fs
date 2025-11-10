namespace Ev2.PLC.Driver.Utils

module OptionHelpers =

    let ofBool (value: bool) = if value then Some () else None

    let ofBoolWith value result = if value then Some result else None

