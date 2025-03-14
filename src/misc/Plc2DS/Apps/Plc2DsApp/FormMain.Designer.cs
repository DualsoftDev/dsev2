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
            this.btnShowTags = new DevExpress.XtraEditors.SimpleButton();
            this.tbCsvFile = new DevExpress.XtraEditors.TextEdit();
            ((System.ComponentModel.ISupportInitialize)(this.tbCsvFile.Properties)).BeginInit();
            this.SuspendLayout();
            // 
            // btnOpenCSV
            // 
            this.btnOpenCSV.Location = new System.Drawing.Point(29, 31);
            this.btnOpenCSV.Name = "btnOpenCSV";
            this.btnOpenCSV.Size = new System.Drawing.Size(112, 34);
            this.btnOpenCSV.TabIndex = 0;
            this.btnOpenCSV.Text = "Open CSV..";
            this.btnOpenCSV.Click += new System.EventHandler(this.btnOpenCSV_Click);
            // 
            // btnShowTags
            // 
            this.btnShowTags.Location = new System.Drawing.Point(29, 71);
            this.btnShowTags.Name = "btnShowTags";
            this.btnShowTags.Size = new System.Drawing.Size(112, 34);
            this.btnShowTags.TabIndex = 1;
            this.btnShowTags.Text = "Show Tags";
            // 
            // tbCsvFile
            // 
            this.tbCsvFile.Location = new System.Drawing.Point(175, 35);
            this.tbCsvFile.Name = "tbCsvFile";
            this.tbCsvFile.Size = new System.Drawing.Size(429, 28);
            this.tbCsvFile.TabIndex = 2;
            // 
            // FormMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(10F, 22F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1053, 470);
            this.Controls.Add(this.tbCsvFile);
            this.Controls.Add(this.btnShowTags);
            this.Controls.Add(this.btnOpenCSV);
            this.Margin = new System.Windows.Forms.Padding(5, 5, 5, 5);
            this.Name = "FormMain";
            this.Text = "Plc2Ds";
            ((System.ComponentModel.ISupportInitialize)(this.tbCsvFile.Properties)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private DevExpress.XtraEditors.SimpleButton btnOpenCSV;
        private DevExpress.XtraEditors.SimpleButton btnShowTags;
        private DevExpress.XtraEditors.TextEdit tbCsvFile;
    }
}

