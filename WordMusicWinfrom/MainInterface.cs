using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Collections;
using BLL;
using Model;
using DAL;

namespace WordMusicWinfrom
{
    /// <summary>
    /// 音言主界面
    /// </summary>
    public partial class MainInterface : Form
    {
        /// <summary>
        /// 用户姓名存储
        /// </summary>
        public string NameInfo = null;

        delegate void set_Text(string s);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        protected static extern bool AnimateWindow(IntPtr hWnd, int dwTime, int dwFlags);
        public const Int32 AW_BLEND = 0x00080000;
        public const Int32 AW_CENTER = 0x00000010;
        public const Int32 AW_ACTIVATE = 0x00020000;
        public const Int32 AW_HIDE = 0x00010000;
        public const Int32 AW_SLIDE = 0x00040000;

        public MainInterface()
        {
            InitializeComponent();
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);

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

        #region 静态方法
        public static string U_id;
        public static string S_id;
        public static string S_name;
        public static int CD_u;
        public static int Album_u;
        public static int MV_u;
        public static string S_pic;
        public static string S_info;
        /// <summary>
        /// 占用 判断是否以点击歌手进入歌手界面
        /// </summary>
        public static int Occupy;

        public static int UserSongNum;

        /// <summary>       
        /// 判断是否选择音乐
        /// </summary>
        public static int SongMusicAlbum;
        public static int AlbumMain;
        public static int ListOfSongsList;
        public static int AlbumMains;
        public static int ListOfSongsListsm;
        public static int SeveUpMusic;
        public static int EditSongListInfor;


        /// <summary>
        /// 占用 判断是否选择歌单详细界面
        /// </summary>
        public static int OccupySong;
        /// <summary>
        /// 占用 判断是否选择专辑详细界面
        /// </summary>
        public static int OccupyAlbum;
        /// <summary>
        /// 判读用户登录信息
        /// </summary>
        public static int UserLoginNum;


        #endregion

        /// <summary>
        /// 判断开关
        /// </summary>
        bool IsPlay = false;

        /// <summary>
        /// 播放开关
        /// </summary>
        Boolean Plays = false;

        #region 窗体加载
        /// <summary>
        /// 窗体加载
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainInterface_Load(object sender, EventArgs e)
        {
            //判断用户名字自如果大于5则只获取前位并加...
            if (UserName.Text.Length > 5)
            {
                NameInfo = UserName.Text.Substring(0, 5) + "...";
                UserName.Text = NameInfo;
            }           
            Publicnull();
            this.DiscoverMusic.BackgroundImage = Image.FromFile(@"IMG/Clickimg.png");

            //设置初始音量40
            this.MusicMainPlayer.settings.volume = 40;

            DiscoverMusicFrm dmf = new DiscoverMusicFrm();
            dmf.TopLevel = false;
            MusicMainInterface.Controls.Add(dmf);
            dmf.Show();
        }
        #endregion

        #region 设置声音大小
        /// <summary>
        /// 声音
        /// </summary>
        int voice;
        int musicNum = 0;

        //设置声音大小
        public void setVoice(int voice)
        {
            this.MusicMainPlayer.settings.volume = voice;
        }
        #endregion

        #region 声音和进度
        int panleX;//获取当前panle的X
        //最大可调节音量
        private void MaximumVolume_MouseDown(object sender, MouseEventArgs e)
        {
            voice = e.Location.X;
            setVoice(voice);
            AdjustableVolume.Size = new Size(e.Location.X, 3);
        }

        private void MaximumVolume_MouseEnter(object sender, EventArgs e)
        {
            this.Cursor = System.Windows.Forms.Cursors.Hand;
        }

        private void MaximumVolume_MouseLeave(object sender, EventArgs e)
        {
            this.Cursor = System.Windows.Forms.Cursors.Default;
        }

        //可调节音量
        private void AdjustableVolume_MouseDown(object sender, MouseEventArgs e)
        {
            voice = e.Location.X;
            AdjustableVolume.Size = new Size(e.Location.X, 3);
            setVoice(voice);
        }

        private void AdjustableVolume_MouseEnter(object sender, EventArgs e)
        {
            this.Cursor = System.Windows.Forms.Cursors.Hand;
        }

        private void AdjustableVolume_MouseLeave(object sender, EventArgs e)
        {
            this.Cursor = System.Windows.Forms.Cursors.Default;
        }


        //歌曲进度
        private void pmusicdown_MouseDown(object sender, MouseEventArgs e)
        {
            pmusicup.Size = new Size(e.Location.X, 3);
            panleX = e.Location.X;
            changeTime(410, panleX);
        }

        private void pmusicdown_MouseEnter(object sender, EventArgs e)
        {
            this.Cursor = System.Windows.Forms.Cursors.Hand;
        }

        private void pmusicdown_MouseLeave(object sender, EventArgs e)
        {
            this.Cursor = System.Windows.Forms.Cursors.Default;
        }

        private void pmusicup_MouseDown(object sender, MouseEventArgs e)
        {
            pmusicup.Size = new Size(e.Location.X, 3);
            panleX = e.Location.X;
            changeTime(410, panleX);
        }

        private void pmusicup_MouseEnter(object sender, EventArgs e)
        {
            this.Cursor = System.Windows.Forms.Cursors.Hand;
        }

        private void pmusicup_MouseLeave(object sender, EventArgs e)
        {
            this.Cursor = System.Windows.Forms.Cursors.Default;
        }



        #endregion

