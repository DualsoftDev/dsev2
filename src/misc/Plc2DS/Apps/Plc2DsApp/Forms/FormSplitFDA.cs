using System.Threading.Tasks;
using System.Windows.Forms;

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
        void showTags(IEnumerable<PlcTagBaseFDA> tags) => new FormTags(tags).ShowDialog();
        public FormSplitFDA(PlcTagBaseFDA[] tags, Pattern[] patterns, bool withUI)
		{
            InitializeComponent();

            _patterns = patterns;
            _tags = tags;

            gridControl1.DataSource = patterns;
            var actionColumn =
                gridView1.AddActionColumn<Pattern>("Apply", p => ("Apply", new Action<Pattern>(p => applyPatterns(TagsStage, [p], withUI))));

            tbCustomPattern.Text = patterns[0].PatternString;     // 일단 맨처음거 아무거나..

            gridView1.SelectionChanged += (s, e) =>
            {
                var pattern = gridView1.GetFocusedRow() as Pattern;
                tbCustomPattern.Text = pattern.PatternString;
            };

            btnOK.Click += (s, e) => { Close(); DialogResult = DialogResult.OK; };
            btnCancel.Click += (s, e) => { Close(); DialogResult = DialogResult.Cancel; };

            if (withUI)
            {
                Task.Run(() =>
                {
                    //var dict = new Dictionary<PlcTagBaseFDA, int>();
                    var dict = patterns.ToDictionary(p => p, p => ApplyPatterns(tags, [p]).Length);
                    this.Do(() =>
                    {
                        var numMatchColumn = gridView1.AddUnboundColumnCustom<Pattern, int>("NumMatches", p => dict[p], null);
                        gridView1.Columns.Add(numMatchColumn); // 컬럼을 명확히 추가
                        gridView1.Columns.Add(actionColumn); // 컬럼을 명확히 추가
                        numMatchColumn.VisibleIndex = 100;
                        actionColumn.VisibleIndex = 101;

                        gridView1.ApplyVisibleColumns([nameof(Pattern.Name), nameof(Pattern.PatternString), nameof(ReplacePattern.Replacement), nameof(Pattern.Description), "NumMatches", "Apply"]);
                        gridView1.Invalidate();
                    });
                });
            }
            else
            {
                this.MakeHiddenSelfOK();
                this.btnApplyAllPatterns_Click(null, null);
            }
        }

        void FormExtractFDA_Load(object sender, EventArgs e)
        {
            btnShowAllTags.Click += (s, e) => showTags(_tags);
            btnShowStageTags.Click += (s, e) => showTags(TagsStage);
            btnShowChosenTags.Click += (s, e) => showTags(_tags.Where(t => t.Choice == Choice.Chosen));
            btnShowCategorizedTags.Click += (s, e) => showTags(_tags.Where(t => t.Choice == Choice.Categorized));
            updateUI();
        }

        //void applyPatterns(Pattern[] patterns, bool withUI) => applyPatterns(TagsStage, patterns, withUI);
        PlcTagBaseFDA[] applyPatterns(PlcTagBaseFDA[] tags, Pattern[] patterns, bool withUI)
        {
            var categorizedCandidates = ApplyPatterns(tags, patterns);

            var form = new FormTags(categorizedCandidates, categorizedCandidates, usageHint: "(Extract FDA pattern)", withUI: withUI);
            if (form.ShowDialog() == DialogResult.OK)
            {
                form.SelectedTags.Where(t => t.Choice == Choice.Stage).Iter(t => t.Choice = Choice.Categorized);
                updateUI();
                TagsDoneSplit.AddRange(form.SelectedTags);
                return form.SelectedTags;
            }
            return [];
        }

        public static PlcTagBaseFDA[] ApplyPatterns(PlcTagBaseFDA[] tags, Pattern[] patterns)
        {
            var done = new HashSet<PlcTagBaseFDA>();
            IEnumerable<PlcTagBaseFDA> collectCategorized(Pattern pattern)
            {
                foreach (var t in tags.Where(t => ! done.Contains(t)))
                {
                    var match = pattern.RegexPattern.Match(t.CsGetName());
                    if (match.Success)
                    {
                        // match group 의 이름 중에 flow, device, action 을 찾아서 t 에 저장
                        t.FlowName = match.Groups["flow"].Value;
                        t.DeviceName = match.Groups["device"].Value;
                        t.ActionName = match.Groups["action"].Value;
                        yield return t;
                    }
                }
            }
            foreach(var p in patterns)
            {
                var matches = collectCategorized(p);
                done.AddRange(matches);
            }

            return done.ToArray();
        }


        void btnApplyCustomPattern_Click(object sender, EventArgs e)
        {
            var pattern = Pattern.Create("임시 패턴", tbCustomPattern.Text);
            applyPatterns(TagsStage, [pattern], true);
        }

        void btnApplyAllPatterns_Click(object sender, EventArgs e)
        {
            var splited = applyPatterns(TagsStage, _patterns, withUI: sender != null).ToArray();
        }
    }
}