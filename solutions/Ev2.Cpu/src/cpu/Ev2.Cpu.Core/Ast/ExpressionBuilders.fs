namespace Ev2.Cpu.Core

/// 표현식 빌더 모듈
module ExpressionBuilder =
    
    /// 상수 생성
    let constant value dtype = Constant(value, dtype)
    let boolConst b = Constant(box b, typeof<bool>)
    let intConst i = Constant(box i, typeof<int>)
    let doubleConst d = Constant(box d, typeof<double>)
    let stringConst s = Constant(box s, typeof<string>)
    
    /// 변수 참조 생성
    let variable tag = Variable(tag)
    let var name dtype = Variable(DsTag.Create(name, dtype))
    
    /// 연산자 표현식 생성
    let unary op expr = UnaryOp(op, expr)
    let binary op left right = BinaryOp(op, left, right)
    
    /// 함수 호출 생성
    let call funcName args = FunctionCall(funcName, args)
    
    /// 조건 표현식 생성
    let conditional cond thenExpr elseExpr = Conditional(cond, thenExpr, elseExpr)
    
    /// 연산자 단축키들 - 논리
    let (!!.) expr = unary Not expr
    let (&&.) left right = binary And left right
    let (||.) left right = binary Or left right
    
    /// 비교 연산자
    let (==.) left right = binary Eq left right
    let (<>.) left right = binary Ne left right
    let (>>.) left right = binary Gt left right
    let (>=.) left right = binary Ge left right
    let (<<.) left right = binary Lt left right
    let (<=.) left right = binary Le left right
    
    /// 산술 연산자
    let (.+.) left right = binary Add left right
    let (.-.) left right = binary Sub left right
    let (.*.) left right = binary Mul left right
    let (./.) left right = binary Div left right
    let (.%.) left right = binary Mod left right
    let (.^.) left right = binary Pow left right
    
    /// 비트 연산자
    let (.&.) left right = binary BitAnd left right
    let (.|.) left right = binary BitOr left right
    let (.^^.) left right = binary BitXor left right
    let (.<<.) left right = binary ShiftLeft left right
    let (.>>.) left right = binary ShiftRight left right
    
    /// 신호 연산자
    let rising expr = unary Rising expr
    let falling expr = unary Falling expr
    let edge expr = unary Edge expr
    
    /// 특수 연산자
    let assign left right = binary Assign left right
    let coalesce left right = binary Coalesce left right
