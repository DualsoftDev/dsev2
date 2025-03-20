using System.Runtime.Serialization;

namespace Plc2DsApp
{
    [DataContract]
    public class AppSettings
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
        [OnDeserialized]
        public void OnDeserializedMethod(StreamingContext context)
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

    }

    public class AppRegistry
    {
        string _lastRead;
        string _lastWrite;
        Vendor _vendor;
        public string LastRead
        {
            get => _lastRead;
            set
            {
                _lastRead = value;
                File.WriteAllText("lastFile.json", EmJson.ToJson(this));
            }
        }
        public string LastWrite
        {
            get => _lastWrite;
            set
            {
                _lastWrite = value;
                File.WriteAllText("lastFile.json", EmJson.ToJson(this));
            }
        }
        public Vendor Vendor
        {
            get => _vendor;
            set
            {
                _vendor = value;
                File.WriteAllText("lastFile.json", EmJson.ToJson(this));
            }
        }
    }
}
