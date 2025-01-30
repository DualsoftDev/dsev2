
using Dual.Common.Base.CS;

using Newtonsoft.Json;

using System.Diagnostics;
using Dual.Ev2;
using static Dual.Ev2.Core;
using static Dual.Ev2.Ev2CSharpExtensions;


class Program
{
    [STAThread] // STAThread 속성 지정
    static void Main()
    {
        DsSystem system = DsSystem.Create("system1");
        var flow1 = system.CreateFlow("flow1");
        var work1 = flow1.CreateWork("work1");
        var call1 = work1.AddVertex(new DsAction("call1"));
        var call2 = work1.AddVertex(new DsAction("call2"));
        var work2 = flow1.CreateWork("work2");
        var call21 = work2.AddVertex(new DsAction("call21"));
        var call22 = work2.AddVertex(new DsAction("call22"));

        var op1 = flow1.AddVertex(new DsOperator("FlowOp1"));
        var cmd1 = work1.AddVertex(new DsOperator("WorkCmd1"));

        work1.CsCreateEdge(call1, call2, CausalEdgeType.Start);


        //var settings = new JsonSerializerSettings() { ReferenceLoopHandling = ReferenceLoopHandling.Ignore };
        //var json = JsonConvert.SerializeObject(system, Formatting.Indented, settings);

        var json = system.CsSerialize();
        Trace.WriteLine($"JSON:\r\n{json}");

        string text = DcClipboard.Read();
        DcClipboard.Write(json);

        Console.WriteLine("Hello, World!");
    }
}

