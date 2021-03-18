namespace WordMusicWinfrom
{
    partial class SongMusicAlbumFrm
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            this.pictureBox2 = new System.Windows.Forms.PictureBox();
            this.MusicAlbumpic = new System.Windows.Forms.PictureBox();
            this.DgvSongMusicAlbum = new System.Windows.Forms.DataGridView();
            this.Column1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column2 = new System.Windows.Forms.DataGridViewImageColumn();
            this.clAlbumid = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clCdname = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clMsinger = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clAname = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clSongtime = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clParlyric = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clMusicaddress = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clMusicid = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.AlbumTimeTxt = new System.Windows.Forms.Label();
            this.AlbumNameTxt = new System.Windows.Forms.Label();
            this.PanAlbum = new System.Windows.Forms.Panel();
            this.panel1 = new System.Windows.Forms.Panel();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox2)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.MusicAlbumpic)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.DgvSongMusicAlbum)).BeginInit();
            this.PanAlbum.SuspendLayout();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // pictureBox2
            // 
            this.pictureBox2.Image = global::WordMusicWinfrom.Properties.Resources.bigCDpic;
            this.pictureBox2.Location = new System.Drawing.Point(176, 14);
            this.pictureBox2.Name = "pictureBox2";
            this.pictureBox2.Size = new System.Drawing.Size(28, 130);
            this.pictureBox2.TabIndex = 1;
            this.pictureBox2.TabStop = false;
            // 
            // MusicAlbumpic
            // 
            this.MusicAlbumpic.Location = new System.Drawing.Point(26, 4);
            this.MusicAlbumpic.Name = "MusicAlbumpic";
            this.MusicAlbumpic.Size = new System.Drawing.Size(150, 150);
            this.MusicAlbumpic.TabIndex = 0;
            this.MusicAlbumpic.TabStop = false;
            this.MusicAlbumpic.Click += new System.EventHandler(this.MusicAlbumpic_Click);
            // 
            // DgvSongMusicAlbum
            // 
            this.DgvSongMusicAlbum.BackgroundColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(250)))), ((int)(((byte)(249)))));
            this.DgvSongMusicAlbum.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.DgvSongMusicAlbum.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.None;
            this.DgvSongMusicAlbum.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.DgvSongMusicAlbum.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.Column1,
            this.Column2,
            this.clAlbumid,
            this.clCdname,
            this.clMsinger,
            this.clAname,
            this.clSongtime,
            this.clParlyric,
            this.clMusicaddress,
            this.clMusicid});
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(51)))), ((int)(((byte)(51)))), ((int)(((byte)(51)))));
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(227)))), ((int)(((byte)(227)))), ((int)(((byte)(229)))));
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(51)))), ((int)(((byte)(51)))), ((int)(((byte)(51)))));
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.DgvSongMusicAlbum.DefaultCellStyle = dataGridViewCellStyle1;
            this.DgvSongMusicAlbum.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.DgvSongMusicAlbum.Location = new System.Drawing.Point(0, -23);
            this.DgvSongMusicAlbum.Name = "DgvSongMusicAlbum";
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle2.Font = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            dataGridViewCellStyle2.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(153)))), ((int)(((byte)(153)))), ((int)(((byte)(153)))));
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.DgvSongMusicAlbum.RowHeadersDefaultCellStyle = dataGridViewCellStyle2;
            this.DgvSongMusicAlbum.RowHeadersVisible = false;
            this.DgvSongMusicAlbum.RowTemplate.DefaultCellStyle.SelectionBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(227)))), ((int)(((byte)(227)))), ((int)(((byte)(229)))));
            this.DgvSongMusicAlbum.RowTemplate.DefaultCellStyle.SelectionForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(51)))), ((int)(((byte)(51)))), ((int)(((byte)(51)))));
            this.DgvSongMusicAlbum.RowTemplate.Height = 23;
            this.DgvSongMusicAlbum.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.DgvSongMusicAlbum.Size = new System.Drawing.Size(542, 110);
            this.DgvSongMusicAlbum.TabIndex = 23;
            this.DgvSongMusicAlbum.RowPostPaint += new System.Windows.Forms.DataGridViewRowPostPaintEventHandler(this.DgvSongMusicAlbum_RowPostPaint);
            this.DgvSongMusicAlbum.DoubleClick += new System.EventHandler(this.DgvSongMusicAlbum_DoubleClick);
            // 
            // Column1
            // 
            this.Column1.HeaderText = " ";
            this.Column1.Name = "Column1";
            this.Column1.Width = 40;
            // 
            // Column2
            // 
            this.Column2.FillWeight = 20F;
            this.Column2.HeaderText = "操作";
            this.Column2.Image = global::WordMusicWinfrom.Properties.Resources.Loveimg;
            this.Column2.Name = "Column2";
            this.Column2.Width = 44;
            // 
            // clAlbumid
            // 
            this.clAlbumid.DataPropertyName = "AId";
            this.clAlbumid.HeaderText = "专辑ID";
            this.clAlbumid.Name = "clAlbumid";
            this.clAlbumid.Visible = false;
            // 
            // clCdname
            // 
            this.clCdname.DataPropertyName = "Cdname";
            this.clCdname.HeaderText = "音乐标题";
            this.clCdname.Name = "clCdname";
            this.clCdname.Width = 376;
            // 
            // clMsinger
            // 
            this.clMsinger.DataPropertyName = "Usinger";
            this.clMsinger.HeaderText = "歌手";
            this.clMsinger.Name = "clMsinger";
            this.clMsinger.Visible = false;
            // 
            // clAname
            // 
            this.clAname.DataPropertyName = "Aname";
            this.clAname.HeaderText = "专辑";
            this.clAname.Name = "clAname";
            this.clAname.Visible = false;
            // 
            // clSongtime
            // 
            this.clSongtime.DataPropertyName = "Songtime";
            this.clSongtime.HeaderText = "时长";
            this.clSongtime.Name = "clSongtime";
            this.clSongtime.Width = 80;
            // 
            // clParlyric
            // 
            this.clParlyric.DataPropertyName = "Parlyric";
            this.clParlyric.HeaderText = "歌词";
            this.clParlyric.Name = "clParlyric";
            this.clParlyric.Visible = false;
            // 
            // clMusicaddress
            // 
            this.clMusicaddress.DataPropertyName = "Pusicaddress";
            this.clMusicaddress.HeaderText = "歌曲";
            this.clMusicaddress.Name = "clMusicaddress";
            this.clMusicaddress.Visible = false;
            this.clMusicaddress.Width = 80;
            // 
            // clMusicid
            // 
            this.clMusicid.DataPropertyName = "Cdid";
            this.clMusicid.HeaderText = "专辑内单曲ID";
            this.clMusicid.Name = "clMusicid";
            this.clMusicid.Visible = false;
            // 
            // AlbumTimeTxt
            // 
            this.AlbumTimeTxt.AutoSize = true;
            this.AlbumTimeTxt.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(51)))), ((int)(((byte)(51)))), ((int)(((byte)(51)))));
            this.AlbumTimeTxt.Location = new System.Drawing.Point(29, 162);
            this.AlbumTimeTxt.Name = "AlbumTimeTxt";
            this.AlbumTimeTxt.Size = new System.Drawing.Size(53, 12);
            this.AlbumTimeTxt.TabIndex = 24;
            this.AlbumTimeTxt.Text = "发布日期";
            // 
            // AlbumNameTxt
            // 
            this.AlbumNameTxt.AutoSize = true;
            this.AlbumNameTxt.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(51)))), ((int)(((byte)(51)))), ((int)(((byte)(51)))));
            this.AlbumNameTxt.Location = new System.Drawing.Point(233, 28);
            this.AlbumNameTxt.Name = "AlbumNameTxt";
            this.AlbumNameTxt.Size = new System.Drawing.Size(53, 12);
            this.AlbumNameTxt.TabIndex = 25;
            this.AlbumNameTxt.Text = "专辑名字";
            // 
            // PanAlbum
            // 
            this.PanAlbum.Controls.Add(this.panel1);
            this.PanAlbum.Dock = System.Windows.Forms.DockStyle.Fill;
            this.PanAlbum.Location = new System.Drawing.Point(0, 0);
            this.PanAlbum.Name = "PanAlbum";
            this.PanAlbum.Size = new System.Drawing.Size(813, 209);
            this.PanAlbum.TabIndex = 26;
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.DgvSongMusicAlbum);
            this.panel1.Location = new System.Drawing.Point(235, 51);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(542, 93);
            this.panel1.TabIndex = 0;
            // 
            // SongMusicAlbumFrm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(250)))), ((int)(((byte)(251)))));
            this.Controls.Add(this.AlbumNameTxt);
            this.Controls.Add(this.AlbumTimeTxt);
            this.Controls.Add(this.pictureBox2);
            this.Controls.Add(this.MusicAlbumpic);
            this.Controls.Add(this.PanAlbum);
            this.Name = "SongMusicAlbumFrm";
            this.Size = new System.Drawing.Size(813, 209);
            this.Load += new System.EventHandler(this.SongMusicAlbumFrm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox2)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.MusicAlbumpic)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.DgvSongMusicAlbum)).EndInit();
            this.PanAlbum.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.PictureBox MusicAlbumpic;
        private System.Windows.Forms.PictureBox pictureBox2;
        private System.Windows.Forms.DataGridView DgvSongMusicAlbum;
        private System.Windows.Forms.Label AlbumTimeTxt;
        private System.Windows.Forms.Label AlbumNameTxt;
        private System.Windows.Forms.Panel PanAlbum;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column1;
        private System.Windows.Forms.DataGridViewImageColumn Column2;
        private System.Windows.Forms.DataGridViewTextBoxColumn clAlbumid;
        private System.Windows.Forms.DataGridViewTextBoxColumn clCdname;
        private System.Windows.Forms.DataGridViewTextBoxColumn clMsinger;
        private System.Windows.Forms.DataGridViewTextBoxColumn clAname;
        private System.Windows.Forms.DataGridViewTextBoxColumn clSongtime;
        private System.Windows.Forms.DataGridViewTextBoxColumn clParlyric;
        private System.Windows.Forms.DataGridViewTextBoxColumn clMusicaddress;
        private System.Windows.Forms.DataGridViewTextBoxColumn clMusicid;
    }
}