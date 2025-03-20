using System.Reflection;
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

        public static ReplacePattern Create(string name, Regex pattern, string replace, string desc = null)
            => new ReplacePattern { Name = name, RegexPattern = pattern, PatternString = pattern.ToString(), Description = desc, Replacement = replace };

    }



    [DataContract]
    public class CsvFilterPattern : Pattern
    {
        /// <summary>
        /// 패턴 매치를 적용할 PlcTagBaseFDA 의 Filed 이름
        /// </summary>
        [DataMember] public string Field { get; set; }

        /// <summary>
        /// <br/> Include == true 이면
        ///     <br/> - match 되면 keep
        ///     <br/> - match 안되면 discard
        /// <br/> Include == false 이면
        ///     <br/> - match 되면 discard
        ///     <br/> - match 안되면 keep
        /// </summary>
        [DataMember] public bool Include { get; set; }

    }


    public static class PatternExtension
    {
        public static bool? IsInclude(this CsvFilterPattern pattern, PlcTagBaseFDA tag)
        {
            // tag 객체로부터 reflection 을 이용해서 Filed 이름의 값을 가져온다.
            PropertyInfo propertyInfo = tag.GetType().GetProperty(pattern.Field);
            if (propertyInfo == null)
                return null;

            string value = propertyInfo.GetValue(tag).ToString();
            if (value.IsNullOrEmpty())
                return null;

            var match = pattern.RegexPattern.Match(value);
            return pattern.Include ? match.Success : !match.Success;
        }
        public static bool? IsExclude(this CsvFilterPattern pattern, PlcTagBaseFDA tag) => !pattern.IsInclude(tag);
    }
}
