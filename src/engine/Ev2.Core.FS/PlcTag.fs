namespace Ev2.Core.FS

open Dual.Common.Base
open Dual.Common.Db.FS
open Newtonsoft.Json
open System.Runtime.Serialization

[<AutoOpen>]
module PlcTagModule =

    // Non-generic interface for TagWithSpec
    type ITagWithSpec =
        abstract member Tag: obj with get  // Returns PlcTag as obj
        abstract member ValueSpec: IValueSpec with get
        abstract member Name: string with get, set
        abstract member Address: string with get, set
        abstract member Value: obj with get, set  // Returns/sets TypedValue.Value as obj
        abstract member ValueType: System.Type with get  // The actual type of the value

    type NamedAddress(name: string, address: string) =
        interface IWithName
        interface IWithAddress
        member val Name = name with get, set
        member val Address = address with get, set
        override this.ToString() = $"{name} ({address})"

    type PlcTag<'T>(name: string, address: string, value:TypedValue<'T>) =
        inherit NamedAddress(name, address)
        new(name, address) = PlcTag<'T>(name, address, TypedValue<'T>(typeof<'T>))
        new() = PlcTag<'T>(null, null)
        member val Value = value with get, set

    type TagWithSpec<'T when 'T : equality and 'T : comparison>(name: string, address: string, value:TypedValue<'T>, valueSpec: ValueSpec<'T>) =
        let tag = PlcTag<'T>(name, address, value)

        interface IWithName
        interface IWithAddress

        interface ITagWithSpec with
            member this.Tag       with get() = box tag
            member this.ValueSpec with get() = valueSpec :> IValueSpec
            member this.Name      with get() = tag.Name            and set(v) = tag.Name        <- v
            member this.Address   with get() = tag.Address         and set(v) = tag.Address     <- v
            member this.Value     with get() = box tag.Value.Value and set(v) = tag.Value.Value <- unbox v
            member this.ValueType with get() = typeof<'T>

        new(name, address, valueSpec) = TagWithSpec<'T>(name, address, TypedValue<'T>(typeof<'T>), valueSpec)
        new() = TagWithSpec<'T>(null, null, TypedValue<'T>(typeof<'T>), ValueSpec.Undefined)

        // Expose internal tag for JSON serialization (내부 tag 를 사용하므로 setter 불필요)
        member val Tag = tag with get

        // ValueSpec property (외부 valueSpec 을 사용하므로 setter 필요)
        member val ValueSpec = valueSpec with get, set

        // Delegate properties from PlcTag (hide from JSON to avoid duplication)
        [<JsonIgnore>] member this.Name    with get() = tag.Name    and set(v) = tag.Name    <- v
        [<JsonIgnore>] member this.Address with get() = tag.Address and set(v) = tag.Address <- v
        [<JsonIgnore>] member this.Value   with get() = tag.Value   and set(v) = tag.Value   <- v


        override this.ToString() = $"{tag.Name} ({tag.Address}) [{this.ValueSpec}]"