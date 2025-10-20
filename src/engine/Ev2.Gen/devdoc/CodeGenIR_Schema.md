# IR Schema Specification — Full (CamelCase)

> 목적: 다양한 PLC/IDE 간 코드 자동 생성을 위해 사용할 **중간표현(IR)** 의 전체 스키마 예시.  

---

## TopLevelOverview
```json
{
  "IrVersion": "1.0.0",
  "Project": { ... },
  "Devices": [ ... ],
  "IO": { ... },
  "DataTypes": { ... },
  "Libraries": [ ... ],
  "Variables": { ... },
  "Tasks": [ ... ],
  "Resources": [ ... ],
  "POUs": [ ... ],
  "Motion": { ... },
  "Safety": { ... },
  "Communication": { ... },
  "HMI": { ... },
  "Graphics": { ... },
  "Constraints": { ... },
  "Localization": { ... },
  "Units": { ... },
  "VendorExtensions": { ... }
}
```

> 비고: JSON은 주석을 허용하지 않으므로, 설명은 본문에서 제공합니다.

---

## Project
```json
{
  "Project": {
    "Name": "PackagingLine01",
    "Description": "Filler+Capper station",
    "Version": "0.3.2",
    "CreatedAt": "2025-10-18T09:12:00Z",
    "ModifiedAt": "2025-10-20T01:00:00Z",
    "TimeBase": "ms",                              
    "TargetProfiles": [
      { "Vendor": "Beckhoff", "Ide": "TwinCAT3", "Profile": "TC3_PLC" },
      { "Vendor": "CODESYS", "Ide": "V3.5", "Profile": "CODESYS_Default" }
    ]
  }
}
```

---

## Devices (Controller/Modules Tree)
```json
{
  "Devices": [
    {
      "Id": "Ctrl01",
      "Type": "PLC",
      "Vendor": "Beckhoff",
      "Model": "CX-XXXX",
      "Firmware": "3.1",
      "Network": { "Address": "10.0.0.10", "Protocol": "EtherCAT" },
      "Slots": [
        { "Id": "TermA", "Type": "IOModule", "Model": "EL7041", "Role": "DriveIF" },
        { "Id": "TermB", "Type": "IOModule", "Model": "EL1809", "Role": "DI" },
        { "Id": "TermC", "Type": "IOModule", "Model": "EL2809", "Role": "DO" }
      ]
    }
  ]
}
```

---

## IO (Channels/Tags/Mapping)
```json
{
  "IO": {
    "Channels": [
      { "Id": "DI_01", "Direction": "In",  "Type": "BOOL", "DeviceRef": "TermB", "ChannelIndex": 0, "Label": "StartPB" },
      { "Id": "DO_01", "Direction": "Out", "Type": "BOOL", "DeviceRef": "TermC", "ChannelIndex": 0, "Label": "LampGreen" }
    ],
    "Mappings": [
      { "Variable": "GVL.StartPB", "ChannelRef": "DI_01" },
      { "Variable": "GVL.LampGreen", "ChannelRef": "DO_01" }
    ]
  }
}
```

---

## DataTypes (UDT/Enum/Alias)
```json
{
  "DataTypes": {
    "UDT": [
      {
        "Name": "AxisParam",
        "Type": "STRUCT",
        "Fields": [
          { "Name": "Vel", "Type": "LREAL", "Unit": "mm/s", "Default": 50.0 },
          { "Name": "Acc", "Type": "LREAL", "Unit": "mm/s^2", "Default": 500.0 },
          { "Name": "Dec", "Type": "LREAL", "Unit": "mm/s^2", "Default": 500.0 }
        ]
      }
    ],
    "Enums": [
      { "Name": "HomeMode", "BaseType": "INT", "Literals": ["LIMIT_SWITCH", "MARKER", "ABSOFFSET"] }
    ],
    "Aliases": [
      { "Name": "AXIS_REF", "UnderlyingType": "POINTER", "Meta": { "Category": "MotionAxisRef" } }
    ]
  }
}
```

---

## Libraries (Dependencies)
```json
{
  "Libraries": [
    { "Name": "PLCopenMotion", "Version": "2.0.0" },
    { "Name": "PLCopenSafety", "Version": "1.0.0" },
    { "Name": "Vendor.TwinCAT.MotionExt", "Version": "1.2.3", "Optional": true }
  ]
}
```

