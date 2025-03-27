using System.Diagnostics;

using DevExpress.Images;
using DevExpress.XtraEditors;

using log4net.Appender;
using log4net.Core;




namespace Plc2DsApp
{
    public partial class FormMain : DevExpress.XtraEditors.XtraForm, IAppender
    {
        public Vendor Vendor
        {
            get => _appRegistry.Vendor;
            set => _appRegistry.Vendor = value;
        }
        public string[] VisibleColumns => _vendorRule.VisibleColumns;

        public static FormMain Instance { get; private set; }

        public PlcTagBaseFDA[] TagsAll = [];


        public PlcTagBaseFDA[] TagsDiscarded => selectTags(Choice.Discarded);
        public PlcTagBaseFDA[] TagsChosen => selectTags(Choice.Chosen);
        public PlcTagBaseFDA[] TagsCategorized => selectTags(Choice.Categorized);
        public PlcTagBaseFDA[] TagsStage => selectTags(Choice.Stage);

        AppRegistry _appRegistry = new AppRegistry();
        Rulebase _vendorRule = null;
        UiUpdator _uiUpdator = new UiUpdator();

        public FormMain()
        {
            InitializeComponent();

            _uiUpdator.StartMainLoop(this, updateUI);
            if (File.Exists("lastFile.json"))
                _appRegistry = EmJson.FromJson<AppRegistry>(File.ReadAllText("lastFile.json"));

            Instance = this;
            Dual.Plc2DS.ModuleInitializer.Initialize();
            DcLogger.EnableTrace = false;

            tbCsvFile.Text = _appRegistry.LastRead;

            void reloadAppsetting()
            {
                AppSettings appSettings = EmJson.FromJson<AppSettings>(File.ReadAllText("appsettings.json"));
                _vendorRule = appSettings.CreateVendorRulebase(Vendor);
            }
            reloadAppsetting();

            string[] vendors = ["LS", "AB", "S7", "MX"];
            string lastVendor = _appRegistry.Vendor?.ToString();
            int selectedIndex = lastVendor == null ? 0 : Array.IndexOf(vendors, lastVendor);
            ucRadioSelector1.SetOptions(vendors, selectedIndex: selectedIndex, itemLayout: RadioGroupItemsLayout.Flow);
            ucRadioSelector1.SelectedOptionChanged += (s, e) =>
            {
                var vendor = e;
                Vendor = Vendor.FromString(vendor);
                reloadAppsetting();
            };

            // C:\Program Files\DevExpress 23.2\Components\Sources\Win\DevExpress.Images\Images\Actions\Open2_16x16.png

            this.AddMenuItem("File/Tag/Open..",          () => btnLoadTags.PerformClick())           .ImageOptions.Image = ImageResourceCache.Default.GetImage("images/actions/open2_16x16.png");
            this.AddMenuItem("File/Tag/Save as..",       () => FormMain.Instance.SaveTagsAs(TagsAll)).ImageOptions.Image = ImageResourceCache.Default.GetImage("images/save/save_16x16.png");
            this.AddMenuItem("File/Tag/Decode AB csv..", () => decodeAbCsv());
            this.AddMenuItem("File/Open install folder", () => Process.Start("explorer.exe", System.AppDomain.CurrentDomain.BaseDirectory));
            this.AddMenuItem("File/Merge appsettings..", () => btnMergeAppSettings.PerformClick());
            this.AddMenuItem("Help/About",               () => new FormAbout().ShowDialog(this)).ImageOptions.Image = ImageResourceCache.Default.GetImage("images/support/info_16x16.png"); ;

            this.FindSubItem("File/Tag").ImageOptions.Image = ImageResourceCache.Default.GetImage("images/programming/tag_16x16.png");
        }
        void FormMain_Load(object sender, EventArgs e)
        {
            //loadTags(tbCsvFile.Text);
            btnShowAllTags.Click += (s, e) => showTags(TagsAll);
            btnShowStageTags.Click += (s, e) => showTags(selectTags(Choice.Stage, true));
            btnShowChosenTags.Click += (s, e) => showTags(selectTags(Choice.Chosen, true));
            btnShowCategorizedTags.Click += (s, e) => showTags(selectTags(Choice.Categorized, true));
            btnShowDiscardedTags.Click += (s, e) =>
            {
                FormTags form = showTags(selectTags(Choice.Discarded, true), selectionColumnCaption: "Resurrect");
                if (form.DialogResult == DialogResult.OK && form.SelectedTags.Any())
                    form.SelectedTags.Iter(t => t.Choice = Choice.Stage);
            };

            btnReplaceFlowName.Enabled = _vendorRule.FlowPatternReplaces.Any();
            btnReplaceDeviceName.Enabled = _vendorRule.DevicePatternReplaces.Any();
            btnReplaceActionName.Enabled = _vendorRule.ActionPatternReplaces.Any();

            btnReplaceFlowName.Click += (s, e) => replaceFDA(_vendorRule.FlowPatternReplaces, FDAT.DuFlow, withUI: true);
            btnReplaceDeviceName.Click += (s, e) => replaceFDA(_vendorRule.DevicePatternReplaces, FDAT.DuDevice, withUI: true);
            btnReplaceActionName.Click += (s, e) => replaceFDA(_vendorRule.ActionPatternReplaces, FDAT.DuAction, withUI: true);
        }
        public void DoAppend(LoggingEvent loggingEvent)
        {
            this.Do(() => ucPanelLog1.AddLog(loggingEvent));
        }

