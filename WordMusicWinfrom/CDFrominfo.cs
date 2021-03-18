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
    public partial class CDFrominfo : Form
    {
        public CDFrominfo()
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

        #region 窗体自绘编号RowPostPaint
        /// <summary>
        /// 窗体自绘编号RowPostPaint
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DgvListOfSongsList_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            //自动编号，与数据无关
            Rectangle rectangle = new Rectangle(e.RowBounds.Location.X,
               e.RowBounds.Location.Y,
               DgvListOfSongsList.RowHeadersWidth - 4,
               e.RowBounds.Height);

            if (e.RowIndex < 9)
            {
                TextRenderer.DrawText(e.Graphics,
                      "0" + (e.RowIndex + 1).ToString(),
                       DgvListOfSongsList.RowHeadersDefaultCellStyle.Font,
                       rectangle,
                       DgvListOfSongsList.RowHeadersDefaultCellStyle.ForeColor,
                       TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
            }
            else
            {
                TextRenderer.DrawText(e.Graphics,
                       (e.RowIndex + 1).ToString(),
                       DgvListOfSongsList.RowHeadersDefaultCellStyle.Font,
                       rectangle,
                       DgvListOfSongsList.RowHeadersDefaultCellStyle.ForeColor,
                       TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
            }
        }
        #endregion

        #region 窗体加载
        
        /// <summary>
        /// 窗体加载
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CDFrominfo_Load(object sender, EventArgs e)
        {
            CmpSetDgv();
            DgvListOfSongsList.AutoGenerateColumns = false;
            string Name = SongMusicfrm.S_name;
            DgvListOfSongsList.DataSource = DBSelectSongList.DBSelectSongListTopTen(Name);

        }
        #endregion

        #region 备注

        #endregion

        public static int ListOfSongsList;
        //播放包装
        public void Music()
        {
            ListOfSongsList += 1;
            //单击获取对应歌单内歌曲编号
            PlayingMusicClass.Cd_id = DgvListOfSongsList.SelectedRows[0].Cells["clMusicid"].Value.ToString();
            //获取对歌曲的播放地址
            PlayingMusicClass.Pusicaddress = DgvListOfSongsList.SelectedRows[0].Cells["clMusicaddress"].Value.ToString();
            //专辑ID获取
            PlayingMusicClass.A_id = DgvListOfSongsList.SelectedRows[0].Cells["clAlbumid"].Value.ToString();
            //歌手姓名
            PlayingMusicClass.S_name = DgvListOfSongsList.SelectedRows[0].Cells["clMsinger"].Value.ToString();
            //转为空值防止重复
            PlayingMusicClass.Songone_id = "";
            //通过Aid获取的专辑ID来判断对应的专辑，然后在通过Cdid来判断当前音乐为歌单中
            //第几首音乐对此进行判断播放,而直接获取歌曲的播放路径是为了点击播放的时候可
            //以传参到主页面供播放器载入播放
        }

        private void DgvListOfSongsList_DoubleClick(object sender, EventArgs e)
        {
            Music();
        }

        

        private void DgvListOfSongsList_Click(object sender, EventArgs e)
        {

        }
    }
}
