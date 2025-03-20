using Dual.Plc2DS;

using Plc2DsApp;

using System;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

namespace Plc2DsApp
{
    [DataContract]
    public class AppSettings : Rulebase
    {
        /// Vendor 별 Tag Semantic: 부가, additional
        [DataMember] public Dictionary<string, Rulebase> AddOns = new();
        /// Vendor 별 Tag Semantic: override.  this 의 항목 override
        [DataMember] public Dictionary<string, Rulebase> Overrides = new();

        public Rulebase CreateVendorRulebase(Vendor vendor)
        {
            Rulebase getRB(Dictionary<string, Rulebase> dic, string vendor) => dic.TryGetValue(vendor, out Rulebase rb) ? rb : null;

            var v = vendor.ToString();
            var addOn = getRB(AddOns, v);
            var ovrride = getRB(Overrides, v);
            if (addOn == null && ovrride == null)
                return this;
            else
            {
                var y = this.Duplicate();
                if (addOn != null && ovrride != null)
                {
                    y.Merge(addOn);
                    y.Override(ovrride);
                    return y;
                }
                else if (addOn != null && ovrride == null)
                    return addOn;
                else if (addOn == null && ovrride != null)
                    return ovrride;
                else
                    throw new Exception("ERROR");
            }
        }
    }

    [DataContract]
    public class Rulebase
    {
        [DataMember] public CsvFilterPattern[] CsvFilterPatterns { get; set; } = [];

        [DataMember] public string FDASplitPattern { get; set; }

        /// Alias.  e.g [CLAMP, CLP, CMP].  [][0] 가 표준어, 나머지는 dialects
        [DataMember] public string[][] Dialects { get; set; } = [];
        [JsonIgnore] public ReplacePattern[] DialectPatterns = [];

        [DataMember] public Pattern[] TagPatternDiscards { get; set; }
        [DataMember] public ReplacePattern[] TagPatternReplaces { get; set; }
        [DataMember] public Pattern[] TagPatternFDAs { get; set; }
        /// <summary>
        /// split 된 FlowName 에서 replace 할 패턴
        /// </summary>
        [DataMember] public ReplacePattern[] FlowPatternReplaces { get; set; }
        /// <summary>
        /// split 된 DeviceName 에서 replace 할 패턴
        /// </summary>
        [DataMember] public ReplacePattern[] DevicePatternReplaces { get; set; }
        /// <summary>
        /// split 된 ActionName 에서 replace 할 패턴
        /// </summary>
        [DataMember] public ReplacePattern[] ActionPatternReplaces { get; set; }
        /// <summary>
        /// Gridview 에 표출할 column 명
        /// </summary>
        [DataMember] public string[] VisibleColumns { get; set; }

        public void OnDeserialized()
        {
            // string array 로 구성된 Dialects 를 ReplacePattern[] 로 변환
            // [표준어, 방언1, 방언2, ...] 형태로 구성된 Dialects 를
            // (방언1|방언2|..) => 표준어 ... 형태의 replace patterns 로 변환
            DialectPatterns =
                Dialects.Select( (ds, i) =>
                {
                    var std = ds[0];
                    var dialects = ds.Skip(1).ToArray();
                    var dialectsPattern =
                        dialects.Select(d =>
$@"(?<=_)({d})(?=[_\d])  # _{d}_
| ^({d})(?=[_\d])        # {d}_ (문자열 시작)
| (?<=_)({d})$           # _{d} (문자열 끝)
| ^({d})$                # {d} (혼자 있을 때)
# 이 모든 것들을 {std} 로 변환
"
                        ).Aggregate((a, b) => $"{a}|{b}")
                        ;
                    var regex = new Regex(dialectsPattern, RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);
                    return ReplacePattern.Create($"Dialect{i}", regex, std);
                }).ToArray()
                ;
        }
        [OnDeserialized]
        public void OnDeserializedMethod(StreamingContext context) => OnDeserialized();


