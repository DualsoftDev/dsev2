

using DevExpress.XtraEditors.Repository;
using DevExpress.XtraGrid.Columns;

using System.Collections.Generic;

namespace Plc2DsApp
{
	public partial class FormGridTags: DevExpress.XtraEditors.XtraForm
	{
        // 선택 상태를 저장하는 Dictionary (선택 정보 관리)
        HashSet<LS.PlcTagInfo> _selectedTags = new();
        public LS.PlcTagInfo[] SelectedTags => _selectedTags.ToArray();
        public FormGridTags(LS.PlcTagInfo[] tags, LS.PlcTagInfo[] selectedTags = null, string[] visibleFields = null, bool confirmMode = false, string selectionColumnCaption = null)
        {
            InitializeComponent();

            gridControl1.DataSource = tags;
            gridView1.OptionsSelection.MultiSelect = true;
            gridView1.OptionsSelection.MultiSelectMode = GridMultiSelectMode.RowSelect;

            _selectedTags = new HashSet<LS.PlcTagInfo>(selectedTags ?? Array.Empty<LS.PlcTagInfo>());
            if (confirmMode)
            {
                btnOK.Text = "Accept";
                btnCancel.Text = "Reject";
            }


            if (visibleFields != null)
            {
                foreach (GridColumn column in gridView1.Columns)
                {
                    column.Visible = visibleFields.Contains(column.FieldName);
                }
            }

            if (selectedTags.NonNullAny())
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
                    var tag = e.Row as LS.PlcTagInfo;
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

                view.RowCellClick += (sender, e) =>
                {
                    if (e.Column.FieldName == "IsChecked") // 체크박스 컬럼인지 확인
                    {
                        bool currentValue = (bool)gridView1.GetRowCellValue(e.RowHandle, e.Column);
                        bool newValue = !currentValue; // 현재 값의 반대 값으로 설정 (토글)

                        // 선택된 모든 행 가져오기
                        var selectedRows = gridView1.GetSelectedRows();

                        foreach (int rowIndex in selectedRows)
                        {
                            // 선택된 행에 동일한 값 설정
                            gridView1.SetRowCellValue(rowIndex, e.Column, newValue);

                            // ✅ selectedTags도 함께 업데이트
                            var tag = gridView1.GetRow(rowIndex) as LS.PlcTagInfo;
                            if (tag != null)
                            {
                                if (newValue)
                                    _selectedTags.Add(tag);
                                else
                                    _selectedTags.Remove(tag);
                            }
                        }
                    }
                };
            }

            btnOK.Click += (s, e) => { Close(); DialogResult = DialogResult.OK; };
            btnCancel.Click += (s, e) => { Close(); DialogResult = DialogResult.Cancel; };
        }

        private void FormGridTags_Load(object sender, EventArgs e)
        {

        }

        public static FormGridTags ShowTags(LS.PlcTagInfo[] tags, string selectionColumnCaption = null)
        {
            var form = new FormGridTags(tags, selectionColumnCaption: selectionColumnCaption);
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