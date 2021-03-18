namespace WordMusicWinfrom
{
    partial class UpNewSongListFrm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(UpNewSongListFrm));
            this.label1 = new System.Windows.Forms.Label();
            this.SongSheetNametxt = new System.Windows.Forms.TextBox();
            this.DeleteNopic = new System.Windows.Forms.PictureBox();
            this.DeleteYespic = new System.Windows.Forms.PictureBox();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.pictureBox2 = new System.Windows.Forms.PictureBox();
            this.checkBox1 = new System.Windows.Forms.CheckBox();
            ((System.ComponentModel.ISupportInitialize)(this.DeleteNopic)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.DeleteYespic)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox2)).BeginInit();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("微软雅黑", 12F);
            this.label1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(51)))), ((int)(((byte)(51)))), ((int)(((byte)(51)))));
            this.label1.Location = new System.Drawing.Point(10, 12);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(106, 21);
            this.label1.TabIndex = 12;
            this.label1.Text = "编辑歌单信息";
            // 
            // SongSheetNametxt
            // 
            this.SongSheetNametxt.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.SongSheetNametxt.Location = new System.Drawing.Point(28, 73);
            this.SongSheetNametxt.Name = "SongSheetNametxt";
            this.SongSheetNametxt.Size = new System.Drawing.Size(284, 23);
            this.SongSheetNametxt.TabIndex = 16;
            this.SongSheetNametxt.TextChanged += new System.EventHandler(this.SongSheetNametxt_TextChanged);
            this.SongSheetNametxt.Enter += new System.EventHandler(this.SongSheetNametxt_Enter);
            this.SongSheetNametxt.Leave += new System.EventHandler(this.SongSheetNametxt_Leave);
            // 
            // DeleteNopic
            // 
            this.DeleteNopic.Image = ((System.Drawing.Image)(resources.GetObject("DeleteNopic.Image")));
            this.DeleteNopic.Location = new System.Drawing.Point(181, 200);
            this.DeleteNopic.Name = "DeleteNopic";
            this.DeleteNopic.Size = new System.Drawing.Size(80, 29);
            this.DeleteNopic.TabIndex = 21;
            this.DeleteNopic.TabStop = false;
            this.DeleteNopic.Click += new System.EventHandler(this.DeleteNopic_Click);
            // 
            // DeleteYespic
            // 
            this.DeleteYespic.Image = ((System.Drawing.Image)(resources.GetObject("DeleteYespic.Image")));
            this.DeleteYespic.Location = new System.Drawing.Point(75, 200);
            this.DeleteYespic.Name = "DeleteYespic";
            this.DeleteYespic.Size = new System.Drawing.Size(80, 29);
            this.DeleteYespic.TabIndex = 20;
            this.DeleteYespic.TabStop = false;
            this.DeleteYespic.Click += new System.EventHandler(this.DeleteYespic_Click);
            // 
            // pictureBox1
            // 
            this.pictureBox1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(225)))), ((int)(((byte)(225)))), ((int)(((byte)(226)))));
            this.pictureBox1.Location = new System.Drawing.Point(-14, 43);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(460, 1);
            this.pictureBox1.TabIndex = 13;
            this.pictureBox1.TabStop = false;
            // 
            // pictureBox2
            // 
            this.pictureBox2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(225)))), ((int)(((byte)(225)))), ((int)(((byte)(226)))));
            this.pictureBox2.Location = new System.Drawing.Point(-61, 186);
            this.pictureBox2.Name = "pictureBox2";
            this.pictureBox2.Size = new System.Drawing.Size(460, 1);
            this.pictureBox2.TabIndex = 22;
            this.pictureBox2.TabStop = false;
            // 
            // checkBox1
            // 
            this.checkBox1.AutoSize = true;
            this.checkBox1.Font = new System.Drawing.Font("微软雅黑", 11F);
            this.checkBox1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(51)))), ((int)(((byte)(51)))), ((int)(((byte)(51)))));
            this.checkBox1.Location = new System.Drawing.Point(28, 103);
            this.checkBox1.Name = "checkBox1";
            this.checkBox1.Size = new System.Drawing.Size(118, 24);
            this.checkBox1.TabIndex = 17;
            this.checkBox1.Text = "设为私密歌单";
            this.checkBox1.UseVisualStyleBackColor = false;
            this.checkBox1.CheckedChanged += new System.EventHandler(this.checkBox1_CheckedChanged);
            // 
            // UpNewSongListFrm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(250)))), ((int)(((byte)(250)))));
            this.ClientSize = new System.Drawing.Size(339, 239);
            this.Controls.Add(this.pictureBox2);
            this.Controls.Add(this.DeleteNopic);
            this.Controls.Add(this.DeleteYespic);
            this.Controls.Add(this.checkBox1);
            this.Controls.Add(this.SongSheetNametxt);
            this.Controls.Add(this.pictureBox1);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "UpNewSongListFrm";
            this.Text = "UpNewSongListFrm";
            this.Load += new System.EventHandler(this.UpNewSongListFrm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.DeleteNopic)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.DeleteYespic)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox2)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox SongSheetNametxt;
        private System.Windows.Forms.PictureBox DeleteNopic;
        private System.Windows.Forms.PictureBox DeleteYespic;
        private System.Windows.Forms.PictureBox pictureBox2;
        private System.Windows.Forms.CheckBox checkBox1;
    }
}