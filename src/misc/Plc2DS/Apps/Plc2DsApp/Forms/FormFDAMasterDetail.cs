namespace Plc2DsApp.Forms
{
	public partial class FormFDAMasterDetail: DevExpress.XtraEditors.XtraForm
	{
        PlcTagBaseFDA[] _tags = [];
        List<dynamic> masterList; // Master 데이터를 담을 리스트
        public FormFDAMasterDetail(PlcTagBaseFDA[] tags)
		{
            InitializeComponent();
            _tags = tags;
            doMasterDetailView();

            cbShowSingletonToo.CheckedChanged += (s, e) => showSingletonToo();
            showSingletonToo();
        }

        void showSingletonToo() => gridView1.ActiveFilterString = cbShowSingletonToo.Checked ? "" : "[Count] > 1";

        private void doMasterDetailView()
        {
            // 2️⃣ Master 데이터 생성 (FlowName + DeviceName 기준 그룹화)
            masterList = _tags
                .GroupBy(t => new { t.FlowName, t.DeviceName })
                .Select(g => new
                {
                    FlowName = g.Key.FlowName,
                    DeviceName = g.Key.DeviceName,
                    Count = g.Count() // 개수 계산
                })
                .ToList<object>();

            // 3️⃣ Master 데이터 바인딩
            gridControl1.DataSource = masterList;

            // 4️⃣ Master-Detail 설정
            gridControl1.LevelTree.Nodes.Add("DetailView", gridView2);
            gridView1.OptionsDetail.EnableMasterViewMode = true;
            gridView1.OptionsBehavior.Editable = false;

            gridView1.MasterRowGetChildList += gridView1_MasterRowGetChildList;
            gridView1.MasterRowExpanded += gridView1_MasterRowExpanded;
            gridView1.MasterRowGetRelationName += (s, e) => e.RelationName = "DetailView";
            gridView1.MasterRowGetRelationCount += (s, e) => e.RelationCount = 1;


            gridView2.OptionsView.ShowGroupPanel = false;
            gridView2.OptionsBehavior.Editable = false;
        }

        void gridView1_MasterRowExpanded(object sender, CustomMasterRowEventArgs e)
        {
            GridView detailView = gridView1.GetDetailView(e.RowHandle, 0) as GridView;
            detailView.ApplyVisibleColumns(FormMain.Instance.VisibleColumns);
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
}

