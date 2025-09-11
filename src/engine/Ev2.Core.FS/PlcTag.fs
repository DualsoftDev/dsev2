namespace Ev2.Core.FS

open Dual.Common.Base
open Dual.Common.Db.FS
open Newtonsoft.Json
open Newtonsoft.Json.Linq
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

        // JSON 직렬화 (타입 정보 포함)
        member this.Jsonize() : string =
            let jobj = JObject()
            jobj["$type"] <- JToken.FromObject(typeof<'T>.Name)
            jobj["Name"] <- if isNull this.Name then JValue.CreateNull() :> JToken else JToken.FromObject(this.Name)
            jobj["Address"] <- if isNull this.Address then JValue.CreateNull() :> JToken else JToken.FromObject(this.Address)
            jobj["Value"] <- JToken.FromObject(this.Value)
            jobj.ToString()

        // JSON 역직렬화
        static member FromJson(json: string) : PlcTag<'T> =
            let jobj = JObject.Parse(json)
            let name = if jobj["Name"].Type = JTokenType.Null then null else jobj["Name"].ToObject<string>()
            let address = if jobj["Address"].Type = JTokenType.Null then null else jobj["Address"].ToObject<string>()
            let value = jobj["Value"].ToObject<TypedValue<'T>>()
            PlcTag<'T>(name, address, value)

    type TagWithSpec<'T when 'T : equality and 'T : comparison>(name: string, address: string, value:TypedValue<'T>, valueSpec: ValueSpec<'T>) =
        let mutable tag = PlcTag<'T>(name, address, value)

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

        // Expose internal tag
        [<JsonIgnore>]
        member this.Tag
            with get() = tag
            and set(v) = tag <- v

        // ValueSpec property
        [<JsonIgnore>]
        member val ValueSpec = valueSpec with get, set

        // JSON 직렬화용 문자열 멤버
        [<JsonProperty("Tag")>]
        member val TagJson = "" with get, set

        [<JsonProperty("ValueSpec")>]
        member val ValueSpecJson = "" with get, set

        // 직렬화 전 콜백 - Tag와 ValueSpec을 JSON 문자열로 저장
        [<OnSerializing>]
        member private this.OnSerializing(context: System.Runtime.Serialization.StreamingContext) =
            this.TagJson <- tag.Jsonize()
            this.ValueSpecJson <- (this.ValueSpec :> IValueSpec).Jsonize()

        // 역직렬화 후 콜백 - JSON 문자열에서 Tag와 ValueSpec 복원
        [<OnDeserialized>]
        member private this.OnDeserialized(context: System.Runtime.Serialization.StreamingContext) =
            if not (System.String.IsNullOrEmpty(this.TagJson)) then
                tag <- PlcTag<'T>.FromJson(this.TagJson)

            if not (System.String.IsNullOrEmpty(this.ValueSpecJson)) then
                this.ValueSpec <- ValueSpec.FromJson(this.ValueSpecJson) :?> ValueSpec<'T>

        // Delegate properties from PlcTag (hide from JSON to avoid duplication)
        [<JsonIgnore>] member this.Name    with get() = tag.Name    and set(v) = tag.Name    <- v
        [<JsonIgnore>] member this.Address with get() = tag.Address and set(v) = tag.Address <- v
        [<JsonIgnore>] member this.Value   with get() = tag.Value   and set(v) = tag.Value   <- v


        override this.ToString() = $"{tag.Name} ({tag.Address}) [{this.ValueSpec}]"

    type TagWithSpec =
        static member FromJson(json: string) : ITagWithSpec =
            // JSON을 파싱해서 Tag와 ValueSpec 문자열 추출 (JsonProperty 이름 사용)
            let jObj = Newtonsoft.Json.Linq.JObject.Parse(json)

            let tagJson = jObj.["Tag"].ToString()
            let valueSpecJson = jObj.["ValueSpec"].ToString()

            // TagJson에서 타입 정보 추출
            let tagJObj = JObject.Parse(tagJson)
            let typeName = tagJObj.["$type"].ToString()

            // 타입 결정
            let genericType =
                match typeName with
                | t when t = typedefof<single>.Name -> typeof<single>
                | t when t = typedefof<double>.Name -> typeof<double>
                | t when t = typedefof<int8>  .Name -> typeof<int8>
                | t when t = typedefof<int16> .Name -> typeof<int16>
                | t when t = typedefof<int32> .Name -> typeof<int32>
                | t when t = typedefof<int64> .Name -> typeof<int64>
                | t when t = typedefof<uint8> .Name -> typeof<uint8>
                | t when t = typedefof<uint16>.Name -> typeof<uint16>
                | t when t = typedefof<uint32>.Name -> typeof<uint32>
                | t when t = typedefof<uint64>.Name -> typeof<uint64>
                | t when t = typedefof<char>  .Name -> typeof<char>
                | t when t = typedefof<bool>  .Name -> typeof<bool>
                | t when t = typedefof<string>.Name -> typeof<string>
                | t when t = typedefof<System.DateTime>.Name -> typeof<System.DateTime>
                | _ -> typeof<obj>

            // TagWithSpec 타입 생성
            let tagWithSpecType = typedefof<TagWithSpec<_>>.MakeGenericType(genericType)

            // TagWithSpec 인스턴스 생성 (기본 생성자)
            let instance = System.Activator.CreateInstance(tagWithSpecType)

            // JSON 역직렬화를 통해 인스턴스 생성 (OnDeserialized 콜백이 자동 호출됨)
            JsonConvert.DeserializeObject(json, tagWithSpecType) :?> ITagWithSpec
