using BLL;
using DAL;
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
    public partial class UpUserSongTableInfoFrm : Form
    {
        public UpUserSongTableInfoFrm()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 窗体加载事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UpUserSongTableInfoFrm_Load(object sender, EventArgs e)
        {
            SingerPC.BackgroundImage = Image.FromFile(SongListfrm.Song_Pic);

            SongSheetNametxt.Text = SongListfrm.Song_Name;

            LabelTagtxt.Text = DBSelectSongListInffo.SelectPartitionName(SongListfrm.Par_id).ToString();

            BriefIntroductionrtb.Text = SongListfrm.Song_Info;




        }

        public static int UserSongNum;

        public static int Song_Id;
        public static string Song_Name;
        public static string Par_name;
        public static string Song_Info;

        public static int UpUserNumYN;
        private void DeleteYespic_Click(object sender, EventArgs e)
        {
            Song_Id = SongListfrm.Song_Id;
            Song_Name = SongSheetNametxt.Text;
            Par_name = DBSelectSongListInffo.SelectPartitionid(LabelTagtxt.Text).ToString();

            if (Song_Info == "")
            {
                Song_Info = "本歌单暂无简介哟。";
            }
            else
            {
                Song_Info = BriefIntroductionrtb.Text;
            }


            if (SongSheetNametxt.Text == "")
            {
                SongSheetNametxt.Text = "歌单名称不能为空";
            }
            else
            {
                UpUserNumYN = 1;

                AddSuccessfulAndUnsuccessful asau = new AddSuccessfulAndUnsuccessful();
                asau.TopLevel = true;

                //获取当前窗体位置
                int x = this.Location.X;
                int y = this.Location.Y;


                ////定位位置
                Point p = new Point(x + 642, y + 347);
                asau.StartPosition = FormStartPosition.Manual;
                asau.Location = p;
                asau.ShowDialog();

                UpUserNumYN = 0;
                UserInfoSelect.UpdateUserSongInfo(Song_Name, Par_name, Song_Info, Song_Id);
                UserSongNum += 1;
            }

            

            
        }

        private void DeleteNopic_Click(object sender, EventArgs e)
        {
            UserSongNum += 1;
        }

        private void SongSheetNametxt_Enter(object sender, EventArgs e)
        {
            if (SongSheetNametxt.Text == "歌单名称不能为空")
            {
                SongSheetNametxt.Text = "";
            }
        }
    }
}
