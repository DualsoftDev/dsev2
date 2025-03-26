namespace Plc2DsApp.Forms
{
	public partial class FormReplaceFDAT: DevExpress.XtraEditors.XtraForm
	{
        PlcTagBaseFDA[] _tags = [];
        ReplacePattern[] _patterns = [];
        FDAT _fdat;
        public int NumChanged { get; set; }

        public FormReplaceFDAT(PlcTagBaseFDA[] tags, ReplacePattern[] patterns, FDAT fdat)
		{
            InitializeComponent();
            _fdat = fdat;
            _patterns = patterns;
            _tags = tags;

            gridControl1.DataSource = patterns;


            var actionColumn =
                gridView1.AddActionColumn<ReplacePattern>("Apply", p =>
                {
                    return ("Apply", new Action<ReplacePattern>(p => applyPatterns([p])));
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
            btnOK.Click += (s, e) => { Close(); DialogResult = DialogResult.OK; };
            btnCancel.Click += (s, e) => { Close(); DialogResult = DialogResult.Cancel; };

            var dict = patterns.ToDictionary(p => p, p => p.CollectCandidates(tags, _fdat).Length);
            var numMatchColumn = gridView1.AddUnboundColumnCustom<ReplacePattern, int>("NumMatches", p => dict[p], null);
            gridView1.Columns.Add(numMatchColumn); // 컬럼을 명확히 추가
            gridView1.Columns.Add(actionColumn); // 컬럼을 명확히 추가
            numMatchColumn.VisibleIndex = 100;
            actionColumn.VisibleIndex = 101;

            gridView1.ApplyVisibleColumns([nameof(Pattern.Name), nameof(Pattern.PatternString), nameof(ReplacePattern.Replacement), nameof(Pattern.Description), "NumMatches", "Apply"]);
            gridView1.Invalidate();
        }

        void FormDiscardFDA_Load(object sender, EventArgs e)
        {
        }

        int applyPatterns(ReplacePattern[] patterns)
        {
            PlcTagBaseFDA[] candidates = patterns.CollectCandidates(_tags, _fdat);
            string descs = patterns.Select(p => p.Name).JoinString("|");

            var form = new FormTags(candidates, candidates, usageHint: $"(Extract {descs} pattern)");
            var getter = new Func<PlcTagBaseFDA, string>(t => t.GetPatternApplication(patterns, _fdat));
            form.GridView.AddUnboundColumnCustom<PlcTagBaseFDA, string>($"AppliedNewName", getter, null);
            if (form.ShowDialog() == DialogResult.OK)
            {
                // 변경 내용 적용
                patterns.Apply(form.SelectedTags, _fdat);

                return form.SelectedTags.Length;
            }

            return 0;
        }

        public void btnApplyAllPatterns_Click(object sender, EventArgs e)
        {
            var replacePatterns = _patterns.Select(ReplacePattern.FromPattern).ToArray();
            NumChanged = applyPatterns(replacePatterns);
        }
    }
}