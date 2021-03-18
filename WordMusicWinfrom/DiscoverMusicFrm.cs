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

/// <summary>
/// 发现音乐
/// </summary>
namespace WordMusicWinfrom
{
    public partial class DiscoverMusicFrm : Form
    {
        public DiscoverMusicFrm()
        {
            InitializeComponent();
        }

        //先设一个范型用来装数据
        List<UserSongListInfo> USInfo = null;
        SongListfrm romt = null;

        

        List<SingerInfo> SInfo = null;
        SongMusicfrm smmt = null;

        private void DiscoverMusicFrm_Load(object sender, EventArgs e)
        {
            //把数据库里的数装进集合中循环显示
            USInfo = DBSelectSongListInffo.SelectUserSongListInfo();
            for (int i = 0; i < 10; i++)
            {
                romt = new SongListfrm(this);
                romt.Tag = USInfo[i].SongId;
                romt.USInfomin = USInfo[i];
                ReMusicTable.Controls.Add(romt);
            }


            //把数据库里的数装进集合中循环显示
            SInfo = DBSelectSingerInfo.SelectSingerInfoAll();
            for (int j = 0; j < 5; j++)
            {

                smmt = new SongMusicfrm(this);
                smmt.Tag = SInfo[j].Sid;
                smmt.SIfomin = SInfo[j];
                ReSingerTable.Controls.Add(smmt);
            }

        }

    }
}
