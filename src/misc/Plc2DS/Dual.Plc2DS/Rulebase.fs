namespace Dual.Plc2DS

open System
open System.Text.RegularExpressions
open System.Runtime.Serialization
open System.Collections.Generic

open Newtonsoft.Json

open Dual.Plc2DS
open Dual.Common.Core.FS

[<DataContract>]
type Rulebase() =

    [<DataMember>]
    member val CsvFilterPatterns     : CsvFilterPattern[]   = [||] with get, set

    [<DataMember>]
    member val FDASplitPattern       : string                = null with get, set

    /// Alias.  e.g [CLAMP, CLP, CMP].  [][0] 가 표준어, 나머지는 dialects
    [<DataMember>]
    member val Dialects             : string[][]            = [||] with get, set

    [<JsonIgnore>]
    member val DialectPatterns      : ReplacePattern[]      = [||] with get, set

    [<DataMember>]
    member val TagPatternDiscards   : Pattern[]             = null with get, set

    [<DataMember>]
    member val TagPatternReplaces   : ReplacePattern[]      = null with get, set

    [<DataMember>]
    member val TagPatternFDAs       : Pattern[]             = null with get, set

    /// <summary>
    /// split 된 FlowName 에서 replace 할 패턴
    /// </summary>
    [<DataMember>]
    member val FlowPatternReplaces  : ReplacePattern[]      = null with get, set

    /// <summary>
    /// split 된 DeviceName 에서 replace 할 패턴
    /// </summary>
    [<DataMember>]
    member val DevicePatternReplaces: ReplacePattern[]      = null with get, set

    /// <summary>
    /// split 된 ActionName 에서 replace 할 패턴
    /// </summary>
    [<DataMember>]
    member val ActionPatternReplaces: ReplacePattern[]      = null with get, set

    /// <summary>
    /// Gridview 에 표출할 column 명
    /// </summary>
    [<DataMember>]
    member val VisibleColumns       : string[]              = null with get, set

    member this.OnDeserialized() =
        // string array 로 구성된 Dialects 를 ReplacePattern[] 로 변환
        // [표준어, 방언1, 방언2, ...] 형태로 구성된 Dialects 를
        // (방언1|방언2|..) => 표준어 ... 형태의 replace patterns 로 변환
        this.DialectPatterns <-
            this.Dialects
            |> Array.mapi (fun i ds ->
                let std = ds.[0]
                let dialects = ds |> Array.skip 1
                let dialectsPattern =
                    dialects
                    |> Array.map (fun d -> $@"(?<=_)({d})(?=[_\d])  # _{d}_
| ^({d})(?=[_\d])        # {d}_ (문자열 시작)
| (?<=_)({d})$           # _{d} (문자열 끝)
| ^({d})$                # {d} (혼자 있을 때)
# 이 모든 것들을 {std} 로 변환")
                    |> Array.reduce (fun a b -> a + "|" + b)
                let regex = Regex(dialectsPattern, RegexOptions.Compiled ||| RegexOptions.IgnorePatternWhitespace)
                ReplacePattern.Create($"Dialect{i}", regex, std)
            )

    [<OnDeserialized>]
    member this.OnDeserializedMethod(_context: StreamingContext) =
        this.OnDeserialized()

    member this.Duplicate() =
        let y = Rulebase()
        // deep copy
        y.CsvFilterPatterns     <- this.CsvFilterPatterns        |> Array.copy
        y.Dialects              <- this.Dialects                 |> Array.map Array.copy
        y.DialectPatterns       <- this.DialectPatterns          |> Array.copy
        y.TagPatternDiscards    <- this.TagPatternDiscards       |> Array.copy
        y.TagPatternReplaces    <- this.TagPatternReplaces       |> Array.copy
        y.TagPatternFDAs        <- this.TagPatternFDAs           |> Array.copy
        y.FlowPatternReplaces   <- this.FlowPatternReplaces      |> Array.copy
        y.DevicePatternReplaces <- this.DevicePatternReplaces    |> Array.copy
        y.ActionPatternReplaces <- this.ActionPatternReplaces    |> Array.copy
        y.VisibleColumns        <- this.VisibleColumns           |> Array.copy
        y.FDASplitPattern       <- this.FDASplitPattern

        y.OnDeserialized()
        y

    member this.Merge(other: Rulebase) =
        this.CsvFilterPatterns     <- Array.append other.CsvFilterPatterns        this.CsvFilterPatterns
        this.Dialects              <- Array.append other.Dialects                 this.Dialects
        this.DialectPatterns       <- Array.append other.DialectPatterns          this.DialectPatterns
        this.TagPatternDiscards    <- Array.append other.TagPatternDiscards       this.TagPatternDiscards
        this.TagPatternReplaces    <- Array.append other.TagPatternReplaces       this.TagPatternReplaces
        this.TagPatternFDAs        <- Array.append other.TagPatternFDAs           this.TagPatternFDAs
        this.FlowPatternReplaces   <- Array.append other.FlowPatternReplaces      this.FlowPatternReplaces
        this.DevicePatternReplaces <- Array.append other.DevicePatternReplaces    this.DevicePatternReplaces
        this.ActionPatternReplaces <- Array.append other.ActionPatternReplaces    this.ActionPatternReplaces
        this.VisibleColumns        <- Array.append other.VisibleColumns           this.VisibleColumns
        //this.FDASplitPattern     <- this.FDASplitPattern                         // 기존 유지

        this.OnDeserialized()

    member this.Override(replace: Rulebase) =
        if not (isNull replace.CsvFilterPatterns) && replace.CsvFilterPatterns.Length > 0 then
            this.CsvFilterPatterns <- replace.CsvFilterPatterns
        if not (isNull replace.Dialects) && replace.Dialects.Length > 0 then
            this.Dialects <- replace.Dialects
        if not (isNull replace.DialectPatterns) && replace.DialectPatterns.Length > 0 then
            this.DialectPatterns <- replace.DialectPatterns
        if not (isNull replace.TagPatternDiscards) && replace.TagPatternDiscards.Length > 0 then
            this.TagPatternDiscards <- replace.TagPatternDiscards
        if not (isNull replace.TagPatternReplaces) && replace.TagPatternReplaces.Length > 0 then
            this.TagPatternReplaces <- replace.TagPatternReplaces
        if not (isNull replace.TagPatternFDAs) && replace.TagPatternFDAs.Length > 0 then
            this.TagPatternFDAs <- replace.TagPatternFDAs
        if not (isNull replace.FlowPatternReplaces) && replace.FlowPatternReplaces.Length > 0 then
            this.FlowPatternReplaces <- replace.FlowPatternReplaces
        if not (isNull replace.DevicePatternReplaces) && replace.DevicePatternReplaces.Length > 0 then
            this.DevicePatternReplaces <- replace.DevicePatternReplaces
        if not (isNull replace.ActionPatternReplaces) && replace.ActionPatternReplaces.Length > 0 then
            this.ActionPatternReplaces <- replace.ActionPatternReplaces
        if not (isNull replace.VisibleColumns) && replace.VisibleColumns.Length > 0 then
            this.VisibleColumns <- replace.VisibleColumns
        if not (String.IsNullOrEmpty replace.FDASplitPattern) then
            this.FDASplitPattern <- replace.FDASplitPattern

        this.OnDeserialized()

[<DataContract>]
type AppSettings() =
    inherit Rulebase()

    /// Vendor 별 Tag Semantic: 부가, additional
    [<DataMember>]
    member val AddOns    : Dictionary<string, Rulebase> = Dictionary() with get, set

    /// Vendor 별 Tag Semantic: override.  this 의 항목 override
    [<DataMember>]
    member val Overrides : Dictionary<string, Rulebase> = Dictionary() with get, set

    member this.CreateVendorRulebase(vendor: Vendor) =
        let getRB (dic: Dictionary<string, Rulebase>) (vendor: string) =
            match dic.TryGetValue(vendor) with
            | true, rb -> rb
            | _        -> getNull<Rulebase>()

        let v = vendor.ToString()
        let addOn   = getRB this.AddOns v
        let ovrride = getRB this.Overrides v
        if isItNull addOn && isItNull ovrride then
            this :> Rulebase
        else
            let y = this.Duplicate()
            if not (isItNull addOn) && not (isItNull ovrride) then
                y.Merge(addOn)
                y.Override(ovrride)
                y
            elif not (isItNull addOn) then addOn
            elif not (isItNull ovrride) then ovrride
            else raise (Exception("ERROR"))

