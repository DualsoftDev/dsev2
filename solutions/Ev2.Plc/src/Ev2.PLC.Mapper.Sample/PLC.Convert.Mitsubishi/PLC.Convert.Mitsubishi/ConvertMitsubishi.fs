namespace PLC.Convert.MX

open System
open System.Collections.Generic
open System.Text.RegularExpressions
open PLC.Convert.FS.ConvertCoilModule

module ConvertMitsubishiModule =
  
    let classifyContent (programCSVLine: ProgramCSVLine) (comments: Dictionary<string, string>) =
        match programCSVLine.Arguments with
        | [| arg |] when arg.IsMemory() ->
            let argumentText = arg.ToText()
            let getComment () = if comments.ContainsKey(argumentText) 
                                then comments.[argumentText].Replace(" ", "_")   
                                else argumentText
            
            match programCSVLine.Instruction with
            | il when il.StartsWith("OUT")  -> Some (Coil (getComment ()))
            | il when il.StartsWith("ANI") || il.StartsWith("ORI") || il.StartsWith("LDI") -> Some (ContactNega (getComment ()))
            | il when il.StartsWith("AND") || il.StartsWith("OR") || il.StartsWith("LD") -> Some (ContactPosi (getComment ()))
            | _ -> None
        | _ -> None

    let parseMXFile (files: string[]) =
        let (pous, comments, _) = CSVParser.parseCSVs(files)
        let rungs = pous |> Array.collect (fun pou -> pou.Rungs)

        let networks =
            rungs
            |> Array.fold (fun acc rung ->
                let content =
                    rung
                    |> Array.choose (fun line -> classifyContent line comments)

                if content.Length > 0 then
                    { Title = ""; Content = content } :: acc
                else acc
            ) []

        networks |> List.toArray
