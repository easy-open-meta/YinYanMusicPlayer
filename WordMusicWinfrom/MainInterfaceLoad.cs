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
    /// <summary>
    /// 音言主界面加载中
    /// </summary>
    public partial class MainInterfaceLoad : Form
    {
        public MainInterfaceLoad()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 窗体加载后进入主界面
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainInterfaceLoad_Load(object sender, EventArgs e)
        {
            
            
               
        }

        /// <summary>
        /// 窗体加载后进如主界面  定时器
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LoadTime_Tick(object sender, EventArgs e)
        {           
            //影藏当前界面
            this.Hide();
            //实例化主界面然后显示出来
            MainInterface mi = new MainInterface();
            mi.Show();
            //结束定时器
            LoadTime.Stop();
        }

        #region 加载
        public static int i = 0;
        private void Login_Tick(object sender, EventArgs e)
        {
            i++;
            if (i == 1)
            {
                MusicLogintxt.Text = ".";
            }
            else if (i == 2)
            {
                MusicLogintxt.Text = "..";
            } else
            {
                MusicLogintxt.Text = "...";
                i = 0;
            }
        }
        #endregion
    }
}