        #region 播放
        /// <summary>
        /// 播放
        /// </summary>
        /// <param name="namepath"></param>
        public void play(string namepath)
        {
            this.MusicMainPlayer.URL = namepath;
            SongName.Text = this.MusicMainPlayer.currentMedia.name;
            IsPlay = true;
            Plays = true;
            SongTimeProgress.Enabled = true;
            getmusicTime();
            if (Plays == true)
            {
                //切换暂停播放的图片
                Suspend.Image = Image.FromFile(@"IMG/Suspendimg.png");
            }
        }
        #endregion

        #region 存储方法和读取方法
        /// <summary>
        /// 获取歌曲路径集合
        /// </summary>
        string[] names;
        List<string> list;


        //存储方法
        public void SaveMusicList()
        {
            if (File.Exists(".\\Music.lst") == true)
            {
                File.Delete(".\\Music.lst");
            }
            SaveFileDialog sf = new SaveFileDialog();
            sf.FileName = "Music.lst";
            sf.RestoreDirectory = true;
            sf.FilterIndex = 1;
            FileStream fs = new FileStream(string.Format("{0}", sf.FileName), FileMode.Create);
            BinaryFormatter bf = new BinaryFormatter();
            bf.Serialize(fs, list);
            fs.Close();
            musicNum = names.Length - 1;
            play(names[musicNum]);
        }

        //读取方法
        public void GetMusicList()
        {
            string[] musicFile;
            if (File.Exists(".\\Music.lst") == false)
            {
            }
            else
            {
                OpenFileDialog of = new OpenFileDialog();
                of.FileName = "Music.lst";
                of.RestoreDirectory = true;
                of.FilterIndex = 1;
                FileStream fs = new FileStream(string.Format("{0}", of.FileName), FileMode.Open);
                BinaryFormatter bf = new BinaryFormatter();
                this.list = ((List<string>)bf.Deserialize(fs));
                fs.Close();
                musicFile = new string[list.Count];
                for (int i = 0; i < list.Count; i++)
                {
                    musicFile[i] = list[i];
                }
                names = musicFile;

            }
        }

        #endregion

        #region 搜索提示消失
        /// <summary>
        /// 搜索消失
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Search_TextChanged(object sender, EventArgs e)
        {
            if (sender.Equals(Search))
            {
                SearchIndex.Visible = Search.Text.Length < 1;
            }
        }
        #endregion

        #region 用户名称大于5时用省略代替
        /// <summary>
        /// 用户名称大于5时用省略代替
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserName_TextChanged(object sender, EventArgs e)
        {
            if (UserName.Text.Length > 5)
            {
                NameInfo = UserName.Text.Substring(0, 5) + "...";
                UserName.Text = NameInfo;
            }
        }
        #endregion

        #region 左侧小菜单变色滑动效果
        private void DiscoverMusic_MouseMove(object sender, MouseEventArgs e)
        {
            this.DiscoverMusicText.ForeColor = Color.Black;
            DiscoverMusicpic.Image = Image.FromFile(@"IMG/musicimgclick.png");
        }
        private void DiscoverMusic_MouseLeave(object sender, EventArgs e)
        {
            if (DiscoverMusic.BackgroundImage == null)
            {
                this.DiscoverMusicText.ForeColor = Color.FromArgb(92, 92, 92);
                DiscoverMusicpic.Image = Image.FromFile(@"IMG/musicimg.png");
            }
        }

        private void PrivateFM_MouseMove(object sender, MouseEventArgs e)
        {
            this.PrivateFMText.ForeColor = Color.Black;
            PrivateFMpic.Image = Image.FromFile(@"IMG/PrivateFMimgclick.png");
        }

        private void PrivateFM_MouseLeave(object sender, EventArgs e)
        {
            if (PrivateFM.BackgroundImage == null)
            {
                this.PrivateFMText.ForeColor = Color.FromArgb(92, 92, 92);
                PrivateFMpic.Image = Image.FromFile(@"IMG/PrivateFMimg.png");
            }
        }

        private void RecomeDaily_MouseMove(object sender, MouseEventArgs e)
        {
            this.RecomeDailyText.ForeColor = Color.Black;
            RecomeDailypic.Image = Image.FromFile(@"IMG/RecomeDailyimgclick.png");
        }

        private void RecomeDaily_MouseLeave(object sender, EventArgs e)
        {
            if (RecomeDaily.BackgroundImage == null)
            {
                this.RecomeDailyText.ForeColor = Color.FromArgb(92, 92, 92);
                RecomeDailypic.Image = Image.FromFile(@"IMG/RecomeDailyimg.png");
            }
        }

        private void SongSheet_MouseMove(object sender, MouseEventArgs e)
        {
            this.SongSheetText.ForeColor = Color.Black;
            SongSheetpic.Image = Image.FromFile(@"IMG/SongSheetimgclick.png");
        }

        private void SongSheet_MouseLeave(object sender, EventArgs e)
        {
            if (SongSheet.BackgroundImage == null)
            {
                this.SongSheetText.ForeColor = Color.FromArgb(92, 92, 92);
                SongSheetpic.Image = Image.FromFile(@"IMG/SongSheetimg.png");
            }
        }

        private void Singer_MouseMove(object sender, MouseEventArgs e)
        {
            this.SingerText.ForeColor = Color.Black;
            Singerpic.Image = Image.FromFile(@"IMG/Singerimgclick.png");
        }

        private void Singer_MouseLeave(object sender, EventArgs e)
        {
            if (Singer.BackgroundImage == null)
            {
                this.SingerText.ForeColor = Color.FromArgb(92, 92, 92);
                Singerpic.Image = Image.FromFile(@"IMG/Singerimg.png");
            }
        }

