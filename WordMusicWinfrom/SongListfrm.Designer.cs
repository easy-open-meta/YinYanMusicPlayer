namespace WordMusicWinfrom
{
    partial class SongListfrm
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
            this.SongLN = new System.Windows.Forms.Label();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.SongLP = new System.Windows.Forms.PictureBox();
            this.RightClickMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.tmCheckSongList = new System.Windows.Forms.ToolStripMenuItem();
            this.tmCollectingSongs = new System.Windows.Forms.ToolStripMenuItem();
            this.Line1 = new System.Windows.Forms.ToolStripSeparator();
            this.tmEditSongListInformation = new System.Windows.Forms.ToolStripMenuItem();
            this.DeleteSongList = new System.Windows.Forms.ToolStripMenuItem();
            this.HotInfotxt = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.SongLP)).BeginInit();
            this.RightClickMenu.SuspendLayout();
            this.SuspendLayout();
            // 
            // SongLN
            // 
            this.SongLN.AutoSize = true;
            this.SongLN.Location = new System.Drawing.Point(5, 151);
            this.SongLN.Name = "SongLN";
            this.SongLN.Size = new System.Drawing.Size(101, 12);
            this.SongLN.TabIndex = 5;
            this.SongLN.Text = "这里是歌单的名字";
            // 
            // pictureBox1
            // 
            this.pictureBox1.Image = global::WordMusicWinfrom.Properties.Resources.musicimg;
            this.pictureBox1.Location = new System.Drawing.Point(7, 169);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(15, 15);
            this.pictureBox1.TabIndex = 6;
            this.pictureBox1.TabStop = false;
            // 
            // SongLP
            // 
            this.SongLP.BackColor = System.Drawing.Color.Transparent;
            this.SongLP.ContextMenuStrip = this.RightClickMenu;
            this.SongLP.Location = new System.Drawing.Point(5, 5);
            this.SongLP.Name = "SongLP";
            this.SongLP.Size = new System.Drawing.Size(140, 140);
            this.SongLP.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            this.SongLP.TabIndex = 4;
            this.SongLP.TabStop = false;
            this.SongLP.Click += new System.EventHandler(this.SongLP_Click);
            this.SongLP.DoubleClick += new System.EventHandler(this.SongLP_DoubleClick);
            // 
            // RightClickMenu
            // 
            this.RightClickMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tmCheckSongList,
            this.tmCollectingSongs,
            this.Line1,
            this.tmEditSongListInformation,
            this.DeleteSongList});
            this.RightClickMenu.Name = "RightClickMenu";
            this.RightClickMenu.Size = new System.Drawing.Size(149, 98);
            this.RightClickMenu.Opening += new System.ComponentModel.CancelEventHandler(this.RightClickMenu_Opening);
            // 
            // tmCheckSongList
            // 
            this.tmCheckSongList.Name = "tmCheckSongList";
            this.tmCheckSongList.Size = new System.Drawing.Size(148, 22);
            this.tmCheckSongList.Text = "查看歌单";
            this.tmCheckSongList.Click += new System.EventHandler(this.tmCheckSongList_Click);
            // 
            // tmCollectingSongs
            // 
            this.tmCollectingSongs.Name = "tmCollectingSongs";
            this.tmCollectingSongs.Size = new System.Drawing.Size(148, 22);
            this.tmCollectingSongs.Text = "收藏歌单";
            this.tmCollectingSongs.Click += new System.EventHandler(this.tmCollectingSongs_Click);
            // 
            // Line1
            // 
            this.Line1.Name = "Line1";
            this.Line1.Size = new System.Drawing.Size(145, 6);
            // 
            // tmEditSongListInformation
            // 
            this.tmEditSongListInformation.Name = "tmEditSongListInformation";
            this.tmEditSongListInformation.Size = new System.Drawing.Size(148, 22);
            this.tmEditSongListInformation.Text = "编辑歌单信息";
            this.tmEditSongListInformation.Click += new System.EventHandler(this.tmEditSongListInformation_Click);
            // 
            // DeleteSongList
            // 
            this.DeleteSongList.Name = "DeleteSongList";
            this.DeleteSongList.Size = new System.Drawing.Size(148, 22);
            this.DeleteSongList.Text = "删除歌单";
            this.DeleteSongList.Click += new System.EventHandler(this.DeleteSongList_Click);
            // 
            // HotInfotxt
            // 
            this.HotInfotxt.AutoSize = true;
            this.HotInfotxt.Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.HotInfotxt.Location = new System.Drawing.Point(21, 169);
            this.HotInfotxt.Name = "HotInfotxt";
            this.HotInfotxt.Size = new System.Drawing.Size(43, 17);
            this.HotInfotxt.TabIndex = 7;
            this.HotInfotxt.Text = "label1";
            // 
            // SongListfrm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.pictureBox1);
            this.Controls.Add(this.HotInfotxt);
            this.Controls.Add(this.SongLN);
            this.Controls.Add(this.SongLP);
            this.ForeColor = System.Drawing.SystemColors.ControlText;
            this.Name = "SongListfrm";
            this.Size = new System.Drawing.Size(150, 200);
            this.Load += new System.EventHandler(this.SongListfrm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.SongLP)).EndInit();
            this.RightClickMenu.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label SongLN;
        private System.Windows.Forms.PictureBox SongLP;
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.Label HotInfotxt;
        private System.Windows.Forms.ContextMenuStrip RightClickMenu;
        private System.Windows.Forms.ToolStripMenuItem tmCheckSongList;
        private System.Windows.Forms.ToolStripMenuItem tmCollectingSongs;
        private System.Windows.Forms.ToolStripMenuItem tmEditSongListInformation;
        private System.Windows.Forms.ToolStripMenuItem DeleteSongList;
        private System.Windows.Forms.ToolStripSeparator Line1;
    }
}