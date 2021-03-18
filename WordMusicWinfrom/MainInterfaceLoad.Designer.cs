namespace WordMusicWinfrom
{
    partial class MainInterfaceLoad
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.LoadTime = new System.Windows.Forms.Timer(this.components);
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.MusicLogintxt = new System.Windows.Forms.Label();
            this.Login = new System.Windows.Forms.Timer(this.components);
            this.label1 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.SuspendLayout();
            // 
            // LoadTime
            // 
            this.LoadTime.Enabled = true;
            this.LoadTime.Interval = 10000;
            this.LoadTime.Tick += new System.EventHandler(this.LoadTime_Tick);
            // 
            // pictureBox1
            // 
            this.pictureBox1.Image = global::WordMusicWinfrom.Properties.Resources.Logo;
            this.pictureBox1.Location = new System.Drawing.Point(226, 51);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(350, 350);
            this.pictureBox1.TabIndex = 0;
            this.pictureBox1.TabStop = false;
            // 
            // MusicLogintxt
            // 
            this.MusicLogintxt.AutoSize = true;
            this.MusicLogintxt.Font = new System.Drawing.Font("微软雅黑", 21F);
            this.MusicLogintxt.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(198)))), ((int)(((byte)(47)))), ((int)(((byte)(47)))));
            this.MusicLogintxt.Location = new System.Drawing.Point(685, 365);
            this.MusicLogintxt.Name = "MusicLogintxt";
            this.MusicLogintxt.Size = new System.Drawing.Size(36, 36);
            this.MusicLogintxt.TabIndex = 1;
            this.MusicLogintxt.Text = "...";
            // 
            // Login
            // 
            this.Login.Enabled = true;
            this.Login.Interval = 1000;
            this.Login.Tick += new System.EventHandler(this.Login_Tick);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("微软雅黑", 21F);
            this.label1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(198)))), ((int)(((byte)(47)))), ((int)(((byte)(47)))));
            this.label1.Location = new System.Drawing.Point(534, 365);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(155, 36);
            this.label1.TabIndex = 2;
            this.label1.Text = "音乐加载中";
            // 
            // MainInterfaceLoad
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(197)))), ((int)(((byte)(47)))), ((int)(((byte)(47)))));
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.MusicLogintxt);
            this.Controls.Add(this.pictureBox1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "MainInterfaceLoad";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "音言界面加载中......";
            this.TransparencyKey = System.Drawing.Color.FromArgb(((int)(((byte)(197)))), ((int)(((byte)(47)))), ((int)(((byte)(47)))));
            this.Load += new System.EventHandler(this.MainInterfaceLoad_Load);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Timer LoadTime;
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.Label MusicLogintxt;
        private System.Windows.Forms.Timer Login;
        private System.Windows.Forms.Label label1;
    }
}

