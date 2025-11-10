namespace Ev2.PLC.Driver.Utils

open System.Text.RegularExpressions

[<AutoOpen>]
module RegexHelpers =

    let inline (?<-) (m: Match) (groupName: string) =
        if m.Success then Some m.Groups.[groupName].Value else None

    /// Active pattern that matches the supplied text with the given regular expression.
    let (|RegexPattern|_|) (pattern: string) (input: string) =
        let m = Regex.Match(input, pattern, RegexOptions.IgnoreCase)
        if m.Success then
            let values =
                [ for i in 1 .. m.Groups.Count - 1 -> m.Groups.[i].Value ]
            Some values
        else
            None

    // Backwards-compatible alias
    let (|RegexMatch|_|) = (|RegexPattern|_|)
