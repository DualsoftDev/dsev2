namespace T.Core

open AasCore.Aas3_0
open Dual.Ev2
open Dual.Ev2.Aas
open Dual.Ev2.Aas.CoreToJsonViaCode
open NUnit.Framework
open Dual.Common.UnitTest.FS
open Dual.Common.Base

open Dual.Common.Base
open Dual.Common.UnitTest.FS
open Dual.Common.Db.FS
open Dual.Common.Core.FS

open Ev2.Core.FS
open Newtonsoft.Json
open System.Reactive.Concurrency
open System.IO
open T



module AasCore3_0Test =
    type T() =

        [<Test>]
        member _.``AasCore: submodel min test`` () =
            // fuzzed_07.json
            let json = """
    {
      "id": "something_48c66017",
      "modelType": "Submodel"
    }"""
            let xmlAnswer = """<submodel xmlns="https://admin-shell.io/aas/3/0">
  <id>something_48c66017</id>
</submodel>"""

            let xml = J.CreateIClassFromJson<Aas.Submodel>(json).ToXml()
            xml === xmlAnswer
            ()

        [<Test>]
        member _.``AasCore: submodels min test`` () =
            // fuzzed_07.json
            let json = """
    {
        "id": "something_48c66017"
        , "modelType": "Submodel"
        , "submodelElements": [
            {
                "idShort": "something3fdd3eb4"
                , "modelType": "SubmodelElementCollection"
                , "value": [
                    {
                        "idShort": "System"
                        , "modelType": "Property"
                        , "value": "Hello, World"
                        , "valueType": "xs:string"
                    }
                    , {
                        "modelType": "SubmodelElementCollection"
                        , "value": [
                        ]
                    }
                    , {
                        "modelType": "SubmodelElementCollection"
                        , "value": [
                            {
                                "idShort": "Works"
                                , "modelType": "Property"
                                , "value": "Hello, World"
                                , "valueType": "xs:string"
                            }
                            , {
                                "idShort": "Work"
                                , "modelType": "SubmodelElementCollection"

                                , "value": [
                                    {
                                        "idShort": "Calls"
                                        , "modelType": "SubmodelElementCollection"
                                        , "value": [
                                            {
                                                "idShort": "Call"
                                                , "modelType": "SubmodelElementCollection"
                                                , "value": [
                                                    {
                                                        "idShort": "Name"
                                                        , "modelType": "Property"
                                                        , "value": "Call1"
                                                        , "valueType": "xs:string"
                                                    }
                                                    , {
                                                        "idShort": "IsDisabled"
                                                        , "modelType": "Property"
                                                        , "value": "false"
                                                        , "valueType": "xs:boolean"
                                                    }
                                                    , {
                                                        "idShort": "AutoPre"
                                                        , "modelType": "Property"
                                                        , "value": "AutoPre"
                                                        , "valueType": "xs:string"
                                                    }
                                                ]


                                            }
                                        ]
                                    }
                                    , {
                                        "idShort": "Arrows"
                                        , "modelType": "SubmodelElementCollection"
                                        , "value": []
                                    }
                                ]




                            }
                            , {
                                "modelType": "SubmodelElementCollection"
                                , "value": []
                            }
                        ]
                    }

                ]
            }
        ]
    }
"""
            let xml = J.CreateIClassFromJson<Submodel>(json).ToXml()
            ()


        [<Test>]
        member _.``AasCore: 상상 submodels min test`` () =
            let json = """
{
        "id": "something_48c66017"
        , "modelType": "Submodel"
        , "submodelElements": [
            {
                "idShort": "something3fdd3eb4"
                , "modelType": "SubmodelElementCollection"

                , "value": [
                    {
                      "idShort": "system_1",
                      "modelType": "SubmodelElementCollection",
                      "value": [
                        {
                          "idShort": "Name",
                          "modelType": "Property",
                          "value": "MainSystem",
                          "valueType": "xs:string"
                        },
                        {
                          "idShort": "Description",
                          "modelType": "Property",
                          "value": "Main production line system",
                          "valueType": "xs:string"
                        },
                        {
                          "idShort": "Works",
                          "modelType": "SubmodelElementCollection",
                          "value": [
                            {
                              "idShort": "work_1",
                              "modelType": "SubmodelElementCollection",
                              "value": [
                                {
                                  "idShort": "Name",
                                  "modelType": "Property",
                                  "value": "Assemble",
                                  "valueType": "xs:string"
                                },
                                {
                                  "idShort": "Type",
                                  "modelType": "Property",
                                  "value": "Manual",
                                  "valueType": "xs:string"
                                },
                                {
                                  "idShort": "Calls",
                                  "modelType": "SubmodelElementCollection",
                                  "value": [
                                    {
                                      "idShort": "call_1",
                                      "modelType": "SubmodelElementCollection",
                                      "value": [
                                        {
                                          "idShort": "Name",
                                          "modelType": "Property",
                                          "value": "StartConveyor",
                                          "valueType": "xs:string"
                                        },
                                        {
                                          "idShort": "IsDisabled",
                                          "modelType": "Property",
                                          "value": "false",
                                          "valueType": "xs:boolean"
                                        },
                                        {
                                          "idShort": "AutoPre",
                                          "modelType": "Property",
                                          "value": "true",
                                          "valueType": "xs:boolean"
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
}
"""
            let xml = J.CreateIClassFromJson<Submodel>(json).ToXml()
            ()

        [<Test>]
        member _.``AasCore: submodel with property element test`` () =
            // double.json
            let json = """{
      "id": "something_48c66017",
      "modelType": "Submodel",
      "submodelElements": [
        {
          "idShort": "something3fdd3eb4",
          "modelType": "Property",
          "value": "1234.01234",
          "valueType": "xs:double"
        }
      ]
    }"""
            let xmlAnswer = """<submodel xmlns="https://admin-shell.io/aas/3/0">
  <id>something_48c66017</id>
  <submodelElements>
    <property>
      <idShort>something3fdd3eb4</idShort>
      <valueType>xs:double</valueType>
      <value>1234.01234</value>
    </property>
  </submodelElements>
</submodel>"""

            let xml = J.CreateIClassFromJson<Aas.Submodel>(json).ToXml()
            xml === xmlAnswer
            ()


        [<Test>]
        member _.``AasCore: submodel with value element test`` () =
            // double.json
            let json = """{
      "id": "S",
      "modelType": "Submodel",
      "submodelElements": [
        {
          "idShort": "S/SE",
          "modelType": "SubmodelElementCollection",
          "value": [
            {
              "idShort": "S/SE/V0",
              "modelType": "Property",
              "value": "1234.01234",
              "valueType": "xs:double"
            },
            {
              "idShort": "S/SE/V1",
              "modelType": "SubmodelElementCollection",
              "value": [
                {
                  "idShort": "S/SE/V1/V0",
                  "modelType": "Property",
                  "value": "false",
                  "valueType": "xs:boolean"
                }
              ]
            }
          ]
        }
      ]
    }"""
            let xmlAnswer = """<submodel xmlns="https://admin-shell.io/aas/3/0">
  <id>S</id>
  <submodelElements>
    <submodelElementCollection>
      <idShort>S/SE</idShort>
      <value>
        <property>
          <idShort>S/SE/V0</idShort>
          <valueType>xs:double</valueType>
          <value>1234.01234</value>
        </property>
        <submodelElementCollection>
          <idShort>S/SE/V1</idShort>
          <value>
            <property>
              <idShort>S/SE/V1/V0</idShort>
              <valueType>xs:boolean</valueType>
              <value>false</value>
            </property>
          </value>
        </submodelElementCollection>
      </value>
    </submodelElementCollection>
  </submodelElements>
</submodel>"""
            let xml = J.CreateIClassFromJson<Aas.Submodel>(json).ToXml()
            xml === xmlAnswer
            ()




        [<Test>]
        member _.``AasCore: submodel test`` () =
            // fuzzed_07.json
            let json = """
    {
      "id": "something_48c66017",
      "modelType": "Submodel",
      "submodelElements": [
        {
          "idShort": "something3fdd3eb4",
          "max": "60",
          "min": "60",
          "modelType": "Range",
          "valueType": "xs:short"
        }
      ]
    }
"""
            //let xml = J.CreateIClassFromJson<Aas.SubmodelElementList>(false_in_letters).ToXml()
            let xml = J.CreateIClassFromJson<Aas.Submodel>(json).ToXml()
            ()

