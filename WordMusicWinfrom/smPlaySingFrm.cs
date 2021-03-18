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
    public partial class smPlaySingFrm : Form
    {
        public smPlaySingFrm()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 关闭窗体
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void closeSmFrm_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        #region DGV样式

        /// <summary>
        /// DGV样式
        /// </summary>
        public void CmpSetDgv()
        {
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            this.DgvListOfSongsList.AllowUserToAddRows = false;
            this.DgvListOfSongsList.AllowUserToDeleteRows = false;
            dataGridViewCellStyle1.BackColor = System.Drawing.Color.LightCyan;
            this.DgvListOfSongsList.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle1;
            //this.DgvListOfSongsList.BackgroundColor = System.Drawing.Color.White;
            this.DgvListOfSongsList.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.DgvListOfSongsList.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.Single;
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.BottomLeft;//211, 223, 240
            dataGridViewCellStyle2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(245)))), ((int)(((byte)(247)))));
            dataGridViewCellStyle2.Font = new System.Drawing.Font("微软雅黑", 10, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            this.DgvListOfSongsList.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle2;
            this.DgvListOfSongsList.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.DgvListOfSongsList.EnableHeadersVisualStyles = false;
            this.DgvListOfSongsList.GridColor = System.Drawing.SystemColors.GradientInactiveCaption;
            this.DgvListOfSongsList.ReadOnly = true;
            this.DgvListOfSongsList.RowHeadersVisible = false;
            this.DgvListOfSongsList.RowTemplate.Height = 28;
            this.DgvListOfSongsList.RowTemplate.ReadOnly = true;
        }






        #endregion

        /// <summary>
        /// 窗体加载事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void smPlaySingFrm_Load(object sender, EventArgs e)
        {
            CmpSetDgv();
            DgvListOfSongsList.AutoGenerateColumns = false;

            if (PlayingMusicClass.Songone_id != "")
            {
                DgvListOfSongsList.DataSource = DBSelectSongListInffo.SelectUserSongInfo(SongListfrm.Song_Id.ToString());
                int i = Convert.ToInt32(PlayingMusicClass.Songone_id);
                DgvListOfSongsList.CurrentCell = DgvListOfSongsList.Rows[i - 1].Cells[1];
            }

            else if (PlayingMusicClass.A_id != "")
            {
                DgvListOfSongsList.DataSource = DBSelectSongList.DBSelectSongListAlbum(PlayingMusicClass.A_id);

                int j = Convert.ToInt32(PlayingMusicClass.Cd_id);
                DgvListOfSongsList.CurrentCell = DgvListOfSongsList.Rows[j - 1].Cells[1];
            }

        }

        public static int ListOfSongsListsm;
        /// <summary>
        /// 播放包装 歌单
        /// </summary>
        public void Musicsong()
        {
            ListOfSongsListsm += 1;
            //单击获取对应歌单内歌曲编号
            PlayingMusicClass.Songone_id = DgvListOfSongsList.SelectedRows[0].Cells["clSongoneid"].Value.ToString();
            //获取对歌曲的播放地址
            PlayingMusicClass.Pusicaddress = DgvListOfSongsList.SelectedRows[0].Cells["clMusicaddress"].Value.ToString();
            //专辑ID获取
            PlayingMusicClass.A_id = DgvListOfSongsList.SelectedRows[0].Cells["clAlbumid"].Value.ToString();
            //歌手姓名
            PlayingMusicClass.S_name = DgvListOfSongsList.SelectedRows[0].Cells["clMsinger"].Value.ToString();

            PlayingMusicClass.Cd_id = "";

            //通过Aid获取的专辑ID来判断对应的专辑，然后在通过Cdid来判断当前音乐为歌单中
            //第几首音乐对此进行判断播放,而直接获取歌曲的播放路径是为了点击播放的时候可
            //以传参到主页面供播放器载入播放
        }
        /// <summary>
        /// 播放包装 专辑
        /// </summary>
        public void Musicsalbum()
        {
            ListOfSongsListsm += 1;
            //单击获取对应歌单内歌曲编号
            PlayingMusicClass.Cd_id = DgvListOfSongsList.SelectedRows[0].Cells["clMusicid"].Value.ToString();
            //获取对歌曲的播放地址
            PlayingMusicClass.Pusicaddress = DgvListOfSongsList.SelectedRows[0].Cells["clMusicaddress"].Value.ToString();
            //专辑ID获取
            PlayingMusicClass.A_id = DgvListOfSongsList.SelectedRows[0].Cells["clAlbumid"].Value.ToString();
            //歌手姓名
            PlayingMusicClass.S_name = DgvListOfSongsList.SelectedRows[0].Cells["clMsinger"].Value.ToString();

            PlayingMusicClass.Songone_id = "";

            //通过Aid获取的专辑ID来判断对应的专辑，然后在通过Cdid来判断当前音乐为歌单中
            //第几首音乐对此进行判断播放,而直接获取歌曲的播放路径是为了点击播放的时候可
            //以传参到主页面供播放器载入播放
        }
        /// <summary>
        /// 双击播放音乐
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DgvListOfSongsList_DoubleClick(object sender, EventArgs e)
        {
            if (PlayingMusicClass.Songone_id != "")
            {
                Musicsong();
            }

            else if (PlayingMusicClass.Cd_id != "")
            {
                Musicsalbum();
            }
        }

       

       
            }
        }


