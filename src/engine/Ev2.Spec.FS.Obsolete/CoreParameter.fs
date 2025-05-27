namespace Dual.EV2.CoreParameter

module Params =

    type CallParam = {
        CallType: string 
        Timeout: int
        ActionType: string
        AutoPreConditions: ResizeArray<string>
        SafetyConditions: ResizeArray<string>
    }

    let defaultCallParam = {
        CallType = "Normal"
        Timeout = 1000
        ActionType = "ActionNormal"
        AutoPreConditions = ResizeArray()
        SafetyConditions = ResizeArray()
    }

    type WorkParam = {
        Motion: string
        Script: string
        DsTime: int * int
        Finished: bool
        RepeatCount: int
    }

    let defaultWorkParam = {
        Motion = ""
        Script = ""
        DsTime = (500, 5)
        Finished = false
        RepeatCount = 1
    }

    type FlowParam = {
        GroupTag: string
        MetaInfo: string
    }

    let defaultFlowParam = {
        GroupTag = ""
        MetaInfo = ""
    }

    type ApiCallParam = {
        InAddress: string
        OutAddress: string
        InSymbol: string
        OutSymbol: string
        IsAnalogSensor: bool
        IsAnalogActuator: bool
    }

    let defaultApiCallParam = {
        InAddress = ""
        OutAddress = ""
        InSymbol = ""
        OutSymbol = ""
        IsAnalogSensor = false
        IsAnalogActuator = false
    }