        public Rulebase Duplicate()
        {
            var y = new Rulebase();
            // deep copy

            y.CsvFilterPatterns     = this.CsvFilterPatterns        .ToArray();
            y.Dialects              = this.Dialects                 .ToArray();
            y.DialectPatterns       = this.DialectPatterns          .ToArray();
            y.TagPatternDiscards    = this.TagPatternDiscards       .ToArray();
            y.TagPatternReplaces    = this.TagPatternReplaces       .ToArray();
            y.TagPatternFDAs        = this.TagPatternFDAs           .ToArray();
            y.FlowPatternReplaces   = this.FlowPatternReplaces      .ToArray();
            y.DevicePatternReplaces = this.DevicePatternReplaces    .ToArray();
            y.ActionPatternReplaces = this.ActionPatternReplaces    .ToArray();
            y.VisibleColumns        = this.VisibleColumns           .ToArray();
            y.FDASplitPattern = this.FDASplitPattern;

            y.OnDeserialized();

            return y;
        }


        public void Merge(Rulebase other)
        {
            this.CsvFilterPatterns     = other.CsvFilterPatterns        .Concat(this.CsvFilterPatterns)        .ToArray();
            this.Dialects              = other.Dialects                 .Concat(this.Dialects).ToArray();
            this.DialectPatterns       = other.DialectPatterns          .Concat(this.DialectPatterns).ToArray();
            this.TagPatternDiscards    = other.TagPatternDiscards       .Concat(this.TagPatternDiscards).ToArray();
            this.TagPatternReplaces    = other.TagPatternReplaces       .Concat(this.TagPatternReplaces).ToArray();
            this.TagPatternFDAs        = other.TagPatternFDAs           .Concat(this.TagPatternFDAs).ToArray();
            this.FlowPatternReplaces   = other.FlowPatternReplaces      .Concat(this.FlowPatternReplaces).ToArray();
            this.DevicePatternReplaces = other.DevicePatternReplaces    .Concat(this.DevicePatternReplaces).ToArray();
            this.ActionPatternReplaces = other.ActionPatternReplaces    .Concat(this.ActionPatternReplaces).ToArray();
            this.VisibleColumns        = other.VisibleColumns           .Concat(this.VisibleColumns).ToArray();
            //this.FDASplitPattern       = this.FDASplitPattern;         .Concat(other.FDASplitPattern

            this.OnDeserialized();
        }


        public void Override(Rulebase replace)
        {
            if (replace.CsvFilterPatterns.NonNullAny())
                this.CsvFilterPatterns = replace.CsvFilterPatterns;
            if (replace.Dialects.NonNullAny())
                this.Dialects = replace.Dialects;
            if (replace.DialectPatterns.NonNullAny())
                this.DialectPatterns = replace.DialectPatterns;
            if (replace.TagPatternDiscards.NonNullAny())
                this.TagPatternDiscards = replace.TagPatternDiscards;
            if (replace.TagPatternReplaces.NonNullAny())
                this.TagPatternReplaces = replace.TagPatternReplaces;
            if (replace.TagPatternFDAs.NonNullAny())
                this.TagPatternFDAs = replace.TagPatternFDAs;
            if (replace.FlowPatternReplaces.NonNullAny())
                this.FlowPatternReplaces = replace.FlowPatternReplaces;
            if (replace.DevicePatternReplaces.NonNullAny())
                this.DevicePatternReplaces = replace.DevicePatternReplaces;
            if (replace.ActionPatternReplaces.NonNullAny())
                this.ActionPatternReplaces = replace.ActionPatternReplaces;
            if (replace.VisibleColumns.NonNullAny())
                this.VisibleColumns = replace.VisibleColumns;
            if (replace.FDASplitPattern.NonNullAny())
                this.FDASplitPattern = replace.FDASplitPattern;

            this.OnDeserialized();
        }

    }
}
