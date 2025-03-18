using System.Runtime.Serialization;

namespace Plc2DsApp
{
    public class AppSettings
    {
        public SemanticSettings Semantics { get; set; }
        public Pattern[] TagPatternDiscards { get; set; }
        public ReplacePattern[] TagPatternReplaces { get; set; }
        public Pattern[] TagPatternFDAs { get; set; }
        public Pattern[] FlowPatternDiscards { get; set; }
        public Pattern[] DevicePatternDiscards { get; set; }
        public Pattern[] ActionPatternDiscards { get; set; }
        public ReplacePattern[] FlowPatternReplaces { get; set; }
        public ReplacePattern[] DevicePatternReplaces { get; set; }
        public ReplacePattern[] ActionPatternReplaces { get; set; }
        public string[] VisibleColumns { get; set; }
        public string DataDir { get; set; }
        public string PrimaryCsv { get; set; }
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
