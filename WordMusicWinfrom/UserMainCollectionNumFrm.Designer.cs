namespace WordMusicWinfrom
{
    partial class UserMainCollectionNumFrm
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
            this.panel3 = new System.Windows.Forms.Panel();
            this.pictureBox8 = new System.Windows.Forms.PictureBox();
            this.PCollectionNumtxt = new System.Windows.Forms.Label();
            this.FLPStorageShoucang = new System.Windows.Forms.FlowLayoutPanel();
            this.panel3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox8)).BeginInit();
            this.SuspendLayout();
            // 
            // panel3
            // 
            this.panel3.Controls.Add(this.pictureBox8);
            this.panel3.Controls.Add(this.PCollectionNumtxt);
            this.panel3.Location = new System.Drawing.Point(0, 0);
            this.panel3.Name = "panel3";
            this.panel3.Size = new System.Drawing.Size(832, 24);
            this.panel3.TabIndex = 4;
            // 
            // pictureBox8
            // 
            this.pictureBox8.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.pictureBox8.Location = new System.Drawing.Point(0, 21);
            this.pictureBox8.Name = "pictureBox8";
            this.pictureBox8.Size = new System.Drawing.Size(814, 1);
            this.pictureBox8.TabIndex = 6;
            this.pictureBox8.TabStop = false;
            // 
            // PCollectionNumtxt
            // 
            this.PCollectionNumtxt.AutoSize = true;
            this.PCollectionNumtxt.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.PCollectionNumtxt.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(102)))), ((int)(((byte)(102)))), ((int)(((byte)(102)))));
            this.PCollectionNumtxt.Location = new System.Drawing.Point(26, -9);
            this.PCollectionNumtxt.Name = "PCollectionNumtxt";
            this.PCollectionNumtxt.Padding = new System.Windows.Forms.Padding(0, 5, 0, 0);
            this.PCollectionNumtxt.Size = new System.Drawing.Size(73, 25);
            this.PCollectionNumtxt.TabIndex = 8;
            this.PCollectionNumtxt.Text = "收藏（0）";
            // 
            // FLPStorageShoucang
            // 
            this.FLPStorageShoucang.AutoScroll = true;
            this.FLPStorageShoucang.Location = new System.Drawing.Point(0, 25);
            this.FLPStorageShoucang.Name = "FLPStorageShoucang";
            this.FLPStorageShoucang.Padding = new System.Windows.Forms.Padding(15, 0, 15, 0);
            this.FLPStorageShoucang.Size = new System.Drawing.Size(846, 331);
            this.FLPStorageShoucang.TabIndex = 5;
            // 
            // UserMainCollectionNumFrm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(250)))), ((int)(((byte)(250)))));
            this.ClientSize = new System.Drawing.Size(828, 356);
            this.Controls.Add(this.panel3);
            this.Controls.Add(this.FLPStorageShoucang);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "UserMainCollectionNumFrm";
            this.Text = "用户收藏的歌单";
            this.Load += new System.EventHandler(this.UserMainCollectionNumFrm_Load);
            this.panel3.ResumeLayout(false);
            this.panel3.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox8)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panel3;
        private System.Windows.Forms.PictureBox pictureBox8;
        private System.Windows.Forms.Label PCollectionNumtxt;
        private System.Windows.Forms.FlowLayoutPanel FLPStorageShoucang;
    }
}