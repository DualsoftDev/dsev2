namespace Ev2.Cpu.Core

open System
open Ev2.Cpu.Ast

/// <summary>프로그램 포뮬라 (스테이트먼트 모음과 메타데이터)</summary>
/// <remarks>
/// 하나의 실행 단위로 묶인 스테이트먼트 목록을 표현합니다.
/// - ScopePath + Key로 고유하게 식별됨
/// - 버전 관리 및 타임스탬프 지원
/// - 읽기/쓰기 변수 분석 기능 제공
/// </remarks>
[<CLIMutable>]
type DsFormula = {
    /// <summary>고유 식별자 (GUID)</summary>
    Id: Guid
    /// <summary>스코프 경로 (계층 구조, 예: "System.PLC01")</summary>
    ScopePath: string
    /// <summary>포뮬라 키 (스코프 내 유일한 이름)</summary>
    Key: string
    /// <summary>실행할 스테이트먼트 목록</summary>
    Statements: DsStatement list
    /// <summary>변수 타입 정보 (변수 이름 -> 타입)</summary>
    Variables: Map<string, Type>
    /// <summary>버전 번호 (수정 시 증가)</summary>
    Version: int
    /// <summary>생성 일시 (UTC)</summary>
    CreatedAt: DateTime
    /// <summary>최종 수정 일시 (UTC)</summary>
    UpdatedAt: DateTime
    /// <summary>태그 집합 (분류, 검색용)</summary>
    Tags: Set<string>
}
with
    /// <summary>DsFormula 생성 팩토리 메서드</summary>
    /// <param name="scopePath">스코프 경로 (유효성 검증됨)</param>
    /// <param name="key">포뮬라 키 (비어있지 않아야 함)</param>
    /// <param name="statements">스테이트먼트 목록 (비어있지 않아야 함, 모두 유효해야 함)</param>
    /// <param name="variables">변수 타입 정보 (optional, 기본값: 빈 Map)</param>
    /// <param name="tags">태그 집합 (optional, 기본값: 빈 Set)</param>
    /// <returns>생성된 DsFormula</returns>
    /// <exception cref="System.ArgumentException">scopePath, key, statements가 유효하지 않은 경우</exception>
    static member Create(scopePath: string, key: string, statements: DsStatement list, ?variables: Map<string, Type>, ?tags: Set<string>) =
        // Validate scope path (simple validation)
        if String.IsNullOrWhiteSpace scopePath then
            invalidArg "scopePath" "ScopePath cannot be empty"
        
        if String.IsNullOrWhiteSpace key then
            invalidArg "key" "Key cannot be empty"
        if List.isEmpty statements then
            invalidArg "statements" "Formula cannot have empty statements"
        
        // Validate all statements
        for stmt in statements do
            match stmt.Validate() with
            | Error msg -> invalidArg "statements" (sprintf "Invalid statement: %s" msg)
            | Ok () -> ()
        
        {
            Id = Guid.NewGuid()
            ScopePath = scopePath
            Key = key
            Statements = statements
            Variables = defaultArg variables Map.empty
            Version = 1
            CreatedAt = DateTime.UtcNow
            UpdatedAt = DateTime.UtcNow
            Tags = defaultArg tags Set.empty
        }

    /// <summary>버전 증가 및 타임스탬프 갱신</summary>
    /// <returns>버전이 1 증가하고 UpdatedAt이 현재 시각으로 갱신된 새 DsFormula</returns>
    member this.IncrementVersion() =
        { this with Version = this.Version + 1; UpdatedAt = DateTime.UtcNow }

    /// <summary>변수 타입 정보 추가 또는 갱신</summary>
    /// <param name="name">변수 이름 (비어있지 않아야 함)</param>
    /// <param name="typ">데이터 타입</param>
    /// <returns>변수가 추가/갱신된 새 DsFormula</returns>
    /// <exception cref="System.ArgumentException">name이 비어있는 경우</exception>
    member this.WithVariable(name: string, typ: Type) =
        if String.IsNullOrWhiteSpace name then
            invalidArg "name" "Variable name cannot be empty"
        { this with Variables = Map.add name typ this.Variables }

    /// <summary>여러 변수 타입 정보 추가</summary>
    /// <param name="variables">변수 목록 (이름, 타입) 쌍</param>
    /// <returns>변수들이 추가/갱신된 새 DsFormula</returns>
    member this.WithVariables(variables: (string * Type) seq) =
        let newVars = variables |> Map.ofSeq
        { this with Variables = Map.fold (fun acc k v -> Map.add k v acc) this.Variables newVars }

    /// <summary>태그 추가</summary>
    /// <param name="tag">추가할 태그 (비어있으면 무시)</param>
    /// <returns>태그가 추가된 새 DsFormula</returns>
    member this.WithTag(tag: string) =
        if String.IsNullOrWhiteSpace tag then this
        else { this with Tags = Set.add tag this.Tags }

    /// <summary>포뮬라가 쓰는 모든 변수 목록</summary>
    /// <returns>쓰기 대상 변수 이름 집합</returns>
    member this.GetWriteTargets() : Set<string> =
        this.Statements |> List.map (fun s -> s.GetWriteTargets()) |> Set.unionMany

    /// <summary>포뮬라가 읽는 모든 변수 목록</summary>
    /// <returns>읽기 대상 변수 이름 집합</returns>
    member this.GetReadTargets() : Set<string> =
        this.Statements |> List.map (fun s -> s.GetReadTargets()) |> Set.unionMany

    /// <summary>포뮬라 복잡도 메트릭스</summary>
    /// <returns>
    /// 익명 레코드:
    /// - StatementCount: 스테이트먼트 총 개수
    /// - MaxNestingDepth: 최대 중첩 깊이
    /// - HasLoops: 루프 포함 여부
    /// - FunctionCalls: 함수 호출 목록
    /// - WriteTargets: 쓰기 변수 개수
    /// - ReadTargets: 읽기 변수 개수
    /// </returns>
    member this.GetMetrics() = {|
        StatementCount = this.Statements |> List.sumBy StmtAnalysis.statementCount
        MaxNestingDepth = this.Statements |> List.map StmtAnalysis.nestingDepth |> List.append [0] |> List.max
        HasLoops = this.Statements |> List.exists StmtAnalysis.hasLoops
        FunctionCalls = this.Statements |> List.map StmtAnalysis.getFunctionCalls |> Set.unionMany
        WriteTargets = this.GetWriteTargets().Count
        ReadTargets = this.GetReadTargets().Count
    |}

    /// <summary>포뮬라를 사람이 읽을 수 있는 텍스트로 변환</summary>
    /// <returns>포뮬라 전체를 표현하는 텍스트 문자열</returns>
    /// <remarks>
    /// 출력 형식:
    /// - FORMULA 헤더 (ScopePath.Key)
    /// - 버전, 생성/수정 일시
    /// - 태그 (있는 경우)
    /// - 변수 목록
    /// - 프로그램 본문 (들여쓰기 포함)
    /// - END_FORMULA
    /// </remarks>
    member this.ToText() : string =
        let header = [
            sprintf "FORMULA %s.%s" this.ScopePath this.Key
            sprintf "  Version: %d" this.Version
            sprintf "  Created: %s" (this.CreatedAt.ToString("yyyy-MM-dd HH:mm"))
            sprintf "  Updated: %s" (this.UpdatedAt.ToString("yyyy-MM-dd HH:mm"))
        ]
        
        let tagsLine = if this.Tags.IsEmpty then [] else [sprintf "  Tags: %s" (String.concat ", " this.Tags)]
        
        let variables = 
            if this.Variables.IsEmpty then []
            else
                "VARIABLES:" :: [
                    for KeyValue(name, typ) in this.Variables ->
                        sprintf "  %s: %O" name typ
                ]
        
        let statements = 
            "PROGRAM:" :: [
                for stmt in this.Statements ->
                    stmt.ToText(1)
            ]
        
        let allLines = List.concat [header; tagsLine; [""]; variables; [""]; statements; ["END_FORMULA"]]
        String.concat "\n" allLines

