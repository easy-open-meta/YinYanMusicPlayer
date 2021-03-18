using BLL;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WordMusicWinfrom
{
    public partial class UserLRFrm : Form
    {
        public UserLRFrm()
        {
            InitializeComponent();

            SetClassLong(this.Handle, GCL_STYLE, GetClassLong(this.Handle, GCL_STYLE) | CS_DropSHADOW); //API函数加载，实现窗体边框阴影效果

        }

        #region 窗体边框阴影效果变量申明

        const int CS_DropSHADOW = 0x20000;
        const int GCL_STYLE = (-26);
        //声明Win32 API
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int SetClassLong(IntPtr hwnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetClassLong(IntPtr hwnd, int nIndex);

        #endregion

        private void UserLRFrm_Load(object sender, EventArgs e)
        {
            UserLogin ul = new UserLogin();
            ul.TopLevel = false;
            UserLRpanel.Controls.Add(ul);
            ul.Show();
        }


        /// <summary>
        /// 返回登录界面
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CloseRegistrationtxt_Click(object sender, EventArgs e)
        {
            RegisterFrmtxt.Visible = true;
            CloseRegistrationtxt.Visible = false;


            //删除所有控件在生成
            UserLRpanel.Controls.Clear();
            UserLogin ul = new UserLogin();
            ul.TopLevel = false;
            UserLRpanel.Controls.Add(ul);
            ul.Show();
        }
        /// <summary>
        /// 返回注册界面
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void register_Click(object sender, EventArgs e)
        {
            CloseRegistrationtxt.Visible = true;
            RegisterFrmtxt.Visible = false;
            //删除所有控件在生成
            UserLRpanel.Controls.Clear();
            UserRegister ur = new UserRegister();
            ur.TopLevel = false;
            UserLRpanel.Controls.Add(ur);
            ur.Show();
        }
        public static int i;
        public static int j;
        public static int UserLoginex;
        public static int UserLoginexs;


        private void UserLoginNums_Tick(object sender, EventArgs e)
        {
            if (i < UserLoginex)
            {
                this.Close();
            }
            if(j < UserLoginexs)
            {
                ++j; 
                CloseRegistrationtxt.Visible = true;
                RegisterFrmtxt.Visible = false;
                //删除所有控件在生成
                UserLRpanel.Controls.Clear();
                LoginandPwdInfo lpi = new LoginandPwdInfo();
                lpi.TopLevel = false;
                UserLRpanel.Controls.Add(lpi);
                lpi.Show();
                UserLoginNums.Stop();
            }
            
        }

        private void UserLoginNum_Tick(object sender, EventArgs e)
        {
            UserLoginex = UserLogin.LoginNum;
            if (i < UserLoginex)
            {
                UserLoginNums.Start();
            }
            UserLoginexs = UserRegister.LoginNums;
            if (j < UserLoginexs)
            {
                UserLoginNums.Start();
            }
        }

        private void MusicClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
