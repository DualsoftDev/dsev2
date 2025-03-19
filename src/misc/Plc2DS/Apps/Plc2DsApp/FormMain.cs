using DevExpress.XtraEditors;

namespace Plc2DsApp
{
    public partial class FormMain : DevExpress.XtraEditors.XtraForm {
        AppSettings _appSettings = null;
        public Vendor Vendor { get; set; } = Vendor.LS;
        public string[] VisibleColumns => _appSettings.VisibleColumns;
        public string DataDir => _appSettings.DataDir;

        /// <summary>
        /// abstract class 인 PlcTagBaseFDA 를 vendor 에 맞는 subclass type 으로 변환
        /// </summary>
        public object ConvertToVendorTags(IEnumerable<PlcTagBaseFDA> tags)
        {
            var typ = FormMain.Instance.Vendor.CsGetTagType();
            return tags.Select(t => Convert.ChangeType(t, typ)).ToArray();
        }
        public static FormMain Instance { get; private set; }

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
        FormTags showTags(PlcTagBaseFDA[] tags, string selectionColumnCaption=null, string usageHint=null)
        {
            var form = new FormTags(tags, selectionColumnCaption: selectionColumnCaption, usageHint: usageHint);
            form.ShowDialog();
            return form;
        }


        public FormMain() {
            InitializeComponent();

            _appSettings = EmJson.FromJson<AppSettings>(File.ReadAllText("appsettings.json"));

            Instance = this;
            Dual.Plc2DS.ModuleInitializer.Initialize();
            DcLogger.EnableTrace = false;

            tbCsvFile.Text = Path.Combine(DataDir, _appSettings.PrimaryCsv);

            btnDiscardTags.ToolTip = "Tag 이름에 대한 패턴을 찾아서 Discard 합니다.";
            //btnAcceptTags.ToolTip = "Tag 이름에 대한 패턴을 찾아서 Accept 합니다.";

            ucRadioSelector1.SetOptions(new string[] { "LS", "AB", "S7", "MX" }, itemLayout:RadioGroupItemsLayout.Flow);
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

            btnDiscardFlowName  .Enabled = _appSettings.FlowPatternDiscards.Any();
            btnDiscardDeviceName.Enabled = _appSettings.DevicePatternDiscards.Any();
            btnDiscardActionName.Enabled = _appSettings.ActionPatternDiscards.Any();

            btnDiscardFlowName  .Click += (s, e) => discardFDA(_appSettings.FlowPatternDiscards, FDAT.DuFlow);
            btnDiscardDeviceName.Click += (s, e) => discardFDA(_appSettings.DevicePatternDiscards, FDAT.DuDevice);
            btnDiscardActionName.Click += (s, e) => discardFDA(_appSettings.ActionPatternDiscards, FDAT.DuAction);
        }

        void discardFDA(Pattern[] pattern, FDAT fdat, bool withUI = true) => discardFDA(TagsCategorized.Concat(TagsChosen).ToArray(), pattern, fdat, withUI);
        void discardFDA(PlcTagBaseFDA[] tags, Pattern[] pattern, FDAT fdat, bool withUI=true)
        {
            Func<PlcTagBaseFDA, string> fdatGetter =
                fdat switch
                {
                    _ when fdat.IsDuFlow   => t => t.FlowName,
                    _ when fdat.IsDuDevice => t => t.DeviceName,
                    _ when fdat.IsDuAction => t => t.ActionName,
                    _ when fdat.IsDuTag    => t => t.CsGetName(),
                    _ => throw new NotImplementedException()
                };
            Action< PlcTagBaseFDA, string> fdatSetter =
                fdat switch
                {
                    _ when fdat.IsDuFlow   => (t, v) => t.FlowName = v,
                    _ when fdat.IsDuDevice => (t, v) => t.DeviceName = v,
                    _ when fdat.IsDuAction => (t, v) => t.ActionName = v,
                    _ when fdat.IsDuTag    => (t, v) => t.CsSetName(v),
                    _ => throw new NotImplementedException()
                };
            if (withUI)
            {
                var form = new FormReplaceFDAT(tags, pattern, fdatGetter, fdatSetter);
                form.ShowDialog();
            }
            else
                FormReplaceFDAT.ApplyPattern(tags, pattern, fdatGetter, fdatSetter);
        }