---

## Variables (GVL/Constants/Retain)
```json
{
  "Variables": {
    "Global": [
      { "Name": "StartPB",   "Type": "BOOL", "Retain": false },
      { "Name": "LampGreen", "Type": "BOOL", "Retain": false },
      { "Name": "AxisX",     "Type": "AXIS_REF" },
      { "Name": "XParam",    "Type": "AxisParam", "Init": { "Vel": 80.0 } }
    ],
    "Constants": [
      { "Name": "HOME_POS", "Type": "LREAL", "Value": 0.0 }
    ],
    "Retain": [
      { "Name": "CycleCount", "Type": "DINT", "Init": 0 }
    ]
  }
}
```

---

## Tasks (Cycle/Priority/Binding)
```json
{
  "Tasks": [
    {
      "Name": "Cyclic10ms",
      "Interval": 10,
      "Priority": 1,
      "ProgramCalls": [
        { "PouRef": "Main", "Order": 1 },
        { "PouRef": "AxisCtrl", "Order": 2 }
      ]
    }
  ]
}
```

---

## Resources (IEC Resources, Optional)
```json
{
  "Resources": [
    {
      "Name": "Res1",
      "Tasks": ["Cyclic10ms"],
      "Watchdog": { "Enabled": true, "TimeoutMs": 50 }
    }
  ]
}
```

---

## POUs (Program/Function/FB)
```json
{
  "POUs": [
    {
      "Name": "Main",
      "Kind": "Program",
      "Language": "ST",
      "Interface": {
        "LocalVars": [
          { "Name": "MoveAbs", "Type": "MC_MoveAbsolute" }
        ]
      },
      "Body": {
        "ST": "MoveAbs(Axis := AxisX, Position := 100.0, Velocity := XParam.Vel, Execute := StartPB);"
      },
      "Annotations": { "SafetyRelevant": false, "Category": "Sequence" }
    },
    {
      "Name": "AxisCtrl",
      "Kind": "Program",
      "Language": "FBD",
      "Interface": { "LocalVars": [ { "Name": "Home", "Type": "MC_Home" } ] },
      "Body": {
        "FBD": {
          "Nodes": [
            { "Id": "N1", "Type": "FB", "FbType": "MC_Home", "Position": { "X": 120, "Y": 80 } },
            { "Id": "N2", "Type": "VAR", "VarName": "AxisX",  "Position": { "X": 40, "Y": 80 } },
            { "Id": "N3", "Type": "VAR", "VarName": "StartPB","Position": { "X": 40, "Y": 130 } }
          ],
          "Wires": [
            { "From": { "Node": "N2", "Port": "Out" }, "To": { "Node": "N1", "Port": "Axis" } },
            { "From": { "Node": "N3", "Port": "Out" }, "To": { "Node": "N1", "Port": "Execute" } }
          ]
        }
      }
    }
  ]
}
```

---

## Motion (Axes/Groups/FB Mapping)
```json
{
  "Motion": {
    "Axes": [
      {
        "Name": "AxisX",
        "DeviceRef": "TermA",
        "Limits": { "PosMin": -10.0, "PosMax": 310.0, "VelMax": 200.0 },
        "Homing": { "Mode": "LIMIT_SWITCH", "Offset": 0.0 }
      }
    ],
    "Groups": [],
    "FbMapping": [
      {
        "Standard": "MC_MoveAbsolute",
        "Targets": [
          { "Profile": "TwinCAT3", "Fb": "MC_MoveAbsolute" },
          { "Profile": "CODESYS_Default", "Fb": "MC_MoveAbsolute" }
        ],
        "ParameterMap": {
          "Position": "Position",
          "Velocity": "Velocity",
          "Acceleration?": "Accel",
          "Deceleration?": "Decel"
        },
        "Semantics": { "DoneEdge": "Rising", "BusyBehavior": "Level" }
      }
    ]
  }
}
```

---

## Safety (PL/SIL/FB/IO Links)
```json
{
  "Safety": {
    "PL": "PL-d",
    "SIL": "SIL2",
    "Circuits": [
      { "Name": "EStopLoop", "Inputs": ["DI_01"], "Logic": "Latching" }
    ],
    "FbInstances": [
      {
        "Name": "EStop",
        "Type": "SF_EmergencyStop",
        "Params": { "ResetMode": "Manual" },
        "IoMap": { "S_EStopIn": "DI_01", "S_EnableOut": "DO_01" }
      }
    ]
  }
}
```

