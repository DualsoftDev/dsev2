namespace Ev2.PLC.Driver.Base

open System
open System.Threading.Tasks
open System.Collections.Concurrent
open Microsoft.Extensions.Logging
open Ev2.PLC.Common.Types
open Ev2.PLC.Common.Interfaces

/// PLC 드라이버 기본 구현 클래스
[<AbstractClass>]
type PlcDriverBase(plcId: string, vendor: PlcVendor, options: ConnectionOptions, logger: ILogger) =
    
    let connectionOptions = options
    let mutable connectionState = PlcConnectionStatus.Disconnected
    let mutable diagnostics = PlcDiagnostics.Empty
    let mutable performanceStats = PlcPerformanceStats.Empty
    let scanGroups = ConcurrentDictionary<string, ScanGroup>()
    let subscriptions = ConcurrentDictionary<string, ConcurrentDictionary<string, TagSubscription>>() // tagId -> subscriberId -> subscription
    
    let getOrCreateSubscriberMap tagId =
        subscriptions.GetOrAdd(tagId, fun _ -> ConcurrentDictionary<string, TagSubscription>())

    // 이벤트
    let connectionStateChangedEvent = Event<ConnectionStateChangedEvent>()
    let tagValueChangedEvent = Event<ScanResult>()
    let scanCompletedEvent = Event<ScanBatch>()
    let errorOccurredEvent = Event<string * exn option>()

    member this.SetDiagnostics(newDiagnostics: PlcDiagnostics) =
        diagnostics <- newDiagnostics

    member this.UpdateDiagnostics(updateFn: PlcDiagnostics -> PlcDiagnostics) =
        diagnostics <- updateFn diagnostics
    
    /// 연결 상태 업데이트
    member private this.UpdateConnectionState(newState: PlcConnectionStatus, ?message: string) =
        let oldState = connectionState
        connectionState <- newState
        
        let stateEvent = ConnectionStateChangedEvent.Create(plcId, oldState, newState, ?message = message)
        connectionStateChangedEvent.Trigger(stateEvent)
        
        logger.LogInformation("PLC {PlcId} connection state changed from {OldState} to {NewState}", 
                             plcId, oldState, newState)
    
    /// 성능 통계 업데이트
    member private this.UpdatePerformanceStats(responseTime: TimeSpan, success: bool) =
        performanceStats <-
            if success then
                PlcPerformanceStats.updateWithSuccess performanceStats responseTime
            else
                PlcPerformanceStats.updateWithFailure performanceStats
    
    /// 에러 발생 처리
    member this.OnError(message: string, ?ex: exn) =
        match ex with
        | Some e -> logger.LogError(e, "PLC {PlcId} error: {Message}", plcId, message)
        | None -> logger.LogError("PLC {PlcId} error: {Message}", plcId, message)
        errorOccurredEvent.Trigger((message, ex))
        
        match connectionState with
        | PlcConnectionStatus.Connected ->
            this.UpdateConnectionState(PlcConnectionStatus.Failure ("Runtime", message))
        | _ -> ()
    
    /// 안전한 비동기 작업 실행
    member this.SafeExecuteAsync<'T>(operation: unit -> Task<'T>, defaultValue: 'T, operationName: string) =
        task {
            try
                let startTime = DateTime.UtcNow
                let! result = operation()
                let endTime = DateTime.UtcNow
                let responseTime = endTime - startTime
                
                this.UpdatePerformanceStats(responseTime, true)
                return result
                
            with
            | ex ->
                this.UpdatePerformanceStats(TimeSpan.Zero, false)
                this.OnError($"Error during {operationName}", ex)
                return defaultValue
        }
    
    /// 태그 구독 알림 처리
    member private this.NotifySubscribers(result: ScanResult) =
        match subscriptions.TryGetValue(result.TagId) with
        | true, subscriberMap ->
            subscriberMap
            |> Seq.iter (fun kvp ->
                let subscriberId = kvp.Key
                let subscription = kvp.Value
                if subscription.ShouldUpdate(result.Value) then
                    let updatedSubscription =
                        { subscription with
                            LastValue = Some result.Value
                            LastUpdateTime = Some DateTime.UtcNow }
                    subscriberMap.[subscriberId] <- updatedSubscription
                    tagValueChangedEvent.Trigger(result))
        | _ -> ()
    
    /// 스캔 결과 처리
    member this.ProcessScanResults(results: ScanResult list) =
        // 구독자들에게 알림
        for result in results do
            this.NotifySubscribers(result)
        
        // 스캔 배치 생성 및 이벤트 발생
        let batch = ScanBatch.Create(plcId, results)
        scanCompletedEvent.Trigger(batch)
    
    // 추상 메서드들 - 하위 클래스에서 구현
    /// 실제 연결 구현
    abstract member ConnectAsyncImpl: unit -> Task<bool>
    
    /// 실제 연결 해제 구현
    abstract member DisconnectAsyncImpl: unit -> Task<unit>
    
    /// 실제 헬스 체크 구현
    abstract member HealthCheckAsyncImpl: unit -> Task<bool>
    
    /// 실제 태그 읽기 구현
    abstract member ReadTagAsyncImpl: tag: TagConfiguration -> Task<ScanResult>
    
    /// 실제 태그 쓰기 구현
    abstract member WriteTagAsyncImpl: tag: TagConfiguration * value: ScalarValue -> Task<bool>
    
    /// 실제 여러 태그 읽기 구현
    abstract member ReadTagsAsyncImpl: tags: TagConfiguration list -> Task<ScanBatch>
    
    /// 실제 여러 태그 쓰기 구현
    abstract member WriteTagsAsyncImpl: tagValues: (TagConfiguration * ScalarValue) list -> Task<bool>
    
    /// 진단 정보 업데이트 구현
    abstract member UpdateDiagnosticsImpl: unit -> Task<unit>
    
    // IPlcDriver 인터페이스 구현
    interface IPlcDriver with
        member this.PlcId = plcId
        member this.Vendor = vendor
        member this.ConnectionState = connectionState
        member this.Diagnostics = diagnostics
        member this.PerformanceStats = performanceStats
        member this.ConnectionOptions = connectionOptions
        
        member this.ConnectAsync() =
            this.SafeExecuteAsync(
                (fun () -> 
                    task {
                        this.UpdateConnectionState(PlcConnectionStatus.Connecting)
                        let! success = this.ConnectAsyncImpl()
                        if success then
                            this.UpdateConnectionState(PlcConnectionStatus.Connected)
                            do! this.UpdateDiagnosticsImpl()
                        else
                            this.UpdateConnectionState(PlcConnectionStatus.Failure ("Connection", "Failed to connect"))
                        return success
                    }
                ),
                false,
                "Connect"
            )
        
        member this.DisconnectAsync() =
            this.SafeExecuteAsync(
                (fun () -> 
                    task {
                        do! this.DisconnectAsyncImpl()
                        this.UpdateConnectionState(PlcConnectionStatus.Disconnected)
                    }
                ),
                (),
                "Disconnect"
            )
        
        member this.HealthCheckAsync() =
            this.SafeExecuteAsync(
                (fun () -> this.HealthCheckAsyncImpl()),
                false,
                "HealthCheck"
            )
        
        member this.ReadTagAsync(tag: TagConfiguration) =
            this.SafeExecuteAsync(
                (fun () -> this.ReadTagAsyncImpl(tag)),
                ScanResult.CreateError(tag.Id, plcId, "Read operation failed"),
                $"ReadTag({tag.Name})"
            )
        
        member this.WriteTagAsync(tag: TagConfiguration, value: ScalarValue) =
            this.SafeExecuteAsync(
                (fun () -> this.WriteTagAsyncImpl(tag, value)),
                false,
                $"WriteTag({tag.Name})"
            )
        
        member this.ReadTagsAsync(tags: TagConfiguration list) =
            this.SafeExecuteAsync(
                (fun () -> 
                    task {
                        let! batch = this.ReadTagsAsyncImpl(tags)
                        this.ProcessScanResults(batch.Results)
                        return batch
                    }
                ),
                ScanBatch.Create(plcId, []),
                "ReadTags"
            )
        
        member this.WriteTagsAsync(tagValues: (TagConfiguration * ScalarValue) list) =
            this.SafeExecuteAsync(
                (fun () -> this.WriteTagsAsyncImpl(tagValues)),
                false,
                "WriteTags"
            )
        
        member this.RegisterScanGroup(group: ScanGroup) =
            scanGroups.[group.Name] <- group
            logger.LogInformation("Registered scan group {GroupName} for PLC {PlcId}", group.Name, plcId)
        
        member this.UnregisterScanGroup(groupName: string) =
            match scanGroups.TryRemove(groupName) with
            | true, _ -> 
                logger.LogInformation("Unregistered scan group {GroupName} for PLC {PlcId}", groupName, plcId)
            | false, _ -> 
                logger.LogWarning("Scan group {GroupName} not found for PLC {PlcId}", groupName, plcId)
        
        member this.GetScanGroups() =
            scanGroups.Values |> Seq.toList
        
        member this.SubscribeTag(subscription: TagSubscription) =
            let subscriberMap = getOrCreateSubscriberMap subscription.TagId
            subscriberMap.[subscription.SubscriberId] <- subscription
            logger.LogDebug("Tag {TagId} subscribed by {SubscriberId} for PLC {PlcId}", 
                           subscription.TagId, subscription.SubscriberId, plcId)
        
        member this.UnsubscribeTag(tagId: string, subscriberId: string) =
            match subscriptions.TryGetValue(tagId) with
            | true, subscriberMap ->
                match subscriberMap.TryRemove(subscriberId) with
                | true, _ ->
                    if subscriberMap.IsEmpty then
                        subscriptions.TryRemove(tagId) |> ignore
                    logger.LogDebug("Tag {TagId} unsubscribed by {SubscriberId} for PLC {PlcId}", 
                                   tagId, subscriberId, plcId)
                | false, _ ->
                    logger.LogWarning("Subscription not found for tag {TagId} and subscriber {SubscriberId} for PLC {PlcId}", 
                                     tagId, subscriberId, plcId)
            | _ ->
                logger.LogWarning("Subscription not found for tag {TagId} and subscriber {SubscriberId} for PLC {PlcId}", 
                                 tagId, subscriberId, plcId)
        
        member this.GetSubscriptions() =
            subscriptions
            |> Seq.collect (fun kvp -> kvp.Value.Values)
            |> Seq.toList
        
        [<CLIEvent>]
        member this.ConnectionStateChanged = connectionStateChangedEvent.Publish
        
        [<CLIEvent>]
        member this.TagValueChanged = tagValueChangedEvent.Publish
        
        [<CLIEvent>]
        member this.ScanCompleted = scanCompletedEvent.Publish
        
        [<CLIEvent>]
        member this.ErrorOccurred = errorOccurredEvent.Publish
