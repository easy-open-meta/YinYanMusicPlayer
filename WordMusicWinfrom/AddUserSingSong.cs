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
    public partial class AddUserSingSong : Form
    {
        public AddUserSingSong()
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

        private void AddUserSingSong_Load(object sender, EventArgs e)
        {
            if (SongListfrm.Yes == 1)
            {
                label1.Visible = false;
                label2.Visible = true;
                label3.Visible = false;
            }
            else if (SongListfrm.Yes == 0)
            {
                label3.Visible = false;
                label2.Visible = false;
                label1.Visible = true;
            }
            else if (SongListfrm.Yes == 2)
            {
                label3.Visible = true;
                label2.Visible = false;
                label1.Visible = false;  
            }
        }

        private void tmrClose_Tick(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
