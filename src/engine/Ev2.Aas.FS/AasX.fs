namespace rec Dual.Ev2.Aas

open System.Linq
open System

open AasCore.Aas3_0

open Dual.Common.Core.FS
open Ev2.Core.FS
open System.Globalization
open System.IO
open System.IO.Compression
open System.Text
open AasCore.Aas3_0
open System.Text.Json
//open System.IO.Packaging
open System.IO
open System.IO.Compression
open System.Text
open System.Xml
open System.IO
open System.IO.Compression
open System.Text
open System.Xml
open System.IO
open System.IO.Compression
open System.Xml.Linq
open AasCore.Aas3_0



open AasCore.Aas3_0




[<AutoOpen>]
module AasXModule =
    type NjProject with
        member x.ToSjENV() : JObj =
            let json = x.ToSjSubmodel().Stringify()
            // 변환을 위한 Submodel/Shell/ConceptDescription 생성
            let sm : Aas.Submodel = J.CreateIClassFromJson<Aas.Submodel>(json)

            let assetInfo =
                AssetInformation(
                    assetKind = AssetKind.Instance,
                    globalAssetId = "urn:dualsoft:asset:nj"
                )


            let smRef =
                let key = Key(KeyTypes.Submodel, sm.Id) :> IKey
                Reference(ReferenceTypes.ModelReference, ResizeArray<IKey>([ key ])) :> IReference

            let shell =
                AssetAdministrationShell(
                    id = "urn:dualsoft:njproject",
                    assetInformation = assetInfo,
                    idShort = "ProjectShell",
                    submodels = ResizeArray<IReference>([ smRef ])
                )

            let env =
                let submodels = sm    :> ISubmodel                 |> Array.singleton |> ResizeArray
                let aasShells = shell :> IAssetAdministrationShell |> Array.singleton |> ResizeArray
                Environment(submodels = submodels, assetAdministrationShells = aasShells)

            Jsonization.Serialize.ToJsonObject(env)

        member x.ToAasJsonENV(): string =
            let jobj = x.ToSjENV()
            jobj.ToJsonString(JsonSerializerOptions(WriteIndented = true))

        member x.ToENV(): Environment =
            x.ToAasJsonENV() |> J.CreateIClassFromJson<Aas.Environment>


        member x.ExportToAasxFile(outputPath: string) =
            let env: Environment = x.ToENV()

            use fileStream = new FileStream(outputPath, FileMode.Create)
            use archive = new ZipArchive(fileStream, ZipArchiveMode.Create)

            // 1. [Content_Types].xml
            do
                let entry = archive.CreateEntry("[Content_Types].xml")
                use writer = new StreamWriter(entry.Open(), Encoding.UTF8)
                writer.Write """<?xml version="1.0" encoding="utf-8"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" /><Default Extension="xml" ContentType="text/xml" /><Override PartName="/aasx/aasx-origin" ContentType="text/plain" /></Types>"""

            // 2. _rels/.rels
            do
                let entry = archive.CreateEntry("_rels/.rels")
                use writer = new StreamWriter(entry.Open(), Encoding.UTF8)
                let target = "/aasx/aasx-origin"
                let id = "R320e13957d794f91"
                writer.Write $"""<?xml version="1.0" encoding="utf-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Type="http://www.admin-shell.io/aasx/relationships/aasx-origin" Target="{target}" Id="{id}" /></Relationships>"""

            // 3. aasx/aasx-origin (빈 내용이지만 반드시 있어야 함)
            do
                let entry = archive.CreateEntry("aasx/aasx-origin")
                use writer = new StreamWriter(entry.Open(), Encoding.UTF8)
                writer.Write("Intentionally empty.")

            // 4. aasx/_rels/aasx-origin.rels
            do
                let entry = archive.CreateEntry("aasx/_rels/aasx-origin.rels")
                use writer = new StreamWriter(entry.Open(), Encoding.UTF8)
                let target = "/aasx/aas/aas.aas.xml"
                let id = "R40528201d6544e91"
                writer.Write $"""<?xml version="1.0" encoding="utf-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Type="http://www.admin-shell.io/aasx/relationships/aas-spec" Target="{target}" Id="{id}" /></Relationships>"""

            // 5. aasx/aas/aas.aas.xml (실제 AAS XML 데이터)
            do
                let entry = archive.CreateEntry("aasx/aas/aas.aas.xml")
                use stream = entry.Open()
                let settings = XmlWriterSettings(Indent = true, Encoding = Encoding.UTF8)
                use writer = XmlWriter.Create(stream, settings)
                AasCore.Aas3_0.Xmlization.Serialize.To(env, writer)
                writer.Flush()