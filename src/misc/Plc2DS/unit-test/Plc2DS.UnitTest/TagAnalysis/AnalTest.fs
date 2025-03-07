namespace T


open System.IO
open System
open System.Text.RegularExpressions

open NUnit.Framework

open Dual.Common.UnitTest.FS

open Dual.Plc2DS
open Dual.Common.Core.FS
open Dual.Common.Base.FS
open Dual.Plc2DS.Common.FS

module AnalTest =
    let getFile(file:string) =
        Path.Combine(__SOURCE_DIRECTORY__, "..", "Samples", file)

    let createSemantic() =
        let json = """
{
  "_NameSeparators": "_",
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
    "A", "ALL", "AUX", "B", "C", "D",
    "EMERGENCY", "HIGH", "LOW", "MEMO", "N",
    "SIDE", "SPEED", "1ST",
    "2ND", "2UNIT", "3DR", "3RD", "3UNIT", "4DR", "4TH", "4UNIT", "5DR",
    "UNIT"
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

  "States": [ "CONDITION", "ERR", "NO", "OK", "RUNNING", "STOP", "WAIT" ],
  "AddOn": {
    "MX": {
      "SplitOnCamelCase": true,
      "DeviceNames": [ "BUFFER" ],
      "NameSeparators": [ "_", ";", " ", "\\\\t", ".", ",", ":" ],
    }
  }
}



"""
        EmJson.FromJson<AppSettings>(json);

    let semantic = createSemantic()

    let dq = "\""
    let ddq = "\"\""
    open FsUnit.Xunit

    type S() =
        [<Test>]
        member _.``Minimal`` () =
            do
                let si = AnalyzedNameSemantic.Create("STN2_B_SHUTTLE_ADVANCE", semantic)
                Tuple.AreEqual(si.Flow, ("STN", Some 2)) === true
                Tuple.AreEqual(si.Device, ("SHT", None)) === true
                Tuple.AreEqual(si.Action, ("ADV", None)) === true

                si.Modifiers.Length === 1
                Tuple.AreEqual(si.Modifiers[0], ("B", None)) === true
                si.SplitNames === [|"STN2"; "B"; "SHUTTLE"; "ADVANCE"|]

            do
                // 표준어 변환, modifiers test
                let si = AnalyzedNameSemantic.Create("STATION2_1ST_SHT_ADV", semantic)
                Tuple.AreEqual(si.Flow, ("STN", Some 2)) === true
                Tuple.AreEqual(si.Device, ("SHT", None)) === true
                Tuple.AreEqual(si.Action, ("ADV", None)) === true
                si.Modifiers.Length === 1
                Tuple.AreEqual(si.Modifiers[0], ("1ST", None)) === true
                si.SplitNames === [|"STATION2"; "1ST"; "SHT"; "ADV"|]

            do
                // 충돌: ADV 와 CLAMP 모두 ACTION 이름으로 match.  먼저 match 되는 ADV 가 우선시 되는 bug
                let si = AnalyzedNameSemantic.Create("S301RH_B_XADV_LOCK_CLAMP", semantic)
                Tuple.AreEqual(si.Action, ("ADV", None)) === true
                si.Modifiers.Length === 1
                Tuple.AreEqual(si.Modifiers[0], ("B", None)) === true
                si.SplitNames === [|"S301RH"; "B"; "ADV"; "LOCK"; "CLAMP"|]

            do
                // 이름 구분자 변경 test
                let semantic = createSemantic()
                semantic.NameSeparators <- ResizeArray [|":"|]
                let si = AnalyzedNameSemantic.Create("STATION2:1ST:SHT:ADV", semantic)
                Tuple.AreEqual(si.Flow, ("STN", Some 2)) === true
                Tuple.AreEqual(si.Device, ("SHT", None)) === true
                Tuple.AreEqual(si.Action, ("ADV", None)) === true
                si.Modifiers.Length === 1
                Tuple.AreEqual(si.Modifiers[0], ("1ST", None)) === true
                si.SplitNames === [|"STATION2"; "1ST"; "SHT"; "ADV"|]


    type T() =
        [<Test>]
        member _.``MinimalAB`` () =
            let l = $"ALIAS,,CTRL2_B_SHUTTLE_ADVANCE,{ddq},{ddq},{dq}B100[1].1{dq},{dq}(RADIX := Decimal, ExternalAccess := Read/Write){dq}"
            let tagInfo:AB.PlcTagInfo = AB.CsvReader.CreatePlcTagInfo(l)
            let si = AnalyzedNameSemantic.Create(tagInfo.GetAnalysisField(), semantic)
            Tuple.AreEqual(si.Device, ("SHT", None)) === true
            Tuple.AreEqual(si.Action, ("ADV", None)) === true
            si.Modifiers.Length === 1
            Tuple.AreEqual(si.Modifiers[0], ("B", None)) === true
            si.SplitNames === [|"CTRL2"; "B"; "SHUTTLE"; "ADVANCE"|]
            si.SplitSemanticCategories[0] === Nope
            si.SplitSemanticCategories[1] === Modifier
            si.SplitSemanticCategories[2] === Device
            si.SplitSemanticCategories[3] === Action

            // 필수 요소만: flow + device
            si.Stringify() === "SHT"
            si.Stringify(withAction=true) === "SHT_ADV"
            si.Stringify(withAction=true, withModifiers=true) === "SHT_ADV_B"
            si.Stringify(withAction=true, withUnmatched=true) === "SHT_ADV_CTRL2"
            si.Stringify(withAction=true, withModifiers=true, withUnmatched=true) === "SHT_ADV_B_CTRL2"
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

            let mx = semantic.CreateVendorSemantic(K.MX)
            mx.SplitOnCamelCase === true
            let xxx = tagInfo.GetAnalysisField()
            let y = xxx
            let si = AnalyzedNameSemantic.Create(tagInfo.GetAnalysisField(), mx)
            si.SplitNames |> SeqEq ["U17"; "REV"; "3"; "BUFFER"; "A"; "VAC"; "ON"]
            Tuple.AreEqual(si.Device, ("BUFFER", None)) === true
            si.SplitSemanticCategories.Filter(fun x -> x <> Nope) |> Seq.length === 1
            si.SplitSemanticCategories[3] === Device
            noop()



        [<Test>]
        member _.``WellDefinedABGrouping`` () =
            let csv = """
ALIAS,,STN1_CYL1_ADV,"","","B100[1].1","COMMENT"
ALIAS,,STN1_CYL1_RET,"","","B100[1].1","COMMENT"
ALIAS,,STN1_CYL2_ADV,"","","B100[1].1","COMMENT"
ALIAS,,STN1_CYL2_RET,"","","B100[1].1","COMMENT"
ALIAS,,STN2_CYL1_ADV,"","","B100[1].1","COMMENT"
ALIAS,,STN2_CYL1_RET,"","","B100[1].1","COMMENT"
"""

            let tagInfos:AB.PlcTagInfo[] =
                csv.SplitByLine()
                |> filter _.NonNullAny()
                |> map AB.CsvReader.CreatePlcTagInfo
                |> Seq.toArray
            let anals = tagInfos |> Array.map (fun t -> AnalyzedNameSemantic.Create(t.GetAnalysisField(), semantic))
            let gr = anals |> groupBy (_.Stringify(withDeviceTrailingNumber=true))
            gr.Length === 3
            do
                let key, items = gr.[0]
                key === "STN1_CYL1"
                items.Length === 2
                Tuple.AreEqual(items[0].Action, ("ADV", None)) === true
                Tuple.AreEqual(items[1].Action, ("RET", None)) === true
                Tuple.AreEqual(items[0].Flow  , ("STN", Some 1)) === true
                Tuple.AreEqual(items[1].Flow  , ("STN", Some 1)) === true
                Tuple.AreEqual(items[0].Device, ("CYL", Some 1)) === true
                Tuple.AreEqual(items[1].Device, ("CYL", Some 1)) === true

            do
                let key, items = gr.[1]
                key === "STN1_CYL2"
                items.Length === 2
                Tuple.AreEqual(items[0].Action, ("ADV", None)) === true
                Tuple.AreEqual(items[1].Action, ("RET", None)) === true
                Tuple.AreEqual(items[0].Flow  , ("STN", Some 1)) === true
                Tuple.AreEqual(items[1].Flow  , ("STN", Some 1)) === true
                Tuple.AreEqual(items[0].Device, ("CYL", Some 2)) === true
                Tuple.AreEqual(items[1].Device, ("CYL", Some 2)) === true

            do
                let key, items = gr.[2]
                key === "STN2_CYL1"
                items.Length === 2
                Tuple.AreEqual(items[0].Action, ("ADV", None)) === true
                Tuple.AreEqual(items[1].Action, ("RET", None)) === true
                Tuple.AreEqual(items[0].Flow  , ("STN", Some 2)) === true
                Tuple.AreEqual(items[1].Flow  , ("STN", Some 2)) === true
                Tuple.AreEqual(items[0].Device, ("CYL", Some 1)) === true
                Tuple.AreEqual(items[1].Device, ("CYL", Some 1)) === true

            noop()


        [<Test>]
        member _.``WellDefinedABGrouping현장`` () =
            let csv = """
ALIAS,,S301RH_B_ADV_LOCK_CLAMP,"","","B301[73].6","(RADIX := Decimal, ExternalAccess := Read/Write)"
ALIAS,,S301RH_B_ADV_LOCK_CLAMP1_ERR,"","","B301[93].22","(RADIX := Decimal, ExternalAccess := Read/Write)"
ALIAS,,S301RH_B_ADV_LOCK_CLAMP1_GP,"","","B301[103].22","(RADIX := Decimal, ExternalAccess := Read/Write)"
ALIAS,,S301RH_B_ADV_LOCK_CLAMP2_ERR,"","","B301[93].24","(RADIX := Decimal, ExternalAccess := Read/Write)"
ALIAS,,S301RH_B_ADV_LOCK_CLAMP2_GP,"","","B301[103].24","(RADIX := Decimal, ExternalAccess := Read/Write)"
ALIAS,,S301RH_B_ADV_LOCK_CLAMP_LAMP,"","","B301[109].4","(RADIX := Decimal, ExternalAccess := Read/Write)"
ALIAS,,S301RH_B_ADV_LOCK_CLAMP_MEMO,"","","B301[86].1","(RADIX := Decimal, ExternalAccess := Read/Write)"
"""
            let tagInfos:AB.PlcTagInfo[] =
                csv.SplitByLine()
                |> filter _.NonNullAny()
                |> map AB.CsvReader.CreatePlcTagInfo
                |> Seq.toArray
            let anals = tagInfos |> Array.map (fun t -> AnalyzedNameSemantic.Create(t.GetAnalysisField(), semantic))
            let gr = anals |> groupBy (_.Stringify(withDeviceTrailingNumber=true))
            gr.Length === 2
