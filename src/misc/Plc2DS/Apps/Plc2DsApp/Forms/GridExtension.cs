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

            TryGetNewValueViaPrompt(currentValue).Match(
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


    private static FSharpOption<T> TryGetNewValueViaPrompt<T>(T currentValue)
    {
        using (Form inputForm = new Form())
        {
            inputForm.Width = 400;
            inputForm.Height = 250;
            inputForm.Text = "새로운 값 입력";

            Control inputControl;
            if (typeof(T).IsEnum) // 🔹 T가 Enum이면 ComboBox 사용
            {
                ComboBox comboBox = new ComboBox()
                {
                    Left = 50,
                    Top = 20,
                    Width = 200,
                    DropDownStyle = ComboBoxStyle.DropDownList // 🔹 드롭다운 리스트
                };

                var enumValues = Enum.GetValues(typeof(T)).Cast<T>().ToList();
                comboBox.Items.AddRange(enumValues.Cast<object>().ToArray());
                comboBox.SelectedItem = currentValue; // 현재 값 선택
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
                    if (inputControl is ComboBox comboBox)
                        return new FSharpOption<T>((T)comboBox.SelectedItem); // 🔹 Enum 변환
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
}


