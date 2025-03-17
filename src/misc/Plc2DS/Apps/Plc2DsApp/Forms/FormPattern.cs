
namespace Plc2DsApp.Forms
{
	public partial class FormPattern: DevExpress.XtraEditors.XtraForm
	{
        PlcTagBaseFDA[] _tags = [];
        PlcTagBaseFDA[] _tagsNotYet = [];
        public PlcTagBaseFDA[] TagsChosen = [];
        void updateUI()
        {
            tbNumTagsAll.Text = _tags.Length.ToString();
            tbNumTagsChosen.Text = TagsChosen.Length.ToString();
            tbNumTagsNotyet.Text = _tagsNotYet.Length.ToString();
        }
        void showTags(PlcTagBaseFDA[] tags) => FormGridTags.ShowTags(tags);

        public FormPattern(PlcTagBaseFDA[] tags, Pattern[] patterns)
		{
            InitializeComponent();

            _tags = tags;
            _tagsNotYet = tags;

            gridControl1.DataSource = patterns;

            void applyPatterns (Regex[] patterns)
            {
                var gr = _tagsNotYet.GroupByToDictionary(t => patterns.Any(p => p.IsMatch(t.CsGetName())));

                if (gr.ContainsKey(true))
                {
                    var form = new FormGridTags(gr[true], selectedTags:gr[true], confirmMode:true) { Text = "Confirm selection.." };
                    if (DialogResult.OK == form.ShowDialog())
                    {
                        TagsChosen = TagsChosen.Concat(gr[true]).ToArray();
                        _tagsNotYet = _tagsNotYet.Except(TagsChosen).ToArray();
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
            btnShowAllTags.Click += (s, e) => showTags(_tags);
            btnShowNotyetTags.Click += (s, e) => showTags(_tagsNotYet);
            btnShowChosenTags.Click += (s, e) => showTags(TagsChosen);
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