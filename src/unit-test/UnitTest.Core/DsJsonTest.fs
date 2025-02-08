namespace T.Core


open System.Linq
open NUnit.Framework
open Newtonsoft.Json

open Dual.Common.UnitTest.FS
open Dual.Common.Base.CS
open Dual.Common.Base.FS
open Dual.Common.Core.FS

open Dual.Ev2

module DsJson =
    /// System1 > Flow1 > Work1 > {Call1 -> Call2}
    let createSystem() =
        let system = DsSystem.Create("system1")
        let flow1 = system.CreateFlow("F1")
        let work1, vWork1 = flow1.CsAddWork("F1W1");
        let call1 = work1.AddVertex(new DsAction("F1W1C1", work1))
        let call2 = work1.AddVertex(new DsAction("F1W1C2", work1))

        work1.CreateEdge(call1, call2, CausalEdgeType.Start) |> verifyNonNull
        system

    let system = createSystem()

    let dsJson = """{
  "Name": "system1",
  "Guid": "951b7709-868a-4cad-a74f-3458cc649244",
  "Flows": [
    {
      "Name": "F1",
      "Guid": "951b7709-868a-4cad-a74f-3458cc649244",
      "Works": [
        {
          "Name": "F1W1",
          "Guid": "951b7709-868a-4cad-a74f-3458cc649244",
          "Actions": [
            {
              "Name": "F1W1C1",
              "Guid": "951b7709-868a-4cad-a74f-3458cc649244",
              "IsDisabled": false,
              "IsPush": false
            },
            {
              "Name": "F1W1C2",
              "Guid": "951b7709-868a-4cad-a74f-3458cc649244",
              "IsDisabled": false,
              "IsPush": false
            }
          ],
          "VertexDTOs": [
            {
              "Guid": "0f866b00-8ef8-4a44-9bde-f124f14110cb",
              "ContentGuid": "951b7709-868a-4cad-a74f-3458cc649244"
            },
            {
              "Guid": "d33a35d7-ce45-407d-940e-5d7a0dc8f87a",
              "ContentGuid": "951b7709-868a-4cad-a74f-3458cc649244"
            }
          ],
          "EdgeDTOs": [
            {
              "Source": "0f866b00-8ef8-4a44-9bde-f124f14110cb",
              "Target": "d33a35d7-ce45-407d-940e-5d7a0dc8f87a",
              "EdgeType": {
                "Case": "Start"
              }
            }
          ]
        }
      ],
      "VertexDTOs": [
        {
          "Guid": "249712fa-113f-407d-837a-faff6b80e62c",
          "ContentGuid": "951b7709-868a-4cad-a74f-3458cc649244"
        }
      ],
      "EdgeDTOs": []
    }
  ]
}"""


    /// Json Test
    type T() =
        [<Test>]
        member _.``Minimal`` () =
            let xxx = createSystem()
            let jsonText = system.ToJson();
            DcClipboard.Write(jsonText)

            let xxx = jsonText.ZeroFillGuid()
            jsonText.ZeroFillGuid() === dsJson.ZeroFillGuid()

            let system2 = DsSystem.FromJson(jsonText)
            let f1 = system2.Flows[0]
            let f1w1 = f1.Works[0]
            f1.System === system2
            f1w1.Flow === f1

            let vs = f1w1.Vertices.ToArray()
            let f1w1c1, f1w1c2 = vs[0], vs[1]
            f1w1c1.Content.Container === f1w1
            f1w1c2.Content.Container === f1w1

            let actions = f1w1.Actions.ToArray()
            actions[0].Work === f1w1
            actions[1].Work === f1w1

            f1w1.Graph.Edges.Count === 1
            let e = f1w1.Graph.Edges.First()
            e.Source === f1w1c1
            e.Target === f1w1c2
            e.EdgeType === CausalEdgeType.Start

            let json2 = system2.ToJson()
            jsonText === json2

        /// Flow 바로 아래에 존재하는 coin 생성 및 연결 test
        [<Test>]
        member _.``FlowDirectCoin`` () =
            let system2 = EmJson.Duplicate(system, JsonSerializerSettings(ReferenceLoopHandling = ReferenceLoopHandling.Ignore))
            let f1 = system2.Flows[0]
            let f1w1 = f1.Works[0]
            //let f1Cmd1 = f1.AddVertex(new DsCommand("F1Cmd1"))
            //let f1Op1  = f1.AddVertex(new DsOperator("F1Op1"))
            //let f1c1   = f1.AddVertex(new DsAction("F1C1"))
            //let f1c2   = f1.AddVertex(new DsAction("F1C2"))
            //f1.CreateEdge(f1c1, f1w1, CausalEdgeType.Start) |> verifyNonNull
            //f1.CreateEdge(f1c2, f1w1, CausalEdgeType.Start) |> verifyNonNull

            let jsonText = system2.ToJson()
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


            let json2 = system2.ToJson()
            jsonText === json2


        /// - Ds object (DsNamedObject) 로부터
        ///
        ///   -  FQDN 얻기 test,
        ///
        ///   -  Lqdn 이용해서 하부의 객체 얻기 test,
        ///
        ///   -  System, Flow, Work 객체 얻기 test
        [<Test>]
        member _.``Fqdn && Lqdn search`` () =
            let system = DsSystem.FromJson(dsJson)
            let f1 = system.Flows[0]
            let f1w1 = f1.Works[0]
            let vs = f1w1.Vertices.ToArray()
            let f1w1c1 = vs[0].Content :?> DsAction
            let f1w1c2 = vs[1].Content :?> DsAction

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

            system.GetWork() === null
            f1    .GetWork() === null
            f1w1  .GetWork() === f1w1
            f1w1c1.GetWork() === f1w1


            //let f1c1 = f1.AddVertex(new DsAction("F1C1"))
            //f1c1.GetSystem() === system
            //f1c1.GetFlow() === f1
            //f1c1.GetWork() === None


            //let f1w1c1 = f1w1.AddVertex(new DsCommand("F1W1C1"))
            //f1w1c1.GetSystem() === system
            //f1w1c1.GetFlow() === f1
            //f1w1c1.GetWork().Value === f1w1


        [<Test>]
        member _.``Coins`` () =
            let system = DsSystem.FromJson(dsJson)
            let f1 = system.Flows[0]
            let f1w1 = f1.Works[0]
            let vs = f1w1.Vertices.ToArray()
            let f1w1c1 = vs[0].Content :?> DsAction
            let f1w1c2 = vs[1].Content :?> DsAction

            //let f1w1s1 = f1w1.AddVertex(new DsSafety("F1W1Saf1", [|"F2.W1.C999"; "F2.W1.C998"; |]))
            //f1w1.CreateEdge(f1w1s1, f1w1c1, CausalEdgeType.Start) |> verifyNonNull
            //let jsonText = system.ToJson()
            //DcClipboard.Write(jsonText)
            ()