        private void PersonalSongList_MouseMove_1(object sender, MouseEventArgs e)
        {
            this.PersonalSongListText.ForeColor = Color.Black;
            PersonalSongListpic.Image = Image.FromFile(@"IMG/PersonalSongListimgclick.png");
        }

        private void Singer_MouseLeave_1(object sender, EventArgs e)
        {
            if (Singer.BackgroundImage == null)
            {
                this.SingerText.ForeColor = Color.FromArgb(92, 92, 92);
                Singerpic.Image = Image.FromFile(@"IMG/Singerimg.png");
            }
        }
        private void LocalMusic_MouseMove(object sender, MouseEventArgs e)
        {
            this.LocalMusicText.ForeColor = Color.Black;
            LocalMusicpic.Image = Image.FromFile(@"IMG/LocalMusicimgclick.png");
        }

        private void LocalMusic_MouseLeave(object sender, EventArgs e)
        {
            if (LocalMusic.BackgroundImage == null)
            {
                this.LocalMusicText.ForeColor = Color.FromArgb(92, 92, 92);
                LocalMusicpic.Image = Image.FromFile(@"IMG/LocalMusicimg.png");
            }
        }

        private void DownLoadAdmin_MouseMove(object sender, MouseEventArgs e)
        {
            this.DownLoadAdminText.ForeColor = Color.Black;
            DownLoadAdminpic.Image = Image.FromFile(@"IMG/DownLoadAdminimgclick.png");
        }

        private void DownLoadAdmin_MouseLeave(object sender, EventArgs e)
        {
            if (DownLoadAdmin.BackgroundImage == null)
            {
                this.DownLoadAdminText.ForeColor = Color.FromArgb(92, 92, 92);
                DownLoadAdminpic.Image = Image.FromFile(@"IMG/DownLoadAdminimg.png");
            }
        }

        private void PersonalSearch_MouseMove(object sender, MouseEventArgs e)
        {
            this.PersonalSearchText.ForeColor = Color.Black;
            PersonalSearchpic.Image = Image.FromFile(@"IMG/PersonalSearchimgclick.png");
        }

        private void PersonalSearch_MouseLeave(object sender, EventArgs e)
        {
            if (PersonalSearch.BackgroundImage == null)
            {
                this.PersonalSearchText.ForeColor = Color.FromArgb(92, 92, 92);
                PersonalSearchpic.Image = Image.FromFile(@"IMG/PersonalSearchimg.png");
            }
        }

        private void Singer_MouseMove_1(object sender, MouseEventArgs e)
        {
            this.SingerText.ForeColor = Color.Black;
            Singerpic.Image = Image.FromFile(@"IMG/Singerimgclick.png");
        }

        private void PersonalSongList_MouseLeave_1(object sender, EventArgs e)
        {
            if (PersonalSongList.BackgroundImage == null)
            {
                this.PersonalSongListText.ForeColor = Color.FromArgb(92, 92, 92);
                PersonalSongListpic.Image = Image.FromFile(@"IMG/PersonalSongListimg.png");
            }
        }
        private void PersonalSongList_MouseMove(object sender, MouseEventArgs e)
        {
            this.PersonalSongListText.ForeColor = Color.Black;
            PersonalSongListpic.Image = Image.FromFile(@"IMG/PersonalSongListimgclick.png");
        }

        private void PersonalSongList_MouseLeave(object sender, EventArgs e)
        {
            if (PersonalSongList.BackgroundImage == null)
            {
                this.PersonalSongListText.ForeColor = Color.FromArgb(92, 92, 92);
                PersonalSongListpic.Image = Image.FromFile(@"IMG/PersonalSongListimg.png");
            }
        }

        #endregion

        #region 左侧导航栏点击效果
        /// <summary>
        /// 返回主页
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DiscoverMusic_Click(object sender, EventArgs e)
        {
            Publicnull();
            this.DiscoverMusic.BackgroundImage = Image.FromFile(@"IMG/Clickimg.png");

            //删除所有控件在生成
            MusicMainInterface.Controls.Clear();
            DiscoverMusicFrm dmf = new DiscoverMusicFrm();
            dmf.TopLevel = false;
            MusicMainInterface.Controls.Add(dmf);
            dmf.Show();
        }

        private void PrivateFM_Click(object sender, EventArgs e)
        {
            Publicnull();
            this.PrivateFM.BackgroundImage = Image.FromFile(@"IMG/Clickimg.png");
        }

        private void RecomeDailyText_Click(object sender, EventArgs e)
        {
            Publicnull();
            this.RecomeDaily.BackgroundImage = Image.FromFile(@"IMG/Clickimg.png");
        }

        /// <summary>
        /// 单击歌单界面
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SongSheet_Click(object sender, EventArgs e)
        {
            Publicnull();
            this.SongSheet.BackgroundImage = Image.FromFile(@"IMG/Clickimg.png");


            MusicMainInterface.Controls.Clear();
            CollectionOfSongsFrm cosf = new CollectionOfSongsFrm();
            cosf.TopLevel = false;
            MusicMainInterface.Controls.Add(cosf);
            cosf.Show();
        }

        /// <summary>
        /// 歌手界面点击事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Singer_Click(object sender, EventArgs e)
        {
            Publicnull();
            this.Singer.BackgroundImage = Image.FromFile(@"IMG/Clickimg.png");

            MusicMainInterface.Controls.Clear();
            SongSheetFrm ssf = new SongSheetFrm();
            ssf.TopLevel = false;
            MusicMainInterface.Controls.Add(ssf);
            ssf.Show();

        }

