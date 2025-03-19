namespace Plc2DsApp
{
    partial class FormMain
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
            this.btnLoadTags = new DevExpress.XtraEditors.SimpleButton();
            this.btnShowAllTags = new DevExpress.XtraEditors.SimpleButton();
            this.groupControl1 = new DevExpress.XtraEditors.GroupControl();
            this.tbNumTagsCategorized = new DevExpress.XtraEditors.TextEdit();
            this.btnShowCategorizedTags = new DevExpress.XtraEditors.SimpleButton();
            this.tbNumTagsDiscarded = new DevExpress.XtraEditors.TextEdit();
            this.tbNumTagsChosen = new DevExpress.XtraEditors.TextEdit();
            this.tbNumTagsStage = new DevExpress.XtraEditors.TextEdit();
            this.tbNumTagsAll = new DevExpress.XtraEditors.TextEdit();
            this.btnShowDiscardedTags = new DevExpress.XtraEditors.SimpleButton();
            this.btnShowChosenTags = new DevExpress.XtraEditors.SimpleButton();
            this.btnShowStageTags = new DevExpress.XtraEditors.SimpleButton();
            this.btnDiscardTags = new DevExpress.XtraEditors.SimpleButton();
            this.groupControl2 = new DevExpress.XtraEditors.GroupControl();
            this.btnReplaceTags = new DevExpress.XtraEditors.SimpleButton();
            this.btnSplitFDA = new DevExpress.XtraEditors.SimpleButton();
            this.ucRadioSelector1 = new Dual.Common.Winform.DevX.UcRadioSelector();
            this.labelControl1 = new DevExpress.XtraEditors.LabelControl();
            this.groupControl4 = new DevExpress.XtraEditors.GroupControl();
            this.btnReplaceActionName = new DevExpress.XtraEditors.SimpleButton();
            this.btnReplaceDeviceName = new DevExpress.XtraEditors.SimpleButton();
            this.btnReplaceFlowName = new DevExpress.XtraEditors.SimpleButton();
            this.btnApplyAll = new DevExpress.XtraEditors.SimpleButton();
            this.tbCsvFile = new DevExpress.XtraEditors.TextEdit();
            ((System.ComponentModel.ISupportInitialize)(this.groupControl1)).BeginInit();
            this.groupControl1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tbNumTagsCategorized.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbNumTagsDiscarded.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbNumTagsChosen.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbNumTagsStage.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbNumTagsAll.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.groupControl2)).BeginInit();
            this.groupControl2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.groupControl4)).BeginInit();
            this.groupControl4.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tbCsvFile.Properties)).BeginInit();
            this.SuspendLayout();
            // 
            // btnLoadTags
            // 
            this.btnLoadTags.Location = new System.Drawing.Point(12, 87);
            this.btnLoadTags.Margin = new System.Windows.Forms.Padding(2);
            this.btnLoadTags.Name = "btnLoadTags";
            this.btnLoadTags.Size = new System.Drawing.Size(112, 34);
            this.btnLoadTags.TabIndex = 0;
            this.btnLoadTags.Text = "Load tags..";
            this.btnLoadTags.Click += new System.EventHandler(this.btnLoadTags_Click);
            // 
            // btnShowAllTags
            // 
            this.btnShowAllTags.Location = new System.Drawing.Point(5, 42);
            this.btnShowAllTags.Margin = new System.Windows.Forms.Padding(2);
            this.btnShowAllTags.Name = "btnShowAllTags";
            this.btnShowAllTags.Size = new System.Drawing.Size(90, 34);
            this.btnShowAllTags.TabIndex = 1;
            this.btnShowAllTags.Text = "All";
            // 
            // groupControl1
            // 
            this.groupControl1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupControl1.Controls.Add(this.tbNumTagsCategorized);
            this.groupControl1.Controls.Add(this.btnShowCategorizedTags);
            this.groupControl1.Controls.Add(this.tbNumTagsDiscarded);
            this.groupControl1.Controls.Add(this.tbNumTagsChosen);
            this.groupControl1.Controls.Add(this.tbNumTagsStage);
            this.groupControl1.Controls.Add(this.tbNumTagsAll);
            this.groupControl1.Controls.Add(this.btnShowDiscardedTags);
            this.groupControl1.Controls.Add(this.btnShowChosenTags);
            this.groupControl1.Controls.Add(this.btnShowStageTags);
            this.groupControl1.Controls.Add(this.btnShowAllTags);
            this.groupControl1.Location = new System.Drawing.Point(11, 467);
            this.groupControl1.Margin = new System.Windows.Forms.Padding(2);
            this.groupControl1.Name = "groupControl1";
            this.groupControl1.Size = new System.Drawing.Size(611, 122);
            this.groupControl1.TabIndex = 3;
            this.groupControl1.Text = "Show tags";
            // 
            // tbNumTagsCategorized
            // 
            this.tbNumTagsCategorized.Location = new System.Drawing.Point(345, 82);
            this.tbNumTagsCategorized.Margin = new System.Windows.Forms.Padding(2);
            this.tbNumTagsCategorized.Name = "tbNumTagsCategorized";
            this.tbNumTagsCategorized.Properties.ReadOnly = true;
            this.tbNumTagsCategorized.Size = new System.Drawing.Size(110, 28);
            this.tbNumTagsCategorized.TabIndex = 12;
            // 
            // btnShowCategorizedTags
            // 
            this.btnShowCategorizedTags.Location = new System.Drawing.Point(345, 42);
            this.btnShowCategorizedTags.Margin = new System.Windows.Forms.Padding(2);
            this.btnShowCategorizedTags.Name = "btnShowCategorizedTags";
            this.btnShowCategorizedTags.Size = new System.Drawing.Size(110, 34);
            this.btnShowCategorizedTags.TabIndex = 11;
            this.btnShowCategorizedTags.Text = "Categorized";
            // 
            // tbNumTagsDiscarded
            // 
            this.tbNumTagsDiscarded.Location = new System.Drawing.Point(476, 82);
            this.tbNumTagsDiscarded.Margin = new System.Windows.Forms.Padding(2);
            this.tbNumTagsDiscarded.Name = "tbNumTagsDiscarded";
            this.tbNumTagsDiscarded.Properties.ReadOnly = true;
            this.tbNumTagsDiscarded.Size = new System.Drawing.Size(90, 28);
            this.tbNumTagsDiscarded.TabIndex = 10;
            // 
            // tbNumTagsChosen
            // 
            this.tbNumTagsChosen.Location = new System.Drawing.Point(231, 82);
            this.tbNumTagsChosen.Margin = new System.Windows.Forms.Padding(2);
            this.tbNumTagsChosen.Name = "tbNumTagsChosen";
            this.tbNumTagsChosen.Properties.ReadOnly = true;
            this.tbNumTagsChosen.Size = new System.Drawing.Size(90, 28);
            this.tbNumTagsChosen.TabIndex = 9;
            // 
            // tbNumTagsStage
            // 
            this.tbNumTagsStage.Location = new System.Drawing.Point(118, 82);
            this.tbNumTagsStage.Margin = new System.Windows.Forms.Padding(2);
            this.tbNumTagsStage.Name = "tbNumTagsStage";
            this.tbNumTagsStage.Properties.ReadOnly = true;
            this.tbNumTagsStage.Size = new System.Drawing.Size(90, 28);
            this.tbNumTagsStage.TabIndex = 8;
            // 
            // tbNumTagsAll
            // 
            this.tbNumTagsAll.Location = new System.Drawing.Point(5, 82);
            this.tbNumTagsAll.Margin = new System.Windows.Forms.Padding(2);
            this.tbNumTagsAll.Name = "tbNumTagsAll";
            this.tbNumTagsAll.Properties.ReadOnly = true;
            this.tbNumTagsAll.Size = new System.Drawing.Size(90, 28);
            this.tbNumTagsAll.TabIndex = 7;
            // 
            // btnShowDiscardedTags
            // 
            this.btnShowDiscardedTags.Location = new System.Drawing.Point(476, 42);
            this.btnShowDiscardedTags.Margin = new System.Windows.Forms.Padding(2);
            this.btnShowDiscardedTags.Name = "btnShowDiscardedTags";
            this.btnShowDiscardedTags.Size = new System.Drawing.Size(90, 34);
            this.btnShowDiscardedTags.TabIndex = 6;
            this.btnShowDiscardedTags.Text = "Discarded";
            // 
            // btnShowChosenTags
            // 
            this.btnShowChosenTags.Location = new System.Drawing.Point(231, 42);
            this.btnShowChosenTags.Margin = new System.Windows.Forms.Padding(2);
            this.btnShowChosenTags.Name = "btnShowChosenTags";
            this.btnShowChosenTags.Size = new System.Drawing.Size(90, 34);
            this.btnShowChosenTags.TabIndex = 5;
            this.btnShowChosenTags.Text = "Chosen";
            // 
            // btnShowStageTags
            // 
            this.btnShowStageTags.Location = new System.Drawing.Point(118, 42);
            this.btnShowStageTags.Margin = new System.Windows.Forms.Padding(2);
            this.btnShowStageTags.Name = "btnShowStageTags";
            this.btnShowStageTags.Size = new System.Drawing.Size(90, 34);
            this.btnShowStageTags.TabIndex = 4;
            this.btnShowStageTags.Text = "Stage";
            // 
            // btnDiscardTags
            // 
            this.btnDiscardTags.Location = new System.Drawing.Point(6, 45);
            this.btnDiscardTags.Margin = new System.Windows.Forms.Padding(2);
            this.btnDiscardTags.Name = "btnDiscardTags";
            this.btnDiscardTags.Size = new System.Drawing.Size(139, 45);
            this.btnDiscardTags.TabIndex = 7;
            this.btnDiscardTags.Text = "Discard";
            this.btnDiscardTags.Click += new System.EventHandler(this.btnDiscardTags_Click);
            // 
            // groupControl2
            // 
            this.groupControl2.Controls.Add(this.btnReplaceTags);
            this.groupControl2.Controls.Add(this.btnSplitFDA);
            this.groupControl2.Controls.Add(this.btnDiscardTags);
            this.groupControl2.Location = new System.Drawing.Point(12, 148);
            this.groupControl2.Margin = new System.Windows.Forms.Padding(2);
            this.groupControl2.Name = "groupControl2";
            this.groupControl2.Size = new System.Drawing.Size(609, 94);
            this.groupControl2.TabIndex = 9;
            this.groupControl2.Text = "Tags";
            // 
            // btnReplaceTags
            // 
            this.btnReplaceTags.Location = new System.Drawing.Point(164, 45);
            this.btnReplaceTags.Margin = new System.Windows.Forms.Padding(2);
            this.btnReplaceTags.Name = "btnReplaceTags";
            this.btnReplaceTags.Size = new System.Drawing.Size(139, 45);
            this.btnReplaceTags.TabIndex = 11;
            this.btnReplaceTags.Text = "Replace";
            this.btnReplaceTags.Click += new System.EventHandler(this.btnReplaceTags_Click);
            // 
            // btnSplitFDA
            // 
            this.btnSplitFDA.Location = new System.Drawing.Point(330, 45);
            this.btnSplitFDA.Margin = new System.Windows.Forms.Padding(2);
            this.btnSplitFDA.Name = "btnSplitFDA";
            this.btnSplitFDA.Size = new System.Drawing.Size(139, 45);
            this.btnSplitFDA.TabIndex = 10;
            this.btnSplitFDA.Text = "Split";
            this.btnSplitFDA.ToolTip = "Flow, Device, Action 명 추출";
            this.btnSplitFDA.Click += new System.EventHandler(this.btnSplitFDA_Click);
            // 
            // ucRadioSelector1
            // 
            this.ucRadioSelector1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.ucRadioSelector1.Location = new System.Drawing.Point(148, 12);
            this.ucRadioSelector1.Margin = new System.Windows.Forms.Padding(2, 3, 2, 3);
            this.ucRadioSelector1.Name = "ucRadioSelector1";
            this.ucRadioSelector1.Size = new System.Drawing.Size(477, 39);
            this.ucRadioSelector1.TabIndex = 11;
            // 
            // labelControl1
            // 
            this.labelControl1.Location = new System.Drawing.Point(17, 22);
            this.labelControl1.Name = "labelControl1";
            this.labelControl1.Size = new System.Drawing.Size(62, 22);
            this.labelControl1.TabIndex = 12;
            this.labelControl1.Text = "Vendor:";
            // 
            // groupControl4
            // 
            this.groupControl4.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupControl4.Controls.Add(this.btnReplaceActionName);
            this.groupControl4.Controls.Add(this.btnReplaceDeviceName);
            this.groupControl4.Controls.Add(this.btnReplaceFlowName);
            this.groupControl4.Location = new System.Drawing.Point(12, 257);
            this.groupControl4.Margin = new System.Windows.Forms.Padding(2);
            this.groupControl4.Name = "groupControl4";
            this.groupControl4.Size = new System.Drawing.Size(610, 98);
            this.groupControl4.TabIndex = 13;
            this.groupControl4.Text = "Discards F/D/A";
            // 
            // btnDiscardActionName
            // 
            this.btnReplaceActionName.Location = new System.Drawing.Point(330, 45);
            this.btnReplaceActionName.Margin = new System.Windows.Forms.Padding(2);
            this.btnReplaceActionName.Name = "btnDiscardActionName";
            this.btnReplaceActionName.Size = new System.Drawing.Size(139, 45);
            this.btnReplaceActionName.TabIndex = 12;
            this.btnReplaceActionName.Text = "Action";
            this.btnReplaceActionName.ToolTip = "Action 명에서 불필요한 부분 제거";
            // 
            // btnDiscardDeviceName
            // 
            this.btnReplaceDeviceName.Location = new System.Drawing.Point(164, 45);
            this.btnReplaceDeviceName.Margin = new System.Windows.Forms.Padding(2);
            this.btnReplaceDeviceName.Name = "btnDiscardDeviceName";
            this.btnReplaceDeviceName.Size = new System.Drawing.Size(139, 45);
            this.btnReplaceDeviceName.TabIndex = 11;
            this.btnReplaceDeviceName.Text = "Device";
            this.btnReplaceDeviceName.ToolTip = "Device 명에서 불필요한 부분 제거";
            // 
            // btnDiscardFlowName
            // 
            this.btnReplaceFlowName.Location = new System.Drawing.Point(4, 45);
            this.btnReplaceFlowName.Margin = new System.Windows.Forms.Padding(2);
            this.btnReplaceFlowName.Name = "btnDiscardFlowName";
            this.btnReplaceFlowName.Size = new System.Drawing.Size(139, 45);
            this.btnReplaceFlowName.TabIndex = 10;
            this.btnReplaceFlowName.Text = "Flow";
            this.btnReplaceFlowName.ToolTip = "Flow 명에서 불필요한 부분 제거";
            // 
            // btnApplyAll
            // 
            this.btnApplyAll.Location = new System.Drawing.Point(387, 407);
            this.btnApplyAll.Margin = new System.Windows.Forms.Padding(2);
            this.btnApplyAll.Name = "btnApplyAll";
            this.btnApplyAll.Size = new System.Drawing.Size(234, 45);
            this.btnApplyAll.TabIndex = 14;
            this.btnApplyAll.Text = "Apply all rules";
            this.btnApplyAll.ToolTip = "일괄 적용";
            this.btnApplyAll.Click += new System.EventHandler(this.btnApplyAll_Click);
            // 
            // tbCsvFile
            // 
            this.tbCsvFile.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tbCsvFile.Location = new System.Drawing.Point(148, 91);
            this.tbCsvFile.Margin = new System.Windows.Forms.Padding(2);
            this.tbCsvFile.Name = "tbCsvFile";
            this.tbCsvFile.Size = new System.Drawing.Size(473, 28);
            this.tbCsvFile.TabIndex = 2;
            // 
            // FormMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(10F, 22F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(634, 600);
            this.Controls.Add(this.btnApplyAll);
            this.Controls.Add(this.groupControl4);
            this.Controls.Add(this.labelControl1);
            this.Controls.Add(this.ucRadioSelector1);
            this.Controls.Add(this.groupControl2);
            this.Controls.Add(this.groupControl1);
            this.Controls.Add(this.tbCsvFile);
            this.Controls.Add(this.btnLoadTags);
            this.Margin = new System.Windows.Forms.Padding(5);
            this.Name = "FormMain";
            this.Text = "Plc2Ds";
            this.Load += new System.EventHandler(this.FormMain_Load);
            ((System.ComponentModel.ISupportInitialize)(this.groupControl1)).EndInit();
            this.groupControl1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.tbNumTagsCategorized.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbNumTagsDiscarded.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbNumTagsChosen.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbNumTagsStage.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbNumTagsAll.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.groupControl2)).EndInit();
            this.groupControl2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.groupControl4)).EndInit();
            this.groupControl4.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.tbCsvFile.Properties)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private DevExpress.XtraEditors.SimpleButton btnLoadTags;
        private DevExpress.XtraEditors.SimpleButton btnShowAllTags;
        private DevExpress.XtraEditors.GroupControl groupControl1;
        private DevExpress.XtraEditors.SimpleButton btnShowDiscardedTags;
        private DevExpress.XtraEditors.SimpleButton btnShowChosenTags;
        private DevExpress.XtraEditors.SimpleButton btnShowStageTags;
        private DevExpress.XtraEditors.TextEdit tbNumTagsDiscarded;
        private DevExpress.XtraEditors.TextEdit tbNumTagsChosen;
        private DevExpress.XtraEditors.TextEdit tbNumTagsStage;
        private DevExpress.XtraEditors.TextEdit tbNumTagsAll;
        private DevExpress.XtraEditors.SimpleButton btnDiscardTags;
        private DevExpress.XtraEditors.GroupControl groupControl2;
        private DevExpress.XtraEditors.SimpleButton btnSplitFDA;
        private UcRadioSelector ucRadioSelector1;
        private DevExpress.XtraEditors.TextEdit tbNumTagsCategorized;
        private DevExpress.XtraEditors.SimpleButton btnShowCategorizedTags;
        private DevExpress.XtraEditors.LabelControl labelControl1;
        private DevExpress.XtraEditors.GroupControl groupControl4;
        private DevExpress.XtraEditors.SimpleButton btnReplaceFlowName;
        private DevExpress.XtraEditors.SimpleButton btnReplaceActionName;
        private DevExpress.XtraEditors.SimpleButton btnReplaceDeviceName;
        private DevExpress.XtraEditors.SimpleButton btnApplyAll;
        private DevExpress.XtraEditors.SimpleButton btnReplaceTags;
        private DevExpress.XtraEditors.TextEdit tbCsvFile;
    }
}

