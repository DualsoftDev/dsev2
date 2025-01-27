namespace T.Core


open System.Linq
open NUnit.Framework

open Dual.Common.UnitTest.FS
open Dual.Common.Base.CS
open Dual.Common.Core.FS

open Dual.Ev2

module JsonSerialization =
    let system =
        let system = DsSystem.Create("system1")
        let flow1 = system.CreateFlow("flow1")
        let work1 = flow1.CreateWork("work1")
        let call1 = work1.AddVertex(new DsAction("call1"))
        let call2 = work1.AddVertex(new DsAction("call2"))

        work1.CreateEdge(call1, call2, CausalEdgeType.Start) |> verifyNonNull
        system

    let json = """{
  "Name": "system1",
  "Flows": [
    {
      "Name": "flow1",
      "Vertices": [
        {
          "Case": "VDWork",
          "Fields": [
            {
              "Name": "work1",
              "Vertices": [
                {
                  "Case": "VDAction",
                  "Fields": [
                    {
                      "Name": "call1"
                    }
                  ]
                },
                {
                  "Case": "VDAction",
                  "Fields": [
                    {
                      "Name": "call2"
                    }
                  ]
                }
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
          ]
        }
      ],
      "Edges": []
    }
  ]
}"""

    [<Test>]
    let ``MinimalJSONSerialization`` () =
        let jsonText = system.Serialize();
        DcClipboard.Write(jsonText);

        jsonText === json

        let system2 = DsSystem.Deserialize(jsonText)
        system2.Flows[0].System === system2
        system2.Flows[0].Works[0].Flow === system2.Flows[0]
        system2.Flows[0].Works[0].Vertices[0].AsVertex().Container === VCWork system2.Flows[0].Works[0]
        system2.Flows[0].Works[0].Vertices[1].AsVertex().Container === VCWork system2.Flows[0].Works[0]

        system2.Flows[0].Works[0].Graph.Edges.Count === 1
        let e = system2.Flows[0].Works[0].Graph.Edges.First()
        VertexDetail.FromVertex(e.Source) === system2.Flows[0].Works[0].Vertices[0]
        VertexDetail.FromVertex(e.Target) === system2.Flows[0].Works[0].Vertices[1]
        e.EdgeType === CausalEdgeType.Start

        let json2 = system2.Serialize()
        jsonText === json2

