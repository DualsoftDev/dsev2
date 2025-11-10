namespace Ev2.Cpu.Core.UserDefined

open System
open Ev2.Cpu.Core

// ═════════════════════════════════════════════════════════════════════════════
// Scoping and Namespace Management
// ═════════════════════════════════════════════════════════════════════════════
// UserFC/FB의 변수 스코핑 및 네임스페이스 관리
// 파라미터/Static 변수의 이름 충돌 방지
// ═════════════════════════════════════════════════════════════════════════════

/// 스코프 타입
[<RequireQualifiedAccess>]
type ScopeType =
    | Global           // 전역 스코프
    | FC of fcName:string        // FC 스코프
    | FBInstance of instanceName:string * fbName:string  // FB 인스턴스 스코프
    | FBStatic of instanceName:string * fbName:string    // FB Static 변수 스코프
    | Temporary        // 임시 스코프

/// 스코프가 적용된 변수 이름
type ScopedName = {
    OriginalName: string    // 원래 이름
    ScopedName: string      // 스코프가 적용된 이름
    Scope: ScopeType        // 스코프 타입
    DataType: Type    // 데이터 타입
}

/// 스코프 매니저
type ScopeManager() =

    /// FC 파라미터에 스코프 적용
    /// 예: "temperature" → "FC_TempConvert_temperature"
    member _.ScopeFC(fcName: string, varName: string) : string =
        sprintf "FC_%s_%s" fcName varName

    /// FB 인스턴스 파라미터에 스코프 적용
    /// 예: "Motor1", "speed" → "FB_Motor1_speed"
    member _.ScopeFBInstance(instanceName: string, varName: string) : string =
        sprintf "FB_%s_%s" instanceName varName

    /// FB Static 변수에 스코프 적용
    /// 예: "Motor1", "counter" → "FB_Motor1_Static_counter"
    member _.ScopeFBStatic(instanceName: string, varName: string) : string =
        sprintf "FB_%s_Static_%s" instanceName varName

    /// FB Temp 변수에 스코프 적용
    /// 예: "Motor1", "tempResult" → "FB_Motor1_Temp_tempResult"
    member _.ScopeFBTemp(instanceName: string, varName: string) : string =
        sprintf "FB_%s_Temp_%s" instanceName varName

    /// 스코프가 적용된 이름에서 원래 이름 추출
    member _.UnscopeName(scopedName: string) : string option =
        // "FC_TempConvert_temperature" → "temperature"
        // "FB_Motor1_speed" → "speed"
        // "FB_Motor1_Static_counter" → "counter"
        if scopedName.StartsWith("FC_") then
            let parts = scopedName.Split('_')
            if parts.Length >= 3 then
                Some (String.concat "_" (Array.skip 2 parts))
            else
                None
        elif scopedName.StartsWith("FB_") && scopedName.Contains("_Static_") then
            let parts = scopedName.Split([|"_Static_"|], StringSplitOptions.None)
            if parts.Length = 2 then
                Some parts.[1]
            else
                None
        elif scopedName.StartsWith("FB_") && scopedName.Contains("_Temp_") then
            let parts = scopedName.Split([|"_Temp_"|], StringSplitOptions.None)
            if parts.Length = 2 then
                Some parts.[1]
            else
                None
        elif scopedName.StartsWith("FB_") then
            let parts = scopedName.Split('_')
            if parts.Length >= 3 then
                Some (String.concat "_" (Array.skip 2 parts))
            else
                None
        else
            Some scopedName  // 스코프가 없는 이름

    /// UserFC의 모든 변수에 스코프 적용
    member this.ScopeUserFC(fc: UserFC) : ScopedName list =
        let inputs =
            fc.Inputs
            |> List.map (fun p ->
                { OriginalName = p.Name
                  ScopedName = this.ScopeFC(fc.Name, p.Name)
                  Scope = ScopeType.FC fc.Name
                  DataType = p.DataType })

        let outputs =
            fc.Outputs
            |> List.map (fun p ->
                { OriginalName = p.Name
                  ScopedName = this.ScopeFC(fc.Name, p.Name)
                  Scope = ScopeType.FC fc.Name
                  DataType = p.DataType })

        inputs @ outputs

    /// UserFB 인스턴스의 모든 변수에 스코프 적용
    member this.ScopeFBInstance(instance: FBInstance) : ScopedName list =
        let fb = instance.FBType
        let instName = instance.Name

        let inputs =
            fb.Inputs
            |> List.map (fun p ->
                { OriginalName = p.Name
                  ScopedName = this.ScopeFBInstance(instName, p.Name)
                  Scope = ScopeType.FBInstance(instName, fb.Name)
                  DataType = p.DataType })

        let outputs =
            fb.Outputs
            |> List.map (fun p ->
                { OriginalName = p.Name
                  ScopedName = this.ScopeFBInstance(instName, p.Name)
                  Scope = ScopeType.FBInstance(instName, fb.Name)
                  DataType = p.DataType })

        let inouts =
            fb.InOuts
            |> List.map (fun p ->
                { OriginalName = p.Name
                  ScopedName = this.ScopeFBInstance(instName, p.Name)
                  Scope = ScopeType.FBInstance(instName, fb.Name)
                  DataType = p.DataType })

        let statics =
            fb.Statics
            |> List.map (fun (name, dt, _) ->
                { OriginalName = name
                  ScopedName = this.ScopeFBStatic(instName, name)
                  Scope = ScopeType.FBStatic(instName, fb.Name)
                  DataType = dt })

        let temps =
            fb.Temps
            |> List.map (fun (name, dt) ->
                { OriginalName = name
                  ScopedName = this.ScopeFBTemp(instName, name)
                  Scope = ScopeType.FBStatic(instName, fb.Name)
                  DataType = dt })

        inputs @ outputs @ inouts @ statics @ temps

