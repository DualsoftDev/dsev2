using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DevExpress.XtraEditors;

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
            LoadData();
            gridView1.MasterRowGetChildList += gridView1_MasterRowGetChildList;
        }

        private void LoadData()
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
            gridView1.MasterRowGetRelationName += (s, e) => e.RelationName = "DetailView";
            gridView1.MasterRowGetRelationCount += (s, e) => e.RelationCount = 1;

            gridView2.OptionsView.ShowGroupPanel = false;
            gridView2.OptionsBehavior.Editable = false;
        }

        private void gridView1_MasterRowGetChildList(object sender, MasterRowGetChildListEventArgs e)
        {
            var flowName = gridView1.GetRowCellValue(e.RowHandle, "FlowName").ToString();
            var deviceName = gridView1.GetRowCellValue(e.RowHandle, "DeviceName").ToString();

            e.ChildList = _tags
                .Where(t => t.FlowName == flowName && t.DeviceName == deviceName)
                .OrderBy(t => t.ActionName)
                .ToList();
        }
    }
}


/*
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using DevExpress.XtraGrid.Views.Grid;

public partial class MainForm : Form
{
    private List<TagData> tagDataList;
    private List<dynamic> masterList; // Master 데이터를 담을 리스트

    public MainForm()
    {
        InitializeComponent();
        LoadData();
    }

    private void LoadData()
    {
        // 1️⃣ 전체 데이터 생성 (하나의 클래스에서 관리)
        tagDataList = new List<TagData>
        {
            new TagData { Variable = "BB_LINE_M_...", DataType = "BOOL", FlowName = "BB", DeviceName = "LINE_TOTAL", ActionName = "ERR", IsChecked = false },
            new TagData { Variable = "BB_LINE_RBT...", DataType = "BOOL", FlowName = "BB", DeviceName = "LINE_RBT", ActionName = "RST", IsChecked = false },
            new TagData { Variable = "BB_LINE_WELD...", DataType = "BOOL", FlowName = "BB", DeviceName = "LINE_WELD", ActionName = "BYPASS", IsChecked = false },
            new TagData { Variable = "BB_MAIN_BATTERY", DataType = "BOOL", FlowName = "BB", DeviceName = "MAIN_BATTERY", ActionName = "ERR", IsChecked = false },
            new TagData { Variable = "BB_MCP_SSP6", DataType = "BOOL", FlowName = "BB", DeviceName = "MCP_SSP6", ActionName = "SEL", IsChecked = false },
            new TagData { Variable = "BB_M_ALL_GATE", DataType = "BOOL", FlowName = "BB", DeviceName = "ALL_GATE_B", ActionName = "AUX", IsChecked = false },
            new TagData { Variable = "BB_M_AUTO", DataType = "BOOL", FlowName = "BB", DeviceName = "AUTO_START", ActionName = "AUX", IsChecked = false }
        };

        // 2️⃣ Master 데이터 생성 (FlowName + DeviceName 기준 그룹화)
        masterList = tagDataList
            .GroupBy(t => new { t.FlowName, t.DeviceName })
            .Select(g => new
            {
                FlowName = g.Key.FlowName,
                DeviceName = g.Key.DeviceName,
                Count = g.Count() // 개수 계산
            })
            .ToList();

        // 3️⃣ Master 데이터 바인딩
        gridControl1.DataSource = masterList;

        // 4️⃣ Master-Detail 설정
        gridControl1.LevelTree.Nodes.Add("DetailView", gridView2);
        gridView1.OptionsDetail.EnableMasterViewMode = true;
        gridView1.OptionsBehavior.Editable = false;

        gridView1.MasterRowGetChildList += GridView1_MasterRowGetChildList;
        gridView1.MasterRowGetRelationName += (s, e) => e.RelationName = "DetailView";
        gridView1.MasterRowGetRelationCount += (s, e) => e.RelationCount = 1;

        gridView2.OptionsView.ShowGroupPanel = false;
        gridView2.OptionsBehavior.Editable = false;
    }

    private void GridView1_MasterRowGetChildList(object sender, MasterRowGetChildListEventArgs e)
    {
        var flowName = gridView1.GetRowCellValue(e.RowHandle, "FlowName").ToString();
        var deviceName = gridView1.GetRowCellValue(e.RowHandle, "DeviceName").ToString();

        e.ChildList = tagDataList
            .Where(t => t.FlowName == flowName && t.DeviceName == deviceName)
            .ToList();
    }
}



 */