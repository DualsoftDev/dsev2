namespace Ev2.Cpu.Generation.Codegen

open System
open System.Text
open Ev2.Cpu.Core
open Ev2.Cpu.Core.Expression
open Ev2.Cpu.Core.Statement
open Ev2.Cpu.Generation.Core
open Ev2.Cpu.Generation.Make.UserFBGen

/// PLC 코드 생성기 (IEC 61131-3 Structured Text)
module PLCCodeGen =

    // ═════════════════════════════════════════════════════════════════════
    // 데이터 타입 변환 (F# -> PLC ST)
    // ═════════════════════════════════════════════════════════════════════

    let dataTypeToPLC (dt: DsDataType) : string =
        match dt with
        | DsDataType.TBool -> "BOOL"
        | DsDataType.TInt -> "INT"
        | DsDataType.TDouble -> "REAL"
        | DsDataType.TString -> "STRING"

    // ═════════════════════════════════════════════════════════════════════
    // 값 포맷팅 헬퍼 (코드 중복 제거)
    // ═════════════════════════════════════════════════════════════════════

    /// 문자열 내부의 단일 따옴표를 이스케이프 (IEC 61131-3: ' → '')
    let private escapeString (s: string) : string =
        s.Replace("'", "''")

    /// 기본값/초기값을 PLC 문법으로 포맷팅
    let private formatDefaultValue (dataType: DsDataType) (value: obj) : string =
        match dataType with
        | DsDataType.TBool -> if (value :?> bool) then " := TRUE" else " := FALSE"
        | DsDataType.TInt -> sprintf " := %d" (value :?> int)
        | DsDataType.TDouble -> sprintf " := %f" (value :?> float)
        | DsDataType.TString -> sprintf " := '%s'" (escapeString (value :?> string))

    /// VAR_INPUT 섹션 생성
    let private generateInputSection (sb: StringBuilder) (params': FunctionParam list) : unit =
        if not (List.isEmpty params') then
            sb.AppendLine("VAR_INPUT") |> ignore
            for param in params' do
                let defaultStr =
                    match param.DefaultValue with
                    | Some v -> formatDefaultValue param.DataType v
                    | None -> ""
                sb.AppendLine(sprintf "    %s : %s%s;" param.Name (dataTypeToPLC param.DataType) defaultStr) |> ignore
            sb.AppendLine("END_VAR") |> ignore
            sb.AppendLine() |> ignore

    /// VAR_OUTPUT 섹션 생성
    let private generateOutputSection (sb: StringBuilder) (params': FunctionParam list) : unit =
        if not (List.isEmpty params') then
            sb.AppendLine("VAR_OUTPUT") |> ignore
            for param in params' do
                sb.AppendLine(sprintf "    %s : %s;" param.Name (dataTypeToPLC param.DataType)) |> ignore
            sb.AppendLine("END_VAR") |> ignore
            sb.AppendLine() |> ignore

    /// VAR_IN_OUT 섹션 생성
    let private generateInOutSection (sb: StringBuilder) (params': FunctionParam list) : unit =
        if not (List.isEmpty params') then
            sb.AppendLine("VAR_IN_OUT") |> ignore
            for param in params' do
                sb.AppendLine(sprintf "    %s : %s;" param.Name (dataTypeToPLC param.DataType)) |> ignore
            sb.AppendLine("END_VAR") |> ignore
            sb.AppendLine() |> ignore

    /// VAR (Static) 섹션 생성
    let private generateStaticSection (sb: StringBuilder) (statics: (string * DsDataType * obj option) list) : unit =
        if not (List.isEmpty statics) then
            sb.AppendLine("VAR") |> ignore
            for (name, dt, initVal) in statics do
                let initStr =
                    match initVal with
                    | Some v -> formatDefaultValue dt v
                    | None -> ""
                sb.AppendLine(sprintf "    %s : %s%s;" name (dataTypeToPLC dt) initStr) |> ignore
            sb.AppendLine("END_VAR") |> ignore
            sb.AppendLine() |> ignore

    /// VAR_TEMP 섹션 생성
    let private generateTempSection (sb: StringBuilder) (temps: (string * DsDataType) list) : unit =
        if not (List.isEmpty temps) then
            sb.AppendLine("VAR_TEMP") |> ignore
            for (name, dt) in temps do
                sb.AppendLine(sprintf "    %s : %s;" name (dataTypeToPLC dt)) |> ignore
            sb.AppendLine("END_VAR") |> ignore
            sb.AppendLine() |> ignore

    // ═════════════════════════════════════════════════════════════════════
    // 수식을 PLC ST 코드로 변환
    // ═════════════════════════════════════════════════════════════════════

    let rec exprToST (expr: DsExpr) : string =
        match expr with
        | Const(value, dt) ->
            match dt with
            | DsDataType.TBool -> if (value :?> bool) then "TRUE" else "FALSE"
            | DsDataType.TInt -> sprintf "%d" (value :?> int)
            | DsDataType.TDouble -> sprintf "%f" (value :?> float)
            | DsDataType.TString -> sprintf "'%s'" (escapeString (value :?> string))

        | Terminal(tag) ->
            tag.Name

        | Binary(op, left, right) ->
            let leftStr = exprToST left
            let rightStr = exprToST right
            match op with
            | DsOp.Add -> sprintf "(%s + %s)" leftStr rightStr
            | DsOp.Sub -> sprintf "(%s - %s)" leftStr rightStr
            | DsOp.Mul -> sprintf "(%s * %s)" leftStr rightStr
            | DsOp.Div -> sprintf "(%s / %s)" leftStr rightStr
            | DsOp.Mod -> sprintf "(%s MOD %s)" leftStr rightStr
            | DsOp.Eq -> sprintf "(%s = %s)" leftStr rightStr
            | DsOp.Ne -> sprintf "(%s <> %s)" leftStr rightStr
            | DsOp.Gt -> sprintf "(%s > %s)" leftStr rightStr
            | DsOp.Ge -> sprintf "(%s >= %s)" leftStr rightStr
            | DsOp.Lt -> sprintf "(%s < %s)" leftStr rightStr
            | DsOp.Le -> sprintf "(%s <= %s)" leftStr rightStr
            | DsOp.And -> sprintf "(%s AND %s)" leftStr rightStr
            | DsOp.Or -> sprintf "(%s OR %s)" leftStr rightStr
            | DsOp.Xor -> sprintf "(%s XOR %s)" leftStr rightStr
            | _ -> sprintf "/* Unsupported op: %A */" op

        | Unary(op, expr) ->
            let exprStr = exprToST expr
            match op with
            | DsOp.Not -> sprintf "NOT %s" exprStr
            | DsOp.Rising -> sprintf "R_TRIG(%s)" exprStr  // Rising edge
            | DsOp.Falling -> sprintf "F_TRIG(%s)" exprStr // Falling edge
            | _ -> sprintf "/* Unsupported unary op: %A */" op

        | Function(name, args) ->
            let argsStr = args |> List.map exprToST |> String.concat ", "
            sprintf "%s(%s)" name argsStr

    // ═════════════════════════════════════════════════════════════════════
    // 명령문을 PLC ST 코드로 변환
    // ═════════════════════════════════════════════════════════════════════

    let rec stmtToST (stmt: DsStmt) : string =
        match stmt with
        | Assign(_, tag, expr) ->
            let tagName = tag.Name
            let exprStr = exprToST expr
            sprintf "%s := %s;" tagName exprStr

        | Command(_, condition, action) ->
            let condStr = exprToST condition
            let actionStr = exprToST action
            sprintf "IF %s THEN\n    %s;\nEND_IF;" condStr actionStr

        | For(_, loopVar, startExpr, endExpr, stepExpr, body) ->
            let varName = loopVar.Name
            let startStr = exprToST startExpr
            let endStr = exprToST endExpr
            let stepStr =
                match stepExpr with
                | Some expr -> sprintf " BY %s" (exprToST expr)
                | None -> ""
            let bodyStr =
                body
                |> List.map stmtToST
                |> String.concat "\n    "
            sprintf "FOR %s := %s TO %s%s DO\n    %s\nEND_FOR;" varName startStr endStr stepStr bodyStr

        | While(_, condition, body, maxIterations) ->
            let condStr = exprToST condition
            let bodyStr =
                body
                |> List.map stmtToST
                |> String.concat "\n    "
            let comment =
                match maxIterations with
                | Some max -> sprintf " (* max iterations: %d *)" max
                | None -> ""
            sprintf "WHILE %s DO%s\n    %s\nEND_WHILE;" condStr comment bodyStr

        | Break(_) ->
            "EXIT;"

    // ═════════════════════════════════════════════════════════════════════
    // FC (Function) 코드 생성
    // ═════════════════════════════════════════════════════════════════════

    let generateFC (fc: UserFC) : string =
        let sb = StringBuilder()

        // 함수 시그니처
        let returnType =
            match fc.Outputs with
            | [] -> "VOID"
            | output::_ -> dataTypeToPLC output.DataType

        sb.AppendLine(sprintf "FUNCTION %s : %s" fc.Name returnType) |> ignore

        // 입력 파라미터
        if not (List.isEmpty fc.Inputs) then
            sb.AppendLine("VAR_INPUT") |> ignore
            for param in fc.Inputs do
                let defaultStr =
                    match param.DefaultValue with
                    | Some v -> formatDefaultValue param.DataType v
                    | None -> ""
                sb.AppendLine(sprintf "    %s : %s%s;" param.Name (dataTypeToPLC param.DataType) defaultStr) |> ignore
            sb.AppendLine("END_VAR") |> ignore

        // 본문
        sb.AppendLine() |> ignore
        let bodyStr = exprToST fc.Body
        sb.AppendLine(sprintf "%s := %s;" fc.Name bodyStr) |> ignore

        // 함수 종료
        sb.AppendLine(sprintf "END_FUNCTION  // %s" fc.Name) |> ignore

        sb.ToString()

    // ═════════════════════════════════════════════════════════════════════
    // FB (Function Block) 코드 생성
    // ═════════════════════════════════════════════════════════════════════

    let generateFB (fb: UserFB) : string =
        let sb = StringBuilder()

        // Function Block 시작
        sb.AppendLine(sprintf "FUNCTION_BLOCK %s" fb.Name) |> ignore

        // 설명 (주석)
        match fb.Metadata.Description with
        | Some desc -> sb.AppendLine(sprintf "(* %s *)" desc) |> ignore
        | None -> ()

        // 변수 섹션들 (헬퍼 함수 사용)
        generateInputSection sb fb.Inputs
        generateOutputSection sb fb.Outputs
        generateInOutSection sb fb.InOuts
        generateStaticSection sb fb.Statics
        generateTempSection sb fb.Temps

        // 본문 (명령문)
        sb.AppendLine("(* Logic *)") |> ignore
        for stmt in fb.Body do
            let stmtStr = stmtToST stmt
            sb.AppendLine(stmtStr) |> ignore

        // Function Block 종료
        sb.AppendLine() |> ignore
        sb.AppendLine(sprintf "END_FUNCTION_BLOCK  // %s" fb.Name) |> ignore

        sb.ToString()

    // ═════════════════════════════════════════════════════════════════════
    // 전체 프로젝트 코드 생성
    // ═════════════════════════════════════════════════════════════════════

    /// UserFB/FC 레지스트리를 PLC 프로젝트 파일로 생성
    let generatePLCProject (registry: UserFBRegistry) (projectName: string) : string =
        let sb = StringBuilder()

        // 프로젝트 헤더
        sb.AppendLine("(*") |> ignore
        sb.AppendLine(sprintf "    PLC Project: %s" projectName) |> ignore
        sb.AppendLine(sprintf "    Generated: %s" (DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))) |> ignore
        sb.AppendLine("    IEC 61131-3 Structured Text") |> ignore
        sb.AppendLine("*)") |> ignore
        sb.AppendLine() |> ignore

        // FC 생성
        let allFCs = registry.GetAllFCs()
        if not (List.isEmpty allFCs) then
            sb.AppendLine("(* ========== FUNCTIONS ========== *)") |> ignore
            sb.AppendLine() |> ignore
            for fc in allFCs do
                sb.AppendLine(generateFC fc) |> ignore
                sb.AppendLine() |> ignore

        // FB 생성
        let allFBs = registry.GetAllFBs()
        if not (List.isEmpty allFBs) then
            sb.AppendLine("(* ========== FUNCTION BLOCKS ========== *)") |> ignore
            sb.AppendLine() |> ignore
            for fb in allFBs do
                sb.AppendLine(generateFB fb) |> ignore
                sb.AppendLine() |> ignore

        // 인스턴스 선언 (메인 프로그램에서 사용할 변수)
        let allInst = registry.GetAllInstances()
        if not (List.isEmpty allInst) then
            sb.AppendLine("(* ========== INSTANCES (for use in main program) ========== *)") |> ignore
            sb.AppendLine("VAR_GLOBAL") |> ignore
            for inst in allInst do
                sb.AppendLine(sprintf "    %s : %s;" inst.Name inst.FBType.Name) |> ignore
            sb.AppendLine("END_VAR") |> ignore
            sb.AppendLine() |> ignore

        sb.ToString()

    // ═════════════════════════════════════════════════════════════════════
    // TwinCAT 프로젝트 파일 생성 (.TcPOU)
    // ═════════════════════════════════════════════════════════════════════

    let generateTwinCATFile (fc: UserFC) : string =
        let sb = StringBuilder()

        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>") |> ignore
        sb.AppendLine("<TcPlcObject Version=\"1.1.0.1\" ProductVersion=\"3.1.4024.12\">") |> ignore
        sb.AppendLine(sprintf "  <POU Name=\"%s\" Id=\"{%s}\" SpecialFunc=\"None\">" fc.Name (Guid.NewGuid().ToString())) |> ignore
        sb.AppendLine("    <Declaration><![CDATA[") |> ignore
        sb.AppendLine(generateFC fc) |> ignore
        sb.AppendLine("    ]]></Declaration>") |> ignore
        sb.AppendLine("    <Implementation>") |> ignore
        sb.AppendLine("      <ST><![CDATA[") |> ignore
        sb.AppendLine(exprToST fc.Body) |> ignore
        sb.AppendLine("      ]]></ST>") |> ignore
        sb.AppendLine("    </Implementation>") |> ignore
        sb.AppendLine("  </POU>") |> ignore
        sb.AppendLine("</TcPlcObject>") |> ignore

        sb.ToString()

    let generateTwinCATFileForFB (fb: UserFB) : string =
        let sb = StringBuilder()

        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>") |> ignore
        sb.AppendLine("<TcPlcObject Version=\"1.1.0.1\" ProductVersion=\"3.1.4024.12\">") |> ignore
        sb.AppendLine(sprintf "  <POU Name=\"%s\" Id=\"{%s}\" SpecialFunc=\"None\">" fb.Name (Guid.NewGuid().ToString())) |> ignore
        sb.AppendLine("    <Declaration><![CDATA[") |> ignore
        sb.AppendLine(generateFB fb) |> ignore
        sb.AppendLine("    ]]></Declaration>") |> ignore
        sb.AppendLine("    <Implementation>") |> ignore
        sb.AppendLine("      <ST><![CDATA[") |> ignore
        for stmt in fb.Body do
            sb.AppendLine(stmtToST stmt) |> ignore
        sb.AppendLine("      ]]></ST>") |> ignore
        sb.AppendLine("    </Implementation>") |> ignore
        sb.AppendLine("  </POU>") |> ignore
        sb.AppendLine("</TcPlcObject>") |> ignore

        sb.ToString()

    // ═════════════════════════════════════════════════════════════════════
    // 파일 저장 헬퍼
    // ═════════════════════════════════════════════════════════════════════

    let savePLCFile (filePath: string) (content: string) =
        System.IO.File.WriteAllText(filePath, content)
        printfn "PLC 파일 생성 완료: %s" filePath

    let savePLCProject (outputDir: string) (projectName: string) (registry: UserFBRegistry) =
        let projectContent = generatePLCProject registry projectName
        let filePath = System.IO.Path.Combine(outputDir, sprintf "%s.st" projectName)
        savePLCFile filePath projectContent

    let saveTwinCATProject (outputDir: string) (registry: UserFBRegistry) =
        // FC 저장
        for fc in registry.GetAllFCs() do
            let content = generateTwinCATFile fc
            let filePath = System.IO.Path.Combine(outputDir, sprintf "%s.TcPOU" fc.Name)
            savePLCFile filePath content

        // FB 저장
        for fb in registry.GetAllFBs() do
            let content = generateTwinCATFileForFB fb
            let filePath = System.IO.Path.Combine(outputDir, sprintf "%s.TcPOU" fb.Name)
            savePLCFile filePath content
