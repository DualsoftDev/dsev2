# Wire 설계 개선안

## 현재 문제점

```fsharp
type PortRef = {
    Node: string  // Element ID인데 타입 명시 안됨
    Port: string  // 어떤 포트가 있는지 알 수 없음
}

type Wire = {
    From: PortRef
    To: PortRef
}
```

**문제:**
1. 타입 안전하지 않음 (문자열 오타 가능)
2. 포트 정의가 없어서 유효성 검증 불가
3. LD vs FBD에서 사용 방식이 다름

## 개선안 1: 타입 안전한 참조 (추천)

```fsharp
// Element ID를 명시적 타입으로
type ElementId = ElementId of string

// 포트는 DU로
type PortName =
    // 공통 포트
    | Out
    | In
    | InOut

    // FB 포트
    | Execute
    | Done
    | Busy
    | Error
    | Axis
    | Position
    | Velocity

    // 타이머 포트
    | PT    // Preset Time
    | Q     // Output
    | ET    // Elapsed Time

    // 커스텀
    | Custom of string

type PortRef = {
    ElementId: ElementId
    Port: PortName
}

type Wire = {
    From: PortRef
    To: PortRef
}
```

**장점:**
- ✅ 타입 안전
- ✅ IntelliSense 지원
- ✅ 오타 방지

**단점:**
- ❌ 모든 가능한 포트를 미리 정의해야 함
- ❌ FB마다 다른 포트가 있는데 일일이 추가 필요

## 개선안 2: 포트 정의 포함 (유연성)

```fsharp
// FBD Node에 포트 정의 추가
type FbdPort = {
    Name: string
    Direction: FbdPortDirection
    DataType: string
}

type FbdPortDirection =
    | Input
    | Output
    | InOut

type FbdNode = {
    Id: string
    NodeType: FbdNodeType
    Position: Position option
    Ports: FbdPort list  // 포트 정의 추가!
} with
    // 포트 검증
    member this.HasPort(portName: string) =
        this.Ports |> List.exists (fun p -> p.Name = portName)

    member this.GetPort(portName: string) =
        this.Ports |> List.tryFind (fun p -> p.Name = portName)

// Wire 검증 함수
let validateWire (diagram: FbdDiagram) (wire: Wire) =
    match diagram.Nodes |> List.tryFind (fun n -> n.Id = wire.From.Node),
          diagram.Nodes |> List.tryFind (fun n -> n.Id = wire.To.Node) with
    | Some fromNode, Some toNode ->
        match fromNode.GetPort(wire.From.Port), toNode.GetPort(wire.To.Port) with
        | Some fromPort, Some toPort ->
            if fromPort.Direction = Output && toPort.Direction = Input then
                Ok ()
            else
                Error "Port direction mismatch"
        | None, _ -> Error $"Port {wire.From.Port} not found on node {fromNode.Id}"
        | _, None -> Error $"Port {wire.To.Port} not found on node {toNode.Id}"
    | None, _ -> Error $"Node {wire.From.Node} not found"
    | _, None -> Error $"Node {wire.To.Node} not found"
```

**장점:**
- ✅ 런타임 검증 가능
- ✅ 유연성 (FB마다 다른 포트)
- ✅ 메타데이터 포함 (DataType, Direction)

**단점:**
- ❌ 컴파일 타임 검증 불가
- ❌ 복잡도 증가

## 개선안 3: 하이브리드 (실용적) ⭐

```fsharp
// 1. 기본 타입은 현재 유지 (문자열)
type PortRef = {
    Node: string  // Element.Id
    Port: string  // 포트 이름
}

type Wire = {
    From: PortRef
    To: PortRef
} with
    // 검증 함수 추가
    member this.Validate(diagram: FbdDiagram) =
        // Node 존재 확인
        let fromNode = diagram.Nodes |> List.tryFind (fun n -> n.Id = this.From.Node)
        let toNode = diagram.Nodes |> List.tryFind (fun n -> n.Id = this.To.Node)

        match fromNode, toNode with
        | Some _, Some _ -> Ok ()
        | None, _ -> Error $"Source node '{this.From.Node}' not found"
        | _, None -> Error $"Target node '{this.To.Node}' not found"

// 2. Helper에서 타입 안전하게
module FbdHelper =
    // ElementId를 반환하도록
    let fbNode fbType =
        let node = {
            Id = System.Guid.NewGuid().ToString()
            NodeType = FB fbType
            Position = None
        }
        node  // node 자체 반환

    // Wire 생성 시 Node 객체 받기
    let wireFromNodes (fromNode: FbdNode) fromPort (toNode: FbdNode) toPort =
        {
            From = { Node = fromNode.Id; Port = fromPort }
            To = { Node = toNode.Id; Port = toPort }
        }

    // 또는 문자열로
    let wire fromNodeId fromPort toNodeId toPort =
        {
            From = { Node = fromNodeId; Port = fromPort }
            To = { Node = toNodeId; Port = toPort }
        }

// 3. 사용 예시
let diagram =
    let n1 = FbdHelper.varNode "Input1"
    let n2 = FbdHelper.opNode "AND"
    let n3 = FbdHelper.varNode "Output"

    {
        Nodes = [ n1; n2; n3 ]
        Wires = [
            FbdHelper.wireFromNodes n1 "Out" n2 "In1"  // 타입 안전!
            FbdHelper.wireFromNodes n2 "Out" n3 "In"
        ]
    }
```

**장점:**
- ✅ 하위 호환 (기존 코드 유지)
- ✅ Helper에서 타입 안전하게 사용 가능
- ✅ 간단함
- ✅ 검증 함수로 보완

**단점:**
- ⚠️ Helper 없이 직접 생성하면 오타 가능

## LD에서 Wire의 의미

Ladder Diagram은 좀 다릅니다:

```fsharp
type LdRung = {
    Id: string
    Elements: LdElement list  // 순서대로 연결됨
    Wires: Wire list          // 병렬 연결용?
    Comment: string option
}
```

**LD의 특성:**
- Elements는 **좌→우 순서대로 암시적 연결**
- Wire는 **병렬 분기(Branch)** 표현용

**예:**
```
Rung: --[A]--+--[B]--+--( Out )--
             |       |
             +--[C]--+
```

이걸 표현하려면:
```fsharp
// 방법 1: Wire로 병렬 표현
{
    Elements = [
        Contact "A"    // E1
        Contact "B"    // E2
        Contact "C"    // E3
        Coil "Out"     // E4
    ]
    Wires = [
        { From = {Node="A"; Port="Out"}; To = {Node="B"; Port="In"} }
        { From = {Node="A"; Port="Out"}; To = {Node="C"; Port="In"} }
        { From = {Node="B"; Port="Out"}; To = {Node="Out"; Port="In"} }
        { From = {Node="C"; Port="Out"}; To = {Node="Out"; Port="In"} }
    ]
}

// 방법 2: Network 구조 (더 직관적)
type LdNetwork =
    | Series of LdNetwork list
    | Parallel of LdNetwork list
    | Element of LdElement

// 사용
let network =
    Series [
        Element (Contact "A")
        Parallel [
            Element (Contact "B")
            Element (Contact "C")
        ]
        Element (Coil "Out")
    ]
```

## 최종 추천

### FBD: 개선안 3 (하이브리드)
- 현재 구조 유지하되 Helper로 타입 안전하게
- `Wire.Validate()` 메서드 추가

### LD: Network 구조 추가
- `LdRung.Wires` 제거
- `LdNetwork` 추가로 병렬/직렬 명시적 표현
