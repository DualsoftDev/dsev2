namespace Ev2.LsProtocol.Tests

open System
open Xunit
open Ev2.LsProtocol
open ProtocolTestHelper

module XgtTypesTests =

    [<Fact>]
    let ``CompanyHeader.IdBytes should be 12 bytes with correct content`` () =
        let idBytes = CompanyHeader.IdBytes
        Assert.Equal(12, idBytes.Length)
        
        // First 8 bytes should be "LSIS-XGT"
        let asciiPart = System.Text.Encoding.ASCII.GetString(idBytes, 0, 8)
        Assert.Equal("LSIS-XGT", asciiPart)
        
        // Remaining bytes should be zero
        Assert.Equal(0uy, idBytes.[8])
        Assert.Equal(0uy, idBytes.[9])
        Assert.Equal(0uy, idBytes.[10])
        Assert.Equal(0uy, idBytes.[11])

    [<Theory>]
    [<InlineData('P')>]
    [<InlineData('M')>]
    [<InlineData('K')>]
    [<InlineData('T')>]
    [<InlineData('C')>]
    [<InlineData('I')>]
    [<InlineData('Q')>]
    [<InlineData('F')>]
    let ``SupportedAreaCodes should contain expected codes`` (areaCode: char) =
        Assert.Contains(areaCode, SupportedAreaCodes.All)

    [<Fact>]
    let ``toDataTypeCode should map Bool to Bit`` () =
        let result = toDataTypeCode PlcTagDataType.Bool
        Assert.Equal(DataType.Bit, result)

    [<Fact>]
    let ``toDataTypeCode should map Int8 to Byte`` () =
        let result = toDataTypeCode PlcTagDataType.Int8
        Assert.Equal(DataType.Byte, result)

    [<Fact>]
    let ``toDataTypeCode should map UInt8 to Byte`` () =
        let result = toDataTypeCode PlcTagDataType.UInt8
        Assert.Equal(DataType.Byte, result)

    [<Fact>]
    let ``toDataTypeCode should map Int16 to Word`` () =
        let result = toDataTypeCode PlcTagDataType.Int16
        Assert.Equal(DataType.Word, result)

    [<Fact>]
    let ``toDataTypeCode should map UInt16 to Word`` () =
        let result = toDataTypeCode PlcTagDataType.UInt16
        Assert.Equal(DataType.Word, result)

    [<Fact>]
    let ``toDataTypeCode should map Int32 to DWord`` () =
        let result = toDataTypeCode PlcTagDataType.Int32
        Assert.Equal(DataType.DWord, result)

    [<Fact>]
    let ``toDataTypeCode should map UInt32 to DWord`` () =
        let result = toDataTypeCode PlcTagDataType.UInt32
        Assert.Equal(DataType.DWord, result)

    [<Fact>]
    let ``toDataTypeCode should map Float32 to DWord`` () =
        let result = toDataTypeCode PlcTagDataType.Float32
        Assert.Equal(DataType.DWord, result)

    [<Fact>]
    let ``toDataTypeCode should map Int64 to LWord`` () =
        let result = toDataTypeCode PlcTagDataType.Int64
        Assert.Equal(DataType.LWord, result)

    [<Fact>]
    let ``toDataTypeCode should map UInt64 to LWord`` () =
        let result = toDataTypeCode PlcTagDataType.UInt64
        Assert.Equal(DataType.LWord, result)

    [<Fact>]
    let ``toDataTypeCode should map Float64 to LWord`` () =
        let result = toDataTypeCode PlcTagDataType.Float64
        Assert.Equal(DataType.LWord, result)

    [<Fact>]
    let ``toDataTypeChar should return X for Bool`` () =
        let result = toDataTypeChar PlcTagDataType.Bool
        Assert.Equal('X', result)

    [<Fact>]
    let ``toDataTypeChar should return B for Int8`` () =
        let result = toDataTypeChar PlcTagDataType.Int8
        Assert.Equal('B', result)

    [<Fact>]
    let ``toDataTypeChar should return B for UInt8`` () =
        let result = toDataTypeChar PlcTagDataType.UInt8
        Assert.Equal('B', result)

    [<Fact>]
    let ``toDataTypeChar should return W for Int16`` () =
        let result = toDataTypeChar PlcTagDataType.Int16
        Assert.Equal('W', result)

    [<Fact>]
    let ``toDataTypeChar should return W for UInt16`` () =
        let result = toDataTypeChar PlcTagDataType.UInt16
        Assert.Equal('W', result)

    [<Fact>]
    let ``toDataTypeChar should return D for Int32`` () =
        let result = toDataTypeChar PlcTagDataType.Int32
        Assert.Equal('D', result)

    [<Fact>]
    let ``toDataTypeChar should return D for UInt32`` () =
        let result = toDataTypeChar PlcTagDataType.UInt32
        Assert.Equal('D', result)

    [<Fact>]
    let ``toDataTypeChar should return D for Float32`` () =
        let result = toDataTypeChar PlcTagDataType.Float32
        Assert.Equal('D', result)

    [<Fact>]
    let ``toDataTypeChar should return L for Int64`` () =
        let result = toDataTypeChar PlcTagDataType.Int64
        Assert.Equal('L', result)

    [<Fact>]
    let ``toDataTypeChar should return L for UInt64`` () =
        let result = toDataTypeChar PlcTagDataType.UInt64
        Assert.Equal('L', result)

    [<Fact>]
    let ``toDataTypeChar should return L for Float64`` () =
        let result = toDataTypeChar PlcTagDataType.Float64
        Assert.Equal('L', result)

    [<Fact>]
    let ``bitSize should return 1 for Bool`` () =
        let result = bitSize PlcTagDataType.Bool
        Assert.Equal(1, result)

    [<Fact>]
    let ``bitSize should return 8 for Int8`` () =
        let result = bitSize PlcTagDataType.Int8
        Assert.Equal(8, result)

    [<Fact>]
    let ``bitSize should return 8 for UInt8`` () =
        let result = bitSize PlcTagDataType.UInt8
        Assert.Equal(8, result)

    [<Fact>]
    let ``bitSize should return 16 for Int16`` () =
        let result = bitSize PlcTagDataType.Int16
        Assert.Equal(16, result)

    [<Fact>]
    let ``bitSize should return 16 for UInt16`` () =
        let result = bitSize PlcTagDataType.UInt16
        Assert.Equal(16, result)

    [<Fact>]
    let ``bitSize should return 32 for Int32`` () =
        let result = bitSize PlcTagDataType.Int32
        Assert.Equal(32, result)

    [<Fact>]
    let ``bitSize should return 32 for UInt32`` () =
        let result = bitSize PlcTagDataType.UInt32
        Assert.Equal(32, result)

    [<Fact>]
    let ``bitSize should return 32 for Float32`` () =
        let result = bitSize PlcTagDataType.Float32
        Assert.Equal(32, result)

    [<Fact>]
    let ``bitSize should return 64 for Int64`` () =
        let result = bitSize PlcTagDataType.Int64
        Assert.Equal(64, result)

    [<Fact>]
    let ``bitSize should return 64 for UInt64`` () =
        let result = bitSize PlcTagDataType.UInt64
        Assert.Equal(64, result)

    [<Fact>]
    let ``bitSize should return 64 for Float64`` () =
        let result = bitSize PlcTagDataType.Float64
        Assert.Equal(64, result)

    [<Fact>]
    let ``byteSize should return 1 for Bool`` () =
        let result = byteSize PlcTagDataType.Bool
        Assert.Equal(1, result)

    [<Fact>]
    let ``byteSize should return 1 for Int8`` () =
        let result = byteSize PlcTagDataType.Int8
        Assert.Equal(1, result)

    [<Fact>]
    let ``byteSize should return 1 for UInt8`` () =
        let result = byteSize PlcTagDataType.UInt8
        Assert.Equal(1, result)

    [<Fact>]
    let ``byteSize should return 2 for Int16`` () =
        let result = byteSize PlcTagDataType.Int16
        Assert.Equal(2, result)

    [<Fact>]
    let ``byteSize should return 2 for UInt16`` () =
        let result = byteSize PlcTagDataType.UInt16
        Assert.Equal(2, result)

    [<Fact>]
    let ``byteSize should return 4 for Int32`` () =
        let result = byteSize PlcTagDataType.Int32
        Assert.Equal(4, result)

    [<Fact>]
    let ``byteSize should return 4 for UInt32`` () =
        let result = byteSize PlcTagDataType.UInt32
        Assert.Equal(4, result)

    [<Fact>]
    let ``byteSize should return 4 for Float32`` () =
        let result = byteSize PlcTagDataType.Float32
        Assert.Equal(4, result)

    [<Fact>]
    let ``byteSize should return 8 for Int64`` () =
        let result = byteSize PlcTagDataType.Int64
        Assert.Equal(8, result)

    [<Fact>]
    let ``byteSize should return 8 for UInt64`` () =
        let result = byteSize PlcTagDataType.UInt64
        Assert.Equal(8, result)

    [<Fact>]
    let ``byteSize should return 8 for Float64`` () =
        let result = byteSize PlcTagDataType.Float64
        Assert.Equal(8, result)

    [<Theory>]
    [<InlineData(0x10uy, "Unsupported command.")>]
    [<InlineData(0x11uy, "Command format error.")>]
    [<InlineData(0x21uy, "Frame checksum (BCC) error.")>]
    [<InlineData(0x99uy, "Unknown error code: 0x99")>]
    let ``getXgtErrorDescription should return correct descriptions`` (errorCode: byte) (expectedDescription: string) =
        let result = getXgtErrorDescription errorCode
        Assert.Equal(expectedDescription, result)

    [<Fact>]
    let ``String dataType bitSize calculation`` () =
        let stringType = PlcTagDataType.String 10
        let result = bitSize stringType
        Assert.Equal(80, result) // 10 * 8 bits

    [<Fact>]
    let ``Bytes dataType bitSize calculation`` () =
        let bytesType = PlcTagDataType.Bytes 5
        let result = bitSize bytesType
        Assert.Equal(40, result) // 5 * 8 bits

    [<Fact>]
    let ``Array dataType bitSize calculation`` () =
        let arrayType = PlcTagDataType.Array(PlcTagDataType.UInt16, 4)
        let result = bitSize arrayType
        Assert.Equal(64, result) // 16 * 4 bits

    [<Fact>]
    let ``Struct dataType bitSize calculation`` () =
        let structType = PlcTagDataType.Struct [
            ("field1", PlcTagDataType.UInt16)
            ("field2", PlcTagDataType.UInt32)
        ]
        let result = bitSize structType
        Assert.Equal(48, result) // 16 + 32 bits

    [<Fact>]
    let ``toDataTypeCode should throw for unsupported types`` () =
        let stringType = PlcTagDataType.String 10
        Assert.Throws<ArgumentException>(fun () -> toDataTypeCode stringType |> ignore)

    [<Fact>]
    let ``toDataTypeChar should throw for unsupported types`` () =
        let stringType = PlcTagDataType.String 10
        Assert.Throws<ArgumentException>(fun () -> toDataTypeChar stringType |> ignore)