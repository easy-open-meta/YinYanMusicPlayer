namespace WordMusicWinfrom
{
    partial class SongMusicfrm
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
            this.SingerFS = new System.Windows.Forms.Label();
            this.SingerNA = new System.Windows.Forms.Label();
            this.SingerPC = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.SingerPC)).BeginInit();
            this.SuspendLayout();
            // 
            // SingerFS
            // 
            this.SingerFS.AutoSize = true;
            this.SingerFS.Location = new System.Drawing.Point(4, 177);
            this.SingerFS.Name = "SingerFS";
            this.SingerFS.Size = new System.Drawing.Size(41, 12);
            this.SingerFS.TabIndex = 9;
            this.SingerFS.Text = "粉丝量";
            // 
            // SingerNA
            // 
            this.SingerNA.AutoSize = true;
            this.SingerNA.Location = new System.Drawing.Point(4, 151);
            this.SingerNA.Name = "SingerNA";
            this.SingerNA.Size = new System.Drawing.Size(53, 12);
            this.SingerNA.TabIndex = 8;
            this.SingerNA.Text = "歌手名字";
            // 
            // SingerPC
            // 
            this.SingerPC.Location = new System.Drawing.Point(5, 5);
            this.SingerPC.Name = "SingerPC";
            this.SingerPC.Size = new System.Drawing.Size(140, 140);
            this.SingerPC.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.SingerPC.TabIndex = 7;
            this.SingerPC.TabStop = false;
            this.SingerPC.DoubleClick += new System.EventHandler(this.SingerPC_DoubleClick);
            // 
            // SongMusicfrm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.SingerFS);
            this.Controls.Add(this.SingerNA);
            this.Controls.Add(this.SingerPC);
            this.Name = "SongMusicfrm";
            this.Size = new System.Drawing.Size(150, 200);
            this.Load += new System.EventHandler(this.SongMusicfrm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.SingerPC)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label SingerFS;
        private System.Windows.Forms.Label SingerNA;
        private System.Windows.Forms.PictureBox SingerPC;
    }
}