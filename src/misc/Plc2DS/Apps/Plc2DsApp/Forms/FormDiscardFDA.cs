using System.Security.Cryptography;
using System.Windows.Forms;

using static DevExpress.Utils.MVVM.Internal.ILReader;

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

        static string getPatternApplication(PlcTagBaseFDA tag, Regex[] patterns, Func<PlcTagBaseFDA, string> fdaGetter)
        {
            string fda = fdaGetter(tag);
            foreach (var p in patterns)
                fda = p.Replace(fda, "");
            return fda;
        }

        void applyPatterns(Regex[] patterns, string desc=null)
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
            Regex[] regexPatterns = patterns.Select(p => new Regex(p.PatternString, RegexOptions.Compiled)).ToArray();
            string descs = patterns.Select(p => p.Name).JoinString("|");
            applyPatterns(regexPatterns, descs);
        }

        public static PlcTagBaseFDA[] ApplyPattern(PlcTagBaseFDA[] tags, Pattern[] patterns, Func<PlcTagBaseFDA, string> fdaGetter, Action<PlcTagBaseFDA, string> fdaSetter)
        {
            Regex[] regexPatterns = patterns.Select(p => new Regex(p.PatternString, RegexOptions.Compiled)).ToArray();
            return ApplyPattern(tags, regexPatterns, fdaGetter, fdaSetter);
        }

        public static PlcTagBaseFDA[] ApplyPattern(PlcTagBaseFDA[] tags, Regex[] patterns, Func<PlcTagBaseFDA, string> fdaGetter, Action<PlcTagBaseFDA, string> fdaSetter)
        {
            IEnumerable<PlcTagBaseFDA> collectCandidates(Regex pattern)
            {
                foreach (var t in tags)
                {
                    string fda = fdaGetter(t); // f, d, a 중 하나를 가져옴
                    var match = pattern.Match(fda);
                    if (match.Success)
                        yield return t;
                }
            }

            PlcTagBaseFDA[] candidates = patterns.SelectMany(collectCandidates).ToArray();
            // 변경 내용 적용
            foreach (var t in candidates)
                fdaSetter(t, getPatternApplication(t, patterns, fdaGetter));

            return candidates;
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