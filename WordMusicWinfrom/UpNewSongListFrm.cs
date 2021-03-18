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
    public partial class UpNewSongListFrm : Form
    {
        public UpNewSongListFrm()
        {
            InitializeComponent();
        }

        private void UpNewSongListFrm_Load(object sender, EventArgs e)
        {
            DeleteYespic.BackgroundImage = Image.FromFile(@"IMG/DontDeleteYespic.png");
            SongSheetNametxt.Text = "请输入新歌单标题";
        }
        /// <summary>
        /// 获得焦点
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SongSheetNametxt_Enter(object sender, EventArgs e)
        {
            if (SongSheetNametxt.Text == "请输入新歌单标题" || SongSheetNametxt.Text == "歌单名称不能为空")
            {
                SongSheetNametxt.ForeColor = Color.FromArgb(51, 51, 51);
                SongSheetNametxt.Text = "";
            }
        }
        /// <summary>
        /// 失去焦点
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SongSheetNametxt_Leave(object sender, EventArgs e)
        {
            if (SongSheetNametxt.Text == "")
            {
                SongSheetNametxt.ForeColor = Color.FromArgb(100, 100, 100);
                SongSheetNametxt.Text = "请输入新歌单标题";
            }
        }

        public static int UpUserSongNumYN;

        /// <summary>
        /// 保存事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DeleteYespic_Click(object sender, EventArgs e)
        {

            if (DeleteYespic.BackgroundImage != Image.FromFile(@"IMG/DontDeleteYespic.png"))
            {


                #region 将用户的输入值传入数据库

                UserSongListInfo usli = new UserSongListInfo();
                usli.Uid = LoginInfo.UserID;
                usli.Uname = LoginInfo.UserName;
                usli.ParId = "";
                usli.SongCollection = 0;
                usli.SongName = SongSheetNametxt.Text;
                usli.SongSingU = 1;
                usli.SongInfo = "";
                usli.SongTime = DateTime.Now.Date;
                usli.SongPic = "IMG\\OriginalSongListinterfacepicssm.png";

                #endregion
                DBselectUserInfo.InsertUpNewSongList(usli);

                

                UpUserSongNumYN = 1;

                //先影藏窗体
                this.Opacity = .0;

                AddSuccessfulAndUnsuccessful asau = new AddSuccessfulAndUnsuccessful();

                asau.TopLevel = true;
                //获取当前窗体位置
                int x = this.Location.X;
                int y = this.Location.Y;


                ////定位位置
                Point p = new Point(x, y);
                asau.StartPosition = FormStartPosition.Manual;
                asau.Location = p;
                asau.ShowDialog();

                UpUserSongNumYN = 0;

                this.Close();
            }
            else
            {
                //没有效果
                SongSheetNametxt.Text = "歌单名称不能为空";
            }
        }

        /// <summary>
        /// 私密歌单
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void SongSheetNametxt_TextChanged(object sender, EventArgs e)
        {
            if (SongSheetNametxt.Text == "请输入新歌单标题" || SongSheetNametxt.Text == "" || SongSheetNametxt.Text == "歌单名称不能为空")
            {
                this.DeleteYespic.BackgroundImage = Image.FromFile(@"IMG/DontDeleteYespic.png");
            }
            else
            {
                this.DeleteYespic.BackgroundImage = Image.FromFile(@"IMG/DeleteYespic.png");
            }
                //DontDeleteYespic
        }

        private void DeleteNopic_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
