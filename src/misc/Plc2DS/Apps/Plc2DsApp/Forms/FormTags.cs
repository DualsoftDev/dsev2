using DevExpress.Utils.Extensions;

using System.ComponentModel;

namespace Plc2DsApp.Forms
{
	public partial class FormTags: DevExpress.XtraEditors.XtraForm
	{
        // 선택 상태를 저장하는 Dictionary (선택 정보 관리)
        HashSet<PlcTagBaseFDA> _selectedTags = new();
        public PlcTagBaseFDA[] SelectedTags => _selectedTags.ToArray();
        string _usageHint = null;
        public FormTags(IEnumerable<PlcTagBaseFDA> tags, IEnumerable<PlcTagBaseFDA> selectedTags = null, string usageHint = null, string selectionColumnCaption = null)
        {
            InitializeComponent();

            _usageHint = usageHint;
            // PlcTagBaseFDA[] 를 GridView 에서 보기 위해서 최종 subclass type (e.g LS.PlcTagInfo[]) 으로 변환
            var vendorTags = FormMain.Instance.ConvertToVendorTags(tags);
            gridControl1.DataSource = new BindingList<object>(vendorTags as object[]);


            gridView1.OptionsSelection.MultiSelect = true;
            gridView1.OptionsSelection.MultiSelectMode = GridMultiSelectMode.RowSelect;

            gridView1.OptionsView.ShowAutoFilterRow = true;

            // 다중 column sorting 기능 지원.  Flow 로 먼저 sorting 하고, flow 내 device 로 sorting
            // Shift + 클릭으로 다중 컬럼 정렬 가능
            // Ctrl + 클릭으로 특정 컬럼 정렬 해제 가능
            gridView1.OptionsCustomization.AllowSort = true;  // 사용자가 정렬 가능
            gridView1.OptionsCustomization.AllowFilter = true; // 필터링도 허용
            gridView1.OptionsCustomization.AllowColumnMoving = true; // 컬럼 이동 가능
            gridView1.OptionsCustomization.AllowGroup = true; // 그룹핑 가능
            gridView1.OptionsCustomization.AllowQuickHideColumns = true; // 빠른 숨기기 기능

            _selectedTags = new HashSet<PlcTagBaseFDA>(selectedTags ?? Array.Empty<PlcTagBaseFDA>());

            Text = $"{usageHint} Tags: {tags.Count()}";

            if (FormMain.Instance.VisibleColumns.NonNullAny())
            {
                var visibles = new HashSet<string>(FormMain.Instance.VisibleColumns); // 빠른 검색을 위한 HashSet

                foreach (GridColumn column in gridView1.Columns)
                {
                    column.Visible = visibles.Contains(column.FieldName);
                    if (column.Visible)
                        column.VisibleIndex = Array.IndexOf(FormMain.Instance.VisibleColumns, column.FieldName);
                }
            }

            //if (selectedTags.NonNullAny())
            {
                GridView view = gridView1;
                // 체크박스 열 추가
                GridColumn colCheck = new GridColumn();
                colCheck.FieldName = "IsChecked"; // 가상의 필드명
                colCheck.Caption = selectionColumnCaption; // 컬럼 헤더 텍스트
                colCheck.Visible = true;
                colCheck.UnboundType = DevExpress.Data.UnboundColumnType.Boolean; // Boolean 타입으로 설정
                                                                                  // 체크박스를 위한 RepositoryItemCheckEdit 추가
                                                                                  // 컬럼을 GridView에 추가
                view.Columns.Add(colCheck);

                //RepositoryItemCheckEdit checkEdit = new RepositoryItemCheckEdit();
                RepositoryItemCheckEdit checkEdit = colCheck.ApplyImmediateCheckBox();
                gridControl1.RepositoryItems.Add(checkEdit);
                colCheck.ColumnEdit = checkEdit;

                // Unbound 값 제공을 위해 CustomUnboundColumnData 이벤트 핸들링
                view.CustomUnboundColumnData += (sender, e) =>
                {
                    var tag = e.Row as PlcTagBaseFDA;
                    if (tag == null) return; // null 방지

                    if (e.Column.FieldName == "IsChecked" && e.IsGetData)
                    {
                        // 선택된 태그 목록에 존재하는지 확인하여 체크 여부 설정
                        e.Value = _selectedTags.Contains(tag);
                    }
                    else if (e.Column.FieldName == "IsChecked" && e.IsSetData)
                    {
                        bool isChecked = (bool)e.Value;

                        if (isChecked)
                            _selectedTags.Add(tag); // 선택된 경우 추가
                        else
                            _selectedTags.Remove(tag); // 해제된 경우 제거
                    }
                };

                view.MakeCheckableMultiRows<PlcTagBaseFDA>(_selectedTags, new string[] { "IsChecked" });
                view.MakeEditableMultiRows<string>(new string[] { "FlowName", "DeviceName", "ActionName" });
                view.MakeEditableMultiRows<Choice>(new string[] { "Choice" });
            }

            btnOK.Click += (s, e) => { Close(); DialogResult = DialogResult.OK; };
            btnCancel.Click += (s, e) => { Close(); DialogResult = DialogResult.Cancel; };
        }
        private void FormGridTags_Load(object sender, EventArgs e)
        {

        }

        public static FormTags ShowTags(IEnumerable<PlcTagBaseFDA> tags, IEnumerable<PlcTagBaseFDA> selectedTags = null, string selectionColumnCaption = null, string usageHint = null)
        {
            var form = new FormTags(tags, selectedTags:selectedTags, selectionColumnCaption: selectionColumnCaption, usageHint:usageHint);
            form.ShowDialog();
            return form;
        }

        private void btnSaveAs_Click(object sender, EventArgs e)
        {
            using SaveFileDialog sfd =
                new SaveFileDialog()
                {
                    //InitialDirectory = dataDir,
                    //Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                string json = EmJson.ToJson(SelectedTags);
                File.WriteAllText(sfd.FileName, json);
            }
        }
    }
}