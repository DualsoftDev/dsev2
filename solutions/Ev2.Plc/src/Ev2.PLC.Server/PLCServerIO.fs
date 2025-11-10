namespace DSPLCServer

open System.Collections.Generic
open Dual.PLC.Common.FS

module RuntimeControlIO =

    let mutable private tagState: RuntimeScanState option = None
    let mutable private env: RuntimeEnv option = None

    /// 내부 공통 초기화 함수
    let private initControlIO (createdEnv: RuntimeEnv): List<DsScanTagBase> =
        let state = scanDsIO createdEnv
        env <- Some createdEnv
        tagState <- Some state
        List<DsScanTagBase>(state.DsScanTags.Values)

    /// 런타임 모델 기반 Tag 설정
    let SetupScanDS(runtimeModel: RuntimeModel): List<DsScanTagBase> =
        createRuntimeEnv runtimeModel |> initControlIO

    /// Config 기반 Tag 설정
    let SetupScanDsFromConfig(sys: DsSystem, modelConfig: ModelConfig, tagConfig: TagConfig): List<DsScanTagBase> =
        createRuntimeEnvUsingConfig(sys, modelConfig, tagConfig) |> initControlIO

    /// Tag 스캔 해제
    let ScanUnsubscribe() =
        tagState |> Option.iter unsubscribeAll

    /// 태그 이벤트를 쓰기
    let WriteToDevice(tagEvent: TagEvent) =
        match env, tagState with
        | Some e, Some s -> handleScanTagWrite e s tagEvent
        | _ -> ()
