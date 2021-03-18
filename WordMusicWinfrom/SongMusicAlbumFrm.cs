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
    public partial class SongMusicAlbumFrm : UserControl
    {

        SongInfoSing sis = null;

        public SongMusicAlbumFrm( SongInfoSing sis )
        {
            InitializeComponent();
            this.sis = sis;
        }

        #region DGV样式

        /// <summary>
        /// DGV样式
        /// </summary>
        public void CmpSetDgv()
        {
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            this.DgvSongMusicAlbum.AllowUserToAddRows = false;
            this.DgvSongMusicAlbum.AllowUserToDeleteRows = false;
            dataGridViewCellStyle1.BackColor = System.Drawing.Color.LightCyan;
            this.DgvSongMusicAlbum.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle1;
            //this.DgvListOfSongsList.BackgroundColor = System.Drawing.Color.White;
            this.DgvSongMusicAlbum.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.DgvSongMusicAlbum.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.Single;
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.BottomLeft;//211, 223, 240
            dataGridViewCellStyle2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(245)))), ((int)(((byte)(247)))));
            dataGridViewCellStyle2.Font = new System.Drawing.Font("微软雅黑", 10, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            this.DgvSongMusicAlbum.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle2;
            this.DgvSongMusicAlbum.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.DgvSongMusicAlbum.EnableHeadersVisualStyles = false;
            this.DgvSongMusicAlbum.GridColor = System.Drawing.SystemColors.GradientInactiveCaption;
            this.DgvSongMusicAlbum.ReadOnly = true;
            this.DgvSongMusicAlbum.RowHeadersVisible = false;
            this.DgvSongMusicAlbum.RowTemplate.Height = 28;
            this.DgvSongMusicAlbum.RowTemplate.ReadOnly = true;
        }

        #endregion


        /// <summary>
        /// 歌手ID
        /// </summary>
        public static string S_id;
        /// <summary>
        /// 歌手姓名
        /// </summary>
        public static string S_name;
        /// <summary>
        /// 专辑ID
        /// </summary>
        public static string A_id;
        /// <summary>
        /// 专辑名字
        /// </summary>
        public static string A_name;
        /// <summary>
        /// 发布专辑时间
        /// </summary>
        public static string A_time;
        /// <summary>
        /// 专辑简介
        /// </summary>
        public static string A_details;
        /// <summary>
        /// 专辑图片大图
        /// </summary>
        public static string A_bigpic;
        /// <summary>
        /// 专辑图片小图
        /// </summary>
        public static string A_smlpic;
        /// <summary>
        /// 专辑单曲ID编号
        /// </summary>
        public static string Cd_id;


        /// <summary>
        /// 获取选中音乐文件
        /// </summary>
        public static string Pusicaddress;
        /// <summary>
        /// 获取选中音乐歌词文件
        /// </summary>
        public static string Parlyric;

        #region 窗体自绘编号RowPostPaint
        /// <summary>
        /// 窗体自绘编号RowPostPaint
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DgvSongMusicAlbum_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            //自动编号，与数据无关
            Rectangle rectangle = new Rectangle(e.RowBounds.Location.X,
               e.RowBounds.Location.Y,
               DgvSongMusicAlbum.RowHeadersWidth - 4,
               e.RowBounds.Height);

            if (e.RowIndex < 9)
            {
                TextRenderer.DrawText(e.Graphics,
                      "0" + (e.RowIndex + 1).ToString(),
                       DgvSongMusicAlbum.RowHeadersDefaultCellStyle.Font,
                       rectangle,
                       DgvSongMusicAlbum.RowHeadersDefaultCellStyle.ForeColor,
                       TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
            }
            else
            {
                TextRenderer.DrawText(e.Graphics,
                       (e.RowIndex + 1).ToString(),
                       DgvSongMusicAlbum.RowHeadersDefaultCellStyle.Font,
                       rectangle,
                       DgvSongMusicAlbum.RowHeadersDefaultCellStyle.ForeColor,
                       TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
            }
        }
        #endregion


        /// <summary>
        /// new一个用户歌单信息表
        /// </summary>
        public SingSongAlbumInfo SSabInfo = new SingSongAlbumInfo();

        public static string Album_Name;
        private void SongMusicAlbumFrm_Load(object sender, EventArgs e)
        {
            CmpSetDgv();

            

            //专辑名字
            AlbumNameTxt.Text = SSabInfo.Aname;
            //发布时间
            AlbumTimeTxt.Text = SSabInfo.Atime;
            //获取图片
            this.MusicAlbumpic.BackgroundImage = Image.FromFile(SSabInfo.Abigpic);
            

            DgvSongMusicAlbum.AutoGenerateColumns = false;
            DgvSongMusicAlbum.DataSource = DBSelectSongList.DBSelectSongListTopThree(SSabInfo.Aid);

        }

        public static int SongMusicAlbum = 0;
        /// <summary>
        /// 判断是否更新
        /// </summary>       
        private void DgvSongMusicAlbum_DoubleClick(object sender, EventArgs e)
        {
            SongMusicAlbum += 1;
            //单击获取对应歌单内歌曲编号
            PlayingMusicClass.Cd_id = DgvSongMusicAlbum.SelectedRows[0].Cells["clMusicid"].Value.ToString();
            //获取对歌曲的播放地址
            PlayingMusicClass.Pusicaddress = DgvSongMusicAlbum.SelectedRows[0].Cells["clMusicaddress"].Value.ToString();
            //专辑ID获取
            PlayingMusicClass.A_id = DgvSongMusicAlbum.SelectedRows[0].Cells["clAlbumid"].Value.ToString();
            //歌手姓名
            PlayingMusicClass.S_name = DgvSongMusicAlbum.SelectedRows[0].Cells["clMsinger"].Value.ToString();
            //转为空值防止重复
            PlayingMusicClass.Songone_id = "";
            //通过Aid获取的专辑ID来判断对应的专辑，然后在通过Cdid来判断当前音乐为歌单中
            //第几首音乐对此进行判断播放,而直接获取歌曲的播放路径是为了点击播放的时候可
            //以传参到主页面供播放器载入播放

        }

        public static int OccupyAlbum;
        /// <summary>
        /// 单击专辑图片进入详细界面
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MusicAlbumpic_Click(object sender, EventArgs e)
        {
            //专辑ID获取
            A_id = DgvSongMusicAlbum.SelectedRows[0].Cells["clAlbumid"].Value.ToString();
            //歌手姓名
            S_name = DgvSongMusicAlbum.SelectedRows[0].Cells["clMsinger"].Value.ToString();
            //歌单时间
            A_time = SSabInfo.Atime;
            //歌单大图
            A_bigpic = SSabInfo.Abigpic;
            //歌单名字
            Album_Name = SSabInfo.Aname;
            //已选中标签
            OccupyAlbum += 1;


        }
    }
}
