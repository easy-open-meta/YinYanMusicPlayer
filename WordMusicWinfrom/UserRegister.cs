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
    public partial class UserRegister : Form
    {
        public UserRegister()
        {
            InitializeComponent();
        }


        #region textbox获取焦点
        /// <summary>
        /// 账号获取焦点效果
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AccountNumtbx_Enter(object sender, EventArgs e)
        {
            if (AccountNumtbx.Text == "请输入注册账号")
            {
                AccountNumtbx.ForeColor = Color.FromArgb(51, 51, 51);
                AccountNumtbx.Text = "";
            }
        }
        /// <summary>
        /// 密码获取焦点效果
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Passwordtbt_Enter(object sender, EventArgs e)
        {
            if (Passwordtbt.Text == "密码")
            {
                Passwordtbt.ForeColor = Color.FromArgb(51, 51, 51);
                Passwordtbt.Text = "";
            }
        }
        /// <summary>
        /// 确认密码获取焦点效果
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void confirmPwdtbt_Enter(object sender, EventArgs e)
        {
            if (confirmPwdtbt.Text == "确认密码")
            {
                confirmPwdtbt.ForeColor = Color.FromArgb(51, 51, 51);
                confirmPwdtbt.Text = "";
            }
        }


        /// <summary>
        /// 账号失去焦点效果
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AccountNumtbx_Leave(object sender, EventArgs e)
        {
            if (AccountNumtbx.Text == "")
            {
                AccountNumtbx.ForeColor = Color.FromArgb(100,100,100);
                AccountNumtbx.Text = "请输入注册账号";
            }
        }
        /// <summary>
        /// 密码失去焦点效果
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Passwordtbt_Leave(object sender, EventArgs e)
        {
            if (Passwordtbt.Text == "")
            {
                Passwordtbt.ForeColor = Color.FromArgb(100,100,100);
                Passwordtbt.Text = "密码";
            }
        }
        /// <summary>
        /// 确认密码失去焦点效果
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void confirmPwdtbt_Leave(object sender, EventArgs e)
        {
            if (confirmPwdtbt.Text == "")
            {
                confirmPwdtbt.ForeColor = Color.FromArgb(100,100,100);
                confirmPwdtbt.Text = "确认密码";
            }
        }


        #endregion

        /// <summary>
        /// 判断账号密码是否为空
        /// </summary>
        public void Login()
        {
            if (AccountNumtbx.Text == "请输入注册账号" || AccountNumtbx.Text == "")
            {
                AccountNumtxt.Text = "账号不能为空";
                AccountNumtxt.Visible = true;
            }
            else
            {
                AccountNumtxt.Visible = false;
            }
            if (Passwordtbt.Text == "密码" || Passwordtbt.Text == "")
            {
                Passwordtxt.Text = "密码不能为空";
                Passwordtxt.Visible = true;
            }
            else
            {
                Passwordtxt.Visible = false;
            }
            if (confirmPwdtbt.Text == "确认密码" || confirmPwdtbt.Text == "")
            {
                confirmPwdtxt.Text = "确认密码不能为空";
                confirmPwdtxt.Visible = true;
            }
            else
            {
                confirmPwdtxt.Visible = false;
            }
        }


        //判断登录后关闭窗体
        public static int LoginNums = 0;

        public static string id;
        public static string pwd;
        public static string cpwd;
        public static string time;

        /// <summary>
        /// 点击注册
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Registerpic_Click(object sender, EventArgs e)
        {

            Login();

            if (AccountNumtbx.Text != "请输入注册账号" && Passwordtbt.Text != "密码" && confirmPwdtbt.Text != "确认密码"&& AccountNumtbx.Text != "" && Passwordtbt.Text != "" && confirmPwdtbt.Text != "")
            {

                //账号 和 密码 和 确认的密码
                id = AccountNumtbx.Text;
                pwd = Passwordtbt.Text;
                cpwd = confirmPwdtbt.Text;

                //判断账号是否存在
                UserInfo UI = DBselectUserInfo.SelectUserfullName(id);

                if (UI == null)
                {
                    if (pwd == cpwd)
                    {
                        //string time = 
                        #region 将用户的输入值传入数据库
                        UserInfo u = new UserInfo();
                        u.UfullName = id;
                        u.Upwd = pwd;
                        u.Uname = id;
                        u.RegisterTime = DateTime.Now;
                        u.Ubirthday = DateTime.Now;
                        u.Usex = "保密";
                        u.Uimage = "SingerIMG\\en.png";
                        u.Usignature = "这个人什么都没有留下来";
                        #endregion

                        time = DateTime.Now.ToShortDateString() ;
                        DBselectUserInfo.InsertUserInfo(u);
                        //将用户的信息更新到主界面
                        LoginNums += 1;
                    }
                    else
                    {
                        confirmPwdtxt.Text = "确认密码与密码不符";
                        confirmPwdtxt.Visible = true;
                        confirmPwdtbt.Text = "";
                    }       
                }
                else
                {
                    //账号已存在
                    AccountNumtxt.Text = "账号已存在";
                    AccountNumtxt.Visible = true;
                    Passwordtbt.Text = "";
                    confirmPwdtbt.Text = "";
                }
            }
           
        }
    }
}
