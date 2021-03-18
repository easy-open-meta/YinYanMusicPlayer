namespace WordMusicWinfrom
{
    partial class UserLRFrm
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
            this.UserLRpanel = new System.Windows.Forms.Panel();
            this.MusicClose = new System.Windows.Forms.PictureBox();
            this.CloseRegistrationtxt = new System.Windows.Forms.LinkLabel();
            this.RegisterFrmtxt = new System.Windows.Forms.LinkLabel();
            this.UserLoginNums = new System.Windows.Forms.Timer(this.components);
            this.UserLoginNum = new System.Windows.Forms.Timer(this.components);
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.MusicClose)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.SuspendLayout();
            // 
            // UserLRpanel
            // 
            this.UserLRpanel.Location = new System.Drawing.Point(0, 32);
            this.UserLRpanel.Name = "UserLRpanel";
            this.UserLRpanel.Size = new System.Drawing.Size(287, 325);
            this.UserLRpanel.TabIndex = 0;
            // 
            // MusicClose
            // 
            this.MusicClose.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(192)))), ((int)(((byte)(51)))), ((int)(((byte)(51)))));
            this.MusicClose.Image = global::WordMusicWinfrom.Properties.Resources.MusicCloseimg;
            this.MusicClose.Location = new System.Drawing.Point(265, 8);
            this.MusicClose.Name = "MusicClose";
            this.MusicClose.Size = new System.Drawing.Size(15, 15);
            this.MusicClose.TabIndex = 3;
            this.MusicClose.TabStop = false;
            this.MusicClose.Click += new System.EventHandler(this.MusicClose_Click);
            // 
            // CloseRegistrationtxt
            // 
            this.CloseRegistrationtxt.AutoSize = true;
            this.CloseRegistrationtxt.LinkColor = System.Drawing.Color.FromArgb(((int)(((byte)(51)))), ((int)(((byte)(51)))), ((int)(((byte)(51)))));
            this.CloseRegistrationtxt.Location = new System.Drawing.Point(7, 10);
            this.CloseRegistrationtxt.Name = "CloseRegistrationtxt";
            this.CloseRegistrationtxt.Size = new System.Drawing.Size(53, 12);
            this.CloseRegistrationtxt.TabIndex = 12;
            this.CloseRegistrationtxt.TabStop = true;
            this.CloseRegistrationtxt.Text = "返回登录";
            this.CloseRegistrationtxt.Visible = false;
            this.CloseRegistrationtxt.Click += new System.EventHandler(this.CloseRegistrationtxt_Click);
            // 
            // RegisterFrmtxt
            // 
            this.RegisterFrmtxt.AutoSize = true;
            this.RegisterFrmtxt.LinkColor = System.Drawing.Color.FromArgb(((int)(((byte)(51)))), ((int)(((byte)(51)))), ((int)(((byte)(51)))));
            this.RegisterFrmtxt.Location = new System.Drawing.Point(250, 362);
            this.RegisterFrmtxt.Name = "RegisterFrmtxt";
            this.RegisterFrmtxt.Size = new System.Drawing.Size(29, 12);
            this.RegisterFrmtxt.TabIndex = 13;
            this.RegisterFrmtxt.TabStop = true;
            this.RegisterFrmtxt.Text = "注册";
            this.RegisterFrmtxt.Click += new System.EventHandler(this.register_Click);
            // 
            // UserLoginNums
            // 
            this.UserLoginNums.Tick += new System.EventHandler(this.UserLoginNums_Tick);
            // 
            // UserLoginNum
            // 
            this.UserLoginNum.Enabled = true;
            this.UserLoginNum.Tick += new System.EventHandler(this.UserLoginNum_Tick);
            // 
            // pictureBox1
            // 
            this.pictureBox1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(192)))), ((int)(((byte)(51)))), ((int)(((byte)(51)))));
            this.pictureBox1.Location = new System.Drawing.Point(257, 0);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(30, 30);
            this.pictureBox1.TabIndex = 14;
            this.pictureBox1.TabStop = false;
            this.pictureBox1.Click += new System.EventHandler(this.MusicClose_Click);
            // 
            // UserLRFrm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(250)))), ((int)(((byte)(250)))));
            this.ClientSize = new System.Drawing.Size(287, 382);
            this.Controls.Add(this.RegisterFrmtxt);
            this.Controls.Add(this.CloseRegistrationtxt);
            this.Controls.Add(this.MusicClose);
            this.Controls.Add(this.UserLRpanel);
            this.Controls.Add(this.pictureBox1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "UserLRFrm";
            this.Text = "UserLRFrm";
            this.Load += new System.EventHandler(this.UserLRFrm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.MusicClose)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Panel UserLRpanel;
        private System.Windows.Forms.PictureBox MusicClose;
        private System.Windows.Forms.LinkLabel CloseRegistrationtxt;
        private System.Windows.Forms.LinkLabel RegisterFrmtxt;
        //private System.Windows.Forms.Timer UserLoginNumex;
        private System.Windows.Forms.Timer UserLoginNums;
        private System.Windows.Forms.Timer UserLoginNum;
        private System.Windows.Forms.PictureBox pictureBox1;
    }
}