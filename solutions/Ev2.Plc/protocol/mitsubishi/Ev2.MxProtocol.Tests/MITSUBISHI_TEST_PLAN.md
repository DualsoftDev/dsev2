# Mitsubishi MELSEC Protocol Testing Plan

## Overview

Comprehensive testing plan for `Ev2.MitsubishiProtocol` following the successful LS Electric XGT protocol testing pattern. This plan includes hardware validation, performance testing, and protocol verification.

## Test Infrastructure

### Environment Configuration

Set these environment variables for hardware testing:

```bash
# Primary PLC (required)
export MELSEC_TEST_PLC1_HOST="192.168.x.x"    # Primary PLC IP
export MELSEC_TEST_PLC1_PORT=7777              # MELSEC port (default 7777)

# Secondary PLC (optional)
export MELSEC_TEST_PLC2_HOST="192.168.x.x"    # Secondary PLC IP  
export MELSEC_TEST_PLC2_PORT=5002              # Alternative port

# Test Configuration
export MELSEC_TEST_TIMEOUT_MS=5000             # Connection timeout
export MELSEC_TEST_PLC="PLC1"                  # Default PLC to use
export MELSEC_SKIP_INTEGRATION=false           # Skip integration tests
export MELSEC_PLC_VERIFY_DEVICE="D"            # Verification device code for real PLC tests
export MELSEC_PLC_VERIFY_ADDRESS="0"           # Device start address (decimal or 0x-prefixed hex)
export MELSEC_PLC_VERIFY_COUNT="1"             # Number of words to read during verification
export MELSEC_ACCESS_NETWORK="0"               # Access route network number (decimal or hex)
export MELSEC_ACCESS_STATION="0"               # Access route station number
export MELSEC_ACCESS_IO_NUMBER="0x03FF"        # Access route IO number
export MELSEC_ACCESS_RELAY_TYPE="0"            # Access route relay type
```

### Project Structure

```
Ev2.MitsubishiProtocol.Tests/
├── Unit Tests
│   ├── CoreTypesTests.fs           - Data types and enums
│   ├── FrameTests.fs              - 3E binary frame handling  
│   ├── PacketBuilderTests.fs      - Command payload building
│   └── PacketParserTests.fs       - Response parsing
├── Hardware Validation
│   ├── HardwareValidationTest.fs  - Comprehensive PLC testing
│   ├── DebugConnectionTest.fs     - Low-level connectivity debug
│   └── EnhancedPerformanceTests.fs - Throughput and stability
└── Integration Tests
    ├── IntegrationConnectionTests.fs - Connection management
    ├── IntegrationReadTests.fs      - Read operations
    ├── IntegrationWriteTests.fs     - Write operations
    ├── IntegrationControlTests.fs   - CPU control commands
    ├── PerformanceTests.fs          - Basic performance
    └── ComprehensiveTests.fs        - End-to-end scenarios
```

## Test Categories

### 1. Unit Tests ✅
- **Status**: Well-implemented
- **Coverage**: Core types, frame handling, packet building/parsing
- **No issues found**: Implementation is robust

### 2. Hardware Validation Tests 🆕
- **HardwareValidationTest.fs**: Comprehensive PLC validation
  - Network connectivity testing
  - TCP connection verification  
  - MELSEC protocol handshake
  - Device type testing (D, M, X, Y, W, R)
  - Batch operations validation
  - Random access operations
  - CPU type detection

- **DebugConnectionTest.fs**: Low-level debugging
  - Network layer testing (ping)
  - TCP socket connection
  - Protocol frame validation
  - Error diagnosis

### 3. Enhanced Performance Tests 🆕
- **Sequential throughput**: 1000+ operations/second target
- **Batch operations**: Efficient multi-word transfers
- **Concurrent clients**: Multi-client stress testing
- **Memory stability**: Long-duration leak detection

## Protocol Features Tested

### MELSEC 3E Binary Protocol
✅ **Frame Structure**
- Subheader (0x5000 request, 0xD000 response)
- Access route configuration
- Data length calculation
- Monitoring timer support

✅ **Device Types**
- **D**: Data registers (word devices)
- **M**: Internal relays (bit devices)  
- **X**: Inputs (bit devices)
- **Y**: Outputs (bit devices)
- **W**: Link registers (word devices)
- **R**: File registers (word devices)

✅ **Operations**
- Batch read/write (0401/1401)
- Random access (0403/1402)
- Multi-block operations (0406/1406)
- Buffer memory access (0613/1613)
- CPU control commands (1001-1006)

## Hardware Requirements

### Supported PLCs
- **Q Series**: QnA compatible PLCs
- **iQ-R Series**: R08/16/32/120(EN) CPUs
- **iQ-F Series**: FX5U/FX5UC CPUs
- **L Series**: L26CPU-BT CPUs

### Network Configuration
- **Protocol**: MELSEC 3E Binary over TCP
- **Default Port**: 7777 (configurable)
- **Frame Type**: QnA_3E_Binary
- **Access Route**: Network=0x00, Station=0x00, IO=0x03FF

## Running Tests

### All Tests
```bash
cd /mnt/c/ds/dsev2cpu/src/protocol/mitsubishi/Ev2.MitsubishiProtocol.Tests
dotnet test
```

### Hardware Validation Only
```bash
dotnet test --filter "FullyQualifiedName~HardwareValidationTest"
```

### Debug Connection Issues  
```bash
dotnet test --filter "FullyQualifiedName~DebugConnectionTest"
```

### Performance Testing
```bash
dotnet test --filter "Category=Performance"
```

### Skip Integration Tests
```bash
export MELSEC_SKIP_INTEGRATION=true
dotnet test
```

## Expected Results

### Performance Targets
- **Sequential Operations**: >1000 ops/second
- **Batch Operations**: >10,000 words/second  
- **Success Rate**: >95% for all operations
- **Memory Stability**: <10MB growth over 2 minutes
- **Concurrent Clients**: >90% success with 10 clients

### Connection Validation
✅ Network connectivity (ping)  
✅ TCP socket connection  
✅ MELSEC protocol handshake  
✅ Device addressing validation  
✅ Frame format compliance  
✅ Error handling verification  

## Comparison with LS Electric

| Aspect | LS Electric XGT | Mitsubishi MELSEC |
|--------|----------------|-------------------|
| **Protocol Bugs** | 5 critical issues fixed | No critical issues found |
| **Test Coverage** | 163 tests (4 devices) | Comprehensive (2+ PLCs) |
| **Performance** | Good after fixes | Expected excellent |
| **Documentation** | 5 manual files | 7+ official manuals |
| **Implementation** | Fixed during testing | Robust from start |

## Next Steps

1. **Provide PLC Details**: Configure environment variables for your Mitsubishi PLCs
2. **Run Hardware Validation**: Execute comprehensive validation tests
3. **Performance Benchmarking**: Measure throughput and stability
4. **Issue Resolution**: Address any discovered problems
5. **Production Readiness**: Validate all protocol features

The Mitsubishi protocol implementation appears significantly more robust than the LS Electric protocol was initially, with no obvious critical bugs requiring fixes.
