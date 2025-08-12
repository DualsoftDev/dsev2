namespace Dual.Ev2.Aas

open System.Runtime.CompilerServices
open Ev2.Core.FS
open Dual.Common.Db.FS

/// C#에서 F# AASX 메서드에 접근하기 위한 Extension 메서드들
/// F#의 type extension은 C#에서 직접 접근이 불가능하므로 wrapper 제공
[<Extension>]
type Ev2AasExtensionForCSharp =

    // =====================
    // Project AASX Export 메서드 - C# 전용
    // =====================

    /// Project를 AASX 파일로 내보내기
    [<Extension>]
    static member CsExportToAasxFile(project:Project, outputPath:string) : unit =
        project.ExportToAasxFile(outputPath)

    /// Project를 AASX 파일로 내보내기 (DB 정보 포함)
    [<Extension>]
    static member CsExportToAasxFile(project:Project, outputPath:string, dbApi:AppDbApi) : unit =
        project.ExportToAasxFile(outputPath, dbApi)

    /// 기존 AASX 파일에 Project submodel 주입
    [<Extension>]
    static member CsInjectToExistingAasxFile(project:Project, aasxPath:string) : unit =
        project.InjectToExistingAasxFile(aasxPath)

    // =====================
    // NjProject AASX Export 메서드 - C# 전용
    // =====================

    /// NjProject를 AASX 파일로 내보내기
    [<Extension>]
    static member CsExportToAasxFile(njProject:NjProject, outputPath:string) : unit =
        njProject.ExportToAasxFile(outputPath)

    /// 기존 AASX 파일에 NjProject submodel 주입
    [<Extension>]
    static member CsInjectToExistingAasxFile(njProject:NjProject, aasxPath:string) : unit =
        njProject.InjectToExistingAasxFile(aasxPath)

    // =====================
    // Helper 메서드들
    // =====================

    /// AASX 파일에서 AAS XML 문자열 읽기
    [<Extension>]
    static member CsUpdateDbAasXml(project:Project, aasxPath:string, dbApi:AppDbApi) : unit =
        project.UpdateDbAasXml(aasxPath, dbApi)

    /// Project를 AAS JSON 문자열로 변환
    [<Extension>]
    static member CsToAasJsonString(njProject:NjProject) : string =
        njProject.ToAasJsonStringENV()

    /// Project를 AAS Environment로 변환
    [<Extension>]
    static member CsToENV(njProject:NjProject) : AasCore.Aas3_0.Environment =
        njProject.ToENV()

// =====================
// Static 메서드들도 C#에서 접근하기 위한 래퍼 클래스
// =====================

/// Project static 메서드 래퍼
type ProjectAasxExtensions =
    /// AASX 파일에서 Project 읽기
    static member FromAasxFile(aasxPath:string) : Project =
        let njProj = NjProject.FromAasxFile(aasxPath)
        njProj.ToJson() |> Project.FromJson

/// NjProject static 메서드 래퍼
type NjProjectAasxExtensions =
    /// AASX 파일에서 NjProject 읽기
    static member FromAasxFile(aasxPath:string) : NjProject =
        let aasFileInfo = AasXModule.readEnvironmentFromAasx aasxPath
        let env = aasFileInfo.Environment

        let projectSubmodel =
            env.Submodels
            |> Seq.tryFind (fun sm -> sm.IdShort = PreludeModule.SubmodelIdShort)
            |> function
                | Some sm -> sm
                | None -> failwith $"No submodel with IdShort='{PreludeModule.SubmodelIdShort}' found in AASX file"

        NjProject.FromISubmodel(projectSubmodel)