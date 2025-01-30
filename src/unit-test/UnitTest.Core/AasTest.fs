namespace T.Core

open System
open Dual.Ev2
open Dual.Ev2.Aas.CoreToJsonViaCode
open NUnit.Framework
open System.Text.Json
open Dual.Common.Core.FS
open Dual.Common.UnitTest.FS



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
            let aasNode = JNode.Parse(aasJson)
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

        [<Test>]
        member _.``CoreToJsonViaCodeTest`` () =
            let system2 = DsSystem.FromJson(json)
            let json =
                let jnode = system2.ToJsonViaCode()
                let settings = JsonSerializerOptions() |> tee(fun s -> s.WriteIndented <- true)
                jnode.ToJsonString(settings)
            ()


        [<Test>]
        member _.``AasJsonRead`` () =
            let aasJson = """
{
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
}
"""
            let aasXml = """<assetAdministrationShell xmlns="https://admin-shell.io/aas/3/0">
  <id>something_142922d6</id>
  <assetInformation>
    <assetKind>NotApplicable</assetKind>
    <globalAssetId>something_eea66fa1</globalAssetId>
  </assetInformation>
</assetAdministrationShell>"""




            let shell = loadAssetAdministrationShells(aasJson) :?> Aas.AssetAdministrationShell
            let xml = toXml(shell :> Aas.IClass)
            xml === aasXml


            let jsonObject = Aas.Jsonization.Serialize.ToJsonObject(shell);


            let system2 = DsSystem.FromJson(json)
            let json =
                let jnode = system2.ToJsonViaCode()
                let settings = JsonSerializerOptions() |> tee(fun s -> s.WriteIndented <- true)
                jnode.ToJsonString(settings)
            ()
