namespace WordMusicWinfrom
{
    partial class DeleteYesNofrm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DeleteYesNofrm));
            this.DeleteYespic = new System.Windows.Forms.PictureBox();
            this.DeleteNopic = new System.Windows.Forms.PictureBox();
            this.label1 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.DeleteYespic)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.DeleteNopic)).BeginInit();
            this.SuspendLayout();
            // 
            // DeleteYespic
            // 
            this.DeleteYespic.Image = ((System.Drawing.Image)(resources.GetObject("DeleteYespic.Image")));
            this.DeleteYespic.Location = new System.Drawing.Point(40, 120);
            this.DeleteYespic.Name = "DeleteYespic";
            this.DeleteYespic.Size = new System.Drawing.Size(80, 29);
            this.DeleteYespic.TabIndex = 0;
            this.DeleteYespic.TabStop = false;
            this.DeleteYespic.Click += new System.EventHandler(this.DeleteYespic_Click);
            // 
            // DeleteNopic
            // 
            this.DeleteNopic.Image = ((System.Drawing.Image)(resources.GetObject("DeleteNopic.Image")));
            this.DeleteNopic.Location = new System.Drawing.Point(143, 120);
            this.DeleteNopic.Name = "DeleteNopic";
            this.DeleteNopic.Size = new System.Drawing.Size(80, 29);
            this.DeleteNopic.TabIndex = 1;
            this.DeleteNopic.TabStop = false;
            this.DeleteNopic.Click += new System.EventHandler(this.DeleteNopic_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("微软雅黑", 12F);
            this.label1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(51)))), ((int)(((byte)(51)))), ((int)(((byte)(51)))));
            this.label1.Location = new System.Drawing.Point(69, 52);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(130, 21);
            this.label1.TabIndex = 2;
            this.label1.Text = "确定删除该歌单?";
            // 
            // DeleteYesNofrm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(250)))), ((int)(((byte)(250)))));
            this.ClientSize = new System.Drawing.Size(270, 170);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.DeleteNopic);
            this.Controls.Add(this.DeleteYespic);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "DeleteYesNofrm";
            this.Text = "DeleteYesNofrm";
            this.Load += new System.EventHandler(this.DeleteYesNofrm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.DeleteYespic)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.DeleteNopic)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.PictureBox DeleteYespic;
        private System.Windows.Forms.PictureBox DeleteNopic;
        private System.Windows.Forms.Label label1;
    }
}