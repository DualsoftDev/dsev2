namespace T


open NUnit.Framework

open Dual.Plc2DS.MX
open System.IO
open Dual.Common.UnitTest.FS
open Dual.Plc2DS.S7

module Sdf =

    type T() =
        [<Test>]
        member _.``Minimal`` () =
            let sdfPath = getFile("S7.min.sdf")
            let data = S7.Reader.ReadSDF(sdfPath)
            data.Length === 4
            data[0].Name === "#5_M TL 핀전진단이상"
            data[0].Address === "M 750.0"
            data[0].DataType === "BOOL"
            data[0].Comment === ""

            data[3].Name === "#5_Q LM 1차 클1 풀림"
            data[3].Address === "Q 216.3"
            data[3].DataType === "BOOL"
            data[3].Comment === "SOL2"
