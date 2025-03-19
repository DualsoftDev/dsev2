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

    public class LastFileInfo
    {
        string _read;
        string _write;
        public string Read
        {
            get => _read;
            set
            {
                _read = value;
                File.WriteAllText("lastFile.json", EmJson.ToJson(this));
            }
        }
        public string Write
        {
            get => _write;
            set
            {
                _write = value;
                File.WriteAllText("lastFile.json", EmJson.ToJson(this));
            }
        }
    }
}
