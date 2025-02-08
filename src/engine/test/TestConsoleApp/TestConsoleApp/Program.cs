
using Dual.Common.Base.CS;

using Newtonsoft.Json;

using System.Diagnostics;
using Dual.Ev2;
using static Dual.Ev2.Core;
using static Dual.Ev2.Ev2CSharpExtensions;
using Dual.Common.Base.FS;


class Program
{
    [STAThread] // STAThread 속성 지정
    static void Main()
    {
        DsSystem system = DsSystem.Create("system1");
        var flow1 = system.CreateFlow("flow1");
        var (work1, vWork1) = flow1.CsAddWork("work1");
        var (call1, call2) = (new DsAction("call1"), new DsAction("call2"));
        GuidVertex vCall1 = work1.AddVertex(call1);
        GuidVertex vCall2 = work1.AddVertex(call2);
        var (work2, vWork2) = flow1.CsAddWork("work2");
        var (call21, call22) = (new DsAction("call21"), new DsAction("call22"));
        var vCall21 = work2.AddVertex(call21);
        var vCall22 = work2.AddVertex(call22);

        //var op1 = flow1.AddVertex(new DsOperator("FlowOp1"));
        //var cmd1 = work1.AddVertex(new DsOperator("WorkCmd1"));

        //work1.CsCreateEdge(call1, call2, CausalEdgeType.Start);


        //var settings = new JsonSerializerSettings() { ReferenceLoopHandling = ReferenceLoopHandling.Ignore };
        //var json = JsonConvert.SerializeObject(system, Formatting.Indented, settings);

        var json = system.CsSerialize();
        Trace.WriteLine($"JSON:\r\n{json}");

        string text = DcClipboard.Read();
        DcClipboard.Write(json);

        Console.WriteLine("Hello, World!");
    }
}

