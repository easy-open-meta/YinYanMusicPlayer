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
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WordMusicWinfrom
{
    public partial class SongListfrm : UserControl
    {
        DiscoverMusicFrm dmf = null;
        CollectionOfSongsFrm cosf = null;
        UserMainSongSheetNumFrm umsn = null;
        UserMainCollectionNumFrm umcn = null;

        //需要用到的方法
        //[DllImport("user32.dll")]
        //public static extern int ReleaseCapture();

        //[DllImport("user32.dll")]
        //public static extern int SendMessage(IntPtr hwnd, int wMsg, int wParam, int lParam);

        //public const int WM_SYSCOMMAND = 0x0112;
        //public const int SC_MOVE = 0xF010;
        //public const int HTCAPTION = 0x0002;

        public static int i;
        public SongListfrm(DiscoverMusicFrm dmf)
        {
            InitializeComponent();
            this.dmf = dmf;
             i = 0;
        }

        public SongListfrm(UserMainSongSheetNumFrm umsn)
        {
            InitializeComponent();
            this.umsn = umsn;
             i = 1;
        }

        public SongListfrm(UserMainCollectionNumFrm umcn)
        {
            InitializeComponent();
            this.umcn = umcn;
             i = 0;
        }

        public SongListfrm(CollectionOfSongsFrm cosf)
        {
            InitializeComponent();
            this.cosf = cosf;
             i = 0;
        }

        public static string U_id;
        public static string U_name;
        public static string Par_id;
        public static int Song_Id;
        public static string Song_Name;
        public static int Song_Collection;
        public static int Song_SingU;
        public static string Song_Info;
        public static DateTime Song_Time;
        public static string Song_Pic;


        public UserSongListInfo USInfomin = new UserSongListInfo();

        private void SongListfrm_Load(object sender, EventArgs e)
        {
            //调用两个信息出来赋值
            SongLN.Text = USInfomin.SongName;
            //this.SongLP.BackgroundImage = Image.FromFile(USInfomin.SongPic);
            this.SongLP.SizeMode = PictureBoxSizeMode.Zoom;
            int hot = USInfomin.SongCollection;
            HotInfotxt.Text = USInfomin.SongCollection.ToString()+"°";
            if (hot > 1000)
            {
                HotInfotxt.ForeColor = Color.Red;
            }
        }

        /// <summary>
        /// 双击图片显示歌单信息
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SongLP_DoubleClick(object sender, EventArgs e)
        {

        }

        /// <summary>
        /// 查看当前歌单
        /// </summary>
        public void playSongLpClick()
        {
            //已选中标签
            OccupySong += 1;

            U_id = USInfomin.Uid;
            U_name = USInfomin.Uname;
            Par_id = USInfomin.ParId;
            Song_Id = USInfomin.SongId;
            Song_Name = USInfomin.SongName;
            Song_Collection = USInfomin.SongCollection;
            Song_SingU = USInfomin.SongSingU;
            Song_Info = USInfomin.SongInfo;
            Song_Time = USInfomin.SongTime;
            Song_Pic = USInfomin.SongPic;
        }

        public static int OccupySong;
        /// <summary>
        /// 单击图片显示歌单信息
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SongLP_Click(object sender, EventArgs e)
        {
            playSongLpClick();
        }

        /// <summary>
        /// 右键查看歌单
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tmCheckSongList_Click(object sender, EventArgs e)
        {
            playSongLpClick();
        }

        public static int Yes;
        /// <summary>
        /// 收藏歌单
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tmCollectingSongs_Click(object sender, EventArgs e)
        {
            if (LoginInfo.UserID == "")
            {   
                 Form frm = Application.OpenForms["UserLRFrm"];  //查找是否打开过UserLRFrm窗体
                 if (frm == null)  //没打开过
                   {
                        UserLRFrm UL = new UserLRFrm();

                    //获取当前窗体位置
                    int x = this.Location.X;
                    int y = this.Location.Y;


                    ////定位位置
                    Point p = new Point(527,194);
                    UL.StartPosition = FormStartPosition.Manual;
                    UL.Location = p;

                    UL.Show();   //重新new一个Show出来
                   }
                 else
                   {
                        frm.Focus();   //打开过就让其获得焦点
                   }            
            }
            else
            {
              int i = Convert.ToInt32(DBSelectSongListInffo.UserYesNoSingSongTable(LoginInfo.UserID, Convert.ToString(USInfomin.SongId)));
                if (i == 0 && LoginInfo.UserID != USInfomin.Uid)
                {
                    Yes = 1;
                    UserCollectionSongSheet u = new UserCollectionSongSheet();
                    u.Userid = LoginInfo.UserID;
                    u.Songid = USInfomin.SongId.ToString();
                    DBSelectSongListInffo.InsertUserSongSingtable(u);

                    DBSelectSongListInffo.UpdateUserSongListCollection(USInfomin.SongCollection++, USInfomin.SongId.ToString());

                    AddUserSingSong auss = new AddUserSingSong();
                    auss.TopLevel = true;

                    HotInfotxt.Text = USInfomin.SongCollection.ToString() + "°";
                    if (USInfomin.SongCollection > 1000)
                    {
                        HotInfotxt.ForeColor = Color.Red;
                    }

                    auss.StartPosition = FormStartPosition.CenterScreen;
                    auss.ShowDialog();

                    


                }
                else if (LoginInfo.UserID == USInfomin.Uid)
                {
                    Yes = 2;
                    AddUserSingSong auss = new AddUserSingSong();
                    auss.TopLevel = true;
 
                    auss.StartPosition = FormStartPosition.CenterScreen;
                    auss.ShowDialog();
                }
                else
                {
                    Yes = 0;

                    AddUserSingSong auss = new AddUserSingSong();
                    auss.TopLevel = true;

                    auss.StartPosition = FormStartPosition.CenterScreen;
                    auss.ShowDialog();

                }
            }
        }

        public static int EditSongListInfor;
        /// <summary>
        /// 如果这是你的歌单编辑歌单相信
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tmEditSongListInformation_Click(object sender, EventArgs e)
        {
            EditSongListInfor++;

            U_id = USInfomin.Uid;
            U_name = USInfomin.Uname;
            Par_id = USInfomin.ParId;
            Song_Id = USInfomin.SongId;
            Song_Name = USInfomin.SongName;
            Song_Collection = USInfomin.SongCollection;
            Song_SingU = USInfomin.SongSingU;
            Song_Info = USInfomin.SongInfo;
            Song_Time = USInfomin.SongTime;
            Song_Pic = USInfomin.SongPic;
        }
        //判断
        public static int DeleteYN = 0;
        /// <summary>
        /// 删除歌单
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DeleteSongList_Click(object sender, EventArgs e)
        {

            DeleteYesNofrm dyn = new DeleteYesNofrm();
            dyn.TopLevel = true;
            dyn.StartPosition = FormStartPosition.CenterScreen;
            dyn.ShowDialog();

            if (DeleteYesNofrm.Yes == 1)
            {

                try
                {
                    string sql = "delete from user_song_list where song_id = '" + USInfomin.SongId + "'";
                    MySqlConnection con = DBHelper.GetConnection();
                    int n = DBHelper.ExecuteNonQuery(sql);
                    string sqls = "delete from song_music where song_id = '" + USInfomin.SongId + "'";
                    MySqlConnection cons = DBHelper.GetConnection();
                    int m = DBHelper.ExecuteNonQuery(sqls);
                    string sqlss = "delete from user_collection_song_sheet where song_id = '" + USInfomin.SongId + "'";
                    MySqlConnection conss = DBHelper.GetConnection();
                    int s = DBHelper.ExecuteNonQuery(sqls);


                    
                    DeleteYN += 1;

                }
                catch
                {
                    
                }


            }
            else
            {
                
            }
            

        }

        /// <summary>
        /// 打开右键时候判断是否是你的歌单
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RightClickMenu_Opening(object sender, CancelEventArgs e)
        {
            SongLP.ContextMenuStrip = RightClickMenu;
            if (USInfomin.Uid == LoginInfo.UserID && i == 1)
            {
                Line1.Visible = true;
                tmEditSongListInformation.Visible = true;
                DeleteSongList.Visible = true;
            }
            else
            {
                Line1.Visible = false;
                tmEditSongListInformation.Visible = false;
                DeleteSongList.Visible = false;
            }
        }


        /// <summary>
        /// 不安全的透明方法
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        //private unsafe GraphicsPath NoteGraphicsPath(Image image)
        //{
        //    if (image == null)
        //        return null;

        //    // 声明GraphicsPath类以便计算位图路径
        //    GraphicsPath graphicsPath = new GraphicsPath(FillMode.Alternate);
        //    Bitmap bitmap = new Bitmap(image);

        //    int picWidth = bitmap.Width;
        //    int picHeight = bitmap.Height;

        //    BitmapData bitmapdata = bitmap.LockBits(new Rectangle(0, 0, picWidth, picHeight), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);

        //    byte* point = (byte*)bitmapdata.Scan0;
        //    int offset = bitmapdata.Stride - picWidth * 3;
        //    int p0, p1, p2;
        //    p0 = point[0];
        //    p1 = point[1];
        //    p2 = point[2];
        //    int start = -1;

        //    for (int h = 0; h < picHeight; h++)
        //    {
        //        for (int x = 0; x < picWidth; x++)
        //        {
        //            // 如果之前的点没有不透明且不透明   
        //            if (start == -1 && (point[0] != p0 || point[1] != p1 || point[2] != p2))
        //            {
        //                start = x;
        //            }
        //            else if (start > -1 && (point[0] == p0 && point[1] == p1 && point[2] == p2))
        //            {
        //                // 如果之前的点是不透明
        //                graphicsPath.AddRectangle(new Rectangle(start, h, x - start - 1, 1));
        //                start = -1;
        //            }

        //            // 如果之前的点是不透明且是最后一个点  
        //            if (x == picWidth - 1 && start > -1)
        //            {
        //                graphicsPath.AddRectangle(new Rectangle(start, h, x - start + 1, 1));
        //                start = -1;
        //            }

        //            point += 3;
        //        }

        //        point += offset;
        //    }

        //    bitmap.UnlockBits(bitmapdata);
        //    bitmap.Dispose();

        //    return graphicsPath;
        //}


        /// <summary>
        /// 需要设置透明效果的控件调用该方法
        /// </summary>
        /// <param name="control">要设置透明效果的控件</param>
        /// <param name="image">控件的图片</param>
        //public void SetPictureBoxTransparent(Control control, Image image)
        //{
        //    GraphicsPath graphic = null;
        //    graphic = NoteGraphicsPath(image);

        //    if (graphic == null)
        //        return;

        //    control.Region = new Region(graphic);
        //}

        /// <summary>
        /// 定义前景图
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        //private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
        //{
        //    SetPictureBoxTransparent(pic前景图, pic前景图.Image);
        //    //SetPictureBoxTransparent(picModelImage, picModelImage.Image);
        //    ReleaseCapture();
        //    SendMessage(this.pic前景图.Handle, WM_SYSCOMMAND, SC_MOVE + HTCAPTION, 0);
        //}
    }
}
