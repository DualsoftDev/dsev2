namespace Plc2DsApp.Forms
{
    partial class FormTags
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
            this.cbShowWithFDA = new DevExpress.XtraEditors.CheckEdit();
            this.cbShowWithoutFDA = new DevExpress.XtraEditors.CheckEdit();
            this.btnCancel = new DevExpress.XtraEditors.SimpleButton();
            this.btnOK = new DevExpress.XtraEditors.SimpleButton();
            this.gridControl1 = new DevExpress.XtraGrid.GridControl();
            this.gridView1 = new DevExpress.XtraGrid.Views.Grid.GridView();
            this.btnSaveTagsAs = new DevExpress.XtraEditors.SimpleButton();
            this.btnMasterDetailView = new DevExpress.XtraEditors.SimpleButton();
            ((System.ComponentModel.ISupportInitialize)(this.cbShowWithFDA.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.cbShowWithoutFDA.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridControl1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridView1)).BeginInit();
            this.SuspendLayout();
            // 
            // cbShowWithFDA
            // 
            this.cbShowWithFDA.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.cbShowWithFDA.EditValue = true;
            this.cbShowWithFDA.Location = new System.Drawing.Point(0, 642);
            this.cbShowWithFDA.Margin = new System.Windows.Forms.Padding(2);
            this.cbShowWithFDA.Name = "cbShowWithFDA";
            this.cbShowWithFDA.Properties.Caption = "Show w/ FDA";
            this.cbShowWithFDA.Size = new System.Drawing.Size(155, 27);
            this.cbShowWithFDA.TabIndex = 1;
            // 
            // cbShowWithoutFDA
            // 
            this.cbShowWithoutFDA.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.cbShowWithoutFDA.Location = new System.Drawing.Point(0, 615);
            this.cbShowWithoutFDA.Margin = new System.Windows.Forms.Padding(2);
            this.cbShowWithoutFDA.Name = "cbShowWithoutFDA";
            this.cbShowWithoutFDA.Properties.Caption = "Show w/o FDA";
            this.cbShowWithoutFDA.Size = new System.Drawing.Size(171, 27);
            this.cbShowWithoutFDA.TabIndex = 2;
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(981, 621);
            this.btnCancel.Margin = new System.Windows.Forms.Padding(2);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(70, 35);
            this.btnCancel.TabIndex = 6;
            this.btnCancel.Text = "Cancel";
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(906, 621);
            this.btnOK.Margin = new System.Windows.Forms.Padding(2);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(70, 35);
            this.btnOK.TabIndex = 5;
            this.btnOK.Text = "OK";
            // 
            // gridControl1
            // 
            this.gridControl1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.gridControl1.EmbeddedNavigator.Margin = new System.Windows.Forms.Padding(2);
            this.gridControl1.Location = new System.Drawing.Point(12, 27);
            this.gridControl1.MainView = this.gridView1;
            this.gridControl1.Margin = new System.Windows.Forms.Padding(2);
            this.gridControl1.Name = "gridControl1";
            this.gridControl1.Size = new System.Drawing.Size(1039, 544);
            this.gridControl1.TabIndex = 7;
            this.gridControl1.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] {
            this.gridView1});
            // 
            // gridView1
            // 
            this.gridView1.GridControl = this.gridControl1;
            this.gridView1.Name = "gridView1";
            // 
            // btnSaveTagsAs
            // 
            this.btnSaveTagsAs.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSaveTagsAs.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnSaveTagsAs.Location = new System.Drawing.Point(681, 621);
            this.btnSaveTagsAs.Margin = new System.Windows.Forms.Padding(2);
            this.btnSaveTagsAs.Name = "btnSaveTagsAs";
            this.btnSaveTagsAs.Size = new System.Drawing.Size(166, 35);
            this.btnSaveTagsAs.TabIndex = 8;
            this.btnSaveTagsAs.Text = "Save as..";
            this.btnSaveTagsAs.Click += new System.EventHandler(this.btnSaveTagsAs_Click);
            // 
            // btnMasterDetailView
            // 
            this.btnMasterDetailView.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnMasterDetailView.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnMasterDetailView.Location = new System.Drawing.Point(439, 621);
            this.btnMasterDetailView.Margin = new System.Windows.Forms.Padding(2);
            this.btnMasterDetailView.Name = "btnMasterDetailView";
            this.btnMasterDetailView.Size = new System.Drawing.Size(213, 35);
            this.btnMasterDetailView.TabIndex = 9;
            this.btnMasterDetailView.Text = "Flow/Device grouping..";
            this.btnMasterDetailView.Click += new System.EventHandler(this.btnMasterDetailView_Click);
            // 
            // FormTags
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(10F, 22F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(1062, 669);
            this.Controls.Add(this.btnMasterDetailView);
            this.Controls.Add(this.btnSaveTagsAs);
            this.Controls.Add(this.gridControl1);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.cbShowWithoutFDA);
            this.Controls.Add(this.cbShowWithFDA);
            this.Margin = new System.Windows.Forms.Padding(2);
            this.Name = "FormTags";
            this.Text = "FormGridTags";
            this.Load += new System.EventHandler(this.FormGridTags_Load);
            ((System.ComponentModel.ISupportInitialize)(this.cbShowWithFDA.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.cbShowWithoutFDA.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridControl1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridView1)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion
        private DevExpress.XtraEditors.CheckEdit cbShowWithFDA;
        private DevExpress.XtraEditors.CheckEdit cbShowWithoutFDA;
        private DevExpress.XtraEditors.SimpleButton btnCancel;
        private DevExpress.XtraEditors.SimpleButton btnOK;
        private DevExpress.XtraGrid.GridControl gridControl1;
        private DevExpress.XtraGrid.Views.Grid.GridView gridView1;
        private DevExpress.XtraEditors.SimpleButton btnSaveTagsAs;
        private DevExpress.XtraEditors.SimpleButton btnMasterDetailView;
    }
}