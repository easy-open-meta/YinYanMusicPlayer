using BLL;
using DAL;
using Model;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WordMusicWinfrom
{
    public partial class SongListMainInterfacefrm : Form
    {
        public SongListMainInterfacefrm()
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

        /// <summary>
        /// 窗体加载事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SongListMainInterfacefrm_Load(object sender, EventArgs e)
        {
            AlbumMaindgv.AutoGenerateColumns = false;

            CmpSetDgv();
            //获取图片
            SingerPC.BackgroundImage = Image.FromFile(SongListfrm.Song_Pic);
            //获取名字
            SongSheetNametxt.Text = SongListfrm.Song_Name;
            //用户名字
            UserNametxt.Text = SongListfrm.U_name;
                if (SongListfrm.Par_id != "")
                {
                    //歌单标签
                    ParName.Text = DBSelectSongListInffo.SelectPartitionName(SongListfrm.Par_id).ToString();
                }
                else
                {
                    ParName.Text = "暂无标签";
                }
            ////用户头像
            //UserPic.BackgroundImage = Image.FromFile(SongListfrm);
            //创建时间
            EstablishTimetxt.Text = SongListfrm.Song_Time.ToString()+"  创建";
            //歌单简介
            BriefIntroductiontxt.Text = SongListfrm.Song_Info;
            //歌单热度
            HotInfotxt.Text = SongListfrm.Song_Collection.ToString();
            //歌曲数量
            NumberOfSongstxt.Text = DBUserAndFriendInfo.SelectUSongNum(SongListfrm.Song_Id.ToString()).ToString();


            AlbumMaindgv.AutoGenerateColumns = false;
            AlbumMaindgv.DataSource = DBSelectSongListInffo.SelectUserSongInfo(SongListfrm.Song_Id.ToString());

           

        }

        //点击变量
        public static int AlbumMain;
        public static int Num;
        //播放包装
        public void Music()
        {
            AlbumMain += 1;

            Num = Convert.ToInt32(AlbumMaindgv.CurrentRow.Index+1);

            //单击获取对应歌单内歌曲编号
            PlayingMusicClass.Songone_id = AlbumMaindgv.SelectedRows[0].Cells["clSongoneid"].Value.ToString();
            //获取对歌曲的播放地址
            PlayingMusicClass.Pusicaddress = AlbumMaindgv.SelectedRows[0].Cells["clMusicaddress"].Value.ToString();
            //专辑ID获取(歌单特有的ID)
            PlayingMusicClass.A_id = AlbumMaindgv.SelectedRows[0].Cells["clAlbumid"].Value.ToString();
            //歌手姓名
            PlayingMusicClass.S_name = AlbumMaindgv.SelectedRows[0].Cells["clMsinger"].Value.ToString();
            //转为空值防止重复
            PlayingMusicClass.Cd_id = "";

            //通过Aid获取的专辑ID来判断对应的专辑，然后在通过Cdid来判断当前音乐为歌单中
            //第几首音乐对此进行判断播放,而直接获取歌曲的播放路径是为了点击播放的时候可
            //以传参到主页面供播放器载入播放
        }

        /// <summary>
        /// 双击播放歌曲
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AlbumMaindgv_DoubleClick(object sender, EventArgs e)
        {
            Music();
        }

        public static string Aid;
        public static string Aname;
        public static string Sname;
        public static string Parlrc;
        public static string Maddress;
        public static string Songtime;
        public static string Cdid;
        public static string Cdname;

        public static int i;

        /// <summary>
        /// 歌曲信息储存
        /// </summary>
        public void UserSongInfo()
        {
            Aid = AlbumMaindgv.SelectedRows[0].Cells["clAlbumid"].Value.ToString();
            Aname = AlbumMaindgv.SelectedRows[0].Cells["clAname"].Value.ToString();
            Sname = AlbumMaindgv.SelectedRows[0].Cells["clMsinger"].Value.ToString();
            Parlrc = AlbumMaindgv.SelectedRows[0].Cells["clParlyric"].Value.ToString();
            Maddress = AlbumMaindgv.SelectedRows[0].Cells["clMusicaddress"].Value.ToString();
            Songtime = AlbumMaindgv.SelectedRows[0].Cells["clSongtime"].Value.ToString();
            Cdid = AlbumMaindgv.SelectedRows[0].Cells["clMusicid"].Value.ToString();
            Cdname = AlbumMaindgv.SelectedRows[0].Cells["clCdname"].Value.ToString();
        }

        /// <summary>
        /// 收藏到歌单里
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CollectToSongList_Click(object sender, EventArgs e)
        {
            UserSongInfo();
            CollectToSongListS ctl = new CollectToSongListS();
            ctl.TopLevel = true;



            //获取当前窗体位置
            int x = this.Location.X;
            int y = this.Location.Y;


            ////定位位置
            Point p = new Point(x + 615, y + 227);
            ctl.StartPosition = FormStartPosition.Manual;
            ctl.Location = p;
            ctl.ShowDialog();

        }
        /// <summary>
        /// 判断用户是否登录了
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tmrRCFSL_Tick(object sender, EventArgs e)
        {
            if (LoginInfo.UserID != "")
            {
                AlbumMaindgv.ContextMenuStrip = RightClickForSongList;
                if (SongListfrm.U_id == LoginInfo.UserID)
                {
                    Line2.Visible = true;
                    RemoveFromPlaylist.Visible = true;
                }
                else
                {
                    Line2.Visible = false;
                    RemoveFromPlaylist.Visible = false;
                }
            }
            else
            {
                AlbumMaindgv.ContextMenuStrip = null;
            }
        }

        /// <summary>
        /// 删除当前歌曲
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RemoveFromPlaylist_Click(object sender, EventArgs e)
        {
            string sql = "delete from song_music where song_id = '" + SongListfrm.Song_Id + "' and a_id = '"+AlbumMaindgv.SelectedRows[0].Cells["clAlbumid"].Value.ToString()+"' and cd_id='" + AlbumMaindgv.SelectedRows[0].Cells["clMusicid"].Value.ToString() + "'";
            MySqlConnection con = DBHelper.GetConnection();
            int n = DBHelper.ExecuteNonQuery(sql);

            AlbumMaindgv.AutoGenerateColumns = false;
            AlbumMaindgv.DataSource = DBSelectSongListInffo.SelectUserSongInfo(SongListfrm.Song_Id.ToString());
        }

        /// <summary>
        /// 播放
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Player_Click(object sender, EventArgs e)
        {
            Music();
        }

        private void UserNametxt_Click(object sender, EventArgs e)
        {

        }

        /// <summary>
        /// 新建歌单保存
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NewSongListCMS_Click(object sender, EventArgs e)
        {
            UpNewSongListFrm asau = new UpNewSongListFrm();
            asau.TopLevel = true;

            //获取当前窗体位置
            int x = this.Location.X;
            int y = this.Location.Y;


            ////定位位置
            Point p = new Point(x + 642, y + 347);
            asau.StartPosition = FormStartPosition.Manual;
            asau.Location = p;
            asau.ShowDialog();
        }
    }
}
