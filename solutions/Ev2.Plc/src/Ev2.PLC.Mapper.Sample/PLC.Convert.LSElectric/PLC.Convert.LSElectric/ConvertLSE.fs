namespace PLC.Convert.LSElectric

open System
open System.IO
open System.Text.RegularExpressions
open PLC.Convert.FS.ConvertCoilModule

module ConvertLSEModule =
    type ElementType =
        | LDElementMode_Start    = 0
        | LineType_Start         = 0
        | VertLineMode           = 0 // LineType_Start '|' 
        | HorzLineMode           = 1 // '-' 
        | MultiHorzLineMode      = 2 // '-->>' 
        ///addonly hereadditional line type device. 
        | LineType_End           = 5

        | ContactType_Start      = 6
        | ContactMode            = 6 // ContactType_Start // '-| |-' 
        | ClosedContactMode      = 7 // '-|/|-' 
        | PulseContactMode       = 8 // '-|P|-' 
        | NPulseContactMode      = 9// '-|N|-' 
        | ClosedPulseContactMode = 10 // '-|P/|-' 
        | ClosedNPulseContactMode= 11// '-|N/|-' 
        ///addonly hereadditional contact type device. 
        | ContactType_End        = 13

        | CoilType_Start         = 14
        | CoilMode               = 14 // CoilType_Start // '-( )-' 
        | ClosedCoilMode         = 15 // '-(/)-' 
        | SetCoilMode            = 16 // '-(S)-' 
        | ResetCoilMode          = 17 // '-(R)-' 
        | PulseCoilMode          = 18 // '-(P)-' 
        | NPulseCoilMode         = 19 // '-(N)-' 
        ///addonly hereadditional coil type device. 
        | CoilType_End           = 30

        | FunctionType_Start     = 31
        | FuncMode               = 32
        | FBMode                 = 33 // '-[F]-' 
        | FBHeaderMode           = 34 // '-[F]-' : Header 
        | FBBodyMode             = 35 // '-[F]-' : Body 
        | FBTailMode             = 36 // '-[F]-' : Tail 
        | FBInputMode            = 37
        | FBOutputMode           = 38
        ///addonly hereadditional function type device. 
        | FunctionType_End       = 45

        | BranchType_Start       = 51
        | SCALLMode              = 52
        | JMPMode                = 53
        | RetMode                = 54
        | SubroutineMode         = 55
        | BreakMode              = 56
        | ForMode                = 57
        | NextMode               = 58
        ///addonly hereadditional branch type device. 
        | BranchType_End         = 60

        | CommentType_Start      = 61
        | InverterMode           = 62 // '-*-' 
        | RungCommentMode        = 63 // 'rung comment' 
        | OutputCommentMode      = 64 // 'output comment' 
        | LabelMode              = 65
        | EndOfPrgMode           = 66
        | RowCompositeMode       = 67 // 'row' 
        | ErrorComponentMode     = 68
        | NullType               = 69
        | VariableMode           = 70
        | CellActionMode         = 71
        | RisingContact          = 72 //add dual    xg5000 4.52
        | FallingContact         = 73 //add dual    xg5000 4.52
        ///addonly hereadditional comment type device. 
        | CommentType_End        = 90

        /// vertical function(function & function block) related 
        | VertFunctionType_Start = 100
        | VertFuncMode           = 101
        | VertFBMode             = 102
        | VertFBHeaderMode       = 103
        | VertFBBodyMode         = 104
        | VertFBTailMode         = 105
        /// add additional vertical function type device here 
        | VertFunctionType_End   = 109
        | LDElementMode_End      = 110

        | Misc_Start             = 120
        | ArrowMode              = 121
        | Misc_End               = 122


    let classifyContent (line: string) =
        let extractContent (line: string) =
            let matchResult = Regex.Match(line, @"<Element[^>]*>(.*?)</Element>")
            if matchResult.Success then Some matchResult.Groups[1].Value else None

        if line.Contains($"ElementType=\"{ElementType.CoilMode |> int}\"") || 
           line.Contains($"ElementType=\"{ElementType.VariableMode |> int}\"") 
        then
            match extractContent line with
            | Some content -> Some (Coil content)
            | None -> None

        elif line.Contains($"ElementType=\"{ElementType.ClosedContactMode |> int}\"") then
            match extractContent line with
            | Some content -> Some (ContactNega content)
            | None -> None

        elif line.Contains($"ElementType=\"{ElementType.ContactMode |> int}\"") then
            match extractContent line with
            | Some content -> Some (ContactPosi content)
            | None -> None
        else
            None
            
    let parseLSEFile (filePath: string) =
        let lines = File.ReadLines(filePath) // Stream 방식으로 메모리 절약
        let networks = ResizeArray<Network>()
        let mutable currentTitle = ""
        let mutable currentContent = ResizeArray<ContentType>()

        let titlePattern = Regex("<Program Task\s*=(.*)")
        let networkStartPattern = Regex("<Rung BlockMask")

        let addLine(line) =   
            match classifyContent line with
                | Some content -> currentContent.Add(content)
                | None -> ()

        for line in lines do
            if line.Contains "%QW3441.1"
            then 
                ()

            if networkStartPattern.IsMatch(line) then
                if currentContent.Count > 0 then
                    networks.Add({ Title = currentTitle; Content = currentContent.ToArray() })
                currentTitle <- ""
                currentContent.Clear()
                addLine(line)
            elif titlePattern.IsMatch(line) then
                let m = titlePattern.Match(line)
                currentTitle <- m.Groups.[1].Value.Trim()
            else 
                addLine(line)
        
   
        networks.ToArray()


    let parseActionOutLSEFile (filePath: string) = XmlReader.ReadTags  (filePath, false)
