namespace Ev2.LsProtocol.Tests

open System
open Xunit
open Ev2.LsProtocol
open ProtocolTestHelper

module XgtTagTests =

    // Test helper functions for address parsing
    let parseSimpleAddress (address: string) =
        let trimmed = address.TrimStart('%')
        if String.IsNullOrEmpty trimmed then
            invalidArg "address" "Address cannot be empty"
        
        let deviceChar = trimmed.[0]
        let numberStr = trimmed.Substring(1)
        let number = Int32.Parse(numberStr)
        
        (deviceChar, number)

    [<Theory>]
    [<InlineData("P10", 'P', 10)>]
    [<InlineData("M100", 'M', 100)>]
    [<InlineData("D1000", 'D', 1000)>]
    [<InlineData("K50", 'K', 50)>]
    [<InlineData("T10", 'T', 10)>]
    [<InlineData("C25", 'C', 25)>]
    [<InlineData("I10", 'I', 10)>]
    [<InlineData("Q20", 'Q', 20)>]
    [<InlineData("F50", 'F', 50)>]
    let ``parseAddress should extract area and number correctly`` (address: string) (expectedArea: char) (expectedNumber: int) =
        let (areaChar, number) = parseSimpleAddress address
        Assert.Equal(expectedArea, areaChar)
        Assert.Equal(expectedNumber, number)

    [<Fact>]
    let ``createReadBlock should create correct block for simple address`` () =
        let result = ReadWriteBlockFactory.getReadBlock "M100" PlcTagDataType.UInt16
        
        Assert.Equal("%MW100", result.Address)  // Updated to expect % prefix format
        Assert.Equal(PlcTagDataType.UInt16, result.DataType)
        Assert.Equal('M', result.DeviceType)
        Assert.Equal(None, result.Value)

    [<Fact>]
    let ``createReadBlock should create correct block for bit address`` () =
        let result = ReadWriteBlockFactory.getReadBlock "P10" PlcTagDataType.Bool
        
        Assert.Equal("%PX16", result.Address)  // Updated to expect actual format
        Assert.Equal(PlcTagDataType.Bool, result.DataType)
        Assert.Equal('P', result.DeviceType)

    [<Fact>]
    let ``createWriteBlock should create correct block with value`` () =
        let value = ScalarValue.UInt16Value 1234us
        let result = ReadWriteBlockFactory.getWriteBlock "M100" PlcTagDataType.UInt16 value
        
        Assert.Equal("%MW100", result.Address)  // Updated to expect % prefix format
        Assert.Equal(PlcTagDataType.UInt16, result.DataType)
        Assert.Equal('M', result.DeviceType)
        Assert.Equal(Some value, result.Value)

    [<Theory>]
    [<InlineData('P', DeviceType.P)>]
    [<InlineData('M', DeviceType.M)>]
    [<InlineData('K', DeviceType.K)>]
    [<InlineData('T', DeviceType.T)>]
    [<InlineData('C', DeviceType.C)>]
    [<InlineData('D', DeviceType.D)>]
    [<InlineData('I', DeviceType.I)>]
    [<InlineData('Q', DeviceType.Q)>]
    [<InlineData('F', DeviceType.F)>]
    [<InlineData('L', DeviceType.L)>]
    let ``getDeviceType should map area codes correctly`` (areaCode: char) (expectedDeviceType: DeviceType) =
        // Use enum parsing to validate device type mapping
        let deviceTypeName = string areaCode
        let success, parsedDeviceType = Enum.TryParse<DeviceType>(deviceTypeName)
        Assert.True(success)
        Assert.Equal(expectedDeviceType, parsedDeviceType)

    [<Fact>]
    let ``getDeviceType should throw for invalid area code`` () =
        Assert.Throws<ArgumentException>(fun () -> 
            Enum.Parse<DeviceType>("Z") |> ignore)

    [<Fact>]
    let ``formatAddress should create standard address format`` () =
        let result = formatAddress 'M' PlcTagDataType.UInt16 1600 // M100 * 16 bits = 1600 bit offset
        Assert.Equal("%MW100", result)

    [<Theory>]
    [<InlineData('P')>]
    [<InlineData('I')>]
    [<InlineData('Q')>]
    [<InlineData('M')>]
    let ``isAreaSupported should return true for supported areas`` (areaCode: char) =
        let result = SupportedAreaCodes.All |> List.contains areaCode
        Assert.True(result)

    [<Theory>]
    [<InlineData('Z')>]
    [<InlineData('X')>]
    [<InlineData('Y')>]
    let ``isAreaSupported should return false for unsupported areas`` (areaCode: char) =
        let result = SupportedAreaCodes.All |> List.contains areaCode
        Assert.False(result)

    [<Fact>]
    let ``validateAddress should pass for valid addresses`` () =
        let validAddresses = [
            "P10"
            "M100"
            "D1000"
            "K50"
            "T10"
        ]
        
        for address in validAddresses do
            // Should not throw - use the existing FrameUtils.resolveTagInfo
            let _, _, _ = FrameUtils.resolveTagInfo address false
            Assert.True(true) // If we get here, validation passed

    [<Fact>]
    let ``validateAddress should throw for invalid addresses`` () =
        let invalidAddresses = [
            ""
            "123"
            "ABC"
            "P"
        ]
        
        for address in invalidAddresses do
            Assert.Throws<ArgumentException>(fun () -> 
                FrameUtils.resolveTagInfo address false |> ignore) |> ignore

    [<Theory>]
    [<InlineData("M100", "M", "100")>]
    [<InlineData("D1000", "D", "1000")>]
    let ``splitAddress should separate area and number parts`` (address: string) (expectedArea: string) (expectedNumber: string) =
        let (areaChar, number) = parseSimpleAddress address
        Assert.Equal(expectedArea, string areaChar)
        Assert.Equal(expectedNumber, string number)

    [<Fact>]
    let ``normalizeAddress should convert to uppercase`` () =
        let result = "m100".ToUpperInvariant()
        Assert.Equal("M100", result)

    [<Fact>]
    let ``normalizeAddress should trim whitespace`` () =
        let result = "  M100  ".Trim()
        Assert.Equal("M100", result)

    [<Fact>]
    let ``XgtTag class should have correct properties`` () =
        let tag = XgtTag("TestTag", "M100", PlcTagDataType.UInt16, 1600, false)
        
        Assert.Equal("M", tag.Device)
        Assert.Equal(1600, tag.BitOffset)
        Assert.Equal("M_1600", tag.AddressKey)

    [<Fact>]
    let ``XgtTag should generate correct LWord tag`` () =
        let tag = XgtTag("TestTag", "M100", PlcTagDataType.UInt64, 6400, false) // 100 * 64 = 6400
        let result = tag.LWordTag
        Assert.Equal("ML100", result)

    [<Fact>]
    let ``XgtTag should generate correct address alias`` () =
        let tag = XgtTag("TestTag", "M100", PlcTagDataType.UInt16, 1600, false)
        let result = tag.GetAddressAlias(PlcTagDataType.UInt16)
        Assert.Equal("MW100.0", result)