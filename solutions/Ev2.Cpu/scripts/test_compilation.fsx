// 컴파일 테스트 스크립트
#r "nuget: System.Reactive"

// Core 타입들 로드 시도
try
    #load "src/cpu/Ev2.Cpu.Core/Core/DataTypes.fs"
    printfn "✅ Core/DataTypes.fs 로드 성공"
with ex ->
    printfn "❌ Core/DataTypes.fs 로드 실패: %s" ex.Message

try
    #load "src/cpu/Ev2.Cpu.Core/Core/Types.fs"
    printfn "✅ Core/Types.fs 로드 성공"
with ex ->
    printfn "❌ Core/Types.fs 로드 실패: %s" ex.Message

try
    #load "src/cpu/Ev2.Cpu.Core/Struct/DataTypes.fs"
    printfn "✅ Struct/DataTypes.fs 로드 성공"
with ex ->
    printfn "❌ Struct/DataTypes.fs 로드 실패: %s" ex.Message

// Runtime 모듈들 로드 시도
try
    #load "src/cpu/Ev2.Cpu.Runtime/Runtime/Memory.fs"
    printfn "✅ Runtime/Memory.fs 로드 성공"
with ex ->
    printfn "❌ Runtime/Memory.fs 로드 실패: %s" ex.Message

try
    #load "src/cpu/Ev2.Cpu.Runtime/Runtime/BuiltinFunctions.fs"
    printfn "✅ Runtime/BuiltinFunctions.fs 로드 성공"
with ex ->
    printfn "❌ Runtime/BuiltinFunctions.fs 로드 실패: %s" ex.Message

printfn "컴파일 테스트 완료"