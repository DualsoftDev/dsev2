namespace Plc2DsApp
{
    public partial class FormMain : DevExpress.XtraEditors.XtraForm {
        AppSettings _appSettings = null;
        public Vendor Vendor { get; set; } = Vendor.LS;
        public string[] VisibleColumns => _appSettings.VisibleColumns;
        string _dataDir => _appSettings.DataDir;

        // abstract class 인 PlcTagBaseFDA 를 vendor 에 맞는 subclass type 으로 변환
        public object ConvertToVendorTags(IEnumerable<PlcTagBaseFDA> tags)
        {
            var typ = FormMain.Instance.Vendor.CsGetTagType();
            return tags.Select(t => Convert.ChangeType(t, typ)).ToArray();
        }
        public static FormMain Instance { get; private set; }
        public SemanticSettings Semantic => _appSettings.Semantics;

        public PlcTagBaseFDA[] TagsAll = [];


        PlcTagBaseFDA[] selectTags(Choice cat)
        {
            if (TagsAll.IsNullOrEmpty())
                TagsAll = loadTags(tbCsvFile.Text);
            return TagsAll.Where(t => t.Choice == cat).ToArray();
        }
        public PlcTagBaseFDA[] TagsDiscarded => selectTags(Choice.Discarded);
        public PlcTagBaseFDA[] TagsChosen => selectTags(Choice.Chosen);
        public PlcTagBaseFDA[] TagsCategorized => selectTags(Choice.Categorized);
        public PlcTagBaseFDA[] TagsStage => selectTags(Choice.Stage);
        FormTags showTags(PlcTagBaseFDA[] tags, string selectionColumnCaption=null, string usageHint=null) =>
            FormTags.ShowTags(tags, selectionColumnCaption: selectionColumnCaption, usageHint: usageHint);


        public FormMain() {
            InitializeComponent();

            _appSettings = EmJson.FromJson<AppSettings>(File.ReadAllText("appsettings.json"));

            Instance = this;
            Dual.Plc2DS.ModuleInitializer.Initialize();
            DcLogger.EnableTrace = false;

            tbCsvFile.Text = Path.Combine(_dataDir, _appSettings.PrimaryCsv);

            btnDiscardTags.ToolTip = "Tag 이름에 대한 패턴을 찾아서 Discard 합니다.";
            //btnAcceptTags.ToolTip = "Tag 이름에 대한 패턴을 찾아서 Accept 합니다.";

            ucRadioSelector1.SetOptions(new string[] { "LS", "AB", "S7", "MX" });
            ucRadioSelector1.SelectedOptionChanged += (s, e) =>
            {
                Vendor = e switch
                {
                    "LS" => Vendor.LS,
                    "AB" => Vendor.AB,
                    "S7" => Vendor.S7,
                    "MX" => Vendor.MX,
                    _ => throw new NotImplementedException()
                };
            };
        }
        private void FormMain_Load(object sender, EventArgs e)
        {
            //loadTags(tbCsvFile.Text);
            btnShowAllTags        .Click += (s, e) => showTags(TagsAll);
            btnShowStageTags      .Click += (s, e) => showTags(selectTags(Choice.Stage));
            btnShowChosenTags     .Click += (s, e) => showTags(selectTags(Choice.Chosen));
            btnShowCategorizedTags.Click += (s, e) => showTags(selectTags(Choice.Categorized));
            btnShowDiscardedTags  .Click += (s, e) =>
            {
                FormTags form = showTags(selectTags(Choice.Discarded), selectionColumnCaption: "Resurrect");
                if (form.DialogResult == DialogResult.OK && form.SelectedTags.Any())
                {
                    form.SelectedTags.Iter(t => t.Choice = Choice.Stage);
                    updateUI();
                }
            };

            btnReadCsvFile.Click += (s, e) => loadTags(tbCsvFile.Text);
        }

        PlcTagBaseFDA[] loadTags(string csvFile)
        {
            using var wf = DcWaitForm.CreateWaitForm("Loading tags...");
            if (Vendor.IsLS)
            {
                TagsAll =
                    CsvReader.ReadLs(csvFile)
                    .Where(t => t.DataType.ToUpper() == "BOOL")
                    .Where(t => t.Scope == "GlobalVariable")
                    .ToArray();
            }
            else if (Vendor.IsAB)
                TagsAll = CsvReader.ReadAb(csvFile).ToArray();
            else if (Vendor.IsS7)
                TagsAll = CsvReader.ReadS7(csvFile).ToArray();
            else if (Vendor.IsMX)
                TagsAll = CsvReader.ReadMx(csvFile).ToArray();

            TagsAll.Iter(t => {
                t.CsSetFDA(t.CsTryGetFDA(Semantic));
                t.Choice = Choice.Stage;
            });

            updateUI();
            return TagsAll;
        }

        void updateUI()
        {
            tbNumTagsAll        .Text = TagsAll        .Length.ToString();
            tbNumTagsDiscarded  .Text = TagsDiscarded  .Length.ToString();
            tbNumTagsChosen     .Text = TagsChosen     .Length.ToString();
            tbNumTagsCategorized.Text = TagsCategorized.Length.ToString();
            tbNumTagsStage      .Text = TagsStage      .Length.ToString();
        }

        private void btnSelectCSV_Click(object sender, EventArgs e)
        {
            using OpenFileDialog ofd =
                new OpenFileDialog()
                {
                    InitialDirectory = _dataDir,
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
            Pattern[] patterns = _appSettings.TagPatternDiscards;
            var json = EmJson.ToJson(patterns);

            var _ = selectTags(Choice.Stage);   // load TagsAll if null or empty
            var form = new FormPattern(TagsAll, patterns);
            if (form.ShowDialog() == DialogResult.OK)
            {
                form.TagsChosen.Iter(t => t.Choice = Choice.Discarded);
                updateUI();
            }
        }

        private void btnExtractFDA_Click(object sender, EventArgs e)
        {
            Pattern[] patterns = _appSettings.TagPatternFDAs;
            var form = new FormExtractFDA(TagsStage, patterns);
            if (form.ShowDialog() == DialogResult.OK)
            {
                form.TagsStage.Iter(t => t.Choice = Choice.Chosen);
                updateUI();
            }
        }
    }
}

