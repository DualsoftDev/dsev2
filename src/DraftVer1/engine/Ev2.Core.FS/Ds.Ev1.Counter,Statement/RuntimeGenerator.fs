namespace Dual.Ev2

open System.Reactive.Subjects
open Dual.Common.Core.FS
open System.Collections.Generic
open Dual.Common.Base.Functions
open System
open Dual.Common.Base

[<AutoOpen>]
module RuntimeGeneratorModule =

    //제어 HW CPU 기기 타입
    type PlatformTarget with
        static member ofString(str:string) = DU.fromString<PlatformTarget> str |?? (fun () -> failwith "ERROR")

        member x.Stringify() = x.ToString()
        member x.IsPLC = x <> WINDOWS
        member x.TryGetPlcType() =
            match x with
            | WINDOWS -> None
            | _ -> Some x


    //제어 Driver IO 기기 타입
    type HwDriveTarget =
        | NONE
        | LS_XGI_IO
        | LS_XGK_IO
        | AB_IO
        | MELSEC_IO
        | SIEMENS_IO
        | PAIX_IO

    //HW CPU,  Driver IO, Slot 정보 조합
    type HwTarget(platformTarget:PlatformTarget, hwDriveTarget:HwDriveTarget, slots:SlotDataType[]) =
        member x.Platform = platformTarget
        member x.HwDrive = hwDriveTarget
        member x.Slots = slots

    type RuntimeMotionMode =
        | MotionAsync
        | MotionSync

    type TimeSimutionMode =
        | TimeNone
        | TimeX0_1
        | TimeX0_5
        | TimeX1
        | TimeX2
        | TimeX4
        | TimeX8
        | TimeX16
        | TimeX100

    type RuntimePackage =
        | PC
        | PCSIM
        | PLC
        | PLCSIM
    with
        member x.IsPCorPCSIM() =
            match x with
            | PC | PCSIM  -> true
            | _ -> false

        member x.IsPLCorPLCSIM() =
            match x with
            | PLC | PLCSIM -> true
            | _ -> false

        member x.IsPackageSIM() =
            match x with
            | PCSIM | PLCSIM -> true
            | _ -> false

    let RuntimePackageList = [ PC; PCSIM; PLC; PLCSIM;  ]

    let ToRuntimePackage s =
        match s with
        | "PC" -> PC
        | "PCSIM" -> PCSIM
        | "PLC" -> PLC
        | "PLCSIM" -> PLCSIM
        | _ -> failwithlogf $"Error {getFuncName()}"

    let InitStartMemory = 1000
    let BufferAlramSize = 9999
    let XGKAnalogOffsetByte = 96
    let XGKAnalogOutOffsetByte = 96


    let ExternalTempMemory =  "M"
    let ExternalTempIECMemory =  "%MX"
    let ExternalTempNoIECMemory =  "M"


    let HMITempMemory =  "%HX99"  //iec xgk 구분안함
    let HMITempManualAction =  "%HX0"  //iec xgk 구분안함


    let getExternalTempMemory (target:HwTarget, index:int) =
        match target.Platform with
        | XGI-> ExternalTempIECMemory+index.ToString()
        | XGK-> ExternalTempNoIECMemory+index.ToString("00000")
        | WINDOWS-> ExternalTempMemory+($"{index/8}.{index%8}")
        | _ -> failwithlog $"{target} not support"

    type ModelConfig = {
        DsFilePath: string
        HwIP: string
        UsingOPC: bool
        RuntimePackage: RuntimePackage
        PlatformTarget: PlatformTarget
        HwDriver: string //LS-XGI, LS-XGK, Paix hw drive 이름
        RuntimeMotionMode: RuntimeMotionMode
        TimeSimutionMode : TimeSimutionMode
        TimeoutCall : uint32
    } with
        interface IModelConfig

    let createDefaultModelConfig() =
        {
            DsFilePath = ""
            HwIP = "127.0.0.1"
            UsingOPC = false
            RuntimePackage = PCSIM //unit test를 위해 PCSIM으로 설정
            PlatformTarget = WINDOWS
            HwDriver = HwDriveTarget.LS_XGK_IO.ToString()
            RuntimeMotionMode = MotionAsync
            TimeSimutionMode = TimeX1
            TimeoutCall = 15000u
        }
    let createDefaultModelConfigWithHwDriver(hwDriver: HwDriveTarget) =
        { createDefaultModelConfig() with HwDriver = hwDriver.ToString() }
    let createModelConfigWithSimMode(config: ModelConfig, package:RuntimePackage) =
        { config with RuntimePackage = package }

    let createModelConfig(path:string,
            hwIP:string,
            usingOPC:bool,
            runtimePackage:RuntimePackage,
            platformTarget:PlatformTarget,
            hwDriver:HwDriveTarget,
            runtimeMotionMode:RuntimeMotionMode,
            timeSimutionMode:TimeSimutionMode,
            timeoutCall:uint32) =
        {
            DsFilePath = path
            HwIP = hwIP
            UsingOPC = usingOPC
            RuntimePackage = runtimePackage
            PlatformTarget = platformTarget
            HwDriver = hwDriver.ToString()
            RuntimeMotionMode = runtimeMotionMode
            TimeSimutionMode = timeSimutionMode
            TimeoutCall = timeoutCall
        }
    let createModelConfigReplacePath (cfg:ModelConfig, path:string) =
        { cfg with DsFilePath = path }
    let createModelConfigReplacePackage (cfg:ModelConfig, runtimePackage:RuntimePackage) =
        { cfg with RuntimePackage = runtimePackage }

    type RuntimeDS() =
        static member val System : ISystem option = None with get, set
        static member val ModelConfig : ModelConfig = createDefaultModelConfig() with get, set

        //RuntimePackage는 외부에서 변경가능
        static member ChangeRuntimePackage(package:RuntimePackage) =
            RuntimeDS.ModelConfig <- createModelConfigWithSimMode(RuntimeDS.ModelConfig, package)

    let getFullSlotHwSlotDataTypes() =
        let hw =
            [|0 .. 11|]
            |> Array.map (fun i ->
                if i % 2 = 0 then
                    SlotDataType(i, IOType.In, DataType.DuUINT64)
                else
                    SlotDataType(i, IOType.Out, DataType.DuUINT64))
        hw

    let getDefaltHwTarget() = HwTarget(WINDOWS, PAIX_IO, getFullSlotHwSlotDataTypes())



