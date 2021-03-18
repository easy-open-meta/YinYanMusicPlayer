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
    public partial class CollectionOfSongsFrm : Form
    {
        public CollectionOfSongsFrm()
        {
            InitializeComponent();
        }

        //先设一个范型用来装数据
        List<UserSongListInfo> USInfo = null;
        SongListfrm romt = null;
        private void CollectionOfSongsFrm_Load(object sender, EventArgs e)
        {
            //ReMusicTable.Controls.Clear();
            //把数据库里的数装进即合里
            USInfo = DBSelectSongListInffo.SelectUserSongListInfo();
            for (int i = 0; i < USInfo.Count; i++)
            {

                romt = new SongListfrm(this);
                romt.Tag = USInfo[i].SongId;
                romt.USInfomin = USInfo[i];
                CollectionOfSingersFLP.Controls.Add(romt);
            }
        }
        /// <summary>
        /// 华语
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Chinesetxt_Click(object sender, EventArgs e)
        {
            //清空控件
            CollectionOfSingersFLP.Controls.Clear();
            USInfo = DBSelectSongListInffo.SelectUserSongChinesetxtListInfo();
            for (int i = 0; i < USInfo.Count; i++)
            {

                romt = new SongListfrm(this);
                romt.Tag = USInfo[i].SongId;
                romt.USInfomin = USInfo[i];
                CollectionOfSingersFLP.Controls.Add(romt);
            }
        }
        /// <summary>
        /// 现代
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Populartxt_Click(object sender, EventArgs e)
        {
            //清空控件
            CollectionOfSingersFLP.Controls.Clear();
            USInfo = DBSelectSongListInffo.SelectUserSongPopulartxtListInfo();
            for (int i = 0; i < USInfo.Count; i++)
            {

                romt = new SongListfrm(this);
                romt.Tag = USInfo[i].SongId;
                romt.USInfomin = USInfo[i];
                CollectionOfSingersFLP.Controls.Add(romt);
            }
        }
        /// <summary>
        /// 古风
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Balladtxt_Click(object sender, EventArgs e)
        {
            //清空控件
            CollectionOfSingersFLP.Controls.Clear();
            USInfo = DBSelectSongListInffo.SelectUserSongBalladtxtListInfo();
            for (int i = 0; i < USInfo.Count; i++)
            {

                romt = new SongListfrm(this);
                romt.Tag = USInfo[i].SongId;
                romt.USInfomin = USInfo[i];
                CollectionOfSingersFLP.Controls.Add(romt);
            }
        }
        /// <summary>
        /// 摇滚
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Rocktxt_Click(object sender, EventArgs e)
        {
            //清空控件
            CollectionOfSingersFLP.Controls.Clear();
            USInfo = DBSelectSongListInffo.SelectUserSongRocktxtListInfo();
            for (int i = 0; i < USInfo.Count; i++)
            {

                romt = new SongListfrm(this);
                romt.Tag = USInfo[i].SongId;
                romt.USInfomin = USInfo[i];
                CollectionOfSingersFLP.Controls.Add(romt);
            }
        }
        /// <summary>
        /// 电子
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Electronicstxt_Click(object sender, EventArgs e)
        {
            //清空控件
            CollectionOfSingersFLP.Controls.Clear();
            USInfo = DBSelectSongListInffo.SelectUserSongElectronicstxtListInfo();
            for (int i = 0; i < USInfo.Count; i++)
            {

                romt = new SongListfrm(this);
                romt.Tag = USInfo[i].SongId;
                romt.USInfomin = USInfo[i];
                CollectionOfSingersFLP.Controls.Add(romt);
            }
        }
        /// <summary>
        /// 另类/独立
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Offbeattxt_Click(object sender, EventArgs e)
        {
            //清空控件
            CollectionOfSingersFLP.Controls.Clear();
            USInfo = DBSelectSongListInffo.SelectUserSongOffbeattxtListInfo();
            for (int i = 0; i < USInfo.Count; i++)
            {

                romt = new SongListfrm(this);
                romt.Tag = USInfo[i].SongId;
                romt.USInfomin = USInfo[i];
                CollectionOfSingersFLP.Controls.Add(romt);
            }
        }
        /// <summary>
        /// 轻音乐
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LightMusictxt_Click(object sender, EventArgs e)
        {
            //清空控件
            CollectionOfSingersFLP.Controls.Clear();
            USInfo = DBSelectSongListInffo.SelectUserSongLightMusictxtListInfo();
            for (int i = 0; i < USInfo.Count; i++)
            {

                romt = new SongListfrm(this);
                romt.Tag = USInfo[i].SongId;
                romt.USInfomin = USInfo[i];
                CollectionOfSingersFLP.Controls.Add(romt);
            }
        }
        /// <summary>
        /// 影视音乐
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MovieReasontxt_Click(object sender, EventArgs e)
        {
            //清空控件
            CollectionOfSingersFLP.Controls.Clear();
            USInfo = DBSelectSongListInffo.SelectUserSongMovieReasontxtListInfo();
            for (int i = 0; i < USInfo.Count; i++)
            {

                romt = new SongListfrm(this);
                romt.Tag = USInfo[i].SongId;
                romt.USInfomin = USInfo[i];
                CollectionOfSingersFLP.Controls.Add(romt);
            }
        }
        /// <summary>
        /// ACG
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ACGtxt_Click(object sender, EventArgs e)
        {
            //清空控件
            CollectionOfSingersFLP.Controls.Clear();
            USInfo = DBSelectSongListInffo.SelectUserSongACGtxtListInfo();
            for (int i = 0; i < USInfo.Count; i++)
            {

                romt = new SongListfrm(this);
                romt.Tag = USInfo[i].SongId;
                romt.USInfomin = USInfo[i];
                CollectionOfSingersFLP.Controls.Add(romt);
            }
        }
        /// <summary>
        /// 怀旧
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Nostalgiatxt_Click(object sender, EventArgs e)
        {
            //清空控件
            CollectionOfSingersFLP.Controls.Clear();
            USInfo = DBSelectSongListInffo.SelectUserSongNostalgiatxtListInfo();
            for (int i = 0; i < USInfo.Count; i++)
            {

                romt = new SongListfrm(this);
                romt.Tag = USInfo[i].SongId;
                romt.USInfomin = USInfo[i];
                CollectionOfSingersFLP.Controls.Add(romt);
            }
        }
    }
}
