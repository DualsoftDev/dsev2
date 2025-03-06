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
    [ "CLP", "CLAMP" ],
    [ "CYL", "CYLINDER" ],
    [ "LOAD", "LOADG" ],
    [ "RET", "RETURN", "RTN" ],
    [ "RBT", "ROBOT", "RB" ],
    [ "ROT", "ROTATE" ],
    [ "SHT", "SHUTTLE" ],
    [ "STN", "STATION" ],
    [ "UNCLP", "UNCLAMP" ]
  ],

  "__이하 _Actions 및 States, MutualResetTuples 등 모든 항목": "표준어로만 기술할 것",

  "Actions": [ "ADV", "CHECK", "HOME", "LOAD", "OFF", "ON", "PRESS", "RET", "ROTATE", "SELECT", "SWING", "UNSWING" ],

  "DeviceNames": [ "CAM", "CARRIER", "CONNECTOR", "CYL", "GATE", "GUN", "LAMP", "LOCK", "LS", "MOTOR", "PART", "PIN", "RBT", "SHT", "SLIDE", "SOL", "VALVE" ],

  "FlowNames": [ "STN" ],

  "Modifiers": [
    "A", "ALL", "AUX", "B", "C", "D", "EMERGENCY", "HIGH", "LOW", "MEMO", "N", "SIDE", "SPEED",
    "1ST", "2ND", "2UNIT", "3DR", "3RD", "3UNIT", "4DR", "4TH", "4UNIT", "5DR", "UNIT"
  ],

  "MutualResetTuples": [
    [ "ADV", "RET" ],
    [ "LATCH", "UNLATCH" ],
    [ "MATCHING", "UNMATCH" ],
    [ "ON", "OFF" ],
    [ "OPEN", "CLOSE" ],
    [ "SWING", "UNSWING" ],
    [ "UP", "DOWN" ]
  ],

  "States": [ "CONDITION", "ERR", "NO", "OK", "RUNNING", "STOP", "WAIT" ]
}

"""
        EmJson.FromJson<AppSettings>(json);

    let dq = "\""
    let ddq = "\"\""
    type T() =
        [<Test>]
        member _.``Minimal`` () =
            let l = $"ALIAS,,CTRL2_B_SHUTTLE_ADVANCE,{ddq},{ddq},{dq}B100[1].1{dq},{dq}(RADIX := Decimal, ExternalAccess := Read/Write){dq}"
            let tagInfo = AB.CsvReader.CreatePlcTagInfo(l)
            let name = tagInfo.GetName()
            let si = AnalyzedNameSemantic.Create(name, semantic)
            si.DeviceName === "SHT"
            si.ActionName === "ADV"
            noop()

            //let csvPath = getFile("AB/BB_Controller_2_Tags.csv")
            //let data = Ab.CsvReader.ReadCommentCSV(csvPath)
            //data |> Array.iter (tracefn "%A")

            //let devices:Device[] = Builder.ExtractDevices(data, semantic)
            //devices |> Array.iter (tracefn "%A")
            //noop()