using Model;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL
{
    /// <summary>
    /// 歌手系列的查询DAL
    /// </summary>
    public class SelectSingerInfo
    {
        /// <summary>
        /// 查询所有歌手信息
        /// </summary>
        /// <returns></returns>
        public static List<SingerInfo> SelectSingerInfos()
        {
            //实例化范型
            List<SingerInfo> Singerin = new List<SingerInfo>();
            //数据库语句
            string sql = "select * from singer";
            //调用方法
            MySqlDataReader dr = DBHelper.ExecutReader(sql);
            while (dr.Read())
            {
                SingerInfo si = new SingerInfo();
                si.Uid = (string)dr["u_id"];
                si.Sid = (string)dr["s_id"];
                si.Stype = (string)dr["s_type"];
                si.Ssex = (string)dr["s_sex"];
                si.Sname = (string)dr["s_name"];
                si.CDu = (int)dr["cd_u"];
                si.Albumu = (int)dr["album_u"];
                si.MVu = (int)dr["mv_u"];
                si.Spic = (string)dr["s_pic"];
                si.Sinfo = (string)dr["s_info"];
                Singerin.Add(si);
            }
            dr.Close();
            DBHelper.Closecon();
            return Singerin;

        }

        /// <summary>
        /// 根据歌手名字获取歌手信息
        /// </summary>
        /// <param name="s_name"></param>
        /// <returns></returns>
        public static List<SingerInfo> SelectSingerInfose(string s_name)
        {
            List<SingerInfo> SI = new List<SingerInfo>();
            string sql = "select * from singer where s_name='" + s_name + "'";
            MySqlDataReader dr = DBHelper.ExecutReader(sql);
            while (dr.Read())
            {
                SingerInfo si = new SingerInfo();
                si.Uid = (string)dr["u_id"];
                si.Sid = (string)dr["s_id"];
                si.Stype = (string)dr["s_type"];
                si.Ssex = (string)dr["s_type"];
                si.Sname = (string)dr["s_name"];
                si.CDu = (int)dr["cd_u"];
                si.Albumu = (int)dr["album_u"];
                si.MVu = (int)dr["mv_u"];
                si.Spic = (string)dr["s_pic"];
                si.Sinfo = (string)dr["s_info"];
                SI.Add(si);
            }
            dr.Close();
            DBHelper.Closecon();
            return SI;

        }

        public void SingerInfos()
        {

        }
        #region 全部系列
        /// <summary>
        /// 全部系列
        /// </summary>
        /// <param name="s_name"></param>
        /// <returns></returns>
        public static List<SingerInfo> SelectallbInfose(string allInfo)
        {
            List<SingerInfo> SI = new List<SingerInfo>();
            string sql = allInfo;
            MySqlDataReader dr = DBHelper.ExecutReader(sql);
            while (dr.Read())
            {
                SingerInfo si = new SingerInfo();
                si.Uid = (string)dr["u_id"];
                si.Sid = (string)dr["s_id"];
                si.Stype = (string)dr["s_type"];
                si.Ssex = (string)dr["s_type"];
                si.Sname = (string)dr["s_name"];
                si.CDu = (int)dr["cd_u"];
                si.Albumu = (int)dr["album_u"];
                si.MVu = (int)dr["mv_u"];
                si.Spic = (string)dr["s_pic"];
                si.Sinfo = (string)dr["s_info"];
                SI.Add(si);
            }
            dr.Close();
            DBHelper.Closecon();
            return SI;

        }
        #endregion

        /// <summary>
        /// 查询歌手名称
        /// </summary>
        /// <returns></returns>
        public static List<string> SelectSingerName()
        {
            List<string> ssn = new List<string>();
            string sql = "select s_name from singer";
            MySqlDataReader dr = DBHelper.ExecutReader(sql);
            while (dr.Read())
            {

                ssn.Add(dr["s_name"].ToString());

            }
            dr.Close();
            DBHelper.Closecon();
            return ssn;
        }

        #region 查询单曲数
        /// <summary>
        /// 查询单曲数
        /// </summary>
        /// <returns></returns>
        public static object SelectCDSingAll(string Name, int i)
        {
            if (i == 1)
            {
                string sql = "select Count(*)from (select distinct a_name from upload_music where m_singer = '" + Name + "') As Temp ";
                return DBHelper.ExecuteScalar(sql);
            }
            else
            {
                string sql = "select Count(*) from upload_music where m_singer = '" + Name + "'";
                return DBHelper.ExecuteScalar(sql);
            }


            //else//MV
            //{
            //    string sql = "select Count(*)from (select distinct a_name from upload_music where m_singer = '" + Name + "') As Temp ";
            //    return DBHelper.ExecuteScalar(sql);
            //}   
        }
        #endregion


        #region 查询单曲数
        /// <summary>
        /// 查询单曲数
        /// </summary>
        /// <returns></returns>
        public static object SelectAlbumSingAll(string Aid)
        {
            string sql = "select Count(*) from upload_music where A_id = '" + Aid + "'";
            return DBHelper.ExecuteScalar(sql);
        }
        #endregion

        


    }
}
