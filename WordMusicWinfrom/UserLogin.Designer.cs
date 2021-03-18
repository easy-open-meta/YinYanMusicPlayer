namespace WordMusicWinfrom
{
    partial class UserLogin
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
            this.Passwordtbt = new System.Windows.Forms.TextBox();
            this.AccountNumtxt = new System.Windows.Forms.Label();
            this.pictureBox4 = new System.Windows.Forms.PictureBox();
            this.pictureBox3 = new System.Windows.Forms.PictureBox();
            this.Logonpic = new System.Windows.Forms.PictureBox();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.pictureBox2 = new System.Windows.Forms.PictureBox();
            this.AccountNumtbt = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox4)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox3)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.Logonpic)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox2)).BeginInit();
            this.SuspendLayout();
            // 
            // Passwordtbt
            // 
            this.Passwordtbt.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(250)))), ((int)(((byte)(250)))));
            this.Passwordtbt.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.Passwordtbt.Font = new System.Drawing.Font("微软雅黑", 11F);
            this.Passwordtbt.ForeColor = System.Drawing.SystemColors.WindowFrame;
            this.Passwordtbt.Location = new System.Drawing.Point(61, 177);
            this.Passwordtbt.Multiline = true;
            this.Passwordtbt.Name = "Passwordtbt";
            this.Passwordtbt.Size = new System.Drawing.Size(169, 18);
            this.Passwordtbt.TabIndex = 2;
            this.Passwordtbt.Text = "密码";
            this.Passwordtbt.Enter += new System.EventHandler(this.Passwordtbt_Enter);
            this.Passwordtbt.Leave += new System.EventHandler(this.Passwordtbt_Leave);
            // 
            // AccountNumtxt
            // 
            this.AccountNumtxt.AutoSize = true;
            this.AccountNumtxt.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(51)))), ((int)(((byte)(51)))), ((int)(((byte)(51)))));
            this.AccountNumtxt.Location = new System.Drawing.Point(63, 144);
            this.AccountNumtxt.Name = "AccountNumtxt";
            this.AccountNumtxt.Size = new System.Drawing.Size(41, 12);
            this.AccountNumtxt.TabIndex = 9;
            this.AccountNumtxt.Text = "label1";
            this.AccountNumtxt.Visible = false;
            // 
            // pictureBox4
            // 
            this.pictureBox4.Image = global::WordMusicWinfrom.Properties.Resources.UserPwdpic;
            this.pictureBox4.Location = new System.Drawing.Point(40, 179);
            this.pictureBox4.Name = "pictureBox4";
            this.pictureBox4.Size = new System.Drawing.Size(16, 16);
            this.pictureBox4.TabIndex = 7;
            this.pictureBox4.TabStop = false;
            // 
            // pictureBox3
            // 
            this.pictureBox3.Image = global::WordMusicWinfrom.Properties.Resources.UserLoginpiccl;
            this.pictureBox3.Location = new System.Drawing.Point(40, 116);
            this.pictureBox3.Name = "pictureBox3";
            this.pictureBox3.Size = new System.Drawing.Size(16, 16);
            this.pictureBox3.TabIndex = 6;
            this.pictureBox3.TabStop = false;
            // 
            // Logonpic
            // 
            this.Logonpic.Image = global::WordMusicWinfrom.Properties.Resources.LoginPic;
            this.Logonpic.Location = new System.Drawing.Point(36, 268);
            this.Logonpic.Name = "Logonpic";
            this.Logonpic.Size = new System.Drawing.Size(200, 36);
            this.Logonpic.TabIndex = 5;
            this.Logonpic.TabStop = false;
            this.Logonpic.Click += new System.EventHandler(this.Logonpic_Click);
            // 
            // pictureBox1
            // 
            this.pictureBox1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(198)))), ((int)(((byte)(47)))), ((int)(((byte)(47)))));
            this.pictureBox1.Location = new System.Drawing.Point(40, 136);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(194, 1);
            this.pictureBox1.TabIndex = 10;
            this.pictureBox1.TabStop = false;
            // 
            // pictureBox2
            // 
            this.pictureBox2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(198)))), ((int)(((byte)(47)))), ((int)(((byte)(47)))));
            this.pictureBox2.Location = new System.Drawing.Point(41, 199);
            this.pictureBox2.Name = "pictureBox2";
            this.pictureBox2.Size = new System.Drawing.Size(194, 1);
            this.pictureBox2.TabIndex = 11;
            this.pictureBox2.TabStop = false;
            // 
            // AccountNumtbt
            // 
            this.AccountNumtbt.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(250)))), ((int)(((byte)(250)))));
            this.AccountNumtbt.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.AccountNumtbt.Font = new System.Drawing.Font("微软雅黑", 11F);
            this.AccountNumtbt.ForeColor = System.Drawing.SystemColors.WindowFrame;
            this.AccountNumtbt.Location = new System.Drawing.Point(61, 114);
            this.AccountNumtbt.Multiline = true;
            this.AccountNumtbt.Name = "AccountNumtbt";
            this.AccountNumtbt.Size = new System.Drawing.Size(169, 18);
            this.AccountNumtbt.TabIndex = 1;
            this.AccountNumtbt.Text = "请输入账号";
            this.AccountNumtbt.Enter += new System.EventHandler(this.AccountNumtbt_Enter);
            this.AccountNumtbt.Leave += new System.EventHandler(this.AccountNumtbt_Leave);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("微软雅黑", 19F);
            this.label1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(51)))), ((int)(((byte)(51)))), ((int)(((byte)(51)))));
            this.label1.Location = new System.Drawing.Point(80, 22);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(119, 35);
            this.label1.TabIndex = 15;
            this.label1.Text = "登录账号";
            // 
            // UserLogin
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(250)))), ((int)(((byte)(250)))));
            this.ClientSize = new System.Drawing.Size(287, 325);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.pictureBox2);
            this.Controls.Add(this.pictureBox1);
            this.Controls.Add(this.AccountNumtxt);
            this.Controls.Add(this.pictureBox4);
            this.Controls.Add(this.pictureBox3);
            this.Controls.Add(this.Logonpic);
            this.Controls.Add(this.Passwordtbt);
            this.Controls.Add(this.AccountNumtbt);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "UserLogin";
            this.Text = "登录界面";
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox4)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox3)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.Logonpic)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox2)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.TextBox Passwordtbt;
        private System.Windows.Forms.PictureBox Logonpic;
        private System.Windows.Forms.PictureBox pictureBox3;
        private System.Windows.Forms.PictureBox pictureBox4;
        private System.Windows.Forms.Label AccountNumtxt;
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.PictureBox pictureBox2;
        private System.Windows.Forms.TextBox AccountNumtbt;
        private System.Windows.Forms.Label label1;
    }
}