using System.Threading.Tasks;
using System.Windows.Forms;

namespace Plc2DsApp.Forms
{
	public partial class FormPattern: DevExpress.XtraEditors.XtraForm
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

        public FormPattern(PlcTagBaseFDA[] tags, Pattern[] patterns, bool withUI)
		{
            InitializeComponent();

            _patterns = patterns;
            _tags = tags;
            _tagsStage = tags;

            if (!withUI)
            {
                this.MakeHiddenSelfOK();
                this.ApplyAllPatterns(false);
            }



            gridControl1.DataSource = patterns;

            gridView1.AddActionColumn<Pattern>("Apply", p =>
            {
                return ("Apply", new Action<Pattern>(p =>
                {
                    var pattern = new Regex(p.PatternString, RegexOptions.Compiled);
                    applyPatterns(new Regex[] { pattern }, withUI);
                }));
            });
            gridView1.SelectionChanged += (s, e) =>
            {
                var pattern = gridView1.GetFocusedRow() as Pattern;
                tbCustomPattern.Text = pattern.PatternString;
            };

            gridView1.ApplyVisibleColumns([nameof(Pattern.Name), nameof(Pattern.PatternString), "Relacement", nameof(Pattern.Description)]);

            if (withUI)
            {
                Task.Run(() =>
                {
                    //var dict = new Dictionary<PlcTagBaseFDA, int>();
                    var dict = patterns.ToDictionary(p => p, p => ApplyPatterns(tags, [p]).Length);
                    this.Do(() =>
                    {
                        gridView1.AddUnboundColumnCustom<Pattern, int>("NumMatches", p => dict[p], null);
                    });
                });
            }



            btnOK.Click += (s, e) => { Close(); DialogResult = DialogResult.OK; };
            btnCancel.Click += (s, e) => { Close(); DialogResult = DialogResult.Cancel; };
        }
        public static PlcTagBaseFDA[] ApplyPatterns(PlcTagBaseFDA[] tags, Regex[] patterns)
        {
            var gr = tags.GroupByToDictionary(t => patterns.Any(p => p.IsMatch(t.CsGetName())));
            return gr.ContainsKey(true) ? gr[true] : [];
        }
        public static PlcTagBaseFDA[] ApplyPatterns(PlcTagBaseFDA[] tags, Pattern[] patterns)
        {
            var regexPatterns = patterns.Select(p => new Regex(p.PatternString, RegexOptions.Compiled)).ToArray();
            return ApplyPatterns(tags, regexPatterns);
        }

        void FormPattern_Load(object sender, EventArgs e)
        {
            btnShowAllTags.Click += (s, e) => showTags(_tags, usageHint:"(All Tags)");
            btnShowStageTags.Click += (s, e) => showTags(_tagsStage, usageHint: "(Stage Tags)");
            btnShowChosenTags.Click += (s, e) => showTags(TagsChosen, usageHint: "(Chosen Tags)");
            updateUI();
        }
        void applyPatterns(Regex[] patterns, bool withUI)
        {
            var chosens = ApplyPatterns(_tagsStage, patterns);
            if (chosens.Any())
            {
                var form = new FormTags(chosens, selectedTags: chosens, usageHint: "(Pattern matching)", withUI:withUI);
                form.ShowDialog();

                TagsChosen = TagsChosen.Concat(chosens).ToArray();
                _tagsStage = _tagsStage.Except(TagsChosen).ToArray();
                updateUI();
            }
        }

        public void ApplyAllPatterns(bool withUI)
        {
            var patterns = _patterns.Select(p => new Regex(p.PatternString, RegexOptions.Compiled)).ToArray();
            applyPatterns(patterns, withUI);
        }
        void btnApplyCustomPattern_Click(object sender, EventArgs e)
        {
            var pattern = new Regex(tbCustomPattern.Text, RegexOptions.Compiled);
            applyPatterns([pattern], true);
        }

        void btnApplyAllPatterns_Click(object sender, EventArgs e) => ApplyAllPatterns(true);    }
}