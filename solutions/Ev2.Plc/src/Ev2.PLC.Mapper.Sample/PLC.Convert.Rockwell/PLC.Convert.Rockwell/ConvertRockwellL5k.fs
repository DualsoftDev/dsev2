module ClassTagGenerator

open System
open System.IO
 
// plcABTAG 모듈
module plcABTAG =
    
    type TAGData = {
        GlobalTags: string list
        LocalTags: string list
        BaseTags: string list
        CommentTags: string list
        AliasTags: string list
    }
    
    let initTAGData (globalTags, localTags) = {
        GlobalTags = globalTags
        LocalTags = localTags
        BaseTags = []
        CommentTags = []
        AliasTags = []
    }
    
    let analyzeGlobalTags data =
        let mutable baseTags = []
        let mutable aliasTags = []
        let mutable commentTags = []
        
        data.GlobalTags |> List.iter (fun tag ->
            if tag.Contains(" : ") then baseTags <- tag :: baseTags
            elif tag.Contains(" OF ") then aliasTags <- tag :: aliasTags
            elif tag.Contains("COMMENT") then commentTags <- tag :: commentTags
        )
        { data with BaseTags = baseTags; AliasTags = aliasTags; CommentTags = commentTags }
    
    let GlobalTAGAnalysis(globalTags, localTags) =
        initTAGData(globalTags, localTags) |> analyzeGlobalTags

        
// plcABConvertor 모듈
module plcABConvertor =
    
    type ConvertorData = {
        BaseConver: string list
        AliasConver: string list
        CommentConver: string list
        CommentCheck: string list
        Temp: string list
    }
    
    let initConvertorData () = {
        BaseConver = []
        AliasConver = []
        CommentConver = []
        CommentCheck = []
        Temp = []
    }
    

    let tagConvertor (baseFileLines:ResizeArray<string>) =
        let lines = baseFileLines.ToArray() |> Array.toList 
        lines |> List.choose (fun (line:string) ->
            match line with
            | s when s.Contains(" OF ") ->
                 let aliasParts = s.Split([|" OF "|], StringSplitOptions.None)
                 if aliasParts.Length > 1 then Some(aliasParts.[0].Replace("\t", "")) else None 

            | s when s.Contains("Base:Global:") ->
                let tags = s.Split '('
                let tag = tags.[0].Replace(" ", "")
                let cleanTag = if tag.Contains(":=") then tag.Split([|":="|], StringSplitOptions.None).[0] else tag
                cleanTag.Replace("Base:Global:", "") |> Some

            | _ -> line |> Some
        )

    let routineConvertor (baseFileLines:ResizeArray<string>) =
        let lines = baseFileLines.ToArray() |> Array.toList 
        lines |> List.choose (fun (line:string) ->
            match line with
            | s when s.TrimStart('\t').StartsWith("N:") -> 
                let code = s.TrimStart('\t').TrimStart("N: ".ToCharArray())
                code |> Some
            | _ -> None
        )


        
    
    type SortingData = {
        ABInfo: string list
        ABModule: string list
        ABModuleList: string list
        ABProgramList: string list
        ABRoutineList: string list
        ABGlobalTAG: string list
        ABLocalTAG: string list
        Base: string list
        Alias: string list
        Comment: string list
    }
    
    let initSortingData () = {
        ABInfo = []
        ABModule = []
        ABModuleList = []
        ABProgramList = []
        ABRoutineList = []
        ABGlobalTAG = []
        ABLocalTAG = []
        Base = []
        Alias = []
        Comment = []
    }
    
    
    let convertFile filePath =
        let fileLines = File.ReadLines(filePath) |> Seq.toList
        
        let mutable data = initSortingData()
        let mutable bOKInfo = true
        let mutable bOKModule = false
        let mutable bOKProgram = false
        let mutable bOKDataType = false
        let tempLine = ResizeArray<string>()
        fileLines |> List.iter (fun line ->
            match line with
            | s when s.TrimStart('\t').StartsWith("MODULE") ->
                bOKInfo <- false
                bOKModule <- true
                let parts = s.Split ' '
                data <- { data with ABModuleList = parts.[1] :: data.ABModuleList }
            | "\tEND_MODULE" -> bOKModule <- false

            | "\tTAG" -> ()
            | "\tEND_TAG" -> 
                (tagConvertor (tempLine)) |> List.iter (fun s -> data <- { data with ABGlobalTAG = s :: data.ABGlobalTAG })
                tempLine.Clear()

            | s when s.TrimStart('\t').StartsWith("PROGRAM") ->
                bOKProgram <- true
                data <- { data with ABProgramList = s :: data.ABProgramList }
            | s when s.TrimStart('\t').StartsWith("END_PROGRAM") -> bOKProgram <- false

            | s when s.TrimStart('\t').StartsWith("ROUTINE") -> ()
            | s when s.TrimStart('\t').StartsWith("END_ROUTINE") ->
                (routineConvertor (tempLine)) |> List.iter (fun s -> data <- { data with ABRoutineList = s :: data.ABRoutineList })
                tempLine.Clear()

            | s when s.TrimStart('\t').StartsWith("DATATYPE") ->
                bOKDataType <- true
                bOKInfo <- false
            | s when s.TrimStart('\t').StartsWith("END_DATATYPE") -> bOKDataType <- false
            | _ -> 
                    tempLine.Add(line)          
                    )
    
        let tagAnalyzer = plcABTAG.GlobalTAGAnalysis(data.ABGlobalTAG, data.ABLocalTAG)
        { data with Base = tagAnalyzer.BaseTags; Alias = tagAnalyzer.AliasTags; Comment = tagAnalyzer.CommentTags }
    