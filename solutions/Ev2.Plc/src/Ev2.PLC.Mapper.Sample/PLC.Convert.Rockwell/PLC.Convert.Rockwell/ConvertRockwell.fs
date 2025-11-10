namespace PLC.Convert.Rockwell

open System
open System.IO
open System.Collections.Generic
open System.Text.RegularExpressions
open PLC.Convert.FS.ConvertCoilModule
open ClassTagGenerator

module ConvertRockwellModule =

    /// **Step 1: Rockwell 명령어 및 변수 추출**
    /// - 정규식을 사용하여 `XIC(...)`, `XIO(...)`, `OTE(...)` 추출
    let extractElements (line: string) =
        let pattern = @"(XIC|XIO|OTE)\(([^)]+)\)"  // 명령어 및 괄호 안 변수 추출
        let matches = Regex.Matches(line, pattern)

        [ for m in matches -> (m.Groups.[1].Value, m.Groups.[2].Value) ]  // 예: ("XIC", "NO1_SHT_S705_RS_IBI_DOWN1")

    /// **Step 2: Rockwell 명령어 분류**
    /// - `XIC` → ContactPosi (ON), `XIO` → ContactNega (OFF), `OTE` → Coil (출력)
    let classifyContent (command: string, variable: string) =
        match command with
        | "XIC" -> Some (ContactPosi variable)
        | "XIO" -> Some (ContactNega variable)
        | "OTE" -> Some (Coil variable)
        | _ -> None  // 알 수 없는 명령어 무시

    /// **Step 3: L5K 파일을 분석하여 네트워크 단위로 분리**
    let parseABFile (filePath: string) =
        let l5k = plcABConvertor.convertFile filePath  // L5K 변환기 실행

        let networks = ResizeArray<Network>()  // 네트워크 데이터를 저장할 리스트
        let mutable currentTitle = ""  // 현재 네트워크 제목
        let mutable currentContent = ResizeArray<ContentType>()  // 네트워크 내 컨텐츠

        for line in l5k.ABRoutineList do
            let parsedElements = extractElements line  

            for (cmd, var) in parsedElements do
                match classifyContent (cmd, var) with
                | Some content -> currentContent.Add(content) // ContentType 데이터 추가
                | None -> ()
            // 마지막 네트워크 추가
            if currentContent.Count > 0 then
                networks.Add({ Title = currentTitle; Content = currentContent.ToArray() })
            
            currentContent.Clear()

        networks.ToArray()