module PlatformTargetExtensions =
        let fromString s =
            match s with
            | "WINDOWS"-> WINDOWS
            | "XGI"    -> XGI
            | "XGK"    -> XGK
            | "AB"     -> AB
            | "MELSEC" -> MELSEC
            | _ -> failwithf $"Error ToPlatformTarget: {s}"

        let allPlatforms =
            [ WINDOWS; XGI; XGK; AB; MELSEC]


module HwDriveTargetExtensions =
    let fromString s =
            match s with
            | "LS_XGI_IO"  -> LS_XGI_IO
            | "LS_XGK_IO"  -> LS_XGK_IO
            | "AB_IO"      -> AB_IO
            | "MELSEC_IO"  -> MELSEC_IO
            | "SIEMENS_IO" -> SIEMENS_IO
            | "PAIX_IO"    -> PAIX_IO
            | _ -> NONE

    let allDrivers =
        [ LS_XGI_IO; LS_XGK_IO; AB_IO; MELSEC_IO; SIEMENS_IO; PAIX_IO; NONE ]


module TimeSimutionModeExtensions =

        let toString mode =
            match mode with
            | TimeNone -> "Ignore Time"
            | TimeX0_1 -> "0.1x Speed"
            | TimeX0_5 -> "0.5x Speed"
            | TimeX1 -> "1x Speed"
            | TimeX2 -> "2x Speed"
            | TimeX4 -> "4x Speed"
            | TimeX8 -> "8x Speed"
            | TimeX16 -> "16x Speed"
            | TimeX100 -> "100x Speed"

        let fromString s =
            match s with
            | "Ignore Time" -> TimeNone
            | "0.1x Speed" -> TimeX0_1
            | "0.5x Speed" -> TimeX0_5
            | "1x Speed" -> TimeX1
            | "2x Speed" -> TimeX2
            | "4x Speed" -> TimeX4
            | "8x Speed" -> TimeX8
            | "16x Speed" -> TimeX16
            | "100x Speed" -> TimeX100
            | _ -> failwithf $"Error ToTimeSimutionMode: {s}"

        let allModes =
            [ TimeNone; TimeX0_1; TimeX0_5; TimeX1; TimeX2; TimeX4; TimeX8; TimeX16; TimeX100 ]
