# Loop Infrastructure - Quick Reference

## 🚀 Quick Start

### Import Required Modules

```fsharp
open Ev2.Cpu.Core
open Ev2.Cpu.Generation.Loops
open Ev2.Cpu.Generation.Loops.ArrayPatterns
open Ev2.Cpu.Generation.Loops.SequencePatterns
```

---

## 📝 FOR Loops

### Basic Patterns

```fsharp
// Range: 0 to n-1
LoopBuilder.forRange "i" 10 body

// Custom range: start to end
LoopBuilder.forFromTo "i" 5 15 body

// With step
LoopBuilder.forFromToStep "i" 0 20 2 body

// Countdown
LoopBuilder.forCountdown "i" 10 body
```

### Fluent API

```fsharp
LoopBuilder.forLoop "i"
    |> _.From(0)
    |> _.To(99)
    |> _.Step(2)
    |> _.Do(body)
    |> _.Build()
```

---

## 🔄 WHILE Loops

### Basic Patterns

```fsharp
// Simple WHILE
LoopBuilder.whileSimple condition body

// With max iterations
LoopBuilder.whileWithLimit condition 1000 body
```

### Fluent API

```fsharp
LoopBuilder.whileLoop condition
    |> _.Do(body)
    |> _.WithMaxIterations(1000)
    |> _.Build()
```

---

## 📊 Array Operations (Quick Reference)

| Operation | Function | Example |
|-----------|----------|---------|
| **Fill** | `fillArray` | `fillArray "temps" 24 (Const(0.0))` |
| **Sequential** | `fillSequential` | `fillSequential "indices" 10` |
| **Copy** | `copyArray` | `copyArray "src" "dest" 100` |
| **Find** | `findValue` | `findValue "data" 100 searchVal "idx"` |
| **Count** | `countMatches` | `countMatches "temps" 24 condition "cnt"` |
| **Map** | `mapArray` | `mapArray "in" "out" 100 transform` |
| **Scale** | `scaleArray` | `scaleArray "data" 100 2.5` |
| **Sum** | `sumArray` | `sumArray "vals" 100 "total"` |
| **Average** | `averageArray` | `averageArray "temps" 24 "avg"` |
| **Max** | `maxArray` | `maxArray "data" 100 "max"` |
| **Min** | `minArray` | `minArray "data" 100 "min"` |

---

## 🎛️ Sequence Control (Quick Reference)

| Pattern | Function | Example |
|---------|----------|---------|
| **Wait Until** | `waitUntil` | `waitUntil condition 1000 "timeout"` |
| **Poll** | `pollWithDelay` | `pollWithDelay condition 10 "success"` |
| **Retry** | `retryOperation` | `retryOperation action check 5 "ok"` |
| **Sequential Start** | `sequentialStart` | `sequentialStart motors 50` |
| **Sequential Stop** | `sequentialStop` | `sequentialStop motors 50` |
| **Interlock Check** | `checkInterlocks` | `checkInterlocks locks "allOK"` |
| **Step Sequence** | `stepSequence` | `stepSequence 10 "step"` |
| **Batch Process** | `batchProcess` | `batchProcess 100 10 action` |
| **Priority Scan** | `priorityScan` | `priorityScan 10 cond "idx"` |
| **Round Robin** | `roundRobinScan` | `roundRobinScan 8 "last" "next"` |

---

## ⚙️ Configuration & Safety

### Default Safety Limits

```fsharp
let loopCtx = LoopContextManager.getDefault()

loopCtx.MaxStackDepth <- 10            // Max nesting depth
loopCtx.DefaultMaxIterations <- 10000  // Max iterations per loop
loopCtx.GlobalTimeout <- 5000          // Timeout in milliseconds
```

### Variable Declaration (REQUIRED!)

```fsharp
// Always declare variables before use
ctx.Memory.DeclareLocal("i", DsDataType.TInt)
ctx.Memory.DeclareLocal("sum", DsDataType.TDouble)
ctx.Memory.DeclareLocal("data", DsDataType.TDouble)
```

---

## 🔍 Loop Analysis

```fsharp
// Get nesting depth
let depth = LoopAnalysis.loopDepth stmt

// Count statements
let count = LoopAnalysis.bodyStatementCount stmt

// Check for BREAK
let hasBreak = LoopAnalysis.containsBreak stmt

// Estimate iterations (for constant ranges)
let maxIter = LoopAnalysis.estimateMaxIterations stmt
```

---

## 🚨 Common Pitfalls

### ❌ Forgetting Variable Declaration

```fsharp
// ERROR: Will fail at runtime
let loop = LoopBuilder.forRange "i" 10 body
StmtEvaluator.exec ctx loop
```

### ✅ Correct

```fsharp
ctx.Memory.DeclareLocal("i", DsDataType.TInt)
let loop = LoopBuilder.forRange "i" 10 body
StmtEvaluator.exec ctx loop
```

---

## 📖 IEC 61131-3 ST Code Generation

```fsharp
// FOR loop → ST
FOR i := 0 TO 9 BY 1 DO
    (* body *)
END_FOR;

// WHILE loop → ST
WHILE condition DO (* max iterations: 100 *)
    (* body *)
END_WHILE;

// BREAK → ST
EXIT;
```

---

## 🎯 Common Use Cases

### Temperature Array Processing

```fsharp
let! avgTemp = averageArray "temps" 24 "avgTemp"
let! maxTemp = maxArray "temps" 24 "maxTemp"
let! hotCount = countMatches "temps" 24 (fun v -> v > 80.0) "hot"
```

### Motor Sequential Start

```fsharp
let! interlockOK = checkInterlocks ["eStop"; "door"; "press"] "ok"
let motorStart = sequentialStart ["m1"; "m2"; "m3"] 50
```

### Retry with Timeout

```fsharp
let retryStmts = retryOperation openValveAction checkOpen 5 "opened"
```

---

## 📚 Full Documentation

For complete API documentation, examples, and architecture details, see:
**[LoopInfrastructure.md](./LoopInfrastructure.md)**

---

**Version**: 1.0 | **Updated**: 2025-10-27
