namespace Plc2DsApp.Forms
{
	public partial class FormDiscardTags: DevExpress.XtraEditors.XtraForm
	{
        PlcTagBaseFDA[] _tags = [];
        PlcTagBaseFDA[] _tagsStage = [];
        public PlcTagBaseFDA[] TagsChosen = [];

        Pattern[] _patterns = [];
        void updateUI()
        {
            tbNumTagsAll.Text = _tags.Length.ToString();
            tbNumTagsChosen.Text = TagsChosen.Length.ToString();
            tbNumTagsStage.Text = _tagsStage.Length.ToString();
        }
        void showTags(PlcTagBaseFDA[] tags, string usageHint = null) =>
            new FormTags(tags, usageHint: usageHint).ShowDialog();

        public FormDiscardTags(PlcTagBaseFDA[] tags, Pattern[] patterns)
		{
            InitializeComponent();

            _patterns = patterns;
            _tags = tags;
            _tagsStage = tags;


            gridControl1.DataSource = patterns;

            var actionColumn =
                gridView1.AddActionColumn<Pattern>("Apply", p =>
                {
                    return ("Apply", new Action<Pattern>(p => applyPatterns([p])));
                });

            gridView1.DoDefaultSettings();
            gridView1.CellValueChanged += (s, e) =>
            {
                var field = e.Column.FieldName;
                if (field == nameof(Pattern.PatternString))
                {
                    var row = gridView1.GetRow<Pattern>(e.RowHandle);
                    row.RegexPattern = new Regex(row.PatternString);
                }
            };


            Task.Run(() =>
            {
                // pattern 별 match 된 tag 수 계산
                var dict = patterns.ToDictionary(p => p, p => p.FindMatches(tags).Length);
                this.Do(() =>
                {
                    var numMatchColumn = gridView1.AddUnboundColumnCustom<Pattern, int>("NumMatches", p => dict[p], null);
                    gridView1.Columns.Add(numMatchColumn); // 컬럼을 명확히 추가
                    gridView1.Columns.Add(actionColumn); // 컬럼을 명확히 추가
                    numMatchColumn.VisibleIndex = 100;
                    actionColumn.VisibleIndex = 101;
                    gridView1.ApplyVisibleColumns([nameof(Pattern.Name), nameof(Pattern.PatternString), "Relacement", nameof(Pattern.Description), "NumMatches", "Apply"]);
                    gridView1.Invalidate();
                });
            });

            btnOK.Click += (s, e) => { Close(); DialogResult = DialogResult.OK; };
            btnCancel.Click += (s, e) => { Close(); DialogResult = DialogResult.Cancel; };
        }
        void FormPattern_Load(object sender, EventArgs e)
        {
            btnShowAllTags.Click += (s, e) => showTags(_tags, usageHint:"(All Tags)");
            btnShowStageTags.Click += (s, e) => showTags(_tagsStage, usageHint: "(Stage Tags)");
            btnShowChosenTags.Click += (s, e) => showTags(TagsChosen, usageHint: "(Chosen Tags)");
            updateUI();
        }

        //static PlcTagBaseFDA[] collectMatchedTags(PlcTagBaseFDA[] tags, Pattern[] patterns)
        //{
        //    var gr = tags.GroupByToDictionary(t => patterns.Any(p => p.RegexPattern.IsMatch(t.CsGetName())));
        //    return gr.ContainsKey(true) ? gr[true] : [];
        //}

        public static PlcTagBaseFDA[] ApplyPatterns(PlcTagBaseFDA[] tags, Pattern[] patterns, bool withUI)
        {
            var chosens = patterns.FindMatches(tags);
            if (chosens.Any())
            {
                var form = new FormTags(chosens, selectedTags: chosens, usageHint: "(Pattern matching)", withUI:withUI);
                form.ShowDialog();
            }
            return chosens;
        }

        void applyPatterns(Pattern[] patterns, bool withUI=true)
        {
            var chosen = ApplyPatterns(_tagsStage, patterns, withUI);
            _tagsStage = _tagsStage.Except(chosen).ToArray();
            TagsChosen = TagsChosen.Concat(chosen).ToArray();
            updateUI();
        }

        void btnApplyAllPatterns_Click(object sender, EventArgs e) => applyPatterns(_patterns);
    }
}