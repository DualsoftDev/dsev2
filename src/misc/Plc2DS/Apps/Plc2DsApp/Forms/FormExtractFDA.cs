using DevExpress.Utils.Extensions;
using DevExpress.XtraEditors;

using System.Runtime.InteropServices.WindowsRuntime;

using static DevExpress.Utils.MVVM.Internal.ILReader;

namespace Plc2DsApp.Forms
{
	public partial class FormExtractFDA: DevExpress.XtraEditors.XtraForm
	{
        PlcTagBaseFDA[] _tags = [];
        Pattern[] _patterns = [];

        public PlcTagBaseFDA[] TagsStage => _tags.Where(t => t.Choice == Choice.Stage).ToArray();
        public PlcTagBaseFDA[] TagsNonStage => _tags.Where(t => t.Choice != Choice.Stage).ToArray();    // chosen + categorized
        void updateUI()
        {
            tbNumTagsAll.Text = _tags.Length.ToString();
            tbNumTagsNonStage.Text = TagsNonStage.Length.ToString();
            tbNumTagsStage.Text = TagsStage.Length.ToString();
            tbNumTagsCategorized.Text = _tags.Where(t => t.Choice == Choice.Categorized).Count().ToString();
        }
        void showTags(IEnumerable<PlcTagBaseFDA> tags) => new FormTags(tags).ShowDialog();
        public FormExtractFDA(PlcTagBaseFDA[] tags, Pattern[] patterns)
		{
            InitializeComponent();

            _patterns = patterns;
            _tags = tags;

            gridControl1.DataSource = patterns;
            gridView1.AddActionColumn<Pattern>("Apply", p =>
            {
                return ("Apply", new Action<Pattern>(p =>
                {
                    var pattern = new Regex(p.PatternString, RegexOptions.Compiled);
                    applyPatterns(new Regex[] { pattern });
                }));
            });

            tbCustomPattern.Text = patterns[0].PatternString;     // 일단 맨처음거 아무거나..

            gridView1.SelectionChanged += (s, e) =>
            {
                var pattern = gridView1.GetFocusedRow() as Pattern;
                tbCustomPattern.Text = pattern.PatternString;
            };

            btnOK.Click += (s, e) => { Close(); DialogResult = DialogResult.OK; };
            btnCancel.Click += (s, e) => { Close(); DialogResult = DialogResult.Cancel; };
        }

        private void FormExtractFDA_Load(object sender, EventArgs e)
        {
            btnShowAllTags.Click += (s, e) => showTags(_tags);
            btnShowStageTags.Click += (s, e) => showTags(TagsStage);
            btnShowChosenTags.Click += (s, e) => showTags(_tags.Where(t => t.Choice == Choice.Chosen));
            btnShowCategorizedTags.Click += (s, e) => showTags(_tags.Where(t => t.Choice == Choice.Categorized));
            updateUI();
        }

        void applyPatterns(Regex[] patterns) => applyPatterns(TagsStage, patterns);
        void applyPatterns(PlcTagBaseFDA[] tags, Regex[] patterns)
        {
            var categorizedCandidates = ApplyPatterns(tags, patterns);

            var form = new FormTags(categorizedCandidates, categorizedCandidates, usageHint: "(Extract FDA pattern)");
            if (form.ShowDialog() == DialogResult.OK)
            {
                form.SelectedTags.Where(t => t.Choice == Choice.Stage).Iter(t => t.Choice = Choice.Categorized);
                updateUI();
            }
        }

        public static PlcTagBaseFDA[] ApplyPatterns(PlcTagBaseFDA[] tags, Pattern[] patterns)
        {
            var regexPatterns = patterns.Select(p => new Regex(p.PatternString, RegexOptions.Compiled)).ToArray();
            return ApplyPatterns(tags, regexPatterns);
        }
        public static PlcTagBaseFDA[] ApplyPatterns(PlcTagBaseFDA[] tags, Regex[] patterns)
        {
            var done = new HashSet<PlcTagBaseFDA>();
            IEnumerable<PlcTagBaseFDA> collectCategorized(Regex pattern)
            {
                foreach (var t in tags.Where(t => ! done.Contains(t)))
                {
                    var match = pattern.Match(t.CsGetName());
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


        private void btnApplyCustomPattern_Click(object sender, EventArgs e)
        {
            var pattern = new Regex(tbCustomPattern.Text, RegexOptions.Compiled);
            applyPatterns([pattern]);
        }

        private void btnApplyAllPatterns_Click(object sender, EventArgs e)
        {
            var patterns = _patterns.Select(p => new Regex(p.PatternString, RegexOptions.Compiled)).ToArray();
            applyPatterns(patterns);
        }
    }
}