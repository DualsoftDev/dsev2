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

module Aas =
    open AasCore.Aas3_0
    type Jsonization = AasCore.Aas3_0.Jsonization
    type Environment = AasCore.Aas3_0.Environment
    type AssetAdministrationShell = AasCore.Aas3_0.AssetAdministrationShell
    type Xmlization = AasCore.Aas3_0.Xmlization
    type IClass = AasCore.Aas3_0.IClass



module Aas1 =
    let [<Literal>] aas = "http://www.admin-shell.io/aas/3/0"
    let json = Json.json

    /// Json Test
    type T() =
        let loadAssetAdministrationShells(aasJson:string) =
            let aasNode:JNode = JNode.Parse(aasJson)
            let container:Aas.Environment = Aas.Jsonization.Deserialize.EnvironmentFrom(aasNode)
            let xxx = container.AssetAdministrationShells[0]
            xxx

        let toXml(iclass:Aas.IClass) =
            let outputBuilder = System.Text.StringBuilder()
            let settings = System.Xml.XmlWriterSettings(Encoding = System.Text.Encoding.UTF8, OmitXmlDeclaration = true, Indent = true)
            use writer = System.Xml.XmlWriter.Create(outputBuilder, settings)
            Aas.Xmlization.Serialize.To(iclass, writer)
            writer.Flush()
            outputBuilder.ToString()

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
            let assetInformation = JObj().Set("assetKind", "NotApplicable").Set("globalAssetId", "something_eea66fa1")
            let assetAdministrationShell = JObj().Set("assetInformation", assetInformation).Set("id", "something_142922d6").Set("modelType", "AssetAdministrationShell")
            let assetAdministrationShells = JObj().Set("assetAdministrationShells", JArr [|assetAdministrationShell|])
            let json = assetAdministrationShells.Stringify()
            json === aasJson

        [<Test>]
        member _.``AasJsonRead`` () =

            let shell = loadAssetAdministrationShells(aasJson) :?> Aas.AssetAdministrationShell
            let xml = toXml(shell :> Aas.IClass)
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
            let edgeDTO = EdgeDTO("source", "target", CausalEdgeType.Start)
            let json = edgeDTO.ToSMEC().Stringify()
            DcClipboard.Write(json)

            let xxx = loadAssetAdministrationShells(json)
            ()
