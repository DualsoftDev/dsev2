namespace Ev2.Core.FS

open Dual.Common.Base
open Dual.Common.Db.FS
open Newtonsoft.Json
open System.Runtime.Serialization

[<AutoOpen>]
module PlcTagModule =

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
        inherit PlcTag<'T>(name, address, value)
        new(name, address, valueSpec) = TagWithSpec<'T>(name, address, TypedValue<'T>(typeof<'T>), valueSpec)
        new() = TagWithSpec<'T>(null, null, ValueSpec.Undefined)
        member val ValueSpec = valueSpec with get, set