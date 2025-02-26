namespace Dual.Ev2

open System
open Dual.Common.Base.FS
open Dual.Common.Core.FS

[<AutoOpen>]
module InterfacesModule =
    type IWithType = interface end

    type IWithType<'T> =
        inherit IWithType

    /// OValue (Object/Opaque type value) 속성을 갖는 최상위 interface.  TValue<_> 및 TFunction<_> 포함
    type IValue =
        /// IValue.OValue : interface 선언
        abstract member OValue: obj with get, set

    /// TValue<_> ('T Typed value) 속성을 갖는 최상위 interface.  TValue<_> 및 TFunction<_> 포함
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

    type IStorage<'T> =
        inherit IExpression<'T>

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
        /// IValue.OValue
        member x.OValue
            with get() = getPropertyValueDynamically(x, "OValue")
            and set (v:obj) = setPropertyValueDynamically(x, "OValue", v)

    type IValue<'T> with
        /// IValue<'T>.TValue (no setter)
        member x.TValue = getPropertyValueDynamically(x, "TValue") :?> 'T

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

