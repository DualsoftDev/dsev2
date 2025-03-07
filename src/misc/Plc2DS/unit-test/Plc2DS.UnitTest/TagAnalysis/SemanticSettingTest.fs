namespace T



open NUnit.Framework

open Dual.Plc2DS
open Dual.Common.Core.FS
open Dual.Common.Base.FS
open Dual.Plc2DS.Common.FS
open Dual.Common.UnitTest.FS

module SemanticSettingTest =
    let semantic =
        let json = """
{
  "Dialects": [
    [ "ADV", "ADVANCE" ],
    [ "RET", "RETURN", "RTN" ],
  ],
  "Actions": [ "ADV", "RET" ],
  "DeviceNames": [ "CYL", "RBT" ],
  "FlowNames": [ "STN" ],
  "Modifiers": [ "A", "HIGH"],
  "MutualResetTuples": [
    [ "ADV", "RET" ],
  ],

  "States": [ "ERR", "OK" ],
  "AddOn": {
    "MX": {
      "NameSeparators": [ "*"],
      "Actions": [ "ABORT", "PAUSE", "RESUME" ]
    }
  },
  "Override": {
    "AB": {
      "Actions": [ "ABORT", "PAUSE", "RESUME" ],
      "States": [ "ABORTED", "PAUSED" ]
    },
    "MX": {
      "NameSeparators": [ "_", ";", " "],
      "Dialects": [
        [ "UNCLP", "UNCLAMP" ]
      ],
      "States": [ "MOVED" ],
      "MutualResetTuples": [
        [ "ON", "OFF" ],
      ],
    }
  }
}

"""
        EmJson.FromJson<AppSettings>(json);

    type SS() =
        [<Test>]
        member _.``Minimal`` () =
            let mx = semantic.CreateVendorSemantic(K.MX)

            // override 항목 체크
            mx.NameSeparators |> SeqEq ["_"; ";"; " "]      // ["*"] 는 addOn 에서 추가된 후, override 되어 삭제됨
            mx.MutualResetTuples.Count === 1
            mx.MutualResetTuples[0] |> SeqEq ["ON"; "OFF" ]
            mx.Dialects.Count === 1
            mx.Dialects.["UNCLAMP"] === "UNCLP"
            mx.States |> SeqEq [ "MOVED" ]

            // addOn 항목 체크
            mx.Actions |> SeqEq ([ "ADV"; "RET" ] @ [ "ABORT"; "PAUSE"; "RESUME" ])

            let s7 = semantic.CreateVendorSemantic(K.S7)
            s7.NameSeparators |> SeqEq semantic.NameSeparators
            s7.States         |> SeqEq semantic.States
            s7.Actions        |> SeqEq semantic.Actions

            // 원본 항목 유지 체크
            semantic.Actions |> SeqEq [ "ADV"; "RET" ]

            ()