/// <summary>포뮬라 저장소 인터페이스</summary>
/// <remarks>
/// 포뮬라의 영속화(persistence)를 담당하는 인터페이스입니다.
/// InMemoryFormulaStore가 기본 구현을 제공하며, 다른 스토리지 백엔드도 구현 가능합니다.
/// </remarks>
type IFormulaStore =
    /// <summary>포뮬라 저장</summary>
    /// <param name="formula">저장할 DsFormula</param>
    /// <remarks>동일한 ScopePath + Key가 있으면 버전 증가하여 덮어씀</remarks>
    abstract member Save: DsFormula -> unit

    /// <summary>포뮬라 로드</summary>
    /// <param name="scopePath">스코프 경로</param>
    /// <param name="key">포뮬라 키</param>
    /// <returns>찾은 포뮬라 (없으면 None)</returns>
    abstract member Load: scopePath:string * key:string -> DsFormula option

    /// <summary>특정 스코프의 모든 포뮬라 목록</summary>
    /// <param name="scopePath">스코프 경로</param>
    /// <returns>해당 스코프의 포뮬라 목록 (Key로 정렬)</returns>
    abstract member List: scopePath:string -> DsFormula list

    /// <summary>모든 포뮬라 목록</summary>
    /// <returns>전체 포뮬라 목록 (ScopePath, Key로 정렬)</returns>
    abstract member ListAll: unit -> DsFormula list

    /// <summary>포뮬라 삭제</summary>
    /// <param name="scopePath">스코프 경로</param>
    /// <param name="key">포뮬라 키</param>
    /// <returns>삭제 성공 여부</returns>
    abstract member Delete: scopePath:string * key:string -> bool

    /// <summary>모든 포뮬라 삭제</summary>
    abstract member Clear: unit -> unit

    /// <summary>저장된 포뮬라 개수</summary>
    /// <returns>총 포뮬라 개수</returns>
    abstract member Count: unit -> int

    /// <summary>포뮬라 검색</summary>
    /// <param name="query">검색 쿼리 (Key, ScopePath, Tags에서 검색)</param>
    /// <returns>검색 결과 포뮬라 목록</returns>
    abstract member Search: query:string -> DsFormula list

