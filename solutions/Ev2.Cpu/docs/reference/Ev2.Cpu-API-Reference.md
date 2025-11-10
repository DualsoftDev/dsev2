# Ev2.Cpu API Reference

**Version:** 1.0
**Last Updated:** 2025-10-23
**Target Audience:** 3rd Party Developers, Integration Engineers

---

## Table of Contents

1. [Overview](#overview)
2. [Quick Reference](#quick-reference)
3. [Architecture](#architecture)
4. [Core API](#core-api)
5. [Runtime API](#runtime-api)
6. [Parsing API](#parsing-api)
7. [Code Generation API](#code-generation-api)
8. [Quick Start](#quick-start)
9. [Advanced Topics](#advanced-topics)

---

## Overview

### What is Ev2.Cpu?

Ev2.Cpu is a high-performance PLC (Programmable Logic Controller) runtime engine that provides:

- **Expression Evaluation**: Evaluate complex mathematical and logical expressions
- **Statement Execution**: Execute PLC-style programming statements
- **Memory Management**: Efficient variable storage with retain memory support
- **Real-time Execution**: Cyclic scan-based execution model
- **User-Defined Functions**: Extensible function library
- **Code Generation**: Generate runtime-optimized code from AST

### Key Features

- ✅ **Type-Safe**: F# type system ensures correctness
- ✅ **High Performance**: Optimized execution engine
- ✅ **Retain Memory**: Persist variable values across power cycles
- ✅ **Extensible**: User-defined functions and types
- ✅ **Standards-Compliant**: Based on IEC 61131-3
- ✅ **Cross-Platform**: Runs on .NET 8.0+

---

## Quick Reference

Need the highlights without scrolling? Start here and jump to the detailed sections when you need more depth.

### Installation Snippet

```bash
dotnet add package Ev2.Cpu.Core
dotnet add package Ev2.Cpu.Runtime
dotnet add package Ev2.Cpu.Generation
```

### Minimal Usage Path

1. Create an execution context: `let ctx = Context.create ()`
2. Declare memory as needed: `ctx.Memory.DeclareLocal("Counter", DsDataType.TInt)`
3. Build statements using `Statement.Assign` / `Expression.*`
4. Run one scan: `StmtEvaluator.execScan ctx stmts`
5. Inspect results via `ctx.Memory.Get`

### Common Helpers

| Task | API | Notes |
|------|-----|-------|
| Create tags | `DsTag.Bool/Int/Double/String` | Auto-registers on first use |
| Make constants | `Expression.Const(box value, DsDataType.TInt)` | DS expressions respect declared types |
| Evaluate expressions | `ExprEvaluator.eval ctx expr` | Returns boxed value |
| Enqueue runtime update | `RuntimeUpdateManager.EnqueueUpdate` | Uses `UpdateRequest` DU |
| Retain snapshot | `ctx.Memory.CreateRetainSnapshot()` | Pair with `BinaryRetainStorage` |

### Runtime Checklist

- ⏱️ Configure `ScanConfig` (cycle time, selective mode) before starting the engine.
- 🧠 Reset or clear `DsTagRegistry` when generating DSL code dynamically.
- ♻️ Register retain storage (`BinaryRetainStorage`) if persistence is required.
- 🔄 Use `UpdateRequest.BatchUpdate` for atomic hot reloads.

---

## Architecture

See the dedicated [Architecture overview](../concepts/ARCHITECTURE.md) for the full component map, dependency graph, and refactoring notes. The remainder of this reference focuses on the surface APIs.

---

## Core API

### Namespace: `Ev2.Cpu.Core`

#### Data Types

```fsharp
// Supported data types
type DsDataType =
    | TBool                    // Boolean
    | TInt                     // Integer
    | TDouble                  // Double-precision float
    | TString                  // String
    | TArray of DsDataType     // Array
    | TStruct of name:string   // Struct/UDT
```

**Usage:**
```fsharp
open Ev2.Cpu.Core

let intType = DsDataType.TInt
let boolType = DsDataType.TBool
let arrayType = DsDataType.TArray(TInt)
```

#### Tags (Variables)

Tags represent variables in the PLC system.

```fsharp
// Create a tag
let tag = DsTag.Create("MotorSpeed", DsDataType.TInt)

// Tag builders for common types
let boolTag = DsTag.Bool("StartButton")
let intTag = DsTag.Int("Counter")
let doubleTag = DsTag.Double("Temperature")
let stringTag = DsTag.String("Status")
```

**Tag Registry:**
```fsharp
open Ev2.Cpu.Core

// Register a tag
DsTagRegistry.register tag |> ignore

// Find a tag
match DsTagRegistry.tryFind "MotorSpeed" with
| Some tag -> printfn "Found: %s" tag.Name
| None -> printfn "Not found"

// Get all tags
let allTags = DsTagRegistry.all()

// Clear registry
DsTagRegistry.clear()
```

#### Operators

```fsharp
// Arithmetic operators
DsOp.Add        // +
DsOp.Sub        // -
DsOp.Mul        // *
DsOp.Div        // /
DsOp.Mod        // %

// Comparison operators
DsOp.Equal      // =
DsOp.NotEqual   // <>
DsOp.LessThan   // <
DsOp.LessOrEq   // <=
DsOp.Greater    // >
DsOp.GreaterEq  // >=

// Logical operators
DsOp.And        // AND
DsOp.Or         // OR
DsOp.Not        // NOT
DsOp.Xor        // XOR
```

#### Expressions

```fsharp
open Ev2.Cpu.Core

// Constant expression
let constExpr = Expression.Const(box 42, DsDataType.TInt)

// Terminal (variable) expression
let tag = DsTag.Int("Counter")
let termExpr = Expression.Terminal(tag)

// Binary expression: Counter + 10
let addExpr = Expression.Binary(
    DsOp.Add,
    Expression.Terminal(tag),
    Expression.Const(box 10, DsDataType.TInt)
)

// Function call: ABS(-5)
let funcExpr = Expression.Function(
    "ABS",
    [Expression.Const(box -5, DsDataType.TInt)]
)
```

---

## Runtime API

### Namespace: `Ev2.Cpu.Runtime`

#### Execution Context

The execution context manages runtime state.

```fsharp
open Ev2.Cpu.Runtime

// Create context
let ctx = Context.create()

// Declare variables
ctx.Memory.DeclareLocal("Counter", DsDataType.TInt, retain=false)
ctx.Memory.DeclareLocal("Status", DsDataType.TBool, retain=true)

// Set values
ctx.Memory.Set("Counter", box 100)
ctx.Memory.Set("Status", box true)

// Get values
let counter = ctx.Memory.Get("Counter") :?> int
let status = ctx.Memory.Get("Status") :?> bool

// Check if variable exists
if ctx.Memory.Exists("Counter") then
    printfn "Counter exists"
```

#### Scan Engine

The core execution engine using cyclic scanning.

```fsharp
open Ev2.Cpu.Core
open Ev2.Cpu.Runtime

// Create a simple program
let program = {
    Body = [
        // Counter := Counter + 1
        Statement.Assign(
            DsTag.Int("Counter"),
            Expression.Binary(
                DsOp.Add,
                Expression.Terminal(DsTag.Int("Counter")),
                Expression.Const(box 1, DsDataType.TInt)
            )
        )
    ]
}

// Create context and declare variables
let ctx = Context.create()
ctx.Memory.DeclareLocal("Counter", DsDataType.TInt)
ctx.Memory.Set("Counter", box 0)

// Create scan engine
let engine = CpuScanEngine(program, ctx, None, None, None)

// Execute one scan
let elapsedMs = engine.ScanOnce()
printfn "Scan completed in %d ms" elapsedMs

// Execute continuous scanning (async)
async {
    do! engine.StartAsync()
    do! Async.Sleep(5000)  // Run for 5 seconds
    do! engine.StopAsync()
} |> Async.RunSynchronously
```

#### Scan Configuration

```fsharp
// Configure scan engine
let config = {
    ScanConfig.Default with
        MaxScanTime = 100<ms>      // Max scan time
        SelectiveMode = true       // Enable selective scanning
        ProfilingEnabled = false   // Disable profiling
}

let engine = CpuScanEngine(program, ctx, Some config, None, None)
```

#### Retain Memory

Persist variable values across power cycles.

```fsharp
open Ev2.Cpu.Runtime

// Create retain storage
let storage = BinaryRetainStorage("plc_retain.dat")

// Declare retain variables
ctx.Memory.DeclareLocal("PersistentCounter", DsDataType.TInt, retain=true)
ctx.Memory.DeclareLocal("TempValue", DsDataType.TInt, retain=false)

// Create engine with retain storage
let engine = CpuScanEngine(program, ctx, None, None, Some storage)

// On stop, values are automatically saved
do! engine.StopAsync()

// On start, values are automatically restored
let engine2 = CpuScanEngine(program, ctx, None, None, Some storage)
```

#### Memory Operations

```fsharp
// Snapshot memory
let snapshot = ctx.Memory.Snapshot()

// Restore from snapshot
ctx.Memory.Restore(snapshot)

// Get memory statistics
let stats = ctx.Memory.GetStats()
printfn "Total variables: %d" stats.TotalVariables
printfn "Memory used: %d bytes" stats.MemoryUsed

// Clear all variables
ctx.Memory.Clear()
```

---

## Parsing API

### Namespace: `Ev2.Cpu.Parsing`

#### Lexer

Tokenize source code.

```fsharp
open Ev2.Cpu.Parsing

let source = "Counter := Counter + 1"

// Tokenize
match Lexer.tokenize source with
| Ok tokens ->
    for token in tokens do
        printfn "%A" token
| Error error ->
    printfn "Lexer error: %s" (error.Format())
```

#### Parser

Parse source code into AST.

```fsharp
open Ev2.Cpu.Parsing

let source = """
    Counter := Counter + 1;
    IF Counter > 100 THEN
        Counter := 0;
    END_IF
"""

// Parse
match Parser.parse source with
| Ok program ->
    printfn "Parsed %d statements" program.Body.Length
| Error error ->
    printfn "Parse error: %s" (error.Format())
```

#### AST Conversion

Convert AST to runtime expressions.

```fsharp
open Ev2.Cpu.Ast

// AST expression
let astExpr = EVar("Counter", DsDataType.TInt)

// Convert to runtime expression
match AstConverter.toRuntimeExpr astExpr with
| Ok runtimeExpr ->
    printfn "Converted successfully"
| Error error ->
    printfn "Conversion error: %s" (error.Format())

// Convert back to AST
let astExpr2 = AstConverter.fromRuntimeExpr runtimeExpr
```

---

## Code Generation API

### Namespace: `Ev2.Cpu.Core.UserDefined`

#### User-Defined Function Blocks

```fsharp
open Ev2.Cpu.Core.UserDefined

// Define input/output parameters
let inputs = [
    UserParameter.Create("Enable", DsDataType.TBool)
    UserParameter.Create("SetPoint", DsDataType.TInt)
]

let outputs = [
    UserParameter.Create("Output", DsDataType.TInt)
    UserParameter.Create("Done", DsDataType.TBool)
]

// Define function block
let fb = UserFunctionBlock.Create(
    name = "MyController",
    inputs = inputs,
    outputs = outputs,
    body = [
        // IF Enable THEN Output := SetPoint; Done := TRUE; END_IF
        UserStmt.IfThen(
            UserExpr.Var("Enable"),
            [
                UserStmt.Assign("Output", UserExpr.Var("SetPoint"))
                UserStmt.Assign("Done", UserExpr.Const(box true, DsDataType.TBool))
            ],
            []
        )
    ]
)

// Register function block
match GlobalUserLibrary.registerFB fb with
| Ok () -> printfn "Function block registered"
| Error error -> printfn "Registration error: %A" error

// Use function block
let instance = UserFBInstance.Create("Controller1", "MyController")
```

---

## Quick Start

### Example 1: Simple Counter

```fsharp
open Ev2.Cpu.Core
open Ev2.Cpu.Runtime

// 1. Create context
let ctx = Context.create()

// 2. Declare variable
ctx.Memory.DeclareLocal("Counter", DsDataType.TInt)
ctx.Memory.Set("Counter", box 0)

// 3. Create program
let program = {
    Body = [
        Statement.Assign(
            DsTag.Int("Counter"),
            Expression.Binary(
                DsOp.Add,
                Expression.Terminal(DsTag.Int("Counter")),
                Expression.Const(box 1, DsDataType.TInt)
            )
        )
    ]
}

// 4. Create and run engine
let engine = CpuScanEngine(program, ctx, None, None, None)

for i in 1..10 do
    engine.ScanOnce() |> ignore
    let value = ctx.Memory.Get("Counter") :?> int
    printfn "Counter = %d" value
```

### Example 2: Traffic Light Controller

```fsharp
open Ev2.Cpu.Core
open Ev2.Cpu.Runtime

let ctx = Context.create()

// Declare variables
ctx.Memory.DeclareLocal("RedLight", DsDataType.TBool)
ctx.Memory.DeclareLocal("YellowLight", DsDataType.TBool)
ctx.Memory.DeclareLocal("GreenLight", DsDataType.TBool)
ctx.Memory.DeclareLocal("State", DsDataType.TInt)
ctx.Memory.DeclareLocal("Timer", DsDataType.TInt)

// Initialize
ctx.Memory.Set("State", box 0)
ctx.Memory.Set("Timer", box 0)

// Program logic
let program = {
    Body = [
        // Timer := Timer + 1
        Statement.Assign(
            DsTag.Int("Timer"),
            Expression.Binary(DsOp.Add,
                Expression.Terminal(DsTag.Int("Timer")),
                Expression.Const(box 1, DsDataType.TInt))
        )

        // State machine for traffic light
        // ... (simplified for brevity)
    ]
}

let engine = CpuScanEngine(program, ctx, None, None, None)
```

### Example 3: Using Parser

```fsharp
open Ev2.Cpu.Parsing
open Ev2.Cpu.Runtime

let source = """
    Counter := 0;
    WHILE Counter < 10 DO
        Counter := Counter + 1;
    END_WHILE
"""

// Parse source code
match Parser.parse source with
| Ok astProgram ->
    // Convert to runtime program
    let runtimeProgram = { Body = astProgram.Body }

    // Execute
    let ctx = Context.create()
    ctx.Memory.DeclareLocal("Counter", DsDataType.TInt)

    let engine = CpuScanEngine(runtimeProgram, ctx, None, None, None)
    engine.ScanOnce() |> ignore

| Error error ->
    printfn "Parse error: %s" (error.Format())
```

---

## Advanced Topics

### Performance Optimization

```fsharp
// Enable selective scanning (only re-evaluate changed dependencies)
let config = { ScanConfig.Default with SelectiveMode = true }

// Disable profiling in production
let config = { config with ProfilingEnabled = false }

// Set maximum scan time
let config = { config with MaxScanTime = 50<ms> }
```

### Error Handling

```fsharp
// All operations return Result<T, Error>
match engine.ScanOnce() with
| elapsed when elapsed > 0 ->
    printfn "Scan completed in %d ms" elapsed
| _ ->
    printfn "Scan failed or timed out"

// Parser errors provide detailed information
match Parser.parse source with
| Ok program -> ()
| Error error ->
    printfn "Error at line %d, column %d" error.Line error.Column
    printfn "Message: %s" error.Message
```

### Memory Management

```fsharp
// Use 'use' for automatic disposal
use engine = new CpuScanEngine(program, ctx, None, None, None)
// Engine is automatically disposed when out of scope

// Manual cleanup
ctx.Memory.Clear()
DsTagRegistry.clear()
```

### Thread Safety

```fsharp
// The engine is thread-safe for concurrent reads
// Writes should be synchronized

let lockObj = obj()

async {
    lock lockObj (fun () ->
        ctx.Memory.Set("Counter", box 42)
    )
}
```

---

## API Stability

### Public API

The following APIs are **stable** and will maintain backward compatibility:

- ✅ `Ev2.Cpu.Core.DsDataType`
- ✅ `Ev2.Cpu.Core.DsTag`
- ✅ `Ev2.Cpu.Core.DsTagRegistry`
- ✅ `Ev2.Cpu.Core.Expression`
- ✅ `Ev2.Cpu.Core.Statement`
- ✅ `Ev2.Cpu.Runtime.Context`
- ✅ `Ev2.Cpu.Runtime.CpuScanEngine`
- ✅ `Ev2.Cpu.Runtime.ScanConfig`
- ✅ `Ev2.Cpu.Runtime.IRetainStorage`
- ✅ `Ev2.Cpu.Parsing.Parser`
- ✅ `Ev2.Cpu.Parsing.Lexer`

### Internal API

Internal APIs (marked with `internal` or in `Internal` namespaces) may change without notice.

---

## Support

For issues, questions, or contributions:

- **Documentation**: `/docs` directory
- **Examples**: `/src/cpu/Ev2.Cpu.Runtime/Examples`
- **Tests**: `/src/UintTest/cpu`

---

**End of API Reference**
