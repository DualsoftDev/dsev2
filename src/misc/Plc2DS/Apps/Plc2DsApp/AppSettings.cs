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

    public class Pattern
    {
        public string Name { get; set; }
        public string PatternString { get; set; }
        public string Description { get; set; }
    }

    public class ReplacePattern : Pattern
    {
        public string ReplacePatternString { get; set; }
    }

}