---

## Communication (OPC UA / Modbus / PN / EIP)
```json
{
  "Communication": {
    "Servers": [
      {
        "Type": "OPCUA",
        "Endpoint": "opc.tcp://10.0.0.10:4840",
        "NamespaceUri": "urn:packaging:line01",
        "Expose": [
          { "NodeId": "ns=2;s=AxisX.ActPos", "SourceVar": "AxisX.ActPos" },
          { "NodeId": "ns=2;s=CycleCount",   "SourceVar": "CycleCount" }
        ]
      }
    ],
    "Clients": [
      { "Type": "ModbusTCP", "Server": "10.0.0.20:502", "Mappings": [ { "HoldingReg": 40001, "Var": "GVL.Setpoint" } ] }
    ]
  }
}
```

---

## HMI (Tags/Bindings)
```json
{
  "HMI": {
    "Tags": [
      { "Name": "StartPB", "Var": "GVL.StartPB", "Rw": "Write" },
      { "Name": "ActPos",  "Var": "AxisX.ActPos", "Rw": "Read", "Format": "0.00" }
    ],
    "Screens": [
      { "Name": "Main", "Bindings": [ "StartPB", "ActPos" ] }
    ]
  }
}
```

---

## Graphics (Editor Canvas, Optional)
```json
{
  "Graphics": {
    "Editors": [
      {
        "Pou": "AxisCtrl",
        "Language": "FBD",
        "Canvas": { "Width": 1600, "Height": 900, "Snap": 10, "Zoom": 1.0 }
      }
    ]
  }
}
```

---

## Constraints (RT/Memory/Watchdogs, Optional)
```json
{
  "Constraints": {
    "CycleBudgetMs": 1.0,
    "Memory": { "RamKB": 512, "NvKB": 128 },
    "Watchdogs": [ { "Task": "Cyclic10ms", "TimeoutMs": 50 } ]
  }
}
```

---

## Localization (I18N, Optional)
```json
{
  "Localization": {
    "Languages": ["ko-KR", "en-US"],
    "Strings": {
      "ko-KR": { "StartPB": "시작버튼" },
      "en-US": { "StartPB": "Start Button" }
    }
  }
}
```

---

## Units (Engineering Units, Optional)
```json
{
  "Units": {
    "Default": "SI",
    "Overrides": { "AxisX.Position": "mm", "AxisX.Velocity": "mm/s" }
  }
}
```

---

## VendorExtensions (Lossless Round-Trip)
```json
{
  "VendorExtensions": {
    "TwinCAT3": {
      "TaskClass": "TcTask",
      "Pragmas": ["{attribute 'call_after_init'}"]
    },
    "CODESYS": {
      "CompilerSwitches": ["/no_var_opt"],
      "GvlFolder": "Globals"
    }
  }
}
```

---

