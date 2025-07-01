namespace T.Core

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
    [<SetUpFixture>]
    type GlobalTestSetup() =
        [<OneTimeSetUp>]
        member _.GlobalSetup() =
            Ev2.Core.FS.ModuleInitializer.Initialize(null)
            DcLogger.EnableTrace <- true
            AppSettings.TheAppSettings <- AppSettings(UseUtcTime = false)

            Directory.CreateDirectory(testDataDir()) |> ignore
            createEditableProject()


        [<OneTimeTearDown>]
        member _.Cleanup() =
            //File.Delete(dbFilePath)
            ()


    type T() =

        [<Test>]
        member _.``AasCore: submodel min test`` () =
            // fuzzed_07.json
            let json = """{
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

