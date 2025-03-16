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
        private void InitializeComponent()
        {
            this.btnOpenCSV = new DevExpress.XtraEditors.SimpleButton();
            this.btnShowAllTags = new DevExpress.XtraEditors.SimpleButton();
            this.tbCsvFile = new DevExpress.XtraEditors.TextEdit();
            this.groupControl1 = new DevExpress.XtraEditors.GroupControl();
            this.tbNumTagsDiscarded = new DevExpress.XtraEditors.TextEdit();
            this.tbNumTagsFixed = new DevExpress.XtraEditors.TextEdit();
            this.tbNumTagsNotyet = new DevExpress.XtraEditors.TextEdit();
            this.tbNumTagsAll = new DevExpress.XtraEditors.TextEdit();
            this.btnShowDiscardedTags = new DevExpress.XtraEditors.SimpleButton();
            this.btnShowFixedTags = new DevExpress.XtraEditors.SimpleButton();
            this.btnShowNotyetTags = new DevExpress.XtraEditors.SimpleButton();
            this.btnReadCsvFile = new DevExpress.XtraEditors.SimpleButton();
            this.btnDiscardTags = new DevExpress.XtraEditors.SimpleButton();
            this.groupControl2 = new DevExpress.XtraEditors.GroupControl();
            this.btnExtractFDA = new DevExpress.XtraEditors.SimpleButton();
            ((System.ComponentModel.ISupportInitialize)(this.tbCsvFile.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.groupControl1)).BeginInit();
            this.groupControl1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tbNumTagsDiscarded.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbNumTagsFixed.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbNumTagsNotyet.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbNumTagsAll.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.groupControl2)).BeginInit();
            this.groupControl2.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnOpenCSV
            // 
            this.btnOpenCSV.Location = new System.Drawing.Point(10, 25);
            this.btnOpenCSV.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.btnOpenCSV.Name = "btnOpenCSV";
            this.btnOpenCSV.Size = new System.Drawing.Size(90, 28);
            this.btnOpenCSV.TabIndex = 0;
            this.btnOpenCSV.Text = "Select CSV..";
            this.btnOpenCSV.Click += new System.EventHandler(this.btnSelectCSV_Click);
            // 
            // btnShowAllTags
            // 
            this.btnShowAllTags.Location = new System.Drawing.Point(4, 34);
            this.btnShowAllTags.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.btnShowAllTags.Name = "btnShowAllTags";
            this.btnShowAllTags.Size = new System.Drawing.Size(72, 28);
            this.btnShowAllTags.TabIndex = 1;
            this.btnShowAllTags.Text = "All";
            // 
            // tbCsvFile
            // 
            this.tbCsvFile.Location = new System.Drawing.Point(118, 29);
            this.tbCsvFile.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.tbCsvFile.Name = "tbCsvFile";
            this.tbCsvFile.Size = new System.Drawing.Size(366, 24);
            this.tbCsvFile.TabIndex = 2;
            // 
            // groupControl1
            // 
            this.groupControl1.Controls.Add(this.tbNumTagsDiscarded);
            this.groupControl1.Controls.Add(this.tbNumTagsFixed);
            this.groupControl1.Controls.Add(this.tbNumTagsNotyet);
            this.groupControl1.Controls.Add(this.tbNumTagsAll);
            this.groupControl1.Controls.Add(this.btnShowDiscardedTags);
            this.groupControl1.Controls.Add(this.btnShowFixedTags);
            this.groupControl1.Controls.Add(this.btnShowNotyetTags);
            this.groupControl1.Controls.Add(this.btnShowAllTags);
            this.groupControl1.Location = new System.Drawing.Point(10, 275);
            this.groupControl1.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.groupControl1.Name = "groupControl1";
            this.groupControl1.Size = new System.Drawing.Size(372, 100);
            this.groupControl1.TabIndex = 3;
            this.groupControl1.Text = "Show tags";
            // 
            // tbNumTagsDiscarded
            // 
            this.tbNumTagsDiscarded.Location = new System.Drawing.Point(275, 67);
            this.tbNumTagsDiscarded.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.tbNumTagsDiscarded.Name = "tbNumTagsDiscarded";
            this.tbNumTagsDiscarded.Properties.ReadOnly = true;
            this.tbNumTagsDiscarded.Size = new System.Drawing.Size(72, 24);
            this.tbNumTagsDiscarded.TabIndex = 10;
            // 
            // tbNumTagsFixed
            // 
            this.tbNumTagsFixed.Location = new System.Drawing.Point(185, 67);
            this.tbNumTagsFixed.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.tbNumTagsFixed.Name = "tbNumTagsFixed";
            this.tbNumTagsFixed.Properties.ReadOnly = true;
            this.tbNumTagsFixed.Size = new System.Drawing.Size(72, 24);
            this.tbNumTagsFixed.TabIndex = 9;
            // 
            // tbNumTagsNotyet
            // 
            this.tbNumTagsNotyet.Location = new System.Drawing.Point(94, 67);
            this.tbNumTagsNotyet.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.tbNumTagsNotyet.Name = "tbNumTagsNotyet";
            this.tbNumTagsNotyet.Properties.ReadOnly = true;
            this.tbNumTagsNotyet.Size = new System.Drawing.Size(72, 24);
            this.tbNumTagsNotyet.TabIndex = 8;
            // 
            // tbNumTagsAll
            // 
            this.tbNumTagsAll.Location = new System.Drawing.Point(4, 67);
            this.tbNumTagsAll.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.tbNumTagsAll.Name = "tbNumTagsAll";
            this.tbNumTagsAll.Properties.ReadOnly = true;
            this.tbNumTagsAll.Size = new System.Drawing.Size(72, 24);
            this.tbNumTagsAll.TabIndex = 7;
            // 
            // btnShowDiscardedTags
            // 
            this.btnShowDiscardedTags.Location = new System.Drawing.Point(275, 34);
            this.btnShowDiscardedTags.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.btnShowDiscardedTags.Name = "btnShowDiscardedTags";
            this.btnShowDiscardedTags.Size = new System.Drawing.Size(72, 28);
            this.btnShowDiscardedTags.TabIndex = 6;
            this.btnShowDiscardedTags.Text = "Discarded";
            // 
            // btnShowFixedTags
            // 
            this.btnShowFixedTags.Location = new System.Drawing.Point(185, 34);
            this.btnShowFixedTags.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.btnShowFixedTags.Name = "btnShowFixedTags";
            this.btnShowFixedTags.Size = new System.Drawing.Size(72, 28);
            this.btnShowFixedTags.TabIndex = 5;
            this.btnShowFixedTags.Text = "Fixed";
            // 
            // btnShowNotyetTags
            // 
            this.btnShowNotyetTags.Location = new System.Drawing.Point(94, 34);
            this.btnShowNotyetTags.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.btnShowNotyetTags.Name = "btnShowNotyetTags";
            this.btnShowNotyetTags.Size = new System.Drawing.Size(72, 28);
            this.btnShowNotyetTags.TabIndex = 4;
            this.btnShowNotyetTags.Text = "Working";
            // 
            // btnReadCsvFile
            // 
            this.btnReadCsvFile.Location = new System.Drawing.Point(497, 25);
            this.btnReadCsvFile.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.btnReadCsvFile.Name = "btnReadCsvFile";
            this.btnReadCsvFile.Size = new System.Drawing.Size(90, 28);
            this.btnReadCsvFile.TabIndex = 6;
            this.btnReadCsvFile.Text = "Read";
            // 
            // btnDiscardTags
            // 
            this.btnDiscardTags.Location = new System.Drawing.Point(4, 30);
            this.btnDiscardTags.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.btnDiscardTags.Name = "btnDiscardTags";
            this.btnDiscardTags.Size = new System.Drawing.Size(111, 37);
            this.btnDiscardTags.TabIndex = 7;
            this.btnDiscardTags.Text = "Discard";
            this.btnDiscardTags.Click += new System.EventHandler(this.btnDiscardTags_Click);
            // 
            // groupControl2
            // 
            this.groupControl2.Controls.Add(this.btnDiscardTags);
            this.groupControl2.Location = new System.Drawing.Point(10, 75);
            this.groupControl2.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.groupControl2.Name = "groupControl2";
            this.groupControl2.Size = new System.Drawing.Size(241, 77);
            this.groupControl2.TabIndex = 9;
            this.groupControl2.Text = "Select tags";
            // 
            // btnExtractFDA
            // 
            this.btnExtractFDA.Location = new System.Drawing.Point(14, 205);
            this.btnExtractFDA.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.btnExtractFDA.Name = "btnExtractFDA";
            this.btnExtractFDA.Size = new System.Drawing.Size(111, 37);
            this.btnExtractFDA.TabIndex = 10;
            this.btnExtractFDA.Text = "Extract";
            this.btnExtractFDA.ToolTip = "Flow, Device, Action 명 추출";
            this.btnExtractFDA.Click += new System.EventHandler(this.btnExtractFDA_Click);
            // 
            // FormMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(842, 385);
            this.Controls.Add(this.btnExtractFDA);
            this.Controls.Add(this.groupControl2);
            this.Controls.Add(this.btnReadCsvFile);
            this.Controls.Add(this.groupControl1);
            this.Controls.Add(this.tbCsvFile);
            this.Controls.Add(this.btnOpenCSV);
            this.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.Name = "FormMain";
            this.Text = "Plc2Ds";
            this.Load += new System.EventHandler(this.FormMain_Load);
            ((System.ComponentModel.ISupportInitialize)(this.tbCsvFile.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.groupControl1)).EndInit();
            this.groupControl1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.tbNumTagsDiscarded.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbNumTagsFixed.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbNumTagsNotyet.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbNumTagsAll.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.groupControl2)).EndInit();
            this.groupControl2.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private DevExpress.XtraEditors.SimpleButton btnOpenCSV;
        private DevExpress.XtraEditors.SimpleButton btnShowAllTags;
        private DevExpress.XtraEditors.TextEdit tbCsvFile;
        private DevExpress.XtraEditors.GroupControl groupControl1;
        private DevExpress.XtraEditors.SimpleButton btnShowDiscardedTags;
        private DevExpress.XtraEditors.SimpleButton btnShowFixedTags;
        private DevExpress.XtraEditors.SimpleButton btnShowNotyetTags;
        private DevExpress.XtraEditors.TextEdit tbNumTagsDiscarded;
        private DevExpress.XtraEditors.TextEdit tbNumTagsFixed;
        private DevExpress.XtraEditors.TextEdit tbNumTagsNotyet;
        private DevExpress.XtraEditors.TextEdit tbNumTagsAll;
        private DevExpress.XtraEditors.SimpleButton btnReadCsvFile;
        private DevExpress.XtraEditors.SimpleButton btnDiscardTags;
        private DevExpress.XtraEditors.GroupControl groupControl2;
        private DevExpress.XtraEditors.SimpleButton btnExtractFDA;
    }
}

