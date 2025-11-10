namespace PLC.Convert.FS

open System
open System.Text
open System.IO
open System.Text.Json
open System.Text.RegularExpressions
open System.Collections.Generic
open PLC.Convert.LSCore.Expression
open FilterJsonModule

module ConvertPLCModule =

    
    ///  특정 키워드(AutoTarget/Safety) 포함 여부 확인**
    /// - 특정 키워드가 포함된 경우 해당 요소를 처리 (XXX 및 (.*) 패턴 대응)
    let isTargetOfType (keywords: string list) (input: string) =
        keywords |> List.exists (fun pattern -> Regex.IsMatch(input, pattern))

    ///  단어 치환 (XXX를 실제 값으로 변환)**
    let replaceWords (replacements: Map<string, string>) (input: string) =
        replacements 
        |> Map.fold (fun acc oldWord newWord ->
            let pattern = oldWord.Replace("XXX", "(.*)")  // `XXX`를 정규식 패턴으로 변환
            let replacement = newWord.Replace("XXX", "$1")  // 캡처 그룹 적용하여 `XXX` 치환
            Regex.Replace(acc, pattern, replacement)  // 실제 값으로 치환
        ) input

      /// **Contact 탐색 (재귀적으로 내부 Expression 검사)**
    let rec getContactNames (contactTerminals: Terminal list) (contactUnits: Terminal list) (expressionText: string) (depth: int) (maxDepth: int) (bPositive: bool) =
        if depth >= maxDepth then contactUnits  // **최대 깊이 도달 시 종료**
        else
            contactTerminals
            |> List.fold (fun acc terminal ->
                if acc |> List.exists (fun c -> c.Name = terminal.Name) then acc // **중복 방지**
                else
                    let isPositive = not (expressionText.Contains($"!{terminal.Name}"))

                    if isPositive = bPositive && not (skipKeywords |> List.exists terminal.Name.Contains) then
                        let updatedUnits = terminal :: acc  // **유효한 터미널 추가**

                        // **내부 Expression 탐색 최적화 (패턴 매칭 적용)**
                        match terminal.HasInnerExpr, terminal.InnerExpr with
                        | true, innerExpr ->
                            getContactNames (List.ofSeq (innerExpr.GetTerminals())) updatedUnits (innerExpr.ToText()) (depth + 1) maxDepth bPositive
                        | _ -> updatedUnits
                    else acc
            ) contactUnits  // **초기 값 contactUnits 설정**

    let getSymName(coil:Terminal) (bUsingComment:bool) =
        if bUsingComment then coil.Symbol.Comment else coil.Name

    let getContactNamesFromCoil(coil: Terminal) (bUsingComment:bool) =
        let expressionText = coil.InnerExpr.ToText()  
        let contacts = coil.GetTerminals() |> List.ofSeq 

        getContactNames (contacts) [] expressionText 0 3 true
        |> List.map (fun c -> getSymName c bUsingComment)
        |> List.filter (fun c -> String.IsNullOrEmpty c |> not)
