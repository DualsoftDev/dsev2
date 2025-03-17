
namespace Plc2DsApp.Forms
{
	public partial class FormPattern: DevExpress.XtraEditors.XtraForm
	{
        PlcTagBaseFDA[] _tags = [];
        PlcTagBaseFDA[] _tagsStage = [];
        public PlcTagBaseFDA[] TagsChosen = [];
        void updateUI()
        {
            tbNumTagsAll.Text = _tags.Length.ToString();
            tbNumTagsChosen.Text = TagsChosen.Length.ToString();
            tbNumTagsStage.Text = _tagsStage.Length.ToString();
        }
        void showTags(PlcTagBaseFDA[] tags, string usageHint = null) => FormTags.ShowTags(tags, usageHint: usageHint);

        public FormPattern(PlcTagBaseFDA[] tags, Pattern[] patterns)
		{
            InitializeComponent();

            _tags = tags;
            _tagsStage = tags;

            gridControl1.DataSource = patterns;

            void applyPatterns (Regex[] patterns)
            {
                var gr = _tagsStage.GroupByToDictionary(t => patterns.Any(p => p.IsMatch(t.CsGetName())));

                if (gr.ContainsKey(true))
                {
                    var form = new FormTags(gr[true], selectedTags:gr[true], usageHint:"(Pattern matching)");
                    if (DialogResult.OK == form.ShowDialog())
                    {
                        TagsChosen = TagsChosen.Concat(gr[true]).ToArray();
                        _tagsStage = _tagsStage.Except(TagsChosen).ToArray();
                        updateUI();
                    }
                }
            }
            gridView1.AddActionColumn<Pattern>("Apply", p =>
            {
                return ("Apply", new Action<Pattern>(p =>
                {
                    var pattern = new Regex(p.PatternString, RegexOptions.Compiled);
                    applyPatterns(new Regex[] { pattern });
                }));
            });
            btnOK    .Click += (s, e) => { Close(); DialogResult = DialogResult.OK; };
            btnCancel.Click += (s, e) => { Close(); DialogResult = DialogResult.Cancel; };
            btnApplyAllPatterns.Click += (s, e) =>
            {
                var regexPatterns = patterns.Select(p => new Regex(p.PatternString, RegexOptions.Compiled)).ToArray();
                applyPatterns(regexPatterns);
            };
        }

        private void FormPattern_Load(object sender, EventArgs e)
        {
            btnShowAllTags.Click += (s, e) => showTags(_tags, usageHint:"(All Tags)");
            btnShowStageTags.Click += (s, e) => showTags(_tagsStage, usageHint: "(Stage Tags)");
            btnShowChosenTags.Click += (s, e) => showTags(TagsChosen, usageHint: "(Chosen Tags)");
            updateUI();
        }
    }

    public class Pattern
    {
        public string Name { get; set; }
        public string PatternString { get; set; }
        public string Description { get; set; }
    }
}