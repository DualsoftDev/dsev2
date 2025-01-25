
using Dual.Common.Base.CS;
using Dual.Ev2;

using Newtonsoft.Json;

using System.Diagnostics;
//using System.Windows.Forms;


class Program
{
    [STAThread] // STAThread 속성 지정
    static void Main()
    {
        DsSystem system = DsSystem.Create("system1");
        var flow = system.CreateFlow("flow1");
        var work = flow.CreateWork("work1");
        var call = work.CreateCall("call1");


        var settings = new JsonSerializerSettings() { ReferenceLoopHandling = ReferenceLoopHandling.Ignore };
        var json = JsonConvert.SerializeObject(system, Formatting.Indented, settings);
        Trace.WriteLine($"JSON:\r\n{json}");

        string text = DcClipboard.Read();
        DcClipboard.Write(json);

        Console.WriteLine("Hello, World!");
    }
}

