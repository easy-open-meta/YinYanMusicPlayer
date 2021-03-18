namespace WordMusicWinfrom
{
    partial class UserMainSongSheetNumFrm
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
            this.components = new System.ComponentModel.Container();
            this.panel2 = new System.Windows.Forms.Panel();
            this.pictureBox7 = new System.Windows.Forms.PictureBox();
            this.PSongPheetNumtxt = new System.Windows.Forms.Label();
            this.FLPStorageSong = new System.Windows.Forms.FlowLayoutPanel();
            this.tmrUserDeleteYN = new System.Windows.Forms.Timer(this.components);
            this.tmrUserDelete = new System.Windows.Forms.Timer(this.components);
            this.panel2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox7)).BeginInit();
            this.SuspendLayout();
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.pictureBox7);
            this.panel2.Controls.Add(this.PSongPheetNumtxt);
            this.panel2.Location = new System.Drawing.Point(0, 0);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(831, 24);
            this.panel2.TabIndex = 2;
            // 
            // pictureBox7
            // 
            this.pictureBox7.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.pictureBox7.Location = new System.Drawing.Point(0, 21);
            this.pictureBox7.Name = "pictureBox7";
            this.pictureBox7.Size = new System.Drawing.Size(828, 1);
            this.pictureBox7.TabIndex = 6;
            this.pictureBox7.TabStop = false;
            // 
            // PSongPheetNumtxt
            // 
            this.PSongPheetNumtxt.AutoSize = true;
            this.PSongPheetNumtxt.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.PSongPheetNumtxt.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(102)))), ((int)(((byte)(102)))), ((int)(((byte)(102)))));
            this.PSongPheetNumtxt.Location = new System.Drawing.Point(26, -9);
            this.PSongPheetNumtxt.Name = "PSongPheetNumtxt";
            this.PSongPheetNumtxt.Padding = new System.Windows.Forms.Padding(0, 5, 0, 0);
            this.PSongPheetNumtxt.Size = new System.Drawing.Size(73, 25);
            this.PSongPheetNumtxt.TabIndex = 8;
            this.PSongPheetNumtxt.Text = "歌单（0）";
            // 
            // FLPStorageSong
            // 
            this.FLPStorageSong.AutoScroll = true;
            this.FLPStorageSong.Location = new System.Drawing.Point(0, 25);
            this.FLPStorageSong.Name = "FLPStorageSong";
            this.FLPStorageSong.Padding = new System.Windows.Forms.Padding(15, 0, 15, 0);
            this.FLPStorageSong.Size = new System.Drawing.Size(841, 333);
            this.FLPStorageSong.TabIndex = 3;
            // 
            // tmrUserDeleteYN
            // 
            this.tmrUserDeleteYN.Tick += new System.EventHandler(this.tmrUserDeleteYN_Tick);
            // 
            // tmrUserDelete
            // 
            this.tmrUserDelete.Enabled = true;
            this.tmrUserDelete.Tick += new System.EventHandler(this.tmrUserDelete_Tick);
            // 
            // UserMainSongSheetNumFrm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(250)))), ((int)(((byte)(250)))));
            this.ClientSize = new System.Drawing.Size(828, 356);
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.FLPStorageSong);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "UserMainSongSheetNumFrm";
            this.Text = "UserMainSongSheetNumFrm";
            this.Load += new System.EventHandler(this.UserMainSongSheetNumFrm_Load);
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox7)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.PictureBox pictureBox7;
        private System.Windows.Forms.Label PSongPheetNumtxt;
        private System.Windows.Forms.FlowLayoutPanel FLPStorageSong;
        private System.Windows.Forms.Timer tmrUserDeleteYN;
        private System.Windows.Forms.Timer tmrUserDelete;
    }
}