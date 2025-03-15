namespace Plc2DsApp
{
	public partial class FormGridTags: DevExpress.XtraEditors.XtraForm
	{
        public FormGridTags(LS.PlcTagInfo[] tags, bool confirmMode=false)
		{
            InitializeComponent();

            ucDxGrid1.DataSource = tags;
            if (confirmMode)
            {
                btnOK.Text = "Accept";
                btnCancel.Text = "Reject";
            }

            btnOK    .Click += (s, e) => { Close(); DialogResult = DialogResult.OK; };
            btnCancel.Click += (s, e) => { Close(); DialogResult = DialogResult.Cancel; };
        }

        private void FormGridTags_Load(object sender, EventArgs e)
        {

        }

        public static void ShowTags(LS.PlcTagInfo[] tags)
        {
            var form = new FormGridTags(tags);
            form.ShowDialog();
        }

    }
}