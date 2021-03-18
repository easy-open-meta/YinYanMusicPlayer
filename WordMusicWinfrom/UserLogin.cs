using BLL;
using Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WordMusicWinfrom
{
    public partial class UserLogin : Form
    {
        public UserLogin()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 错误提示
        /// </summary>
        public void OperationFailed()
        {
            AccountNumtxt.Visible = true;
            AccountNumtxt.Text = "账号或者密码错误";
            Passwordtbt.Text = "";
        }

        //判断登录后关闭窗体
        public static int LoginNum = 0;
        /// <summary>
        /// 单击登录
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Logonpic_Click(object sender, EventArgs e)
        {
            if (AccountNumtbt.Text != "" && Passwordtbt.Text != "" && AccountNumtbt.Text != "请输入账号" && AccountNumtbt.Text != "密码")
            {

                //账号 和 密码
                string id = AccountNumtbt.Text;
                string pwd = Passwordtbt.Text;

                //判断账号是否存在
                UserInfo UI = DBselectUserInfo.SelectUserfullName(id);

                if (UI != null)
                {
                    //判断账号的密码是否和一致
                    UI = DBselectUserInfo.SelectUserPwdOKNO(id, pwd);
                    if (UI != null)
                    {
                        LoginInfo.UserID = UI.Uid.ToString();
                        LoginInfo.UserName = UI.Uname;
                        LoginInfo.RegisterTime = UI.RegisterTime.ToString();
                        LoginInfo.UserBirthday = UI.Ubirthday.ToString();
                        LoginInfo.UserSex = UI.Usex;
                        LoginInfo.UserImage = UI.Uimage;
                        LoginInfo.UserSignature = UI.Usignature;
                        //将用户的信息更新到主界面
                        LoginNum += 1;
                        

                    }
                    else
                    {
                        //密码错误时
                        OperationFailed();
                    }
                }
                else
                {
                    //账号错误时
                    OperationFailed();
                }
            }
            else
            {
                //密码或账号为空时
                AccountNumtxt.Visible = true;
                AccountNumtxt.Text = "账号和密码不能为空";
            }
        }

        #region 焦点选中后显示提示

        private void AccountNumtbt_Enter(object sender, EventArgs e)
        {
            if (AccountNumtbt.Text == "请输入账号")
            {
                AccountNumtbt.ForeColor = Color.FromArgb(51, 51, 51);
                AccountNumtbt.Text = "";
            }
        }

        private void Passwordtbt_Enter(object sender, EventArgs e)
        {
            if (Passwordtbt.Text == "密码")
            {
                Passwordtbt.ForeColor = Color.FromArgb(51, 51, 51);
                Passwordtbt.PasswordChar = ('●');
                Passwordtbt.Text = "";
            }
        }

        private void AccountNumtbt_Leave(object sender, EventArgs e)
        {
            if (AccountNumtbt.Text == "")
            {
                AccountNumtbt.ForeColor = Color.FromArgb(100,100,100);
                
                AccountNumtbt.Text = "请输入账号";
            }
        }

        private void Passwordtbt_Leave(object sender, EventArgs e)
        {
            if (Passwordtbt.Text == "")
            {
                Passwordtbt.ForeColor = Color.FromArgb(100,100,100);
                Passwordtbt.PasswordChar = new char();
                Passwordtbt.Text = "密码";
            }
        }

        #endregion

    }
}
