namespace Ev2.Core.FS

open Dual.Common.Base
open Dual.Common.Db.FS
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open System.Runtime.Serialization
open Dual.Common.Core.FS

/// JSON 문자열을 이스케이프 없이 그대로 쓰기 위한 JsonConverter
type RawJsonConverter() =
    inherit JsonConverter()

    override x.CanConvert(objectType) =
        objectType = typeof<string>

    override x.WriteJson(writer, value, serializer) =
        match value with
        | null -> writer.WriteNull()
        | :? string as json when not (System.String.IsNullOrWhiteSpace(json)) ->
            // JSON 문자열을 이스케이프 없이 그대로 출력
            writer.WriteRawValue(json)
        | _ -> writer.WriteNull()

    override x.ReadJson(reader, objectType, existingValue, serializer) =
        match reader.TokenType with
        | JsonToken.String ->
            // 구형식: 이스케이프된 문자열
            reader.Value
        | JsonToken.StartObject | JsonToken.StartArray ->
            // 신형식: 객체나 배열을 문자열로 변환
            let jToken = JToken.Load(reader)
            jToken.ToString(Formatting.None) :> obj
        | JsonToken.Null ->
            null
        | _ ->
            null

    override x.CanRead = true
    override x.CanWrite = true

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
        abstract member Jsonize: unit -> string

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
            jobj.ToString(Formatting.None)

        // JSON 역직렬화
        static member FromJson(json: string) : PlcTag<'T> =
            let jobj = JObject.Parse(json)
            let name = if jobj["Name"].Type = JTokenType.Null then null else jobj["Name"].ToObject<string>()
            let address = if jobj["Address"].Type = JTokenType.Null then null else jobj["Address"].ToObject<string>()
            let value = jobj["Value"].ToObject<TypedValue<'T>>()
            PlcTag<'T>(name, address, value)

    type TagWithSpec<'T when 'T : equality and 'T : comparison>(name: string, address: string, value:TypedValue<'T>, valueSpec: ValueSpec<'T>) =
        interface IWithName
        interface IWithAddress

        interface ITagWithSpec with
            member x.Tag       with get() = box x.Tag
            member x.ValueSpec with get() = valueSpec :> IValueSpec
            member x.Name      with get() = x.Tag.Name            and set(v) = x.Tag.Name        <- v
            member x.Address   with get() = x.Tag.Address         and set(v) = x.Tag.Address     <- v
            member x.Value     with get() = box x.Tag.Value.Value and set(v) = x.Tag.Value.Value <- unbox v
            member x.ValueType with get() = typeof<'T>
            member x.Jsonize() = JsonConvert.SerializeObject(x)

        new(name, address, valueSpec) = TagWithSpec<'T>(name, address, TypedValue<'T>(typeof<'T>), valueSpec)
        new() = TagWithSpec<'T>(null, null, TypedValue<'T>(typeof<'T>), ValueSpec.Undefined)

        // Expose internal tag
        [<JsonIgnore>]
        member val Tag = PlcTag<'T>(name, address, value) with get, set

        // ValueSpec property
        [<JsonIgnore>]
        member val ValueSpec = valueSpec with get, set

        // JSON 직렬화용 문자열 멤버 (RawJsonConverter로 이스케이프 방지)
        [<JsonProperty("Tag"); JsonConverter(typeof<RawJsonConverter>)>]
        member val TagJson = "" with get, set

        [<JsonProperty("ValueSpec"); JsonConverter(typeof<RawJsonConverter>)>]
        member val ValueSpecJson = "" with get, set

        // 직렬화 전 콜백 - Tag와 ValueSpec을 JSON 문자열로 저장
        [<OnSerializing>]
        member private this.OnSerializing(context: System.Runtime.Serialization.StreamingContext) =
            this.TagJson <- this.Tag.Jsonize()
            this.ValueSpecJson <- (this.ValueSpec :> IValueSpec).Jsonize()

        // 역직렬화 후 콜백 - JSON 문자열에서 Tag와 ValueSpec 복원
        [<OnDeserialized>]
        member private this.OnDeserialized(context: System.Runtime.Serialization.StreamingContext) =
            if not (System.String.IsNullOrEmpty(this.TagJson)) then
                this.Tag <- PlcTag<'T>.FromJson(this.TagJson)

            if not (System.String.IsNullOrEmpty(this.ValueSpecJson)) then
                this.ValueSpec <- ValueSpec.FromJson(this.ValueSpecJson) :?> ValueSpec<'T>

        // Delegate properties from PlcTag (hide from JSON to avoid duplication)
        [<JsonIgnore>] member x.Name    with get() = x.Tag.Name    and set(v) = x.Tag.Name    <- v
        [<JsonIgnore>] member x.Address with get() = x.Tag.Address and set(v) = x.Tag.Address <- v
        [<JsonIgnore>] member x.Value   with get() = x.Tag.Value   and set(v) = x.Tag.Value   <- v


        override x.ToString() = $"{x.Tag.Name} ({x.Tag.Address}) [{x.ValueSpec}]"

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
            let genericType = tryGetTypeFromSimpleName typeName |? typeof<obj>

            // TagWithSpec 타입 생성
            let tagWithSpecType = typedefof<TagWithSpec<_>>.MakeGenericType(genericType)

            // TagWithSpec 인스턴스 생성 (기본 생성자)
            let instance = System.Activator.CreateInstance(tagWithSpecType)

            // JSON 역직렬화를 통해 인스턴스 생성 (OnDeserialized 콜백이 자동 호출됨)
            JsonConvert.DeserializeObject(json, tagWithSpecType) :?> ITagWithSpec

    type IOTagsWithSpec(inTag:ITagWithSpec, outTag:ITagWithSpec) =
        new() = IOTagsWithSpec(null, null)

        [<JsonIgnore>] member val InTag  = inTag  with get, set
        [<JsonIgnore>] member val OutTag = outTag with get, set
        [<JsonProperty("InTag"); JsonConverter(typeof<RawJsonConverter>)>]  member val InTagJson  = "" with get, set
        [<JsonProperty("OutTag"); JsonConverter(typeof<RawJsonConverter>)>] member val OutTagJson = "" with get, set

        [<OnSerializing>]
        member private this.OnSerializing(context: StreamingContext) =
            match box this.InTag with
            | null -> ()
            | _ -> this.InTagJson <- this.InTag.Jsonize()
            match box this.OutTag with
            | null -> ()
            | _ -> this.OutTagJson <- this.OutTag.Jsonize()
        [<OnDeserialized>]
        member private this.OnDeserialized(context: StreamingContext) =
            if not (System.String.IsNullOrEmpty(this.InTagJson)) then
                this.InTag  <- TagWithSpec.FromJson(this.InTagJson)
            if not (System.String.IsNullOrEmpty(this.OutTagJson)) then
                this.OutTag <- TagWithSpec.FromJson(this.OutTagJson)

        /// 논리적으로 빈 상태인지 확인 (InTag, OutTag 모두 null)
        member x.IsLogicallyEmpty() = isItNull x || (isItNull x.InTag && isItNull x.OutTag)
        /// Null safe JSON 직렬화 (논리적으로 빈 경우 null 반환)
        member x.Jsonize(): string = if x.IsLogicallyEmpty() then null else EmJson.ToJson x
        static member FromJson(json:string) = if json.IsNullOrEmpty() then getNull<IOTagsWithSpec>() else EmJson.FromJson<IOTagsWithSpec>(json)
