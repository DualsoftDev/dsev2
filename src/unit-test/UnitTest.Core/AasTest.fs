namespace T.Core

open Dual.Ev2
open Dual.Ev2.Aas
open Dual.Ev2.Aas.CoreToJsonViaCode
open NUnit.Framework
open Dual.Common.UnitTest.FS
open Dual.Common.Base.CS



type JNode = System.Text.Json.Nodes.JsonNode


module Aas1 =
    let [<Literal>] aas = "http://www.admin-shell.io/aas/3/0"
    let json = Json.json

    /// Json Test
    type T() =
        let loadAssetAdministrationShells(aasJson:string) =
            let container:Aas.Environment = J.CreateIClass<Aas.Environment>(aasJson)
            //let xxx = container.AssetAdministrationShells[0]
            //xxx
            container

        let aasJson = """{
  "assetAdministrationShells": [
    {
      "id": "something_142922d6",
      "assetInformation": {
        "assetKind": "NotApplicable",
        "globalAssetId": "something_eea66fa1"
      },
      "modelType": "AssetAdministrationShell"
    }
  ]
}"""
        let aasXml = """<environment xmlns="https://admin-shell.io/aas/3/0">
  <assetAdministrationShells>
    <assetAdministrationShell>
      <id>something_142922d6</id>
      <assetInformation>
        <assetKind>NotApplicable</assetKind>
        <globalAssetId>something_eea66fa1</globalAssetId>
      </assetInformation>
    </assetAdministrationShell>
  </assetAdministrationShells>
</environment>"""

        [<Test>]
        member _.``AasShell: JObj -> string conversion test`` () =
            let assetInformation = JObj().Set(N.AssetKind, "NotApplicable").Set(N.GlobalAssetId, "something_eea66fa1")
            let assetAdministrationShell = JObj().Set(N.Id, "something_142922d6").Set(N.AssetInformation, assetInformation).Set(N.ModelType, "AssetAdministrationShell")
            let assetAdministrationShells = JObj().Set(N.AssetAdministrationShells, JArr [|assetAdministrationShell|])
            let json = assetAdministrationShells.Stringify()
            json === aasJson

        [<Test>]
        member _.``AasShell: Json -> JObj -> {Xml, Json} conversion test`` () =

            //let shell = loadAssetAdministrationShells(aasJson) :?> Aas.AssetAdministrationShell
            let env = loadAssetAdministrationShells(aasJson)
            let xml = env.ToXml()
            xml === aasXml


            let jsonObject = Aas.Jsonization.Serialize.ToJsonObject(env);
            let json = jsonObject.Stringify()
            json === aasJson


            let system2 = DsSystem.FromJson(json)
            let json = system2.ToJsonViaCode().Stringify()
            ()

        [<Test>]
        member _.``CoreToJsonViaCodeTest`` () =
            let system2 = DsSystem.FromJson(json)
            let json = system2.ToJsonViaCode().Stringify()
            ()



        [<Test>]
        member _.``Edge: instance -> JObj -> Json -> Xml ConversionTest`` () =
            let jsonAnswer = """{
  "modelType": "SubmodelElementCollection",
  "idShort": "Edge",
  "semanticId": {
    "type": "ExternalReference",
    "keys": [
      {
        "type": "ConceptDescription",
        "value": "Start"
      }
    ]
  },
  "value": [
    {
      "category": "CONSTANT",
      "modelType": "SubmodelElementCollection",
      "idShort": "Source",
      "semanticId": {
        "type": "ExternalReference",
        "keys": [
          {
            "type": "ConceptDescription",
            "value": "Dual__source"
          }
        ]
      }
    },
    {
      "category": "CONSTANT",
      "modelType": "SubmodelElementCollection",
      "idShort": "Target",
      "semanticId": {
        "type": "ExternalReference",
        "keys": [
          {
            "type": "ConceptDescription",
            "value": "Dual__target"
          }
        ]
      }
    }
  ]
}"""

            let xmlAnswer = """<submodelElementCollection xmlns="https://admin-shell.io/aas/3/0">
  <idShort>Edge</idShort>
  <semanticId>
    <type>ExternalReference</type>
    <keys>
      <key>
        <type>ConceptDescription</type>
        <value>Start</value>
      </key>
    </keys>
  </semanticId>
  <value>
    <submodelElementCollection>
      <category>CONSTANT</category>
      <idShort>Source</idShort>
      <semanticId>
        <type>ExternalReference</type>
        <keys>
          <key>
            <type>ConceptDescription</type>
            <value>Dual__source</value>
          </key>
        </keys>
      </semanticId>
    </submodelElementCollection>
    <submodelElementCollection>
      <category>CONSTANT</category>
      <idShort>Target</idShort>
      <semanticId>
        <type>ExternalReference</type>
        <keys>
          <key>
            <type>ConceptDescription</type>
            <value>Dual__target</value>
          </key>
        </keys>
      </semanticId>
    </submodelElementCollection>
  </value>
</submodelElementCollection>"""
            let edgeDTO = EdgeDTO("Dual__source", "Dual__target", CausalEdgeType.Start)
            let json = edgeDTO.ToSMEC().Stringify()
            DcClipboard.Write(json)
            json === jsonAnswer

            let smec:Aas.SubmodelElementCollection = Aas.Jsonization.Deserialize.SubmodelElementCollectionFrom(JNode.Parse(json))
            let xml = smec.ToXml()
            xml === xmlAnswer

            let smec:Aas.SubmodelElementCollection = J.CreateIClass<Aas.SubmodelElementCollection>(json)
            let xml = smec.ToXml()
            xml === xmlAnswer
            ()


        [<Test>]
        member _.``Action: instance -> JObj -> Json ConversionTest`` () =
            let action:VertexDetail = Action <| DsAction("action1")
            let jsonAnswer = """{
  "modelType": "SubmodelElementCollection",
  "idShort": "Action",
  "semanticId": {
    "type": "ExternalReference",
    "keys": [
      {
        "type": "ConceptDescription",
        "value": "action1"
      }
    ]
  }
}"""
            let json = action.ToProperties().Stringify()
            json === jsonAnswer
            ()


        [<Test>]
        member _.``Command: instance -> JObj -> Json ConversionTest`` () =
            let Command:VertexDetail = Command <| DsCommand("command1")
            let jsonAnswer = """{
  "modelType": "SubmodelElementCollection",
  "idShort": "Command",
  "semanticId": {
    "type": "ExternalReference",
    "keys": [
      {
        "type": "ConceptDescription",
        "value": "command1"
      }
    ]
  }
}"""
            let json = Command.ToProperties().Stringify()
            DcClipboard.Write(json)
            json === jsonAnswer
            ()


        [<Test>]
        member _.``Work: instance -> JObj -> Json ConversionTest`` () =

            let system2 = DsSystem.FromJson(json)
            let work = system2.Flows[0].Works[0]
            let jsonAnswer = """{
  "modelType": "SubmodelElementCollection",
  "idShort": "Work",
  "semanticId": {
    "type": "ExternalReference",
    "keys": [
      {
        "type": "ConceptDescription",
        "value": "F1W1"
      }
    ]
  },
  "value": [
    {
      "modelType": "SubmodelElementCollection",
      "idShort": "Graph",
      "semanticId": {
        "type": "ExternalReference",
        "keys": [
          {
            "type": "ConceptDescription",
            "value": "Graph"
          }
        ]
      },
      "value": [
        {
          "modelType": "SubmodelElementCollection",
          "idShort": "Vertices",
          "semanticId": {
            "type": "ExternalReference",
            "keys": [
              {
                "type": "ConceptDescription",
                "value": "Vertices"
              }
            ]
          },
          "value": [
            {
              "modelType": "SubmodelElementCollection",
              "idShort": "Action",
              "semanticId": {
                "type": "ExternalReference",
                "keys": [
                  {
                    "type": "ConceptDescription",
                    "value": "F1W1C1"
                  }
                ]
              }
            },
            {
              "modelType": "SubmodelElementCollection",
              "idShort": "Action",
              "semanticId": {
                "type": "ExternalReference",
                "keys": [
                  {
                    "type": "ConceptDescription",
                    "value": "F1W1C2"
                  }
                ]
              }
            }
          ]
        },
        {
          "modelType": "SubmodelElementCollection",
          "idShort": "Edges",
          "semanticId": {
            "type": "ExternalReference",
            "keys": [
              {
                "type": "ConceptDescription",
                "value": "Edges"
              }
            ]
          },
          "value": [
            {
              "modelType": "SubmodelElementCollection",
              "idShort": "Edge",
              "semanticId": {
                "type": "ExternalReference",
                "keys": [
                  {
                    "type": "ConceptDescription",
                    "value": "Start"
                  }
                ]
              },
              "value": [
                {
                  "category": "CONSTANT",
                  "modelType": "SubmodelElementCollection",
                  "idShort": "Source",
                  "semanticId": {
                    "type": "ExternalReference",
                    "keys": [
                      {
                        "type": "ConceptDescription",
                        "value": "F1W1C1"
                      }
                    ]
                  }
                },
                {
                  "category": "CONSTANT",
                  "modelType": "SubmodelElementCollection",
                  "idShort": "Target",
                  "semanticId": {
                    "type": "ExternalReference",
                    "keys": [
                      {
                        "type": "ConceptDescription",
                        "value": "F1W1C2"
                      }
                    ]
                  }
                }
              ]
            }
          ]
        }
      ]
    }
  ]
}"""
            let json = work.ToProperties().Stringify()
            DcClipboard.Write(json)
            json === jsonAnswer

            let xml = J.CreateIClass<Aas.SubmodelElementCollection>(json).ToXml()
            ()


        [<Todo("Fix me")>]
        [<Test>]
        member _.``Flow: instance -> JObj -> Json ConversionTest`` () =

            let system2 = DsSystem.FromJson(json)
            let flow = system2.Flows[0]
            let jsonAnswer = """{
  "modelType": "SubmodelElementCollection",
  "idShort": "Flow",
  "semanticId": {
    "type": "ExternalReference",
    "keys": [
      {
        "type": "ConceptDescription",
        "value": "F1"
      }
    ]
  },
  "value": [
    {
      "modelType": "SubmodelElementCollection",
      "idShort": "Graph",
      "semanticId": {
        "type": "ExternalReference",
        "keys": [
          {
            "type": "ConceptDescription",
            "value": "Graph"
          }
        ]
      },
      "value": [
        {
          "modelType": "SubmodelElementCollection",
          "idShort": "Vertices",
          "semanticId": {
            "type": "ExternalReference",
            "keys": [
              {
                "type": "ConceptDescription",
                "value": "Vertices"
              }
            ]
          },
          "value": [
            {
              "modelType": "SubmodelElementCollection",
              "idShort": "Work",
              "semanticId": {
                "type": "ExternalReference",
                "keys": [
                  {
                    "type": "ConceptDescription",
                    "value": "F1W1"
                  }
                ]
              },
              "value": [
                {
                  "modelType": "SubmodelElementCollection",
                  "idShort": "Graph",
                  "semanticId": {
                    "type": "ExternalReference",
                    "keys": [
                      {
                        "type": "ConceptDescription",
                        "value": "Graph"
                      }
                    ]
                  },
                  "value": [
                    {
                      "modelType": "SubmodelElementCollection",
                      "idShort": "Vertices",
                      "semanticId": {
                        "type": "ExternalReference",
                        "keys": [
                          {
                            "type": "ConceptDescription",
                            "value": "Vertices"
                          }
                        ]
                      },
                      "value": [
                        {
                          "modelType": "SubmodelElementCollection",
                          "idShort": "Action",
                          "semanticId": {
                            "type": "ExternalReference",
                            "keys": [
                              {
                                "type": "ConceptDescription",
                                "value": "F1W1C1"
                              }
                            ]
                          }
                        },
                        {
                          "modelType": "SubmodelElementCollection",
                          "idShort": "Action",
                          "semanticId": {
                            "type": "ExternalReference",
                            "keys": [
                              {
                                "type": "ConceptDescription",
                                "value": "F1W1C2"
                              }
                            ]
                          }
                        }
                      ]
                    },
                    {
                      "modelType": "SubmodelElementCollection",
                      "idShort": "Edges",
                      "semanticId": {
                        "type": "ExternalReference",
                        "keys": [
                          {
                            "type": "ConceptDescription",
                            "value": "Edges"
                          }
                        ]
                      },
                      "value": [
                        {
                          "modelType": "SubmodelElementCollection",
                          "idShort": "Edge",
                          "semanticId": {
                            "type": "ExternalReference",
                            "keys": [
                              {
                                "type": "ConceptDescription",
                                "value": "Start"
                              }
                            ]
                          },
                          "value": [
                            {
                              "category": "CONSTANT",
                              "modelType": "SubmodelElementCollection",
                              "idShort": "Source",
                              "semanticId": {
                                "type": "ExternalReference",
                                "keys": [
                                  {
                                    "type": "ConceptDescription",
                                    "value": "F1W1C1"
                                  }
                                ]
                              }
                            },
                            {
                              "category": "CONSTANT",
                              "modelType": "SubmodelElementCollection",
                              "idShort": "Target",
                              "semanticId": {
                                "type": "ExternalReference",
                                "keys": [
                                  {
                                    "type": "ConceptDescription",
                                    "value": "F1W1C2"
                                  }
                                ]
                              }
                            }
                          ]
                        }
                      ]
                    }
                  ]
                }
              ]
            }
          ]
        },
        {
          "modelType": "SubmodelElementCollection",
          "idShort": "Edges",
          "semanticId": {
            "type": "ExternalReference",
            "keys": [
              {
                "type": "ConceptDescription",
                "value": "Edges"
              }
            ]
          }
        }
      ]
    }
  ]
}"""
            let json = flow.ToProperties().Stringify()
            DcClipboard.Write(json)
            json === jsonAnswer

            let xml = J.CreateIClass<Aas.SubmodelElementCollection>(json).ToXml()

            ()