        /// <summary>
        /// 本地音乐点击事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LocalMusic_Click(object sender, EventArgs e)
        {
            Publicnull();
            this.LocalMusic.BackgroundImage = Image.FromFile(@"IMG/Clickimg.png");

            MusicMainInterface.Controls.Clear();
            SeveUserMusicfrm sum = new SeveUserMusicfrm();
            sum.TopLevel = false;
            MusicMainInterface.Controls.Add(sum);
            sum.Show();
        }

        private void DownLoadAdmin_Click(object sender, EventArgs e)
        {
            Publicnull();
            this.DownLoadAdmin.BackgroundImage = Image.FromFile(@"IMG/Clickimg.png");
        }

        public static int PLSearchSongList;
        /// <summary>
        /// 个人收藏
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PersonalSearch_Click(object sender, EventArgs e)
        {

            if (UserName.Text == "未登录")
            {
                Form frm = Application.OpenForms["UserLRFrm"];  //查找是否打开过UserLRFrm窗体
                if (frm == null)  //没打开过
                {
                    UserLRFrm UL = new UserLRFrm();

                    //获取当前窗体位置
                    int x = this.Location.X;
                    int y = this.Location.Y;


                    ////定位位置
                    Point p = new Point(x + 367, y + 144);
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

                Publicnull();
                this.PersonalSearch.BackgroundImage = Image.FromFile(@"IMG/Clickimg.png");
                PLSearchSongList = 1;

                MusicMainInterface.Controls.Clear();
                UserMainFrm umf = new UserMainFrm();
                umf.TopLevel = false;
                MusicMainInterface.Controls.Add(umf);
                umf.Show();
                PLSearchSongList = 0;
            }
        }

        /// <summary>
        /// 个人歌单
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PersonalSongList_Click(object sender, EventArgs e)
        {
            if (UserName.Text == "未登录")
            {
                Form frm = Application.OpenForms["UserLRFrm"];  //查找是否打开过UserLRFrm窗体
                if (frm == null)  //没打开过
                {
                    UserLRFrm UL = new UserLRFrm();

                    //获取当前窗体位置
                    int x = this.Location.X;
                    int y = this.Location.Y;


                    ////定位位置
                    Point p = new Point(x + 367, y + 144);
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
                Publicnull();
                this.PersonalSongList.BackgroundImage = Image.FromFile(@"IMG/Clickimg.png");
                PLSearchSongList = 2;
                MusicMainInterface.Controls.Clear();
                UserMainFrm umf = new UserMainFrm();
                umf.TopLevel = false;
                MusicMainInterface.Controls.Add(umf);
                umf.Show();
                PLSearchSongList = 0;
            }

        }
        #endregion

        #region 全部清空点击栏
        /// <summary>
        /// 全部清空点击栏框
        /// </summary>
        public void Publicnull()
        {
            DiscoverMusic.BackgroundImage = null;
            PrivateFM.BackgroundImage = null;
            RecomeDaily.BackgroundImage = null;
            SongSheet.BackgroundImage = null;
            Singer.BackgroundImage = null;
            LocalMusic.BackgroundImage = null;
            DownLoadAdmin.BackgroundImage = null;
            PersonalSearch.BackgroundImage = null;
            PersonalSongList.BackgroundImage = null;

            this.DiscoverMusicText.ForeColor = Color.FromArgb(92, 92, 92);
            this.PrivateFMText.ForeColor = Color.FromArgb(92, 92, 92);
            this.RecomeDailyText.ForeColor = Color.FromArgb(92, 92, 92);
            this.SongSheetText.ForeColor = Color.FromArgb(92, 92, 92);
            this.SingerText.ForeColor = Color.FromArgb(92, 92, 92);
            this.LocalMusicText.ForeColor = Color.FromArgb(92, 92, 92);
            this.DownLoadAdminText.ForeColor = Color.FromArgb(92, 92, 92);
            this.PersonalSearchText.ForeColor = Color.FromArgb(92, 92, 92);
            this.PersonalSongListText.ForeColor = Color.FromArgb(92, 92, 92);

            DiscoverMusicpic.Image = Image.FromFile(@"IMG/musicimg.png");
            PrivateFMpic.Image = Image.FromFile(@"IMG/PrivateFMimg.png");
            RecomeDailypic.Image = Image.FromFile(@"IMG/RecomeDailyimg.png");
            SongSheetpic.Image = Image.FromFile(@"IMG/SongSheetimg.png");
            Singerpic.Image = Image.FromFile(@"IMG/Singerimg.png");
            LocalMusicpic.Image = Image.FromFile(@"IMG/LocalMusicimg.png");
            DownLoadAdminpic.Image = Image.FromFile(@"IMG/DownLoadAdminimg.png");
            PersonalSearchpic.Image = Image.FromFile(@"IMG/PersonalSearchimg.png");
            PersonalSongListpic.Image = Image.FromFile(@"IMG/PersonalSongListimg.png");
        }
        #endregion

        #region 移动不规则窗体

        #region 记录鼠标和窗体坐标的方法
        private Point mouseOld;//鼠标旧坐标
        private Point formOld;//窗体旧坐标 
        #endregion

        #region 记录移动窗体的坐标
        /// <summary>
        /// 记录移动窗体的坐标
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HeadNavigation_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Point mouseNew = MousePosition;
                int moveX = mouseNew.X - mouseOld.X;
                int moveY = mouseNew.Y - mouseOld.Y;
                this.Location = new Point(formOld.X + moveX, formOld.Y + moveY);
            }
        }
        #endregion

        #region 记录窗体移动后的坐标
        /// <summary>
        /// 记录窗体移动后的坐标
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HeadNavigation_MouseDown(object sender, MouseEventArgs e)
        {
            formOld = this.Location;
            mouseOld = MousePosition;
        }









        #endregion

        #endregion