        /// <summary>
        /// abstract class 인 PlcTagBaseFDA 를 vendor 에 맞는 subclass type 으로 변환
        /// </summary>
        public object ConvertToVendorTags(IEnumerable<PlcTagBaseFDA> tags)
        {
            var typ = FormMain.Instance.Vendor.CsGetTagType();
            return tags.Select(t => Convert.ChangeType(t, typ)).ToArray();
        }


        PlcTagBaseFDA[] selectTags(Choice cat, bool loadOnDemand = false)
        {
            if (loadOnDemand && TagsAll.IsNullOrEmpty())
                TagsAll = loadTags(tbCsvFile.Text, _vendorRule.CsvFilterExpression);

            return TagsAll.Where(t => t.Choice == cat).ToArray();
        }

        FormTags showTags(PlcTagBaseFDA[] tags, string selectionColumnCaption = null, string usageHint = null)
        {
            var form = new FormTags(tags, selectionColumnCaption: selectionColumnCaption, usageHint: usageHint).Tee(f => f.PlaceAtScreenCenter());
            form.DoShow();
            return form;
        }

        int replaceFDA(ReplacePattern[] patterns, FDAT fdat, bool withUI = false)
        {
            var tags = TagsCategorized.Concat(TagsChosen).ToArray();
            return replaceFDA(tags, patterns, fdat, withUI);
        }

        int replaceFDA(PlcTagBaseFDA[] tags, ReplacePattern[] patterns, FDAT fdat, bool withUI = false)
        {
            if (withUI)
            {
                var form = new FormReplaceFDAT(tags, patterns, fdat).Tee(f => f.PlaceAtScreenCenter());
                form.ShowDialog();
                return form.NumChanged;
            }
            else
            {
                var applied = patterns.Apply(tags, fdat);
                return applied.Length;
            }
        }


        public void SaveTagsAs(IEnumerable<PlcTagBaseFDA> tags)
        {
            string filter = new[] { filterJson, filterAll }.JoinString("|");
            var f = DcFileDialog.SaveFile(filter, Path.GetDirectoryName(_appRegistry.LastWrite));
            if (f.NonNullAny())
            {
                string json = EmJson.ToJson(tags);
                File.WriteAllText(f, json);
                _appRegistry.LastWrite = f;
            }
        }

