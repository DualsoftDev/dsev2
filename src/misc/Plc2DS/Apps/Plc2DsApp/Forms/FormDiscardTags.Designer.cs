namespace Plc2DsApp.Forms
{
    partial class FormDiscardTags
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
        void InitializeComponent()
        {
            this.gridControl1 = new DevExpress.XtraGrid.GridControl();
            this.gridView1 = new DevExpress.XtraGrid.Views.Grid.GridView();
            this.groupControl1 = new DevExpress.XtraEditors.GroupControl();
            this.tbNumTagsChosen = new DevExpress.XtraEditors.TextEdit();
            this.tbNumTagsStage = new DevExpress.XtraEditors.TextEdit();
            this.tbNumTagsAll = new DevExpress.XtraEditors.TextEdit();
            this.btnShowChosenTags = new DevExpress.XtraEditors.SimpleButton();
            this.btnShowStageTags = new DevExpress.XtraEditors.SimpleButton();
            this.btnShowAllTags = new DevExpress.XtraEditors.SimpleButton();
            this.btnCancel = new DevExpress.XtraEditors.SimpleButton();
            this.btnOK = new DevExpress.XtraEditors.SimpleButton();
            this.btnApplyAllPatterns = new DevExpress.XtraEditors.SimpleButton();
            ((System.ComponentModel.ISupportInitialize)(this.gridControl1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridView1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.groupControl1)).BeginInit();
            this.groupControl1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tbNumTagsChosen.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbNumTagsStage.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbNumTagsAll.Properties)).BeginInit();
            this.SuspendLayout();
            // 
            // gridControl1
            // 
            this.gridControl1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.gridControl1.Location = new System.Drawing.Point(22, 12);
            this.gridControl1.MainView = this.gridView1;
            this.gridControl1.Name = "gridControl1";
            this.gridControl1.Size = new System.Drawing.Size(1037, 375);
            this.gridControl1.TabIndex = 0;
            this.gridControl1.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] {
            this.gridView1});
            // 
            // gridView1
            // 
            this.gridView1.GridControl = this.gridControl1;
            this.gridView1.Name = "gridView1";
            // 
            // groupControl1
            // 
            this.groupControl1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.groupControl1.Controls.Add(this.tbNumTagsChosen);
            this.groupControl1.Controls.Add(this.tbNumTagsStage);
            this.groupControl1.Controls.Add(this.tbNumTagsAll);
            this.groupControl1.Controls.Add(this.btnShowChosenTags);
            this.groupControl1.Controls.Add(this.btnShowStageTags);
            this.groupControl1.Controls.Add(this.btnShowAllTags);
            this.groupControl1.Location = new System.Drawing.Point(12, 455);
            this.groupControl1.Name = "groupControl1";
            this.groupControl1.Size = new System.Drawing.Size(465, 122);
            this.groupControl1.TabIndex = 4;
            this.groupControl1.Text = "Show tags";
            // 
            // tbNumTagsChosen
            // 
            this.tbNumTagsChosen.Location = new System.Drawing.Point(231, 82);
            this.tbNumTagsChosen.Name = "tbNumTagsChosen";
            this.tbNumTagsChosen.Properties.ReadOnly = true;
            this.tbNumTagsChosen.Size = new System.Drawing.Size(90, 28);
            this.tbNumTagsChosen.TabIndex = 9;
            // 
            // tbNumTagsStage
            // 
            this.tbNumTagsStage.Location = new System.Drawing.Point(118, 82);
            this.tbNumTagsStage.Name = "tbNumTagsStage";
            this.tbNumTagsStage.Properties.ReadOnly = true;
            this.tbNumTagsStage.Size = new System.Drawing.Size(90, 28);
            this.tbNumTagsStage.TabIndex = 8;
            // 
            // tbNumTagsAll
            // 
            this.tbNumTagsAll.Location = new System.Drawing.Point(5, 82);
            this.tbNumTagsAll.Name = "tbNumTagsAll";
            this.tbNumTagsAll.Properties.ReadOnly = true;
            this.tbNumTagsAll.Size = new System.Drawing.Size(90, 28);
            this.tbNumTagsAll.TabIndex = 7;
            // 
            // btnShowChosenTags
            // 
            this.btnShowChosenTags.Location = new System.Drawing.Point(231, 42);
            this.btnShowChosenTags.Name = "btnShowChosenTags";
            this.btnShowChosenTags.Size = new System.Drawing.Size(90, 34);
            this.btnShowChosenTags.TabIndex = 5;
            this.btnShowChosenTags.Text = "Chosen";
            // 
            // btnShowStageTags
            // 
            this.btnShowStageTags.Location = new System.Drawing.Point(118, 42);
            this.btnShowStageTags.Name = "btnShowStageTags";
            this.btnShowStageTags.Size = new System.Drawing.Size(90, 34);
            this.btnShowStageTags.TabIndex = 4;
            this.btnShowStageTags.Text = "Stage";
            // 
            // btnShowAllTags
            // 
            this.btnShowAllTags.Location = new System.Drawing.Point(5, 42);
            this.btnShowAllTags.Name = "btnShowAllTags";
            this.btnShowAllTags.Size = new System.Drawing.Size(90, 34);
            this.btnShowAllTags.TabIndex = 1;
            this.btnShowAllTags.Text = "All";
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(989, 542);
            this.btnCancel.Margin = new System.Windows.Forms.Padding(2, 3, 2, 3);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(70, 35);
            this.btnCancel.TabIndex = 11;
            this.btnCancel.Text = "Cancel";
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(913, 542);
            this.btnOK.Margin = new System.Windows.Forms.Padding(2, 3, 2, 3);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(70, 35);
            this.btnOK.TabIndex = 10;
            this.btnOK.Text = "OK";
            // 
            // btnApplyAllPatterns
            // 
            this.btnApplyAllPatterns.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnApplyAllPatterns.Location = new System.Drawing.Point(868, 393);
            this.btnApplyAllPatterns.Name = "btnApplyAllPatterns";
            this.btnApplyAllPatterns.Size = new System.Drawing.Size(191, 34);
            this.btnApplyAllPatterns.TabIndex = 10;
            this.btnApplyAllPatterns.Text = "Apply all patterns";
            this.btnApplyAllPatterns.ToolTip = "Grid 상의 pattern 들을 순서대로 적용\r\n- 적용 순서에 따라 전체 결과가 달라 질 수 있음.\r\n- 필요시, 개별 항목들을 순서 변경하면" +
    "서 실행.";
            this.btnApplyAllPatterns.Click += new System.EventHandler(this.btnApplyAllPatterns_Click);
            // 
            // FormDiscardTags
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(10F, 22F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1074, 591);
            this.Controls.Add(this.btnApplyAllPatterns);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.groupControl1);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.gridControl1);
            this.Name = "FormDiscardTags";
            this.Text = "FormDiscardTags";
            this.Load += new System.EventHandler(this.FormPattern_Load);
            ((System.ComponentModel.ISupportInitialize)(this.gridControl1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridView1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.groupControl1)).EndInit();
            this.groupControl1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.tbNumTagsChosen.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbNumTagsStage.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbNumTagsAll.Properties)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private DevExpress.XtraGrid.GridControl gridControl1;
        private DevExpress.XtraGrid.Views.Grid.GridView gridView1;
        private DevExpress.XtraEditors.GroupControl groupControl1;
        private DevExpress.XtraEditors.TextEdit tbNumTagsChosen;
        private DevExpress.XtraEditors.TextEdit tbNumTagsStage;
        private DevExpress.XtraEditors.TextEdit tbNumTagsAll;
        private DevExpress.XtraEditors.SimpleButton btnShowChosenTags;
        private DevExpress.XtraEditors.SimpleButton btnShowStageTags;
        private DevExpress.XtraEditors.SimpleButton btnShowAllTags;
        private DevExpress.XtraEditors.SimpleButton btnCancel;
        private DevExpress.XtraEditors.SimpleButton btnOK;
        private DevExpress.XtraEditors.SimpleButton btnApplyAllPatterns;
    }
}