        #region 接收数值判断是否打开进入歌手页面的计时器和全部小播放计时器

        public int i = 0;        
        public int o = 0;
        public int u = 0;
        public int s = 0;
        public int p = 0;

        /// <summary>
        /// 专辑音乐播放判断
        /// </summary>
        public int Music = 0;
        /// <summary>
        /// 专辑详细界面音乐播放判断
        /// </summary>
        public int AMusic = 0;
        /// <summary>
        /// 专辑TOP10播放判断
        /// </summary>
        public int TopTen = 0;
        /// <summary>
        /// 用户歌单播放判断
        /// </summary>
        public int SongM = 0;
        /// <summary>
        /// 判断小播放列表
        /// </summary>
        public int ASongSm = 0;
        /// <summary>
        /// 判断用户上传歌曲
        /// </summary>
        public int UserUpMusics = 0;
        /// <summary>
        /// 判断用户是否想要修改歌单
        /// </summary>
        public int EditSongListInfors = 0;


        /// <summary>
        /// 总计时器，用来判断刚刚页面的返回值
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tmrGetName_Tick(object sender, EventArgs e)
        {
            //判断是否点击歌手加入个人页面
            Occupy = SongMusicfrm.Occupy;
            if (i < Occupy)
            {
                //打开计时器
                tmrGetNames.Start();
            }

            //判断是否发返回用户主界面
            UserSongNum = UpUserSongTableInfoFrm.UserSongNum;
            if (p < UserSongNum)
            {
                //打开计时器
                tmrUserSongNum.Start();
            }

            //打开单个歌单详细页面
            OccupySong = SongListfrm.OccupySong;
            if (o < OccupySong)
            {
                //打开计时器
                tmrSongAllInfo.Start();
            }

            //打开单个专辑详细页面
            OccupyAlbum = SongMusicAlbumFrm.OccupyAlbum;
            if (u < OccupyAlbum)
            {
                //打开计时器
                tmrAlbumAllInfo.Start();
            }

            //重新加载用户信息
            UserLoginNum = UserLogin.LoginNum;
            if (s < UserLoginNum)
            {
                //打开计时器
                tmrUserLoginNum.Start();
            }

            //判断是否播放歌曲（专辑小窗体）
            SongMusicAlbum = SongMusicAlbumFrm.SongMusicAlbum;
            if (Music < SongMusicAlbum)
            {
                //打开计时器
                tmrGetMusic.Start();
            }

            //判断是否播放歌曲（歌单详细页面）
            AlbumMain = SongListMainInterfacefrm.AlbumMain;
            if (SongM < AlbumMain)
            {
                //打开计时器
                tmrGetMusic.Start();
            }

            //判断是否播放歌曲（热门前十）
            ListOfSongsList = CDFrominfo.ListOfSongsList;
            if (TopTen < ListOfSongsList)
            {
                //打开计时器
                tmrGetMusic.Start();
            }
            //专辑详细页面播放开关
            AlbumMains = AlbumMainInterface.AlbumMain;
            if (AMusic < AlbumMains)
            {
                //打开计时器
                tmrGetMusic.Start();
            }

            //打开小型列表用于储存歌单
            ListOfSongsListsm = smPlaySingFrm.ListOfSongsListsm;
            if(ASongSm < ListOfSongsListsm)
            {
                //打开计时器
                tmrGetMusic.Start();
            }
            //打开用户上传音乐表
            SeveUpMusic = SeveUserMusicfrm.SeveUpMusicp;
            if (UserUpMusics < SeveUpMusic)
            {
                tmrServerUserMusic.Start();
            }

            EditSongListInfor = SongListfrm.EditSongListInfor;
            if (EditSongListInfors < EditSongListInfor)
            {
                tmrEditSongListInfor.Start();
            }





        }


        #endregion

        #region 改变进度条长度 上一首和下一首

        double alltime;//全部时间
        double thistime;//当前时间
        double bfb;//百分比
        double thisX;
        //改变进度条长度
        public void getmusicTime()
        {
            thistime = this.MusicMainPlayer.Ctlcontrols.currentPosition;
            alltime = this.MusicMainPlayer.currentMedia.duration;
            bfb = thistime / alltime;
            thisX = 410 * bfb;
            pmusicup.Size = new Size((int)thisX, 3);
        }

        public static int Numsm;

        public static int Snums;
        public static int Snum;

        public static int Numso;
        public static int Cnums;
        public static int Cnum;

