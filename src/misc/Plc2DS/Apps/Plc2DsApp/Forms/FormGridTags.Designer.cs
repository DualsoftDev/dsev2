namespace Plc2DsApp
{
    partial class FormGridTags
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
            this.ucDxGrid1 = new Dual.Common.Winform.DevX.UcDxGrid();
            this.cbShowWithFDA = new DevExpress.XtraEditors.CheckEdit();
            this.cbShowWithoutFDA = new DevExpress.XtraEditors.CheckEdit();
            this.btnCancel = new DevExpress.XtraEditors.SimpleButton();
            this.btnOK = new DevExpress.XtraEditors.SimpleButton();
            ((System.ComponentModel.ISupportInitialize)(this.cbShowWithFDA.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.cbShowWithoutFDA.Properties)).BeginInit();
            this.SuspendLayout();
            // 
            // ucDxGrid1
            // 
            this.ucDxGrid1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.ucDxGrid1.DataSource = null;
            this.ucDxGrid1.Location = new System.Drawing.Point(13, 1);
            this.ucDxGrid1.Margin = new System.Windows.Forms.Padding(2, 3, 2, 3);
            this.ucDxGrid1.Name = "ucDxGrid1";
            this.ucDxGrid1.Size = new System.Drawing.Size(1130, 564);
            this.ucDxGrid1.TabIndex = 0;
            // 
            // cbShowWithFDA
            // 
            this.cbShowWithFDA.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.cbShowWithFDA.EditValue = true;
            this.cbShowWithFDA.Location = new System.Drawing.Point(0, 644);
            this.cbShowWithFDA.Name = "cbShowWithFDA";
            this.cbShowWithFDA.Properties.Caption = "Show w/ FDA";
            this.cbShowWithFDA.Size = new System.Drawing.Size(155, 27);
            this.cbShowWithFDA.TabIndex = 1;
            // 
            // cbShowWithoutFDA
            // 
            this.cbShowWithoutFDA.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.cbShowWithoutFDA.Location = new System.Drawing.Point(0, 617);
            this.cbShowWithoutFDA.Name = "cbShowWithoutFDA";
            this.cbShowWithoutFDA.Properties.Caption = "Show w/o FDA";
            this.cbShowWithoutFDA.Size = new System.Drawing.Size(171, 27);
            this.cbShowWithoutFDA.TabIndex = 2;
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(1073, 624);
            this.btnCancel.Margin = new System.Windows.Forms.Padding(2, 3, 2, 3);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(70, 35);
            this.btnCancel.TabIndex = 6;
            this.btnCancel.Text = "Cancel";
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(997, 624);
            this.btnOK.Margin = new System.Windows.Forms.Padding(2, 3, 2, 3);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(70, 35);
            this.btnOK.TabIndex = 5;
            this.btnOK.Text = "OK";
            // 
            // FormGridTags
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(10F, 22F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(1154, 671);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.cbShowWithoutFDA);
            this.Controls.Add(this.cbShowWithFDA);
            this.Controls.Add(this.ucDxGrid1);
            this.Name = "FormGridTags";
            this.Text = "FormGridTags";
            this.Load += new System.EventHandler(this.FormGridTags_Load);
            ((System.ComponentModel.ISupportInitialize)(this.cbShowWithFDA.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.cbShowWithoutFDA.Properties)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private Dual.Common.Winform.DevX.UcDxGrid ucDxGrid1;
        private DevExpress.XtraEditors.CheckEdit cbShowWithFDA;
        private DevExpress.XtraEditors.CheckEdit cbShowWithoutFDA;
        private DevExpress.XtraEditors.SimpleButton btnCancel;
        private DevExpress.XtraEditors.SimpleButton btnOK;
    }
}