namespace Dual.Ev2

open System
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open Dual.Common.Base.FS
open Dual.Common.Core.FS
[<AutoOpen>]
module rec ValueHolderModule =

    type ValueHolder(typ: Type, ?value: obj) =
        member val ObjectHolder = ObjHolder(typ, ?value=value) with get, set

        // 향후 전개 가능한 interface 목록.  실제 interface 의 method 는 구현하지 않고, 확장 method 를 통해 접근
        // e.g IWithName 은 확장을 통해 Name 속성의 get, set 제공
        interface IWithName
        interface IWithAddress
        interface IWithType
        interface IExpressionEv2


        /// DynamicDictionary.
        ///
        /// ObjHolder 의 부가 속성 정의 용.  e.g Name, Address, Rising, Negation 등
        member val PropertiesDto = getNull<DynamicDictionary>() with get, set
        /// PropertiesDto 접근용
        [<JsonIgnore>]
        member x.DD =
            if x.PropertiesDto = null then
                x.PropertiesDto <- DynamicDictionary()
            x.PropertiesDto

    type ValueHolder with
        new () = ValueHolder(typeof<obj>, null)
        [<JsonIgnore>] member x.ValueTypeName = x.ObjectHolder.ValueTypeName

        /// Holded value
        [<JsonIgnore>]
        member x.Value
            with get() = x.ObjectHolder.Value
            and set (v:obj) = x.ObjectHolder.Value <- v

        [<JsonIgnore>] member x.Type = x.ObjectHolder.Type


        /// ObjHolder.Name with DD
        [<JsonIgnore>]
        member x.Name
            with get() = x.DD.TryGet<string>("Name") |? null
            and set (v:string) = x.DD.Set<string>("Name", v)

        [<JsonIgnore>]
        member x.Address
            with get() = x.DD.TryGet<string>("Address") |? null
            and set (v:string) = x.DD.Set<string>("Address", v)

        [<JsonIgnore>]
        member x.Comment
            with get() = x.DD.TryGet<string>("Comment") |? null
            and set (v:string) = x.DD.Set<string>("Comment", v)

        [<JsonIgnore>]
        member x.IsLiteral
            with get() = x.DD.TryGet<bool>("IsLiteral") |? false
            and set (v:bool) = x.DD.Set<bool>("IsLiteral", v)