namespace Dual.Ev2

open System
open Dual.Common.Base.FS
open Dual.Common.Core.FS

[<AutoOpen>]
module InterfacesModule =
    type IWithType = interface end

    type IWithType<'T> =
        inherit IWithType

    type IValue =
        abstract member Value: obj with get, set

    type IValue<'T> =
        inherit IValue
        abstract member TValue: 'T with get, set

    type IExpression =
        inherit IWithType
        inherit IValue
        inherit IWithName

    type IExpression<'T> =
        inherit IExpression
        inherit IWithType<'T>
        inherit IValue<'T>

    type IStorage =
        inherit IExpression
    //type ISystem  = interface end


[<AutoOpen>]
module ReflectionInterfacesModule =
    type IWithType with
        /// IWithType.Type (no setter)
        member x.Type = getPropertyValueDynamically(x, "Type") :?> Type
        member x.DataType = x.Type  // 임시

    type IWithName with
        /// IWithType.Name
        member x.Name
            with get() = getPropertyValueDynamically(x, "Name") :?> string
            and set (v:string) = setPropertyValueDynamically(x, "Name", v)

    type IValue with
        /// IWithType.Type
        member x.Value
            with get() = getPropertyValueDynamically(x, "Value")
            and set (v:obj) = setPropertyValueDynamically(x, "Value", v)

    type IValue<'T> with
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