        PlcTagBaseFDA[] loadTags(string csvFile, CsvFilterExpression filter)
        {
            Logger.Info($"Loading tags from {csvFile}");

            using var wf = DcWaitForm.CreateWaitForm("Loading tags...");
            var ext = Path.GetExtension(csvFile).ToLower();
            var vendor = Vendor.ToString();

            PlcTagBaseFDA[] tags = ext switch
            {
                ".csv" => vendor switch
                {
                    "LS" => CsvReader.ReadLs(csvFile).ToArray(),
                    "AB" => CsvReader.ReadAb(csvFile).ToArray(),
                    "S7" => CsvReader.ReadS7(csvFile).ToArray(),
                    "MX" => CsvReader.ReadMx(csvFile).ToArray(),
                    _ => throw new NotImplementedException()
                },
                ".json" => vendor switch
                {
                    "LS" => EmJson.FromJson<LS.PlcTagInfo[]>(File.ReadAllText(csvFile)),
                    "AB" => EmJson.FromJson<AB.PlcTagInfo[]>(File.ReadAllText(csvFile)),
                    "S7" => EmJson.FromJson<S7.PlcTagInfo[]>(File.ReadAllText(csvFile)),
                    "MX" => EmJson.FromJson<MX.PlcTagInfo[]>(File.ReadAllText(csvFile)),
                    _ => throw new NotImplementedException()
                },
                ".xml" => vendor switch
                {
                    "LS" => XmlReader.ReadLs(csvFile).ToArray(),
                    _ => throw new NotImplementedException()
                },
                _ => throw new NotImplementedException()
            };


            Logger.Info($"  Loaded {tags.Length} tags from {csvFile}");
            var grDic =
                tags.GroupByToDictionary(t =>
                    filter.CsTryMatch(t).MatchMap(
                        result => result,
                        () => true)
                    );
            var excludes = new HashSet<PlcTagBaseFDA>(grDic.ContainsKey(false) ? grDic[false] : []);
            excludes.Iter(t => t.Choice = Choice.Discarded);
            if (excludes.Any())
            {
                Logger.Info($"  Discarded {excludes.Count} tags from {csvFile} using CsvFilterPatterns");
                var text = excludes.Select(t => t.Csvify()).JoinString("\r\n");
                var file = csvFile + ".discarded.csv";
                File.WriteAllText(file, text);
                Logger.Info($"  You can check discarded tags on {file}");
            }

            TagsAll = tags.Where(t => !excludes.Contains(t)).ToArray();

            var fdaSplitPattern = new Regex(_vendorRule.FDASplitPattern, RegexOptions.Compiled);
            TagsAll.Iter(t =>
                {
                    t.CsSetFDA(t.CsTryGetFDA([fdaSplitPattern]));
                    t.Choice = Choice.Stage;
                });

            var invalidTags = TagsAll.Where(t => !t.CsIsValid()).ToArray();
            if (invalidTags.Any())
            {
                var f = invalidTags.First();
                var msg = $"Total {invalidTags.Length} invalid tags: {f.Csvify()}...\r\nCheck essentail filed (Name or Address) non-empty!\r\nContinue?";
                if (DialogResult.No == MessageBox.Show(msg, "ERROR", MessageBoxButtons.YesNo))
                    return [];
            }

            return TagsAll;
        }

        void updateUI()
        {
            tbNumTagsAll.Text = TagsAll.Length.ToString();
            tbNumTagsDiscarded.Text = TagsDiscarded.Length.ToString();
            tbNumTagsChosen.Text = TagsChosen.Length.ToString();
            tbNumTagsCategorized.Text = TagsCategorized.Length.ToString();
            tbNumTagsStage.Text = TagsStage.Length.ToString();
            var buttons =
                new[] {
                    btnDiscardTags, btnReplaceTags, btnSplitFDA,
                    btnReplaceFlowName, btnReplaceDeviceName, btnReplaceActionName,
                    btnApplyAll
                };
            bool fileSpecified = tbCsvFile.Text.Any();
            buttons.Iter(b => b.Enabled = fileSpecified);
            if (fileSpecified)
            {
                btnReplaceFlowName.Enabled = _vendorRule.FlowPatternReplaces.Any();
                btnReplaceDeviceName.Enabled = _vendorRule.DevicePatternReplaces.Any();
                btnReplaceActionName.Enabled = _vendorRule.ActionPatternReplaces.Any();
            }

            SimpleButton[] showTagButtons = [
                btnShowAllTags,
                btnShowStageTags,
                btnShowChosenTags,
                btnShowCategorizedTags,
                btnShowDiscardedTags
            ];
            showTagButtons.Iter(b => b.Enabled = TagsAll.Any());

        }


        string filterCsv  = "CSV files (*.csv)|*.csv";
        string filterSdf  = "SDF files (*.sdf)|*.sdf";
        string filterJson = "JSON files (*.json)|*.json";
        string filterXml  = "XML files (*.xml)|*.xml";
        string filterAll  = "All files (*.*)|*.*";

        void btnLoadTags_Click(object sender, EventArgs e)
        {
            using var _ = btnLoadTags.Disabler();
            string[] filters = [];

            if (Vendor == Vendor.LS)
                filters = [filterCsv, filterXml, filterJson, filterAll];
            else if (Vendor == Vendor.S7)
                filters = [filterSdf, filterJson, filterAll];
            else
                filters = [filterCsv, filterJson, filterAll];
            string filter = filters.JoinString("|");

            var f = DcFileDialog.OpenFile(filter, Path.GetDirectoryName(_appRegistry.LastRead));
            if (f.NonNullAny())
            {
                tbCsvFile.Text = f;
                loadTags(f, _vendorRule.CsvFilterExpression);
                _appRegistry.LastRead = f;
            }
        }

        void decodeAbCsv()
        {
            string filter = new[] { filterCsv, filterAll }.JoinString("|");
            string r = DcFileDialog.OpenFile(filter, Path.GetDirectoryName(_appRegistry.LastRead));
            if (r.IsNullOrEmpty()) return;
            string w = DcFileDialog.SaveFile(filter, Path.GetDirectoryName(_appRegistry.LastWrite));
            if (w.IsNullOrEmpty()) return;

            var decoded = AB.CsvReader.Decode(File.ReadAllText(r), decodeSpecialChar:false, decodeHangule:true);
            File.WriteAllText(w, decoded);
        }



