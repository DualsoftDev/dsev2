using Dual.Common.Base.FS;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using static Dual.Plc2DS.AppSettingsModule;

namespace Plc2DsApp
{
    public partial class FormMain : DevExpress.XtraEditors.XtraForm {
        SemanticSettings semanticSettings = null;
        public FormMain() {
            InitializeComponent();
            var text = File.ReadAllText("appsettings.json");
            semanticSettings = EmJson.FromJson<SemanticSettings>(File.ReadAllText("appsettings.json"));
        }

        private void btnOpenCSV_Click(object sender, EventArgs e)
        {
            using OpenFileDialog ofd =
                new OpenFileDialog()
                {
                    InitialDirectory = "Z:/dsev2/src/misc/Plc2DS/unit-test/Plc2DS.UnitTest/Samples/LS/Autoland광명2",
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                };

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                //return ofd.FileName;

            }
        }
    }
}
