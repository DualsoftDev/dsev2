namespace T

open NUnit.Framework
open Dual.Common.UnitTest.FS
open Ev2.Gen

type BitwiseShiftTest() =
    [<Test>]
    member _.``SHL_int32``() =
        let result = shl<int32> (literal 0x0001) (literal 3)
        result.TValue === 0x0008

    [<Test>]
    member _.``SHR_int32_arithmetic``() =
        let result = shr<int32> (literal -64) (literal 2)
        result.TValue === -16

    [<Test>]
    member _.``SHR_uint32_logical``() =
        let result = shr<uint32> (literal 0x80000000u) (literal 1)
        result.TValue === 0x40000000u

    [<Test>]
    member _.``BAND_uint16``() =
        let result = band<uint16> (literal (uint16 0x0F0F)) (literal (uint16 0x00FF))
        result.TValue === (uint16 0x000F)

    [<Test>]
    member _.``BOR_int32``() =
        let result = bor<int32> (literal 0x0F00) (literal 0x00F0)
        result.TValue === 0x0FF0

    [<Test>]
    member _.``BXOR_uint32``() =
        let result = bxor<uint32> (literal 0xFF00u) (literal 0x0FF0u)
        result.TValue === 0xF0F0u

    [<Test>]
    member _.``BNOT_int8``() =
        let result = bnot<int8> (literal 0b0000_1111y)
        result.TValue === 0b1111_0000y
