using DevExpress.XtraEditors;

namespace Plc2DsApp.Forms
{
	public partial class FormExtractFDA: DevExpress.XtraEditors.XtraForm
	{
        PlcTagBaseFDA[] _tags = [];
        public PlcTagBaseFDA[] TagsStage => _tags.Where(t => t.Choice == Choice.Stage).ToArray();
        public PlcTagBaseFDA[] TagsNonStage => _tags.Where(t => t.Choice != Choice.Stage).ToArray();    // chosen + categorized
        void updateUI()
        {
            tbNumTagsAll.Text = _tags.Length.ToString();
            tbNumTagsNonStage.Text = TagsNonStage.Length.ToString();
            tbNumTagsStage.Text = TagsStage.Length.ToString();
            tbNumTagsCategorized.Text = _tags.Where(t => t.Choice == Choice.Categorized).Count().ToString();
        }
        void showTags(PlcTagBaseFDA[] tags) => FormTags.ShowTags(tags);
        public FormExtractFDA(PlcTagBaseFDA[] tags, Pattern[] patterns)
		{
            InitializeComponent();

            this._tags = tags;

            gridControl1.DataSource = patterns;
            tbPattern.Text = patterns[0].PatternString;     // 일단 맨처음거 아무거나..

            gridView1.SelectionChanged += (s, e) =>
            {
                var pattern = gridView1.GetFocusedRow() as Pattern;
                tbPattern.Text = pattern.PatternString;
            };

            btnOK.Click += (s, e) => { Close(); DialogResult = DialogResult.OK; };
            btnCancel.Click += (s, e) => { Close(); DialogResult = DialogResult.Cancel; };
        }

        private void FormExtractFDA_Load(object sender, EventArgs e)
        {
            btnShowAllTags.Click += (s, e) => showTags(_tags);
            btnShowStageTags.Click += (s, e) => showTags(TagsStage);
            btnShowChosenTags.Click += (s, e) => showTags(TagsNonStage);
            updateUI();
        }

        private void btnApply_Click(object sender, EventArgs e)
        {
            var pattern = new Regex(tbPattern.Text, RegexOptions.Compiled);
            IEnumerable<PlcTagBaseFDA> collectCategorized()
            {
                foreach (var t in TagsStage)
                {
                    var match = pattern.Match(t.CsGetName());
                    if (match.Success)
                    {
                        // match group 의 이름 중에 flow, device, action 을 찾아서 t 에 저장
                        t.FlowName = match.Groups["flow"].Value;
                        t.DeviceName = match.Groups["device"].Value;
                        t.ActionName = match.Groups["action"].Value;
                        //t.Choice = Choice.Categorized;
                        yield return t;
                    }
                }
            }
            var categorizedCandidates = collectCategorized().ToArray();
            var form = FormTags.ShowTags(categorizedCandidates, categorizedCandidates);
            if (form.DialogResult == DialogResult.OK)
            {
                form.SelectedTags.Where(t => t.Choice == Choice.Stage).Iter(t => t.Choice = Choice.Categorized);
                updateUI();
            }
        }
    }
}