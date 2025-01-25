
using Dual.Common.Base.CS;
using static Dual.Ev2.Interfaces;
using static Dual.Ev2.Core;
//using static Dual.Ev2.FS.DualEv2;

using Newtonsoft.Json;

using System.Diagnostics;
//using System.Windows.Forms;


class Program
{
    [STAThread] // STAThread 속성 지정
    static void Main()
    {
        DsSystem system = DsSystem.Create("system1");
        var flow1 = system.CreateFlow("flow1");
        var work1 = flow1.CreateWork("work1");
        var call1 = work1.CreateCall("call1");
        var call2 = work1.CreateCall("call2");
        var work2 = flow1.CreateWork("work2");
        var call21 = work2.CreateCall("call21");
        var call22 = work2.CreateCall("call22");

        var op1 = flow1.CreateOperator("FlowOp1");
        var cmd1 = work1.CreateOperator("WorkCmd1");


        var settings = new JsonSerializerSettings() { ReferenceLoopHandling = ReferenceLoopHandling.Ignore };
        var json = JsonConvert.SerializeObject(system, Formatting.Indented, settings);
        Trace.WriteLine($"JSON:\r\n{json}");

        string text = DcClipboard.Read();
        DcClipboard.Write(json);

        Console.WriteLine("Hello, World!");
    }
}

