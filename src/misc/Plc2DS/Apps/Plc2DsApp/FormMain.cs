using DevExpress.XtraEditors;

using log4net.Appender;
using log4net.Core;

namespace Plc2DsApp
{
    public partial class FormMain : DevExpress.XtraEditors.XtraForm, IAppender
    {
        AppSettings _appSettings = null;
        public Vendor Vendor { get; set; } = Vendor.LS;
        public string[] VisibleColumns => _appSettings.VisibleColumns;

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

        LastFileInfo _lastFileInfo = new LastFileInfo();
        public FormMain() {
            InitializeComponent();

            _appSettings = EmJson.FromJson<AppSettings>(File.ReadAllText("appsettings.json"));
            if (File.Exists("lastFile.json"))
                _lastFileInfo = EmJson.FromJson<LastFileInfo>(File.ReadAllText("lastFile.json"));

            Instance = this;
            Dual.Plc2DS.ModuleInitializer.Initialize();
            DcLogger.EnableTrace = false;

            tbCsvFile.Text = _lastFileInfo.Read;

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
        void FormMain_Load(object sender, EventArgs e)
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

            btnReplaceFlowName  .Enabled = _appSettings.FlowPatternReplaces.Any();
            btnReplaceDeviceName.Enabled = _appSettings.DevicePatternReplaces.Any();
            btnReplaceActionName.Enabled = _appSettings.ActionPatternReplaces.Any();

            btnReplaceFlowName  .Click += (s, e) => replaceFDA(_appSettings.FlowPatternReplaces,   FDAT.DuFlow);
            btnReplaceDeviceName.Click += (s, e) => replaceFDA(_appSettings.DevicePatternReplaces, FDAT.DuDevice);
            btnReplaceActionName.Click += (s, e) => replaceFDA(_appSettings.ActionPatternReplaces, FDAT.DuAction);
        }
        public void DoAppend(LoggingEvent loggingEvent)
        {
            this.Do(() => ucPanelLog1.AddLog(loggingEvent));
        }

        int replaceFDA(ReplacePattern[] pattern, FDAT fdat, bool withUI = true) => replaceFDA(TagsCategorized.Concat(TagsChosen).ToArray(), pattern, fdat, withUI);
        int replaceFDA(PlcTagBaseFDA[] tags, ReplacePattern[] pattern, FDAT fdat, bool withUI=true)
        {
            string verify(PlcTagBaseFDA tag, string category, string x)
            {
                if (x.IsNullOrEmpty())
                    Logger.Error($"Empty {category} on Tag {tag.Stringify()}");
                return x;
            }
            Func<PlcTagBaseFDA, string> fdatGetter =
                fdat switch
                {
                    _ when fdat.IsDuFlow   => t => t.FlowName   .Tee(x => verify(t, "FlowName", x)),
                    _ when fdat.IsDuDevice => t => t.DeviceName .Tee(x => verify(t, "DeviceName", x)),
                    _ when fdat.IsDuAction => t => t.ActionName .Tee(x => verify(t, "ActionName", x)),
                    _ when fdat.IsDuTag    => t => t.CsGetName().Tee(x => verify(t, "Name", x)),
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

            var form = new FormReplaceFDAT(tags, pattern, fdatGetter, fdatSetter, withUI);
            form.ShowDialog();
            return form.NumChanged;
        }

        public void SaveTagsAs(IEnumerable<PlcTagBaseFDA> tags)
        {
            using SaveFileDialog sfd =
                new SaveFileDialog()
                {
                    InitialDirectory = Path.GetDirectoryName(_lastFileInfo.Write),
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                string json = EmJson.ToJson(tags);
                File.WriteAllText(sfd.FileName, json);
                _lastFileInfo.Write = sfd.FileName;
            }

        }

        PlcTagBaseFDA[] loadTags(string csvFile)
        {
            Logger.Info($"Loading tags from {csvFile}");

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

            Logger.Info($"  Loaded tags from {csvFile}");

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

        void btnLoadTags_Click(object sender, EventArgs e)
        {
            using OpenFileDialog ofd =
                new OpenFileDialog()
                {
                    InitialDirectory = Path.GetDirectoryName(_lastFileInfo.Read),
                    Filter = "CSV files (*.csv)|*.csv|JSON files (*.json)|*.json|All files (*.*)|*.*",
                };

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                var f = ofd.FileName;
                tbCsvFile.Text = f;
                loadTags(f);
                _lastFileInfo.Read = f;
            }
        }

        int applyDiscardTags(bool withUI=true)
        {
            Pattern[] patterns = _appSettings.TagPatternDiscards;
            var _ = selectTags(Choice.Stage);   // load TagsAll if null or empty

            var form = new FormDiscardTags(TagsAll, patterns, withUI);

            if (form.ShowDialog() == DialogResult.OK)
            {
                var chosen = form.TagsChosen.ToArray();
                form.TagsChosen.Iter(t => t.Choice = Choice.Discarded);
                updateUI();
                return chosen.Length;
            }

            return 0;
        }
        void btnDiscardTags_Click(object sender, EventArgs e) => applyDiscardTags();
        void btnReplaceTags_Click(object sender, EventArgs e) => applyReplaceTags();

        void btnSplitFDA_Click(object sender, EventArgs e) => applySplitFDA();

        int applySplitFDA(bool withUI=true)
        {
            Pattern[] patterns = _appSettings.TagPatternFDAs;

            var tags = TagsStage.ToArray();
            var form = new FormSplitFDA(tags, patterns, withUI);

            if (form.ShowDialog() == DialogResult.OK)
            {
                form.TagsDoneSplit.Iter(t => t.Choice = Choice.Categorized);
                updateUI();

                var dones = new HashSet<PlcTagBaseFDA>(form.TagsDoneSplit);
                foreach(var t in tags.Where(tags => ! dones.Contains(tags)))
                {
                    t.Choice = Choice.Discarded;
                    Logger.Error($"Discarding tag failed to split F/D/A: {t.Stringify()}");
                }
            }
            return form.TagsDoneSplit.Count();
        }

        void applyReplaceTags(bool withUI=true)
        {
            var patterns = _appSettings.TagPatternReplaces.Concat(_appSettings.DialectPatterns).ToArray();
            replaceFDA(TagsStage, patterns, FDAT.DuTag, withUI);
        }

        void btnApplyAll_Click(object sender, EventArgs e)
        {
            bool withUI = false;
            int discarded = applyDiscardTags(withUI);
            applyReplaceTags(withUI);
            applySplitFDA(withUI);

            int changedF = replaceFDA(_appSettings.FlowPatternReplaces,   FDAT.DuFlow,   withUI);
            int changedD = replaceFDA(_appSettings.DevicePatternReplaces, FDAT.DuDevice, withUI);
            int changedA = replaceFDA(_appSettings.ActionPatternReplaces, FDAT.DuAction, withUI);


            int standardF = replaceFDA(_appSettings.DialectPatterns, FDAT.DuFlow, withUI);
            int standardD = replaceFDA(_appSettings.DialectPatterns, FDAT.DuDevice, withUI);
            int standardA = replaceFDA(_appSettings.DialectPatterns, FDAT.DuAction, withUI);
        }
    }
}

