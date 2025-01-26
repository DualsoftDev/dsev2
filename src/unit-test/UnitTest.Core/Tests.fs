namespace T.Core


open System
open Xunit
open Dual.Common.UnitTest.FS
open NUnit.Framework

open Dual.Ev2
open Engine.Common
open Dual.Common.Base.CS

module Serialization =
    let system =
        let system = DsSystem.Create("system1")
        let flow1 = system.CreateFlow("flow1")
        let work1 = flow1.CreateWork("work1")
        let call1 = work1.CreateCall("call1")
        let call2 = work1.CreateCall("call2")

        work1.CsCreateEdge(call1, call2, EdgeType.Start)
        system
    let json = """{
  "Name": "system1",
  "Flows": [
    {
      "Name": "flow1",
      "Works": [
        {
          "Name": "work1",
          "Coins": [
            {
              "Name": "call1",
              "CoinType": "Call"
            },
            {
              "Name": "call2",
              "CoinType": "Call"
            }
          ],
          "GraphDTO": {
            "Vertices": [
              "call1",
              "call2"
            ],
            "Edges": [
              {
                "Source": "call1",
                "Target": "call2",
                "EdgeType": 1
              }
            ]
          }
        }
      ],
      "Coins": [],
      "GraphDTO": {
        "Vertices": [
          "work1"
        ],
        "Edges": []
      }
    }
  ]
}"""

    [<Test>]
    let ``My test`` () =
        let jsonText = system.Serialize();
        DcClipboard.Write(jsonText);

        jsonText === json

        let system2 = DsSystem.Deserialize(jsonText)
        let json2 = system2.Serialize()
        jsonText === json2


        1 === 1
