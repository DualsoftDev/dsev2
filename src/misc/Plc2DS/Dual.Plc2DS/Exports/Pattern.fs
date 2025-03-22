namespace rec Dual.Plc2DS

open System
open System.Reflection
open System.Runtime.Serialization
open System.Text.RegularExpressions
open Newtonsoft.Json
open Dual.Common.Core.FS


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
        let p = ReplacePattern(Name = name, PatternString = pattern, Description = defaultArg desc "", Replacement = replace)
        p.OnDeserialized()
        p

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
    static member Create(name: string, field:string, pattern: string, ?desc: string) =
        let p = CsvFilterPattern(Name = name, Field = field, PatternString = pattern, Description = defaultArg desc "")
        p.OnDeserialized()
        p
    member x.Duplicate() =
        let p = CsvFilterPattern.Create(x.Name, x.Field, x.PatternString, x.Description)
        p.RegexPattern <- x.RegexPattern
        p

type CsvFilterExpression =
    | Unit of CsvFilterPattern
    | And  of CsvFilterExpression[]
    | Or   of CsvFilterExpression[]
    | Not  of CsvFilterExpression


type CsvFilterExpression with
    member x.TryMatch (tag: PlcTagBaseFDA) : bool option =
        // And 인 경우, 모든 패턴이 Some true 이면 Some true 반환.  모든 패턴이 Some false 이면 Some false 반환.  하나라도 None 이면 None 반환.
        // Or 인 경우, 하나라도 Some true 이면 Some true 반환.  모든 패턴이 Some false 이면 Some false 반환.  하나라도 None 이면 None 반환.
        // Not 인 경우, 패턴이 Some true 이면 Some false 반환.  패턴이 Some false 이면 Some true 반환.  패턴이 None 이면 None 반환.
        let allMatch (exprs:CsvFilterExpression[]) =
            let results = exprs |> Array.map (fun e -> e.TryMatch(tag))
            if results |> Array.contains None then None
            elif results |> Array.forall ((=) (Some true)) then Some true
            elif results |> Array.forall ((=) (Some false)) then Some false
            else Some false

        let anyMatch (exprs:CsvFilterExpression[]) =
            let results = exprs |> Array.map (fun e -> e.TryMatch(tag))

            if   results |> Array.exists ((=) (Some true)) then Some true
            elif results |> Array.forall ((=) (Some false)) then Some false
            elif results |> Array.contains None then None
            else Some false

        match x with
        | Unit p  -> p.TryMatch(tag)
        | And  ps -> allMatch ps
        | Or   ps -> anyMatch ps
        | Not  p  ->
            match p.TryMatch(tag) with
            | Some b -> Some (not b)
            | None   -> None

    member x.Duplicate() =
        match x with
        | Unit p -> Unit(p.Duplicate())
        | And ps -> And (ps |> Array.map (fun p -> p.Duplicate()))
        | Or  ps -> Or  (ps |> Array.map (fun p -> p.Duplicate()))
        | Not p  -> Not (p.Duplicate())

    member x.Merge(other:CsvFilterExpression) =
        if isItNull other then
            x
        else
            And([|x; other|])


[<AutoOpen>]
module PatternExtension =
    type CsvFilterPattern with

        member x.TryMatch (tag: PlcTagBaseFDA) : bool option =

            // tag 객체로부터 reflection 을 이용해서 Field 이름의 값을 가져온다.
            let propInfo:PropertyInfo = tag.GetType().GetProperty(x.Field)
            match propInfo with
            | null -> None
            | _ ->
                match propInfo.GetValue(tag) with
                | null -> None
                | value ->
                    Some <| x.RegexPattern.IsMatch(value.ToString())

