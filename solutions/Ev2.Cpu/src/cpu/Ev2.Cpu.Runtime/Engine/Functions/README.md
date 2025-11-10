# Functions Layer

## 현재 상태

현재 모든 내장 함수는 `Engine/BuiltinFunctions.fs` (370줄)에 모듈 형태로 잘 구조화되어 있습니다:

```
BuiltinFunctions.fs
├── Comparison       - 비교 연산 (eq, lt, gt, le, ge, ne)
├── Arithmetic       - 산술 연산 (add, sub, mul, divide, modulo, power)
├── MathFunctions    - 수학 함수 (abs, neg, sqrt, round, floor, ceiling, clamp)
├── StringFunctions  - 문자열 함수 (concat, length, substring, trim, replace 등)
├── PLCFunctions     - PLC 전용 함수 (limit, select, mux 등)
└── SystemFunctions  - 시스템 함수 (random, timestamp, now 등)
```

## 향후 리팩토링 계획

파일이 370줄로 관리 가능한 수준이며, 이미 모듈로 잘 나뉘어 있어 현재는 분리하지 않습니다.

파일 분리가 필요한 경우 (500줄 초과 시):
1. 각 모듈을 별도 파일로 분리
2. `FunctionCommon.fs` - 공용 유틸리티 (eps, validateArgCount 등)
3. `ComparisonFunctions.fs` - Comparison 모듈
4. `ArithmeticFunctions.fs` - Arithmetic 모듈
5. `MathFunctions.fs` - MathFunctions 모듈
6. `StringFunctions.fs` - StringFunctions 모듈
7. `PLCFunctions.fs` - PLCFunctions 모듈
8. `SystemFunctions.fs` - SystemFunctions 모듈
9. `BuiltinFunctionRegistry.fs` - 함수 디스패처

## 사용법

```fsharp
open Ev2.Cpu.Runtime

// 비교
let result = BuiltinFunctions.Comparison.eq (box 1) (box 1)  // true

// 산술
let sum = BuiltinFunctions.Arithmetic.add (box 10) (box 20)  // box 30

// 수학
let abs = BuiltinFunctions.MathFunctions.abs (box -5)  // box 5

// 문자열
let upper = BuiltinFunctions.StringFunctions.toUpper (box "hello")  // box "HELLO"
```
