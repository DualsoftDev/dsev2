using Microsoft.FSharp.Reflection;

using Plc2DsApp;

using System.Reactive.Disposables;

public static class GridExtension
{
    // T: 필드 타입
    public static void MakeEditableMultiRows<T>(this GridView gridView, string[] fields)
    {
        gridView.RowCellClick += (sender, e) =>
        {
            if (Keyboard.IsShiftKeyPressed || Keyboard.IsControlKeyPressed) return;

            if (!fields.Contains(e.Column.FieldName)) return; // 지정된 필드만 처리

            // 현재 값 가져오기 (T가 참조형이면 as 사용, 값형이면 Convert.ChangeType 사용)
            var selectedRows = gridView.GetSelectedRows();

            object cellValue = gridView.GetRowCellValue(e.RowHandle, e.Column);
            T currentValue = cellValue is T ? (T)cellValue : default;

            tryGetNewValueViaPrompt(currentValue).Match(
                newValue =>
                {
                    foreach (int rowIndex in selectedRows)
                        gridView.SetRowCellValue(rowIndex, e.Column, newValue);
                },
                () => { });
        };
    }

    // T: 클래스 타입
    public static void MakeCheckableMultiRows<T>(this GridView gridView, HashSet<T> selectedItems, string[] checkableFields)
        where T : class // ✅ T가 클래스(참조형)일 경우에만 적용 가능하도록 제한
    {
        gridView.RowCellClick += (sender, e) =>
        {
            if (!checkableFields.Contains(e.Column.FieldName)) return; // 지정된 필드인지 확인

            // 선택된 모든 행 가져오기
            var selectedRows = gridView.GetSelectedRows();

            bool currentValue = (bool)gridView.GetRowCellValue(e.RowHandle, e.Column);
            bool newValue = !currentValue; // 반전


            foreach (int rowIndex in selectedRows)
            {
                gridView.SetRowCellValue(rowIndex, e.Column, newValue);

                // ✅ 선택된 아이템 리스트도 함께 업데이트
                var item = gridView.GetRow(rowIndex) as T;
                if (item != null)
                {
                    if (newValue)
                        selectedItems.Add(item);
                    else
                        selectedItems.Remove(item);
                }
            }
        };
    }


