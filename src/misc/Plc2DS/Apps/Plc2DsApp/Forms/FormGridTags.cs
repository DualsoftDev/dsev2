

using DevExpress.XtraEditors.Repository;
using DevExpress.XtraGrid.Columns;

using System.Collections.Generic;

namespace Plc2DsApp
{
	public partial class FormGridTags: DevExpress.XtraEditors.XtraForm
	{
        // 선택 상태를 저장하는 Dictionary (선택 정보 관리)
        HashSet<LS.PlcTagInfo> selectedTags = new();
        public LS.PlcTagInfo[] SelectedTags => selectedTags.ToArray();
        public FormGridTags(LS.PlcTagInfo[] tags, bool confirmMode=false, string selectionColumnCaption=null)
		{
            InitializeComponent();

            gridControl1.DataSource = tags;
            gridView1.OptionsSelection.MultiSelect = true;
            gridView1.OptionsSelection.MultiSelectMode = GridMultiSelectMode.RowSelect;

            if (confirmMode)
            {
                btnOK.Text = "Accept";
                btnCancel.Text = "Reject";
            }

            if (selectionColumnCaption.NonNullAny())
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
                        e.Value = selectedTags.Contains(tag);
                    }
                    else if (e.Column.FieldName == "IsChecked" && e.IsSetData)
                    {
                        bool isChecked = (bool)e.Value;

                        if (isChecked)
                            selectedTags.Add(tag); // 선택된 경우 추가
                        else
                            selectedTags.Remove(tag); // 해제된 경우 제거
                    }
                };

                //view.CellValueChanged += (sender, e) =>
                //{
                //    // 특정 컬럼에서 변경이 발생했는지 확인
                //    if (e.Column.FieldName == "IsChecked")
                //    {
                //        bool newValue = (bool)e.Value; // 변경된 값 가져오기

                //        // 선택된 모든 행 가져오기
                //        var selectedRows = gridView1.GetSelectedRows();

                //        foreach (int rowIndex in selectedRows)
                //        {
                //            // 선택된 행에 동일한 값 설정
                //            gridView1.SetRowCellValue(rowIndex, e.Column, newValue);
                //        }
                //    }
                //};

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
                                    selectedTags.Add(tag);
                                else
                                    selectedTags.Remove(tag);
                            }
                        }
                    }
                };
            }

            btnOK    .Click += (s, e) => { Close(); DialogResult = DialogResult.OK; };
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

    }
}