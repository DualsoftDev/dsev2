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
    member val CsvFilterExpression : CsvFilterExpression   = getNull<CsvFilterExpression>() with get, set

    [<DataMember>]
    member val FDASplitPattern      : string                = null with get, set

    /// Alias.  e.g [CLAMP, CLP, CMP].  [][0] 가 표준어, 나머지는 dialects
    [<DataMember>]
    member val Dialects             : string[][]            = [||] with get, set

    [<JsonIgnore>]
    member val DialectPatterns      : ReplacePattern[]      = [||] with get, set

    [<DataMember>]
    member val TagPatternDiscards   : Pattern[]             = [||] with get, set

    [<DataMember>]
    member val TagPatternReplaces   : ReplacePattern[]      = [||] with get, set

    [<DataMember>]
    member val TagPatternFDAs       : Pattern[]             = [||] with get, set

    /// <summary>
    /// split 된 FlowName 에서 replace 할 패턴
    /// </summary>
    [<DataMember>]
    member val FlowPatternReplaces  : ReplacePattern[]      = [||] with get, set

    /// <summary>
    /// split 된 DeviceName 에서 replace 할 패턴
    /// </summary>
    [<DataMember>]
    member val DevicePatternReplaces: ReplacePattern[]      = [||] with get, set

    /// <summary>
    /// split 된 ActionName 에서 replace 할 패턴
    /// </summary>
    [<DataMember>]
    member val ActionPatternReplaces: ReplacePattern[]      = [||] with get, set

    /// <summary>
    /// Gridview 에 표출할 column 명
    /// </summary>
    [<DataMember>]
    member val VisibleColumns       : string[]              = [||] with get, set

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
        if isItNotNull this.CsvFilterExpression then
            y.CsvFilterExpression   <- this.CsvFilterExpression.Duplicate()

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
        if isItNotNull other.CsvFilterExpression then
            this.CsvFilterExpression     <- other.CsvFilterExpression.Merge(this.CsvFilterExpression)

        this.Dialects              <- other.Dialects              @ this.Dialects
        this.DialectPatterns       <- other.DialectPatterns       @ this.DialectPatterns
        this.TagPatternDiscards    <- other.TagPatternDiscards    @ this.TagPatternDiscards
        this.TagPatternReplaces    <- other.TagPatternReplaces    @ this.TagPatternReplaces
        this.TagPatternFDAs        <- other.TagPatternFDAs        @ this.TagPatternFDAs
        this.FlowPatternReplaces   <- other.FlowPatternReplaces   @ this.FlowPatternReplaces
        this.DevicePatternReplaces <- other.DevicePatternReplaces @ this.DevicePatternReplaces
        this.ActionPatternReplaces <- other.ActionPatternReplaces @ this.ActionPatternReplaces
        this.VisibleColumns        <- other.VisibleColumns        @ this.VisibleColumns
        //this.FDASplitPattern     <- this.FDASplitPattern                         // 기존 유지

        this.OnDeserialized()

    member this.Override(replace: Rulebase) =
        if isItNotNull replace.CsvFilterExpression then
            this.CsvFilterExpression   <- replace.CsvFilterExpression
        if replace.Dialects             .NonNullAny() then
            this.Dialects              <- replace.Dialects
        if replace.DialectPatterns      .NonNullAny() then
            this.DialectPatterns       <- replace.DialectPatterns
        if replace.TagPatternDiscards   .NonNullAny() then
            this.TagPatternDiscards    <- replace.TagPatternDiscards
        if replace.TagPatternReplaces   .NonNullAny() then
            this.TagPatternReplaces    <- replace.TagPatternReplaces
        if replace.TagPatternFDAs       .NonNullAny() then
            this.TagPatternFDAs        <- replace.TagPatternFDAs
        if replace.FlowPatternReplaces  .NonNullAny() then
            this.FlowPatternReplaces   <- replace.FlowPatternReplaces
        if replace.DevicePatternReplaces.NonNullAny() then
            this.DevicePatternReplaces <- replace.DevicePatternReplaces
        if replace.ActionPatternReplaces.NonNullAny() then
            this.ActionPatternReplaces <- replace.ActionPatternReplaces
        if replace.VisibleColumns       .NonNullAny() then
            this.VisibleColumns        <- replace.VisibleColumns
        if replace.FDASplitPattern      .NonNullAny() then
            this.FDASplitPattern       <- replace.FDASplitPattern

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

    member x.CreateVendorRulebase(vendor: Vendor): Rulebase =
        let vendor = vendor.ToString()
        let addOn = x.AddOns.TryGet(vendor)
        let ovrride = x.Overrides.TryGet(vendor)
        if addOn.IsNone && ovrride.IsNone then
            x
        else
            let y = x.Duplicate()
            addOn.Iter (fun a -> y.Merge(a))
            ovrride.Iter (fun o -> y.Override(o))
            y
