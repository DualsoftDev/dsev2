namespace Plc2DsApp.Forms
{
	public partial class FormTags: DevExpress.XtraEditors.XtraForm
	{
        PlcTagBaseFDA[] _tags = null;
        // 선택 상태를 저장하는 Dictionary (선택 정보 관리)
        HashSet<PlcTagBaseFDA> _selectedTags = new();
        public PlcTagBaseFDA[] SelectedTags => _selectedTags.ToArray();
        public GridView GridView => gridView1;
        string _usageHint = null;
        public FormTags(IEnumerable<PlcTagBaseFDA> tags, IEnumerable<PlcTagBaseFDA> selectedTags = null, string usageHint = null, string selectionColumnCaption = null)
        {
            InitializeComponent();

            _tags = tags.ToArray();
            _usageHint = usageHint;
            // PlcTagBaseFDA[] 를 GridView 에서 보기 위해서 최종 subclass type (e.g LS.PlcTagInfo[]) 으로 변환
            var vendorTags = FormMain.Instance.ConvertToVendorTags(_tags);
            gridControl1.DataSource = new BindingList<object>(vendorTags as object[]);


            gridView1.OptionsSelection.MultiSelect = true;
            gridView1.OptionsSelection.MultiSelectMode = GridMultiSelectMode.RowSelect;

            gridView1.EnableColumnSearch();


            _selectedTags = new HashSet<PlcTagBaseFDA>(selectedTags ?? Array.Empty<PlcTagBaseFDA>());

            Text = $"{usageHint} Tags: {_tags.Count()}";

            gridView1.ApplyVisibleColumns(FormMain.Instance.VisibleColumns);

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


        private void btnSaveTagsAs_Click(object sender, EventArgs e) => FormMain.Instance.SaveTagsAs(_tags);

        private void btnMasterDetailView_Click(object sender, EventArgs e)
        {
            new FormFDAMasterDetail(_tags).ShowDialog();
        }
    }
}