using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WordMusicWinfrom
{
    public partial class SeveUserMusicfrm : Form
    {
        public SeveUserMusicfrm()
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

        #endregion


        string[] names;//获取歌曲路径集合
        List<string> list;
        int musicNum = 0;

        public static int mua;

        #region 单击保存音乐   控件点击事件
        /// <summary>
        /// 单击保存音乐
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param> 
        private void ClickSingpic_Click(object sender, EventArgs e)
        {
            list = new List<string>();
            string[] oldFile;//保存以前的names
            string[] newFile;//排序后的names
            OpenFileDialog of = new OpenFileDialog();
            of.InitialDirectory = "D:\\WordMusicWinfrom\\WordMusicWinfrom\\WordMusicWinfrom\\bin\\Debug\\SingSong";
            of.Filter = "mp3|*.mp3|wav|*.wav|flac|*.flac";
            of.RestoreDirectory = true;
            of.FilterIndex = 1;
            of.Multiselect = true;
            if (of.ShowDialog() == DialogResult.OK)
            {
                int k = 0;
                int same = 0;//记录相同数量
                if (names == null)
                {
                    oldFile = new string[of.FileNames.Length];
                    foreach (var i in of.FileNames)
                    {
                        oldFile[k] = i;
                        k++;
                    }
                }
                else
                {
                    oldFile = new string[of.FileNames.Length + names.Length];
                    for (int y = 0; y < names.Length; y++)
                    {
                        oldFile[k] = names[y];
                        k++;
                    }
                    foreach (var i in of.FileNames)
                    {
                        oldFile[k] = i;
                        k++;

                    }
                }
                for (int i = 0; i < oldFile.Length; i++)
                {
                    for (int j = i + 1; j < oldFile.Length; j++)
                    {
                        if (oldFile[i] == oldFile[j])
                        {
                            same++;
                        }
                    }
                }
                for (int i = 0; i < oldFile.Length; i++)
                {
                    for (int j = i + 1; j < oldFile.Length; j++)
                    {
                        if (oldFile[i] == oldFile[j])
                        {
                            oldFile[i] = "null";
                        }
                    }
                }
                //消除重复歌曲
                int w = 0;
                newFile = new string[oldFile.Length - same];
                for (int i = 0; i < oldFile.Length; i++)
                {
                    if (oldFile[i] != "null")
                    {
                        newFile[w] = oldFile[i];
                        w++;
                    }
                }

                names = newFile;
                for (int i = 0; i < names.Length; i++)
                {
                    list.Add(names[i]);
                }
                SaveMusicList();
            }
            setMusicList();
        }

        #endregion


        #region 读取音乐方法
        /// <summary>
        /// 读取方法
        /// </summary>
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
            //play(names[musicNum]);
        }

        #endregion


        #region 返回歌曲名字
        /// <summary>
        /// 返回歌曲名字
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private string getFileName(string path)
        {
            return System.IO.Path.GetFileNameWithoutExtension(path);
        }
        #endregion


        #region 添加歌单显示
        int index = 0;//下标
        public static string[] a;
        public static int Num = 0;
        /// <summary>
        /// 添加歌单显示
        /// </summary>
        private void setMusicList()
        {
            if (mua == 1 && names != null)
            {
                int num = names.Length;
                int i = 0;
                for (i = 0; num > i; i++)
                {
                    string NeiRongs = getFileName(names[index + Num]);

                        Num++;
                        string[] a = { "",Num.ToString(), NeiRongs };
                        DgvListOfSongsList.Rows.Add(a);
                }
                mua = 0;
                label2.Text = "共" + names.Length + "首";
            }
            else
            {
                try
                {
                    //点击生成
                    string NeiRong = getFileName(names[index + Num]);
                    Num++;
                    string[] a = { "", Num.ToString(), NeiRong };
                    DgvListOfSongsList.Rows.Add(a);
                    label2.Text = "共" + names.Length + "首";
                }
                catch
                {

                }

            }


        }

        #endregion


        #region 读取方法

        /// <summary>
        /// 读取方法
        /// </summary>
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
                //txtMusicNum.Text = names.Length.ToString();
            }

            #endregion

        }
        //窗体加载事件
        private void SeveUserMusicfrm_Load(object sender, EventArgs e)
        {
            CmpSetDgv();
            mua = 1;
            Num = 0;

            GetMusicList();
            setMusicList();

            if (names == null)
            {
                label2.Text = "共0首";
            }
            else
            {
                label2.Text = "共" + names.Length + "首";
            }
            

        }

        public static string UpMusic;
        public static string UpMusicName;
        public static int SeveUpMusicp;
        
        /// <summary>
        /// 双击播放音乐
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DgvListOfSongsList_DoubleClick(object sender, EventArgs e)
        {
            SeveUpMusicp++;
            UpMusic = names[Convert.ToInt32(DgvListOfSongsList.SelectedRows[0].Cells["clNum"].Value)-1];
            UpMusicName = DgvListOfSongsList.SelectedRows[0].Cells["clCdname"].Value.ToString();

        }

        private void DgvListOfSongsList_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }
    }
}
