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
        void InitializeComponent()
        {
            btnCancel = new DevExpress.XtraEditors.SimpleButton();
            btnOK = new DevExpress.XtraEditors.SimpleButton();
            gridControl1 = new DevExpress.XtraGrid.GridControl();
            gridView1 = new GridView();
            btnSaveTagsAs = new DevExpress.XtraEditors.SimpleButton();
            btnMasterDetailView = new DevExpress.XtraEditors.SimpleButton();
            ((ISupportInitialize)gridControl1).BeginInit();
            ((ISupportInitialize)gridView1).BeginInit();
            SuspendLayout();
            // 
            // btnCancel
            // 
            btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnCancel.DialogResult = DialogResult.Cancel;
            btnCancel.Location = new System.Drawing.Point(981, 621);
            btnCancel.Margin = new Padding(2);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new System.Drawing.Size(70, 35);
            btnCancel.TabIndex = 6;
            btnCancel.Text = "Cancel";
            // 
            // btnOK
            // 
            btnOK.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnOK.DialogResult = DialogResult.OK;
            btnOK.Location = new System.Drawing.Point(906, 621);
            btnOK.Margin = new Padding(2);
            btnOK.Name = "btnOK";
            btnOK.Size = new System.Drawing.Size(70, 35);
            btnOK.TabIndex = 5;
            btnOK.Text = "OK";
            // 
            // gridControl1
            // 
            gridControl1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            gridControl1.EmbeddedNavigator.Margin = new Padding(2);
            gridControl1.Location = new System.Drawing.Point(12, 27);
            gridControl1.MainView = gridView1;
            gridControl1.Margin = new Padding(2);
            gridControl1.Name = "gridControl1";
            gridControl1.Size = new System.Drawing.Size(1039, 544);
            gridControl1.TabIndex = 7;
            gridControl1.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] { gridView1 });
            // 
            // gridView1
            // 
            gridView1.GridControl = gridControl1;
            gridView1.Name = "gridView1";
            // 
            // btnSaveTagsAs
            // 
            btnSaveTagsAs.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnSaveTagsAs.Location = new System.Drawing.Point(681, 621);
            btnSaveTagsAs.Margin = new Padding(2);
            btnSaveTagsAs.Name = "btnSaveTagsAs";
            btnSaveTagsAs.Size = new System.Drawing.Size(166, 35);
            btnSaveTagsAs.TabIndex = 8;
            btnSaveTagsAs.Text = "Save as..";
            btnSaveTagsAs.Click += btnSaveTagsAs_Click;
            // 
            // btnMasterDetailView
            // 
            btnMasterDetailView.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnMasterDetailView.Location = new System.Drawing.Point(439, 621);
            btnMasterDetailView.Margin = new Padding(2);
            btnMasterDetailView.Name = "btnMasterDetailView";
            btnMasterDetailView.Size = new System.Drawing.Size(213, 35);
            btnMasterDetailView.TabIndex = 9;
            btnMasterDetailView.Text = "Flow/Device grouping..";
            btnMasterDetailView.Click += btnMasterDetailView_Click;
            // 
            // FormTags
            // 
            AcceptButton = btnOK;
            AutoScaleDimensions = new System.Drawing.SizeF(10F, 22F);
            AutoScaleMode = AutoScaleMode.Font;
            CancelButton = btnCancel;
            ClientSize = new System.Drawing.Size(1062, 669);
            Controls.Add(btnMasterDetailView);
            Controls.Add(btnSaveTagsAs);
            Controls.Add(gridControl1);
            Controls.Add(btnCancel);
            Controls.Add(btnOK);
            Margin = new Padding(2);
            Name = "FormTags";
            Text = "FormGridTags";
            Load += FormGridTags_Load;
            ((ISupportInitialize)gridControl1).EndInit();
            ((ISupportInitialize)gridView1).EndInit();
            ResumeLayout(false);

        }

        #endregion
        private DevExpress.XtraEditors.SimpleButton btnCancel;
        private DevExpress.XtraEditors.SimpleButton btnOK;
        private DevExpress.XtraGrid.GridControl gridControl1;
        private DevExpress.XtraGrid.Views.Grid.GridView gridView1;
        private DevExpress.XtraEditors.SimpleButton btnSaveTagsAs;
        private DevExpress.XtraEditors.SimpleButton btnMasterDetailView;
    }
}