namespace PLC.Convert.Mermaid
{
    partial class FormMermaid
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            button_openRockwell = new Button();
            button_openDir = new Button();
            button_MelsecConvert = new Button();
            button_SiemensConvert = new Button();
            button_LSEConvert = new Button();
            SuspendLayout();
            // 
            // button_openRockwell
            // 
            button_openRockwell.Location = new Point(218, 75);
            button_openRockwell.Name = "button_openRockwell";
            button_openRockwell.Size = new Size(111, 50);
            button_openRockwell.TabIndex = 0;
            button_openRockwell.Text = "Open RockwellAB(*.l5k)";
            button_openRockwell.UseVisualStyleBackColor = true;
            // 
            // button_openDir
            // 
            button_openDir.Location = new Point(720, 75);
            button_openDir.Name = "button_openDir";
            button_openDir.Size = new Size(111, 50);
            button_openDir.TabIndex = 0;
            button_openDir.Text = "Open Dir";
            button_openDir.UseVisualStyleBackColor = true;
            // 
            // button_MelsecConvert
            // 
            button_MelsecConvert.Location = new Point(338, 75);
            button_MelsecConvert.Name = "button_MelsecConvert";
            button_MelsecConvert.Size = new Size(111, 50);
            button_MelsecConvert.TabIndex = 0;
            button_MelsecConvert.Text = "Open Melsec(*.csv)";
            button_MelsecConvert.UseVisualStyleBackColor = true;
            // 
            // button_SiemensConvert
            // 
            button_SiemensConvert.Location = new Point(458, 75);
            button_SiemensConvert.Name = "button_SiemensConvert";
            button_SiemensConvert.Size = new Size(111, 50);
            button_SiemensConvert.TabIndex = 0;
            button_SiemensConvert.Text = "Open Siemens(*.AWL)";
            button_SiemensConvert.UseVisualStyleBackColor = true;
            // 
            // button_LSEConvert
            // 
            button_LSEConvert.Location = new Point(578, 75);
            button_LSEConvert.Name = "button_LSEConvert";
            button_LSEConvert.Size = new Size(111, 50);
            button_LSEConvert.TabIndex = 0;
            button_LSEConvert.Text = "Open LSElectric(*.xml)";
            button_LSEConvert.UseVisualStyleBackColor = true;
            // 
            // FormMermaid
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1493, 1009);
            Controls.Add(button_LSEConvert);
            Controls.Add(button_SiemensConvert);
            Controls.Add(button_MelsecConvert);
            Controls.Add(button_openDir);
            Controls.Add(button_openRockwell);
            Name = "FormMermaid";
            Text = "Form1";
            ResumeLayout(false);
        }

        #endregion

        private Button button_openRockwell;
        private Button button_openDir;
        private Button button_MelsecConvert;
        private Button button_SiemensConvert;
        private Button button_LSEConvert;
    }
}
