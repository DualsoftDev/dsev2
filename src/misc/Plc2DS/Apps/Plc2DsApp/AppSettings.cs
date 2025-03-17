using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Plc2DsApp
{
    public class AppSettings
    {
        public SemanticSettings Semantics { get; set; }
        public Pattern[] TagPatternDiscards { get; set; }
        public Pattern[] TagPatternFDAs { get; set; }
        public string[] VisibleColumns { get; set; }
        public string DataDir { get; set; }
        public string PrimaryCsv { get; set; }
    }
}
