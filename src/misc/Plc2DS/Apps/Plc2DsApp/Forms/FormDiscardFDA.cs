using log4net.Util;

namespace Plc2DsApp.Forms
{
	public partial class FormDiscardFDA: DevExpress.XtraEditors.XtraForm
	{
        PlcTagBaseFDA[] _tags = [];
        Pattern[] _patterns = [];
        Func<PlcTagBaseFDA, string> _fdaGetter = null;
        Action<PlcTagBaseFDA, string> _fdaSetter = null;

        public FormDiscardFDA(PlcTagBaseFDA[] tags, Pattern[] patterns, Func<PlcTagBaseFDA, string> fdaGetter, Action<PlcTagBaseFDA, string> fdaSetter)
		{
            InitializeComponent();

            _fdaSetter = fdaSetter;
            _fdaGetter = fdaGetter;
            _patterns = patterns;
            _tags = tags;

            gridControl1.DataSource = patterns;
            gridView1.AddActionColumn<Pattern>("Apply", p =>
            {
                return ("Apply", new Action<Pattern>(p =>
                {
                    var pattern = new Regex(p.PatternString, RegexOptions.Compiled);
                    //applyPatterns(new Regex[] { pattern });
                    applyPatterns([p]);
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

        private void FormDiscardFDA_Load(object sender, EventArgs e)
        {
        }

        static string getPatternApplication(PlcTagBaseFDA tag, ReplacePattern[] replacePatterns, Func<PlcTagBaseFDA, string> fdaGetter)
        {
            string fda = fdaGetter(tag);
            foreach (var p in replacePatterns)
            {
                fda = p.RegexPattern.Replace(fda, p.Replacement);
            }
            return fda;
        }

        void applyPatterns(ReplacePattern[] patterns, string desc=null)
        {
            PlcTagBaseFDA[] candidates = ApplyPattern(_tags, patterns, _fdaGetter, _fdaSetter);
            var form = new FormTags(candidates, candidates, usageHint: $"(Extract {desc} pattern)");
            var getter = new Func<PlcTagBaseFDA, string>(t => getPatternApplication(t, patterns, _fdaGetter));
            form.GridView.AddUnboundColumnCustom<PlcTagBaseFDA, string>($"AppliedNewName", getter, null);
            if (form.ShowDialog() == DialogResult.OK)
            {
                // 변경 내용 적용
                foreach (var t in form.SelectedTags)
                    _fdaSetter(t, getPatternApplication(t, patterns, _fdaGetter));
            }
        }

        void applyPatterns(Pattern[] patterns)
        {
            ReplacePattern[] replacePatterns =
                patterns.Select(p => ReplacePattern.FromPattern(p)).ToArray();

            string descs = patterns.Select(p => p.Name).JoinString("|");
            applyPatterns(replacePatterns, descs);
        }

        public static PlcTagBaseFDA[] ApplyPattern(PlcTagBaseFDA[] tags, Pattern[] patterns, Func<PlcTagBaseFDA, string> fdaGetter, Action<PlcTagBaseFDA, string> fdaSetter)
        {

            IEnumerable<PlcTagBaseFDA> collectCandidates(ReplacePattern replacePattern)
            {
                foreach (var t in tags)
                {
                    string fda = fdaGetter(t); // f, d, a 중 하나를 가져옴
                    var match = replacePattern.RegexPattern.Match(fda);
                    if (match.Success)
                        yield return t;
                }
            }

            ReplacePattern[] replacePatterns = patterns.Select(ReplacePattern.FromPattern).ToArray();

            PlcTagBaseFDA[] candidates = replacePatterns.SelectMany(collectCandidates).ToArray();
            // 변경 내용 적용
            foreach (var t in candidates)
                fdaSetter(t, getPatternApplication(t, replacePatterns, fdaGetter));

            return candidates;
        }

        private void btnApplyCustomPattern_Click(object sender, EventArgs e)
        {
            // default 는 discard 이므로, replaceString 이 "" 가 됨
            var replacePattern = ReplacePattern.Create("NoName", tbCustomPattern.Text, "", "");
            applyPatterns([replacePattern]);
        }

        private void btnApplyAllPatterns_Click(object sender, EventArgs e)
        {
            var replacePatterns = _patterns.Select(ReplacePattern.FromPattern).ToArray();
            applyPatterns(replacePatterns);
        }
    }
}