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
    public partial class SongSheetFrm : Form
    {
        public SongSheetFrm()
        {
            InitializeComponent();
        }

        List<SingerInfo> SInfo = null;
        SongMusicfrm smmt = null;

        private void SongSheetFrm_Load(object sender, EventArgs e)
        {
            //清空控件
            ListOfSingersFLP.Controls.Clear();
            //调用方法
            SInfo = DBSelectSingerInfo.SelectSingerInfoAll();

            for (int i = 0; i < SInfo.Count; i++)
            {
                smmt = new SongMusicfrm(this);
                smmt.Tag = SInfo[i].Sid;
                smmt.SIfomin = SInfo[i];
                ListOfSingersFLP.Controls.Add(smmt);
            }
        }

        public string Findtxt;

        List<string> ssn = DBSelectSingerInfo.SelectSingerName();
        /// <summary>
        /// 查询
        /// </summary>
        public void AZselect()
        {
            //清空控件
            ListOfSingersFLP.Controls.Clear();
            ////调用方法
            //SInfo = SelectSingerInfo.SelectSingerInfos();


            string songtext = GetSpellCode(Findtxt);

            List<string> list = new List<string>();
            for (int i = 0; i < ssn.Count; i++)
            {
                if (GetSpellCode(ssn[i].ToString()).IndexOf(songtext) == 0)
                {
                    list.Add(ssn[i]);
                    ListOfSingersFLP.Controls.Clear();
                }
            }
            for (int i = 0; i < list.Count; i++)
            {
                //根据名字来查找歌手
                List<SingerInfo> smi = DBSelectSingerInfo.SelectSingerInfose(list[i]);
                    for (int j = 0; j < smi.Count; j++)
                    {
                        smmt = new SongMusicfrm(this);
                        smmt.Tag = smi[j].Sid;
                        smmt.SIfomin = smi[j];
                        ListOfSingersFLP.Controls.Add(smmt);
                    }

                if (smi.Count > 10)
                {
                    scrollBar1.Visible = true;                   
                }
                else
                {
                    scrollBar1.Visible = false;
                }

            }
            

        }

        private void A_Click(object sender, EventArgs e)
        {
            Findtxt = "A";
            AZselect();
        }

        private void B_Click(object sender, EventArgs e)
        {
            Findtxt = "B";
            AZselect();
        }

        private void C_Click(object sender, EventArgs e)
        {
            Findtxt = "C";
            AZselect();
        }

        private void D_Click(object sender, EventArgs e)
        {
            Findtxt = "D";
            AZselect();
        }

        private void E_Click(object sender, EventArgs e)
        {
            Findtxt = "E";
            AZselect();
        }

        private void F_Click(object sender, EventArgs e)
        {
            Findtxt = "F";
            AZselect();
        }

        private void G_Click(object sender, EventArgs e)
        {
            Findtxt = "G";
            AZselect();
        }

        private void H_Click(object sender, EventArgs e)
        {
            Findtxt = "H";
            AZselect();
        }

        private void I_Click(object sender, EventArgs e)
        {
            Findtxt = "I";
            AZselect();
        }

        private void J_Click(object sender, EventArgs e)
        {
            Findtxt = "J";
            AZselect();
        }

        private void K_Click(object sender, EventArgs e)
        {
            Findtxt = "K";
            AZselect();
        }

        private void L_Click(object sender, EventArgs e)
        {
            Findtxt = "L";
            AZselect();
        }

        private void M_Click(object sender, EventArgs e)
        {
            Findtxt = "M";
            AZselect();
        }

        private void N_Click(object sender, EventArgs e)
        {
            Findtxt = "N";
            AZselect();
        }

        private void O_Click(object sender, EventArgs e)
        {
            Findtxt = "O";
            AZselect();
        }

        private void P_Click(object sender, EventArgs e)
        {
            Findtxt = "P";
            AZselect();
        }

        private void Q_Click(object sender, EventArgs e)
        {
            Findtxt = "Q";
            AZselect();
        }

        private void R_Click(object sender, EventArgs e)
        {
            Findtxt = "R";
            AZselect();
        }

        private void S_Click(object sender, EventArgs e)
        {
            Findtxt = "S";
            AZselect();
        }

        private void T_Click(object sender, EventArgs e)
        {
            Findtxt = "T";
            AZselect();
        }

        private void U_Click(object sender, EventArgs e)
        {
            Findtxt = "U";
            AZselect();
        }

        private void V_Click(object sender, EventArgs e)
        {
            Findtxt = "V";
            AZselect();
        }

        private void W_Click(object sender, EventArgs e)
        {
            Findtxt = "W";
            AZselect();
        }

        private void X_Click(object sender, EventArgs e)
        {
            Findtxt = "X";
            AZselect();
        }

        private void Y_Click(object sender, EventArgs e)
        {
            Findtxt = "Y";
            AZselect();
        }

        private void Z_Click(object sender, EventArgs e)
        {
            Findtxt = "Z";
            AZselect();
        }

        private void QT_Click(object sender, EventArgs e)
        {
            Findtxt = "?";
            AZselect();
        }

        /// <summary>
        /// 查看全部
        /// </summary>
        public void AllSingSonglb()
        {
            //清空控件
            ListOfSingersFLP.Controls.Clear();
            //调用方法
            SInfo = DBSelectSingerInfo.SelectSingerInfoAll();

            for (int i = 0; i < SInfo.Count; i++)
            {
                smmt = new SongMusicfrm(this);
                smmt.Tag = SInfo[i].Sid;
                smmt.SIfomin = SInfo[i];
                ListOfSingersFLP.Controls.Add(smmt);
            }
            if (SInfo.Count > 10)
            {
                scrollBar1.Visible = true;
            }
            else
            {
                scrollBar1.Visible = false;
            }
        }

        /// <summary>
        /// 查询全部
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PopSingerlb_Click(object sender, EventArgs e)
        {
            AllSingSonglb();
        }


        #region 在指定的字符串列表CnStr中检索符合拼音索引字符串

        /// <summary>

        /// 在指定的字符串列表CnStr中检索符合拼音索引字符串

        /// </summary>

        /// <param name="CnStr">汉字字符串</param>

        /// <returns>相对应的汉语拼音首字母串</returns>

        public static string GetSpellCode(string CnStr)
        {

            string strTemp = "";

            int iLen = CnStr.Length;

            int i = 0;
            //strTemp = GetCharSpellCode(CnStr.Substring(0, 1));
            for (i = 0; i <= iLen - 1; i++)
            {

                strTemp += GetCharSpellCode(CnStr.Substring(i, 1));

            }

            return strTemp;

        }
        #endregion

        #region 得到一个汉字的拼音第一个字母，如果是一个英文字母则直接返回大写字母
        /// <summary>

        /// 得到一个汉字的拼音第一个字母，如果是一个英文字母则直接返回大写字母

        /// </summary>

        /// <param name="CnChar">单个汉字</param>

        /// <returns>单个大写字母</returns>

        private static string GetCharSpellCode(string CnChar)
        {

            long iCnChar;

            byte[] ZW = System.Text.Encoding.Default.GetBytes(CnChar);

            //如果是字母，则直接返回

            if (ZW.Length == 1)
            {

                return CnChar.ToUpper();

            }

            else
            {

                // get the array of byte from the single char

                int i1 = (short)(ZW[0]);

                int i2 = (short)(ZW[1]);

                iCnChar = i1 * 256 + i2;

            }

            // iCnChar match the constant

            if ((iCnChar >= 45217) && (iCnChar <= 45252))
            {

                return "A";

            }

            else if ((iCnChar >= 45253) && (iCnChar <= 45760))
            {

                return "B";

            }
            else if ((iCnChar >= 45761) && (iCnChar <= 46317))
            {

                return "C";

            }
            else if ((iCnChar >= 46318) && (iCnChar <= 46825))
            {

                return "D";

            }
            else if ((iCnChar >= 46826) && (iCnChar <= 47009))
            {

                return "E";

            }
            else if ((iCnChar >= 47010) && (iCnChar <= 47296))
            {

                return "F";

            }
            else if ((iCnChar >= 47297) && (iCnChar <= 47613))
            {

                return "G";

            }
            else if ((iCnChar >= 47614) && (iCnChar <= 48118))
            {

                return "H";

            }
            else if ((iCnChar >= 48119) && (iCnChar <= 49061))
            {

                return "J";

            }
            else if ((iCnChar >= 49062) && (iCnChar <= 49323))
            {

                return "K";

            }
            else if ((iCnChar >= 49324) && (iCnChar <= 49895))
            {

                return "L";

            }
            else if ((iCnChar >= 49896) && (iCnChar <= 50370))
            {

                return "M";

            }
            else if ((iCnChar >= 50371) && (iCnChar <= 50613))
            {

                return "N";

            }
            else if ((iCnChar >= 50614) && (iCnChar <= 50621))
            {

                return "O";

            }
            else if ((iCnChar >= 50622) && (iCnChar <= 50905))
            {

                return "P";

            }
            else if ((iCnChar >= 50906) && (iCnChar <= 51386))
            {

                return "Q";

            }
            else if ((iCnChar >= 51387) && (iCnChar <= 51445))
            {

                return "R";

            }
            else if ((iCnChar >= 51446) && (iCnChar <= 52217))
            {

                return "S";

            }
            else if ((iCnChar >= 52218) && (iCnChar <= 52697))
            {

                return "T";

            }
            else if ((iCnChar >= 52698) && (iCnChar <= 52979))
            {

                return "W";

            }
            else if ((iCnChar >= 52980) && (iCnChar <= 53640))
            {

                return "X";

            }
            else if ((iCnChar >= 53689) && (iCnChar <= 54480))
            {

                return "Y";

            }
            else if ((iCnChar >= 54481) && (iCnChar <= 55289))
            {

                return "Z";

            }
            else

                return ("?");

        }






        #endregion

        private void SingSongAll_Tick(object sender, EventArgs e)
        {
            
        }

        private void AllCategorielb_Click(object sender, EventArgs e)
        {
            AllSingSonglb();
        }

        private void AllLanguagelb_Click(object sender, EventArgs e)
        {
            AllSingSonglb();
        }

        /// <summary>
        /// 华语
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Chineselb_Click(object sender, EventArgs e)
        {
            //清空控件
            ListOfSingersFLP.Controls.Clear();
            List<SingerInfo> smi = DBSelectSingerInfo.SelectChineselbInfose();
            for (int j = 0; j < smi.Count; j++)
            {
                smmt = new SongMusicfrm(this);
                smmt.Tag = smi[j].Sid;
                smmt.SIfomin = smi[j];
                ListOfSingersFLP.Controls.Add(smmt);
            }

            if (smi.Count > 10)
            {
                scrollBar1.Visible = true;
            }
            else
            {
                scrollBar1.Visible = false;
            }
        }
        /// <summary>
        /// 欧美
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EandAlb_Click(object sender, EventArgs e)
        {
            //清空控件
            ListOfSingersFLP.Controls.Clear();
            List<SingerInfo> smi = DBSelectSingerInfo.SelectEandAlbInfose();
            for (int j = 0; j < smi.Count; j++)
            {
                smmt = new SongMusicfrm(this);
                smmt.Tag = smi[j].Sid;
                smmt.SIfomin = smi[j];
                ListOfSingersFLP.Controls.Add(smmt);
            }

            if (smi.Count > 10)
            {
                scrollBar1.Visible = true;
            }
            else
            {
                scrollBar1.Visible = false;
            }
        }
        /// <summary>
        /// 日本系列
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Japanlb_Click(object sender, EventArgs e)
        {
            //清空控件
            ListOfSingersFLP.Controls.Clear();
            List<SingerInfo> smi = DBSelectSingerInfo.SelectJapanlbInfose();
            for (int j = 0; j < smi.Count; j++)
            {
                smmt = new SongMusicfrm(this);
                smmt.Tag = smi[j].Sid;
                smmt.SIfomin = smi[j];
                ListOfSingersFLP.Controls.Add(smmt);
            }

            if (smi.Count > 10)
            {
                scrollBar1.Visible = true;
            }
            else
            {
                scrollBar1.Visible = false;
            }
        }
        /// <summary>
        /// 韩国
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Korealb_Click(object sender, EventArgs e)
        {
            //清空控件
            ListOfSingersFLP.Controls.Clear();
            List<SingerInfo> smi = DBSelectSingerInfo.SelectKorealbInfose();
            for (int j = 0; j < smi.Count; j++)
            {
                smmt = new SongMusicfrm(this);
                smmt.Tag = smi[j].Sid;
                smmt.SIfomin = smi[j];
                ListOfSingersFLP.Controls.Add(smmt);
            }

            if (smi.Count > 10)
            {
                scrollBar1.Visible = true;
            }
            else
            {
                scrollBar1.Visible = false;
            }
        }
        /// <summary>
        /// 其他系列
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Bosslb_Click(object sender, EventArgs e)
        {
            //清空控件
            ListOfSingersFLP.Controls.Clear();
            List<SingerInfo> smi = DBSelectSingerInfo.SelectBosslbInfose();
            for (int j = 0; j < smi.Count; j++)
            {
                smmt = new SongMusicfrm(this);
                smmt.Tag = smi[j].Sid;
                smmt.SIfomin = smi[j];
                ListOfSingersFLP.Controls.Add(smmt);
            }

            if (smi.Count > 10)
            {
                scrollBar1.Visible = true;
            }
            else
            {
                scrollBar1.Visible = false;
            }
        }
        /// <summary>
        /// 男歌手
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MaleSingerlb_Click(object sender, EventArgs e)
        {
            //清空控件
            ListOfSingersFLP.Controls.Clear();
            List<SingerInfo> smi = DBSelectSingerInfo.SelectMaleSingerlbInfose();
            for (int j = 0; j < smi.Count; j++)
            {
                smmt = new SongMusicfrm(this);
                smmt.Tag = smi[j].Sid;
                smmt.SIfomin = smi[j];
                ListOfSingersFLP.Controls.Add(smmt);
            }

            if (smi.Count > 10)
            {
                scrollBar1.Visible = true;
            }
            else
            {
                scrollBar1.Visible = false;
            }
        }
        /// <summary>
        /// 女歌手
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FemaleSingerlb_Click(object sender, EventArgs e)
        {
            //清空控件
            ListOfSingersFLP.Controls.Clear();
            List<SingerInfo> smi = DBSelectSingerInfo.SelectFemaleSingerlbInfose();
            for (int j = 0; j < smi.Count; j++)
            {
                smmt = new SongMusicfrm(this);
                smmt.Tag = smi[j].Sid;
                smmt.SIfomin = smi[j];
                ListOfSingersFLP.Controls.Add(smmt);
            }

            if (smi.Count > 10)
            {
                scrollBar1.Visible = true;
            }
            else
            {
                scrollBar1.Visible = false;
            }
        }
        /// <summary>
        /// 乐队组合
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BandCombolb_Click(object sender, EventArgs e)
        {
            //清空控件
            ListOfSingersFLP.Controls.Clear();
            List<SingerInfo> smi = DBSelectSingerInfo.SelectBandCombolbInfose();
            for (int j = 0; j < smi.Count; j++)
            {
                smmt = new SongMusicfrm(this);
                smmt.Tag = smi[j].Sid;
                smmt.SIfomin = smi[j];
                ListOfSingersFLP.Controls.Add(smmt);
            }

            if (smi.Count > 10)
            {
                scrollBar1.Visible = true;
            }
            else
            {
                scrollBar1.Visible = false;
            }
        }
    }
}
