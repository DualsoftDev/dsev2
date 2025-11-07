namespace Ev2.Gen.IR.Unused

open System
open Ev2.Gen

[<AutoOpen>]
module IRPOUs =

    /// Function Block Node Type
    type FunctionBlockNodeType =
        | VAR of varName: string  // Variable reference
        | CONST of value: obj  // Constant value
        | FB of fbType: string  // Function Block instance
        | OP of operator: string  // Operator (AND, OR, ADD, etc.)

    /// Function Block Node
    type FunctionBlockNode = {
        Id: string
        NodeType: FunctionBlockNodeType
        Position: Position option
    } with
        // Type Extension
        member this.IsInput =
            match this.NodeType with
            | VAR _ | CONST _ -> true
            | _ -> false

        member this.IsOutput =
            match this.NodeType with
            | VAR _ -> true  // VAR은 입출력 모두 가능
            | _ -> false

        member this.IsLogic =
            match this.NodeType with
            | FB _ | OP _ -> true
            | _ -> false

    /// Function Block Diagram (그래픽 표현)
    type FunctionBlockDiagram = {
        Nodes: FunctionBlockNode list
        Wires: Wire list
    }

    /// Function Block Body (상태를 갖는 인스턴스 기반 구현)
    type FunctionBlockBody = {
        Diagram: FunctionBlockDiagram
    }

    /// Function Expression for algorithmic POUs (순수 계산 표현)
    type FunctionExpression =
        | Literal of obj
        | Variable of string
        | Call of name: string * arguments: FunctionExpression list
        | Unary of operator: string * operand: FunctionExpression
        | Binary of left: FunctionExpression * operator: string * right: FunctionExpression

    /// Function Statement (지역 계산 또는 제어 흐름)
    type FunctionStatement =
        | Assignment of target: string * expression: FunctionExpression
        | Invoke of name: string * arguments: FunctionExpression list
        | Return of FunctionExpression option

    /// Function 구현 유형
    type FunctionImplementation =
        | Expression of FunctionExpression
        | Statements of FunctionStatement list

    /// Function Body (인스턴스 없이 결과값을 반환하는 순수 함수)
    type FunctionBody = {
        ReturnType: string
        ReturnVariable: string
        Implementation: FunctionImplementation
    }

    /// LD Contact Type
    type LdContactType =
        | NormalOpen  // --| |--
        | NormalClose  // --|/|--
        | PosEdge  // --|P|--
        | NegEdge  // --|N|--

    /// LD Coil Type
    type LdCoilType =
        | Normal  // --( )--
        | Negated  // --(/)--
        | Set  // --(S)--
        | Reset  // --(R)--

    /// LD Contact
    type LdContact = {
        Id: string
        ContactType: LdContactType
        VarName: string
        Position: Position option
    }

    /// LD Coil
    type LdCoil = {
        Id: string
        CoilType: LdCoilType
        VarName: string
        Position: Position option
    }

    /// LD Function Block
    type LdFunctionBlock = {
        Id: string
        FbType: string
        Instance: string option
        Position: Position option
    }

    /// LD 비교 연산자
    type LdCompareOp =
        | Equal          // ==
        | NotEqual       // <>
        | GreaterThan    // >
        | LessThan       // <
        | GreaterOrEqual // >=
        | LessOrEqual    // <=

    /// LD Compare Contact
    type LdCompareContact = {
        Id: string
        Operator: LdCompareOp
        Operand1: string  // 변수명 또는 상수
        Operand2: string  // 변수명 또는 상수
        Position: Position option
    }

    /// LD Timer Type
    type LdTimerType =
        | TON  // Timer On-Delay
        | TOF  // Timer Off-Delay
        | TP   // Timer Pulse
        | TONR // Timer On-Delay Retentive

    /// LD Timer
    type LdTimer = {
        Id: string
        TimerType: LdTimerType
        Instance: string  // 인스턴스 변수명
        PT: string  // Preset Time 변수/상수
        Position: Position option
    }

    /// LD Counter Type
    type LdCounterType =
        | CTU  // Count Up
        | CTD  // Count Down
        | CTUD // Count Up/Down

    /// LD Counter
    type LdCounter = {
        Id: string
        CounterType: LdCounterType
        Instance: string
        PV: string  // Preset Value
        Position: Position option
    }

    /// LD Math Operation
    type LdMathOp =
        | ADD
        | SUB
        | MUL
        | DIV
        | MOD

    /// LD Math Block
    type LdMathBlock = {
        Id: string
        Operation: LdMathOp
        Inputs: string list  // 입력 변수/상수 리스트
        Output: string  // 출력 변수
        Position: Position option
    }

    /// LD Element (Rung의 구성 요소) - 확장 포함
    type LdElement =
        // 기본 요소
        | Contact of LdContact
        | Coil of LdCoil
        | FunctionBlock of LdFunctionBlock
        // 확장 요소
        | CompareContact of LdCompareContact
        | Timer of LdTimer
        | Counter of LdCounter
        | MathBlock of LdMathBlock

        // Type Extension: 공통 속성
        member this.Id =
            match this with
            | Contact c -> c.Id
            | Coil c -> c.Id
            | FunctionBlock fb -> fb.Id
            | CompareContact cc -> cc.Id
            | Timer t -> t.Id
            | Counter c -> c.Id
            | MathBlock m -> m.Id

        member this.Position =
            match this with
            | Contact c -> c.Position
            | Coil c -> c.Position
            | FunctionBlock fb -> fb.Position
            | CompareContact cc -> cc.Position
            | Timer t -> t.Position
            | Counter c -> c.Position
            | MathBlock m -> m.Position

        // 검증
        member this.Validate() =
            match this with
            | Contact c when System.String.IsNullOrWhiteSpace(c.VarName) ->
                Error "Contact variable name cannot be empty"
            | Timer t when System.String.IsNullOrWhiteSpace(t.Instance) ->
                Error "Timer instance name cannot be empty"
            | Counter c when System.String.IsNullOrWhiteSpace(c.Instance) ->
                Error "Counter instance name cannot be empty"
            | _ -> Ok ()

    /// Active Patterns for LdElement
    module LdElementPatterns =
        /// Element를 역할별로 분류
        let (|InputElement|OutputElement|LogicElement|) element =
            match element with
            | Contact _ | CompareContact _ -> InputElement
            | Coil _ -> OutputElement
            | FunctionBlock _ | Timer _ | Counter _ | MathBlock _ -> LogicElement

        /// Element를 복잡도별로 분류
        let (|SimpleElement|ComplexElement|) element =
            match element with
            | Contact _ | Coil _ -> SimpleElement
            | _ -> ComplexElement

    /// LD Rung (래더의 한 줄)
    type LdRung = {
        Id: string
        Elements: LdElement list
        Wires: Wire list
        Comment: string option
    }

    /// LD Diagram
    type LdDiagram = {
        Rungs: LdRung list
    }

    /// POU Body (언어별 구현)
    type PouBody =
        | LD of LdDiagram
        | FunctionBlock of FunctionBlockBody
        | Function of FunctionBody
        | ST of code: string  // Structured Text (확장용)
        | SFC of json: string  // SFC는 복잡하므로 일단 JSON 문자열로 (확장용)
        | IL of code: string  // Instruction List (확장용)

    /// Variable declaration in POU interface
    type InterfaceVar = {
        Name: string
        VarType: string
        Init: InitValue option
    }

    /// POU Interface
    type PouInterface = {
        InputVars: InterfaceVar list
        OutputVars: InterfaceVar list
        InOutVars: InterfaceVar list
        LocalVars: InterfaceVar list
    }

    /// POU Annotations
    type PouAnnotations = {
        SafetyRelevant: bool
        Category: string option
        Custom: Map<string, MetaValue> option
    }

    /// POU (Program Organization Unit)
    type POU = {
        Name: string
        Kind: PouType
        Language: PouLanguage
        Interface: PouInterface
        Body: PouBody
        Annotations: PouAnnotations option
    }

    // ============================================================================
    // Helper Functions
    // ============================================================================

    /// LD Helper 함수들
    module LdHelper =

        /// Contact 생성
        let contact varName contactType =
            Contact {
                Id = System.Guid.NewGuid().ToString()
                ContactType = contactType
                VarName = varName
                Position = None
            }

        /// Normal Open Contact 생성
        let normalOpen varName = contact varName NormalOpen

        /// Normal Close Contact 생성
        let normalClose varName = contact varName NormalClose

        /// Coil 생성
        let coil varName coilType =
            Coil {
                Id = System.Guid.NewGuid().ToString()
                CoilType = coilType
                VarName = varName
                Position = None
            }

        /// Normal Coil 생성
        let normalCoil varName = coil varName Normal

        /// Set Coil 생성
        let setCoil varName = coil varName Set

        /// Reset Coil 생성
        let resetCoil varName = coil varName Reset

        /// 비교 Contact 생성
        let compare op operand1 operand2 =
            CompareContact {
                Id = System.Guid.NewGuid().ToString()
                Operator = op
                Operand1 = operand1
                Operand2 = operand2
                Position = None
            }

        /// Timer 생성
        let timer timerType instance pt =
            Timer {
                Id = System.Guid.NewGuid().ToString()
                TimerType = timerType
                Instance = instance
                PT = pt
                Position = None
            }

        /// TON Timer 생성
        let ton instance pt = timer TON instance pt

        /// Counter 생성
        let counter counterType instance pv =
            Counter {
                Id = System.Guid.NewGuid().ToString()
                CounterType = counterType
                Instance = instance
                PV = pv
                Position = None
            }

        /// CTU Counter 생성
        let ctu instance pv = counter CTU instance pv

        /// Math Block 생성
        let math operation inputs output =
            MathBlock {
                Id = System.Guid.NewGuid().ToString()
                Operation = operation
                Inputs = inputs
                Output = output
                Position = None
            }

        /// Rung 생성
        let createRung elements comment =
            {
                Id = System.Guid.NewGuid().ToString()
                Elements = elements
                Wires = []
                Comment = comment
            }

    /// Function Block Helper 함수들
    module FunctionBlockHelper =

        /// FB Node 생성
        let fbNode fbType =
            {
                Id = System.Guid.NewGuid().ToString()
                NodeType = FB fbType
                Position = None
            }

        /// Variable Node 생성
        let varNode varName =
            {
                Id = System.Guid.NewGuid().ToString()
                NodeType = VAR varName
                Position = None
            }

        /// Const Node 생성
        let constNode value =
            {
                Id = System.Guid.NewGuid().ToString()
                NodeType = CONST value
                Position = None
            }

        /// Operator Node 생성
        let opNode operator =
            {
                Id = System.Guid.NewGuid().ToString()
                NodeType = OP operator
                Position = None
            }

        /// Wire 생성
        let wire fromNode fromPort toNode toPort =
            {
                From = { Node = fromNode; Port = fromPort }
                To = { Node = toNode; Port = toPort }
            }

        /// Function Block Body 생성
        let createBody nodes wires =
            {
                Diagram = {
                    Nodes = nodes
                    Wires = wires
                }
            }

    /// Function Helper 함수들
    module FunctionHelper =

        let literal value = Literal value

        let variable name = Variable name

        let call name arguments = Call(name, arguments)

        let unary operator operand = Unary(operator, operand)

        let binary left operator right = Binary(left, operator, right)

        let assignment target expression = Assignment(target, expression)

        let invoke name arguments = Invoke(name, arguments)

        let returnValue expression = Return(Some expression)

        let returnVoid = Return None

        let implementationFromExpression expression = Expression expression

        let implementationFromStatements statements = Statements statements

        let create returnType returnVariable implementation =
            {
                ReturnType = returnType
                ReturnVariable = returnVariable
                Implementation = implementation
            }
