namespace T


open System.IO
open System
open System.Text.RegularExpressions

open NUnit.Framework

open Dual.Common.UnitTest.FS

open Dual.Plc2DS
open Dual.Common.Core.FS
open Dual.Common.Base.FS
open Dual.Plc2DS

module AnalTest =
    let getFile(file:string) =
        Path.Combine(__SOURCE_DIRECTORY__, "..", "Samples", file)

    let createSemantic() =
        let json = """
{
  "_Separators": "_",
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

  "Actions": [ "ADV", "CHECK", "CLP", "HOME", "LOAD", "OFF", "ON", "PRESS", "RET", "ROTATE", "SELECT", "SWING", "UNSWING" ],

  "Devices": [ "ADV_LOCK", "CAM", "CARRIER", "CONNECTOR", "CYL", "GATE", "GUN", "LAMP", "LOCK", "LS", "MOTOR", "PART", "PIN", "RBT", "SHT", "SLIDE", "SOL", "VALVE" ],

  "Flows": [ "STN" ],

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
      "Devices": [ "BUFFER" ],
      "NameSeparators": [ "_", ";", " ", "\t", ".", ",", ":" ],
    }
  }
}



"""
        EmJson.FromJson<SemanticSettings>(json);

    let semantic = createSemantic()

    let dq = "\""
    let ddq = "\"\""

    type S() =
        [<Test>]
        member _.``Minimal`` () =
            do
                // "~:STN:2@0" : STN 의 prefix 숫자는 없고(~), postfix 숫자는 '2' 이고, 위치는(@) 쪼갠 이름의 0번째
                let si = semantic.CreateDefault("STN2_B_SHUTTLE_ADVANCE")
                si.Flows     |> exactlyOne |> toString === "~:STN:2@0"
                si.Devices   |> exactlyOne |> toString === "~:SHT:~@2"
                si.Actions   |> exactlyOne |> toString === "~:ADV:~@3"
                si.Modifiers |> exactlyOne |> toString === "~:B:~@1"
                si.SplitNames === [|"STN2"; "B"; "SHUTTLE"; "ADVANCE"|]

            do
                // 표준어 변환, modifiers test
                let si = semantic.CreateDefault("STATION2_1ST_SHT_ADV")
                si.Flows     |> exactlyOne |> toString === "~:STN:2@0"
                si.Devices   |> exactlyOne |> toString === "~:SHT:~@2"
                si.Actions   |> exactlyOne |> toString === "~:ADV:~@3"
                si.Modifiers |> exactlyOne |> toString === "1:ST:~@1"       // PName: "1ST" 전체를 modifier 로 인식.
                si.SplitNames === [|"STATION2"; "1ST"; "SHT"; "ADV"|]



            do
                // 충돌: ADV 와 CLAMP 모두 ACTION 이름으로 match.  먼저 match 되는 ADV 가 우선시 되는 bug
                let si = semantic.CreateDefault("S301RH_B_ADV_LOCK_CLAMP")
                si.Actions.Length === 2
                si.Actions[0].ToString() === "~:ADV:~@2"
                si.Actions[1].ToString() === "~:CLP:~@4"
                si.Modifiers |> exactlyOne |> toString === "~:B:~@1"
                si.SplitNames === [|"S301RH"; "B"; "ADV"; "LOCK"; "CLAMP"|]

            do
                // 이름 구분자 변경 test
                let semantic = createSemantic()
                semantic.NameSeparators <- ResizeArray [|":"|]
                let si = semantic.CreateDefault("STATION2:1ST:SHT:ADV")


                si.Flows     |> exactlyOne |> toString === "~:STN:2@0"
                si.Devices   |> exactlyOne |> toString === "~:SHT:~@2"
                si.Actions   |> exactlyOne |> toString === "~:ADV:~@3"
                si.Modifiers |> exactlyOne |> toString === "1:ST:~@1"

                si.SplitNames === [|"STATION2"; "1ST"; "SHT"; "ADV"|]


    type T() =
        [<Test>]
        member _.``MinimalAB`` () =
            do
                let l = $"ALIAS,,CTRL2_B_SHUTTLE_ADVANCE,{ddq},{ddq},{dq}B100[1].1{dq},{dq}(RADIX := Decimal, ExternalAccess := Read/Write){dq}"
                let tagInfo:AB.PlcTagInfo = AB.CsvReader.CreatePlcTagInfo(l)
                let si = semantic.CreateDefault(tagInfo.GetAnalysisField())

                si.Devices   |> exactlyOne |> toString === "~:SHT:~@2"
                si.Actions   |> exactlyOne |> toString === "~:ADV:~@3"
                si.Modifiers |> exactlyOne |> toString === "~:B:~@1"

                si.SplitNames === [|"CTRL2"; "B"; "SHUTTLE"; "ADVANCE"|]
                si.SplitSemanticCategories[0] === DuNone
                si.SplitSemanticCategories[1] === DuModifier
                si.SplitSemanticCategories[2] === DuDevice
                si.SplitSemanticCategories[3] === DuAction

                // 필수 요소만: flow + device
                si.Stringify() === "SHT"
                si.Stringify(withAction=true) === "SHT_ADV"
                si.Stringify(withAction=true, withModifiers=true) === "SHT_ADV_B"
                si.Stringify(withAction=true, withUnmatched=true) === "SHT_ADV_CTRL2"
                si.Stringify(withAction=true, withModifiers=true, withUnmatched=true) === "SHT_ADV_B_CTRL2"
                noop()

            do
                let l = $"ALIAS,,S441_PLC_BOX_1:3:I,{ddq},{ddq},{dq}S441_PLC_BOX_1:I.Data[3]{dq},{dq}(RADIX := Binary, ExternalAccess := Read/Write){dq}"
                let tagInfo:AB.PlcTagInfo = AB.CsvReader.CreatePlcTagInfo(l)
                tagInfo.Type === "ALIAS"
                tagInfo.Name === "S441_PLC_BOX_1:3:I"
                tagInfo.Specifier === "S441_PLC_BOX_1:I.Data[3]"
                tagInfo.OptIOType === Some true

                let si = semantic.CreateDefault(tagInfo.Name)
                si.SplitNames === [|"S441"; "PLC"; "BOX"; "1:3:I"|]
                noop()

        [<Test>]
        member _.``MinimalMX`` () =
            let l = $"{dq}Y6B0{dq}	{dq}U17:    Rev.3   Buffer.AVacOn{dq}"
            let tagInfo:MX.PlcTagInfo = MX.CsvReader.CreatePlcTagInfo(l, delimeter='\t', hasLabel=false)
            tagInfo.Device === "Y6B0"
            tagInfo.Comment === "U17:    Rev.3   Buffer.AVacOn"
            tagInfo.Label === ""
            tagInfo.OptIOType === Some false

            let mx = semantic.CreateVendorSemantic(Vendor.MX)
            mx.SplitOnCamelCase === true
            let xxx = tagInfo.GetAnalysisField()
            let y = xxx
            let si = mx.CreateDefault(tagInfo.GetAnalysisField())
            si.SplitNames |> SeqEq ["U17"; "REV"; "3"; "BUFFER"; "A"; "VAC"; "ON"]
            si.Devices   |> exactlyOne |> toString === "~:BUFFER:~@3"
            si.SplitSemanticCategories.Filter(fun x -> x <> DuNone) |> Seq.length === 1
            si.SplitSemanticCategories[3] === DuDevice
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
            let anals = tagInfos |> Array.map (fun t -> semantic.CreateDefault(t.GetAnalysisField()))
            let gr = anals |> groupBy (_.Stringify(withDeviceNumber=true))
            gr.Length === 3
            do
                let key, items = gr.[0]
                key === "STN1_CYL1"
                items.Length === 2
                items[0].Actions   |> exactlyOne |> toString === "~:ADV:~@2"
                items[1].Actions   |> exactlyOne |> toString === "~:RET:~@2"

                items[0].Flows     |> exactlyOne |> toString === "~:STN:1@0"
                items[1].Flows     |> exactlyOne |> toString === "~:STN:1@0"
                items[0].Devices   |> exactlyOne |> toString === "~:CYL:1@1"    // CYL1
                items[1].Devices   |> exactlyOne |> toString === "~:CYL:1@1"    // CYL1

            do
                let key, items = gr.[1]
                key === "STN1_CYL2"
                items.Length === 2

                items[0].Actions   |> exactlyOne |> toString === "~:ADV:~@2"
                items[1].Actions   |> exactlyOne |> toString === "~:RET:~@2"

                items[0].Flows     |> exactlyOne |> toString === "~:STN:1@0"
                items[1].Flows     |> exactlyOne |> toString === "~:STN:1@0"
                items[0].Devices   |> exactlyOne |> toString === "~:CYL:2@1"    // CYL2
                items[1].Devices   |> exactlyOne |> toString === "~:CYL:2@1"    // CYL2

            do
                let key, items = gr.[2]
                key === "STN2_CYL1"
                items.Length === 2
                items[0].Actions   |> exactlyOne |> toString === "~:ADV:~@2"
                items[1].Actions   |> exactlyOne |> toString === "~:RET:~@2"

                items[0].Flows     |> exactlyOne |> toString === "~:STN:2@0"    // STN2
                items[1].Flows     |> exactlyOne |> toString === "~:STN:2@0"    // STN2
                items[0].Devices   |> exactlyOne |> toString === "~:CYL:1@1"
                items[1].Devices   |> exactlyOne |> toString === "~:CYL:1@1"

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
            let anals = tagInfos |> Array.map (fun t -> semantic.CreateDefault(t.GetAnalysisField()))

            do  // key tests
                //anals[1] : "S301RH_B_ADV_LOCK_CLAMP1_ERR"
                // "LOCK" 이 Action 으로 등록되어 있지 않고, Device 로 등록되어 있는 상태
                semantic.Actions |> contains "LOCK" |> ShouldBeFalse
                semantic.Devices |> contains "LOCK" |> ShouldBeTrue
                semantic.States  |> contains "ERR"  |> ShouldBeTrue

                anals[1].States  |> exactlyOne |> toString === "~:ERR:~@5"
                anals[1].Devices |> exactlyOne |> toString === "~:LOCK:~@3"
                anals[1].Actions |> map toString === [| "~:ADV:~@2"; "~:CLP:1@4" |]
                anals[1].Stringify(withDeviceNumber=true, withState=false) === "LOCK"
                anals[1].Stringify(withDeviceNumber=true, withState=true) === "LOCK_ERR"

            let grByKeys = anals |> map (_.Stringify(withDeviceNumber=true, withModifiers=false))
            tracefn "%A" grByKeys
            grByKeys |> distinct === [| "LOCK"; "LOCK_LAMP"|]
            let gr = anals |> groupBy (_.Stringify(withDeviceNumber=true, withModifiers=false))
            gr.Length === 2
