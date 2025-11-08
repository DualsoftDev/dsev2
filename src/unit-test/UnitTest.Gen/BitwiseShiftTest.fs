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
