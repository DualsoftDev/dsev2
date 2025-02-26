namespace T.Core

open Dual.Ev2
open Dual.Ev2.Aas
open Dual.Ev2.Aas.CoreToJsonViaCode
open NUnit.Framework
open Dual.Common.UnitTest.FS
open Dual.Common.Base.CS
open System
open Dual.Common.Base.FS


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
            let xxx = system2.Name
            let yyy = (system2 :> IWithName).Name

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
      "value": "053c20dc-e3c6-4bbe-930f-82756d311f8f"
    },
    {
      "modelType": "Property",
      "idShort": "Target",
      "valueType": "xs:string",
      "value": "569cbc28-15c9-4437-b87b-88f6f880e6e0"
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
      <value>b337a224-d593-46ba-b5a0-a29264ba31bc</value>
    </property>
    <property>
      <idShort>Target</idShort>
      <valueType>xs:string</valueType>
      <value>d7788816-bddb-4c7a-8ea8-78aa673dfa85</value>
    </property>
  </value>
</submodelElementCollection>"""
            let guid1, guid2 = Guid.NewGuid(), Guid.NewGuid()
            let edgeDTO = EdgeDTO(guid1, guid2, CausalEdgeType.Start)
            let json = edgeDTO.ToSMC().Stringify()
            writeClipboard(json)
            json.ZeroFillGuid() === jsonAnswer.ZeroFillGuid()

            let smec:Aas.SubmodelElementCollection = Aas.Jsonization.Deserialize.SubmodelElementCollectionFrom(JNode.Parse(json))
            let xml = smec.ToXml()
            let xxx = xml.ZeroFillGuidOnXml()
            xml.ZeroFillGuidOnXml() === xmlAnswer.ZeroFillGuidOnXml()

            let smec:Aas.SubmodelElementCollection = J.CreateIClassFromJson<Aas.SubmodelElementCollection>(json)
            let xml = smec.ToXml()
            xml.ZeroFillGuidOnXml() === xmlAnswer.ZeroFillGuidOnXml()
            ()


        [<Test>]
        member _.``Action: instance -> JObj -> Json ConversionTest`` () =
            //let action:VertexDetailObsolete = Action <| DsAction("action1")
            let action = DsAction("action1")
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
      "idShort": "Guid",
      "valueType": "xs:string",
      "value": "47adffca-451a-4a23-b1ac-90582564634b"
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
            json.ZeroFillGuid() === jsonAnswer.ZeroFillGuid()

            let xml = J.CreateIClassFromJson<Aas.SubmodelElementCollection>(jsonAnswer).ToXml()

            ()



        [<Test>]
        member _.``Work: instance -> JObj -> Json ConversionTest`` () =

            let system2 = DsSystem.FromJson(dsJson)
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
      "modelType": "Property",
      "idShort": "Guid",
      "valueType": "xs:string",
      "value": "9a7c646f-cc24-454b-991f-cca08a62ad83"
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
              "idShort": "Vertex",
              "value": [
                {
                  "modelType": "Property",
                  "idShort": "Guid",
                  "valueType": "xs:string",
                  "value": "6926aadb-bfd8-4a08-a37f-25eeaa521b4c"
                },
                {
                  "modelType": "Property",
                  "idShort": "ContentGuid",
                  "valueType": "xs:string",
                  "value": "73b49a57-a497-4a8a-8c9a-533dceeb7b5e"
                }
              ]
            },
            {
              "modelType": "SubmodelElementCollection",
              "idShort": "Vertex",
              "value": [
                {
                  "modelType": "Property",
                  "idShort": "Guid",
                  "valueType": "xs:string",
                  "value": "79f5bd27-b753-4d12-8c31-644992e14ec1"
                },
                {
                  "modelType": "Property",
                  "idShort": "ContentGuid",
                  "valueType": "xs:string",
                  "value": "6f6c0275-bd7b-4faa-8e8f-6636ca158625"
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
                  "value": "6926aadb-bfd8-4a08-a37f-25eeaa521b4c"
                },
                {
                  "modelType": "Property",
                  "idShort": "Target",
                  "valueType": "xs:string",
                  "value": "79f5bd27-b753-4d12-8c31-644992e14ec1"
                }
              ]
            }
          ]
        }
      ]
    },
    {
      "modelType": "SubmodelElementCollection",
      "idShort": "Works",
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
              "idShort": "Guid",
              "valueType": "xs:string",
              "value": "73b49a57-a497-4a8a-8c9a-533dceeb7b5e"
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
              "idShort": "Guid",
              "valueType": "xs:string",
              "value": "6f6c0275-bd7b-4faa-8e8f-6636ca158625"
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
    }
  ]
}"""
            let work = system2.Works[0]
            let json = work.ToSMC().Stringify()
            writeClipboard(json)
            json.ZeroFillGuid() === jsonAnswer.ZeroFillGuid()

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
      "modelType": "Property",
      "idShort": "Guid",
      "valueType": "xs:string",
      "value": "4f6ea2c2-af30-4c09-8837-5c634ce6082a"
    }
  ]
}"""
            let json = flow.ToSMC().Stringify()
            writeClipboard(json)
            json.ZeroFillGuid() === jsonAnswer.ZeroFillGuid()

            let xml = J.CreateIClassFromJson<Aas.SubmodelElementCollection>(json).ToXml()

            ()


        [<Test>]
        member _.``X System: instance -> JObj(SMC) -> Json ConversionTest`` () =

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
      "modelType": "Property",
      "idShort": "Guid",
      "valueType": "xs:string",
      "value": "d428dc1c-9806-4366-84c9-fddc1ab5b98b"
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
              "modelType": "Property",
              "idShort": "Guid",
              "valueType": "xs:string",
              "value": "70ca4de9-84e5-43e4-ba41-6a6574b74595"
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
                      "idShort": "Vertex",
                      "value": [
                        {
                          "modelType": "Property",
                          "idShort": "Guid",
                          "valueType": "xs:string",
                          "value": "fb1cd059-3d73-4f5f-acb4-dea10ad276d2"
                        },
                        {
                          "modelType": "Property",
                          "idShort": "ContentGuid",
                          "valueType": "xs:string",
                          "value": "9a7c646f-cc24-454b-991f-cca08a62ad83"
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
            },
            {
              "modelType": "SubmodelElementCollection",
              "idShort": "Works",
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
                      "modelType": "Property",
                      "idShort": "Guid",
                      "valueType": "xs:string",
                      "value": "9a7c646f-cc24-454b-991f-cca08a62ad83"
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
                              "idShort": "Vertex",
                              "value": [
                                {
                                  "modelType": "Property",
                                  "idShort": "Guid",
                                  "valueType": "xs:string",
                                  "value": "6926aadb-bfd8-4a08-a37f-25eeaa521b4c"
                                },
                                {
                                  "modelType": "Property",
                                  "idShort": "ContentGuid",
                                  "valueType": "xs:string",
                                  "value": "73b49a57-a497-4a8a-8c9a-533dceeb7b5e"
                                }
                              ]
                            },
                            {
                              "modelType": "SubmodelElementCollection",
                              "idShort": "Vertex",
                              "value": [
                                {
                                  "modelType": "Property",
                                  "idShort": "Guid",
                                  "valueType": "xs:string",
                                  "value": "79f5bd27-b753-4d12-8c31-644992e14ec1"
                                },
                                {
                                  "modelType": "Property",
                                  "idShort": "ContentGuid",
                                  "valueType": "xs:string",
                                  "value": "6f6c0275-bd7b-4faa-8e8f-6636ca158625"
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
                                  "value": "6926aadb-bfd8-4a08-a37f-25eeaa521b4c"
                                },
                                {
                                  "modelType": "Property",
                                  "idShort": "Target",
                                  "valueType": "xs:string",
                                  "value": "79f5bd27-b753-4d12-8c31-644992e14ec1"
                                }
                              ]
                            }
                          ]
                        }
                      ]
                    },
                    {
                      "modelType": "SubmodelElementCollection",
                      "idShort": "Works",
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
                              "idShort": "Guid",
                              "valueType": "xs:string",
                              "value": "73b49a57-a497-4a8a-8c9a-533dceeb7b5e"
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
                              "idShort": "Guid",
                              "valueType": "xs:string",
                              "value": "6f6c0275-bd7b-4faa-8e8f-6636ca158625"
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
}"""
            let json = system2.ToSMC().Stringify()
            writeClipboard(json)


            json.ZeroFillGuid() === jsonAnswer.ZeroFillGuid()

            let xml = J.CreateIClassFromJson<Aas.SubmodelElementCollection>(json).ToXml()

            ()



        [<Test>]
        member _.``System: instance -> JObj(SM) -> Json ConversionTest`` () =

            let system2 = DsSystem.FromJson(dsJson)
            let json = system2.ToSM().Stringify()
            writeClipboard(json)

            let xml = J.CreateIClassFromJson<Aas.Submodel>(json).ToXml()

            ()


    /// Json Test
    type T2() =
        [<Test>]
        member _.``System: instance -> Aas Test`` () =
            let system2 = DsSystem.FromJson(dsJson)
            let sm = system2.ToSM()
            ()