/// <summary>인메모리 포뮬라 저장소 구현</summary>
/// <remarks>
/// ConcurrentDictionary를 사용하여 thread-safe한 저장소를 제공합니다.
/// 키는 "ScopePath::Key" 형식으로 생성됩니다.
/// </remarks>
type InMemoryFormulaStore() =
    let store = System.Collections.Concurrent.ConcurrentDictionary<string, DsFormula>()
    
    let makeKey (scopePath: string, key: string) =
        if String.IsNullOrWhiteSpace scopePath || String.IsNullOrWhiteSpace key then
            invalidArg "scopePath/key" "Scope path and key cannot be empty"
        sprintf "%s::%s" scopePath key

    interface IFormulaStore with
        member _.Save(formula) =
            let k = makeKey(formula.ScopePath, formula.Key)
            match store.TryGetValue k with
            | true, existing ->
                store.[k] <- { formula with Version = existing.Version + 1; UpdatedAt = DateTime.UtcNow }
            | false, _ ->
                store.[k] <- formula

        member _.Load(scopePath, key) =
            let k = makeKey(scopePath, key)
            match store.TryGetValue k with
            | true, formula -> Some formula
            | false, _ -> None

        member _.List(scopePath) =
            if String.IsNullOrWhiteSpace scopePath then
                invalidArg "scopePath" "Scope path cannot be empty"
            store.Values
            |> Seq.filter (fun f -> f.ScopePath = scopePath)
            |> Seq.sortBy (fun f -> f.Key)
            |> Seq.toList

        member _.ListAll() =
            store.Values
            |> Seq.sortBy (fun f -> f.ScopePath, f.Key)
            |> Seq.toList

        member _.Delete(scopePath, key) =
            let k = makeKey(scopePath, key)
            store.TryRemove(k) |> fst

        member _.Clear() = store.Clear()

        member _.Count() = store.Count

        member _.Search(query) =
            if String.IsNullOrWhiteSpace query then []
            else
                let lowerQuery = query.ToLowerInvariant()
                store.Values
                |> Seq.filter (fun f ->
                    f.Key.ToLowerInvariant().Contains(lowerQuery) ||
                    f.ScopePath.ToLowerInvariant().Contains(lowerQuery) ||
                    f.Tags |> Set.exists (fun t -> t.ToLowerInvariant().Contains(lowerQuery)))
                |> Seq.sortBy (fun f -> f.ScopePath, f.Key)
                |> Seq.toList

