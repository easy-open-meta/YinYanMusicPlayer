namespace WordMusicWinfrom
{
    partial class CDFrominfo
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
            this.panel2 = new System.Windows.Forms.Panel();
            this.panel1 = new System.Windows.Forms.Panel();
            this.DgvListOfSongsList = new System.Windows.Forms.DataGridView();
            this.label1 = new System.Windows.Forms.Label();
            this.pictureBox10 = new System.Windows.Forms.PictureBox();
            this.dataGridViewImageColumn1 = new System.Windows.Forms.DataGridViewImageColumn();
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
            this.panel2.SuspendLayout();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.DgvListOfSongsList)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox10)).BeginInit();
            this.SuspendLayout();
            // 
            // panel2
            // 
            this.panel2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(250)))), ((int)(((byte)(250)))));
            this.panel2.Controls.Add(this.panel1);
            this.panel2.Controls.Add(this.label1);
            this.panel2.Controls.Add(this.pictureBox10);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel2.Location = new System.Drawing.Point(0, 0);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(813, 362);
            this.panel2.TabIndex = 1;
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.DgvListOfSongsList);
            this.panel1.Location = new System.Drawing.Point(232, 46);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(542, 284);
            this.panel1.TabIndex = 24;
            // 
            // DgvListOfSongsList
            // 
            this.DgvListOfSongsList.BackgroundColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(250)))), ((int)(((byte)(249)))));
            this.DgvListOfSongsList.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.None;
            this.DgvListOfSongsList.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
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
            this.DgvListOfSongsList.DefaultCellStyle = dataGridViewCellStyle1;
            this.DgvListOfSongsList.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.DgvListOfSongsList.Location = new System.Drawing.Point(0, -24);
            this.DgvListOfSongsList.Name = "DgvListOfSongsList";
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle2.Font = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            dataGridViewCellStyle2.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(153)))), ((int)(((byte)(153)))), ((int)(((byte)(153)))));
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.DgvListOfSongsList.RowHeadersDefaultCellStyle = dataGridViewCellStyle2;
            this.DgvListOfSongsList.RowHeadersVisible = false;
            this.DgvListOfSongsList.RowTemplate.DefaultCellStyle.SelectionBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(227)))), ((int)(((byte)(227)))), ((int)(((byte)(229)))));
            this.DgvListOfSongsList.RowTemplate.DefaultCellStyle.SelectionForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(51)))), ((int)(((byte)(51)))), ((int)(((byte)(51)))));
            this.DgvListOfSongsList.RowTemplate.Height = 23;
            this.DgvListOfSongsList.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.DgvListOfSongsList.Size = new System.Drawing.Size(542, 307);
            this.DgvListOfSongsList.TabIndex = 22;
            this.DgvListOfSongsList.RowPostPaint += new System.Windows.Forms.DataGridViewRowPostPaintEventHandler(this.DgvListOfSongsList_RowPostPaint);
            this.DgvListOfSongsList.Click += new System.EventHandler(this.DgvListOfSongsList_Click);
            this.DgvListOfSongsList.DoubleClick += new System.EventHandler(this.DgvListOfSongsList_DoubleClick);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(51)))), ((int)(((byte)(51)))), ((int)(((byte)(51)))));
            this.label1.Location = new System.Drawing.Point(232, 17);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(53, 12);
            this.label1.TabIndex = 23;
            this.label1.Text = "热门10首";
            // 
            // pictureBox10
            // 
            this.pictureBox10.Image = global::WordMusicWinfrom.Properties.Resources.top10;
            this.pictureBox10.Location = new System.Drawing.Point(30, 17);
            this.pictureBox10.Name = "pictureBox10";
            this.pictureBox10.Size = new System.Drawing.Size(150, 150);
            this.pictureBox10.TabIndex = 0;
            this.pictureBox10.TabStop = false;
            // 
            // dataGridViewImageColumn1
            // 
            this.dataGridViewImageColumn1.FillWeight = 20F;
            this.dataGridViewImageColumn1.HeaderText = "操作";
            this.dataGridViewImageColumn1.Image = global::WordMusicWinfrom.Properties.Resources.Loveimg;
            this.dataGridViewImageColumn1.Name = "dataGridViewImageColumn1";
            this.dataGridViewImageColumn1.Width = 44;
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
            this.clAlbumid.DataPropertyName = "Aid";
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
            // CDFrominfo
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(813, 362);
            this.Controls.Add(this.panel2);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "CDFrominfo";
            this.Text = "CDFrominfo";
            this.Load += new System.EventHandler(this.CDFrominfo_Load);
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            this.panel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.DgvListOfSongsList)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox10)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.PictureBox pictureBox10;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.DataGridView DgvListOfSongsList;
        private System.Windows.Forms.DataGridViewImageColumn dataGridViewImageColumn1;
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