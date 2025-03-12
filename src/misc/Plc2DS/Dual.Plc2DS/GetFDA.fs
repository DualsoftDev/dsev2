namespace Dual.Plc2DS

open System.Text.RegularExpressions

open Dual.Plc2DS
open Dual.Common.Core.FS

[<AutoOpen>]
module GetFDA =

    type IPlcTag with
        member x.TryGetFDA(semantic:Semantic): (string*string*string) option =
            let sm = semantic
            let fs = sm.Flows   |> toArray
            let ds = sm.Devices |> toArray
            let zs = sm.Actions |> toArray
            let name = x.GetName()
            let rs:MatchSet[] = StringSearch.MatchRawFDA(name, fs, ds, zs)

            let useRegex() =
                let tailNumber = Regex.Match(name, "_\d+$")
                let nname, ntail =
                    if tailNumber.Success then
                        let tail = tailNumber.Value
                        let head = name.Substring(0, name.Length - tail.Length)
                        head, tail
                    else
                        name, ""


                StringSearch.MatchRegexFDA(nname)
                |> map _.Text
                |> fun xs ->
                    if xs.Length = 3 then
                        Some (xs.[0], xs.[1], (xs.[2] + ntail))
                    else
                        None
            if rs.any() && rs[0].Matches.Length = 3 then
                let unmatched = PartialMatch.ComputeUnmatched(name, rs[0].Matches, sm.NameSeparators |> toArray, sm.Discards |> toArray)
                if unmatched.Length > 0 then
                    useRegex()
                else
                    let ms = rs[0].Matches
                    Some(ms[0].Text, ms[1].Text, ms[2].Text)
            else
                useRegex()
