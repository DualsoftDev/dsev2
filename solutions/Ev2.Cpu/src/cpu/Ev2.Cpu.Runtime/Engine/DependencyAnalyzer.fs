namespace Ev2.Cpu.Runtime
open System
open System.Collections.Generic
open Ev2.Cpu.Core

module DependencyAnalyzer =

    /// 표현식에서 사용된 변수 이름 추출
    let rec getExpressionVariables (expr: DsExpr) : string list =
        match expr with
        | Const _ -> []
        | Terminal tag -> [tag.Name]
        | Unary (_, inner) -> getExpressionVariables inner
        | Binary (_, left, right) ->
            getExpressionVariables left @ getExpressionVariables right
        | Function (_, args) ->
            args |> List.collect getExpressionVariables

    /// Phase 2.1: 향상된 의존성 노드 (실행 우선순위 포함)
    type DependencyNode = {
        Statement: DsStmt
        StepNumber: int option
        Dependencies: Set<string>
        Dependents: Set<string>
        mutable ExecutionOrder: int
        mutable LastExecuted: int64
    }

    /// Phase 2.1: 실행 그래프 생성
    let buildExecutionGraph (stmts: DsStmt list) : Map<string, DependencyNode> =
        let extractStepNumber stmt =
            match stmt with
            | Assign (step, _, _) -> step
            | Command (step, _, _) -> step

        let extractTargetName stmt =
            match stmt with
            | Assign (_, target, _) -> Some target.Name
            | Command (_, _, Function ("MOV", [_; Terminal target])) -> Some target.Name
            | _ -> None

        // 1단계: 기본 노드 생성 (handle duplicates by merging dependencies)
        let nodes =
            stmts
            |> List.choose (fun stmt ->
                match extractTargetName stmt with
                | Some targetName ->
                    let deps =
                        match stmt with
                        | Assign (_, _, expr) -> getExpressionVariables expr
                        | Command (_, condition, action) ->
                            getExpressionVariables condition @ getExpressionVariables action
                        | _ -> []
                    Some (targetName, {
                        Statement = stmt
                        StepNumber = Some (extractStepNumber stmt)
                        Dependencies = Set.ofList deps |> Set.remove targetName
                        Dependents = Set.empty
                        ExecutionOrder = 0
                        LastExecuted = 0L
                    })
                | None -> None)
            |> List.fold (fun (map: Map<string, DependencyNode>) (targetName, node) ->
                match map.TryFind targetName with
                | Some existing ->
                    // Merge dependencies when duplicate target found
                    let merged = { existing with
                                    Dependencies = Set.union existing.Dependencies node.Dependencies
                                    Statement = node.Statement }  // Keep latest statement
                    Map.add targetName merged map
                | None ->
                    Map.add targetName node map
            ) Map.empty

        // 2단계: 역방향 종속성 계산
        let nodesWithDependents =
            nodes
            |> Map.map (fun targetName node ->
                let dependents =
                    nodes
                    |> Map.toSeq
                    |> Seq.filter (fun (_, otherNode) -> Set.contains targetName otherNode.Dependencies)
                    |> Seq.map fst
                    |> Set.ofSeq
                { node with Dependents = dependents })

        // 3단계: 실행 순서 계산 (토폴로지 정렬)
        let calculateExecutionOrder (nodes: Map<string, DependencyNode>) =
            let mutable order = 0
            let visited = HashSet<string>()
            
            let rec visit nodeName =
                if not (visited.Contains(nodeName)) then
                    visited.Add(nodeName) |> ignore
                    match nodes.TryFind(nodeName) with
                    | Some node ->
                        // 종속성이 있는 노드들을 먼저 방문
                        for dep in node.Dependencies do
                            visit dep
                        node.ExecutionOrder <- order
                        order <- order + 1
                    | None -> ()
            
            for KeyValue(nodeName, _) in nodes do
                visit nodeName

        calculateExecutionOrder nodesWithDependents
        nodesWithDependents

    /// 프로그램 전체 의존성 맵 (target → 해당 target이 의존하는 입력) 생성 - 레거시 호환성
    let buildDependencyMap (stmts: DsStmt list) : Map<string, Set<string>> =
        let addTargetDependencies deps targetName inputs =
            let filtered =
                inputs
                |> List.filter (fun name -> name <> targetName)
                |> Set.ofList
            // Merge with existing dependencies instead of overwriting
            match Map.tryFind targetName deps with
            | Some existingDeps -> deps |> Map.add targetName (Set.union existingDeps filtered)
            | None -> deps |> Map.add targetName filtered

        let handleAssign deps (target: DsTag) expr =
            let inputs = getExpressionVariables expr
            addTargetDependencies deps target.Name inputs

        let handleCommand deps condition action =
            match action with
            | Function ("MOV", [source; Terminal targetTag]) ->
                let inputs =
                    getExpressionVariables condition @ getExpressionVariables source
                addTargetDependencies deps targetTag.Name inputs
            | Function ("SET", [Terminal targetTag]) | Function ("RESET", [Terminal targetTag]) ->
                let inputs = getExpressionVariables condition
                addTargetDependencies deps targetTag.Name inputs
            | Function (fname, args) when fname.StartsWith("CT") || fname.StartsWith("TO") ->
                // Timers (TON, TOF) and Counters (CTU, CTD, CTUD) modify state
                // Extract target from second argument if it's a Terminal
                match args with
                | _ :: Terminal targetTag :: _ ->
                    let inputs = getExpressionVariables condition @ (args |> List.collect getExpressionVariables)
                    addTargetDependencies deps targetTag.Name inputs
                | _ -> deps
            | _ -> deps

        // Process all statements
        let rec processStatement deps stmt =
            match stmt with
            | Assign (_, target, expr) -> handleAssign deps target expr
            | Command (_, condition, actionExpr) -> handleCommand deps condition actionExpr
            | _ -> deps

        stmts |> List.fold processStatement Map.empty

    /// Phase 2.1: 선택적 실행 후보 결정
    let getSelectiveExecutionCandidates (graph: Map<string, DependencyNode>) (changedVars: Set<string>) : Set<string> =
        let candidates = HashSet<string>()
        
        let rec addInfluenced (varName: string) =
            if candidates.Add(varName) then
                match graph.TryFind(varName) with
                | Some node ->
                    for dependent in node.Dependents do
                        addInfluenced dependent
                | None -> ()
        
        for changedVar in changedVars do
            addInfluenced changedVar
        
        Set.ofSeq candidates
