namespace Dual.Plc2DS


open System
open System.Linq
open System.Runtime.CompilerServices
open System.Collections.Generic
open System.Text.RegularExpressions

open Dual.Common.Core.FS


[<AutoOpen>]
module private PatternExtImpl =
    let collectMatchedTags (tags: PlcTagBaseFDA[]) (patterns: Pattern[]) : PlcTagBaseFDA[] =
        let gr = tags.GroupByToDictionary(fun t -> patterns.Any(fun p -> p.RegexPattern.IsMatch(t.GetName())))
        if gr.ContainsKey(true) then gr[true] else [||]


    let verify (tag: PlcTagBaseFDA) (category: string) (x: string) =
        if String.IsNullOrEmpty(x) then
            logError $"Empty {category} on Tag {tag.Stringify()}"
        x

    let getFdatGetter (fdat: FDAT) =
        match fdat with
        | DuFlow -> fun (t: PlcTagBaseFDA) -> t.FlowName |> verify t "FlowName"
        | DuDevice -> fun t -> t.DeviceName |> verify t "DeviceName"
        | DuAction -> fun t -> t.ActionName |> verify t "ActionName"
        | DuTag -> fun t -> t.GetName() |> verify t "Name"
        | _ -> raise (NotImplementedException())

    let getFdatSetter (fdat: FDAT) : PlcTagBaseFDA -> string -> unit  =
        match fdat with
        | DuFlow -> fun (t: PlcTagBaseFDA) (v: string) -> t.FlowName <- v
        | DuDevice -> fun t v -> t.DeviceName <- v
        | DuAction -> fun t v -> t.ActionName <- v
        | DuTag -> fun t v -> t.SetName(v)
        | _ -> raise (NotImplementedException())

    let collectCandidates (replacePattern: ReplacePattern) (tags: PlcTagBaseFDA[]) (fdat:FDAT) : PlcTagBaseFDA[] =
        [|
            let fdatGetter = getFdatGetter fdat
            for t in tags do
                let fda = fdatGetter t
                let m = replacePattern.RegexPattern.Match(fda)
                if m.Success then
                    yield t
        |]

    let getPatternApplication (tag: PlcTagBaseFDA) (replacePatterns: ReplacePattern[]) (fdat:FDAT) : string =
        let fdatGetter = getFdatGetter fdat
        let mutable fda = fdatGetter(tag)
        for p in replacePatterns do
            while p.RegexPattern.IsMatch(fda) do
                fda <- p.RegexPattern.Replace(fda, p.Replacement)
        fda


    let categorizeFDA (patterns: Pattern[]) (tags: PlcTagBaseFDA[]) : PlcTagBaseFDA[] =
        let doneSet = HashSet<PlcTagBaseFDA>()

        let collectCategorized (pattern: Pattern) : seq<PlcTagBaseFDA> =
            seq {
                for t in tags do
                    if not (doneSet.Contains(t)) then
                        let m: Match = pattern.RegexPattern.Match(t.CsGetName())
                        if m.Success then
                            t.FlowName <- m.Groups.["flow"].Value
                            t.DeviceName <- m.Groups.["device"].Value
                            t.ActionName <- m.Groups.["action"].Value
                            yield t
            }

        for p in patterns do
            let matches = collectCategorized p
            for t in matches do
                doneSet.Add(t) |> ignore

        doneSet.ToArray()


type PatternExt =
    [<Extension>] static member FindMatches(patterns: Pattern[], tags: PlcTagBaseFDA[]) = collectMatchedTags tags patterns
    [<Extension>] static member FindMatches(pattern: Pattern, tags: PlcTagBaseFDA[]) = collectMatchedTags tags [|pattern|]

    [<Extension>] static member GetPatternApplication(tag:PlcTagBaseFDA, replacePatterns:ReplacePattern[], fdat:FDAT) = getPatternApplication tag replacePatterns fdat
    [<Extension>]
    static member Apply(patterns: ReplacePattern[], tags:PlcTagBaseFDA[], fdat:FDAT) =
        let setter = getFdatSetter fdat
        let candidates =
            patterns
            |> bind (fun p -> collectCandidates p tags fdat)
            |> distinct
        for c in candidates do
            setter c (getPatternApplication c patterns fdat)
        candidates

    [<Extension>] static member CollectCandidates(patterns: ReplacePattern[], tags: PlcTagBaseFDA[], fdat:FDAT) =
                    patterns |> bind (fun p -> collectCandidates p tags fdat) |> distinct
    [<Extension>] static member CollectCandidates(pattern: ReplacePattern, tags: PlcTagBaseFDA[], fdat:FDAT) =
                    collectCandidates pattern tags fdat |> distinct

    [<Extension>] static member Categorize(patterns: Pattern[], tags:PlcTagBaseFDA[]) = categorizeFDA patterns tags
    [<Extension>] static member Categorize(pattern: Pattern, tags:PlcTagBaseFDA[]) = categorizeFDA [|pattern|] tags


