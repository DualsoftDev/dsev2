namespace rec Dual.Ev2.Aas

open System.IO
open System.IO.Compression
open System.Text
open System.Text.Json
open System.Xml


open AasCore.Aas3_0
open Dual.Common.Core.FS

open System
open Ev2.Core.FS


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
                | "https://admin-shell.io/aas/3/0"    -> Some (Version(3,0))
                | _ -> None
            else
                None
        with
        | _ -> None

    /// AASX 파일에서 AAS XML 파일 경로를 찾는 함수
    let findAasXmlFilePath (archive: ZipArchive): string =
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

    /// AAS 버전을 검증하는 함수
    let validateAasVersion (versionOpt: System.Version option): unit =
        match versionOpt with
        | Some v when v >= Version(3,0) ->
            () // OK
        | Some v ->
            failwith ($"Unsupported AAS version: {v}. Only AAS version 3.0 or higher is supported.")
        | None ->
            failwith "Could not detect AAS version from XML."

    /// AASX 파일에서 Environment를 읽어오는 함수
    let readEnvironmentFromAasx (aasxPath: string): {| FilePath: string; Version: string; Environment: Aas.Environment |} =
        use fileStream = new FileStream(aasxPath, FileMode.Open)
        use archive = new ZipArchive(fileStream, ZipArchiveMode.Read)

        // 1. [Content_Types].xml 파일 확인
        let contentTypesEntry = archive.GetEntry("[Content_Types].xml")
        if contentTypesEntry = null then
            failwith "AASX file does not contain [Content_Types].xml"

        // 2. AAS XML 파일 경로 찾기
        let aasXmlFile = findAasXmlFilePath archive

        // 3. AAS XML 파일에서 Environment 추출 및 버전 확인
        let aasEntry = archive.GetEntry(aasXmlFile)
        if aasEntry = null then
            failwith $"AASX file does not contain {aasXmlFile}"

        let xml =
            use reader = new StreamReader(aasEntry.Open(), Encoding.UTF8)
            reader.ReadToEnd()

        // 4. AAS 버전 확인 및 검증
        let aasVersionOpt = detectAasVersionFromXml(xml)
        validateAasVersion aasVersionOpt

        let env = J.CreateIClassFromXml<Aas.Environment>(xml)
        {|
            FilePath = aasXmlFile;
            Version = (aasVersionOpt |> Option.map string |> Option.defaultValue "unknown")
            Environment = env
        |}

        /// Submodel을 업데이트하는 함수
    let updateSubmodels (existingEnv: Aas.Environment) (newProjectSubmodel: Aas.Submodel): ResizeArray<ISubmodel> =
        // IdShort 기반 비교 - SequenceControlSubmodel 상수 사용
        let targetIdShort = PreludeModule.SubmodelIdShort

        // 기존 submodel 중에서 같은 IdShort을 가진 것 찾기
        let existingSubmodelWithSameIdShort =
            existingEnv.Submodels
            |> tryFind (fun sm -> sm.IdShort = targetIdShort)

        // 디버깅: 기존 submodel 정보 출력
        tracefn "기존 Submodel 개수: %d" existingEnv.Submodels.Count
        existingEnv.Submodels |> Seq.iteri (fun i sm ->
            tracefn "  [%d] ID: %s, IdShort: %s" i sm.Id sm.IdShort)
        tracefn "새 Project Submodel ID: %s, IdShort: %s" newProjectSubmodel.Id newProjectSubmodel.IdShort
        tracefn "찾는 IdShort: %s" targetIdShort

        match existingSubmodelWithSameIdShort with
        | Some existingSubmodel ->
            // 기존 submodel이 있으면 교체
            tracefn "기존 submodel 발견 - 교체 수행: %s (ID: %s)" existingSubmodel.IdShort existingSubmodel.Id
            existingEnv.Submodels
            |-> (fun sm ->
                if sm.IdShort = targetIdShort then
                    tracefn "Submodel 교체: %s (ID: %s, IdShort: %s)" sm.IdShort sm.Id sm.IdShort
                    newProjectSubmodel :> ISubmodel
                else
                    sm
            ) |> ResizeArray
        | None ->
            // 기존 submodel이 없으면 새로 추가
            tracefn "기존 submodel 없음 - 새로 추가"
            let allSubmodels = existingEnv.Submodels @ [newProjectSubmodel :> ISubmodel]
            allSubmodels |> ResizeArray

    /// AssetAdministrationShell의 submodel 참조를 업데이트하는 함수
    let updateAssetAdministrationShells
        (existingShells: ResizeArray<IAssetAdministrationShell>)
        (newProjectSubmodel: Aas.Submodel)
        (existingEnv: Aas.Environment): ResizeArray<IAssetAdministrationShell>
      =
        existingShells
        |-> (fun aas ->
            let updatedSubmodelRefs =
                // IdShort 기반으로 기존 참조 확인
                let targetIdShort = PreludeModule.SubmodelIdShort

                // 기존 submodel 중에서 같은 IdShort을 가진 것의 ID 찾기
                let existingSubmodelWithSameIdShort =
                    existingEnv.Submodels
                    |> Seq.tryFind (fun sm -> sm.IdShort = targetIdShort)

                // 기존 참조 중에서 같은 IdShort을 가진 submodel을 참조하는 것 찾기
                let existingRefWithSameIdShort =
                    existingSubmodelWithSameIdShort
                    >>= (fun existingSubmodel ->
                        aas.Submodels
                        |> tryFind (fun ref ->
                            let submodelId =
                                ref.Keys
                                |> Seq.find (fun k -> k.Type = KeyTypes.Submodel)
                                |> fun k -> k.Value
                            submodelId = existingSubmodel.Id
                        ))

                match existingRefWithSameIdShort with
                | Some existingRef ->
                    // 기존 참조가 있으면 새 submodel로 교체
                    tracefn "기존 참조 발견 - 새 submodel로 교체: %s" targetIdShort
                    aas.Submodels
                    |-> (fun ref ->
                        if ref = existingRef then
                            let newKey = Key(KeyTypes.Submodel, newProjectSubmodel.Id) :> IKey
                            Aas.Reference(ReferenceTypes.ModelReference, ResizeArray<IKey>([newKey])) :> IReference
                        else
                            ref
                    ) |> ResizeArray
                | None ->
                    // 기존 참조가 없으면 새 참조 추가
                    tracefn "기존 참조 없음 - 새 참조 추가: %s" targetIdShort
                    let newRef =
                        let key = Key(KeyTypes.Submodel, newProjectSubmodel.Id) :> IKey
                        Aas.Reference(ReferenceTypes.ModelReference, ResizeArray<IKey>([key])) :> IReference

                    aas.Submodels @ [newRef] |> ResizeArray

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
        ) |> ResizeArray

    /// 업데이트된 AASX 파일을 생성하는 함수
    let createUpdatedAasxFile (aasxPath: string) (aasXmlFilePath: string) (updatedEnv: Aas.Environment): string =
        let tempPath = Path.GetTempFileName()
        do
            use tempFileStream = new FileStream(tempPath, FileMode.Create)
            use tempArchive = new ZipArchive(tempFileStream, ZipArchiveMode.Create)
            // 기존 AASX 파일의 모든 엔트리를 복사하되, AAS XML 파일만 업데이트
            use sourceFileStream = new FileStream(aasxPath, FileMode.Open)
            use sourceArchive = new ZipArchive(sourceFileStream, ZipArchiveMode.Read)
            for entry in sourceArchive.Entries do
                if entry.FullName = aasXmlFilePath then
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
        tempPath

    /// 파일을 안전하게 교체하는 함수
    let replaceFileWithBackup (originalPath: string) (newPath: string): unit =
        let backupPath = originalPath + ".backup"
        if File.Exists(backupPath) then
            File.Delete(backupPath)
        File.Move(originalPath, backupPath)
        File.Move(newPath, originalPath)

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
                Aas.Reference(ReferenceTypes.ModelReference, ResizeArray<IKey>([ key ])) :> IReference

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
            // 1. AASX 파일에서 기존 Environment 읽기
            let aasFileInfo = readEnvironmentFromAasx aasxPath
            let existingEnv = aasFileInfo.Environment

            // 디버깅: 기존 Environment의 submodel 정보 출력
            tracefn "=== 기존 Environment 분석 ==="
            tracefn "기존 Submodel 개수: %d" existingEnv.Submodels.Count
            existingEnv.Submodels |> Seq.iteri (fun i sm -> tracefn "  [%d] ID: %s, IdShort: %s" i sm.Id sm.IdShort)

            // 2. 현재 프로젝트의 Submodel 생성
            let newProjectSubmodel =
                let json = prj.ToSjSubmodel().Stringify()
                let sm = J.CreateIClassFromJson<Aas.Submodel>(json)
                // IdShort을 SequenceControlSubmodel로 설정
                Submodel(
                    id = sm.Id,
                    idShort = PreludeModule.SubmodelIdShort,
                    submodelElements = sm.SubmodelElements,
                    semanticId = sm.SemanticId,
                    supplementalSemanticIds = sm.SupplementalSemanticIds,
                    qualifiers = sm.Qualifiers,
                    embeddedDataSpecifications = sm.EmbeddedDataSpecifications,
                    kind = sm.Kind,
                    category = sm.Category,
                    description = sm.Description,
                    displayName = sm.DisplayName,
                    administration = sm.Administration
                )

            tracefn "=== 새 Project Submodel 정보 ==="
            tracefn "새 Submodel ID: %s" newProjectSubmodel.Id
            tracefn "새 Submodel IdShort: %s" newProjectSubmodel.IdShort

            // 3. Environment 업데이트
            let updatedSubmodels = updateSubmodels existingEnv newProjectSubmodel
            let updatedAssetAdministrationShells = updateAssetAdministrationShells existingEnv.AssetAdministrationShells newProjectSubmodel existingEnv

            tracefn "=== 업데이트 후 Submodel 정보 ==="
            tracefn "업데이트된 Submodel 개수: %d" updatedSubmodels.Count
            updatedSubmodels |> Seq.iteri (fun i sm -> tracefn "  [%d] ID: %s, IdShort: %s" i sm.Id sm.IdShort)

            let updatedEnv =
                Environment(
                    submodels = updatedSubmodels,
                    assetAdministrationShells = updatedAssetAdministrationShells,
                    conceptDescriptions = existingEnv.ConceptDescriptions
                )

            // 4. 업데이트된 AASX 파일 생성
            let tempPath = createUpdatedAasxFile aasxPath aasFileInfo.FilePath updatedEnv

            // 5. 파일 교체 (백업 포함)
            replaceFileWithBackup aasxPath tempPath

    type Project with   // ExportToAasxFile
        member x.ExportToAasxFile(outputPath: string): unit =
            let njProj = x.ToJson() |> NjProject.FromJson
            njProj.ExportToAasxFile(outputPath)

        /// 기존의 aasx 파일에서 Project submodel 만 교체해서 저장
        member x.InjectToExistingAasxFile(aasxPath: string) =
            let njProj = x.ToJson() |> NjProject.FromJson
            njProj.InjectToExistingAasxFile(aasxPath)
