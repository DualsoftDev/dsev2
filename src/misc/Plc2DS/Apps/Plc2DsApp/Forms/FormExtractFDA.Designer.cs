namespace Plc2DsApp.Forms
{
    partial class FormExtractFDA
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
            this.groupControl1 = new DevExpress.XtraEditors.GroupControl();
            this.tbNumTagsNonStage = new DevExpress.XtraEditors.TextEdit();
            this.tbNumTagsStage = new DevExpress.XtraEditors.TextEdit();
            this.tbNumTagsAll = new DevExpress.XtraEditors.TextEdit();
            this.btnShowChosenTags = new DevExpress.XtraEditors.SimpleButton();
            this.btnShowStageTags = new DevExpress.XtraEditors.SimpleButton();
            this.btnShowAllTags = new DevExpress.XtraEditors.SimpleButton();
            this.btnOK = new DevExpress.XtraEditors.SimpleButton();
            this.gridControl1 = new DevExpress.XtraGrid.GridControl();
            this.gridView1 = new DevExpress.XtraGrid.Views.Grid.GridView();
            this.labelControl1 = new DevExpress.XtraEditors.LabelControl();
            this.tbPattern = new DevExpress.XtraEditors.TextEdit();
            this.btnApply = new DevExpress.XtraEditors.SimpleButton();
            this.tbNumTagsCategorized = new DevExpress.XtraEditors.TextEdit();
            this.btnShowCategorizedTags = new DevExpress.XtraEditors.SimpleButton();
            ((System.ComponentModel.ISupportInitialize)(this.groupControl1)).BeginInit();
            this.groupControl1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tbNumTagsNonStage.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbNumTagsStage.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbNumTagsAll.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridControl1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridView1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbPattern.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbNumTagsCategorized.Properties)).BeginInit();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(910, 619);
            this.btnCancel.Margin = new System.Windows.Forms.Padding(2, 3, 2, 3);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(70, 35);
            this.btnCancel.TabIndex = 15;
            this.btnCancel.Text = "Cancel";
            // 
            // groupControl1
            // 
            this.groupControl1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.groupControl1.Controls.Add(this.tbNumTagsCategorized);
            this.groupControl1.Controls.Add(this.tbNumTagsNonStage);
            this.groupControl1.Controls.Add(this.btnShowCategorizedTags);
            this.groupControl1.Controls.Add(this.tbNumTagsStage);
            this.groupControl1.Controls.Add(this.tbNumTagsAll);
            this.groupControl1.Controls.Add(this.btnShowChosenTags);
            this.groupControl1.Controls.Add(this.btnShowStageTags);
            this.groupControl1.Controls.Add(this.btnShowAllTags);
            this.groupControl1.Location = new System.Drawing.Point(12, 532);
            this.groupControl1.Name = "groupControl1";
            this.groupControl1.Size = new System.Drawing.Size(465, 122);
            this.groupControl1.TabIndex = 13;
            this.groupControl1.Text = "Show tags";
            // 
            // tbNumTagsNonStage
            // 
            this.tbNumTagsNonStage.Location = new System.Drawing.Point(231, 82);
            this.tbNumTagsNonStage.Name = "tbNumTagsNonStage";
            this.tbNumTagsNonStage.Properties.ReadOnly = true;
            this.tbNumTagsNonStage.Size = new System.Drawing.Size(90, 28);
            this.tbNumTagsNonStage.TabIndex = 9;
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
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(834, 619);
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
            this.gridControl1.Size = new System.Drawing.Size(968, 358);
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
            this.labelControl1.Location = new System.Drawing.Point(17, 405);
            this.labelControl1.Name = "labelControl1";
            this.labelControl1.Size = new System.Drawing.Size(62, 22);
            this.labelControl1.TabIndex = 16;
            this.labelControl1.Text = "Pattern:";
            // 
            // tbPattern
            // 
            this.tbPattern.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tbPattern.Location = new System.Drawing.Point(85, 402);
            this.tbPattern.Name = "tbPattern";
            this.tbPattern.Size = new System.Drawing.Size(774, 28);
            this.tbPattern.TabIndex = 17;
            // 
            // btnApply
            // 
            this.btnApply.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnApply.Location = new System.Drawing.Point(886, 398);
            this.btnApply.Name = "btnApply";
            this.btnApply.Size = new System.Drawing.Size(90, 34);
            this.btnApply.TabIndex = 10;
            this.btnApply.Text = "Apply";
            this.btnApply.Click += new System.EventHandler(this.btnApply_Click);
            // 
            // tbNumTagsCategorized
            // 
            this.tbNumTagsCategorized.Location = new System.Drawing.Point(342, 82);
            this.tbNumTagsCategorized.Name = "tbNumTagsCategorized";
            this.tbNumTagsCategorized.Properties.ReadOnly = true;
            this.tbNumTagsCategorized.Size = new System.Drawing.Size(106, 28);
            this.tbNumTagsCategorized.TabIndex = 19;
            // 
            // btnShowCategorizedTags
            // 
            this.btnShowCategorizedTags.Location = new System.Drawing.Point(342, 42);
            this.btnShowCategorizedTags.Name = "btnShowCategorizedTags";
            this.btnShowCategorizedTags.Size = new System.Drawing.Size(106, 34);
            this.btnShowCategorizedTags.TabIndex = 18;
            this.btnShowCategorizedTags.Text = "Categorized";
            // 
            // FormExtractFDA
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(10F, 22F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(988, 666);
            this.Controls.Add(this.btnApply);
            this.Controls.Add(this.tbPattern);
            this.Controls.Add(this.labelControl1);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.groupControl1);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.gridControl1);
            this.Name = "FormExtractFDA";
            this.Text = "FormExtractFDA";
            this.Load += new System.EventHandler(this.FormExtractFDA_Load);
            ((System.ComponentModel.ISupportInitialize)(this.groupControl1)).EndInit();
            this.groupControl1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.tbNumTagsNonStage.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbNumTagsStage.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbNumTagsAll.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridControl1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridView1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbPattern.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbNumTagsCategorized.Properties)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private DevExpress.XtraEditors.SimpleButton btnCancel;
        private DevExpress.XtraEditors.GroupControl groupControl1;
        private DevExpress.XtraEditors.TextEdit tbNumTagsNonStage;
        private DevExpress.XtraEditors.TextEdit tbNumTagsStage;
        private DevExpress.XtraEditors.TextEdit tbNumTagsAll;
        private DevExpress.XtraEditors.SimpleButton btnShowChosenTags;
        private DevExpress.XtraEditors.SimpleButton btnShowStageTags;
        private DevExpress.XtraEditors.SimpleButton btnShowAllTags;
        private DevExpress.XtraEditors.SimpleButton btnOK;
        private DevExpress.XtraGrid.GridControl gridControl1;
        private GridView gridView1;
        private DevExpress.XtraEditors.LabelControl labelControl1;
        private DevExpress.XtraEditors.TextEdit tbPattern;
        private DevExpress.XtraEditors.SimpleButton btnApply;
        private DevExpress.XtraEditors.TextEdit tbNumTagsCategorized;
        private DevExpress.XtraEditors.SimpleButton btnShowCategorizedTags;
    }
}