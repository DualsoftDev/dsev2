# Ev2.PLC.Common.FS

Universal PLC Common Library for Engine V2 - Vendor-agnostic types and interfaces for industrial PLC communications.

## Overview

This library provides a unified type system and interface definitions that work across all PLC vendors, enabling consistent development of PLC drivers and applications regardless of the underlying hardware or protocol.

## Supported Vendors

- **Allen-Bradley** (ControlLogix, CompactLogix, MicroLogix, etc.)
- **Siemens** (S7-300, S7-400, S7-1200, S7-1500, etc.)
- **Mitsubishi** (FX, Q-Series, L-Series, etc.)
- **LS Electric** (XGI, XGK, XGB series, etc.)
- **Generic/Custom** PLCs via extensible framework

## Core Components

### 1. Core Data Types (`CoreDataTypes.fs`)
- **`PlcDataType`** - Universal data type enumeration
- **`PlcValue`** - Universal value container with type safety
- **Type conversion and validation utilities**

### 2. Quality Management (`QualityTypes.fs`)
- **`DataQuality`** - Good/Uncertain/Bad quality indicators
- **`DataStatus`** - Extended status with metadata
- **Quality statistics and trend analysis**

### 3. Connection Management (`ConnectionTypes.fs`)
- **`PlcConnectionStatus`** - Universal connection states
- **`ConnectionConfig`** - Vendor-agnostic connection configuration
- **Connection metrics and monitoring**

### 4. Scanning & Data Collection (`ScanTypes.fs`)
- **`ScanResult`** - Individual tag scan results
- **`ScanBatch`** - Batch operation results
- **Performance metrics and scheduling**

### 5. Tag Management (`TagTypes.fs`)
- **`TagConfiguration`** - Universal tag configuration
- **`TagGroup`** - Tag organization and grouping
- **Tag templates and bulk operations**

### 6. Diagnostics (`DiagnosticsTypes.fs`)
- **`PlcDiagnostics`** - Comprehensive diagnostic information
- **`PerformanceMetrics`** - Performance monitoring
- **Health status and alerting**

### 7. Universal Interfaces (`IPlcDriver.fs`)
- **`IPlcDriver`** - Core driver interface
- **`IAdvancedPlcDriver`** - Extended capabilities
- **`IPlcDriverFactory`** - Driver factory pattern

## Key Features

### 🔄 Universal Data Model
- Single data type system works across all vendors
- Automatic type conversion and validation
- Support for complex data types (arrays, structures)

### 📊 Quality Management
- OPC-UA inspired quality model
- Quality statistics and trending
- Configurable quality thresholds

### 🔌 Flexible Connections
- TCP, UDP, Serial, USB support
- Automatic reconnection with backoff
- Connection pooling and metrics

### ⚡ Performance Monitoring
- Real-time performance metrics
- Response time percentiles
- Throughput monitoring

### 🏷️ Advanced Tag Management
- Hierarchical tag organization
- Template-based tag creation
- Bulk operations support

### 🩺 Comprehensive Diagnostics
- Health monitoring
- Performance analysis
- Trend detection

## Usage Example

```fsharp
// Create a tag configuration
let tagConfig = TagConfiguration.Create(
    id = "Tank01_Level",
    plcId = "PLC_001", 
    name = "Tank 1 Level",
    address = PlcAddress.Create("DB1.DBD0"),
    dataType = Float32
)

// Universal driver interface
let driver: IPlcDriver = factory.CreateDriver("PLC_001", "Siemens", connectionConfig)

// Read tag value
let! result = driver.ReadTagAsync(tagConfig)
match result with
| Ok scanResult -> 
    printfn "Value: %A, Quality: %A" scanResult.Value scanResult.Quality
| Error error -> 
    printfn "Read failed: %s" error
```

## Architecture Benefits

### For Driver Developers
- **Consistent Interface**: Same interface across all vendors
- **Built-in Features**: Quality, diagnostics, and performance monitoring included
- **Type Safety**: Compile-time guarantees for data operations
- **Extensibility**: Easy to add vendor-specific features

### For Application Developers  
- **Vendor Independence**: Switch between PLCs without code changes
- **Rich Metadata**: Quality, timestamps, and diagnostics included
- **Performance**: Optimized for industrial applications
- **Reliability**: Proven patterns for connection management

### For System Integrators
- **Unified Configuration**: Same configuration model for all vendors
- **Monitoring**: Built-in health and performance monitoring
- **Scalability**: Designed for large-scale industrial systems
- **Maintainability**: Clear separation of concerns

## Design Principles

1. **Vendor Agnostic**: Works with any PLC vendor
2. **Type Safe**: Compile-time guarantees
3. **Performance First**: Optimized for industrial use
4. **Quality Focused**: Quality is a first-class citizen
5. **Extensible**: Easy to add new features
6. **Testable**: Designed for unit testing
7. **Observable**: Built-in monitoring and diagnostics

## Version History

### 3.0.0 (Current)
- Complete rewrite with universal type system
- Vendor-agnostic design
- Enhanced quality management
- Comprehensive diagnostics
- Performance optimizations

### 2.x.x (Legacy)
- Vendor-specific implementations
- Basic type system
- Limited quality support

## Dependencies

- **.NET 8.0** - Modern .NET runtime
- **F# 8.0** - Latest F# compiler
- **System.Net** - Network communications

## License

© 2024 Dualsoft. All rights reserved.