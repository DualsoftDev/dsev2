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

    let dataTypeToPLC (dt: Type) : string =
        if dt = typeof<bool> then "BOOL"
        elif dt = typeof<sbyte> then "SINT"      // Signed INTeger, 8-bit
        elif dt = typeof<byte> then "USINT"      // Unsigned Short INTeger, 8-bit
        elif dt = typeof<int16> then "INT"       // 16-bit signed (PLC standard)
        elif dt = typeof<uint16> then "UINT"     // 16-bit unsigned
        elif dt = typeof<int> then "DINT"        // Double INTeger, 32-bit signed (BREAKING CHANGE: was INT)
        elif dt = typeof<uint32> then "UDINT"    // Unsigned Double INTeger, 32-bit
        elif dt = typeof<int64> then "LINT"      // Long INTeger, 64-bit signed
        elif dt = typeof<uint64> then "ULINT"    // Unsigned Long INTeger, 64-bit
        elif dt = typeof<double> then "LREAL"    // Long REAL, 64-bit (BREAKING CHANGE: was REAL)
        elif dt = typeof<string> then "STRING"
        else failwith $"Unsupported data type: {dt.Name}"

    // ═════════════════════════════════════════════════════════════════════
    // 값 포맷팅 헬퍼 (코드 중복 제거)
    // ═════════════════════════════════════════════════════════════════════

    /// 문자열 내부의 단일 따옴표를 이스케이프 (IEC 61131-3: ' → '')
    let private escapeString (s: string) : string =
        s.Replace("'", "''")

    /// 기본값/초기값을 PLC 문법으로 포맷팅
    let private formatDefaultValue (dataType: Type) (value: obj) : string =
        if dataType = typeof<bool> then
            if (value :?> bool) then " := TRUE" else " := FALSE"
        elif dataType = typeof<sbyte> then
            sprintf " := %d" (value :?> sbyte)
        elif dataType = typeof<byte> then
            sprintf " := %d" (value :?> byte)
        elif dataType = typeof<int16> then
            sprintf " := %d" (value :?> int16)
        elif dataType = typeof<uint16> then
            sprintf " := %d" (value :?> uint16)
        elif dataType = typeof<int> then
            sprintf " := %d" (value :?> int)
        elif dataType = typeof<uint32> then
            sprintf " := %d" (value :?> uint32)
        elif dataType = typeof<int64> then
            sprintf " := %d" (value :?> int64)
        elif dataType = typeof<uint64> then
            sprintf " := %d" (value :?> uint64)
        elif dataType = typeof<double> then
            sprintf " := %f" (value :?> float)
        elif dataType = typeof<string> then
            sprintf " := '%s'" (escapeString (value :?> string))
        else
            failwith $"Unsupported data type for default value: {dataType.Name}"

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
    let private generateStaticSection (sb: StringBuilder) (statics: (string * Type * obj option) list) : unit =
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
    let private generateTempSection (sb: StringBuilder) (temps: (string * Type) list) : unit =
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
            if dt = typeof<bool> then
                if (value :?> bool) then "TRUE" else "FALSE"
            elif dt = typeof<sbyte> then
                sprintf "%d" (value :?> sbyte)
            elif dt = typeof<byte> then
                sprintf "%d" (value :?> byte)
            elif dt = typeof<int16> then
                sprintf "%d" (value :?> int16)
            elif dt = typeof<uint16> then
                sprintf "%d" (value :?> uint16)
            elif dt = typeof<int> then
                sprintf "%d" (value :?> int)
            elif dt = typeof<uint32> then
                sprintf "%d" (value :?> uint32)
            elif dt = typeof<int64> then
                sprintf "%d" (value :?> int64)
            elif dt = typeof<uint64> then
                sprintf "%d" (value :?> uint64)
            elif dt = typeof<double> then
                sprintf "%f" (value :?> float)
            elif dt = typeof<string> then
                sprintf "'%s'" (escapeString (value :?> string))
            else
                failwith $"Unsupported constant type: {dt.Name}"

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
