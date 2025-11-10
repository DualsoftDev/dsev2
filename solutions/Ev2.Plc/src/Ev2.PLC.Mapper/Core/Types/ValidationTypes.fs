namespace Ev2.PLC.Mapper.Core.Types

open System
open Ev2.PLC.Common.Types

/// 검증 요약
type ValidationSummary = {
    TotalChecks: int
    PassedChecks: int
    InfoCount: int
    WarningCount: int
    ErrorCount: int
    CriticalCount: int
    ValidationTime: TimeSpan
} with
    static member FromResults(results: ValidationResult list) =
        let groupedResults = results |> List.groupBy (fun result -> result.Severity)
        let getCount severity =
            groupedResults
            |> List.tryFind (fun (s, _) -> s = severity)
            |> Option.map (fun (_, r) -> r.Length)
            |> Option.defaultValue 0

        {
            TotalChecks = results.Length
            PassedChecks = results |> List.filter (fun r -> r.IsValid) |> List.length
            InfoCount = getCount ValidationSeverity.ValidationInfo
            WarningCount = getCount ValidationSeverity.ValidationWarning
            ErrorCount = getCount ValidationSeverity.ValidationError
            CriticalCount = getCount ValidationSeverity.ValidationCritical
            ValidationTime = TimeSpan.Zero
        }

    member this.SuccessRate =
        if this.TotalChecks = 0 then 100.0
        else (float this.PassedChecks) / (float this.TotalChecks) * 100.0

/// 전체 검증 결과
type ValidationResults = {
    OverallSuccess: bool
    Results: ValidationResult list
    Summary: ValidationSummary
    Recommendations: string list
} with
    static member Create(results: ValidationResult list) =
        let summary = ValidationSummary.FromResults(results)
        {
            OverallSuccess = summary.CriticalCount = 0 && summary.ErrorCount = 0
            Results = results
            Summary = summary
            Recommendations = []
        }

    member this.HasErrors = this.Summary.ErrorCount > 0 || this.Summary.CriticalCount > 0
    member this.HasWarnings = this.Summary.WarningCount > 0

/// 명명 규칙 검증
module NamingValidation =
    let validateVariableName (conventions: NamingConvention list) (variable: RawVariable) : ValidationResult =
        let matchedConventions =
            conventions
            |> List.filter (fun conv ->
                System.Text.RegularExpressions.Regex.IsMatch(variable.Name, conv.Pattern))

        match matchedConventions with
        | [] ->
            ValidationResult.Warning(
                $"Variable '{variable.Name}' does not match any naming convention",
                variable.Name,
                "Consider following standard naming patterns like AREA_DEVICE_API")
        | [ _ ] ->
            ValidationResult.Success
        | _ ->
            ValidationResult.Warning(
                $"Variable '{variable.Name}' matches multiple conventions",
                variable.Name,
                "Use more specific naming to avoid ambiguity")

    let validateDeviceName (deviceName: string) : ValidationResult =
        if String.IsNullOrWhiteSpace(deviceName) then
            ValidationResult.Error("Device name cannot be empty")
        elif deviceName.Length > 50 then
            ValidationResult.Warning("Device name is too long", suggestion = "Keep device names under 50 characters")
        elif not (System.Text.RegularExpressions.Regex.IsMatch(deviceName, @"^[A-Z0-9_]+$")) then
            ValidationResult.Warning("Device name should use only uppercase letters, numbers, and underscores")
        else
            ValidationResult.Success

/// 주소 검증
module AddressValidation =
    let validateAddressRange (ranges: Map<string, AddressRange>) (address: PlcAddress) : ValidationResult =
        match address.DeviceArea with
        | None -> ValidationResult.Warning("Address has no device area specified")
        | Some deviceArea ->
            match ranges.TryFind(deviceArea) with
            | None ->
                ValidationResult.Warning(
                    $"No address range defined for device type '{deviceArea}'",
                    suggestion = "Define address ranges in configuration")
            | Some range ->
                match address.Index with
                | None -> ValidationResult.Warning("Address has no index specified")
                | Some index ->
                    if index < range.StartAddress || index > range.EndAddress then
                        ValidationResult.Error(
                            $"Address {address.FullAddress} is outside valid range {range.StartAddress}-{range.EndAddress}")
                    else
                        ValidationResult.Success

    let validateAddressConflict (variables: IOVariable list) : ValidationResult list =
        variables
        |> List.groupBy (fun v -> v.PhysicalAddress.FullAddress)
        |> List.choose (fun (addr, vars) ->
            if vars.Length > 1 then
                let varNames = vars |> List.map (fun v -> v.LogicalName) |> String.concat ", "
                Some (ValidationResult.Error(
                    $"Address conflict: {addr} is used by multiple variables: {varNames}",
                    suggestion = "Assign unique addresses to each variable"))
            else
                None)

