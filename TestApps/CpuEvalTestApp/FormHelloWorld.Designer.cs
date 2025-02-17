namespace CpuEvalTestApp
{
    partial class FormHelloWorld
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
            btnTurnOn = new Button();
            btnTurnOff = new Button();
            SuspendLayout();
            // 
            // btnTurnOn
            // 
            btnTurnOn.Location = new Point(101, 60);
            btnTurnOn.Name = "btnTurnOn";
            btnTurnOn.Size = new Size(112, 34);
            btnTurnOn.TabIndex = 0;
            btnTurnOn.Text = "ON";
            btnTurnOn.UseVisualStyleBackColor = true;
            // 
            // btnTurnOff
            // 
            btnTurnOff.Location = new Point(101, 122);
            btnTurnOff.Name = "btnTurnOff";
            btnTurnOff.Size = new Size(112, 34);
            btnTurnOff.TabIndex = 1;
            btnTurnOff.Text = "OFF";
            btnTurnOff.UseVisualStyleBackColor = true;
            // 
            // FormHelloWorld
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(btnTurnOff);
            Controls.Add(btnTurnOn);
            Name = "FormHelloWorld";
            Text = "Form1";
            Load += FormHelloWorld_Load;
            ResumeLayout(false);
        }

        #endregion

        private Button btnTurnOn;
        private Button btnTurnOff;
    }
}
