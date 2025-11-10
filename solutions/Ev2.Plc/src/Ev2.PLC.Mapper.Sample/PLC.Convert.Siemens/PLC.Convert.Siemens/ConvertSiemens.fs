namespace PLC.Convert.Siemens

open System
open System.IO
open System.Text.RegularExpressions
open PLC.Convert.FS.ConvertCoilModule

module ConvertSiemensModule =
  

    let classifyContent (line: string) =
        if line.StartsWith("      =") || line.StartsWith("      CALL") then
            Coil (line.Split('"')[1])
        elif line.StartsWith("      ON") || line.StartsWith("      AN") then
            ContactNega (line.Split('"')[1])
        elif line.StartsWith("      A") || line.StartsWith("      O") then
            ContactPosi (line.Split('"')[1])
        else
            Other (line.Split('"')[1])

    let parseSiemensFile (filePath: string) =
        let lines = File.ReadLines(filePath) // Stream 방식으로 메모리 절약
        let networks = ResizeArray<Network>()
        let mutable currentTitle = ""
        let mutable currentContent = ResizeArray<ContentType>()

        let titlePattern = Regex("^TITLE\s*=(.*)")
        let networkStartPattern = Regex("^NETWORK")

        for line in lines do
            if networkStartPattern.IsMatch(line) then
                if currentContent.Count > 0 then
                    networks.Add({ Title = currentTitle; Content = currentContent.ToArray() })
                currentTitle <- ""
                currentContent.Clear()
            elif titlePattern.IsMatch(line) then
                let m = titlePattern.Match(line)
                currentTitle <- m.Groups.[1].Value.Trim()
            elif line.EndsWith("\"; ")  then
                currentContent.Add(classifyContent line)
        
        if currentContent.Count > 0 then
            networks.Add({ Title = currentTitle; Content = currentContent.ToArray() })

        networks.ToArray()