    static FSharpOption<T> tryGetNewValueViaPrompt<T>(T currentValue)
    {
        using (Form inputForm = new Form())
        {
            inputForm.Width = 400;
            inputForm.Height = 250;
            inputForm.Text = "새로운 값 입력";

            ComboBox comboBox = new ComboBox()
            {
                Left = 50,
                Top = 20,
                Width = 200,
                DropDownStyle = ComboBoxStyle.DropDownList // 🔹 드롭다운 리스트
            };

            Control inputControl;
            if (typeof(T).IsEnum) // 🔹 T가 Enum이면 ComboBox 사용
            {
                var enumValues = Enum.GetValues(typeof(T)).Cast<T>().ToList();
                comboBox.Items.AddRange(enumValues.Cast<object>().ToArray());
                comboBox.SelectedItem = currentValue; // 현재 값 선택
                inputControl = comboBox;
            }
            else if (FSharpType.IsUnion(typeof(T), null)) // 🔹 F# Union Type이면 ComboBox 사용
            {
                var unionCases =
                    FSharpType.GetUnionCases(typeof(T), null)
                        .Select(caseInfo => FSharpValue.MakeUnion(caseInfo, new object[0], null))
                        .Cast<T>()
                        .ToList();

                comboBox.Items.AddRange(unionCases.Cast<object>().ToArray());
                comboBox.SelectedItem = currentValue;
                inputControl = comboBox;
            }
            else // 🔹 일반 타입이면 TextBox 사용
            {
                TextBox textBox = new TextBox()
                {
                    Left = 50,
                    Top = 20,
                    Width = 200,
                    Text = currentValue?.ToString()
                };
                inputControl = textBox;
            }

            Button okButton = new Button() { Text = "확인", Left = 100, Width = 100, Top = 50, DialogResult = DialogResult.OK };

            inputForm.Controls.Add(inputControl);
            inputForm.Controls.Add(okButton);
            inputForm.AcceptButton = okButton;

            if (inputForm.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    if (inputControl is ComboBox combo)
                        return new FSharpOption<T>((T)combo.SelectedItem); // 🔹 Enum 변환
                    else if (inputControl is TextBox textBox)
                        return new FSharpOption<T>((T)Convert.ChangeType(textBox.Text, typeof(T))); // 🔹 일반 타입 변환
                }
                catch
                {
                    MessageBox.Show("잘못된 입력값입니다.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            return FSharpOption<T>.None; // 변경 없음
        }
    }

    /// <summary>
    /// Visible column 이름 목록에 포함된 column 만 순서대로 보이게 설정
    /// </summary>
    public static void ApplyVisibleColumns(this GridView gridView, string[] visibleColumnNames)
    {
        if (visibleColumnNames.NonNullAny())
        {
            foreach (GridColumn column in gridView.Columns)
            {
                column.Visible = visibleColumnNames.Contains(column.FieldName);
                if (column.Visible)
                    column.VisibleIndex = Array.IndexOf(visibleColumnNames, column.FieldName);
            }
        }
    }

    /// <summary>
    /// GridView 의 column 별 search 지원
    /// </summary>
    public static void EnableColumnSearch(this GridView gridView)
    {
        gridView.OptionsView.ShowAutoFilterRow = true;

        // 다중 column sorting 기능 지원.  Flow 로 먼저 sorting 하고, flow 내 device 로 sorting
        // Shift + 클릭으로 다중 컬럼 정렬 가능
        // Ctrl + 클릭으로 특정 컬럼 정렬 해제 가능
        gridView.OptionsCustomization.AllowSort = true;  // 사용자가 정렬 가능
        gridView.OptionsCustomization.AllowFilter = true; // 필터링도 허용
        gridView.OptionsCustomization.AllowColumnMoving = true; // 컬럼 이동 가능
        gridView.OptionsCustomization.AllowGroup = true; // 그룹핑 가능
        gridView.OptionsCustomization.AllowQuickHideColumns = true; // 빠른 숨기기 기능

    }


    public static void DoDefaultSettings(this GridView gridView)
    {
        gridView.BestFitColumns();
        gridView.EnsureMinimumColumnWidths(60, ["Count"]);
        gridView.EnsureMinimumColumnWidths(100, ["FlowName", "DeviceName", "ActionName"]);
        gridView.HideGroupPanel();
    }

    public static void EnsureMinimumColumnWidths(this GridView gridView, int minSize, string[] columns)
    {
        foreach (string columnName in columns)
        {
            GridColumn column = gridView.Columns.ColumnByFieldName(columnName);
            if (column != null)
            {
                if (column.Width < minSize)
                {
                    column.MinWidth = minSize;
                    column.Width = minSize;
                }
                else
                {
                    column.MinWidth = minSize;
                }
            }
        }
    }

    public static void Noop() {}
}


public static class FormExtension
{
    /// <summary>
    /// 생성된 Form 이 ShowDialog() 실행 시, 자동으로 dialog reusult OK 값 갖도록 설정
    /// <br/> - form 을 생성해서 OK button 누르는 것과 동일한 효과
    /// </summary>
    public static void MakeHiddenSelfOK(this Form form)
    {
        form.ShowInTaskbar = false;
        form.StartPosition = FormStartPosition.Manual;
        form.Opacity = 0;   // 완전 투명하게 해서 보이지 않게
        form.Load += (s, e) => form.DialogResult = DialogResult.OK;
    }

    public static DialogResult DoShow(this Form form) {
        if (Keyboard.IsShiftKeyPressed)
        {
            form.Show();
            return DialogResult.None;
        }
        else
            return form.ShowDialog();
    }
}