/// <summary>Fluent API를 사용한 포뮬라 빌더</summary>
/// <remarks>
/// 스테이트먼트를 순차적으로 추가하고 DsFormula를 생성하는 편리한 빌더 패턴입니다.
/// </remarks>
/// <example>
/// <code>
/// let builder = FormulaBuilder("System.PLC01", "MyFormula")
/// builder.Assign("OUT1", typeof<bool>, EVar("IN1"))
/// builder.Tag("control")
/// let formula = builder.Build()
/// </code>
/// </example>
type FormulaBuilder(scopePath: string, key: string) =
    let mutable statements = []
    let mutable variables = Map.empty
    let mutable author = None
    let mutable description = None
    let mutable tags = Set.empty

    /// <summary>할당(Assignment) 스테이트먼트 추가</summary>
    /// <param name="targetName">대상 변수 이름</param>
    /// <param name="targetType">대상 변수 타입</param>
    /// <param name="condition">할당할 표현식</param>
    member _.Assign(targetName: string, targetType: Type, condition: DsExpr) =
        let stmt = StmtBuilder.assign targetName targetType condition
        statements <- stmt :: statements

    /// <summary>코일(Coil) 스테이트먼트 추가</summary>
    /// <param name="setCond">Set 조건</param>
    /// <param name="resetCond">Reset 조건</param>
    /// <param name="coilName">코일 이름</param>
    /// <param name="coilType">코일 타입</param>
    /// <param name="selfHold">자기 유지(Self-hold) 여부</param>
    member _.Coil(setCond: DsExpr, resetCond: DsExpr, coilName: string, coilType: Type, selfHold: bool) =
        let stmt = StmtBuilder.coil coilName coilType setCond resetCond selfHold
        statements <- stmt :: statements

    /// <summary>타이머 스테이트먼트 추가</summary>
    /// <param name="name">타이머 이름</param>
    /// <param name="presetMs">프리셋 시간 (밀리초)</param>
    /// <param name="rungIn">Rung In 조건 (optional)</param>
    /// <param name="reset">Reset 조건 (optional)</param>
    member _.Timer(name: string, presetMs: int, ?rungIn: DsExpr, ?reset: DsExpr) =
        let stmt = StmtBuilder.timer name presetMs rungIn reset
        statements <- stmt :: statements

    /// <summary>카운터 스테이트먼트 추가</summary>
    /// <param name="name">카운터 이름</param>
    /// <param name="preset">프리셋 값</param>
    /// <param name="up">Up 조건 (optional)</param>
    /// <param name="down">Down 조건 (optional)</param>
    /// <param name="reset">Reset 조건 (optional)</param>
    member _.Counter(name: string, preset: int, ?up: DsExpr, ?down: DsExpr, ?reset: DsExpr) =
        let stmt = StmtBuilder.counter name preset up down reset
        statements <- stmt :: statements


    /// <summary>커스텀 스테이트먼트 추가</summary>
    /// <param name="stmt">추가할 DsStatement</param>
    member _.Statement(stmt: DsStatement) =
        statements <- stmt :: statements

    /// <summary>변수 타입 정보 추가</summary>
    /// <param name="name">변수 이름</param>
    /// <param name="typ">데이터 타입</param>
    member _.Variable(name: string, typ: Type) =
        variables <- Map.add name typ variables

    /// <summary>여러 변수 타입 정보 추가</summary>
    /// <param name="vars">변수 목록 (이름, 타입) 쌍</param>
    member _.Variables(vars: (string * Type) seq) =
        variables <- vars |> Seq.fold (fun acc (name, typ) -> Map.add name typ acc) variables

    /// <summary>작성자 설정</summary>
    /// <param name="authorName">작성자 이름</param>
    member _.Author(authorName: string) =
        author <- Some authorName

    /// <summary>설명 설정</summary>
    /// <param name="desc">설명 문자열</param>
    member _.Description(desc: string) =
        description <- Some desc

    /// <summary>태그 추가</summary>
    /// <param name="tag">추가할 태그</param>
    member _.Tag(tag: string) =
        tags <- Set.add tag tags

    /// <summary>여러 태그 추가</summary>
    /// <param name="tagList">추가할 태그 목록</param>
    member _.Tags(tagList: string seq) =
        tags <- tagList |> Set.ofSeq |> Set.union tags

    /// <summary>포뮬라 빌드</summary>
    /// <returns>생성된 DsFormula</returns>
    /// <remarks>스테이트먼트 목록이 추가 순서대로 정렬됩니다.</remarks>
    member _.Build() : DsFormula =
        DsFormula.Create(
            scopePath, 
            key, 
            List.rev statements,  // Reverse to maintain order
            variables,
            tags
        )

