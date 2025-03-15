
using System.Text.RegularExpressions;

using static Dual.Plc2DS.InterfaceModule;

namespace Plc2DsApp
{
    public partial class FormMain : DevExpress.XtraEditors.XtraForm {
        SemanticSettings sm = null;

        LS.PlcTagInfo[] tags = [];


        LS.PlcTagInfo[] selectTags(Choice cat)
        {
            if (tags.IsNullOrEmpty())
                tags = loadTags(tbCsvFile.Text);
            return tags.Where(t => t.Choice == cat).ToArray();
        }
        LS.PlcTagInfo[] tagsDiscarded => selectTags(Choice.Discarded);
        LS.PlcTagInfo[] tagsFixed => selectTags(Choice.Fixed);
        LS.PlcTagInfo[] tagsNotYet => selectTags(Choice.Undefined);
        void showTags(LS.PlcTagInfo[] tags) => FormGridTags.ShowTags(tags);


        const string dataDir = @"Z:\dsev2\src\misc\Plc2DS\unit-test\Plc2DS.UnitTest\Samples\LS\Autoland광명2";
        public FormMain() {
            InitializeComponent();

            Dual.Plc2DS.ModuleInitializer.Initialize();
            DcLogger.EnableTrace = false;

            var text = File.ReadAllText("appsettings.json");
            sm = EmJson.FromJson<SemanticSettings>(File.ReadAllText("appsettings.json"));
            tbCsvFile.Text = Path.Combine(dataDir, "BB 메인제어반.csv");

            btnDiscardTags.ToolTip = "Tag 이름에 대한 패턴을 찾아서 Discard 합니다.";
            btnAcceptTags.ToolTip = "Tag 이름에 대한 패턴을 찾아서 Accept 합니다.";
        }
        private void FormMain_Load(object sender, EventArgs e)
        {
            //loadTags(tbCsvFile.Text);
            btnShowAllTags      .Click += (s, e) => showTags(tags);
            btnShowNotyetTags   .Click += (s, e) => showTags(selectTags(Choice.Undefined));
            btnShowFixedTags    .Click += (s, e) => showTags(selectTags(Choice.Fixed));
            btnShowDiscardedTags.Click += (s, e) => showTags(selectTags(Choice.Discarded));

            btnReadCsvFile      .Click += (s, e) => loadTags(tbCsvFile.Text);
        }

        LS.PlcTagInfo[] loadTags(string csvFile)
        {
            using var wf = DcWaitForm.CreateWaitForm("Loading tags...");
            tags = CsvReader.CsRead(Vendor.LS, csvFile).Cast<LS.PlcTagInfo>().ToArray();
            tags.Iter(t => {
                t.CsSetFDA(t.CsTryGetFDA(sm));
                t.Choice = Choice.Undefined;
            });

            updateUI();
            return tags;
        }

        void updateUI()
        {
            tbNumTagsAll.Text = tags.Length.ToString();
            tbNumTagsDiscarded.Text = tagsDiscarded.Length.ToString();
            tbNumTagsFixed.Text = tagsFixed.Length.ToString();
            tbNumTagsNotyet.Text = tagsNotYet.Length.ToString();
        }

        private void btnSelectCSV_Click(object sender, EventArgs e)
        {
            using OpenFileDialog ofd =
                new OpenFileDialog()
                {
                    InitialDirectory = dataDir,
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                };

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                tbCsvFile.Text = ofd.FileName;
                loadTags(tbCsvFile.Text);
            }
        }

        void btnDiscardTags_Click(object sender, EventArgs e)
        {
            Pattern[] patterns = new Pattern[] {
                  new Pattern { Name = "'_' 갯수 미달",      PatternString = "^[^_]+(_[^_]+)?$", Description = "'_'구분자가 없거나, 하나만 존재하는 항목" }
                , new Pattern { Name = "'[숫자]' indexing", PatternString = @"\[\d+\]$",         Description = "'[' 와 ']' 사이에 숫자로 끝나는 항목" }
                , new Pattern { Name = "'CN_숫자' 로 마감",  PatternString = @"_CN\d+$",          Description = "'CN_' 와 숫자로 끝나는 항목" }
                , new Pattern { Name = "'BT_숫자' 로 마감",  PatternString = @"_BT_(\d+M|CHANGE|NORMAL|AS|EMPTY|EMPTY_CRR|NG_BODY|NG_CRR|OUT_CRR|STOCK)$", Description = "'BT_' 와 숫자, 혹은 M, CHANGE, NORMAL, .. 등으로 끝나는 항목" }
            };
            var form = new FormPattern(tags, patterns);
            if (form.ShowDialog() == DialogResult.OK)
            {
                form.TagsChosen.Iter(t => t.Choice = Choice.Discarded);
                updateUI();
            }
        }

        private void btnAcceptTags_Click(object sender, EventArgs e)
        {

        }
    }
}