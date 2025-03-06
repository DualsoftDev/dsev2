namespace T


open System.IO
open System
open System.Text.RegularExpressions

open NUnit.Framework

open Dual.Common.UnitTest.FS

open Dual.Plc2DS
open Dual.Plc2DS.AB
open Dual.Common.Core.FS
open Dual.Common.Base.FS

module AnalTest =
    let getFile(file:string) =
        Path.Combine(__SOURCE_DIRECTORY__, "..", "Samples", file)
    let semantic =
        let json = """
{
  "Dialects": [
    [ "ADV", "ADVANCE" ],
    [ "RET", "RETURN", "RTN" ],
    [ "CYL", "CYLINDER" ],
    [ "RBT", "ROBOT" ],
    [ "STN", "STATION" ]
  ],

  "__이하 _Actions 및 States, MutualResetTuples 등 모든 항목": "표준어로만 기술할 것",

  "Actions": [ "ADV", "RET" ],
  "States": [ "ERR", "OK", "WAIT", "STOP" ],
  "MutualResetTuples": [
    [ "ADV", "RET" ],
    [ "UP", "DOWN" ]
  ],
  "FlowNames": [ "STN" ],
  "DeviceNames": [ "CYL", "RBT" ]
}
"""
        EmJson.FromJson<AppSettings>(json);

    type T() =
        [<Test>]
        member _.``Minimal`` () =
            let csvPath = getFile("AB/BB_Controller_2_Tags.csv")
            let data = Ab.CsvReader.ReadCommentCSV(csvPath)
            data |> Array.iter (tracefn "%A")

            let devices:Device[] = Builder.ExtractDevices(data, semantic)
            devices |> Array.iter (tracefn "%A")
            noop()