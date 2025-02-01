namespace T.Core

open Dual.Ev2
open Dual.Ev2.Aas
open Dual.Ev2.Aas.CoreToJsonViaCode
open NUnit.Framework
open Dual.Common.UnitTest.FS
open Dual.Common.Base.CS


module ToAasTest =
    let dsJson = DsJson.dsJson
    let aasJson = Aas.aasJson0
    let aasXml = Aas.aasXml0

    /// Json Test
    type T() =
        [<Test>]
        member _.``AasShell: JObj -> string conversion test`` () =
            let assetInformation = JObj().Set(N.AssetKind, "NotApplicable").Set(N.GlobalAssetId, "something_eea66fa1")
            let assetAdministrationShell = JObj().Set(N.Id, "something_142922d6").Set(N.AssetInformation, assetInformation).Set(N.ModelType, "AssetAdministrationShell")
            let assetAdministrationShells = JObj().Set(N.AssetAdministrationShells, J.CreateJArr( [assetAdministrationShell] ))
            let json = assetAdministrationShells.Stringify()
            json === aasJson

        [<Test>]
        member _.``AasShell: Json -> JObj -> {Xml, Json} conversion test`` () =
            let env = J.CreateIClassFromJson<Aas.Environment>(aasJson)
            let xml = env.ToXml()
            xml === aasXml


            let jsonObject = Aas.Jsonization.Serialize.ToJsonObject(env);
            let json = jsonObject.Stringify()
            json === aasJson

            env.ToJson() === aasJson


            let system2 = DsSystem.FromJson(dsJson)
            let json = system2.ToJson()
            json === dsJson

            let json = system2.ToJsonViaCode().Stringify()
            ()

        [<Test>]
        member _.``CoreToJsonViaCodeTest`` () =
            let system2 = DsSystem.FromJson(dsJson)
            let json = system2.ToJsonViaCode().Stringify()
            ()



        [<Test>]
        member _.``Edge: instance -> JObj -> Json -> Xml ConversionTest`` () =
            let jsonAnswer = """{
  "modelType": "SubmodelElementCollection",
  "idShort": "Edge",
  "value": [
    {
      "modelType": "Property",
      "idShort": "EdgeType",
      "valueType": "xs:string",
      "value": "Start"
    },
    {
      "modelType": "Property",
      "idShort": "Source",
      "valueType": "xs:string",
      "value": "Dual__source"
    },
    {
      "modelType": "Property",
      "idShort": "Target",
      "valueType": "xs:string",
      "value": "Dual__target"
    }
  ]
}"""

            let xmlAnswer = """<submodelElementCollection xmlns="https://admin-shell.io/aas/3/0">
  <idShort>Edge</idShort>
  <value>
    <property>
      <idShort>EdgeType</idShort>
      <valueType>xs:string</valueType>
      <value>Start</value>
    </property>
    <property>
      <idShort>Source</idShort>
      <valueType>xs:string</valueType>
      <value>Dual__source</value>
    </property>
    <property>
      <idShort>Target</idShort>
      <valueType>xs:string</valueType>
      <value>Dual__target</value>
    </property>
  </value>
</submodelElementCollection>"""
            let edgeDTO = EdgeDTO("Dual__source", "Dual__target", CausalEdgeType.Start)
            let json = edgeDTO.ToSMC().Stringify()
            DcClipboard.Write(json)
            json === jsonAnswer

            let smec:Aas.SubmodelElementCollection = Aas.Jsonization.Deserialize.SubmodelElementCollectionFrom(JNode.Parse(json))
            let xml = smec.ToXml()
            xml === xmlAnswer

            let smec:Aas.SubmodelElementCollection = J.CreateIClassFromJson<Aas.SubmodelElementCollection>(json)
            let xml = smec.ToXml()
            xml === xmlAnswer
            ()


        [<Test>]
        member _.``Action: instance -> JObj -> Json ConversionTest`` () =
            let action:VertexDetail = Action <| DsAction("action1")
            let jsonAnswer = """{
  "modelType": "SubmodelElementCollection",
  "idShort": "Action",
  "value": [
    {
      "modelType": "Property",
      "idShort": "Name",
      "valueType": "xs:string",
      "value": "action1"
    },
    {
      "modelType": "Property",
      "idShort": "IsDisable",
      "valueType": "xs:boolean",
      "value": "False"
    }
  ]
}"""
            let json = action.ToSMC().Stringify()
            json === jsonAnswer

            let xml = J.CreateIClassFromJson<Aas.SubmodelElementCollection>(jsonAnswer).ToXml()

            ()


        [<Test>]
        member _.``Command: instance -> JObj -> Json ConversionTest`` () =
            let Command:VertexDetail = Command <| DsCommand("command1")
            let jsonAnswer = """{
  "modelType": "SubmodelElementCollection",
  "idShort": "Command",
  "value": [
    {
      "modelType": "Property",
      "idShort": "Name",
      "valueType": "xs:string",
      "value": "command1"
    }
  ]
}"""
            let json = Command.ToSMC().Stringify()
            DcClipboard.Write(json)
            json === jsonAnswer
            ()


        [<Test>]
        member _.``Work: instance -> JObj -> Json ConversionTest`` () =

            let system2 = DsSystem.FromJson(dsJson)
            let work = system2.Flows[0].Works[0]
            let jsonAnswer = """{
  "modelType": "SubmodelElementCollection",
  "idShort": "Work",
  "value": [
    {
      "modelType": "Property",
      "idShort": "Name",
      "valueType": "xs:string",
      "value": "F1W1"
    },
    {
      "modelType": "SubmodelElementCollection",
      "idShort": "Graph",
      "value": [
        {
          "modelType": "SubmodelElementCollection",
          "idShort": "Vertices",
          "value": [
            {
              "modelType": "SubmodelElementCollection",
              "idShort": "Action",
              "value": [
                {
                  "modelType": "Property",
                  "idShort": "Name",
                  "valueType": "xs:string",
                  "value": "F1W1C1"
                },
                {
                  "modelType": "Property",
                  "idShort": "IsDisable",
                  "valueType": "xs:boolean",
                  "value": "False"
                }
              ]
            },
            {
              "modelType": "SubmodelElementCollection",
              "idShort": "Action",
              "value": [
                {
                  "modelType": "Property",
                  "idShort": "Name",
                  "valueType": "xs:string",
                  "value": "F1W1C2"
                },
                {
                  "modelType": "Property",
                  "idShort": "IsDisable",
                  "valueType": "xs:boolean",
                  "value": "False"
                }
              ]
            }
          ]
        },
        {
          "modelType": "SubmodelElementCollection",
          "idShort": "Edges",
          "value": [
            {
              "modelType": "SubmodelElementCollection",
              "idShort": "Edge",
              "value": [
                {
                  "modelType": "Property",
                  "idShort": "EdgeType",
                  "valueType": "xs:string",
                  "value": "Start"
                },
                {
                  "modelType": "Property",
                  "idShort": "Source",
                  "valueType": "xs:string",
                  "value": "F1W1C1"
                },
                {
                  "modelType": "Property",
                  "idShort": "Target",
                  "valueType": "xs:string",
                  "value": "F1W1C2"
                }
              ]
            }
          ]
        }
      ]
    }
  ]
}"""
            let json = work.ToSMC().Stringify()
            DcClipboard.Write(json)
            json === jsonAnswer

            let xml = J.CreateIClassFromJson<Aas.SubmodelElementCollection>(json).ToXml()
            ()


        [<Test>]
        member _.``Flow: instance -> JObj -> Json ConversionTest`` () =

            let system2 = DsSystem.FromJson(dsJson)
            let flow = system2.Flows[0]
            let jsonAnswer = """{
  "modelType": "SubmodelElementCollection",
  "idShort": "Flow",
  "value": [
    {
      "modelType": "Property",
      "idShort": "Name",
      "valueType": "xs:string",
      "value": "F1"
    },
    {
      "modelType": "SubmodelElementCollection",
      "idShort": "Graph",
      "value": [
        {
          "modelType": "SubmodelElementCollection",
          "idShort": "Vertices",
          "value": [
            {
              "modelType": "SubmodelElementCollection",
              "idShort": "Work",
              "value": [
                {
                  "modelType": "Property",
                  "idShort": "Name",
                  "valueType": "xs:string",
                  "value": "F1W1"
                },
                {
                  "modelType": "SubmodelElementCollection",
                  "idShort": "Graph",
                  "value": [
                    {
                      "modelType": "SubmodelElementCollection",
                      "idShort": "Vertices",
                      "value": [
                        {
                          "modelType": "SubmodelElementCollection",
                          "idShort": "Action",
                          "value": [
                            {
                              "modelType": "Property",
                              "idShort": "Name",
                              "valueType": "xs:string",
                              "value": "F1W1C1"
                            },
                            {
                              "modelType": "Property",
                              "idShort": "IsDisable",
                              "valueType": "xs:boolean",
                              "value": "False"
                            }
                          ]
                        },
                        {
                          "modelType": "SubmodelElementCollection",
                          "idShort": "Action",
                          "value": [
                            {
                              "modelType": "Property",
                              "idShort": "Name",
                              "valueType": "xs:string",
                              "value": "F1W1C2"
                            },
                            {
                              "modelType": "Property",
                              "idShort": "IsDisable",
                              "valueType": "xs:boolean",
                              "value": "False"
                            }
                          ]
                        }
                      ]
                    },
                    {
                      "modelType": "SubmodelElementCollection",
                      "idShort": "Edges",
                      "value": [
                        {
                          "modelType": "SubmodelElementCollection",
                          "idShort": "Edge",
                          "value": [
                            {
                              "modelType": "Property",
                              "idShort": "EdgeType",
                              "valueType": "xs:string",
                              "value": "Start"
                            },
                            {
                              "modelType": "Property",
                              "idShort": "Source",
                              "valueType": "xs:string",
                              "value": "F1W1C1"
                            },
                            {
                              "modelType": "Property",
                              "idShort": "Target",
                              "valueType": "xs:string",
                              "value": "F1W1C2"
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
          "idShort": "Edges"
        }
      ]
    }
  ]
}"""
            let json = flow.ToSMC().Stringify()
            DcClipboard.Write(json)
            json === jsonAnswer

            let xml = J.CreateIClassFromJson<Aas.SubmodelElementCollection>(json).ToXml()

            ()


        [<Test>]
        member _.``System: instance -> JObj -> Json ConversionTest`` () =

            let system2 = DsSystem.FromJson(dsJson)
            let jsonAnswer = """{
  "modelType": "SubmodelElementCollection",
  "idShort": "System",
  "value": [
    {
      "modelType": "Property",
      "idShort": "Name",
      "valueType": "xs:string",
      "value": "system1"
    },
    {
      "modelType": "SubmodelElementCollection",
      "idShort": "Flows",
      "value": [
        {
          "modelType": "SubmodelElementCollection",
          "idShort": "Flow",
          "value": [
            {
              "modelType": "Property",
              "idShort": "Name",
              "valueType": "xs:string",
              "value": "F1"
            },
            {
              "modelType": "SubmodelElementCollection",
              "idShort": "Graph",
              "value": [
                {
                  "modelType": "SubmodelElementCollection",
                  "idShort": "Vertices",
                  "value": [
                    {
                      "modelType": "SubmodelElementCollection",
                      "idShort": "Work",
                      "value": [
                        {
                          "modelType": "Property",
                          "idShort": "Name",
                          "valueType": "xs:string",
                          "value": "F1W1"
                        },
                        {
                          "modelType": "SubmodelElementCollection",
                          "idShort": "Graph",
                          "value": [
                            {
                              "modelType": "SubmodelElementCollection",
                              "idShort": "Vertices",
                              "value": [
                                {
                                  "modelType": "SubmodelElementCollection",
                                  "idShort": "Action",
                                  "value": [
                                    {
                                      "modelType": "Property",
                                      "idShort": "Name",
                                      "valueType": "xs:string",
                                      "value": "F1W1C1"
                                    },
                                    {
                                      "modelType": "Property",
                                      "idShort": "IsDisable",
                                      "valueType": "xs:boolean",
                                      "value": "False"
                                    }
                                  ]
                                },
                                {
                                  "modelType": "SubmodelElementCollection",
                                  "idShort": "Action",
                                  "value": [
                                    {
                                      "modelType": "Property",
                                      "idShort": "Name",
                                      "valueType": "xs:string",
                                      "value": "F1W1C2"
                                    },
                                    {
                                      "modelType": "Property",
                                      "idShort": "IsDisable",
                                      "valueType": "xs:boolean",
                                      "value": "False"
                                    }
                                  ]
                                }
                              ]
                            },
                            {
                              "modelType": "SubmodelElementCollection",
                              "idShort": "Edges",
                              "value": [
                                {
                                  "modelType": "SubmodelElementCollection",
                                  "idShort": "Edge",
                                  "value": [
                                    {
                                      "modelType": "Property",
                                      "idShort": "EdgeType",
                                      "valueType": "xs:string",
                                      "value": "Start"
                                    },
                                    {
                                      "modelType": "Property",
                                      "idShort": "Source",
                                      "valueType": "xs:string",
                                      "value": "F1W1C1"
                                    },
                                    {
                                      "modelType": "Property",
                                      "idShort": "Target",
                                      "valueType": "xs:string",
                                      "value": "F1W1C2"
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
                  "idShort": "Edges"
                }
              ]
            }
          ]
        }
      ]
    }
  ]
}"""
            let json = system2.ToSMC().Stringify()
            DcClipboard.Write(json)
            json === jsonAnswer

            let xml = J.CreateIClassFromJson<Aas.SubmodelElementCollection>(json).ToXml()

            ()
