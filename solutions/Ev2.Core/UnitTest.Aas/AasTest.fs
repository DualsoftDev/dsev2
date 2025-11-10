namespace T.Core


open System.Linq
open NUnit.Framework
open Newtonsoft.Json

open Dual.Common.UnitTest.FS
open Dual.Common.Base
open Dual.Common.Base
open Dual.Common.Core.FS

open Dual.Ev2
open System.IO

module Aas =
    /// submodel 없는 env 만 존재하는 최외곽 aas json
    let aasJson0 = """{
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

    /// submodel 없는 env 만 존재하는 최외곽 aas xml
    let aasXml0 = """<environment xmlns="https://admin-shell.io/aas/3/0">
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



