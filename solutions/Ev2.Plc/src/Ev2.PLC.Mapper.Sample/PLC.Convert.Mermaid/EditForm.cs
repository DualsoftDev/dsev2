using System;
using System.Windows.Forms;

namespace PLC.Convert.Mermaid
{
    public class EditForm : Form
    {
        private RichTextBox richTextBox;
        private Button btnSave;

        public string EditedText => richTextBox.Text; // 편집된 텍스트 가져오기

        public EditForm(string initialText)
        {
            this.Text = "Subgraph 편집";
            this.Size = new System.Drawing.Size(600, 400);
            this.StartPosition = FormStartPosition.CenterScreen;

            // 🟢 RichTextBox 추가
            richTextBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                Text = initialText
            };
            this.Controls.Add(richTextBox);

            // 🟢 저장 버튼 추가
            btnSave = new Button
            {
                Text = "저장",
                Dock = DockStyle.Bottom
            };
            btnSave.Click += BtnSave_Click;
            this.Controls.Add(btnSave);
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
