namespace PLC.Convert.FS

open System.Text
open ConvertPLCModule
open SegmentModule
open PLC.Convert.LSCore.Expression
open FilterJsonModule
open System
open System.Collections.Generic

module MermaidExportModule =


/// - Area별 그룹화 및 Subgraph 자동 생성 (ID 변환 적용)
    let generateMermaid (targets: Map<string, string list>) (nofilter: bool) =
        let sb = StringBuilder()
        sb.AppendLine("graph TD;") |> ignore

        let mutable idCounter = 0
        let idMap = Dictionary<string, string>()

        let getId (name: string) =
            match idMap.TryGetValue(name) with
            | true, id -> id
            | false, _ ->
                idCounter <- idCounter + 1
                let newId = $"ID{idCounter}"
                idMap.Add(name, newId)
                newId

        // **Area별 그룹화**
        let groupedTargets = targets |> Seq.groupBy (fun kv -> (splitSegment kv.Key).Area)

        let mutable index = 0
        for (targetArea, nodes) in groupedTargets do
            let targetArea = if targetArea = "" 
                              then
                                    index <- index + 1
                                    $"Area_{index}"
                              else targetArea

            sb.AppendLine($"    subgraph {targetArea}") |> ignore

            for kvp in nodes do  
                let target = replaceWords targetReplacements kvp.Key |> splitSegment
                let targetId = getId target.FullName

                if isTargetOfType safetyKeywords kvp.Key then 
                    let safetyItemText =
                        kvp.Value 
                        |> List.map (fun s -> replaceWords sourceReplacements s |> splitSegment)
                        |> List.filter (fun s -> s <> target)
                        |> List.filter (fun s -> s.Area <> "" || nofilter)
                        |> List.map (fun s -> $"{getId s.FullName}[{s.FullNameSkipArea(targetArea)}]")
                        |> String.concat " & "

                    if safetyItemText <> "" then
                        sb.AppendLine($"        {targetId}[{target.FullName}].Safety{{\r\n\t\t\t{safetyItemText}\r\n\t\t}} --> {targetId};") |> ignore

                let sourcesText = 
                    kvp.Value 
                    |> List.map (fun source -> replaceWords sourceReplacements source |> splitSegment)
                    |> List.filter (fun s -> s <> target)
                    |> List.filter (fun seg -> seg.Area <> "" || nofilter)
                    |> List.map (fun seg -> $"{getId seg.FullName}[{seg.FullNameSkipArea(targetArea)}]")
                    |> String.concat " & "

                if sourcesText <> "" then
                    sb.AppendLine($"        {sourcesText} --> {targetId}[{target.DeviceApi}];") |> ignore

            sb.AppendLine("    end") |> ignore

        sb.ToString()


    /// **Rung 데이터를 Mermaid 다이어그램으로 변환하여 저장하는 함수**
 
    let Convert (coils: Terminal seq, bComment:bool) =
        let rungMap = 
            coils 
            //|> Seq.take 20
            |> Seq.filter (fun coil -> String.IsNullOrEmpty (getSymName coil bComment) |> not)  
            |> Seq.filter (fun coil -> (splitSegment (getSymName coil bComment)).Area <> "" )  
            |> Seq.filter (fun coil -> isTargetOfType (autoKeywords@safetyKeywords) (getSymName coil bComment)) 
            |> Seq.filter (fun coil -> not(isTargetOfType (skipKeywords) (getSymName coil bComment)) )
            |> Seq.map (fun coil -> (getSymName coil bComment), getContactNamesFromCoil coil bComment)  
            |> Map.ofSeq

        // ✅ **Mermaid 다이어그램 변환**
        let mermaidText = generateMermaid rungMap  false
        mermaidText    /// **Rung 데이터를 Mermaid 다이어그램으로 변환하여 저장하는 함수**


    let ConvertEdges (coils: Terminal seq, bComment:bool) =

        let rungMap = 
            coils 
            |> Seq.filter (fun coil -> String.IsNullOrEmpty (getSymName coil bComment) |> not)  
            |> Seq.filter (fun coil -> (splitSegment (getSymName coil bComment)).Area <> "" )  
            |> Seq.filter (fun coil -> not(isTargetOfType (skipKeywords) coil.Name)) 
            |> Seq.map (fun coil -> 
            
                (getSymName coil bComment)
                , getContactNamesFromCoil coil bComment)  
            |> Map.ofSeq

        // **Mermaid 다이어그램 변환**
        let mermaidText = generateMermaid rungMap  false
        mermaidText
      

      