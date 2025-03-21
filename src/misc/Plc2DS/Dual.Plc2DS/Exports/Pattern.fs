namespace rec Dual.Plc2DS

open System
open System.Reflection
open System.Runtime.Serialization
open System.Text.RegularExpressions
open Newtonsoft.Json


type IPattern = interface end

[<DataContract>]
type Pattern() =
    interface IPattern
    [<DataMember>]
    member val Name          : string = "" with get, set

    [<DataMember>]
    member val PatternString : string = "" with get, set

    [<DataMember>]
    member val Description   : string = "" with get, set

    [<JsonIgnore>]
    member val RegexPattern  : Regex  = null with get, set

    member this.OnDeserialized() =
        if not (String.IsNullOrEmpty this.PatternString) then
            this.RegexPattern <- Regex(this.PatternString, RegexOptions.Compiled)

    [<OnDeserialized>]
    member this.OnDeserializedMethod(_context: StreamingContext) =
        this.OnDeserialized()

    static member Create(name: string, pattern: string, ?desc: string) =
        let rp = ReplacePattern(Name = name, PatternString = pattern, Description = defaultArg desc "")
        rp.OnDeserialized()
        rp :> Pattern


[<DataContract>]
type ReplacePattern() =
    inherit Pattern()

    [<DataMember>]
    member val Replacement : string = "" with get, set

    static member FromPattern(p: Pattern) =
        match p with
        | :? ReplacePattern as rp -> rp
        | _ -> ReplacePattern.Create(p.Name, p.PatternString, "", p.Description)

    static member Create(name: string, pattern: string, replace: string, ?desc: string) =
        let rp = ReplacePattern(Name = name, PatternString = pattern, Description = defaultArg desc "", Replacement = replace)
        rp.OnDeserialized()
        rp

    static member Create(name: string, pattern: Regex, replace: string, ?desc: string) =
        ReplacePattern(Name = name, RegexPattern = pattern, PatternString = pattern.ToString(), Description = defaultArg desc "", Replacement = replace)


[<DataContract>]
type CsvFilterPattern() =
    inherit Pattern()

    /// <summary>
    /// 패턴 매치를 적용할 PlcTagBaseFDA 의 Filed 이름
    /// </summary>
    [<DataMember>]
    member val Field : string = "" with get, set

    /// <summary>
    /// <br/> Include == true 이면
    ///     <br/> - match 되면 keep
    ///     <br/> - match 안되면 discard
    /// <br/> Include == false 이면
    ///     <br/> - match 되면 discard
    ///     <br/> - match 안되면 keep
    /// </summary>
    [<DataMember>]
    member val Include : bool = false with get, set


[<AutoOpen>]
module PatternExtension =
    type CsvFilterPattern with
        member x.IsInclude (tag: PlcTagBaseFDA) : bool option =
            // tag 객체로부터 reflection 을 이용해서 Field 이름의 값을 가져온다.
            match tag.GetType().GetProperty(x.Field) with
            | null -> None
            | propertyInfo ->
                match propertyInfo.GetValue(tag) with
                | null -> None
                | value when String.IsNullOrEmpty(value.ToString()) -> None
                | value ->
                    let matchResult = x.RegexPattern.Match(value.ToString())
                    Some (if x.Include then matchResult.Success else not matchResult.Success)

        member x.IsExclude (tag: PlcTagBaseFDA) : bool option =
            match x.IsInclude tag with
            | Some result -> Some (not result)
            | None -> None


