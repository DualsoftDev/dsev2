using System.Runtime.Serialization;

namespace Plc2DsApp
{
    [DataContract]
    public class AppSettings
    {
        [DataMember] public string FDASplitPattern { get; set; }

        /// Alias.  e.g [CLAMP, CLP, CMP].  [][0] 가 표준어, 나머지는 dialects
        [DataMember] public string[][] Dialects { get; set; } = [];
        [JsonIgnore] public ReplacePattern[] DialectPatterns = [];

        [DataMember] public Pattern[] TagPatternDiscards { get; set; }
        [DataMember] public ReplacePattern[] TagPatternReplaces { get; set; }
        [DataMember] public Pattern[] TagPatternFDAs { get; set; }
        [DataMember] public Pattern[] FlowPatternDiscards { get; set; }
        [DataMember] public Pattern[] DevicePatternDiscards { get; set; }
        [DataMember] public Pattern[] ActionPatternDiscards { get; set; }
        [DataMember] public ReplacePattern[] FlowPatternReplaces { get; set; }
        [DataMember] public ReplacePattern[] DevicePatternReplaces { get; set; }
        [DataMember] public ReplacePattern[] ActionPatternReplaces { get; set; }
        [DataMember] public string[] VisibleColumns { get; set; }
        [DataMember] public string DataDir { get; set; }
        [DataMember] public string PrimaryCsv { get; set; }
        [OnDeserialized]
        public void OnDeserializedMethod(StreamingContext context)
        {
            DialectPatterns =
                Dialects.SelectMany( (ds, i) =>
                {
                    var std = ds[0];
                    return ds.Skip(1).Select((d, j) => ReplacePattern.Create($"Dialect{i}_{j}", d, std));
                }).ToArray()
                ;
        }

    }

    [DataContract]
    public class Pattern
    {
        [DataMember] public string Name { get; set; } = "";
        [DataMember] public string PatternString { get; set; } = "";
        [DataMember] public string Description { get; set; } = "";
        [JsonIgnore] public Regex RegexPattern { get; set; }
        public void OnDeserialized()
        {
            if (PatternString.NonNullAny())
                RegexPattern = new Regex(PatternString, RegexOptions.Compiled);
        }
        [OnDeserialized]
        public void OnDeserializedMethod(StreamingContext context) => OnDeserialized();
    }

    [DataContract]
    public class ReplacePattern : Pattern
    {
        [DataMember] public string Replacement { get; set; } = "";
        public static ReplacePattern FromPattern(Pattern p)
        {
            if (p is ReplacePattern rp)
                return rp;

            return ReplacePattern.Create(p.Name, p.PatternString, replace:"", p.Description);
        }
        public static ReplacePattern Create(string name, string pattern, string replace, string desc = null)
        {
            var rp = new ReplacePattern { Name = name, PatternString = pattern, Description = desc, Replacement = replace };
            rp.OnDeserialized();
            return rp;
        }
    }

}
