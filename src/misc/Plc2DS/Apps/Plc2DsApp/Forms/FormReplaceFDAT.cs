using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Plc2DsApp.Forms
{
	public partial class FormReplaceFDAT: DevExpress.XtraEditors.XtraForm
	{
        PlcTagBaseFDA[] _tags = [];
        ReplacePattern[] _patterns = [];
        Func<PlcTagBaseFDA, string> _fdatGetter = null;
        Action<PlcTagBaseFDA, string> _fdatSetter = null;
        public int NumChanged { get; set; }

        public FormReplaceFDAT(PlcTagBaseFDA[] tags, ReplacePattern[] patterns, Func<PlcTagBaseFDA, string> fdatGetter, Action<PlcTagBaseFDA, string> fdatSetter, bool withUI)
		{
            InitializeComponent();

            _fdatSetter = fdatSetter;
            _fdatGetter = fdatGetter;
            _patterns = patterns;
            _tags = tags;

            gridControl1.DataSource = patterns;


            var actionColumn =
                gridView1.AddActionColumn<ReplacePattern>("Apply", p =>
                {
                    return ("Apply", new Action<ReplacePattern>(p => applyPatterns([p], withUI)));
                });

            if (patterns.Any())
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
                    var dict = patterns.ToDictionary(p => p, p => ApplyPatterns(tags, [p], _fdatGetter).Length);
                    this.Do(() =>
                    {
                        var numMatchColumn = gridView1.AddUnboundColumnCustom<ReplacePattern, int>("NumMatches", p => dict[p], null);
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

        void FormDiscardFDA_Load(object sender, EventArgs e)
        {
        }

        static string getPatternApplication(PlcTagBaseFDA tag, ReplacePattern[] replacePatterns, Func<PlcTagBaseFDA, string> fdatGetter)
        {
            string fda = fdatGetter(tag);
            foreach (var p in replacePatterns)
            {
                while(p.RegexPattern.IsMatch(fda))
                {
                    if (p.PatternString == "(?<word>[A-Za-z]+)/(?<number>\\d+)")
                        Noop();

                    fda = p.RegexPattern.Replace(fda, p.Replacement);
                }
            }
            return fda;
        }

        int applyPatterns(ReplacePattern[] patterns, bool withUI)
        {
            PlcTagBaseFDA[] candidates = ApplyPatterns(_tags, patterns, _fdatGetter, null);
            string descs = patterns.Select(p => p.Name).JoinString("|");
            var form = new FormTags(candidates, candidates, usageHint: $"(Extract {descs} pattern)", withUI:withUI);
            var getter = new Func<PlcTagBaseFDA, string>(t => getPatternApplication(t, patterns, _fdatGetter));
            form.GridView.AddUnboundColumnCustom<PlcTagBaseFDA, string>($"AppliedNewName", getter, null);
            if (form.ShowDialog() == DialogResult.OK)
            {
                // 변경 내용 적용
                foreach (var t in form.SelectedTags)
                    _fdatSetter(t, getPatternApplication(t, patterns, _fdatGetter));

                return form.SelectedTags.Length;
            }

            return 0;
        }


        public static PlcTagBaseFDA[] ApplyPatterns(PlcTagBaseFDA[] tags, ReplacePattern[] replacePatterns, Func<PlcTagBaseFDA, string> fdatGetter, Action<PlcTagBaseFDA, string> fdatSetter=null)
        {
            if (replacePatterns.IsNullOrEmpty())
                return [];

            IEnumerable<PlcTagBaseFDA> collectCandidates(ReplacePattern replacePattern)
            {
                foreach (var t in tags)
                {
                    string fda = fdatGetter(t); // f, d, a 중 하나를 가져옴
                    if (fda == null || fda.Contains("<@"))
                        Noop();
                    var match = replacePattern.RegexPattern.Match(fda);
                    if (match.Success)
                        yield return t;
                }
            }

            PlcTagBaseFDA[] candidates = replacePatterns.SelectMany(collectCandidates).ToArray();
            if (fdatSetter != null)
            {
                // 변경 내용 적용
                foreach (var t in candidates)
                    fdatSetter(t, getPatternApplication(t, replacePatterns, fdatGetter));
            }

            return candidates;
        }

        void btnApplyCustomPattern_Click(object sender, EventArgs e)
        {
            // default 는 discard 이므로, replaceString 이 "" 가 됨
            var replacePattern = ReplacePattern.Create("NoName", tbCustomPattern.Text, "", "");
            NumChanged = applyPatterns([replacePattern], withUI: sender != null);
        }

        public void btnApplyAllPatterns_Click(object sender, EventArgs e)
        {
            var replacePatterns = _patterns.Select(ReplacePattern.FromPattern).ToArray();
            NumChanged = applyPatterns(replacePatterns, withUI:sender != null);
        }
    }
}