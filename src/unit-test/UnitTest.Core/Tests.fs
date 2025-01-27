namespace T.Core


open System
open System.Linq
open Xunit
open Dual.Common.UnitTest.FS
open NUnit.Framework

open Dual.Ev2
//open Engine.Common
open Dual.Common.Base.CS
open Dual.Common.Core.FS

module Serialization =
    let system =
        let system = DsSystem.Create("system1")
        let flow1 = system.CreateFlow("flow1")
        let work1 = flow1.CreateWork("work1")
        let call1 = work1.CreateCall("call1")
        let call2 = work1.CreateCall("call2")

        work1.CreateEdge(call1, call2, CausalEdgeType.Start) |> verifyNonNull
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
              "CoinType": {
                "Case": "Action"
              }
            },
            {
              "Name": "call2",
              "CoinType": {
                "Case": "Action"
              }
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
                "EdgeType": {
                  "Case": "Start"
                }
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
        system2.Flows[0].System === system2
        system2.Flows[0].Works[0].Flow === system2.Flows[0]
        system2.Flows[0].Works[0].Coins[0].Parent === system2.Flows[0].Works[0]
        system2.Flows[0].Works[0].Coins[1].Parent === system2.Flows[0].Works[0]

        //let xxx = system2.Flows[0].Works[0].GetGraph()
        //let yyy = xxx.Edges
        system2.Flows[0].Works[0].GetGraph().Edges.Count === 1
        let e = system2.Flows[0].Works[0].GetGraph().Edges.First()
        e.Source === system2.Flows[0].Works[0].Coins[0]
        e.Target === system2.Flows[0].Works[0].Coins[1]
        e.EdgeType === CausalEdgeType.Start

        let json2 = system2.Serialize()
        jsonText === json2


        1 === 1
