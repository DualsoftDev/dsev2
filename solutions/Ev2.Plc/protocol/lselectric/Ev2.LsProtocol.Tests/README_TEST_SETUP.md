# XGI LS Electric Protocol Test Setup

## Test Configuration

### Hardware Devices
- **EFMTB Device**: 192.168.9.100 (Port 2004, EFMTB mode)
- **LocalEthernet Device**: 192.168.9.102 (Port 2004, LocalEthernet mode)

### XGI Memory Areas Tested
- **M**: Memory area (MW100, MD100, M100)
- **I**: Input area (IW10, I10)
- **Q**: Output area (QW20, Q20)
- **F**: File register area (FW50, FD50, F50)
- **L**: Link register area (LW5, LD5, L5)

## Protocol Bug Fixes Applied

### 1. ReceiveFrame Complete Buffer Reading
- **Issue**: `stream.Read()` might not read all requested bytes
- **Fix**: Loop until complete buffer is read with connection failure detection

### 2. Response Buffer Size Calculation
- **Issue**: Fixed heuristic instead of actual data type sizes
- **Fix**: Calculate buffer size based on actual data types

### 3. Multi-Read Parser Variable Stride
- **Issue**: Fixed 10-byte stride regardless of data sizes
- **Fix**: Adaptive stride for variable-length data blocks

### 4. Frame ID Generation
- **Issue**: Potential truncation in 2-byte Frame ID
- **Fix**: Proper bitwise operations for IP and port

### 5. Network Timeout Application
- **Issue**: Timeouts not applied to TCP operations
- **Fix**: ReadTimeout and WriteTimeout applied to all operations

## Test Files

### Core Tests
- `BugFixValidationTests.fs` - Unit tests for bug fixes
- `XgiAddressTests.fs` - XGI address parsing and validation
- `HardwareValidationTest.fs` - Comprehensive hardware validation
- `DebugConnectionTest.fs` - Connection debugging for both devices

### Integration Tests
- `IntegrationConnectionTests.fs` - Connection tests
- `IntegrationReadTests.fs` - XGI read operations (updated for XGI only)
- `IntegrationWriteTests.fs` - XGI write operations
- `PerformanceTests.fs` - Performance benchmarking

## Usage

### Environment Variables
```
XGT_TEST_IP=192.168.9.100          # Primary test IP
XGT_TEST_PORT=2004                 # Test port
XGT_TIMEOUT_MS=5000                # Timeout in milliseconds
XGT_SKIP_INTEGRATION=false         # Set to true to skip integration tests
```

### Running Tests
```bash
# All tests
dotnet test

# Hardware validation only
dotnet test --filter "XGI PLC comprehensive validation"

# Debug connection tests
dotnet test --filter "Debug EFMTB connection"
dotnet test --filter "Debug LocalEthernet connection"

# XGI address tests
dotnet test --filter "XgiAddressTests"

# Integration tests (requires actual hardware)
dotnet test --filter "Integration"
```

### Test Results Expected
1. **Connection Tests**: Both EFMTB and LocalEthernet connections should succeed
2. **Address Parsing**: All XGI address formats should parse correctly
3. **Single Reads**: Individual register reads should complete without errors
4. **Multi-Reads**: Variable-length data blocks should be parsed correctly
5. **Bug Fix Validation**: All protocol fixes should be verified

## Notes
- XGK address types are not included (to be added later)
- Tests focus on XGI CPU address spaces only
- Both EFMTB and LocalEthernet modes are tested on separate devices
- All critical protocol bugs have been fixed and validated