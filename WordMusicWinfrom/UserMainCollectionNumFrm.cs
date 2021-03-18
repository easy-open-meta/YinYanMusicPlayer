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
    public partial class UserMainCollectionNumFrm : Form
    {
        public UserMainCollectionNumFrm()
        {
            InitializeComponent();
        }


        List<UserSongListInfo> SongsheetInfo = null;
        SongListfrm romt = null;
        /// <summary>
        /// 查询用户自己收藏的歌单
        /// </summary>
        public void SelectCollectionInfo()
        {
            SongsheetInfo = DBSelectSongListInffo.SelectUserMeCollectionListInfo(LoginInfo.UserID);
            for (int i = 0; i < SongsheetInfo.Count; i++)
            {
                romt = new SongListfrm(this);
                romt.Tag = SongsheetInfo[i].SongId;
                romt.USInfomin = SongsheetInfo[i];
                FLPStorageShoucang.Controls.Add(romt);
            }
        }


        private void UserMainCollectionNumFrm_Load(object sender, EventArgs e)
        {
           
            if (LoginInfo.UserID == LoginFriendinfo.FUserID || LoginFriendinfo.FUserID == "")
            {
                //列表收藏数
                PCollectionNumtxt.Text = "我收藏的歌单(" + DBUserAndFriendInfo.SelectUFCollectionSongSheetNum(LoginInfo.UserID) + ")";
                SelectCollectionInfo();
            }
            else
            {
                //列表收藏数
                PCollectionNumtxt.Text = "收藏(" + DBUserAndFriendInfo.SelectUFCollectionSongSheetNum(LoginFriendinfo.FUserID) + ")";

            }
        }
    }
}
