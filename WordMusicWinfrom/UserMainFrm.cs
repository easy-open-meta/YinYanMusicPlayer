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
    public partial class UserMainFrm : Form
    {
        public UserMainFrm()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 窗体加载事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserMainFrm_Load(object sender, EventArgs e)
        {
            #region 用户信息
            //获取名字
            UserNametxt.Text = LoginInfo.UserName;
            //获取头像
            //this.UserHeadpic.BackgroundImage = Image.FromFile(LoginInfo.UserImage);
            //获取性别
            string Sex = LoginInfo.UserSex;
            if (Sex == "男")
            {
                this.UserSexpic.BackgroundImage = Image.FromFile(@"IMG/Manpic.png");
            }
            else if (Sex == "女")
            {
                this.UserSexpic.BackgroundImage = Image.FromFile(@"IMG/Wenpic.png");
            }else { }
            //用户有多少歌单
            SongSheetNumtxt.Text = DBUserAndFriendInfo.SelectUFSongSheetNum(LoginInfo.UserID).ToString();
            //用户有多少关注
            FollowNumtxt.Text = DBUserAndFriendInfo.SelectUFfollowNum(LoginInfo.UserID).ToString();
            //用户有多少粉丝
            FansNumtxt.Text = DBUserAndFriendInfo.SelectUFFansNum(LoginInfo.UserID).ToString();
            //用户签名
            PersonalProfiletxt.Text += LoginInfo.UserSignature;
            ////列表歌单数
            //PSongPheetNumtxt.Text = "歌单("+ SongSheetNumtxt.Text + ")";
            ////列表收藏数
            //PCollectionNumtxt.Text = "收藏("+DBUserAndFriendInfo.SelectUFCollectionSongSheetNum(LoginInfo.UserID) + ")";

            
            #endregion

            if (MainInterface.PLSearchSongList == 1)
            {
                Collectionpic.Image = Image.FromFile(@"IMG/UserCollectionpiccl.png");
                SongSheetpic.Image = Image.FromFile(@"IMG/UserSongSheetpic.png");

                UserSongSheetCarrier.Controls.Clear();
                UserMainCollectionNumFrm umcnf = new UserMainCollectionNumFrm();
                umcnf.TopLevel = false;
                UserSongSheetCarrier.Controls.Add(umcnf);
                umcnf.Show();
            }
            else if (MainInterface.PLSearchSongList == 2)
            {
                //收藏还原歌单变色
                Collectionpic.Image = Image.FromFile(@"IMG/UserCollectionpic.png");
                SongSheetpic.Image = Image.FromFile(@"IMG/UserSongSheetpiccl.png");

                //进入个人主页
                UserSongSheetCarrier.Controls.Clear();
                UserMainSongSheetNumFrm umssnf = new UserMainSongSheetNumFrm();
                umssnf.TopLevel = false;
                UserSongSheetCarrier.Controls.Add(umssnf);
                umssnf.Show();
            }
            else
            {

            }



        }

        /// <summary>
        /// 进入用户歌单
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SongSheetpic_Click(object sender, EventArgs e)
        {
            //收藏还原歌单变色
            Collectionpic.Image = Image.FromFile(@"IMG/UserCollectionpic.png"); 
            SongSheetpic.Image = Image.FromFile(@"IMG/UserSongSheetpiccl.png");

            //进入个人主页
            UserSongSheetCarrier.Controls.Clear();
            UserMainSongSheetNumFrm umssnf = new UserMainSongSheetNumFrm();
            umssnf.TopLevel = false;
            UserSongSheetCarrier.Controls.Add(umssnf);
            umssnf.Show();

        }

        /// <summary>
        /// 进入收藏歌单
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Collectionpic_Click(object sender, EventArgs e)
        {
            //歌单还原收藏变色
            Collectionpic.Image = Image.FromFile(@"IMG/UserCollectionpiccl.png");
            SongSheetpic.Image = Image.FromFile(@"IMG/UserSongSheetpic.png");

            UserSongSheetCarrier.Controls.Clear();
            UserMainCollectionNumFrm umcnf = new UserMainCollectionNumFrm();
            umcnf.TopLevel = false;
            UserSongSheetCarrier.Controls.Add(umcnf);
            umcnf.Show();
        }

        private void button1_Click(object sender, EventArgs e)
        {

            UpNewSongListFrm asau = new UpNewSongListFrm();
            asau.TopLevel = true;

            //获取当前窗体位置
            int x = this.Location.X;
            int y = this.Location.Y;


            ////定位位置
            Point p = new Point(x + 600, y + 300);
            asau.StartPosition = FormStartPosition.Manual;
            asau.Location = p;
            asau.ShowDialog();
        }
    }
}
