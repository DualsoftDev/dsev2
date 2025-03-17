namespace Plc2DsApp.Forms
{
	public partial class FormExtractFDA: DevExpress.XtraEditors.XtraForm
	{
        PlcTagBaseFDA[] tags = [];
        PlcTagBaseFDA[] tagsNotYet => tags.Where(t => t.Choice == Choice.Undefined).ToArray();
        PlcTagBaseFDA[] tagsCategorized = [];
        void updateUI()
        {
            tbNumTagsAll.Text = tags.Length.ToString();
            tbNumTagsChosen.Text = tagsCategorized.Length.ToString();
            tbNumTagsNotyet.Text = tagsNotYet.Length.ToString();
        }
        void showTags(PlcTagBaseFDA[] tags) => FormTags.ShowTags(tags);
        public FormExtractFDA(PlcTagBaseFDA[] tags, Pattern[] patterns)
		{
            InitializeComponent();

            this.tags = tags;

            gridControl1.DataSource = patterns;
            tbPattern.Text = patterns[0].PatternString;     // 일단 맨처음거 아무거나..
        }

        private void FormExtractFDA_Load(object sender, EventArgs e)
        {
            btnShowAllTags.Click += (s, e) => showTags(tags);
            btnShowNotyetTags.Click += (s, e) => showTags(tagsNotYet);
            btnShowChosenTags.Click += (s, e) => showTags(tagsCategorized);
            updateUI();
        }

        private void btnApply_Click(object sender, EventArgs e)
        {
            var pattern = new Regex(tbPattern.Text, RegexOptions.Compiled);
            IEnumerable<PlcTagBaseFDA> collectCategorized()
            {
                foreach (var t in tagsNotYet)
                {
                    var match = pattern.Match(t.CsGetName());
                    if (match.Success)
                    {
                        // match group 의 이름 중에 flow, device, action 을 찾아서 t 에 저장
                        t.FlowName = match.Groups["flow"].Value;
                        t.DeviceName = match.Groups["device"].Value;
                        t.ActionName = match.Groups["action"].Value;
                        t.Choice = Choice.Fixed;
                        yield return t;
                    }
                }
            }
            tagsCategorized = tagsCategorized.Concat(collectCategorized()).ToArray();
            var form = new FormTags(tagsCategorized);
            form.ShowDialog();
        }
    }
}