        /// <summary>
        /// 下一首
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NextOne_Click(object sender, EventArgs e)
        {
            if (PlayingMusicClass.Songone_id != "")
            {
                Snums = Convert.ToInt32(PlayingMusicClass.Songone_id) + 1;
                //获取歌单单曲总数
                Numsm = SongListfrm.Song_SingU;
                for (int i = 1; i > 0; i++)
                {
                    //获取当前音乐的编号
                    Snum = Convert.ToInt32(ListNextPlayMusicSelect.SelectSingSongSheetSongoneId(SongListfrm.Song_Id.ToString(), Snums.ToString()));
                    
                    if (Snum == 0 && Snums < Numsm)
                    {
                        //断掉了,下一首
                        Snums++;
                    }
                    else if (Snums > Numsm)
                    {
                        //两级反转
                        Snums = 1;
                    }
                    else
                    {
                        //退出循环
                        i = -1;
                    }
                }


                if (Snum > 0)
                {
                    ListOfSongsListsm += 1;
                    //单击获取对应歌单内歌曲编号
                    PlayingMusicClass.Songone_id = Snum.ToString();
                    //获取对歌曲的播放地址
                    PlayingMusicClass.Pusicaddress = ListNextPlayMusicSelect.SelectSingSongSheetPusicaddress(SongListfrm.Song_Id.ToString(), Snum.ToString()).ToString();
                    //专辑ID获取
                    PlayingMusicClass.A_id = ListNextPlayMusicSelect.SelectSingSongSheetAid(SongListfrm.Song_Id.ToString(), Snum.ToString()).ToString();
                    //歌手姓名
                    PlayingMusicClass.S_name = ListNextPlayMusicSelect.SelectSingSongSheetSname(SongListfrm.Song_Id.ToString(), Snum.ToString()).ToString();

                    PlayingMusicClass.Cd_id = "";

                    MusicPlayS();
                }
                //Musicsong();
            }

            else if (PlayingMusicClass.Cd_id != "")
            {
                Cnums = Convert.ToInt32(PlayingMusicClass.Cd_id) + 1;
                for (int i = 1; i > 0; i++)
                {
                    //获取当前音乐的编号
                    Cnum = Convert.ToInt32(ListNextPlayMusicSelect.SelectAlbumSingSongSheetCDid(PlayingMusicClass.A_id, Cnums.ToString()));
                    //获取专辑单曲总数
                    Numso = Convert.ToInt32(DBSelectSingerInfo.DBSelectSingAll(SongMusicAlbumFrm.A_id));
                    if (Cnum == 0 && Cnums < Numso)
                    {
                        //断掉了,下一首
                        Cnums++;
                    }
                    else if (Cnums > Numso)
                    {
                        //两级反转
                        Cnums = 1;
                    }
                    else
                    {
                        //退出循环
                        i = -1;
                    }
                }

                if (Cnum > 0)
                {
                    ListOfSongsListsm += 1;
                    //单击获取对应歌单内歌曲编号
                    PlayingMusicClass.Cd_id = Cnum.ToString();
                    //获取对歌曲的播放地址
                    PlayingMusicClass.Pusicaddress = ListNextPlayMusicSelect.SelectAlbumSingSongSheetPusicaddress(PlayingMusicClass.A_id, PlayingMusicClass.Cd_id).ToString();
                    //专辑ID获取
                    PlayingMusicClass.A_id = ListNextPlayMusicSelect.SelectAlbumSingSongSheetAid(PlayingMusicClass.A_id, PlayingMusicClass.Cd_id).ToString();
                    //歌手姓名
                    PlayingMusicClass.S_name = ListNextPlayMusicSelect.SelectAlbumSingSongSheetSname(PlayingMusicClass.A_id, PlayingMusicClass.Cd_id).ToString();

                    PlayingMusicClass.Songone_id = "";

                    MusicPlayS();
                }
                //Musicsalbum();
            }
        }
        /// <summary>
        /// 上一首
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LastOne_Click(object sender, EventArgs e)
        {
            if (PlayingMusicClass.Songone_id != "")
            {

                Snums = Convert.ToInt32(PlayingMusicClass.Songone_id) - 1;
                //获取歌单单曲总数
                Numsm = SongListfrm.Song_SingU;
                for (int i = 1; i > 0; i++)
                {
                    //获取当前音乐的编号
                    Snum = Convert.ToInt32(ListNextPlayMusicSelect.SelectSingSongSheetSongoneId(SongListfrm.Song_Id.ToString(), Snums.ToString()));
                    if (Snum == 0 && Snums < Numsm && Snums > 0)
                    {
                        //断掉了,上一首
                        Snums--;
                    }
                    else if (Snums <= 0)
                    {
                        //两级反转
                        Snums = Numsm;
                    }
                    else
                    {
                        //退出循环
                        i = -1;
                    }
                }

                if (Snum > 0)
                {
                    //单击获取对应歌单内歌曲编号
                    PlayingMusicClass.Songone_id = Snum.ToString();
                    //获取对歌曲的播放地址
                    PlayingMusicClass.Pusicaddress = ListNextPlayMusicSelect.SelectSingSongSheetPusicaddress(SongListfrm.Song_Id.ToString(), Snum.ToString()).ToString();
                    //专辑ID获取
                    PlayingMusicClass.A_id = ListNextPlayMusicSelect.SelectSingSongSheetAid(SongListfrm.Song_Id.ToString(), Snum.ToString()).ToString();
                    //歌手姓名
                    PlayingMusicClass.S_name = ListNextPlayMusicSelect.SelectSingSongSheetSname(SongListfrm.Song_Id.ToString(), Snum.ToString()).ToString();

                    PlayingMusicClass.Cd_id = "";


                    MusicPlayS();
                }
                //Musicsong();
            }

            else if (PlayingMusicClass.Cd_id != "")
            {
                Cnums = Convert.ToInt32(PlayingMusicClass.Cd_id) - 1;
                //获取专辑单曲总数
                Numso = Convert.ToInt32(DBSelectSingerInfo.DBSelectSingAll(SongMusicAlbumFrm.A_id));
                for (int i = 1; i > 0; i++)
                {
                    //获取当前音乐的编号
                    Cnum = Convert.ToInt32(ListNextPlayMusicSelect.SelectAlbumSingSongSheetCDid(PlayingMusicClass.A_id, Cnums.ToString()));       
                    if (Cnum == 0 && Cnums < Numso && Cnums > 0)
                    {
                        //断掉了,上一首
                        Cnums--;
                    }
                    else if (Cnums <= 0)
                    {
                        //两级反转
                        Cnums = Numso;
                    }
                    else
                    {
                        //退出循环
                        i = -1;
                    }
                }
                if (Cnum > 0)
                {
                    ListOfSongsListsm += 1;
                    //单击获取对应歌单内歌曲编号
                    PlayingMusicClass.Cd_id = Cnum.ToString();
                    //获取对歌曲的播放地址
                    PlayingMusicClass.Pusicaddress = ListNextPlayMusicSelect.SelectAlbumSingSongSheetPusicaddress(PlayingMusicClass.A_id, PlayingMusicClass.Cd_id).ToString();
                    //专辑ID获取
                    PlayingMusicClass.A_id = ListNextPlayMusicSelect.SelectAlbumSingSongSheetAid(PlayingMusicClass.A_id, PlayingMusicClass.Cd_id).ToString();
                    //歌手姓名
                    PlayingMusicClass.S_name = ListNextPlayMusicSelect.SelectAlbumSingSongSheetSname(PlayingMusicClass.A_id, PlayingMusicClass.Cd_id).ToString();

                    PlayingMusicClass.Songone_id = "";

                    MusicPlayS();
                }
            }
        }


