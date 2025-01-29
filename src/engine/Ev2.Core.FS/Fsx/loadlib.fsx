open System.Collections.Generic


// "F:\Git\dual\soft\Delta\Dual.Common.FS\Scripts" 에서의 상대 경로
//                         < .. >          <.>


//#I @"Z:\ds\Submodules\nuget\bin\net8.0"
//#r "Dual.Common.Core.dll"
//#r "Dual.Common.Core.FS.dll"

#r @"F:\Git\ds\Submodules\nuget\bin\net8.0\Dual.Common.Core.dll"
#r @"F:\Git\ds\Submodules\nuget\bin\net8.0\Dual.Common.Core.FS.dll"

#r "nuget: Newtonsoft.Json"
#r "nuget: AasCore.Aas3_0"
#r "nuget: AasCore.Aas3.Package"

open System
open System.IO
open Dual.Common
open Dual.Common.Core.FS

open AasCore.Aas3_0
type AasXmlization = AasCore.Aas3_0.Xmlization

type MyDevice = {
    Id: string
    Name: string
    Description: string
}

let toAAS (device: MyDevice) : AssetAdministrationShell =
    let aas = AssetAdministrationShell(
        Id = device.Id,
        Description = LangString.Create("en", device.Description)
    )
    aas

let serializeAAS (aas: AssetAdministrationShell) : string =
    let xmlWriter = XmlWriter.CreateString()
    Xmlization.Serialize.To(xmlWriter, aas)
    xmlWriter.ToString()

let deserializeAAS (xml: string) : AssetAdministrationShell =
    let xmlReader = XmlReader.CreateString(xml)
    Xmlization.Deserialize.From(xmlReader) :?> AssetAdministrationShell

// Example usage
let device = { Id = "123"; Name = "Sensor"; Description = "Temperature Sensor" }
let aas = toAAS device
let xml = serializeAAS aas
printfn "Serialized XML:\n%s" xml

let deserializedAAS = deserializeAAS xml
printfn "Deserialized AAS ID: %s" deserializedAAS.Id
