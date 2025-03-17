

namespace Plc2DsApp
{
    public partial class FormMain : DevExpress.XtraEditors.XtraForm {
        public static FormMain Instance { get; private set; }
        public SemanticSettings Semantic = null;

        public PlcTagBaseFDA[] TagsAll = [];


        PlcTagBaseFDA[] selectTags(Choice cat)
        {
            if (TagsAll.IsNullOrEmpty())
                TagsAll = loadTags(tbCsvFile.Text);
            return TagsAll.Where(t => t.Choice == cat).ToArray();
        }
        public PlcTagBaseFDA[] TagsDiscarded => selectTags(Choice.Discarded);
        public PlcTagBaseFDA[] TagsFixed => selectTags(Choice.Fixed);
        public PlcTagBaseFDA[] TagsNotYet => selectTags(Choice.Undefined);
        FormGridTags showTags(PlcTagBaseFDA[] tags, string selectionColumnCaption=null) => FormGridTags.ShowTags(tags, selectionColumnCaption: selectionColumnCaption);


        const string dataDir = @"Z:\dsev2\src\misc\Plc2DS\unit-test\Plc2DS.UnitTest\Samples\LS\Autoland광명2";
        public FormMain() {
            InitializeComponent();

            Instance = this;
            Dual.Plc2DS.ModuleInitializer.Initialize();
            DcLogger.EnableTrace = false;

            var text = File.ReadAllText("appsettings.json");
            Semantic = EmJson.FromJson<SemanticSettings>(File.ReadAllText("appsettings.json"));
            tbCsvFile.Text = Path.Combine(dataDir, "BB 메인제어반.csv");

            btnDiscardTags.ToolTip = "Tag 이름에 대한 패턴을 찾아서 Discard 합니다.";
            //btnAcceptTags.ToolTip = "Tag 이름에 대한 패턴을 찾아서 Accept 합니다.";
        }
        private void FormMain_Load(object sender, EventArgs e)
        {
            //loadTags(tbCsvFile.Text);
            btnShowAllTags      .Click += (s, e) => showTags(TagsAll);
            btnShowNotyetTags   .Click += (s, e) => showTags(selectTags(Choice.Undefined));
            btnShowFixedTags    .Click += (s, e) => showTags(selectTags(Choice.Fixed));
            btnShowDiscardedTags.Click += (s, e) =>
            {
                FormGridTags form = showTags(selectTags(Choice.Discarded), selectionColumnCaption: "Resurrect");
                if (form.DialogResult == DialogResult.OK && form.SelectedTags.Any())
                {
                    form.SelectedTags.Iter(t => t.Choice = Choice.Undefined);
                    updateUI();
                }
            };

            btnReadCsvFile.Click += (s, e) => loadTags(tbCsvFile.Text);
        }

        PlcTagBaseFDA[] loadTags(string csvFile)
        {
            using var wf = DcWaitForm.CreateWaitForm("Loading tags...");
            TagsAll =
                CsvReader.ReadLs(csvFile)
                .Where(t => t.DataType.ToUpper() == "BOOL")
                .Where(t => t.Scope == "GlobalVariable")
                .ToArray();
            TagsAll.Iter(t => {
                t.CsSetFDA(t.CsTryGetFDA(Semantic));
                t.Choice = Choice.Undefined;
            });

            updateUI();
            return TagsAll;
        }

