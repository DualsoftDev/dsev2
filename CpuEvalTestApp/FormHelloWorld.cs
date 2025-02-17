using CpuEvalTest;

using Dual.Common.Base.CS;

namespace CpuEvalTestApp
{
    public partial class FormHelloWorld : Form
    {
        public FormHelloWorld()
        {
            InitializeComponent();
            btnTurnOn.Click += (s, e) => Main.addChange("var0", 1);
            btnTurnOff.Click += (s, e) => Main.addChange("var0", 0);
        }

        private async void FormHelloWorld_Load(object sender, EventArgs e)
        {
            ConsoleEx.Allocate("HelloWorld");
            await Main.scanLoopAsync();
        }
    }
}