        #endregion

        #region 计时器打开接受值 进入歌手界面

        /// <summary>
        /// 计时器打开接受值 进入歌手界面
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tmrGetNames_Tick(object sender, EventArgs e)
        {
            ++i;
            S_name = SongMusicfrm.S_name;
            //进行跳转
            Publicnull();
            this.Singer.BackgroundImage = Image.FromFile(@"IMG/Clickimg.png");

            MusicMainInterface.Controls.Clear();
            SongInfoSing sif = new SongInfoSing();
            sif.TopLevel = false;
            MusicMainInterface.Controls.Add(sif);
            sif.Show();

            //进入歌手页面关闭计时器
            tmrGetNames.Stop();
        }

        #endregion
        
        #region 选中专辑歌曲后播放音乐的计时器判断
        /// <summary>
        /// 选中专辑歌曲后播放音乐
        /// </summary>
        public string A_id;
        public string Cd_id;
        public string Songone_id;


        public void MusicPlayS()
        {
            play(PlayingMusicClass.Pusicaddress);

            A_id = PlayingMusicClass.A_id;
            MusicCover.BackgroundImage = Image.FromFile(DBSelectSingSongAlbumInfo.SelectSingAlbumtxPic(A_id).ToString());
            Songone_id = PlayingMusicClass.Songone_id;
            SongSinger.Text = PlayingMusicClass.S_name;

            Cd_id = PlayingMusicClass.Cd_id;

            tmrGetMusic.Stop();
        }

        private void tmrGetMusic_Tick(object sender, EventArgs e)
        {
            //播放选中音乐文件
            
            if (Music < SongMusicAlbum)
            {
                ++Music;
                MusicPlayS();
            }
            if (SongM < AlbumMain)
            {
                ++SongM;
                MusicPlayS();
            }
            if (TopTen < ListOfSongsList)
            {
                ++TopTen;
                MusicPlayS();
            }
            if (AMusic < AlbumMains)
            {
                ++AMusic;
                MusicPlayS();
            }
            if (ASongSm < ListOfSongsListsm)
            {
                ++ASongSm;
                MusicPlayS();
            }
        }
        #endregion
        
        #region 控制时间和歌曲进度的定时器
        /// <summary>
        /// 控制时间和歌曲进度
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SongTimeProgress_Tick(object sender, EventArgs e)
        {
            if (IsPlay == true)
            {
                getmusicTime();
                this.MusicCP.Text = this.MusicMainPlayer.Ctlcontrols.currentPositionString;
                this.MusicTP.Text = this.MusicMainPlayer.currentMedia.durationString;
                if (this.MusicMainPlayer.playState.ToString() == "wmppsStopped")
                {
                    //timer1.Enabled = false;
                    MusicCP.Text = "00:00";
                    try
                    {
                        musicNum++;
                        play(names[musicNum]);
                    }
                    catch (Exception)
                    {

                        SongTimeProgress.Enabled = false;
                    }
                }
            }
            else
            {

            }
        }
        #endregion

        #region 暂停与开始
        /// <summary>
        /// 暂停与开始
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Suspend_Click(object sender, EventArgs e)
        {
            if (Plays == false)
            {
                this.MusicMainPlayer.Ctlcontrols.play();
                SongTimeProgress.Enabled = true;
                //切换图片
                Suspend.Image = Image.FromFile(@"IMG/Broadcastimg.png");
                Plays = true;
            }
            else
            {
                //切换图片
                Suspend.Image = Image.FromFile(@"IMG/Suspendimg.png");
                this.MusicMainPlayer.Ctlcontrols.pause();
                SongTimeProgress.Enabled = false;
                Plays = false;
            }
        }


        #endregion

        #region 获取当前进度条
        //获取当前进度
        double Alltime;
        double thisTime;
        Double b;
        public void changeTime(double all, double thisp)
        {
            b = thisp / all;
            Alltime = this.MusicMainPlayer.currentMedia.duration;
            thisTime = Alltime * b;
            this.MusicMainPlayer.Ctlcontrols.currentPosition = thisTime;
        }

        #endregion

        #region 最小化窗体
        /// <summary>
        /// 最小化窗体
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Ellipsis_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }
        #endregion

        #region 鼠标移动到播放进度条显示时间
        /// <summary>
        /// 鼠标移动到播放器总进度条显示时间
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void pmusicdown_MouseHover(object sender, EventArgs e)
        {
            this.PlayerTouch.SetToolTip(pmusicup, MusicCP.Text);
        }
        /// <summary>
        /// 鼠标移动到播放器进度条显示当前播放时间
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void pmusicup_MouseHover(object sender, EventArgs e)
        {
            this.PlayerTouch.SetToolTip(pmusicdown, MusicTP.Text);
        }




        #endregion

