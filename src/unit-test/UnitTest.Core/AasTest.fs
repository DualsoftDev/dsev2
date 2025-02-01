namespace T.Core


open System.Linq
open NUnit.Framework
open Newtonsoft.Json

open Dual.Common.UnitTest.FS
open Dual.Common.Base.CS
open Dual.Common.Base.FS
open Dual.Common.Core.FS

open Dual.Ev2

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


    let fileAasJsonWithEmptySubmodels = @"Z:\dsev2\submodules\aas-core3.0-csharp\test_data\Json\ContainedInEnvironment\Expected\AssetAdministrationShell\maximal.json"
    let fileAasJsonSubmodels = @"Z:\dsev2\submodules\aas-core3.0-csharp\test_data\Json\ContainedInEnvironment\Expected\Reference\for_a_model_reference_first_key_in_globally_and_aas_identifiables.json"
    let fileAasJsonSubmodelWithAnnotatedRelationshipElement = @"Z:\dsev2\submodules\aas-core3.0-csharp\test_data\Json\ContainedInEnvironment\Expected\Submodel\maximal.json"
    let fileAasJsonSubmodelWithSMC = @"Z:\dsev2\submodules\aas-core3.0-csharp\test_data\Json\ContainedInEnvironment\Expected\SubmodelElementCollection\maximal.json"
    let fileAasJsonSubmodelWithSML = @"Z:\dsev2\submodules\aas-core3.0-csharp\test_data\Json\ContainedInEnvironment\Expected\SubmodelElementList\maximal.json"

