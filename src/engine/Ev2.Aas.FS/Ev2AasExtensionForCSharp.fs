namespace Dual.Ev2.Aas

open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open AasCore.Aas3_0
open Dual.Common.Db.FS
open Ev2.Core.FS

/// C#에서 F# AASX 메서드에 접근하기 위한 Extension 메서드들
/// F#의 type extension은 C#에서 직접 접근이 불가능하므로 wrapper 제공
type Ev2AasExtensionForCSharp = // CsExportToAasxFile, CsInjectToExistingAasxFile, CsToAasJsonString, CsToENV, CsUpdateDbAasXml

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

/// Project static 메서드 래퍼
type AasxExtensions = // CsTryGetPropValue<'T>, CsTrySetProperty<'T>, FromAasxFile
    /// AASX 파일에서 Project 읽기
    static member FromAasxFile(aasxPath:string) : Project =
        let njProj = NjProject.FromAasxFile(aasxPath)
        //njProj.ToJson() |> Project.FromJson
        njProj.ToJson() |> ProjectExtensions.CsFromJson

    [<Extension>]
    static member CsTrySetProperty<'T>(
        jObj:JObj, value:'T, name:string
        , [<Optional; DefaultParameterValue(null:PropertyCounter)>] counters: PropertyCounter
    ) =
        let counters = if counters = null then None else Some counters
        jObj.TrySetProperty(value, name, ?counters=counters)

    [<Extension>]
    static member CsTryGetPropValue<'T>(smc: SubmodelElementCollection, propName:string) =
        smc.TryGetPropValue<'T>(propName)

