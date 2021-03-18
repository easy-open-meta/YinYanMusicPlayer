using BLL;
using DAL;
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
    public partial class SongInfoSing : Form
    {
        public SongInfoSing()
        {
            InitializeComponent();
        }


        //先设一个范型用来装数据
        List<SingSongAlbumInfo> SSInfo = null;
        SongMusicAlbumFrm smaf = null;

        /// <summary>
        /// 窗体加载事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// 
        private void SongInfoSing_Load(object sender, EventArgs e)
        {
            int i;
            //获取歌手名字赋值
            string Name = SongMusicfrm.S_name;
            
            this.SingerHead.BackgroundImage = Image.FromFile(SongMusicfrm.S_pic); 

            //将歌手名字赋值到对应label里面
            SingName.Text = Name;
            //通过i值的变化判断返回对应的专辑、CD、MV数量。
            i = 1;
            SinglesNum.Text = DBSelectSingerInfo.DBSelectCDSingAll(Name,i).ToString();
            i = 2;
            AlbumNum.Text = DBSelectSingerInfo.DBSelectCDSingAll(Name,i).ToString();


            CDFrominfo cdf = new CDFrominfo();
            cdf.TopLevel = false;
            AlbumFrom.Controls.Add(cdf);
            cdf.Show();





            SSInfo = DBSelectSingSongAlbumInfo.SelectSingAlbumAll(Name);
            for (int j = 0; j < SSInfo.Count; j++)
            {
                smaf = new SongMusicAlbumFrm(this);
                smaf.Tag = SSInfo[j].Aid;
                smaf.SSabInfo = SSInfo[j];
                AlbumFrom.Controls.Add(smaf);
            }


        }
    }
}