## Complete Example (Merged)
```json
{
  "IrVersion": "1.0.0",
  "Project": {
    "Name": "DemoLine",
    "Description": "End-to-end demo with motion, safety, comms",
    "Version": "1.0.0",
    "CreatedAt": "2025-10-18T09:12:00Z",
    "ModifiedAt": "2025-10-20T01:00:00Z",
    "TimeBase": "ms",
    "TargetProfiles": [
      { "Vendor": "Beckhoff", "Ide": "TwinCAT3", "Profile": "TC3_PLC" },
      { "Vendor": "CODESYS", "Ide": "V3.5", "Profile": "CODESYS_Default" }
    ]
  },
  "Devices": [
    {
      "Id": "Ctrl01",
      "Type": "PLC",
      "Vendor": "Beckhoff",
      "Model": "CX-XXXX",
      "Firmware": "3.1",
      "Network": { "Address": "10.0.0.10", "Protocol": "EtherCAT" },
      "Slots": [
        { "Id": "TermA", "Type": "IOModule", "Model": "EL7041", "Role": "DriveIF" },
        { "Id": "TermB", "Type": "IOModule", "Model": "EL1809", "Role": "DI" },
        { "Id": "TermC", "Type": "IOModule", "Model": "EL2809", "Role": "DO" }
      ]
    }
  ],
  "IO": {
    "Channels": [
      { "Id": "DI_01", "Direction": "In",  "Type": "BOOL", "DeviceRef": "TermB", "ChannelIndex": 0, "Label": "StartPB" },
      { "Id": "DO_01", "Direction": "Out", "Type": "BOOL", "DeviceRef": "TermC", "ChannelIndex": 0, "Label": "LampGreen" }
    ],
    "Mappings": [
      { "Variable": "GVL.StartPB", "ChannelRef": "DI_01" },
      { "Variable": "GVL.LampGreen", "ChannelRef": "DO_01" }
    ]
  },
  "DataTypes": {
    "UDT": [
      { "Name": "AxisParam", "Type": "STRUCT", "Fields": [
        { "Name": "Vel", "Type": "LREAL", "Unit": "mm/s", "Default": 50.0 },
        { "Name": "Acc", "Type": "LREAL", "Unit": "mm/s^2", "Default": 500.0 },
        { "Name": "Dec", "Type": "LREAL", "Unit": "mm/s^2", "Default": 500.0 }
      ]}
    ],
    "Enums": [
      { "Name": "HomeMode", "BaseType": "INT", "Literals": ["LIMIT_SWITCH", "MARKER", "ABSOFFSET"] }
    ],
    "Aliases": [
      { "Name": "AXIS_REF", "UnderlyingType": "POINTER", "Meta": { "Category": "MotionAxisRef" } }
    ]
  },
  "Libraries": [
    { "Name": "PLCopenMotion", "Version": "2.0.0" },
    { "Name": "PLCopenSafety", "Version": "1.0.0" }
  ],
  "Variables": {
    "Global": [
      { "Name": "StartPB",   "Type": "BOOL", "Retain": false },
      { "Name": "LampGreen", "Type": "BOOL", "Retain": false },
      { "Name": "AxisX",     "Type": "AXIS_REF" },
      { "Name": "XParam",    "Type": "AxisParam", "Init": { "Vel": 80.0 } }
    ],
    "Constants": [
      { "Name": "HOME_POS", "Type": "LREAL", "Value": 0.0 }
    ],
    "Retain": [
      { "Name": "CycleCount", "Type": "DINT", "Init": 0 }
    ]
  },
  "Tasks": [
    { "Name": "Cyclic10ms", "Interval": 10, "Priority": 1,
      "ProgramCalls": [ { "PouRef": "Main", "Order": 1 }, { "PouRef": "AxisCtrl", "Order": 2 } ] }
  ],
  "Resources": [
    { "Name": "Res1", "Tasks": ["Cyclic10ms"], "Watchdog": { "Enabled": true, "TimeoutMs": 50 } }
  ],
  "POUs": [
    { "Name": "Main", "Kind": "Program", "Language": "ST",
      "Interface": { "LocalVars": [ { "Name": "MoveAbs", "Type": "MC_MoveAbsolute" } ] },
      "Body": { "ST": "MoveAbs(Axis := AxisX, Position := 100.0, Velocity := XParam.Vel, Execute := StartPB);" },
      "Annotations": { "SafetyRelevant": false, "Category": "Sequence" }
    },
    { "Name": "AxisCtrl", "Kind": "Program", "Language": "FBD",
      "Interface": { "LocalVars": [ { "Name": "Home", "Type": "MC_Home" } ] },
      "Body": { "FBD": {
        "Nodes": [
          { "Id": "N1", "Type": "FB",  "FbType": "MC_Home", "Position": { "X": 120, "Y": 80 } },
          { "Id": "N2", "Type": "VAR", "VarName": "AxisX",  "Position": { "X": 40, "Y": 80 } },
          { "Id": "N3", "Type": "VAR", "VarName": "StartPB","Position": { "X": 40, "Y": 130 } }
        ],
        "Wires": [
          { "From": { "Node": "N2", "Port": "Out" }, "To": { "Node": "N1", "Port": "Axis" } },
          { "From": { "Node": "N3", "Port": "Out" }, "To": { "Node": "N1", "Port": "Execute" } }
        ]
      }}
    }
  ],
  "Motion": {
    "Axes": [
      { "Name": "AxisX", "DeviceRef": "TermA",
        "Limits": { "PosMin": -10.0, "PosMax": 310.0, "VelMax": 200.0 },
        "Homing": { "Mode": "LIMIT_SWITCH", "Offset": 0.0 } }
    ],
    "Groups": [],
    "FbMapping": [
      { "Standard": "MC_MoveAbsolute",
        "Targets": [ { "Profile": "TwinCAT3", "Fb": "MC_MoveAbsolute" },
                     { "Profile": "CODESYS_Default", "Fb": "MC_MoveAbsolute" } ],
        "ParameterMap": { "Position": "Position", "Velocity": "Velocity", "Acceleration?": "Accel", "Deceleration?": "Decel" },
        "Semantics": { "DoneEdge": "Rising", "BusyBehavior": "Level" } }
    ]
  },
  "Safety": {
    "PL": "PL-d",
    "SIL": "SIL2",
    "Circuits": [ { "Name": "EStopLoop", "Inputs": ["DI_01"], "Logic": "Latching" } ],
    "FbInstances": [
      { "Name": "EStop", "Type": "SF_EmergencyStop", "Params": { "ResetMode": "Manual" },
        "IoMap": { "S_EStopIn": "DI_01", "S_EnableOut": "DO_01" } }
    ]
  },
  "Communication": {
    "Servers": [
      { "Type": "OPCUA", "Endpoint": "opc.tcp://10.0.0.10:4840", "NamespaceUri": "urn:packaging:line01",
        "Expose": [ { "NodeId": "ns=2;s=AxisX.ActPos", "SourceVar": "AxisX.ActPos" },
                    { "NodeId": "ns=2;s=CycleCount",   "SourceVar": "CycleCount" } ] }
    ],
    "Clients": [
      { "Type": "ModbusTCP", "Server": "10.0.0.20:502", "Mappings": [ { "HoldingReg": 40001, "Var": "GVL.Setpoint" } ] }
    ]
  },
  "HMI": {
    "Tags": [
      { "Name": "StartPB", "Var": "GVL.StartPB", "Rw": "Write" },
      { "Name": "ActPos",  "Var": "AxisX.ActPos", "Rw": "Read", "Format": "0.00" }
    ],
    "Screens": [ { "Name": "Main", "Bindings": [ "StartPB", "ActPos" ] } ]
  },
  "Graphics": {
    "Editors": [
      { "Pou": "AxisCtrl", "Language": "FBD", "Canvas": { "Width": 1600, "Height": 900, "Snap": 10, "Zoom": 1.0 } }
    ]
  },
  "Constraints": { "CycleBudgetMs": 1.0, "Memory": { "RamKB": 512, "NvKB": 128 },
    "Watchdogs": [ { "Task": "Cyclic10ms", "TimeoutMs": 50 } ] },
  "Localization": {
    "Languages": ["ko-KR", "en-US"],
    "Strings": { "ko-KR": { "StartPB": "시작버튼" }, "en-US": { "StartPB": "Start Button" } }
  },
  "Units": { "Default": "SI", "Overrides": { "AxisX.Position": "mm", "AxisX.Velocity": "mm/s" } },
  "VendorExtensions": {
    "TwinCAT3": { "TaskClass": "TcTask", "Pragmas": ["{attribute 'call_after_init'}"] },
    "CODESYS": { "CompilerSwitches": ["/no_var_opt"], "GvlFolder": "Globals" }
  }
}
```

---

## MVP Example (Smallest Useful Subset)
```json
{
  "IrVersion": "1.0.0",
  "Project": { "Name": "Demo", "TimeBase": "ms" },
  "Variables": {
    "Global": [
      { "Name": "AxisX", "Type": "AXIS_REF" },
      { "Name": "StartPB", "Type": "BOOL" }
    ]
  },
  "Tasks": [
    { "Name": "Cyclic10ms", "Interval": 10, "Priority": 1,
      "ProgramCalls": [ { "PouRef": "Main", "Order": 1 } ] }
  ],
  "POUs": [
    { "Name": "Main", "Kind": "Program", "Language": "ST",
      "Interface": { "LocalVars": [ { "Name": "MoveAbs", "Type": "MC_MoveAbsolute" } ] },
      "Body": { "ST": "MoveAbs(Axis := AxisX, Position := 100.0, Velocity := 50.0, Execute := StartPB);" } }
  ],
  "Motion": {
    "Axes": [ { "Name": "AxisX" } ],
    "FbMapping": [ { "Standard": "MC_MoveAbsolute", "Targets": [ { "Profile": "Generic", "Fb": "MC_MoveAbsolute" } ] } ]
  }
}
```
