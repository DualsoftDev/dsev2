namespace T.Core


open System.Linq
open NUnit.Framework
open Newtonsoft.Json

open Dual.Common.UnitTest.FS
open Dual.Common.Base
open Dual.Common.Base
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
  "Guid": "74ba241e-e871-4b80-a50b-d433d75430eb",
  "Flows": [
    {
      "Name": "F1",
      "Guid": "4f6ea2c2-af30-4c09-8837-5c634ce6082a"
    }
  ],
  "Works": [
    {
      "Name": "F1W1",
      "Guid": "c9da22cb-9ddf-4986-abee-2b539830aa75",
      "Flow": {
        "Name": "F1",
        "Guid": "4f6ea2c2-af30-4c09-8837-5c634ce6082a"
      },
      "Actions": [
        {
          "Name": "F1W1C1",
          "Guid": "6ee88157-4642-4d08-a8af-7f4e327d4f48",
          "IsDisabled": false,
          "IsPush": false
        },
        {
          "Name": "F1W1C2",
          "Guid": "e936f6fb-ed2a-4b2a-8ca3-2324b4a8a955",
          "IsDisabled": false,
          "IsPush": false
        }
      ],
      "VertexDTOs": [
        {
          "Guid": "a5f15801-51a1-4c7b-bb57-4131b1f5d58f",
          "ContentGuid": "6ee88157-4642-4d08-a8af-7f4e327d4f48"
        },
        {
          "Guid": "055a0aa5-3ddc-48c7-8ce1-c2c06df16317",
          "ContentGuid": "e936f6fb-ed2a-4b2a-8ca3-2324b4a8a955"
        }
      ],
      "EdgeDTOs": [
        {
          "Source": "a5f15801-51a1-4c7b-bb57-4131b1f5d58f",
          "Target": "055a0aa5-3ddc-48c7-8ce1-c2c06df16317",
          "EdgeType": {
            "Case": "Start"
          }
        }
      ]
    }
  ],
  "VertexDTOs": [
    {
      "Guid": "4f768533-05bd-49da-93ba-0cb0adc2d2c6",
      "ContentGuid": "c9da22cb-9ddf-4986-abee-2b539830aa75"
    }
  ],
  "EdgeDTOs": []
}"""


    /// Json Test
    type T() =
        [<Test>]
        member _.``Minimal`` () =
            let xxx = createSystem()
            let jsonText = system.ToJson();
            writeClipboard(jsonText)

            let xxx = jsonText.ZeroFillGuid()
            jsonText.ZeroFillGuid() === dsJson.ZeroFillGuid()

            let system2 = DsSystem.FromJson(jsonText)
            let f1 = system2.Flows[0]
            ()


            let f1w1 = system2.Works[0]
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
            let f1w1 = system.Works[0]
            f1w1.Flow === f1
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

        //    /// 객체에서 그 하부의 LQDN 으로 찾기
        //    system.TryFindLqdnObj("F1")            .Value === f1
        //    system.TryFindLqdnObj("F999")                 === None
        //    system.TryFindLqdnObj("F1.F1W1")       .Value === f1w1
        //    system.TryFindLqdnObj("F1.F1W1.F1W1C1").Value === f1w1c1
        //    system.TryFindLqdnObj("F1.F1W1.F1W1C2").Value === f1w1c2
        //    f1    .TryFindLqdnObj("F1W1")          .Value === f1w1
        //    f1    .TryFindLqdnObj("F1W1")          .Value === f1w1
        //    f1    .TryFindLqdnObj("F1W1.F1W1C1")   .Value === f1w1c1
        //    f1    .TryFindLqdnObj("F1W1.F1W1C2")   .Value === f1w1c2
        //    f1w1  .TryFindLqdnObj("F1W1C1")        .Value === f1w1c1
        //    f1w1  .TryFindLqdnObj("F1W1C2")        .Value === f1w1c2

        //    system.TryFindLqdnObj(["F1"; "F1W1"; "F1W1C1"]).Value === f1w1c1
        //    system.TryFindLqdnObj(["F1"; "F1W1"; "F1W1C2"]).Value === f1w1c2

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


        //[<Test>]
        //member _.``Coins`` () =
        //    let system = DsSystem.FromJson(dsJson)
        //    let f1 = system.Flows[0]
        //    let f1w1 = f1.Works[0]
        //    let vs = f1w1.Vertices.ToArray()
        //    let f1w1c1 = vs[0].Content :?> DsAction
        //    let f1w1c2 = vs[1].Content :?> DsAction

        //    //let f1w1s1 = f1w1.AddVertex(new DsSafety("F1W1Saf1", [|"F2.W1.C999"; "F2.W1.C998"; |]))
        //    //f1w1.CreateEdge(f1w1s1, f1w1c1, CausalEdgeType.Start) |> verifyNonNull
        //    //let jsonText = system.ToJson()
        //    //writeClipboard(jsonText)
        //    ()




