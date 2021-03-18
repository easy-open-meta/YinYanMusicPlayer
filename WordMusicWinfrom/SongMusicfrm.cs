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
    public partial class SongMusicfrm : UserControl
    {
        DiscoverMusicFrm dmf = null;
        public SongMusicfrm(DiscoverMusicFrm dmf)
        {
            InitializeComponent();
            this.dmf = dmf;
        }


        //public delegate void changetext(string SingNametxt);
        //public event changetext changetext_event;
        SongSheetFrm ssf = null;
        public SongMusicfrm(SongSheetFrm ssf)
        {
            InitializeComponent();
            this.ssf = ssf;
        }

        public static string U_id;
        public static string S_id;
        public static string S_name;
        public static string S_type;
        public static string S_sex;
        public static int CD_u;
        public static int Album_u;
        public static int MV_u;
        public static string S_pic;
        public static string S_info;
        /// <summary>
        /// 占用 判断是否以选择歌手
        /// </summary>
        public static int Occupy;


        /// <summary>
        /// new一个用户歌单信息表
        /// </summary>
        public SingerInfo SIfomin = new SingerInfo();

        private void SongMusicfrm_Load(object sender, EventArgs e)
        {
            //调用两个信息出来赋值
            SingerNA.Text = SIfomin.Sname;            
            this.SingerPC.BackgroundImage = Image.FromFile(SIfomin.Spic);
      

          
        }

        /// <summary>
        /// 双击歌手图片显示歌手主页
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SingerPC_DoubleClick(object sender, EventArgs e)
        {
            //已选中标签
            Occupy += 1;
            //歌手名字
            S_name = SIfomin.Sname;
            //CD数量
            CD_u = SIfomin.CDu;
            //歌曲数量
            Album_u = SIfomin.Albumu;
            //MV数量
            MV_u = SIfomin.MVu;
            //赋值图片
            S_pic = SIfomin.Spic;

            //MessageBox.Show("歌手名："+SingerNA.Text, "登录提示", MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation);
            //MainInterface mif = new MainInterface(this);
            //mif.changetext_event += new Form2.changetext(frm_changetext_event);
            //changetext_event(SingerNA.Text);

            ////删除所有控件在生成
            //MusicMainInterface.Controls.Clear();
            //DiscoverMusicFrm dmf = new DiscoverMusicFrm();
            //dmf.TopLevel = false;
            //MusicMainInterface.Controls.Add(dmf);
            //dmf.Show();
        }
    }
}