        public void SaveTagsAs(IEnumerable<PlcTagBaseFDA> tags)
        {
            using SaveFileDialog sfd =
                new SaveFileDialog()
                {
                    InitialDirectory = FormMain.Instance.DataDir,
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                string json = EmJson.ToJson(tags);
                File.WriteAllText(sfd.FileName, json);
            }

        }

        PlcTagBaseFDA[] loadTags(string csvFile)
        {
            using var wf = DcWaitForm.CreateWaitForm("Loading tags...");
            var ext = Path.GetExtension(csvFile).ToLower();
            if ( ext == ".csv")
            {
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
            }
            else if (ext == ".json")
            {
                var json = File.ReadAllText(csvFile);
                if      (Vendor.IsLS) TagsAll = EmJson.FromJson<LS.PlcTagInfo[]>(json);
                else if (Vendor.IsAB) TagsAll = EmJson.FromJson<AB.PlcTagInfo[]>(json);
                else if (Vendor.IsS7) TagsAll = EmJson.FromJson<S7.PlcTagInfo[]>(json);
                else if (Vendor.IsMX) TagsAll = EmJson.FromJson<MX.PlcTagInfo[]>(json);
                else throw new Exception("ERROR");
            }
            else
                throw new NotImplementedException();

            var fdaSplitPattern = new Regex(_appSettings.FDASplitPattern, RegexOptions.Compiled);
            TagsAll.Iter(t =>
                {
                    t.CsSetFDA(t.CsTryGetFDA([fdaSplitPattern]));
                    t.Choice = Choice.Stage;
                });

            var invalidTags = TagsAll.Where(t => !t.CsIsValid()).ToArray();
            if (invalidTags.Any())
            {
                var f = invalidTags.First();
                var msg = $"Total {invalidTags.Length} invalid tags: {f.CsGetName()}...\r\nContinue?";
                if (DialogResult.No == MessageBox.Show(msg, "ERROR", MessageBoxButtons.YesNo))
                    return [];
            }


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
                    InitialDirectory = DataDir,
                    Filter = "CSV files (*.csv)|*.csv|JSON files (*.json)|*.json|All files (*.*)|*.*",
                };

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                tbCsvFile.Text = ofd.FileName;
                loadTags(tbCsvFile.Text);
            }
        }

        void applyDiscardTags(bool withUI=true)
        {
            Pattern[] patterns = _appSettings.TagPatternDiscards;
            var json = EmJson.ToJson(patterns);

            var _ = selectTags(Choice.Stage);   // load TagsAll if null or empty

            PlcTagBaseFDA[] chosen = [];
            if (withUI)
            {
                var form = new FormPattern(TagsAll, patterns);
                if (form.ShowDialog() == DialogResult.OK)
                    chosen = form.TagsChosen;
            }
            else
                chosen = FormPattern.ApplyPatterns(TagsAll, patterns);

            chosen.Iter(t => t.Choice = Choice.Discarded);
            updateUI();
        }
        void btnDiscardTags_Click(object sender, EventArgs e) => applyDiscardTags();
        void btnReplaceTags_Click(object sender, EventArgs e) => applyReplaceTags();

        private void btnExtractFDA_Click(object sender, EventArgs e) => applyExtractFDA();

        void applyExtractFDA(bool withUI=true)
        {
            Pattern[] patterns = _appSettings.TagPatternFDAs;
            PlcTagBaseFDA[] chosen = [];
            if (withUI)
            {
                var form = new FormExtractFDA(TagsStage, patterns);
                if (form.ShowDialog() == DialogResult.OK)
                    chosen = form.TagsStage;
            }
            else
                chosen = FormExtractFDA.ApplyPatterns(TagsStage, patterns);

            chosen.Iter(t => t.Choice = Choice.Categorized);
            updateUI();
        }

        void applyReplaceTags(bool withUI=true)
        {
            var patterns = _appSettings.TagPatternReplaces.Concat(_appSettings.DialectPatterns).ToArray();
            discardFDA(TagsStage, patterns, FDAT.DuTag, withUI);
        }

        private void btnApplyAll_Click(object sender, EventArgs e)
        {
            bool withUI = false;
            applyDiscardTags(withUI);
            applyReplaceTags(withUI);
            applyExtractFDA(withUI);
            discardFDA(_appSettings.FlowPatternDiscards,   FDAT.DuFlow, withUI);
            discardFDA(_appSettings.DevicePatternDiscards, FDAT.DuDevice, withUI);
            discardFDA(_appSettings.ActionPatternDiscards, FDAT.DuAction, withUI);
        }

    }
}

