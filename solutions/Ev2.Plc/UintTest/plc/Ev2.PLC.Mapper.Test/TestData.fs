namespace Ev2.PLC.Mapper.Test

open System
open Ev2.PLC.Common.Types
open Ev2.PLC.Mapper.Core.Types

/// Test data module for shared test data
module TestData =

    /// Sample L5K content for Allen-Bradley
    let sampleL5K = """
CONTROLLER TestController (ProcessorType := "1756-L75", MajorRev := 32)

PROGRAM MainProgram

ROUTINE TestRoutine
RUNG 0
XIC(StartButton) XIO(StopButton) OTE(Motor);
// Motor control logic
END_RUNG

RUNG 1
TON(Timer1, 5000) XIC(Timer1.DN) OTE(AlarmLight);
// Timer alarm logic
END_RUNG
END_ROUTINE

TAG
StartButton : BOOL;
StopButton : BOOL;
Motor : BOOL;
Timer1 : TIMER;
AlarmLight : BOOL;
END_TAG

END_PROGRAM
"""

    /// Sample XML content for LS Electric
    let sampleLSXML = """<?xml version="1.0" encoding="UTF-8"?>
<XG5000Project version="2.0">
    <Program Name="MainProgram" Language="LD">
        <Rung Number="0">
            <Comment>Motor Start/Stop Control</Comment>
            <Elements>
                <Element Type="NO_CONTACT" Name="StartButton" />
                <Element Type="NC_CONTACT" Name="StopButton" />
                <Element Type="COIL" Name="Motor" />
            </Elements>
        </Rung>
        <Rung Number="1">
            <Comment>Timer Operation</Comment>
            <Elements>
                <Element Type="NO_CONTACT" Name="Motor" />
                <Element Type="TON" Name="Timer1" Parameter="T#5s" />
                <Element Type="COIL" Name="TimerDone" />
            </Elements>
        </Rung>
    </Program>
</XG5000Project>"""

    /// Sample CSV content for Mitsubishi
    let sampleMitsubishiCSV = """Step,Instruction,Device,Comment
0,LD,X0,Start Button
1,AND,X1,Safety Switch
2,OUT,Y0,Motor Output
3,LD,Y0,
4,TON,T0,T#5s
5,LD,T0,Timer Done
6,OUT,Y1,Alarm
7,END,,"""

    /// Sample AWL content for Siemens
    let sampleSiemensAWL = """
NETWORK 1
TITLE = Motor Start/Stop

// Motor control with start and stop buttons
      A     I 0.0       // Start button
      AN    I 0.1       // Stop button
      =     Q 0.0       // Motor output

NETWORK 2
TITLE = Timer Function

      A     I 0.2
      L     S5T#5S
      SD    T 1
      A     T 1
      =     Q 0.1
"""

    /// Create sample raw variables
    let createSampleVariables() = [
        {
            Name = "Motor1_Start"
            Address = "%IX0.0"
            DataType = "BOOL"
            Comment = Some "Motor 1 start button"
            InitialValue = None
            Scope = Some "Global"
            AccessLevel = Some "ReadWrite"
            Properties = Map.empty
        }
        {
            Name = "Motor1_Stop"
            Address = "%IX0.1"
            DataType = "BOOL"
            Comment = Some "Motor 1 stop button"
            InitialValue = None
            Scope = Some "Global"
            AccessLevel = Some "ReadWrite"
            Properties = Map.empty
        }
        {
            Name = "Motor1_Running"
            Address = "%QX0.0"
            DataType = "BOOL"
            Comment = Some "Motor 1 running status"
            InitialValue = None
            Scope = Some "Global"
            AccessLevel = Some "ReadWrite"
            Properties = Map.empty
        }
        {
            Name = "Temperature_Sensor1"
            Address = "%IW10"
            DataType = "INT"
            Comment = Some "Temperature sensor 1"
            InitialValue = None
            Scope = Some "Global"
            AccessLevel = Some "ReadOnly"
            Properties = Map.empty
        }
        {
            Name = "Temperature_Setpoint"
            Address = "%MW20"
            DataType = "INT"
            Comment = Some "Temperature setpoint"
            InitialValue = Some "25"
            Scope = Some "Global"
            AccessLevel = Some "ReadWrite"
            Properties = Map.empty
        }
    ]

    /// Create sample raw logic
    let createSampleLogic() : RawLogic list = [
        {
            Id = Some "1"
            Name = Some "Motor Control"
            Number = 1
            Content = "XIC(StartButton) XIO(StopButton) OTE(Motor)"
            RawContent = Some "XIC(StartButton) XIO(StopButton) OTE(Motor)"
            LogicType = LogicType.LadderRung
            Type = Some LogicFlowType.Simple
            Variables = ["StartButton"; "StopButton"; "Motor"]
            Comments = ["Motor start/stop control"]
            LineNumber = Some 1
            Properties = Map.empty
            Comment = Some "Motor start/stop control"
        }
        {
            Id = Some "2"
            Name = Some "Timer Logic"
            Number = 2
            Content = "TON(Timer1, 5000) XIC(Timer1.DN) OTE(AlarmLight)"
            RawContent = Some "TON(Timer1, 5000) XIC(Timer1.DN) OTE(AlarmLight)"
            LogicType = LogicType.LadderRung
            Type = Some LogicFlowType.Timer
            Variables = ["Timer1"; "AlarmLight"]
            Comments = ["Timer alarm logic"]
            LineNumber = Some 2
            Properties = Map.empty
            Comment = Some "Timer alarm logic"
        }
    ]

    /// Create sample mapping configuration
    let createTestMappingConfig(vendor: PlcVendor) = {
        Vendor = vendor
        Rules = {
            VariableNaming = {
                Prefix = Some "PLC_"
                Suffix = None
                CasingStyle = "PascalCase"
                Separator = "_"
                MaxLength = Some 64
                MinLength = Some 3
                AllowedCharacters = Some "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_"
                ReservedWords = ["IF"; "THEN"; "ELSE"; "END"]
            }
            DeviceGrouping = {
                GroupByPrefix = true
                GroupBySuffix = false
                GroupByType = true
                MinGroupSize = 2
                MaxGroupSize = Some 100
                Separators = ["_"; "."]
            }
            ApiGeneration = {
                GenerateReadApi = true
                GenerateWriteApi = true
                GenerateBatchApi = true
                UseAsync = true
                MaxBatchSize = 100
                Timeout = TimeSpan.FromSeconds(30.0)
            }
        }
        CustomProperties = Map.empty
    }

    /// Create test project info
    let createTestProjectInfo(vendor: PlcVendor) = {
        Name = "TestProject"
        Version = "1.0.0"
        Vendor = vendor
        Format = CustomFormat("test.xml", "XML")
        FilePath = "C:\\test\\project.xml"
        CreatedDate = DateTime.Now
        ModifiedDate = DateTime.Now
        FileSize = 1024L
        Metadata = Map.empty
    }