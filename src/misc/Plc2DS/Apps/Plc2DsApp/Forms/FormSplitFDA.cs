namespace Plc2DsApp.Forms
{
	public partial class FormSplitFDA: DevExpress.XtraEditors.XtraForm
	{
        PlcTagBaseFDA[] _tags = [];
        Pattern[] _patterns = [];

        public PlcTagBaseFDA[] TagsStage => _tags.Where(t => t.Choice == Choice.Stage).ToArray();
        public PlcTagBaseFDA[] TagsNonStage => _tags.Where(t => t.Choice != Choice.Stage).ToArray();    // chosen + categorized

        public HashSet<PlcTagBaseFDA> TagsDoneSplit = new();
        void updateUI()
        {
            tbNumTagsAll.Text = _tags.Length.ToString();
            tbNumTagsNonStage.Text = TagsNonStage.Length.ToString();
            tbNumTagsStage.Text = TagsStage.Length.ToString();
            tbNumTagsCategorized.Text = _tags.Where(t => t.Choice == Choice.Categorized).Count().ToString();
        }
        void showTags(IEnumerable<PlcTagBaseFDA> tags) => new FormTags(tags).PlaceAtScreenCenter().ShowDialog();
        public FormSplitFDA(PlcTagBaseFDA[] tags, Pattern[] patterns)
		{
            InitializeComponent();

            _patterns = patterns;
            _tags = tags;

            gridControl1.DataSource = patterns;
            var actionColumn =
                gridView1.AddActionColumn<Pattern>("Apply", p => ("Apply", new Action<Pattern>(p => applyPatterns(TagsStage, [p]))));

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

            btnOK.Click += (s, e) => { Close(); DialogResult = DialogResult.OK; };
            btnCancel.Click += (s, e) => { Close(); DialogResult = DialogResult.Cancel; };

            var dict = patterns.ToDictionary(p => p, p => p.Categorize(tags).Length);
            var numMatchColumn = gridView1.AddUnboundColumnCustom<Pattern, int>("NumMatches", p => dict[p], null);
            gridView1.Columns.Add(numMatchColumn); // 컬럼을 명확히 추가
            gridView1.Columns.Add(actionColumn); // 컬럼을 명확히 추가
            numMatchColumn.VisibleIndex = 100;
            actionColumn.VisibleIndex = 101;

            gridView1.ApplyVisibleColumns([nameof(Pattern.Name), nameof(Pattern.PatternString), nameof(ReplacePattern.Replacement), nameof(Pattern.Description), "NumMatches", "Apply"]);
            gridView1.Invalidate();
        }

        void FormExtractFDA_Load(object sender, EventArgs e)
        {
            btnShowAllTags.Click += (s, e) => showTags(_tags);
            btnShowStageTags.Click += (s, e) => showTags(TagsStage);
            btnShowChosenTags.Click += (s, e) => showTags(_tags.Where(t => t.Choice == Choice.Chosen));
            btnShowCategorizedTags.Click += (s, e) => showTags(_tags.Where(t => t.Choice == Choice.Categorized));
            updateUI();
        }


        PlcTagBaseFDA[] applyPatterns(PlcTagBaseFDA[] tags, Pattern[] patterns)
        {
            var categorizedCandidates = patterns.Categorize(tags);

            var form = new FormTags(categorizedCandidates, categorizedCandidates, usageHint: "(Extract FDA pattern)").Tee(f => f.PlaceAtScreenCenter());
            if (form.ShowDialog() == DialogResult.OK)
            {
                form.SelectedTags.Where(t => t.Choice == Choice.Stage).Iter(t => t.Choice = Choice.Categorized);
                updateUI();
                TagsDoneSplit.AddRange(form.SelectedTags);
                return form.SelectedTags;
            }
            return [];
        }

        void btnApplyAllPatterns_Click(object sender, EventArgs e)
        {
            var splited = applyPatterns(TagsStage, _patterns).ToArray();
        }
    }
}