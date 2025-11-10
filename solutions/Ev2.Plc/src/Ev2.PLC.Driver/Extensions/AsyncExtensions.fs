namespace Ev2.PLC.Driver.Extensions

open System
open System.Threading
open System.Threading.Tasks

/// 비동기 작업 확장 모듈
module AsyncExtensions =
    
    /// 타임아웃과 함께 Task 실행
    let withTimeout (timeoutMs: int) (task: Task<'T>) : Task<'T option> =
        let tcs = TaskCompletionSource<'T option>()
        let timeoutCts = new CancellationTokenSource(timeoutMs)
        
        task.ContinueWith(fun (t: Task<'T>) ->
            if not timeoutCts.Token.IsCancellationRequested then
                if t.IsCompleted && not t.IsFaulted && not t.IsCanceled then
                    tcs.TrySetResult(Some t.Result) |> ignore
                else
                    tcs.TrySetResult(None) |> ignore
        ) |> ignore
        
        timeoutCts.Token.Register(fun _ ->
            tcs.TrySetResult(None) |> ignore
        ) |> ignore
        
        tcs.Task
    
    /// 여러 Task를 병렬로 실행하고 모든 결과 수집
    let runParallel (tasks: Task<'T> list) : Task<'T list> =
        Task.WhenAll(tasks).ContinueWith(fun (t: Task<'T[]>) -> 
            Array.toList t.Result
        )
    
    /// Task를 결과 타입으로 변환 (예외를 Result로 변환)
    let toResult (task: Task<'T>) : Task<Result<'T, exn>> =
        task.ContinueWith(fun (t: Task<'T>) ->
            if t.IsCompleted && not t.IsFaulted && not t.IsCanceled then
                Ok t.Result
            elif t.IsFaulted then
                Error (if t.Exception.InnerException <> null then t.Exception.InnerException else t.Exception :> exn)
            else
                Error (OperationCanceledException() :> exn)
        )

/// Task 확장 메서드
[<AutoOpen>]
module TaskExtensions =
    
    type Task<'T> with
        /// 타임아웃과 함께 실행
        member this.WithTimeout(timeoutMs: int) =
            AsyncExtensions.withTimeout timeoutMs this
        
        /// 결과 타입으로 변환
        member this.ToResult() =
            AsyncExtensions.toResult this
    
    type Task with
        /// Unit Task를 타임아웃과 함께 실행
        member this.WithTimeout(timeoutMs: int) =
            let tcs = TaskCompletionSource<bool>()
            let timeoutCts = new CancellationTokenSource(timeoutMs)
            
            this.ContinueWith(fun (t: Task) ->
                if not timeoutCts.Token.IsCancellationRequested then
                    tcs.TrySetResult(t.IsCompleted && not t.IsFaulted && not t.IsCanceled) |> ignore
            ) |> ignore
            
            timeoutCts.Token.Register(fun _ ->
                tcs.TrySetResult(false) |> ignore
            ) |> ignore
            
            tcs.Task

/// 비동기 유틸리티 클래스
type AsyncUtils =
    
    /// 안전한 비동기 실행 (예외를 로깅하고 기본값 반환)
    static member SafeExecuteAsync<'T>(operation: unit -> Task<'T>, defaultValue: 'T, ?onError: exn -> unit) : Task<'T> =
        try
            operation().ContinueWith(fun (t: Task<'T>) ->
                if t.IsCompleted && not t.IsFaulted && not t.IsCanceled then
                    t.Result
                else
                    onError |> Option.iter (fun handler -> 
                        if t.Exception <> null then 
                            let ex = if t.Exception.InnerException <> null then t.Exception.InnerException else t.Exception :> exn
                            handler ex)
                    defaultValue
            )
        with
        | ex ->
            onError |> Option.iter (fun handler -> handler ex)
            Task.FromResult(defaultValue)
    
    /// 조건부 대기
    static member WaitUntilAsync(condition: unit -> bool, checkIntervalMs: int, timeoutMs: int) : Task<bool> =
        let startTime = DateTime.UtcNow
        let tcs = TaskCompletionSource<bool>()
        
        let rec checkCondition() =
            if condition() then
                tcs.TrySetResult(true) |> ignore
            elif DateTime.UtcNow - startTime > TimeSpan.FromMilliseconds(float timeoutMs) then
                tcs.TrySetResult(false) |> ignore
            else
                Task.Delay(checkIntervalMs).ContinueWith(fun _ -> checkCondition()) |> ignore
        
        checkCondition()
        tcs.Task