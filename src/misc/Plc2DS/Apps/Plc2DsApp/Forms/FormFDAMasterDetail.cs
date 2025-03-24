namespace Plc2DsApp.Forms
{
	public partial class FormFDAMasterDetail: DevExpress.XtraEditors.XtraForm
	{
        PlcTagBaseFDA[] _tags = [];
        List<MasterItem> masterList; // Master 데이터를 담을 리스트
        public FormFDAMasterDetail(PlcTagBaseFDA[] tags)
		{
            InitializeComponent();
            _tags = tags;
            doMasterDetailView();
            gridView1.DoDefaultSettings();

            cbShowSingletonToo.CheckedChanged += (s, e) => showSingletonToo();
            showSingletonToo();

            btnOK.Click += (s, e) => { Close(); DialogResult = DialogResult.OK; };
            btnCancel.Click += (s, e) => { Close(); DialogResult = DialogResult.Cancel; };
        }

        void showSingletonToo() => gridView1.ActiveFilterString = cbShowSingletonToo.Checked ? "" : "[Count] > 1";

        void doMasterDetailView()
        {
            // 2️⃣ Master 데이터 생성 (FlowName + DeviceName 기준 그룹화)
            masterList = _tags
                .GroupBy(t => new { t.FlowName, t.DeviceName })
                .Select(g => new MasterItem()
                {
                    FlowName = g.Key.FlowName,
                    DeviceName = g.Key.DeviceName,
                    Details = g.ToList(),
                    Count = g.Count() // 개수 계산
                })
                .ToList();

            // 3️⃣ Master 데이터 바인딩
            gridControl1.DataSource = masterList;

            // 4️⃣ Master-Detail 설정
            gridControl1.LevelTree.Nodes.Add("DetailView", gridView2);
            gridView1.OptionsDetail.EnableMasterViewMode = true;

            gridView1.MasterRowGetChildList += gridView1_MasterRowGetChildList;
            gridView1.MasterRowExpanded += gridView1_MasterRowExpanded;
            gridView1.MasterRowGetRelationName += (s, e) => e.RelationName = "DetailView";
            gridView1.MasterRowGetRelationCount += (s, e) => e.RelationCount = 1;
            gridView1.AddUnboundColumnCustom<MasterItem, string>("Actions", m => m.Details.Select(t => t.ActionName).JoinString(", "), null);

            gridView2.OptionsView.ShowGroupPanel = false;
            gridView1.EnableColumnSearch();

        }

        void gridView1_MasterRowExpanded(object sender, CustomMasterRowEventArgs e)
        {
            GridView detailView = gridView1.GetDetailView(e.RowHandle, 0) as GridView;
            detailView.ApplyVisibleColumns(FormMain.Instance.VisibleColumns);
            detailView.EnableColumnSearch();
        }

        void gridView1_MasterRowGetChildList(object sender, MasterRowGetChildListEventArgs e)
        {
            var flowName = gridView1.GetRowCellValue(e.RowHandle, "FlowName").ToString();
            var deviceName = gridView1.GetRowCellValue(e.RowHandle, "DeviceName").ToString();

            var fdTags =
                _tags
                .Where(t => t.FlowName == flowName && t.DeviceName == deviceName)
                .OrderBy(t => t.ActionName)
                ;

            object[] vendorTags = FormMain.Instance.ConvertToVendorTags(fdTags) as object[];
            e.ChildList = vendorTags;
        }
    }

    class MasterItem
    {
        public string FlowName { get; set; } = "";
        public string DeviceName { get; set; } = "";
        [Browsable(false)]
        public List<PlcTagBaseFDA> Details { get; set; }
        public int Count { get; set; }
    }
}