        void btnDiscardTags_Click(object sender, EventArgs e)
        {
            using var _ = btnDiscardTags.Disabler();

            Pattern[] patterns = _vendorRule.TagPatternDiscards;
            var _2 = selectTags(Choice.Stage, true);   // load TagsAll if null or empty

            var form = new FormDiscardTags(TagsAll, patterns).Tee(f => f.PlaceAtScreenCenter());

            if (form.ShowDialog() == DialogResult.OK)
            {
                var chosen = form.TagsChosen.ToArray();
                form.TagsChosen.Iter(t => t.Choice = Choice.Discarded);
            }
        }

        void btnReplaceTags_Click(object sender, EventArgs e)
        {
            using var _ = btnReplaceTags.Disabler();
            var patterns = _vendorRule.TagPatternReplaces.Concat(_vendorRule.DialectPatterns).ToArray();

            var form = new FormReplaceFDAT(TagsStage, patterns, FDAT.DuTag).PlaceAtScreenCenter();
            form.ShowDialog();
        }

        void btnSplitFDA_Click(object sender, EventArgs e)
        {
            using var _ = btnSplitFDA.Disabler();

            Pattern[] patterns = _vendorRule.TagPatternFDAs;

            var tags = TagsStage.ToArray();

            var form = new FormSplitFDA(tags, patterns).Tee(f => f.PlaceAtScreenCenter());

            if (form.ShowDialog() == DialogResult.OK)
            {
                form.TagsDoneSplit.Iter(t => t.Choice = Choice.Categorized);

                var dones = new HashSet<PlcTagBaseFDA>(form.TagsDoneSplit);
                foreach (var t in tags.Where(tags => !dones.Contains(tags)))
                {
                    t.Choice = Choice.Discarded;
                    Logger.Error($"Discarding tag failed to split F/D/A: {t.Stringify()}");
                }
            }
        }


        void btnMergeAppSettings_Click(object sender, EventArgs e)
        {
            string filter = new[] { filterJson, filterAll }.JoinString("|");
            var f = DcFileDialog.OpenFile(filter, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Rulebases"));
            if (f.NonNullAny())
            {
                Rulebase partial = EmJson.FromJson<AppSettings>(File.ReadAllText(f)).CreateVendorRulebase(Vendor);
                if (cbMergeAppSettingsOverride.Checked)
                    _vendorRule.Override(partial);
                else
                    _vendorRule.Merge(partial);

                MessageBox.Show("Merged AppSettings!");
            }
        }



        void btnApplyAll_Click(object sender, EventArgs e)
        {
            using var _ = btnApplyAll.Disabler();
            var _2 = selectTags(Choice.Stage, true);   // load TagsAll if null or empty

            var r = _vendorRule;

            // discards tags
            r.TagPatternDiscards.FindMatches(TagsAll).Iter(t => t.Choice = Choice.Discarded);

            // replaces tags
            {
                var patterns = r.TagPatternReplaces.Concat(r.DialectPatterns).ToArray();
                replaceFDA(TagsStage, patterns, FDAT.DuTag);
            }

            // split tag name => {flow, device, action}
            applySplitFDA();

            // replaces {flow, device, action}
            int changedF = replaceFDA(r.FlowPatternReplaces, FDAT.DuFlow, withUI: false);
            int changedD = replaceFDA(r.DevicePatternReplaces, FDAT.DuDevice, withUI: false);
            int changedA = replaceFDA(r.ActionPatternReplaces, FDAT.DuAction, withUI: false);

            // standardizes {flow, device, action}
            int standardF = replaceFDA(r.DialectPatterns, FDAT.DuFlow, withUI: false);
            int standardD = replaceFDA(r.DialectPatterns, FDAT.DuDevice, withUI: false);
            int standardA = replaceFDA(r.DialectPatterns, FDAT.DuAction, withUI: false);



            int applySplitFDA()
            {
                Pattern[] patterns = r.TagPatternFDAs;

                var tags = TagsStage.ToArray();
                var categorized = patterns.Categorize(tags);
                categorized.Where(t => t.Choice == Choice.Stage).Iter(t => t.Choice = Choice.Categorized);

                tags.Except(categorized).Iter(t =>
                {
                    t.Choice = Choice.Discarded;
                    Logger.Error($"Discarding tag failed to split F/D/A: {t.Stringify()}");
                });

                return categorized.Length;
            }
        }
    }
}

