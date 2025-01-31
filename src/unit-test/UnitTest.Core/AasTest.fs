namespace T.Core

open System
open Dual.Ev2
open Dual.Ev2.Aas
open Dual.Ev2.Aas.CoreToJsonViaCode
open NUnit.Framework
open System.Text.Json
open Dual.Common.Core.FS
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
            let xxx = container.AssetAdministrationShells[0]
            xxx

        let aasJson = """{
  "assetAdministrationShells": [
    {
      "assetInformation": {
        "assetKind": "NotApplicable",
        "globalAssetId": "something_eea66fa1"
      },
      "id": "something_142922d6",
      "modelType": "AssetAdministrationShell"
    }
  ]
}"""
        let aasXml = """<assetAdministrationShell xmlns="https://admin-shell.io/aas/3/0">
  <id>something_142922d6</id>
  <assetInformation>
    <assetKind>NotApplicable</assetKind>
    <globalAssetId>something_eea66fa1</globalAssetId>
  </assetInformation>
</assetAdministrationShell>"""

        [<Test>]
        member _.``AasBuildJson`` () =
            let assetInformation = JObj().Set(N.AssetKind, "NotApplicable").Set(N.GlobalAssetId, "something_eea66fa1")
            let assetAdministrationShell = JObj().Set(N.AssetInformation, assetInformation).Set(N.Id, "something_142922d6").Set(N.ModelType, "AssetAdministrationShell")
            let assetAdministrationShells = JObj().Set(N.AssetAdministrationShells, JArr [|assetAdministrationShell|])
            let json = assetAdministrationShells.Stringify()
            json === aasJson

        [<Test>]
        member _.``AasJsonRead`` () =

            let shell = loadAssetAdministrationShells(aasJson) :?> Aas.AssetAdministrationShell
            let xml = shell.ToXml()
            xml === aasXml


            let jsonObject = Aas.Jsonization.Serialize.ToJsonObject(shell);


            let system2 = DsSystem.FromJson(json)
            let json = system2.ToJsonViaCode().Stringify()
            ()

        [<Test>]
        member _.``CoreToJsonViaCodeTest`` () =
            let system2 = DsSystem.FromJson(json)
            let json = system2.ToJsonViaCode().Stringify()
            ()



        [<Test>]
        member _.``SimpleAasConversionTest`` () =
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
