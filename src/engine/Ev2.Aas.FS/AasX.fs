namespace rec Dual.Ev2.Aas

open System.IO
open System.IO.Compression
open System.Text
open System.Text.Json
open System.Xml

open AasCore.Aas3_0

open Ev2.Core.FS



open AasCore.Aas3_0
open System

[<AutoOpen>]
module AasXModule =
    /// XML에서 AAS 버전을 감지하는 헬퍼 함수 (System.Version 사용)
    let detectAasVersionFromXml(xmlContent: string): System.Version option =
        try
            let doc = XmlDocument()
            doc.LoadXml(xmlContent)
            let rootElement = doc.DocumentElement
            if rootElement <> null then
                match rootElement.NamespaceURI with
                | "http://www.admin-shell.io/aas/1/0" -> Some (Version(1,0))
                | "http://www.admin-shell.io/aas/2/0" -> Some (Version(2,0))
                | "https://admin-shell.io/aas/3/0" -> Some (Version(3,0))
                | _ -> None
            else
                None
        with
        | _ -> None

    type NjProject with
        member x.ToSjENV() : JObj =

            (* https://github.com/aas-core-works/aas-package3-csharp.git : AAS 비공식 인 듯..*)

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

        member x.ToAasJsonStringENV(): string =
            let jobj = x.ToSjENV()
            jobj.ToJsonString(JsonSerializerOptions(WriteIndented = true))

        member x.ToENV(): Aas.Environment =
            x.ToAasJsonStringENV() |> J.CreateIClassFromJson<Aas.Environment>


        member prj.ExportToAasxFile(outputPath: string): unit =
            let env: Aas.Environment = prj.ToENV()

            use fileStream = new FileStream(outputPath, FileMode.Create)
            use archive = new ZipArchive(fileStream, ZipArchiveMode.Create)

            // 1. [Content_Types].xml
            do
                let entry = archive.CreateEntry("[Content_Types].xml")
                use writer = new StreamWriter(entry.Open(), Encoding.UTF8)
                writer.Write """<?xml version="1.0" encoding="utf-8"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
    <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
    <Default Extension="xml" ContentType="text/xml" /><Override PartName="/aasx/aasx-origin" ContentType="text/plain" />
</Types>"""

            // 2. _rels/.rels
            do
                let entry = archive.CreateEntry("_rels/.rels")
                use writer = new StreamWriter(entry.Open(), Encoding.UTF8)
                let target = "/aasx/aasx-origin"
                let id = guid2str prj.Guid
                let id = "R320e13957d794f91"
                writer.Write $"""<?xml version="1.0" encoding="utf-8"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
    <Relationship Type="http://www.admin-shell.io/aasx/relationships/aasx-origin" Target="{target}" Id="{id}" />
</Relationships>"""

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
                writer.Write $"""<?xml version="1.0" encoding="utf-8"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
    <Relationship Type="http://www.admin-shell.io/aasx/relationships/aas-spec" Target="{target}" Id="{id}" />
</Relationships>"""

            // 5. aasx/aas/aas.aas.xml (실제 AAS XML 데이터)
            do
                let entry = archive.CreateEntry("aasx/aas/aas.aas.xml")
                use stream = entry.Open()
                let settings = XmlWriterSettings(Indent = true, Encoding = Encoding.UTF8)
                use writer = XmlWriter.Create(stream, settings)
                Xmlization.Serialize.To(env, writer)
                writer.Flush()


        /// 기존의 aasx 파일에서 Project submodel 만 교체해서 저장
        /// 개선된 버전: AASX 파일 구조 분석, AAS 버전 확인, Content_Types.xml 기반 파일 찾기
        member prj.InjectToExistingAasxFile(aasxPath: string): unit =
            // AASX 파일 구조 분석 및 AAS 버전 확인
            let aasFileInfo, existingEnv =
                use fileStream = new FileStream(aasxPath, FileMode.Open)
                use archive = new ZipArchive(fileStream, ZipArchiveMode.Read)

                // 1. [Content_Types].xml 파일 분석
                let contentTypesEntry = archive.GetEntry("[Content_Types].xml")
                if contentTypesEntry = null then
                    failwith "AASX file does not contain [Content_Types].xml"

                let contentTypesXml =
                    use reader = new StreamReader(contentTypesEntry.Open(), Encoding.UTF8)
                    reader.ReadToEnd()

                // 2. aasx-origin.rels에서 AAS XML 파일 찾기 (네임스페이스-aware)
                let aasXmlFile = 
                    let aasxOriginRelsEntry = archive.GetEntry("aasx/_rels/aasx-origin.rels")
                    if aasxOriginRelsEntry = null then
                        // 기본 경로 시도
                        "aasx/aas/aas.aas.xml"
                    else
                        let relsXml =
                            use reader = new StreamReader(aasxOriginRelsEntry.Open(), Encoding.UTF8)
                            reader.ReadToEnd()
                        let doc = XmlDocument()
                        doc.LoadXml(relsXml)
                        let nsmgr = new XmlNamespaceManager(doc.NameTable)
                        nsmgr.AddNamespace("rel", "http://schemas.openxmlformats.org/package/2006/relationships")
                        let relationships = doc.SelectNodes("//rel:Relationship[@Type='http://admin-shell.io/aasx/relationships/aas-spec']", nsmgr)
                        if relationships.Count > 0 then
                            let relationship = relationships.Item(0) :?> XmlElement
                            let target = relationship.GetAttribute("Target")
                            target.TrimStart('/')
                        else
                            // 기본 경로 시도
                            "aasx/aas/aas.aas.xml"

                // 3. AAS XML 파일에서 Environment 추출 및 버전 확인
                let aasEntry = archive.GetEntry(aasXmlFile)
                if aasEntry = null then
                    failwith $"AASX file does not contain {aasXmlFile}"

                let xml =
                    use reader = new StreamReader(aasEntry.Open(), Encoding.UTF8)
                    reader.ReadToEnd()

                // 4. AAS 버전 확인
                let aasVersionOpt = detectAasVersionFromXml(xml)

                match aasVersionOpt with
                | Some v when v = Version(3,0) ->
                    () // OK
                | Some v when v = Version(2,0) ->
                    failwith "AAS version 2.0 is not supported. Only AAS version 3.0 is supported for injection."
                | Some v ->
                    failwith ($"Unsupported AAS version: {v}. Only AAS version 3.0 is supported.")
                | None ->
                    failwith "Could not detect AAS version from XML."

                let env = J.CreateIClassFromXml<Aas.Environment>(xml)
                ({| FilePath = aasXmlFile; Version = (aasVersionOpt |> Option.map string |> Option.defaultValue "unknown") |}, env)

            // 현재 프로젝트의 Submodel 생성
            let newProjectSubmodel =
                let json = prj.ToSjSubmodel().Stringify()
                J.CreateIClassFromJson<Aas.Submodel>(json)

            // 기존 Environment에서 Project submodel 교체 또는 추가
            let updatedSubmodels =
                let existingSubmodelIds =
                    existingEnv.Submodels
                    |> Seq.map (fun sm -> sm.Id)
                    |> Set.ofSeq

                let replacedSubmodels =
                    existingEnv.Submodels
                    |> Seq.map (fun sm ->
                        if sm.Id = newProjectSubmodel.Id then
                            newProjectSubmodel :> ISubmodel
                        else
                            sm :> ISubmodel
                    )

                if existingSubmodelIds.Contains(newProjectSubmodel.Id) then
                    // 기존 submodel이 있으면 교체
                    replacedSubmodels |> ResizeArray<ISubmodel>
                else
                    // 기존 submodel이 없으면 새로 추가
                    let allSubmodels =
                        replacedSubmodels
                        |> Seq.append [newProjectSubmodel :> ISubmodel]
                    allSubmodels |> ResizeArray<ISubmodel>

            // AssetAdministrationShell의 submodel 참조도 업데이트
            let updatedAssetAdministrationShells =
                existingEnv.AssetAdministrationShells
                |> Seq.map (fun aas ->
                    let updatedSubmodelRefs =
                        let existingRefIds =
                            aas.Submodels
                            |> Seq.map (fun ref ->
                                ref.Keys
                                |> Seq.find (fun k -> k.Type = KeyTypes.Submodel)
                                |> fun k -> k.Value
                            )
                            |> Set.ofSeq

                        if existingRefIds.Contains(newProjectSubmodel.Id) then
                            // 기존 참조가 있으면 그대로 유지 (교체된 submodel 참조)
                            aas.Submodels
                        else
                            // 기존 참조가 없으면 새 참조 추가
                            let newRef =
                                let key = Key(KeyTypes.Submodel, newProjectSubmodel.Id) :> IKey
                                Reference(ReferenceTypes.ModelReference, ResizeArray<IKey>([key])) :> IReference

                            let allRefs =
                                aas.Submodels
                                |> Seq.append [newRef]
                            ResizeArray<IReference>(allRefs)

                    AssetAdministrationShell(
                        id = aas.Id,
                        assetInformation = aas.AssetInformation,
                        idShort = aas.IdShort,
                        submodels = updatedSubmodelRefs,
                        extensions = aas.Extensions,
                        category = aas.Category,
                        description = aas.Description,
                        displayName = aas.DisplayName,
                        administration = aas.Administration,
                        embeddedDataSpecifications = aas.EmbeddedDataSpecifications,
                        derivedFrom = aas.DerivedFrom
                    ) :> IAssetAdministrationShell
                )
                |> ResizeArray<IAssetAdministrationShell>

            let updatedEnv =
                Environment(
                    submodels = updatedSubmodels,
                    assetAdministrationShells = updatedAssetAdministrationShells,
                    conceptDescriptions = existingEnv.ConceptDescriptions
                )

            // 임시 파일에 저장
            let tempPath = Path.GetTempFileName()
            do
                use tempFileStream = new FileStream(tempPath, FileMode.Create)
                use tempArchive = new ZipArchive(tempFileStream, ZipArchiveMode.Create)
                // 기존 AASX 파일의 모든 엔트리를 복사하되, AAS XML 파일만 업데이트
                use sourceFileStream = new FileStream(aasxPath, FileMode.Open)
                use sourceArchive = new ZipArchive(sourceFileStream, ZipArchiveMode.Read)
                for entry in sourceArchive.Entries do
                    if entry.FullName = aasFileInfo.FilePath then
                        // AAS XML 파일만 새로 생성
                        let newEntry = tempArchive.CreateEntry(entry.FullName)
                        use stream = newEntry.Open()
                        let settings = XmlWriterSettings(Indent = true, Encoding = Encoding.UTF8)
                        use writer = XmlWriter.Create(stream, settings)
                        Xmlization.Serialize.To(updatedEnv, writer)
                        writer.Flush()
                    else
                        // 다른 파일들은 그대로 복사
                        let newEntry = tempArchive.CreateEntry(entry.FullName)
                        use sourceStream = entry.Open()
                        use targetStream = newEntry.Open()
                        sourceStream.CopyTo(targetStream)

            // use 블록이 끝난 후 파일 이동
            let backupPath = aasxPath + ".backup"
            if File.Exists(backupPath) then
                File.Delete(backupPath)
            File.Move(aasxPath, backupPath)
            File.Move(tempPath, aasxPath)

