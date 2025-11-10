namespace Ev2.Cpu.Runtime

open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.Text
open Ev2.Cpu.Core
open Ev2.Cpu.Core.Common

/// Phase 2.2: 메모리 풀링 시스템 (GC 압력 감소)
module MemoryPool =
    
    /// 스냅샷 객체 풀
    type SnapshotPool() =
        let pool = ConcurrentQueue<Map<string, obj>>()
        let maxPoolSize = 50
        
        member _.Rent() =
            match pool.TryDequeue() with
            | true, map -> map
            | false, _ -> Map.empty
        
        member _.Return(_ : Map<string, obj>) =
            // Note: Maps are immutable - we cannot "clear" and reuse them like mutable structures
            // This pool maintains a queue of empty maps to reduce Map.empty allocations
            // The supplied populated map is discarded (GC'd) as it cannot be reused
            if pool.Count < maxPoolSize then
                pool.Enqueue(Map.empty)
    
    /// 문자열 빌더 풀  
    type StringBuilderPool() =
        let pool = ConcurrentQueue<StringBuilder>()
        let maxPoolSize = 20
        
        member _.Rent() =
            match pool.TryDequeue() with
            | true, sb -> 
                sb.Clear() |> ignore
                sb
            | false, _ -> StringBuilder(256)
        
        member _.Return(sb: StringBuilder) =
            if pool.Count < maxPoolSize then
                pool.Enqueue(sb)
    
    /// 배열 풀 (다양한 크기)
    type ArrayPool<'T>() =
        let smallPool = ConcurrentQueue<'T[]>()  // 크기 64
        let mediumPool = ConcurrentQueue<'T[]>() // 크기 256
        let largePool = ConcurrentQueue<'T[]>()  // 크기 1024
        let maxPoolSize = 10
        
        member _.Rent(minSize: int) =
            if minSize <= 64 then
                match smallPool.TryDequeue() with
                | true, arr -> arr
                | false, _ -> Array.zeroCreate<'T> 64
            elif minSize <= 256 then
                match mediumPool.TryDequeue() with
                | true, arr -> arr
                | false, _ -> Array.zeroCreate<'T> 256
            elif minSize <= 1024 then
                match largePool.TryDequeue() with
                | true, arr -> arr
                | false, _ -> Array.zeroCreate<'T> 1024
            else
                // Oversized request - create exact-size array (not pooled)
                Array.zeroCreate<'T> minSize
        
        member _.Return(arr: 'T[]) =
            if arr.Length = 64 && smallPool.Count < maxPoolSize then
                Array.Clear(arr, 0, arr.Length)
                smallPool.Enqueue(arr)
            elif arr.Length = 256 && mediumPool.Count < maxPoolSize then
                Array.Clear(arr, 0, arr.Length)
                mediumPool.Enqueue(arr)
            elif arr.Length = 1024 && largePool.Count < maxPoolSize then
                Array.Clear(arr, 0, arr.Length)
                largePool.Enqueue(arr)
    
    /// 히스토리 엔트리 풀
    type HistoryEntryPool() =
        let pool = ConcurrentQueue<string * obj * DateTime>()
        let maxPoolSize = 100
        
        member _.Rent(name: string, value: obj, timestamp: DateTime) =
            (name, value, timestamp)
        
        member _.Return(entry: string * obj * DateTime) =
            if pool.Count < maxPoolSize then
                pool.Enqueue(("", null, DateTime.MinValue))
    
    /// 전역 풀 인스턴스들
    let snapshotPool = SnapshotPool()
    let stringBuilderPool = StringBuilderPool()
    let objArrayPool = ArrayPool<obj>()
    let stringArrayPool = ArrayPool<string>()
    let historyPool = HistoryEntryPool()
    
    /// 편의 함수들
    let rentSnapshot() = snapshotPool.Rent()
    let returnSnapshot(snapshot) = snapshotPool.Return(snapshot)
    
    let rentStringBuilder() = stringBuilderPool.Rent()
    let returnStringBuilder(sb) = stringBuilderPool.Return(sb)
    
    let rentObjArray(size) = objArrayPool.Rent(size)
    let returnObjArray(arr) = objArrayPool.Return(arr)
    
    let rentStringArray(size) = stringArrayPool.Rent(size)
    let returnStringArray(arr) = stringArrayPool.Return(arr)

/// Phase 2.2: 풀링을 사용하는 최적화된 메모리 관리자
type PooledMemory() =
    let entries = Dictionary<string, OptimizedSlot>(StringComparer.Ordinal)
    let varIndex = Dictionary<string, int>(StringComparer.Ordinal)
    let slots = Array.zeroCreate<OptimizedSlot> 2000
    let mutable nextIndex = 0
    
    // 풀링된 히스토리 (순환 버퍼)
    let maxHistory = 1000
    let historyBuffer = Array.zeroCreate<string * obj * DateTime> maxHistory
    let mutable historyHead = 0
    let mutable historyCount = 0
    
    member _.DeclareVariable(name: string, dataType: DsDataType, area: MemoryArea) =
        if nextIndex >= slots.Length then
            RuntimeExceptions.raiseMemoryLimit slots.Length
        let index = nextIndex
        nextIndex <- nextIndex + 1
        varIndex.[name] <- index
        slots.[index] <- {
            Value = dataType.DefaultValue
            LastValue = null
            Changed = false
            DsDataType = dataType
            Area = area
        }
        index
    
    member this.Get(name: string) =
        match varIndex.TryGetValue(name) with
        | true, index -> slots.[index].Value
        | false, _ -> null
    
    member this.Set(name: string, value: obj) =
        match varIndex.TryGetValue(name) with
        | true, index ->
            let slot = &slots.[index]
            if not slot.Area.IsWritable then RuntimeExceptions.raiseCannotWriteToVariable name
            
            let changed = not (Object.Equals(slot.Value, value))
            if changed then
                slot.LastValue <- slot.Value
                slot.Value <- value
                slot.Changed <- true
                
                // 풀링된 히스토리 추가
                historyBuffer.[historyHead] <- (name, value, DateTime.Now)
                historyHead <- (historyHead + 1) % maxHistory
                if historyCount < maxHistory then historyCount <- historyCount + 1
        | false, _ ->
            // HIGH FIX (DEFECT-019-5): Don't auto-declare unknown variables
            // Previous code auto-declared as Internal, contradicting Memory.Set contract (Memory.fs:424)
            // Also crashed on null value because value.GetType() was called without null check
            RuntimeExceptions.raiseVariableNotDeclared name
    
    member _.HasChanged(name: string) =
        match varIndex.TryGetValue(name) with
        | true, index -> slots.[index].Changed
        | false, _ -> false
    
    member _.ClearChangeFlags() =
        for i = 0 to nextIndex - 1 do
            slots.[i].Changed <- false
    
    member _.GetChangedVariables() =
        seq {
            for KeyValue(name, index) in varIndex do
                if slots.[index].Changed then yield name
        } |> Set.ofSeq
    
    member _.Snapshot() =
        // 풀에서 스냅샷 맵 대여
        let snapshot = MemoryPool.rentSnapshot()
        let mutable result = snapshot
        
        for KeyValue(name, index) in varIndex do
            let slot = slots.[index]
            let key = slot.Area.Prefix + name
            result <- result.Add(key, slot.Value)
        
        result
    
    member _.ReturnSnapshot(snapshot: Map<string, obj>) =
        MemoryPool.returnSnapshot(snapshot)
    
    member _.GetHistory() =
        seq {
            for i = 0 to min historyCount maxHistory - 1 do
                let actualIndex = (historyHead - historyCount + i + maxHistory) % maxHistory
                yield historyBuffer.[actualIndex]
        }