        #region 打开单个歌单详细页面
        /// <summary>
        /// 打开单个歌单详细页面
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tmrSongAllInfo_Tick(object sender, EventArgs e)
        {
            ++o;
            S_name = SongMusicfrm.S_name;
            //进行跳转
            Publicnull();
            this.SongSheet.BackgroundImage = Image.FromFile(@"IMG/Clickimg.png");

            MusicMainInterface.Controls.Clear();
            SongListMainInterfacefrm smif = new SongListMainInterfacefrm();
            smif.TopLevel = false;
            MusicMainInterface.Controls.Add(smif);
            smif.Show();

            //进入歌单详细页面关闭计时器
            tmrSongAllInfo.Stop();
        }
        #endregion

        #region 打开单个专辑详细页面
        /// <summary>
        /// 打开单个专辑详细页面
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tmrAlbumAllInfo_Tick(object sender, EventArgs e)
        {
            ++u;
            Publicnull();
            this.Singer.BackgroundImage = Image.FromFile(@"IMG/Clickimg.png");

            MusicMainInterface.Controls.Clear();
            AlbumMainInterface ami = new AlbumMainInterface();
            ami.TopLevel = false;
            MusicMainInterface.Controls.Add(ami);
            ami.Show();

            //进入详细专辑关闭当前计时器
            tmrAlbumAllInfo.Stop();
        }
        #endregion

        #region 返回页面
        /// <summary>
        /// 返回正常页面
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tmrUserSongNum_Tick(object sender, EventArgs e)
        {
            ++p;

            MusicMainInterface.Controls.Clear();
            UserMainFrm umf = new UserMainFrm();
            umf.TopLevel = false;
            MusicMainInterface.Controls.Add(umf);
            umf.Show();

            tmrUserSongNum.Stop();
        }
        #endregion

        #region 单击打开登录界面
        /// <summary>
        /// 单击打开登录界面
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserInfos_Click(object sender, EventArgs e)
        {
            if (UserName.Text == "未登录")
            {
                Form frm = Application.OpenForms["UserLRFrm"];  //查找是否打开过UserLRFrm窗体
                if (frm == null)  //没打开过
                {
                    UserLRFrm UL = new UserLRFrm();

                    //获取当前窗体位置
                    int x = this.Location.X;
                    int y = this.Location.Y;


                    ////定位位置
                    Point p = new Point(x+367, y + 144);
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
                //进入一个全新的个人页面

                MusicMainInterface.Controls.Clear();
                UserMainFrm umf = new UserMainFrm();
                umf.TopLevel = false;
                MusicMainInterface.Controls.Add(umf);
                umf.Show();

            }
        }
        #endregion

        #region 重新加载用户信息的计时器
        /// <summary>
        /// 重新加载用户信息的计时器
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tmrUserLoginNum_Tick(object sender, EventArgs e)
        {
            ++s;
            //获取名字
            UserName.Text = LoginInfo.UserName;
            //获取头像
            this.HeadPortrait.LoadAsync("https://timgsa.baidu.com/timg?image&quality=80&size=b9999_10000&sec=1587209835893&di=02964b1de4a1ef4f938f7d3ae12b5b17&imgtype=0&src=http%3A%2F%2Fbpic.588ku.com%2Felement_origin_min_pic%2F17%2F11%2F25%2F0ef5a188956c2717db96d72d58524dec.jpg");
            tmrUserLoginNum.Stop(); 
        }
        #endregion
        
        #region 播放用户上传音乐
        /// <summary>
        /// 播放用户上传音乐
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tmrServerUserMusic_Tick(object sender, EventArgs e)
        {
            ++UserUpMusics;

            play(SeveUserMusicfrm.UpMusic);

            A_id = "";
            this.MusicCover.BackgroundImage = Image.FromFile(@"IMG/OriginalSongListInterfacepicsm.png");
            Songone_id = "";
            SongSinger.Text = "未知歌手";
            Cd_id = "";

            tmrServerUserMusic.Stop();
        }
        #endregion 

        #region 判断用户点击打开修改歌单界面判断定时器
        /// <summary>
        /// 判断用户点击打开修改歌单界面判断定时器
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tmrEditSongListInfor_Tick(object sender, EventArgs e)
        {

            ++EditSongListInfors;
            Publicnull();
            this.SongSheet.BackgroundImage = Image.FromFile(@"IMG/Clickimg.png");

            MusicMainInterface.Controls.Clear();
            UpUserSongTableInfoFrm usti = new UpUserSongTableInfoFrm();
            usti.TopLevel = false;
            MusicMainInterface.Controls.Add(usti);
            usti.Show();

            //进入修改界面后关闭当前计时器
            tmrEditSongListInfor.Stop();

        }
        #endregion

        #region 显示小播放列表
        /// <summary>
        /// 显示小播放列表
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NumberOS_Click(object sender, EventArgs e)
        {
            smPlaySingFrm psf = new smPlaySingFrm();
            psf.TopLevel = true;

            //获取当前窗体位置
            int x = this.Location.X;
            int y = this.Location.Y;


            ////定位位置
            Point p = new Point(x+442, y+147);
            psf.StartPosition = FormStartPosition.Manual;
            psf.Location = p;
            psf.ShowDialog();


            //smPlaySingFrm psf = new smPlaySingFrm();
            //psf.TopLevel = false;
            //psf.Dock = DockStyle.Fill;
            //this.smPlaySingFrm.Controls.Add(psf);
            //psf.Show();

            


        }
        #endregion

        #region 关闭窗口
        /// <summary>
        /// 关闭窗体
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MusicClose_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }
        #endregion
        
        
        
    }

}
