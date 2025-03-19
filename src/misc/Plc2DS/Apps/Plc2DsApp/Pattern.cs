using System.Runtime.Serialization;

namespace Plc2DsApp
{
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
        public static Pattern Create(string name, string pattern, string desc = null)
        {
            var rp = new ReplacePattern { Name = name, PatternString = pattern, Description = desc};
            rp.OnDeserialized();
            return rp;
        }
    }

    [DataContract]
    public class ReplacePattern : Pattern
    {
        [DataMember] public string Replacement { get; set; } = "";
        public static ReplacePattern FromPattern(Pattern p)
        {
            if (p is ReplacePattern rp)
                return rp;

            return ReplacePattern.Create(p.Name, p.PatternString, replace: "", p.Description);
        }
        public static ReplacePattern Create(string name, string pattern, string replace, string desc = null)
        {
            var rp = new ReplacePattern { Name = name, PatternString = pattern, Description = desc, Replacement = replace };
            rp.OnDeserialized();
            return rp;
        }
    }
}
