namespace Dual.Ev2.Aas

(* AAS Json/Xml 로부터 Core 를 생성하기 위한 코드 *)

open System.Linq
open System

open AasCore.Aas3_0

open Dual.Common.Core.FS
open Dual.Common.Base.FS
open Ev2.Core.FS
open System.Globalization
open System.Runtime.CompilerServices

open System.Text.Json
open System.Text.Json.Nodes

[<AutoOpen>]
module rec AasExtensions =
    /// SemanticId 키 매칭 유틸
    type IHasSemantics with
        member internal semantic.hasSemanticKey (semanticKey: string) =
            semantic.SemanticId <> null &&
            semantic.SemanticId.Keys
            |> Seq.exists (fun k -> k.Value = AasSemantics.map[semanticKey])

    type UniqueInfo = { Name: string; Guid: Guid; Parameter: string; Id: Id option }

    type SMEsExtension =
        [<Extension>]
        static member TryGetPropValueBySemanticKey(smc:ISubmodelElement seq, semanticKey:string): string option =
            smc.OfType<Property>()
            |> tryPick (function
                | p when p.hasSemanticKey semanticKey -> Some p.Value
                | _ -> None)

        [<Extension>]
        static member TryGetPropValueByCategory (smc:ISubmodelElement seq, category:string): string option =
            smc.OfType<Property>()
            |> tryPick (function
                | p when p.Category = category -> Some p.Value
                | _ -> None)

        [<Extension>]
        static member CollectChildrenSMEWithSemanticKey(smc:ISubmodelElement seq, semanticKey: string): ISubmodelElement [] =
            smc
            |> filter (fun sme -> sme.hasSemanticKey semanticKey)
            |> toArray

        [<Extension>]
        static member CollectChildrenSMCWithSemanticKey(smc:ISubmodelElement seq, semanticKey: string): SubmodelElementCollection [] =
            smc.CollectChildrenSMEWithSemanticKey semanticKey |> Seq.cast<SubmodelElementCollection> |> toArray

        [<Extension>]
        static member TryGetPropValue (smc:ISubmodelElement seq, propName:string) = smc.TryGetPropValueBySemanticKey propName

        [<Extension>]
        static member TryGetPropValue<'T> (smc:ISubmodelElement seq, propName: string): 'T option =
            smc.TryGetPropValue propName
            >>= (fun str ->
                try
                    let value =
                        match typeof<'T> with
                        | _ when typeof<'T> = typeof<string> ->
                            box str
                        | _ when typeof<'T> = typeof<Guid> ->
                            str |> Guid.Parse |> box
                        | _ when typeof<'T> = typeof<int> ->
                            str |> Int32.Parse |> box
                        | _ when typeof<'T> = typeof<float> ->
                            str |> Double.Parse |> box
                        | _ when typeof<'T> = typeof<bool> ->
                            str |> Boolean.Parse |> box
                        | _ ->
                            // 일반적인 Convert.ChangeType 사용
                            Convert.ChangeType(str, typeof<'T>, CultureInfo.InvariantCulture)
                    Some (value :?> 'T)
                with _ -> None)

        [<Extension>]
        static member GetPropValue(smc:ISubmodelElement seq, propName) =
            smc.TryGetPropValue propName |> Option.get

        [<Extension>]
        static member TryFindChildSME(smc:ISubmodelElement seq, semanticKey: string): ISubmodelElement option =
            smc.CollectChildrenSMEWithSemanticKey semanticKey |> tryHead

        [<Extension>]
        static member TryFindChildSMC(smc:ISubmodelElement seq, semanticKey: string): SubmodelElementCollection option =
            (smc.TryFindChildSME semanticKey).Cast<SubmodelElementCollection>()

        [<Extension>]
        static member ReadUniqueInfo(smc:ISubmodelElement seq) =
            let name      = smc.TryGetPropValue "Name"      |? null
            let guid      = smc.GetPropValue    "Guid"      |> Guid.Parse
            let parameter = smc.TryGetPropValue "Parameter" |? null
            let id        = smc.TryGetPropValue "Id"        |-> Id.Parse
            { Name=name; Guid=guid; Parameter=parameter; Id=id }


    let private nonnullize(values:ResizeArray<ISubmodelElement>) = if values = null then ResizeArray<ISubmodelElement>() else values
    type SubmodelElementCollection with
        member smc.ReadUniqueInfo() = nonnullize(smc.Value).ReadUniqueInfo()
        member smc.ValuesOfType<'T when 'T :> ISubmodelElement>() = nonnullize(smc.Value).OfType<'T>()
        member smc.TryGetPropValueBySemanticKey (semanticKey:string): string option = nonnullize(smc.Value).TryGetPropValueBySemanticKey semanticKey
        member smc.TryGetPropValueByCategory (category:string): string option = nonnullize(smc.Value).TryGetPropValueByCategory category
        member smc.CollectChildrenSMEWithSemanticKey(semanticKey: string): ISubmodelElement [] = nonnullize(smc.Value).CollectChildrenSMEWithSemanticKey semanticKey
        member smc.CollectChildrenSMCWithSemanticKey(semanticKey: string): SubmodelElementCollection [] = nonnullize(smc.Value).CollectChildrenSMEWithSemanticKey semanticKey |> Seq.cast<SubmodelElementCollection> |> toArray
        member smc.TryGetPropValue (propName:string) = smc.TryGetPropValueBySemanticKey propName
        member smc.TryGetPropValue<'T> (propName: string): 'T option = nonnullize(smc.Value).TryGetPropValue<'T> propName
        member smc.GetPropValue (propName:string):string = nonnullize(smc.Value).GetPropValue propName
        member smc.TryFindChildSME(semanticKey: string): ISubmodelElement option = nonnullize(smc.Value).TryFindChildSME semanticKey
        member smc.TryFindChildSMC(semanticKey: string): SubmodelElementCollection option = nonnullize(smc.Value).TryFindChildSMC semanticKey

        member smc.GetSMC(semanticKey: string): SubmodelElementCollection [] =
                smc.CollectChildrenSMCWithSemanticKey semanticKey

    type ISubmodel with
        member sm.GetSMCWithSemanticKey(semanticKey:string): SubmodelElementCollection [] =
            sm.SubmodelElements
                .CollectChildrenSMCWithSemanticKey semanticKey

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
    let readEnvironmentFromAasx (aasxPath: string): {| FilePath: string; Version: string; Environment: Aas.Environment; OriginalXml: string |} =
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
            Environment = env;
            OriginalXml = xml
        |}

    /// AASX 파일에서 submodel xml 파일 읽어서 문자열로 반환
    let getAasXmlFromAasxFile(aasxPath: string): string =
        let aasFileInfo = readEnvironmentFromAasx aasxPath
        aasFileInfo.OriginalXml


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