/// 네임스페이스 관리
module NamespaceManager =

    /// 네임스페이스 구분자
    let private separator = "."

    /// 전체 이름 생성 (네임스페이스 + 이름)
    /// 예: "MyLib", "MotorControl" → "MyLib.MotorControl"
    let makeFullName (namespace': string option) (name: string) : string =
        match namespace' with
        | Some ns when not (String.IsNullOrWhiteSpace(ns)) ->
            sprintf "%s%s%s" ns separator name
        | _ -> name

    /// 네임스페이스 분리
    /// 예: "MyLib.MotorControl" → (Some "MyLib", "MotorControl")
    let splitNamespace (fullName: string) : (string option * string) =
        let parts = fullName.Split([|separator|], StringSplitOptions.RemoveEmptyEntries)
        if parts.Length > 1 then
            let ns = String.concat separator (Array.take (parts.Length - 1) parts)
            let name = parts.[parts.Length - 1]
            (Some ns, name)
        else
            (None, fullName)

    /// 네임스페이스 가져오기
    /// 예: "MyLib.MotorControl" → Some "MyLib"
    let getNamespace (fullName: string) : string option =
        fst (splitNamespace fullName)

    /// 이름 가져오기 (네임스페이스 제거)
    /// 예: "MyLib.MotorControl" → "MotorControl"
    let getName (fullName: string) : string =
        snd (splitNamespace fullName)

    /// 네임스페이스 검증
    /// 네임스페이스는 알파벳, 숫자, 언더스코어, 점만 허용
    let validateNamespace (namespace': string) : Result<unit, string> =
        if String.IsNullOrWhiteSpace(namespace') then
            Error "Namespace cannot be empty"
        else
            let parts = namespace'.Split([|separator|], StringSplitOptions.RemoveEmptyEntries)
            let invalidParts =
                parts
                |> Array.filter (fun part ->
                    not (UserDefinitionValidation.isValidIdentifier part))
            if Array.isEmpty invalidParts then
                Ok ()
            else
                let invalid = String.concat ", " invalidParts
                Error (sprintf "Invalid namespace parts: %s" invalid)

/// 변수 이름 충돌 검사
module CollisionDetector =

    /// 두 개의 ScopedName 리스트 간 충돌 검사
    let detectCollisions (names1: ScopedName list) (names2: ScopedName list) : (ScopedName * ScopedName) list =
        let scopedNames1 = names1 |> List.map (fun n -> n.ScopedName) |> Set.ofList
        let scopedNames2 = names2 |> List.map (fun n -> n.ScopedName) |> Set.ofList

        let collisions = Set.intersect scopedNames1 scopedNames2

        collisions
        |> Set.toList
        |> List.choose (fun scopedName ->
            let n1 = names1 |> List.tryFind (fun n -> n.ScopedName = scopedName)
            let n2 = names2 |> List.tryFind (fun n -> n.ScopedName = scopedName)
            match n1, n2 with
            | Some n1, Some n2 -> Some (n1, n2)
            | _ -> None)

    /// UserFC들 간 변수 이름 충돌 검사
    let detectFCCollisions (fcs: UserFC list) : (string * string * string) list =
        let scopeManager = ScopeManager()
        let allScopedNames =
            fcs
            |> List.map (fun fc ->
                (fc.Name, scopeManager.ScopeUserFC fc))

        // 각 FC 쌍에 대해 충돌 검사
        allScopedNames
        |> List.collect (fun (fc1Name, fc1Names) ->
            allScopedNames
            |> List.filter (fun (fc2Name, _) -> fc1Name < fc2Name)  // 중복 방지
            |> List.collect (fun (fc2Name, fc2Names) ->
                detectCollisions fc1Names fc2Names
                |> List.map (fun (n1, n2) ->
                    (fc1Name, fc2Name, n1.ScopedName))))

    /// FB 인스턴스들 간 변수 이름 충돌 검사
    let detectInstanceCollisions (instances: FBInstance list) : (string * string * string) list =
        let scopeManager = ScopeManager()
        let allScopedNames =
            instances
            |> List.map (fun inst ->
                (inst.Name, scopeManager.ScopeFBInstance inst))

        // 각 인스턴스 쌍에 대해 충돌 검사
        allScopedNames
        |> List.collect (fun (inst1Name, inst1Names) ->
            allScopedNames
            |> List.filter (fun (inst2Name, _) -> inst1Name < inst2Name)
            |> List.collect (fun (inst2Name, inst2Names) ->
                detectCollisions inst1Names inst2Names
                |> List.map (fun (n1, n2) ->
                    (inst1Name, inst2Name, n1.ScopedName))))

/// 스코프 컨텍스트
/// 현재 실행 중인 FC/FB의 스코프 정보
type ScopeContext = {
    ActiveFCs: Map<string, UserFC>              // 활성 FC 목록
    ActiveInstances: Map<string, FBInstance>    // 활성 FB 인스턴스 목록
    ScopeManager: ScopeManager                  // 스코프 매니저
} with
    static member Empty = {
        ActiveFCs = Map.empty
        ActiveInstances = Map.empty
        ScopeManager = ScopeManager()
    }

    /// FC 활성화
    member this.ActivateFC(fc: UserFC) : ScopeContext =
        { this with ActiveFCs = Map.add fc.Name fc this.ActiveFCs }

    /// FB 인스턴스 활성화
    member this.ActivateInstance(instance: FBInstance) : ScopeContext =
        { this with ActiveInstances = Map.add instance.Name instance this.ActiveInstances }

    /// 변수 이름 해결 (스코프 적용)
    /// 현재 활성 FC/FB 인스턴스를 기준으로 변수 이름을 스코프가 적용된 이름으로 변환
    member this.ResolveName(varName: string) : string option =
        // 1. 현재 활성 FC에서 찾기
        this.ActiveFCs
        |> Map.tryPick (fun fcName fc ->
            let parameters = fc.Inputs @ fc.Outputs
            if parameters |> List.exists (fun p -> p.Name = varName) then
                Some (this.ScopeManager.ScopeFC(fcName, varName))
            else
                None)
        // 2. 현재 활성 FB 인스턴스에서 찾기
        |> Option.orElseWith (fun () ->
            this.ActiveInstances
            |> Map.tryPick (fun instName inst ->
                let fb = inst.FBType
                let allVars =
                    (fb.Inputs @ fb.Outputs @ fb.InOuts |> List.map (fun p -> p.Name)) @
                    (fb.Statics |> List.map (fun (n, _, _) -> n)) @
                    (fb.Temps |> List.map (fun (n, _) -> n))
                if allVars |> List.contains varName then
                    // Static/Temp 구분
                    if fb.Statics |> List.exists (fun (n, _, _) -> n = varName) then
                        Some (this.ScopeManager.ScopeFBStatic(instName, varName))
                    elif fb.Temps |> List.exists (fun (n, _) -> n = varName) then
                        Some (this.ScopeManager.ScopeFBTemp(instName, varName))
                    else
                        Some (this.ScopeManager.ScopeFBInstance(instName, varName))
                else
                    None))
