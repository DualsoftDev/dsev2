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
  "Guid": "d428dc1c-9806-4366-84c9-fddc1ab5b98b",
  "Flows": [
    {
      "Name": "F1",
      "Guid": "70ca4de9-84e5-43e4-ba41-6a6574b74595",
      "Works": [
        {
          "Name": "F1W1",
          "Guid": "9a7c646f-cc24-454b-991f-cca08a62ad83",
          "Actions": [
            {
              "Name": "F1W1C1",
              "Guid": "73b49a57-a497-4a8a-8c9a-533dceeb7b5e",
              "IsDisabled": false,
              "IsPush": false
            },
            {
              "Name": "F1W1C2",
              "Guid": "6f6c0275-bd7b-4faa-8e8f-6636ca158625",
              "IsDisabled": false,
              "IsPush": false
            }
          ],
          "VertexDTOs": [
            {
              "Guid": "6926aadb-bfd8-4a08-a37f-25eeaa521b4c",
              "ContentGuid": "73b49a57-a497-4a8a-8c9a-533dceeb7b5e"
            },
            {
              "Guid": "79f5bd27-b753-4d12-8c31-644992e14ec1",
              "ContentGuid": "6f6c0275-bd7b-4faa-8e8f-6636ca158625"
            }
          ],
          "EdgeDTOs": [
            {
              "Source": "6926aadb-bfd8-4a08-a37f-25eeaa521b4c",
              "Target": "79f5bd27-b753-4d12-8c31-644992e14ec1",
              "EdgeType": {
                "Case": "Start"
              }
            }
          ]
        }
      ],
      "VertexDTOs": [
        {
          "Guid": "fb1cd059-3d73-4f5f-acb4-dea10ad276d2",
          "ContentGuid": "9a7c646f-cc24-454b-991f-cca08a62ad83"
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


            let fqdn1 = f1w1c1.Fqdn()
            let fqdn2 = f1w1c2.Fqdn()

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




