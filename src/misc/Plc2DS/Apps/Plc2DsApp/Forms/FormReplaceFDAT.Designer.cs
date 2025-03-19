namespace Plc2DsApp.Forms
{
    partial class FormReplaceFDAT
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.btnCancel = new DevExpress.XtraEditors.SimpleButton();
            this.btnOK = new DevExpress.XtraEditors.SimpleButton();
            this.gridControl1 = new DevExpress.XtraGrid.GridControl();
            this.gridView1 = new DevExpress.XtraGrid.Views.Grid.GridView();
            this.labelControl1 = new DevExpress.XtraEditors.LabelControl();
            this.tbCustomPattern = new DevExpress.XtraEditors.TextEdit();
            this.btnApplyCustomPattern = new DevExpress.XtraEditors.SimpleButton();
            this.btnApplyAllPatterns = new DevExpress.XtraEditors.SimpleButton();
            ((System.ComponentModel.ISupportInitialize)(this.gridControl1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridView1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbCustomPattern.Properties)).BeginInit();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(962, 632);
            this.btnCancel.Margin = new System.Windows.Forms.Padding(2, 3, 2, 3);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(70, 35);
            this.btnCancel.TabIndex = 15;
            this.btnCancel.Text = "Cancel";
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(886, 632);
            this.btnOK.Margin = new System.Windows.Forms.Padding(2, 3, 2, 3);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(70, 35);
            this.btnOK.TabIndex = 14;
            this.btnOK.Text = "OK";
            // 
            // gridControl1
            // 
            this.gridControl1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.gridControl1.Location = new System.Drawing.Point(12, 12);
            this.gridControl1.MainView = this.gridView1;
            this.gridControl1.Name = "gridControl1";
            this.gridControl1.Size = new System.Drawing.Size(1020, 371);
            this.gridControl1.TabIndex = 12;
            this.gridControl1.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] {
            this.gridView1});
            // 
            // gridView1
            // 
            this.gridView1.GridControl = this.gridControl1;
            this.gridView1.Name = "gridView1";
            // 
            // labelControl1
            // 
            this.labelControl1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.labelControl1.Location = new System.Drawing.Point(17, 449);
            this.labelControl1.Name = "labelControl1";
            this.labelControl1.Size = new System.Drawing.Size(128, 22);
            this.labelControl1.TabIndex = 16;
            this.labelControl1.Text = "Custom Pattern:";
            // 
            // tbCustomPattern
            // 
            this.tbCustomPattern.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tbCustomPattern.Location = new System.Drawing.Point(151, 446);
            this.tbCustomPattern.Name = "tbCustomPattern";
            this.tbCustomPattern.Size = new System.Drawing.Size(760, 28);
            this.tbCustomPattern.TabIndex = 17;
            // 
            // btnApplyCustomPattern
            // 
            this.btnApplyCustomPattern.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnApplyCustomPattern.Location = new System.Drawing.Point(938, 442);
            this.btnApplyCustomPattern.Name = "btnApplyCustomPattern";
            this.btnApplyCustomPattern.Size = new System.Drawing.Size(90, 34);
            this.btnApplyCustomPattern.TabIndex = 10;
            this.btnApplyCustomPattern.Text = "Apply";
            this.btnApplyCustomPattern.Click += new System.EventHandler(this.btnApplyCustomPattern_Click);
            // 
            // btnApplyAllPatterns
            // 
            this.btnApplyAllPatterns.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnApplyAllPatterns.Location = new System.Drawing.Point(841, 389);
            this.btnApplyAllPatterns.Name = "btnApplyAllPatterns";
            this.btnApplyAllPatterns.Size = new System.Drawing.Size(191, 34);
            this.btnApplyAllPatterns.TabIndex = 18;
            this.btnApplyAllPatterns.Text = "Apply all patterns";
            this.btnApplyAllPatterns.ToolTip = "Grid 상의 pattern 들을 순서대로 적용\r\n- 적용 순서에 따라 전체 결과가 달라 질 수 있음.\r\n- 필요시, 개별 항목들을 순서 변경하면" +
    "서 실행.";
            this.btnApplyAllPatterns.Click += new System.EventHandler(this.btnApplyAllPatterns_Click);
            // 
            // FormReplaceFDAT
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(10F, 22F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1040, 679);
            this.Controls.Add(this.btnApplyAllPatterns);
            this.Controls.Add(this.btnApplyCustomPattern);
            this.Controls.Add(this.tbCustomPattern);
            this.Controls.Add(this.labelControl1);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.gridControl1);
            this.Name = "FormReplaceFDAT";
            this.Text = "FormReplaceFDAT";
            this.Load += new System.EventHandler(this.FormDiscardFDA_Load);
            ((System.ComponentModel.ISupportInitialize)(this.gridControl1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridView1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbCustomPattern.Properties)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private DevExpress.XtraEditors.SimpleButton btnCancel;
        private DevExpress.XtraEditors.SimpleButton btnOK;
        private DevExpress.XtraGrid.GridControl gridControl1;
        private GridView gridView1;
        private DevExpress.XtraEditors.LabelControl labelControl1;
        private DevExpress.XtraEditors.TextEdit tbCustomPattern;
        private DevExpress.XtraEditors.SimpleButton btnApplyCustomPattern;
        private DevExpress.XtraEditors.SimpleButton btnApplyAllPatterns;
    }
}