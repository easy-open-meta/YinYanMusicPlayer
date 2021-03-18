namespace WordMusicWinfrom
{
    partial class CollectToSongListS
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
            this.dgvSongTable = new System.Windows.Forms.DataGridView();
            this.clSongIdTables = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.clSongNameTables = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.panel1 = new System.Windows.Forms.Panel();
            this.MusicClose = new System.Windows.Forms.PictureBox();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.label1 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.dgvSongTable)).BeginInit();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.MusicClose)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.SuspendLayout();
            // 
            // dgvSongTable
            // 
            this.dgvSongTable.BackgroundColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(250)))), ((int)(((byte)(249)))));
            this.dgvSongTable.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.None;
            this.dgvSongTable.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvSongTable.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.clSongIdTables,
            this.clSongNameTables});
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(51)))), ((int)(((byte)(51)))), ((int)(((byte)(51)))));
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(227)))), ((int)(((byte)(227)))), ((int)(((byte)(229)))));
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(51)))), ((int)(((byte)(51)))), ((int)(((byte)(51)))));
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.dgvSongTable.DefaultCellStyle = dataGridViewCellStyle1;
            this.dgvSongTable.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.dgvSongTable.Location = new System.Drawing.Point(-1, -28);
            this.dgvSongTable.Name = "dgvSongTable";
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle2.Font = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            dataGridViewCellStyle2.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(153)))), ((int)(((byte)(153)))), ((int)(((byte)(153)))));
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dgvSongTable.RowHeadersDefaultCellStyle = dataGridViewCellStyle2;
            this.dgvSongTable.RowHeadersVisible = false;
            this.dgvSongTable.RowTemplate.DefaultCellStyle.SelectionBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(227)))), ((int)(((byte)(227)))), ((int)(((byte)(229)))));
            this.dgvSongTable.RowTemplate.DefaultCellStyle.SelectionForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(51)))), ((int)(((byte)(51)))), ((int)(((byte)(51)))));
            this.dgvSongTable.RowTemplate.Height = 23;
            this.dgvSongTable.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvSongTable.Size = new System.Drawing.Size(223, 353);
            this.dgvSongTable.TabIndex = 26;
            this.dgvSongTable.DoubleClick += new System.EventHandler(this.dgvSongTable_DoubleClick);
            // 
            // clSongIdTables
            // 
            this.clSongIdTables.DataPropertyName = "SongId";
            this.clSongIdTables.HeaderText = "歌单ID";
            this.clSongIdTables.Name = "clSongIdTables";
            this.clSongIdTables.Visible = false;
            // 
            // clSongNameTables
            // 
            this.clSongNameTables.DataPropertyName = "SongName";
            this.clSongNameTables.HeaderText = "歌单名字";
            this.clSongNameTables.Name = "clSongNameTables";
            this.clSongNameTables.ReadOnly = true;
            this.clSongNameTables.Width = 200;
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.dgvSongTable);
            this.panel1.Location = new System.Drawing.Point(-1, 57);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(208, 307);
            this.panel1.TabIndex = 27;
            // 
            // MusicClose
            // 
            this.MusicClose.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(192)))), ((int)(((byte)(51)))), ((int)(((byte)(51)))));
            this.MusicClose.Image = global::WordMusicWinfrom.Properties.Resources.MusicCloseimg;
            this.MusicClose.Location = new System.Drawing.Point(183, 5);
            this.MusicClose.Name = "MusicClose";
            this.MusicClose.Size = new System.Drawing.Size(15, 15);
            this.MusicClose.TabIndex = 28;
            this.MusicClose.TabStop = false;
            this.MusicClose.Click += new System.EventHandler(this.pictureBox1_Click);
            // 
            // pictureBox1
            // 
            this.pictureBox1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(192)))), ((int)(((byte)(51)))), ((int)(((byte)(51)))));
            this.pictureBox1.Location = new System.Drawing.Point(177, 0);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(25, 25);
            this.pictureBox1.TabIndex = 29;
            this.pictureBox1.TabStop = false;
            this.pictureBox1.Click += new System.EventHandler(this.pictureBox1_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("微软雅黑", 14F);
            this.label1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(51)))), ((int)(((byte)(51)))), ((int)(((byte)(51)))));
            this.label1.Location = new System.Drawing.Point(12, 13);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(107, 25);
            this.label1.TabIndex = 30;
            this.label1.Text = "收藏到歌单";
            // 
            // CollectToSongListS
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.WhiteSmoke;
            this.ClientSize = new System.Drawing.Size(202, 360);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.MusicClose);
            this.Controls.Add(this.pictureBox1);
            this.Controls.Add(this.panel1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "CollectToSongListS";
            this.Text = "CollectToSongListS";
            this.Load += new System.EventHandler(this.CollectToSongListS_Load);
            ((System.ComponentModel.ISupportInitialize)(this.dgvSongTable)).EndInit();
            this.panel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.MusicClose)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.DataGridView dgvSongTable;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.PictureBox MusicClose;
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.DataGridViewTextBoxColumn clSongIdTables;
        private System.Windows.Forms.DataGridViewTextBoxColumn clSongNameTables;
    }
}