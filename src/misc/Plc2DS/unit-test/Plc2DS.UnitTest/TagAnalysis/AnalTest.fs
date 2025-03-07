namespace T


open System.IO
open System
open System.Text.RegularExpressions

open NUnit.Framework

open Dual.Common.UnitTest.FS

open Dual.Plc2DS
open Dual.Common.Core.FS
open Dual.Common.Base.FS

module AnalTest =
    let getFile(file:string) =
        Path.Combine(__SOURCE_DIRECTORY__, "..", "Samples", file)

    let createSemantic() =
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

    let semantic = createSemantic()

    let dq = "\""
    let ddq = "\"\""

    type S() =
        [<Test>]
        member _.``Minimal`` () =
            do
                let si = AnalyzedNameSemantic.Create("STN2_B_SHUTTLE_ADVANCE", semantic)
                si.FlowName === "STN"
                si.DeviceName === "SHT"
                si.ActionName === "ADV"
                si.Modifiers === [|"B"|]
                si.SplitNames === [|"STN2"; "B"; "SHUTTLE"; "ADVANCE"|]

            do
                // 표준어 변환, modifiers test
                let si = AnalyzedNameSemantic.Create("STATION2_1ST_SHT_ADV", semantic)
                si.FlowName === "STN"
                si.DeviceName === "SHT"
                si.ActionName === "ADV"
                si.Modifiers === [|"1ST"|]
                si.SplitNames === [|"STATION2"; "1ST"; "SHT"; "ADV"|]

            do
                // 이름 구분자 변경 test
                let semantic = createSemantic()
                semantic.NameSeparators <- [|":"|]
                let si = AnalyzedNameSemantic.Create("STATION2:1ST:SHT:ADV", semantic)
                si.FlowName === "STN"
                si.DeviceName === "SHT"
                si.ActionName === "ADV"
                si.Modifiers === [|"1ST"|]
                si.SplitNames === [|"STATION2"; "1ST"; "SHT"; "ADV"|]


    type T() =
        [<Test>]
        member _.``MinimalAB`` () =
            let l = $"ALIAS,,CTRL2_B_SHUTTLE_ADVANCE,{ddq},{ddq},{dq}B100[1].1{dq},{dq}(RADIX := Decimal, ExternalAccess := Read/Write){dq}"
            let tagInfo:AB.PlcTagInfo = AB.CsvReader.CreatePlcTagInfo(l)
            let name = tagInfo.GetName()
            let si = AnalyzedNameSemantic.Create(name, semantic)
            si.DeviceName === "SHT"
            si.ActionName === "ADV"
            si.Modifiers === [|"B"|]
            si.SplitNames === [|"CTRL2"; "B"; "SHUTTLE"; "ADVANCE"|]
            noop()


            let l = $"ALIAS,,S441_PLC_BOX_1:3:I,{ddq},{ddq},{dq}S441_PLC_BOX_1:I.Data[3]{dq},{dq}(RADIX := Binary, ExternalAccess := Read/Write){dq}"
            let tagInfo:AB.PlcTagInfo = AB.CsvReader.CreatePlcTagInfo(l)
            tagInfo.Type === "ALIAS"
            tagInfo.Name === "S441_PLC_BOX_1:3:I"
            tagInfo.Specifier === "S441_PLC_BOX_1:I.Data[3]"
            tagInfo.OptIOType === Some true

            let si = AnalyzedNameSemantic.Create(tagInfo.Name, semantic)
            si.SplitNames === [|"S441"; "PLC"; "BOX"; "1:3:I"|]
            noop()


            //let csvPath = getFile("AB/BB_Controller_2_Tags.csv")
            //let data = Ab.CsvReader.ReadCommentCSV(csvPath)
            //data |> Array.iter (tracefn "%A")

            //let devices:Device[] = Builder.ExtractDevices(data, semantic)
            //devices |> Array.iter (tracefn "%A")
            //noop()

        [<Test>]
        member _.``MinimalMX`` () =
            let l = $"{dq}Y6B0{dq}	{dq}U17:    Rev.3   Buffer.AVacOn{dq}"
            let tagInfo:MX.PlcTagInfo = MX.CsvReader.CreatePlcTagInfo(l, delimeter='\t', hasLabel=false)
            tagInfo.Device === "Y6B0"
            tagInfo.Comment === "U17:    Rev.3   Buffer.AVacOn"
            tagInfo.Label === ""
            tagInfo.OptIOType === Some false

            //let si = AnalyzedNameSemantic.Create(tagInfo.GetName(), semantic)
            //si.DeviceName === "SHT"
            //si.ActionName === "ADV"
            noop()
