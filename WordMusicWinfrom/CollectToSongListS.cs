using BLL;
using Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WordMusicWinfrom
{
    public partial class CollectToSongListS : Form
    {
        public CollectToSongListS()
        {
            InitializeComponent();
            SetClassLong(this.Handle, GCL_STYLE, GetClassLong(this.Handle, GCL_STYLE) | CS_DropSHADOW); //API函数加载，实现窗体边框阴影效果

        }

        #region 窗体边框阴影效果变量申明

        const int CS_DropSHADOW = 0x20000;
        const int GCL_STYLE = (-26);
        //声明Win32 API
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int SetClassLong(IntPtr hwnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetClassLong(IntPtr hwnd, int nIndex);

        #endregion


        #region DGV样式

        /// <summary>
        /// DGV样式
        /// </summary>
        public void CmpSetDgv()
        {
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            this.dgvSongTable.AllowUserToAddRows = false;
            this.dgvSongTable.AllowUserToDeleteRows = false;
            dataGridViewCellStyle1.BackColor = System.Drawing.Color.LightCyan;
            this.dgvSongTable.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle1;
            //this.DgvListOfSongsList.BackgroundColor = System.Drawing.Color.White;
            this.dgvSongTable.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.dgvSongTable.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.Single;
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.BottomLeft;//211, 223, 240
            dataGridViewCellStyle2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(245)))), ((int)(((byte)(247)))));
            dataGridViewCellStyle2.Font = new System.Drawing.Font("微软雅黑", 10, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            this.dgvSongTable.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle2;
            this.dgvSongTable.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvSongTable.EnableHeadersVisualStyles = false;
            this.dgvSongTable.GridColor = System.Drawing.SystemColors.GradientInactiveCaption;
            this.dgvSongTable.ReadOnly = true;
            this.dgvSongTable.RowHeadersVisible = false;
            this.dgvSongTable.RowTemplate.Height = 28;
            this.dgvSongTable.RowTemplate.ReadOnly = true;
        }




        #endregion

        /// <summary>
        /// 窗体加载事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CollectToSongListS_Load(object sender, EventArgs e)
        {
            CmpSetDgv();
            dgvSongTable.AutoGenerateColumns = false;


            

            dgvSongTable.DataSource =  DBSelectSongListInffo.SelectUserMeSongListTableInfo(LoginInfo.UserID);

        }

        public static int Yes;
        public static int No;


        //双击将音乐存入歌单
        private void dgvSongTable_DoubleClick(object sender, EventArgs e)
        {
            //单击获取对应歌单内歌曲编号
            string Id = dgvSongTable.SelectedRows[0].Cells["clSongIdTables"].Value.ToString();

            string IdNum = DBSelectSongListInffo.SelectSingsongoneNumAll(Id).ToString();

            //SongMusicInfo
            #region 获取歌单音乐的信息
            SongMusicInfo s = new SongMusicInfo();
            //歌单ID
            s.SongId = Id;
            //歌单内单曲ID
            int i = 1;
            i += Convert.ToInt32(IdNum);
            s.SongoneId = i.ToString();
            //单曲专辑的ID
            s.Aid = SongListMainInterfacefrm.Aid;
            //专辑名字
            s.Aname = SongListMainInterfacefrm.Aname;
            //歌手名字
            s.Usinger = SongListMainInterfacefrm.Sname;
            //歌曲lrc文件
            s.Parlyric = SongListMainInterfacefrm.Parlrc;
            //歌曲路径
            s.Pusicaddress = SongListMainInterfacefrm.Maddress;
            //歌曲时间
            s.Songtime = SongListMainInterfacefrm.Songtime;
            //专辑内的ID
            s.Cdid = SongListMainInterfacefrm.Cdid;
            //歌曲的名字
            s.Cdname = SongListMainInterfacefrm.Cdname;
            #endregion

            //先影藏窗体
            this.FormBorderStyle = FormBorderStyle.None;
            this.Opacity = .0;

            int Num = Convert.ToInt32(DBSelectSongListInffo.SingSongYesNo(s.Aid, s.Cdid,Id));
            if (Num != 1)
            {
                DBSelectSongListInffo.InsertSpendInfo(s);
                Yes = 1;

                AddSuccessfulAndUnsuccessful asau = new AddSuccessfulAndUnsuccessful();
                asau.TopLevel = true;

                //获取当前窗体位置
                int x = this.Location.X;
                int y = this.Location.Y;


                ////定位位置
                Point p = new Point(x,y+90);
                asau.StartPosition = FormStartPosition.Manual;
                asau.Location = p;
                asau.ShowDialog();

                this.Close();



            }
            else
            {
                Yes = 0;

                AddSuccessfulAndUnsuccessful asau = new AddSuccessfulAndUnsuccessful();
                asau.TopLevel = true;

                //获取当前窗体位置
                int x = this.Location.X;
                int y = this.Location.Y;


                ////定位位置
                Point p = new Point(x,y+90);
                asau.StartPosition = FormStartPosition.Manual;
                asau.Location = p;
                asau.ShowDialog();

                this.Close();
            }

        }

        private void MusicClose_Click(object sender, EventArgs e)
        {

        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}

