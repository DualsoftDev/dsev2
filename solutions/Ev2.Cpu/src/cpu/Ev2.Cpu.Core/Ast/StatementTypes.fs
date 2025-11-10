namespace Ev2.Cpu.Core

/// 문장 AST
[<StructuralEquality; NoComparison>]
type Stmt =
    | Assignment of DsTag * Expr                    // 대입 문
    | Conditional of Expr * Stmt list * Stmt list  // 조건문 (if-then-else)
    | Loop of Expr * Stmt list                     // 루프문
    | Block of Stmt list                           // 블록문
    | FunctionCall of string * Expr list           // 함수 호출 문
    | Return of Expr option                        // 반환문
    | Break                                        // 브레이크문
    | Continue                                     // 컨티뉴문
    | Comment of string                            // 코멘트
    | Empty                                        // 빈 문장
    
    // 시스템 문장들
    | TimerCall of string * Expr * Expr            // 타이머 호출 (name, preset, enable)
    | CounterCall of string * Expr * Expr * Expr   // 카운터 호출 (name, preset, count, reset/load)
    | SystemCall of string * Expr list            // 시스템 호출