/// <summary>프로그램 분석 유틸리티 모듈</summary>
/// <remarks>
/// 포뮬라 간 의존성 분석, 순환 참조 탐지, 실행 순서 계산 등을 제공합니다.
/// </remarks>
module ProgramAnalysis =

    /// <summary>포뮬라 의존성 분석</summary>
    /// <param name="formulas">분석할 포뮬라 목록</param>
    /// <returns>
    /// 각 포뮬라에 대한 의존성 정보 목록:
    /// - Formula: 대상 포뮬라
    /// - Providers: 이 포뮬라가 읽는 변수를 제공하는 포뮬라들
    /// - Consumers: 이 포뮬라가 쓰는 변수를 읽는 포뮬라들
    /// - ReadVars: 읽는 변수 집합
    /// - WriteVars: 쓰는 변수 집합
    /// </returns>
    let analyzeDependencies (formulas: DsFormula list) =
        let formulaMap = formulas |> List.map (fun f -> (f.ScopePath, f.Key), f) |> Map.ofList
        
        let getDependencies (formula: DsFormula) =
            let readVars = formula.GetReadTargets()
            let writeVars = formula.GetWriteTargets()
            
            // Find which other formulas provide the variables this formula reads
            let providers = 
                formulas 
                |> List.filter (fun other -> other.Id <> formula.Id)
                |> List.filter (fun other -> 
                    let otherWrites = other.GetWriteTargets()
                    not (Set.intersect readVars otherWrites).IsEmpty)
            
            // Find which other formulas read the variables this formula writes
            let consumers = 
                formulas 
                |> List.filter (fun other -> other.Id <> formula.Id)
                |> List.filter (fun other ->
                    let otherReads = other.GetReadTargets()
                    not (Set.intersect writeVars otherReads).IsEmpty)
            
            {|
                Formula = formula
                Providers = providers
                Consumers = consumers
                ReadVars = readVars
                WriteVars = writeVars
            |}
        
        formulas |> List.map getDependencies

    /// <summary>잠재적 순환 참조 탐지</summary>
    /// <param name="formulas">검사할 포뮬라 목록</param>
    /// <returns>순환 참조에 포함된 포뮬라 ID 목록</returns>
    /// <remarks>DFS(깊이 우선 탐색)를 사용하여 순환을 탐지합니다.</remarks>
    let findCircularDependencies (formulas: DsFormula list) =
        let deps = analyzeDependencies formulas
        
        // Build dependency graph
        let graph = 
            deps |> List.map (fun d -> 
                d.Formula.Id, d.Providers |> List.map (fun p -> p.Id)
            ) |> Map.ofList
        
        // Detect cycles using DFS
        let rec hasCycle visited path current =
            if Set.contains current path then true
            elif Set.contains current visited then false
            else
                let neighbors = Map.tryFind current graph |> Option.defaultValue []
                let newVisited = Set.add current visited
                let newPath = Set.add current path
                neighbors |> List.exists (hasCycle newVisited newPath)
        
        let allFormulas = formulas |> List.map (fun f -> f.Id) |> Set.ofList
        allFormulas |> Set.filter (hasCycle Set.empty Set.empty) |> Set.toList

    /// <summary>의존성 기반 실행 순서 계산</summary>
    /// <param name="formulas">정렬할 포뮬라 목록</param>
    /// <returns>의존성을 고려한 실행 순서대로 정렬된 포뮬라 목록</returns>
    /// <remarks>
    /// 위상 정렬(topological sort)을 사용하여 실행 순서를 결정합니다.
    /// 순환 참조가 있는 경우 나머지 포뮬라를 순서대로 추가합니다.
    /// </remarks>
    let calculateExecutionOrder (formulas: DsFormula list) =
        let deps = analyzeDependencies formulas
        
        // Topological sort
        let rec topSort visited result remaining =
            match remaining with
            | [] -> List.rev result
            | _ ->
                // Find formulas with no unprocessed dependencies
                let ready = 
                    remaining |> List.filter (fun f ->
                        let providers = deps |> List.find (fun d -> d.Formula.Id = f.Id) |> fun d -> d.Providers
                        providers |> List.forall (fun p -> visited |> Set.contains p.Id)
                    )
                
                match ready with
                | [] -> 
                    // Circular dependency detected, just add remaining formulas
                    (List.rev result) @ remaining
                | _ ->
                    let newVisited = ready |> List.fold (fun acc f -> Set.add f.Id acc) visited
                    let newRemaining = remaining |> List.filter (fun f -> not (ready |> List.exists (fun r -> r.Id = f.Id)))
                    topSort newVisited (ready @ result) newRemaining
        
        topSort Set.empty [] formulas