/// 데이터 타입 검증
module DataTypeValidation =
    let validateDataTypeCompatibility (address: PlcAddress) (dataType: PlcDataType) (vendor: PlcVendor) : ValidationResult =
        match vendor, address.DeviceArea, dataType with
        | LSElectric _, Some "M", t when t <> Bool ->
            ValidationResult.Error("LS Electric M devices only support BOOL data type")
        | LSElectric _, Some "D", Bool ->
            ValidationResult.Warning("Using BOOL with D register wastes memory", suggestion = "Consider using M register for BOOL")
        | Mitsubishi _, Some "X", t when t <> Bool ->
            ValidationResult.Error("Mitsubishi X devices only support BOOL data type")
        | AllenBradley _, _, String _ ->
            ValidationResult.Warning("String handling may require special consideration in Allen-Bradley PLCs")
        | _ ->
            ValidationResult.Success

/// 로직 검증
module LogicValidation =
    let validateApiDependencies (dependencies: ApiDependency list) : ValidationResult list =
        dependencies
        |> List.collect (fun dep ->
            let rec checkCircular (api: string) (visited: string list) =
                if visited |> List.contains api then
                    [ValidationResult.Error($"Circular dependency detected in API '{api}'")]
                else
                    let newVisited = api :: visited
                    dependencies
                    |> List.filter (fun d -> d.PrecedingApis |> List.contains api)
                    |> List.collect (fun d -> checkCircular d.Api newVisited)

            let circularResults = checkCircular dep.Api []

            let conflictResults =
                dep.InterlockApis
                |> List.choose (fun interlock ->
                    if dep.PrecedingApis |> List.contains interlock then
                        Some (ValidationResult.Warning(
                            $"API '{dep.Api}' has '{interlock}' as both prerequisite and interlock",
                            suggestion = "Review logic - this may cause deadlock"))
                    else
                        None)

            circularResults @ conflictResults)

    let validateSafetyInterlocks (sequences: DeviceSequence list) : ValidationResult list =
        sequences
        |> List.collect (fun seq ->
            if seq.SafetyInterlocks.IsEmpty && seq.Device.ToUpper().Contains("MOTOR") then
                [ValidationResult.Warning(
                    $"Motor device '{seq.Device}' has no safety interlocks defined",
                    suggestion = "Consider adding emergency stop interlocks")]
            else
                [])

/// 성능 검증
module PerformanceValidation =
    let validateAddressOptimization (variables: IOVariable list) : ValidationResult list =
        let groupedByType = variables |> List.groupBy (fun v -> v.PhysicalAddress.DeviceArea)

        groupedByType
        |> List.collect (fun (deviceArea, vars) ->
            let addresses = vars |> List.choose (fun v -> v.PhysicalAddress.Index) |> List.sort
            if addresses.IsEmpty then
                []
            else
                let gaps =
                    addresses
                    |> List.pairwise
                    |> List.filter (fun (a, b) -> b - a > 1)
                    |> List.length

                if gaps > addresses.Length / 2 then
                    [ValidationResult.Warning(
                        $"Address allocation for {deviceArea} has many gaps ({gaps} gaps for {addresses.Length} addresses)",
                        suggestion = "Consider compacting address allocation for better performance")]
                else
                    [])

/// 검증 엔진 설정
type ValidationConfiguration = {
    EnableNamingValidation: bool
    EnableAddressValidation: bool
    EnableDataTypeValidation: bool
    EnableLogicValidation: bool
    EnablePerformanceValidation: bool
    StrictMode: bool
    CustomValidators: (RawVariable -> ValidationResult) list
} with
    static member Default = {
        EnableNamingValidation = true
        EnableAddressValidation = true
        EnableDataTypeValidation = true
        EnableLogicValidation = true
        EnablePerformanceValidation = false
        StrictMode = false
        CustomValidators = []
    }

    static member Strict = {
        ValidationConfiguration.Default with
            StrictMode = true
            EnablePerformanceValidation = true
    }
