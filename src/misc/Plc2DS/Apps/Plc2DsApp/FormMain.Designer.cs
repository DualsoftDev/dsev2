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
            ComponentResourceManager resources = new ComponentResourceManager(typeof(FormMain));
            btnLoadTags = new DevExpress.XtraEditors.SimpleButton();
            btnShowAllTags = new DevExpress.XtraEditors.SimpleButton();
            groupControl1 = new DevExpress.XtraEditors.GroupControl();
            tbNumTagsCategorized = new DevExpress.XtraEditors.TextEdit();
            btnShowCategorizedTags = new DevExpress.XtraEditors.SimpleButton();
            tbNumTagsDiscarded = new DevExpress.XtraEditors.TextEdit();
            tbNumTagsChosen = new DevExpress.XtraEditors.TextEdit();
            tbNumTagsStage = new DevExpress.XtraEditors.TextEdit();
            tbNumTagsAll = new DevExpress.XtraEditors.TextEdit();
            btnShowDiscardedTags = new DevExpress.XtraEditors.SimpleButton();
            btnShowChosenTags = new DevExpress.XtraEditors.SimpleButton();
            btnShowStageTags = new DevExpress.XtraEditors.SimpleButton();
            btnDiscardTags = new DevExpress.XtraEditors.SimpleButton();
            groupControl2 = new DevExpress.XtraEditors.GroupControl();
            btnReplaceTags = new DevExpress.XtraEditors.SimpleButton();
            btnSplitFDA = new DevExpress.XtraEditors.SimpleButton();
            labelControl1 = new DevExpress.XtraEditors.LabelControl();
            groupControl4 = new DevExpress.XtraEditors.GroupControl();
            btnReplaceActionName = new DevExpress.XtraEditors.SimpleButton();
            btnReplaceDeviceName = new DevExpress.XtraEditors.SimpleButton();
            btnReplaceFlowName = new DevExpress.XtraEditors.SimpleButton();
            btnApplyAll = new DevExpress.XtraEditors.SimpleButton();
            tbCsvFile = new DevExpress.XtraEditors.TextEdit();
            ucPanelLog1 = new Dual.Common.Winform.DevX.UserControls.UcPanelLog();
            ucRadioSelector1 = new UcRadioSelector();
            btnMergeAppSettings = new DevExpress.XtraEditors.SimpleButton();
            cbMergeAppSettingsOverride = new DevExpress.XtraEditors.CheckEdit();
            ((ISupportInitialize)groupControl1).BeginInit();
            groupControl1.SuspendLayout();
            ((ISupportInitialize)tbNumTagsCategorized.Properties).BeginInit();
            ((ISupportInitialize)tbNumTagsDiscarded.Properties).BeginInit();
            ((ISupportInitialize)tbNumTagsChosen.Properties).BeginInit();
            ((ISupportInitialize)tbNumTagsStage.Properties).BeginInit();
            ((ISupportInitialize)tbNumTagsAll.Properties).BeginInit();
            ((ISupportInitialize)groupControl2).BeginInit();
            groupControl2.SuspendLayout();
            ((ISupportInitialize)groupControl4).BeginInit();
            groupControl4.SuspendLayout();
            ((ISupportInitialize)tbCsvFile.Properties).BeginInit();
            ((ISupportInitialize)cbMergeAppSettingsOverride.Properties).BeginInit();
            SuspendLayout();
            // 
            // btnLoadTags
            // 
            btnLoadTags.ImageOptions.Image = (System.Drawing.Image)resources.GetObject("btnLoadTags.ImageOptions.Image");
            btnLoadTags.Location = new System.Drawing.Point(12, 158);
            btnLoadTags.Margin = new Padding(2);
            btnLoadTags.Name = "btnLoadTags";
            btnLoadTags.Size = new System.Drawing.Size(163, 34);
            btnLoadTags.TabIndex = 0;
            btnLoadTags.Text = "Load tags..";
            btnLoadTags.ToolTip = "다음 두가지 type 에 대해서 tags 를 읽어 들인다.\r\n- *.csv: PLC vendor 에서 제공하는 tag export 기능을 이용해서 저장한 파일\r\n  - Siemens 의 경우, *.sdf\r\n- *.json: 이 프로그램에서 저장한 tag file";
            btnLoadTags.Click += btnLoadTags_Click;
            // 
            // btnShowAllTags
            // 
            btnShowAllTags.Location = new System.Drawing.Point(3, 40);
            btnShowAllTags.Margin = new Padding(2);
            btnShowAllTags.Name = "btnShowAllTags";
            btnShowAllTags.Size = new System.Drawing.Size(90, 34);
            btnShowAllTags.TabIndex = 1;
            btnShowAllTags.Text = "All";
            btnShowAllTags.ToolTip = "모든 tags 보이기\r\n- CsvFilterPatterns 에 의해서 버려진 tags 는 제외한 모든 tags";
            // 
            // groupControl1
            // 
            groupControl1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            groupControl1.Controls.Add(tbNumTagsCategorized);
            groupControl1.Controls.Add(btnShowCategorizedTags);
            groupControl1.Controls.Add(tbNumTagsDiscarded);
            groupControl1.Controls.Add(tbNumTagsChosen);
            groupControl1.Controls.Add(tbNumTagsStage);
            groupControl1.Controls.Add(tbNumTagsAll);
            groupControl1.Controls.Add(btnShowDiscardedTags);
            groupControl1.Controls.Add(btnShowChosenTags);
            groupControl1.Controls.Add(btnShowStageTags);
            groupControl1.Controls.Add(btnShowAllTags);
            groupControl1.Location = new System.Drawing.Point(18, 492);
            groupControl1.Margin = new Padding(2);
            groupControl1.Name = "groupControl1";
            groupControl1.Size = new System.Drawing.Size(1023, 123);
            groupControl1.TabIndex = 3;
            groupControl1.Text = "Show tags";
            // 
            // tbNumTagsCategorized
            // 
            tbNumTagsCategorized.Location = new System.Drawing.Point(343, 80);
            tbNumTagsCategorized.Margin = new Padding(2);
            tbNumTagsCategorized.Name = "tbNumTagsCategorized";
            tbNumTagsCategorized.Properties.ReadOnly = true;
            tbNumTagsCategorized.Size = new System.Drawing.Size(110, 28);
            tbNumTagsCategorized.TabIndex = 12;
            // 
            // btnShowCategorizedTags
            // 
            btnShowCategorizedTags.Location = new System.Drawing.Point(343, 40);
            btnShowCategorizedTags.Margin = new Padding(2);
            btnShowCategorizedTags.Name = "btnShowCategorizedTags";
            btnShowCategorizedTags.Size = new System.Drawing.Size(110, 34);
            btnShowCategorizedTags.TabIndex = 11;
            btnShowCategorizedTags.Text = "Categorized";
            btnShowCategorizedTags.ToolTip = "Split 되어 Flow, Device, Action 명이 지정된 tags";
            // 
            // tbNumTagsDiscarded
            // 
            tbNumTagsDiscarded.Location = new System.Drawing.Point(474, 80);
            tbNumTagsDiscarded.Margin = new Padding(2);
            tbNumTagsDiscarded.Name = "tbNumTagsDiscarded";
            tbNumTagsDiscarded.Properties.ReadOnly = true;
            tbNumTagsDiscarded.Size = new System.Drawing.Size(90, 28);
            tbNumTagsDiscarded.TabIndex = 10;
            // 
            // tbNumTagsChosen
            // 
            tbNumTagsChosen.Location = new System.Drawing.Point(229, 80);
            tbNumTagsChosen.Margin = new Padding(2);
            tbNumTagsChosen.Name = "tbNumTagsChosen";
            tbNumTagsChosen.Properties.ReadOnly = true;
            tbNumTagsChosen.Size = new System.Drawing.Size(90, 28);
            tbNumTagsChosen.TabIndex = 9;
            // 
            // tbNumTagsStage
            // 
            tbNumTagsStage.Location = new System.Drawing.Point(116, 80);
            tbNumTagsStage.Margin = new Padding(2);
            tbNumTagsStage.Name = "tbNumTagsStage";
            tbNumTagsStage.Properties.ReadOnly = true;
            tbNumTagsStage.Size = new System.Drawing.Size(90, 28);
            tbNumTagsStage.TabIndex = 8;
            // 
            // tbNumTagsAll
            // 
            tbNumTagsAll.Location = new System.Drawing.Point(3, 80);
            tbNumTagsAll.Margin = new Padding(2);
            tbNumTagsAll.Name = "tbNumTagsAll";
            tbNumTagsAll.Properties.ReadOnly = true;
            tbNumTagsAll.Size = new System.Drawing.Size(90, 28);
            tbNumTagsAll.TabIndex = 7;
            // 
            // btnShowDiscardedTags
            // 
            btnShowDiscardedTags.Location = new System.Drawing.Point(474, 40);
            btnShowDiscardedTags.Margin = new Padding(2);
            btnShowDiscardedTags.Name = "btnShowDiscardedTags";
            btnShowDiscardedTags.Size = new System.Drawing.Size(90, 34);
            btnShowDiscardedTags.TabIndex = 6;
            btnShowDiscardedTags.Text = "Discarded";
            btnShowDiscardedTags.ToolTip = "버려진 tags";
            // 
            // btnShowChosenTags
            // 
            btnShowChosenTags.Location = new System.Drawing.Point(229, 40);
            btnShowChosenTags.Margin = new Padding(2);
            btnShowChosenTags.Name = "btnShowChosenTags";
            btnShowChosenTags.Size = new System.Drawing.Size(90, 34);
            btnShowChosenTags.TabIndex = 5;
            btnShowChosenTags.Text = "Chosen";
            btnShowChosenTags.ToolTip = "취할 tags.  버려질 tags 가 아님을 의미";
            // 
            // btnShowStageTags
            // 
            btnShowStageTags.Location = new System.Drawing.Point(116, 40);
            btnShowStageTags.Margin = new Padding(2);
            btnShowStageTags.Name = "btnShowStageTags";
            btnShowStageTags.Size = new System.Drawing.Size(90, 34);
            btnShowStageTags.TabIndex = 4;
            btnShowStageTags.Text = "Stage";
            btnShowStageTags.ToolTip = "버릴지 취할지 결정되지 않은 tags";
            // 
            // btnDiscardTags
            // 
            btnDiscardTags.Location = new System.Drawing.Point(6, 43);
            btnDiscardTags.Margin = new Padding(2);
            btnDiscardTags.Name = "btnDiscardTags";
            btnDiscardTags.Size = new System.Drawing.Size(139, 45);
            btnDiscardTags.TabIndex = 7;
            btnDiscardTags.Text = "Discard";
            btnDiscardTags.ToolTip = "TagPatternDiscards 를 이용해서 Tags 의 이름에서 match 되는 항목을 버립니다.";
            btnDiscardTags.Click += btnDiscardTags_Click;
            // 
            // groupControl2
            // 
            groupControl2.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            groupControl2.Controls.Add(btnReplaceTags);
            groupControl2.Controls.Add(btnSplitFDA);
            groupControl2.Controls.Add(btnDiscardTags);
            groupControl2.Location = new System.Drawing.Point(12, 206);
            groupControl2.Margin = new Padding(2);
            groupControl2.Name = "groupControl2";
            groupControl2.Size = new System.Drawing.Size(1029, 95);
            groupControl2.TabIndex = 9;
            groupControl2.Text = "Tags";
            // 
            // btnReplaceTags
            // 
            btnReplaceTags.Location = new System.Drawing.Point(164, 43);
            btnReplaceTags.Margin = new Padding(2);
            btnReplaceTags.Name = "btnReplaceTags";
            btnReplaceTags.Size = new System.Drawing.Size(139, 45);
            btnReplaceTags.TabIndex = 11;
            btnReplaceTags.Text = "Replace";
            btnReplaceTags.ToolTip = "TagPatternReplaces 를 이용해서 Tags 의 이름 자체를 치환합니다.";
            btnReplaceTags.Click += btnReplaceTags_Click;
            // 
            // btnSplitFDA
            // 
            btnSplitFDA.Location = new System.Drawing.Point(330, 43);
            btnSplitFDA.Margin = new Padding(2);
            btnSplitFDA.Name = "btnSplitFDA";
            btnSplitFDA.Size = new System.Drawing.Size(139, 45);
            btnSplitFDA.TabIndex = 10;
            btnSplitFDA.Text = "Split";
            btnSplitFDA.ToolTip = "Flow, Device, Action 명 추출\r\nTagPatternFDAs 를 이용해서 Tags 이름을 Flow, Device, Action 으로 나눕니다.";
            btnSplitFDA.Click += btnSplitFDA_Click;
            // 
            // labelControl1
            // 
            labelControl1.Location = new System.Drawing.Point(17, 61);
            labelControl1.Name = "labelControl1";
            labelControl1.Size = new System.Drawing.Size(62, 22);
            labelControl1.TabIndex = 12;
            labelControl1.Text = "Vendor:";
            // 
            // groupControl4
            // 
            groupControl4.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            groupControl4.Controls.Add(btnReplaceActionName);
            groupControl4.Controls.Add(btnReplaceDeviceName);
            groupControl4.Controls.Add(btnReplaceFlowName);
            groupControl4.Location = new System.Drawing.Point(12, 315);
            groupControl4.Margin = new Padding(2);
            groupControl4.Name = "groupControl4";
            groupControl4.Size = new System.Drawing.Size(1029, 98);
            groupControl4.TabIndex = 13;
            groupControl4.Text = "Discards F/D/A";
            // 
            // btnReplaceActionName
            // 
            btnReplaceActionName.Location = new System.Drawing.Point(330, 43);
            btnReplaceActionName.Margin = new Padding(2);
            btnReplaceActionName.Name = "btnReplaceActionName";
            btnReplaceActionName.Size = new System.Drawing.Size(139, 45);
            btnReplaceActionName.TabIndex = 12;
            btnReplaceActionName.Text = "Action";
            btnReplaceActionName.ToolTip = "Action 명에서 불필요한 부분 제거\r\n- ActionPatternReplaces 에 정의";
            // 
            // btnReplaceDeviceName
            // 
            btnReplaceDeviceName.Location = new System.Drawing.Point(164, 43);
            btnReplaceDeviceName.Margin = new Padding(2);
            btnReplaceDeviceName.Name = "btnReplaceDeviceName";
            btnReplaceDeviceName.Size = new System.Drawing.Size(139, 45);
            btnReplaceDeviceName.TabIndex = 11;
            btnReplaceDeviceName.Text = "Device";
            btnReplaceDeviceName.ToolTip = "Device 명에서 불필요한 부분 제거\r\n- DevicePatternReplaces 에 정의";
            // 
            // btnReplaceFlowName
            // 
            btnReplaceFlowName.Location = new System.Drawing.Point(4, 43);
            btnReplaceFlowName.Margin = new Padding(2);
            btnReplaceFlowName.Name = "btnReplaceFlowName";
            btnReplaceFlowName.Size = new System.Drawing.Size(139, 45);
            btnReplaceFlowName.TabIndex = 10;
            btnReplaceFlowName.Text = "Flow";
            btnReplaceFlowName.ToolTip = "Flow 명에서 불필요한 부분 제거\r\n- FlowPatternReplaces 에 정의";
            // 
            // btnApplyAll
            // 
            btnApplyAll.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnApplyAll.Location = new System.Drawing.Point(807, 432);
            btnApplyAll.Margin = new Padding(2);
            btnApplyAll.Name = "btnApplyAll";
            btnApplyAll.Size = new System.Drawing.Size(234, 45);
            btnApplyAll.TabIndex = 14;
            btnApplyAll.Text = "Apply all rules";
            btnApplyAll.ToolTip = "모든 규칙 일괄 적용";
            btnApplyAll.Click += btnApplyAll_Click;
            // 
            // tbCsvFile
            // 
            tbCsvFile.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            tbCsvFile.Location = new System.Drawing.Point(191, 162);
            tbCsvFile.Margin = new Padding(2);
            tbCsvFile.Name = "tbCsvFile";
            tbCsvFile.Size = new System.Drawing.Size(854, 28);
            tbCsvFile.TabIndex = 2;
            // 
            // ucPanelLog1
            // 
            ucPanelLog1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            ucPanelLog1.Location = new System.Drawing.Point(12, 622);
            ucPanelLog1.Margin = new Padding(4, 5, 4, 5);
            ucPanelLog1.Name = "ucPanelLog1";
            ucPanelLog1.SelectedIndex = -1;
            ucPanelLog1.Size = new System.Drawing.Size(1029, 246);
            ucPanelLog1.TabIndex = 15;
            // 
            // ucRadioSelector1
            // 
            ucRadioSelector1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            ucRadioSelector1.Location = new System.Drawing.Point(148, 51);
            ucRadioSelector1.Margin = new Padding(2, 3, 2, 3);
            ucRadioSelector1.Name = "ucRadioSelector1";
            ucRadioSelector1.Size = new System.Drawing.Size(897, 39);
            ucRadioSelector1.TabIndex = 11;
            // 
            // btnMergeAppSettings
            // 
            btnMergeAppSettings.Location = new System.Drawing.Point(12, 101);
            btnMergeAppSettings.Margin = new Padding(2);
            btnMergeAppSettings.Name = "btnMergeAppSettings";
            btnMergeAppSettings.Size = new System.Drawing.Size(181, 34);
            btnMergeAppSettings.TabIndex = 16;
            btnMergeAppSettings.Text = "Merge appSettings..";
            btnMergeAppSettings.ToolTip = "다음 두가지 type 에 대해서 tags 를 읽어 들인다.\r\n- *.csv: PLC vendor 에서 제공하는 tag export 기능을 이용해서 저장한 파일\r\n  - Siemens 의 경우, *.sdf\r\n- *.json: 이 프로그램에서 저장한 tag file";
            btnMergeAppSettings.Click += btnMergeAppSettings_Click;
            // 
            // cbMergeAppSettingsOverride
            // 
            cbMergeAppSettingsOverride.Location = new System.Drawing.Point(223, 107);
            cbMergeAppSettingsOverride.Name = "cbMergeAppSettingsOverride";
            cbMergeAppSettingsOverride.Properties.Caption = "Override";
            cbMergeAppSettingsOverride.Size = new System.Drawing.Size(112, 27);
            cbMergeAppSettingsOverride.TabIndex = 17;
            // 
            // FormMain
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(10F, 22F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1056, 882);
            Controls.Add(cbMergeAppSettingsOverride);
            Controls.Add(btnMergeAppSettings);
            Controls.Add(ucPanelLog1);
            Controls.Add(btnApplyAll);
            Controls.Add(groupControl4);
            Controls.Add(labelControl1);
            Controls.Add(ucRadioSelector1);
            Controls.Add(groupControl2);
            Controls.Add(groupControl1);
            Controls.Add(tbCsvFile);
            Controls.Add(btnLoadTags);
            Margin = new Padding(5);
            Name = "FormMain";
            Text = "Plc2Ds";
            Load += FormMain_Load;
            ((ISupportInitialize)groupControl1).EndInit();
            groupControl1.ResumeLayout(false);
            ((ISupportInitialize)tbNumTagsCategorized.Properties).EndInit();
            ((ISupportInitialize)tbNumTagsDiscarded.Properties).EndInit();
            ((ISupportInitialize)tbNumTagsChosen.Properties).EndInit();
            ((ISupportInitialize)tbNumTagsStage.Properties).EndInit();
            ((ISupportInitialize)tbNumTagsAll.Properties).EndInit();
            ((ISupportInitialize)groupControl2).EndInit();
            groupControl2.ResumeLayout(false);
            ((ISupportInitialize)groupControl4).EndInit();
            groupControl4.ResumeLayout(false);
            ((ISupportInitialize)tbCsvFile.Properties).EndInit();
            ((ISupportInitialize)cbMergeAppSettingsOverride.Properties).EndInit();
            ResumeLayout(false);
            PerformLayout();

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
        private Dual.Common.Winform.DevX.UserControls.UcPanelLog ucPanelLog1;
        private DevExpress.XtraEditors.SimpleButton btnMergeAppSettings;
        private DevExpress.XtraEditors.CheckEdit cbMergeAppSettingsOverride;
    }
}

