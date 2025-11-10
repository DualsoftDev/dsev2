# Complete LS Electric XGT Protocol Test Setup

## 🎯 **Device Configuration**

### **XGI Devices (Original)**
- **XGI EFMTB**: 192.168.9.100 (Port 2004, EFMTB mode)
- **XGI LocalEthernet**: 192.168.9.102 (Port 2004, LocalEthernet mode)

### **XGK Devices (New)**  
- **XGK EFMTB**: 192.168.9.103 (Port 2004, EFMTB mode)
- **XGK LocalEthernet**: 192.168.9.105 (Port 2004, LocalEthernet mode)

## 📋 **Address Types Supported**

### **XGI Memory Areas**
- **M**: Memory area (M100, MW100, MD100)
- **I**: Input area (I10, IW10, ID10)  
- **Q**: Output area (Q20, QW20, QD20)
- **F**: File register area (F50, FW50, FD50)
- **L**: Link register area (L5, LW5, LD5)

### **XGK Memory Areas**
- **P**: Input area (P100, PW100, PD100)
- **M**: Memory area (M200, MW200, MD200)
- **K**: Keep relay area (K50, KW50, KD50)
- **T**: Timer area (T5, TW5, TD5)
- **C**: Counter area (C8, CW8, CD8)
- **D**: Data register area (D1000, DW1000, DD1000)

## 🧪 **Test File Structure**

### **Core Protocol Tests**
- `BugFixValidationTests.fs` - Protocol bug fix validation
- `XgtResponseTests.fs` - Response parsing tests
- `XgtTypesTests.fs` - Data type conversion tests
- `XgtTagTests.fs` - Address tag handling tests

### **XGI-Specific Tests**
- `XgiAddressTests.fs` - XGI address parsing and validation
- `DebugConnectionTest.fs` - XGI connection debugging
- `HardwareValidationTest.fs` - XGI hardware validation

### **XGK-Specific Tests (New)**
- `XgkAddressTests.fs` - XGK address parsing and validation
- `XgkDebugConnectionTest.fs` - XGK connection debugging
- `ComprehensiveHardwareValidationTest.fs` - All 4 devices validation

### **Integration Tests**
- `Integration/IntegrationReadTests.fs` - XGI read operations
- `Integration/IntegrationWriteTests.fs` - XGI write operations
- `Integration/XgkIntegrationTests.fs` - XGK read/write operations
- `Integration/PerformanceTests.fs` - Performance benchmarking

## 🔧 **Protocol Features Validated**

### ✅ **Critical Bug Fixes**
1. **ReceiveFrame Complete Reading** - Ensures all bytes are read
2. **Response Buffer Size Calculation** - Based on actual data types
3. **Multi-Read Parser Variable Stride** - Handles different data sizes
4. **Frame ID 2-byte Generation** - Proper identifier creation
5. **Network Timeout Application** - Applied to all TCP operations

### ✅ **Protocol Requirements**
- **Same Data Type Rule**: All addresses in one frame must have same data type
- **Address Format**: Uses `%` prefix with data type chars (MW, PD, etc.)
- **Connection Modes**: EFMTB vs LocalEthernet properly handled
- **CPU Type Support**: Both XGI and XGK addressing schemes

## 🚀 **Running Tests**

### **Environment Variables**
```bash
XGT_TEST_IP=192.168.9.100          # Primary test IP (can be any of the 4)
XGT_TEST_PORT=2004                 # Test port
XGT_TIMEOUT_MS=5000                # Timeout in milliseconds
XGT_SKIP_INTEGRATION=false         # Set to true to skip integration tests
```

### **Test Commands**

#### **All Tests**
```bash
dotnet test
```

#### **Device-Specific Tests**
```bash
# XGI devices
dotnet test --filter "XgiAddressTests"
dotnet test --filter "Debug EFMTB connection"
dotnet test --filter "Debug LocalEthernet connection"

# XGK devices  
dotnet test --filter "XgkAddressTests"
dotnet test --filter "Debug XGK EFMTB connection"
dotnet test --filter "Debug XGK LocalEthernet connection"

# Comprehensive validation (all 4 devices)
dotnet test --filter "Comprehensive validation of all PLC devices"
```

#### **Integration Tests by CPU Type**
```bash
# XGI integration tests
dotnet test --filter "IntegrationReadTests"
dotnet test --filter "IntegrationWriteTests"

# XGK integration tests
dotnet test --filter "XgkIntegrationTests"
```

#### **Performance Tests**
```bash
dotnet test --filter "PerformanceTests"
```

## 📊 **Expected Test Results**

### **Unit Tests** (Should be 100% passing)
- `BugFixValidationTests`: 2/2 ✅
- `XgiAddressTests`: 3/3 ✅  
- `XgkAddressTests`: 3/3 ✅
- `XgtResponseTests`: 15/15 ✅
- `XgtTagTests`: 40/40 ✅
- `XgtTypesTests`: 62/62 ✅

### **Hardware Tests** (Dependent on network/devices)
- `DebugConnectionTest`: 2/2 ✅
- `XgkDebugConnectionTest`: 2/2 ✅
- `ComprehensiveHardwareValidationTest`: 1/1 ✅

### **Integration Tests** (Dependent on PLC availability)
- XGI Integration: 5/5 tests per device
- XGK Integration: 10/10 tests per device
- Performance: 5/5 tests

## 🔍 **Troubleshooting**

### **Common Issues**
1. **Connection Drops**: "원격 호스트에 의해 강제로 끊겼습니다"
   - **Solution**: Check network stability, reduce test concurrency
   
2. **Address Not Supported**: "Unsupported address format"
   - **Solution**: Verify CPU type (XGI vs XGK) and use correct address format
   
3. **Data Type Mismatch**: "All data types in a single XGT frame must match"
   - **Solution**: Use same data type for all addresses in multi-read operations

### **Network Configuration**
- Ensure all 4 PLC devices are accessible on the network
- Verify firewall settings allow TCP port 2004
- Test basic ping connectivity before running protocol tests

## 🎯 **Success Criteria**

- **Unit Tests**: 125+ tests passing (100% success rate)
- **Hardware Tests**: Device connectivity validated for all 4 PLCs
- **Integration Tests**: Read/write operations successful for both XGI and XGK
- **Protocol Bugs**: All 5 critical issues resolved and validated

The LS Electric XGT protocol implementation now supports comprehensive testing across both XGI and XGK CPU types with EFMTB and LocalEthernet communication modes.