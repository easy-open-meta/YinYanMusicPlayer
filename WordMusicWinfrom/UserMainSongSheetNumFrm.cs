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
    public partial class UserMainSongSheetNumFrm : Form
    {
        public UserMainSongSheetNumFrm()
        {
            InitializeComponent();
        }


        List<UserSongListInfo> SongsheetInfo = null;
        SongListfrm romt = null;
        /// <summary>
        /// 查询用户自己创建的歌单
        /// </summary>
        public void SelectSongSheetInfo()
        {
            SongsheetInfo = DBSelectSongListInffo.SelectUserMeSongListInfo(LoginInfo.UserID);
            for (int i = 0; i < SongsheetInfo.Count; i++)
            {
                romt = new SongListfrm(this);
                romt.Tag = SongsheetInfo[i].SongId;
                romt.USInfomin = SongsheetInfo[i];
                FLPStorageSong.Controls.Add(romt);
            }
        }

        private void UserMainSongSheetNumFrm_Load(object sender, EventArgs e)
        {
            
            if (LoginInfo.UserID == LoginFriendinfo.FUserID || LoginFriendinfo.FUserID == "")
            {
                //列表歌单数
                PSongPheetNumtxt.Text = "我创建的歌单(" + DBUserAndFriendInfo.SelectUFSongSheetNum(LoginInfo.UserID).ToString() + ")";
                SelectSongSheetInfo();
            }
            else
            {
                //列表歌单数
                PSongPheetNumtxt.Text = "歌单(" + DBUserAndFriendInfo.SelectUFSongSheetNum(LoginFriendinfo.FUserID).ToString() + ")";
            }
        }
        public static int Delete = 0;
        public static int DeleteYN;
        private void tmrUserDeleteYN_Tick(object sender, EventArgs e)
        {
            Delete++;
                FLPStorageSong.Controls.Clear();
                SongsheetInfo = DBSelectSongListInffo.SelectUserMeSongListInfo(LoginInfo.UserID);
                for (int i = 0; i < SongsheetInfo.Count; i++)
                {
                    romt = new SongListfrm(this);
                    romt.Tag = SongsheetInfo[i].SongId;
                    romt.USInfomin = SongsheetInfo[i];
                    FLPStorageSong.Controls.Add(romt);
                }
            tmrUserDeleteYN.Stop();
        }

        private void tmrUserDelete_Tick(object sender, EventArgs e)
        {
            DeleteYN = SongListfrm.DeleteYN;
            if (Delete < DeleteYN)
            {
                tmrUserDeleteYN.Start();
            }
        }
    }
}
