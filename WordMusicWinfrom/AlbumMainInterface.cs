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
    public partial class AlbumMainInterface : Form
    {
        public AlbumMainInterface()
        {
            InitializeComponent();
        }

        #region DGV样式

        /// <summary>
        /// DGV样式
        /// </summary>
        public void CmpSetDgv()
        {
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            this.AlbumMaindgv.AllowUserToAddRows = false;
            this.AlbumMaindgv.AllowUserToDeleteRows = false;
            dataGridViewCellStyle1.BackColor = System.Drawing.Color.LightCyan;
            this.AlbumMaindgv.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle1;
            //this.DgvListOfSongsList.BackgroundColor = System.Drawing.Color.White;
            this.AlbumMaindgv.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.AlbumMaindgv.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.Single;
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.BottomLeft;//211, 223, 240
            dataGridViewCellStyle2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(245)))), ((int)(((byte)(247)))));
            dataGridViewCellStyle2.Font = new System.Drawing.Font("微软雅黑", 10, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            this.AlbumMaindgv.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle2;
            this.AlbumMaindgv.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.AlbumMaindgv.EnableHeadersVisualStyles = false;
            this.AlbumMaindgv.GridColor = System.Drawing.SystemColors.GradientInactiveCaption;
            this.AlbumMaindgv.ReadOnly = true;
            this.AlbumMaindgv.RowHeadersVisible = false;
            this.AlbumMaindgv.RowTemplate.Height = 28;
            this.AlbumMaindgv.RowTemplate.ReadOnly = true;
        }

        #region 窗体自绘编号RowPostPaint
        /// <summary>
        /// 窗体自绘编号RowPostPaint
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AlbumMaindgv_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            //自动编号，与数据无关
            Rectangle rectangle = new Rectangle(e.RowBounds.Location.X,
               e.RowBounds.Location.Y,
               AlbumMaindgv.RowHeadersWidth - 4,
               e.RowBounds.Height);

            if (e.RowIndex < 9)
            {
                TextRenderer.DrawText(e.Graphics,
                      "0" + (e.RowIndex + 1).ToString(),
                       AlbumMaindgv.RowHeadersDefaultCellStyle.Font,
                       rectangle,
                       AlbumMaindgv.RowHeadersDefaultCellStyle.ForeColor,
                       TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
            }
            else
            {
                TextRenderer.DrawText(e.Graphics,
                       (e.RowIndex + 1).ToString(),
                       AlbumMaindgv.RowHeadersDefaultCellStyle.Font,
                       rectangle,
                       AlbumMaindgv.RowHeadersDefaultCellStyle.ForeColor,
                       TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
            }
        }
        #endregion

        #endregion


        private void AlbumMainInterface_Load(object sender, EventArgs e)
        {

            CmpSetDgv();
            //专辑名字
            AlbumNametxt.Text = SongMusicAlbumFrm.Album_Name;
            //歌单时间
            AlbumTimertxt.Text = SongMusicAlbumFrm.A_time+"  创建";
            //专辑头像
            AlbumHeadpic.BackgroundImage = Image.FromFile(SongMusicAlbumFrm.A_bigpic);
            //歌手名字
            SingNametxt.Text = SongMusicAlbumFrm.S_name;

            //查看当前歌单歌曲数目
            SingNumbertxt.Text = DBSelectSingerInfo.DBSelectSingAll(SongMusicAlbumFrm.A_id).ToString();

            AlbumMaindgv.AutoGenerateColumns = false;
            //查询语句
            AlbumMaindgv.DataSource = DBSelectSongList.DBSelectSongListAlbum(SongMusicAlbumFrm.A_id);
        }


        public static int AlbumMain;
        //播放包装
        public void Music()
        {
            AlbumMain += 1;
            //单击获取对应歌单内歌曲编号
            PlayingMusicClass.Cd_id = AlbumMaindgv.SelectedRows[0].Cells["clMusicid"].Value.ToString();
            //获取对歌曲的播放地址
            PlayingMusicClass.Pusicaddress = AlbumMaindgv.SelectedRows[0].Cells["clMusicaddress"].Value.ToString();
            //专辑ID获取
            PlayingMusicClass.A_id = AlbumMaindgv.SelectedRows[0].Cells["clAlbumid"].Value.ToString();
            //歌手姓名
            PlayingMusicClass.S_name = AlbumMaindgv.SelectedRows[0].Cells["clMsinger"].Value.ToString();
            //转为空值防止重复
            PlayingMusicClass.Songone_id = "";
            //通过Aid获取的专辑ID来判断对应的专辑，然后在通过Cdid来判断当前音乐为歌单中
            //第几首音乐对此进行判断播放,而直接获取歌曲的播放路径是为了点击播放的时候可
            //以传参到主页面供播放器载入播放
        }
        private void AlbumMaindgv_DoubleClick(object sender, EventArgs e)
        {
            Music();
        }
    }
}
