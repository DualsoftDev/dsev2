namespace Ev2.PLC.Mapper.Core.Types

open System
open Ev2.PLC.Common.Types

/// I/O 방향
type IODirection =
    | Input
    | Output
    | Bidirectional
    | Internal

/// 디바이스 타입
type DeviceType =
    | Motor
    | Cylinder
    | Sensor
    | Valve
    | Conveyor
    | PushButton
    | Lamp
    | Counter
    | Timer
    | HMI
    | Custom of string

    member this.DisplayName =
        match this with
        | Motor -> "Motor"
        | Cylinder -> "Cylinder"
        | Sensor -> "Sensor"
        | Valve -> "Valve"
        | Conveyor -> "Conveyor"
        | PushButton -> "Push Button"
        | Lamp -> "Lamp"
        | Counter -> "Counter"
        | Timer -> "Timer"
        | HMI -> "HMI"
        | Custom name -> name

    member this.StandardApis =
        match this with
        | Motor -> ["FWD"; "BACK"; "STOP"; "SPEED"; "RUNNING"; "ERROR"]
        | Cylinder -> ["UP"; "DOWN"; "EXTEND"; "RETRACT"; "UP_SENSOR"; "DOWN_SENSOR"]
        | Sensor -> ["DETECT"; "VALUE"; "ERROR"; "CALIBRATE"]
        | Valve -> ["OPEN"; "CLOSE"; "POSITION"; "ERROR"]
        | Conveyor -> ["START"; "STOP"; "FWD"; "BACK"; "SPEED"; "RUNNING"]
        | PushButton -> ["PRESSED"; "RELEASED"]
        | Lamp -> ["ON"; "OFF"; "BLINK"]
        | Counter -> ["COUNT"; "RESET"; "UP"; "DOWN"; "VALUE"]
        | Timer -> ["START"; "STOP"; "RESET"; "ELAPSED"; "DONE"]
        | HMI -> ["UPDATE"; "ALARM"; "MESSAGE"]
        | Custom _ -> ["COMMAND"; "STATUS"; "VALUE"]

/// API 타입
type ApiType =
    | Command
    | Status
    | Parameter
    | Feedback
    | Safety
    | Diagnostic

    member this.Description =
        match this with
        | Command -> "Command signal to device"
        | Status -> "Device status feedback"
        | Parameter -> "Configuration parameter"
        | Feedback -> "Real-time feedback value"
        | Safety -> "Safety interlock signal"
        | Diagnostic -> "Diagnostic information"

/// API 정의
type ApiDefinition = {
    Name: string
    Type: ApiType
    DataType: PlcTagDataType
    Direction: IODirection
    Description: string
    Unit: string option
    MinValue: float option
    MaxValue: float option
    DefaultValue: ScalarValue option
    PrecedingApis: string list
    InterlockApis: string list
    SafetyLevel: int
} with
    static member Create(name: string, apiType: ApiType, dataType: PlcTagDataType, direction: IODirection) = {
        Name = name
        Type = apiType
        DataType = dataType
        Direction = direction
        Description = ""
        Unit = None
        MinValue = None
        MaxValue = None
        DefaultValue = None
        PrecedingApis = []
        InterlockApis = []
        SafetyLevel = 0
    }

/// I/O 변수
type IOVariable = {
    LogicalName: string
    PhysicalAddress: PlcAddress
    DataType: PlcTagDataType
    Direction: IODirection
    Device: string
    Api: string option
    Comment: string option
    InitialValue: ScalarValue option
    Scaling: (float * float) option
} with
    static member Create(logicalName: string, physicalAddress: PlcAddress, dataType: PlcTagDataType, direction: IODirection) = {
        LogicalName = logicalName
        PhysicalAddress = physicalAddress
        DataType = dataType
        Direction = direction
        Device = ""
        Api = None
        Comment = None
        InitialValue = None
        Scaling = None
    }

/// I/O 매핑
type IOMapping = {
    Inputs: IOVariable list
    Outputs: IOVariable list
    Parameters: IOVariable list
    Internal: IOVariable list
} with
    static member Empty = {
        Inputs = []
        Outputs = []
        Parameters = []
        Internal = []
    }

    member this.AllVariables =
        this.Inputs @ this.Outputs @ this.Parameters @ this.Internal

    member this.GetVariable(logicalName: string) =
        this.AllVariables |> List.tryFind (fun v -> v.LogicalName = logicalName)

    member this.AddVariable(variable: IOVariable) =
        match variable.Direction with
        | Input -> { this with Inputs = variable :: this.Inputs }
        | Output -> { this with Outputs = variable :: this.Outputs }
        | Internal -> { this with Internal = variable :: this.Internal }
        | Bidirectional -> { this with Parameters = variable :: this.Parameters }

/// 디바이스 정의
type Device = {
    Name: string
    Type: DeviceType
    Area: string
    Description: string
    SupportedApis: ApiDefinition list
    IOMapping: IOMapping
    Properties: Map<string, string>
    Position: (float * float) option
} with
    static member Create(name: string, deviceType: DeviceType, area: string) = {
        Name = name
        Type = deviceType
        Area = area
        Description = ""
        SupportedApis = []
        IOMapping = IOMapping.Empty
        Properties = Map.empty
        Position = None
    }

    member this.GetApi(apiName: string) =
        this.SupportedApis |> List.tryFind (fun api -> api.Name = apiName)

/// 영역 (Area) 정의
type Area = {
    Name: string
    Description: string
    Devices: string list
    Priority: int
    Properties: Map<string, string>
} with
    static member Create(name: string) = {
        Name = name
        Description = ""
        Devices = []
        Priority = 0
        Properties = Map.empty
    }

/// 변수명 패턴 분석 결과
type VariableNamingPattern = {
    OriginalName: string
    Prefix: string option
    DeviceName: string
    ApiSuffix: string option
    IOType: IODirection
    Confidence: float
} with
    static member Create(originalName: string, deviceName: string) = {
        OriginalName = originalName
        Prefix = None
        DeviceName = deviceName
        ApiSuffix = None
        IOType = Output
        Confidence = 0.5
    }

    member this.HasValidPattern = this.Confidence > 0.7

/// 변수 분석 결과
type VariableAnalysisResult = {
    Variable: RawVariable
    Pattern: VariableNamingPattern option
    Device: Device option
    Api: ApiDefinition option
    IOVariable: IOVariable option
    Issues: string list
    Confidence: float
} with
    static member Create(variable: RawVariable) = {
        Variable = variable
        Pattern = None
        Device = None
        Api = None
        IOVariable = None
        Issues = []
        Confidence = 0.0
    }

    member this.IsValid = this.Confidence > 0.5 && this.Issues.IsEmpty

    member this.HasDevice = this.Device.IsSome

    member this.HasApi = this.Api.IsSome
