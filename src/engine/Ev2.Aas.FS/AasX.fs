namespace rec Dual.Ev2.Aas

open System.IO
open System.IO.Compression
open System.Text
open System.Text.Json
open System.Xml
open System.Linq


open AasCore.Aas3_0
open Dual.Common.Core.FS

open Dapper
open Ev2.Core.FS
open Dual.Common.Base


[<AutoOpen>]
module AasXModule2 =

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

    type Project with   // UpdateDbAasXml, FromAasxFile, ExportToAasxFile, InjectToExistingAasxFile
        /// AASX 파일에서 aas submodel xml 파일을 읽어서 database 의 project table 의 aasXml column 을 update
        member project.UpdateDbAasXml(aasxPath: string, dbApi: AppDbApi): unit =
            // 1. AASX 파일에서 원본 XML 읽기
            let originalXml = getAasXmlFromAasxFile aasxPath

            // 2. 프로젝트 ID 확인
            let projectId = project.Id |? failwith "Project Id is not set"

            // 3. 데이터베이스에서 aasXml 컬럼만 업데이트
            dbApi.With(fun (conn, tr) ->
                let affectedRows = conn.Execute($"UPDATE {Tn.Project} SET aasXml = @AasXml WHERE id = @Id",
                    {| AasXml = originalXml; Id = projectId |}, tr)
                if affectedRows = 0 then
                    failwith $"Project with Id {projectId} not found for AasXml update"
            )

        static member FromAasxFile(aasxPath: string): Project =
            let njProj = NjProject.FromAasxFile(aasxPath)
            njProj.ToJson() |> Project.FromJson


        member x.ReadRuntimeDataFromDatabase(dbApi: AppDbApi): unit =
            let rtObjs = x.EnumerateRtObjects()
            let works = rtObjs.OfType<Work>().ToArray()
            let calls = rtObjs.OfType<Call>().ToArray()
            let workDict = works.Where(fun w -> w.Id.IsSome).ToDictionary((fun w -> w.Id.Value), id)
            let callDict = calls.Where(fun c -> c.Id.IsSome).ToDictionary((fun c -> c.Id.Value), id)
            dbApi.With(fun (conn, tr) ->
                // 1. works 에 대해 work 테이블에서 데이터 읽어서 Status4 update (Status4Id 에 해당하는 Work 객체의 Status4 속성 업데이트)
                let workIds = works |> Array.choose (_.Id) |> Array.map int
                if workIds.Length > 0 then
                    let ormWorks = conn.QueryRows<ORMWork>(Tn.Work, "id", workIds, tr)
                    for ormWork in ormWorks do
                        match workDict.TryGetValue(ormWork.Id.Value) with
                        | true, work -> work.Status4 <- ormWork.Status4Id >>= dbApi.TryFindEnumValue<DbStatus4>
                        | false, _ -> ()

                // 2. calls 에 대해 call 테이블에서 데이터 읽어서 Status4 update (Status4Id 에 해당하는 Call 객체의 Status4 속성 업데이트)
                let callIds = calls |> Array.choose (_.Id) |> Array.map int
                if callIds.Length > 0 then
                    let ormCalls = conn.QueryRows<ORMCall>(Tn.Call, "id", callIds, tr)
                    for ormCall in ormCalls do
                        match callDict.TryGetValue(ormCall.Id.Value) with
                        | true, call -> call.Status4 <- ormCall.Status4Id >>= dbApi.TryFindEnumValue<DbStatus4>
                        | false, _ -> ()
            )


        member x.ExportToAasxFile(outputPath: string, ?dbApi: AppDbApi): unit =
            dbApi |> iter x.ReadRuntimeDataFromDatabase
            let njProj = x.ToJson() |> NjProject.FromJson
            njProj.ExportToAasxFile(outputPath)

        /// 기존의 aasx 파일에서 Project submodel 만 교체해서 저장
        member x.InjectToExistingAasxFile(aasxPath: string) =
            let njProj = x.ToJson() |> NjProject.FromJson
            njProj.InjectToExistingAasxFile(aasxPath)
