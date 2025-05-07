
using Dual.Common.Base;

using Newtonsoft.Json;

using System.Diagnostics;
using Dual.Ev2;
using static Dual.Ev2.Core;
using static Dual.Ev2.Ev2CSharpExtensions;
using static Dual.Ev2.Interfaces;


class Program
{
    [STAThread] // STAThread 속성 지정
    static void Main()
    {
        Dual.Ev2.ModuleInitializer.Initialize();


        DsSystem system = new DsSystem("system1");
        var flow1 = system.CsCreateFlow("flow1");
        var (work1, vWork1) = flow1.CsAddWork("work1");
        var (call1, vCall1) = work1.CsAddAction("call1");
        var (call2, vCall2) = work1.CsAddAction("call2");
        var (work2, vWork2) = flow1.CsAddWork("work2");
        var (call21, vCall21) = work2.CsAddAction("call21");
        var (call22, vCall22) = work2.CsAddAction("call22");

        DeviceModule.testMe();

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