        void updateUI()
        {
            tbNumTagsAll      .Text = TagsAll      .Length.ToString();
            tbNumTagsDiscarded.Text = TagsDiscarded.Length.ToString();
            tbNumTagsFixed    .Text = TagsFixed    .Length.ToString();
            tbNumTagsNotyet   .Text = TagsNotYet   .Length.ToString();
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
                , new Pattern { Name = "'_' 로 시작", PatternString = @"^_",         Description = "'_' 로 시작하는 항목" }
                , new Pattern { Name = "'[숫자]' indexing", PatternString = @"\[\d+\]$",         Description = "'[' 와 ']' 사이에 숫자로 끝나는 항목" }
                , new Pattern { Name = "'_CN_숫자' 로 마감",  PatternString = @"_CN_\d+$",          Description = "'_CN_' 와 숫자로 끝나는 항목" }
                , new Pattern { Name = "'_ECHO_숫자' 로 마감",  PatternString = @"_ECHO_\d+$",          Description = "'_ECHO_' 와 숫자로 끝나는 항목" }
                , new Pattern { Name = "'_(SELECT|CTYPE)_숫자' 로 마감",  PatternString = @"_(SELECT|CTYPE)_\d+$",          Description = "'_SELECT_' 혹은 '_CTYPE_' 와 숫자로 끝나는 항목" }
                , new Pattern { Name = "'_BT_' 포함",  PatternString = @"_BT(\d)*_", Description = "'_BT_' 혹은 '_BT숫자_' 포함하는 항목" }
                , new Pattern { Name = "'_OPT_' 포함",  PatternString = @"_OPT(\d)*_", Description = "'_OPT_' 혹은 '_OPT숫자_' 포함하는 항목" }
                , new Pattern { Name = "'_BOLTING_ERR_' 포함",  PatternString = @"_BOLTING_ERR(\d)*_", Description = "'_BOLTING_ERR_' 혹은 '_BOLTING_ERR숫자_' 포함하는 항목" }
                , new Pattern { Name = "'_COMM_ERR' 로 마감",  PatternString = @"_COMM_ERR$", Description = "'_COMM_ERR' 로 끝나는 항목" }
                , new Pattern { Name = "'_CARR_TYPE_[A-Z]' 로 마감",  PatternString = @"_C(AR(R)?)?(_)?TYPE_[A-Z]$", Description = "CARR_TYPE_, CTYPE_ 뒤에 alphabet 으로 끝나는 항목. e.g '_CTYPE_E'" }
                , new Pattern { Name = "'_CARR_NO_숫자' 로 마감",  PatternString = @"_CARR_NO_\d+$", Description = "CARR_NO_' 뒤에 숫자로 끝나는 항목. e.g '_CARR_NO_16'" }
                , new Pattern { Name = "'_SERVO' 포함",  PatternString = @"_SERVO\.", Description = "'_SERVO.' 을 포함하는 항목" }
                , new Pattern { Name = "'_SRV_' 포함",  PatternString = @"_(SRV\d*|SERVO\d*)_", Description = "'_SRV_' 또는 '_SERVO_' 을 포함하는 항목" }
                , new Pattern { Name = "'_BOLTING_ERR_' 포함",  PatternString = @"__BOLTING_ERR__", Description = "'_BOLTING_ERR_' 을 포함하는 항목" }
                , new Pattern { Name = "'_MANUAL_INT' 포함",  PatternString = @"_(MANUAL|MUTUAL)_INT\d*", Description = "'_MANUAL_INT' 을 포함하는 항목" }
            };
            var form = new FormPattern(TagsAll, patterns);
            if (form.ShowDialog() == DialogResult.OK)
            {
                form.TagsChosen.Iter(t => t.Choice = Choice.Discarded);
                updateUI();
            }
        }

        private void btnExtractFDA_Click(object sender, EventArgs e)
        {
            Pattern[] patterns = new Pattern[] {
                  new Pattern { Name = "ROBOT 패턴",      PatternString = @"^(?<flow>[^_]+)_([IQM]_)?(?<device>(RB|RBT|ROBOT)\d+)_(?<action>.*)$", Description = "e.g S301_I_RB2_1ST_WORK_COMP => {S301, RB2, 1ST_WORK_COMP} 로 분리" }
                //, new Pattern { Name = "'_' 로 시작", PatternString = @"^_",         Description = "'_' 로 시작하는 항목" }
            };

            var form = new FormExtractFDA(TagsNotYet, patterns);
            form.ShowDialog();
        }
    }
}

