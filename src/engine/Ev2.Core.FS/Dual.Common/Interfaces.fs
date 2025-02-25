namespace Dual.Ev2

open System
open Dual.Common.Base.FS
open Dual.Common.Core.FS

[<AutoOpen>]
module InterfacesModule =
    type IWithType = interface end

    type IWithType<'T> =
        inherit IWithType

    type IWithValue = interface end
    type IWithValue<'T> =
        inherit IWithValue

    type IExpression =
        inherit IWithType
        inherit IWithValue
        inherit IWithName
        abstract member Evaluate: unit -> obj

    type IExpression<'T> =
        inherit IExpression
        inherit IWithType<'T>
        inherit IWithValue<'T>
        abstract member TEvaluate: unit -> 'T
[<AutoOpen>]
module ReflectionInterfacesModule =
    type IWithType with
        /// IWithType.Type (no setter)
        member x.Type = getPropertyValueDynamically(x, "Type") :?> Type
        member x.DataType = x.Type  // 임시

    type IWithValue with
        /// IWithType.Type (no setter)
        member x.Value = getPropertyValueDynamically(x, "Value")
    type IWithValue<'T> with
        /// IWithType.Type (no setter)
        member x.TValue = getPropertyValueDynamically(x, "Value") :?> 'T

    type IWithAddress with
        /// IWithAddress.Address
        member x.Address
            with get() = tryGetStringPropertyDynamically(x, "Address") |? null
            and set (v:string) = setPropertyValueDynamically(x, "Address", v)

    type IWithComment with
        /// IWithComment.Comment
        member x.Comment
            with get() = tryGetStringPropertyDynamically(x, "Comment") |? null
            and set (v:string) = setPropertyValueDynamically(x, "Comment", v)

