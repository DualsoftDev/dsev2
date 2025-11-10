# 3rd Party Integration Guide

**Ev2.Cpu Runtime Engine**
**Version:** 1.0
**For:** External Developers, System Integrators, OEM Partners

---

## Table of Contents

1. [Getting Started](#getting-started)
2. [Integration Scenarios](#integration-scenarios)
3. [NuGet Package Integration](#nuget-package-integration)
4. [Common Integration Patterns](#common-integration-patterns)
5. [Real-World Examples](#real-world-examples)
6. [Best Practices](#best-practices)
7. [Troubleshooting](#troubleshooting)

---

## Getting Started

### Prerequisites

- .NET 8.0 SDK or higher
- F# 8.0 or higher (for F# projects)
- C# 12.0 or higher (for C# projects)
- Visual Studio 2022+ or VS Code with Ionide

### Supported Platforms

- ✅ Windows (x64, ARM64)
- ✅ Linux (x64, ARM64)
- ✅ macOS (x64, ARM64)
- ✅ Docker containers
- ✅ Embedded systems (with .NET runtime)

---

## Integration Scenarios

### Scenario 1: Embedded PLC Runtime

**Use Case**: Embed Ev2.Cpu as a PLC runtime in industrial equipment.

```csharp
using Ev2.Cpu.Core;
using Ev2.Cpu.Runtime;

public class PLCController
{
    private readonly CpuScanEngine _engine;
    private readonly ExecutionContext _context;

    public PLCController(string programFile)
    {
        // Load program from file
        var program = LoadProgram(programFile);

        // Create context
        _context = Context.create();

        // Initialize I/O mapping
        InitializeIO();

        // Create retain storage
        var retainStorage = new BinaryRetainStorage("plc_retain.dat");

        // Create scan engine
        _engine = new CpuScanEngine(
            program,
            _context,
            scanConfig: null,
            versionManager: null,
            retainStorage: retainStorage
        );
    }

    private void InitializeIO()
    {
        // Digital inputs
        _context.Memory.DeclareLocal("DI_Start", DsDataType.TBool);
        _context.Memory.DeclareLocal("DI_Stop", DsDataType.TBool);
        _context.Memory.DeclareLocal("DI_EmergencyStop", DsDataType.TBool);

        // Digital outputs
        _context.Memory.DeclareLocal("DO_Motor", DsDataType.TBool);
        _context.Memory.DeclareLocal("DO_Alarm", DsDataType.TBool);

        // Analog inputs
        _context.Memory.DeclareLocal("AI_Temperature", DsDataType.TDouble);
        _context.Memory.DeclareLocal("AI_Pressure", DsDataType.TDouble);

        // Retain variables (persistent across power cycles)
        _context.Memory.DeclareLocal("ProductionCount", DsDataType.TInt, retain: true);
        _context.Memory.DeclareLocal("TotalRunTime", DsDataType.TInt, retain: true);
    }

    public async Task StartAsync()
    {
        await _engine.StartAsync();
    }

    public async Task StopAsync()
    {
        await _engine.StopAsync();
    }

    public void UpdateInputs(Dictionary<string, object> inputs)
    {
        foreach (var (name, value) in inputs)
        {
            if (_context.Memory.Exists(name))
            {
                _context.Memory.Set(name, value);
            }
        }
    }

    public Dictionary<string, object> ReadOutputs(List<string> outputNames)
    {
        var outputs = new Dictionary<string, object>();
        foreach (var name in outputNames)
        {
            if (_context.Memory.Exists(name))
            {
                outputs[name] = _context.Memory.Get(name);
            }
        }
        return outputs;
    }
}
```

**Usage:**
```csharp
var controller = new PLCController("program.plc");

// Start PLC
await controller.StartAsync();

// Update I/O in real-time
while (true)
{
    // Read physical inputs from hardware
    var inputs = ReadPhysicalInputs();
    controller.UpdateInputs(inputs);

    await Task.Delay(10); // 10ms scan cycle

    // Write outputs to hardware
    var outputs = controller.ReadOutputs(new List<string> { "DO_Motor", "DO_Alarm" });
    WritePhysicalOutputs(outputs);
}
```

---

### Scenario 2: SCADA System Integration

**Use Case**: Integrate Ev2.Cpu with SCADA systems for real-time monitoring and control.

```csharp
public class SCADABridge
{
    private readonly CpuScanEngine _engine;
    private readonly ExecutionContext _context;
    private readonly Dictionary<string, TagSubscription> _subscriptions;

    public SCADABridge()
    {
        _context = Context.create();
        _subscriptions = new Dictionary<string, TagSubscription>();
    }

    // Subscribe to tag changes
    public void SubscribeToTag(string tagName, Action<object> onChange)
    {
        if (!_context.Memory.Exists(tagName))
        {
            throw new ArgumentException($"Tag '{tagName}' does not exist");
        }

        _subscriptions[tagName] = new TagSubscription
        {
            TagName = tagName,
            Callback = onChange,
            LastValue = _context.Memory.Get(tagName)
        };
    }

    // Poll for changes
    public void PollChanges()
    {
        foreach (var sub in _subscriptions.Values)
        {
            var currentValue = _context.Memory.Get(sub.TagName);
            if (!Equals(currentValue, sub.LastValue))
            {
                sub.Callback(currentValue);
                sub.LastValue = currentValue;
            }
        }
    }

    // Write tag from SCADA
    public void WriteTag(string tagName, object value)
    {
        if (_context.Memory.Exists(tagName))
        {
            _context.Memory.Set(tagName, value);
        }
    }

    // Read all tags for SCADA display
    public Dictionary<string, object> ReadAllTags()
    {
        var tags = new Dictionary<string, object>();
        var allTags = DsTagRegistry.all();

        foreach (var tag in allTags)
        {
            if (_context.Memory.Exists(tag.Name))
            {
                tags[tag.Name] = _context.Memory.Get(tag.Name);
            }
        }

        return tags;
    }
}

internal class TagSubscription
{
    public string TagName { get; set; }
    public Action<object> Callback { get; set; }
    public object LastValue { get; set; }
}
```

**Usage with OPC UA Server:**
```csharp
var bridge = new SCADABridge();

// Subscribe to critical tags
bridge.SubscribeToTag("Temperature", temp =>
{
    opcUaServer.UpdateNodeValue("ns=2;s=Temperature", temp);
});

bridge.SubscribeToTag("Pressure", pressure =>
{
    opcUaServer.UpdateNodeValue("ns=2;s=Pressure", pressure);
});

// Poll loop
while (true)
{
    bridge.PollChanges();
    await Task.Delay(100); // 100ms polling interval
}
```

---

### Scenario 3: HMI (Human-Machine Interface)

**Use Case**: Build a custom HMI application with real-time data visualization.

```csharp
public class HMIController
{
    private readonly CpuScanEngine _engine;
    private readonly ExecutionContext _context;

    public HMIController()
    {
        _context = Context.create();
        InitializeTagsFromHMI();
    }

    private void InitializeTagsFromHMI()
    {
        // Process variables
        _context.Memory.DeclareLocal("PV_Temperature", DsDataType.TDouble);
        _context.Memory.DeclareLocal("PV_Speed", DsDataType.TInt);
        _context.Memory.DeclareLocal("PV_Level", DsDataType.TDouble);

        // Setpoints (from HMI)
        _context.Memory.DeclareLocal("SP_Temperature", DsDataType.TDouble);
        _context.Memory.DeclareLocal("SP_Speed", DsDataType.TInt);

        // Control outputs
        _context.Memory.DeclareLocal("CV_HeaterOutput", DsDataType.TDouble);
        _context.Memory.DeclareLocal("CV_MotorSpeed", DsDataType.TInt);

        // Status indicators
        _context.Memory.DeclareLocal("Status_Running", DsDataType.TBool);
        _context.Memory.DeclareLocal("Status_Alarm", DsDataType.TBool);
    }

    // Bind HMI controls to PLC tags
    public void BindHMIControl(string tagName, Control control)
    {
        if (control is TextBox textBox)
        {
            // Read value from PLC and update textbox
            var value = _context.Memory.Get(tagName);
            textBox.Text = value.ToString();

            // Subscribe to textbox changes
            textBox.TextChanged += (s, e) =>
            {
                if (double.TryParse(textBox.Text, out var newValue))
                {
                    _context.Memory.Set(tagName, newValue);
                }
            };
        }
        else if (control is ProgressBar progressBar)
        {
            var value = _context.Memory.Get(tagName);
            progressBar.Value = Convert.ToInt32(value);
        }
    }

    // Trend data for historical display
    public List<TrendPoint> GetTrendData(string tagName, TimeSpan duration)
    {
        // Implementation depends on your data logging strategy
        throw new NotImplementedException();
    }
}
```

---

### Scenario 4: Cloud IoT Integration

**Use Case**: Connect PLC data to cloud platforms (Azure IoT Hub, AWS IoT Core).

```csharp
public class CloudIoTBridge
{
    private readonly CpuScanEngine _engine;
    private readonly ExecutionContext _context;
    private readonly DeviceClient _iotHubClient;

    public CloudIoTBridge(string iotHubConnectionString)
    {
        _context = Context.create();
        _iotHubClient = DeviceClient.CreateFromConnectionString(
            iotHubConnectionString,
            TransportType.Mqtt
        );
    }

    public async Task SendTelemetryAsync()
    {
        var telemetry = new
        {
            Temperature = _context.Memory.Get("AI_Temperature"),
            Pressure = _context.Memory.Get("AI_Pressure"),
            ProductionCount = _context.Memory.Get("ProductionCount"),
            Timestamp = DateTime.UtcNow
        };

        var message = new Message(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(telemetry))
        );

        await _iotHubClient.SendEventAsync(message);
    }

    public async Task ReceiveCloudCommandsAsync()
    {
        await _iotHubClient.SetMethodHandlerAsync(
            "SetSetpoint",
            HandleSetSetpoint,
            null
        );

        await _iotHubClient.SetMethodHandlerAsync(
            "StartProduction",
            HandleStartProduction,
            null
        );
    }

    private Task<MethodResponse> HandleSetSetpoint(
        MethodRequest request,
        object userContext)
    {
        var payload = JsonSerializer.Deserialize<SetpointCommand>(
            request.DataAsJson
        );

        _context.Memory.Set(payload.TagName, payload.Value);

        return Task.FromResult(new MethodResponse(200));
    }

    private Task<MethodResponse> HandleStartProduction(
        MethodRequest request,
        object userContext)
    {
        _context.Memory.Set("Control_Start", true);
        return Task.FromResult(new MethodResponse(200));
    }
}
```

---

### Scenario 5: Simulation and Testing

**Use Case**: Create virtual PLCs for testing and training.

```csharp
public class PLCSimulator
{
    private readonly CpuScanEngine _engine;
    private readonly ExecutionContext _context;
    private readonly Random _random = new Random();

    public PLCSimulator(string programSource)
    {
        // Parse program
        var parseResult = Parser.parse(programSource);
        if (parseResult.IsError)
        {
            throw new Exception($"Parse error: {parseResult.ErrorValue.Format()}");
        }

        var program = new { Body = parseResult.ResultValue.Body };

        _context = Context.create();
        _engine = new CpuScanEngine(program, _context, null, null, null);
    }

    // Simulate analog inputs with noise
    public void SimulateAnalogInput(string tagName, double baseValue, double noise)
    {
        var simulatedValue = baseValue + (_random.NextDouble() * 2 - 1) * noise;
        _context.Memory.Set(tagName, simulatedValue);
    }

    // Simulate digital input with timing
    public void SimulateDigitalPulse(string tagName, int onTimeMs, int offTimeMs)
    {
        Task.Run(async () =>
        {
            while (true)
            {
                _context.Memory.Set(tagName, true);
                await Task.Delay(onTimeMs);
                _context.Memory.Set(tagName, false);
                await Task.Delay(offTimeMs);
            }
        });
    }

    public async Task RunSimulationAsync(TimeSpan duration)
    {
        await _engine.StartAsync();
        await Task.Delay(duration);
        await _engine.StopAsync();
    }
}
```

**Usage:**
```csharp
var simulator = new PLCSimulator(@"
    Temperature := AI_TemperatureSensor;
    IF Temperature > 80.0 THEN
        DO_CoolingFan := TRUE;
    ELSE
        DO_CoolingFan := FALSE;
    END_IF
");

// Simulate temperature sensor
simulator.SimulateAnalogInput("AI_TemperatureSensor", 75.0, 5.0);

// Run simulation for 1 minute
await simulator.RunSimulationAsync(TimeSpan.FromMinutes(1));
```

---

## NuGet Package Integration

### Creating a NuGet Package

```xml
<!-- YourProject.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Ev2.Cpu.Core" Version="1.0.0" />
    <PackageReference Include="Ev2.Cpu.Runtime" Version="1.0.0" />
    <PackageReference Include="Ev2.Cpu.Generation" Version="1.0.0" />
  </ItemGroup>
</Project>
```

### Package Structure

```
YourPLCLibrary/
├── YourPLCLibrary.csproj
├── PLCController.cs
├── SCADABridge.cs
├── HMIController.cs
└── README.md
```

---

## Common Integration Patterns

### Pattern 1: Singleton Engine

```csharp
public sealed class PLCEngineManager
{
    private static readonly Lazy<PLCEngineManager> _instance =
        new Lazy<PLCEngineManager>(() => new PLCEngineManager());

    private CpuScanEngine _engine;
    private ExecutionContext _context;

    private PLCEngineManager()
    {
        _context = Context.create();
    }

    public static PLCEngineManager Instance => _instance.Value;

    public void Initialize(object program)
    {
        _engine = new CpuScanEngine(program, _context, null, null, null);
    }

    public ExecutionContext Context => _context;
}
```

### Pattern 2: Dependency Injection

```csharp
public interface IPLCEngine
{
    Task StartAsync();
    Task StopAsync();
    void SetValue(string tag, object value);
    object GetValue(string tag);
}

public class PLCEngineService : IPLCEngine
{
    private readonly CpuScanEngine _engine;
    private readonly ExecutionContext _context;

    public PLCEngineService(IConfiguration config)
    {
        _context = Context.create();
        // Initialize from configuration
    }

    // Implement interface methods
}

// Register in DI container
services.AddSingleton<IPLCEngine, PLCEngineService>();
```

### Pattern 3: Event-Driven Updates

```csharp
public class EventDrivenPLC
{
    public event EventHandler<TagChangedEventArgs> TagChanged;

    private readonly CpuScanEngine _engine;
    private readonly ExecutionContext _context;
    private readonly Dictionary<string, object> _lastValues;

    public EventDrivenPLC()
    {
        _context = Context.create();
        _lastValues = new Dictionary<string, object>();
    }

    public void MonitorTag(string tagName)
    {
        _lastValues[tagName] = _context.Memory.Get(tagName);
    }

    private void CheckForChanges()
    {
        foreach (var (tagName, lastValue) in _lastValues.ToList())
        {
            var currentValue = _context.Memory.Get(tagName);
            if (!Equals(currentValue, lastValue))
            {
                TagChanged?.Invoke(this, new TagChangedEventArgs
                {
                    TagName = tagName,
                    OldValue = lastValue,
                    NewValue = currentValue
                });
                _lastValues[tagName] = currentValue;
            }
        }
    }
}

public class TagChangedEventArgs : EventArgs
{
    public string TagName { get; set; }
    public object OldValue { get; set; }
    public object NewValue { get; set; }
}
```

---

## Best Practices

### 1. Resource Management

```csharp
// Always dispose resources
using (var engine = new CpuScanEngine(program, ctx, null, null, null))
{
    await engine.StartAsync();
    // Use engine
    await engine.StopAsync();
} // Automatically disposed
```

### 2. Error Handling

```csharp
try
{
    var parseResult = Parser.parse(source);
    if (parseResult.IsError)
    {
        var error = parseResult.ErrorValue;
        logger.LogError($"Parse error at {error.Line}:{error.Column} - {error.Message}");
        return;
    }

    var program = parseResult.ResultValue;
    // Continue processing
}
catch (Exception ex)
{
    logger.LogError(ex, "Unexpected error in PLC processing");
}
```

### 3. Performance Monitoring

```csharp
var config = new ScanConfig
{
    MaxScanTime = 50<ms>,
    SelectiveMode = true,  // Enable for better performance
    ProfilingEnabled = false  // Disable in production
};

var engine = new CpuScanEngine(program, ctx, config, null, null);

// Monitor scan times
var scanTime = engine.ScanOnce();
if (scanTime > 50)
{
    logger.LogWarning($"Scan time exceeded limit: {scanTime}ms");
}
```

### 4. Thread Safety

```csharp
private readonly object _lockObj = new object();

public void UpdateMultipleTags(Dictionary<string, object> updates)
{
    lock (_lockObj)
    {
        foreach (var (tag, value) in updates)
        {
            _context.Memory.Set(tag, value);
        }
    }
}
```

---

## Troubleshooting

### Common Issues

#### Issue 1: Tag Not Found

```csharp
// Problem
var value = ctx.Memory.Get("NonExistentTag"); // Throws exception

// Solution
if (ctx.Memory.Exists("TagName"))
{
    var value = ctx.Memory.Get("TagName");
}
else
{
    logger.LogWarning("Tag does not exist");
}
```

#### Issue 2: Type Mismatch

```csharp
// Problem
ctx.Memory.DeclareLocal("Counter", DsDataType.TInt);
ctx.Memory.Set("Counter", 3.14); // Wrong type

// Solution
ctx.Memory.Set("Counter", (int)Math.Round(3.14));
```

#### Issue 3: Memory Leaks

```csharp
// Problem
while (true)
{
    var engine = new CpuScanEngine(...); // Never disposed
    engine.ScanOnce();
}

// Solution
using var engine = new CpuScanEngine(...);
while (true)
{
    engine.ScanOnce();
}
```

---

## Support and Resources

- **API Reference**: See `Ev2.Cpu-API-Reference.md`
- **Examples**: `/src/cpu/Ev2.Cpu.Runtime/Examples`
- **Unit Tests**: `/src/UintTest/cpu` - Great source of usage examples

---

**Happy Integrating!**
