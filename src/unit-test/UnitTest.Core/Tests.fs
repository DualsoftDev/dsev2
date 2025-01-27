namespace T.Core


open System.Linq
open NUnit.Framework
open Newtonsoft.Json

open Dual.Common.UnitTest.FS
open Dual.Common.Base.CS
open Dual.Common.Base.FS
open Dual.Common.Core.FS

open Dual.Ev2

module Json =
    /// System1 > Flow1 > Work1 > {Call1 -> Call2}
    let system =
        let system = DsSystem.Create("system1")
        let flow1 = system.CreateFlow("F1")
        let work1 = flow1.CreateWork("F1W1")
        let call1 = work1.AddVertex(new DsAction("F1W1C1"))
        let call2 = work1.AddVertex(new DsAction("F1W1C2"))

        work1.CreateEdge(call1, call2, CausalEdgeType.Start) |> verifyNonNull
        system

    let json = """{
  "Name": "system1",
  "Flows": [
    {
      "Name": "F1",
      "Vertices": [
        {
          "Case": "VDWork",
          "Fields": [
            {
              "Name": "F1W1",
              "Vertices": [
                {
                  "Case": "VDAction",
                  "Fields": [
                    {
                      "Name": "F1W1C1"
                    }
                  ]
                },
                {
                  "Case": "VDAction",
                  "Fields": [
                    {
                      "Name": "F1W1C2"
                    }
                  ]
                }
              ],
              "Edges": [
                {
                  "Source": "F1W1C1",
                  "Target": "F1W1C2",
                  "EdgeType": {
                    "Case": "Start"
                  }
                }
              ]
            }
          ]
        }
      ],
      "Edges": []
    }
  ]
}"""
    /// Json Test
    type T() =
        [<Test>]
        member _.``Minimal`` () =
            let jsonText = system.Serialize();
            DcClipboard.Write(jsonText)

            jsonText === json

            let system2 = DsSystem.Deserialize(jsonText)
            let f1 = system2.Flows[0]
            let f1w1 = f1.Works[0]
            f1.System === system2
            f1w1.Flow === f1
            let f1w1c1, f1w1c2 = f1w1.Vertices[0], f1w1.Vertices[1]
            f1w1c1.AsVertex().Container === VCWork f1w1
            f1w1c2.AsVertex().Container === VCWork f1w1

            f1w1.Graph.Edges.Count === 1
            let e = f1w1.Graph.Edges.First()
            VertexDetail.FromVertex(e.Source) === f1w1c1
            VertexDetail.FromVertex(e.Target) === f1w1c2
            e.EdgeType === CausalEdgeType.Start

            let json2 = system2.Serialize()
            jsonText === json2

        /// Flow 바로 아래에 존재하는 coin 생성 및 연결 test
        [<Test>]
        member _.``FlowDirectCoin`` () =
            let system2 = EmJson.Duplicate(system, JsonSerializerSettings(ReferenceLoopHandling = ReferenceLoopHandling.Ignore))
            let f1 = system2.Flows[0]
            let f1w1 = f1.Works[0]
            let f1Cmd1 = f1.AddVertex(new DsCommand("F1Cmd1"))
            let f1Op1  = f1.AddVertex(new DsOperator("F1Op1"))
            let f1c1   = f1.AddVertex(new DsAction("F1C1"))
            let f1c2   = f1.AddVertex(new DsAction("F1C2"))
            f1.CreateEdge(f1c1, f1w1, CausalEdgeType.Start) |> verifyNonNull
            f1.CreateEdge(f1c2, f1w1, CausalEdgeType.Start) |> verifyNonNull

            let jsonText = system2.Serialize()
            DcClipboard.Write(jsonText);
            let str = """"Edges": [
        {
          "Source": "F1C1",
          "Target": "F1W1",
          "EdgeType": {
            "Case": "Start"
          }
        }"""
            jsonText.Contains(str) === true

            let str = """"Edges": [
        {
          "Source": "F1C1",
          "Target": "F1W1",
          "EdgeType": {
            "Case": "Start"
          }
        },
        {
          "Source": "F1C2",
          "Target": "F1W1",
          "EdgeType": {
            "Case": "Start"
          }
        }
      ]"""
            jsonText.Contains(str) === true


            let json2 = system2.Serialize()
            jsonText === json2


        [<Test>]
        member _.``FQDN`` () =
            let system = DsSystem.Deserialize(json)
            let f1 = system.Flows[0]
            let f1w1 = f1.Works[0]
            let f1w1c1, f1w1c2 = f1w1.Vertices[0].AsVertex(), f1w1.Vertices[1].AsVertex()

            system.Fqdn() === "system1"
            f1    .Fqdn() === "system1.F1"
            f1w1  .Fqdn() === "system1.F1.F1W1"
            f1w1c1.Fqdn() === "system1.F1.F1W1.F1W1C1"
            f1w1c2.Fqdn() === "system1.F1.F1W1.F1W1C2"

            /// 객체에서 그 하부의 LQDN 으로 찾기
            system.TryFindLqdnObj("F1")            .Value === f1
            system.TryFindLqdnObj("F999")                 === None
            system.TryFindLqdnObj("F1.F1W1")       .Value === f1w1
            system.TryFindLqdnObj("F1.F1W1.F1W1C1").Value === f1w1c1
            system.TryFindLqdnObj("F1.F1W1.F1W1C2").Value === f1w1c2
            f1    .TryFindLqdnObj("F1W1")          .Value === f1w1
            f1    .TryFindLqdnObj("F1W1")          .Value === f1w1
            f1    .TryFindLqdnObj("F1W1.F1W1C1")   .Value === f1w1c1
            f1    .TryFindLqdnObj("F1W1.F1W1C2")   .Value === f1w1c2
            f1w1  .TryFindLqdnObj("F1W1C1")        .Value === f1w1c1
            f1w1  .TryFindLqdnObj("F1W1C2")        .Value === f1w1c2

            system.TryFindLqdnObj(["F1"; "F1W1"; "F1W1C1"]).Value === f1w1c1
            system.TryFindLqdnObj(["F1"; "F1W1"; "F1W1C2"]).Value === f1w1c2

            system.GetSystem() === system
            f1.System === system
            f1w1.GetSystem() === system 
            f1w1c1.GetSystem() === system

            (fun () -> system.GetFlow() |> ignore) |> ShouldFail
            f1    .GetFlow() === f1
            f1w1  .GetFlow() === f1
            f1w1c1.GetFlow() === f1

            system.TryGetWork() === None
            f1    .TryGetWork() === None
            f1w1  .TryGetWork().Value === f1w1
            f1w1c1.TryGetWork().Value === f1w1


            let f1c1 = f1.AddVertex(new DsAction("F1C1"))
            f1c1.GetSystem() === system
            f1c1.GetFlow() === f1
            f1c1.TryGetWork() === None


            let f1w1c1 = f1w1.AddVertex(new DsCommand("F1W1C1"))
            f1w1c1.GetSystem() === system
            f1w1c1.GetFlow() === f1
            f1w1c1.TryGetWork().Value === f1w1



