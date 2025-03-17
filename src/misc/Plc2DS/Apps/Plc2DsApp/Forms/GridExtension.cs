using DevExpress.XtraGrid.Views.Grid;

using Microsoft.FSharp.Core;

using System;
using System.Linq;
using System.Windows.Forms;

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

            TextBox textBox = new TextBox() { Left = 50, Top = 20, Width = 200, Text = currentValue?.ToString() };
            Button okButton = new Button() { Text = "확인", Left = 100, Width = 100, Top = 50, DialogResult = DialogResult.OK };

            inputForm.Controls.Add(textBox);
            inputForm.Controls.Add(okButton);
            inputForm.AcceptButton = okButton;

            if (inputForm.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    return new FSharpOption<T>((T)Convert.ChangeType(textBox.Text, typeof(T)));
                }
                catch
                {
                    MessageBox.Show("잘못된 입력값입니다.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            return FSharpOption<T>.None;    // 변경 없음
        }
    }
}
