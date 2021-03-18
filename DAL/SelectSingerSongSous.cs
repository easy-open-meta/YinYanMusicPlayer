using Model;
using MySql.Data.MySqlClient;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL
{
    /// <summary>
    /// 歌手专用信息
    /// </summary>
    public class SelectSingerSongSous
    {
        /// <summary>
        /// 搜索前十歌曲
        /// </summary>
        /// <param name="Name"></param>
        /// <returns></returns>
        public static List<UpMusicInfo> SelectReserAll(string Name)
        {
            List<UpMusicInfo> UMI = new List<UpMusicInfo>();       
            string sql = "select TOP 10 a_id,cd_name,m_singer,a_name,song_time,par_lyric,music_address,cd_id  from upload_music where m_singer = '" + Name+"'";
            MySqlDataReader dr = DBHelper.ExecutReader(sql);
            while (dr.Read())
            {
                UpMusicInfo umi = new UpMusicInfo();
                //专辑ID
                umi.Aid = (string)dr["a_id"];
                //歌曲名字
                umi.Cdname = (string)dr["cd_name"];
                //歌手名字
                umi.Usinger = dr["m_singer"].ToString();
                //专辑名字
                umi.Aname = (string)dr["a_name"];
                //歌曲时长
                umi.Songtime = (string)dr["song_time"];
                //歌曲歌词链接
                umi.Parlyric = (string)dr["par_lyric"];
                //歌曲音乐链接
                umi.Pusicaddress = (string)dr["music_address"];
                //歌曲ID
                umi.Cdid = (string)dr["cd_id"];

                UMI.Add(umi);
            }
            dr.Close();
            DBHelper.Closecon();
            return UMI;
        }

        /// <summary>
        /// 搜索歌单前三首
        /// </summary>
        /// <param name="AID"></param>
        /// <returns></returns>
        public static List<UpMusicInfo> SelectReserThree(string AID)
        {
            List<UpMusicInfo> UMI = new List<UpMusicInfo>();
            string sql = "select TOP 3 a_id,cd_name,m_singer,a_name,song_time,par_lyric,music_address,cd_id from upload_music where a_id = '" + AID + "'";
            MySqlDataReader dr = DBHelper.ExecutReader(sql);
            while (dr.Read())
            {
                UpMusicInfo umi = new UpMusicInfo();
                //专辑ID
                umi.Aid = (string)dr["a_id"];
                //歌曲名字
                umi.Cdname = (string)dr["cd_name"];
                //歌手名字
                umi.Usinger = dr["m_singer"].ToString();
                //专辑名字
                umi.Aname = (string)dr["a_name"];
                //歌曲时长
                umi.Songtime = (string)dr["song_time"];
                //歌曲歌词链接
                umi.Parlyric = (string)dr["par_lyric"];
                //歌曲音乐链接
                umi.Pusicaddress = (string)dr["music_address"];
                //歌曲ID
                umi.Cdid = (string)dr["cd_id"];
                UMI.Add(umi);
            }
            dr.Close();
            DBHelper.Closecon();
            return UMI;
        }

        /// <summary>
        /// 根据ID搜索专辑歌曲
        /// </summary>
        /// <param name="AID"></param>
        /// <returns></returns>
        public static List<UpMusicInfo> DBSelectSongListAlbum(string AID)
        {
            List<UpMusicInfo> UMI = new List<UpMusicInfo>();
            string sql = "select a_id,cd_name,m_singer,a_name,song_time,par_lyric,music_address,cd_id from upload_music where a_id = '" + AID + "'";
            MySqlDataReader dr = DBHelper.ExecutReader(sql);
            while (dr.Read())
            {
                UpMusicInfo umi = new UpMusicInfo();
                //专辑ID
                umi.Aid = (string)dr["a_id"];
                //歌曲名字
                umi.Cdname = (string)dr["cd_name"];
                //歌手名字
                umi.Usinger = dr["m_singer"].ToString();
                //专辑名字
                umi.Aname = (string)dr["a_name"];
                //歌曲时长
                umi.Songtime = (string)dr["song_time"];
                //歌曲歌词链接
                umi.Parlyric = (string)dr["par_lyric"];
                //歌曲音乐链接
                umi.Pusicaddress = (string)dr["music_address"];
                //歌曲ID
                umi.Cdid = (string)dr["cd_id"];
                UMI.Add(umi);
            }
            dr.Close();
            DBHelper.Closecon();
            return UMI;
        }


        /// <summary>
        /// 根据名字获取歌手专辑数目
        /// </summary>
        /// <param name="Name"></param>
        /// <returns></returns>
        public static List<SingSongAlbumInfo> SelectSingAlbumAll(string Name)
        {

            List<SingSongAlbumInfo> SSAI = new List<SingSongAlbumInfo>();
            string sql = "select * from album where s_name = '" + Name + "'";
            MySqlDataReader dr = DBHelper.ExecutReader(sql);
            while (dr.Read())
            {
                SingSongAlbumInfo ssai = new SingSongAlbumInfo();
                //歌手ID
                ssai.Sid = (string)dr["s_id"];
                //歌手名字
                ssai.Sname = dr["s_name"].ToString();
                //专辑ID
                ssai.Aid = (string)dr["a_id"];
                //专辑名字
                ssai.Aname = (string)dr["a_name"];
                //专辑发布时间
                ssai.Atime = (string)dr["a_time"];
                //专辑简介
                ssai.Adetails = (string)dr["a_details"];
                //专辑大图
                ssai.Abigpic = (string)dr["a_bigpic"];
                //专辑小图
                ssai.Asmlpic = (string)dr["a_smlpic"];
                //专辑歌单头像
                ssai.Asmlpictx = (string)dr["a_smlpictx"];
                SSAI.Add(ssai);
            }
            dr.Close();
            DBHelper.Closecon();
            return SSAI;
        }

        ///// <summary>
        ///// 根据专辑ID获取专辑头像
        ///// </summary>
        ///// <param name="Name"></param>
        ///// <returns></returns>
        //public static List<SingSongAlbumInfo> SelectSingAlbumtxPic(string AID)
        //{

        //    List<SingSongAlbumInfo> SSAI = new List<SingSongAlbumInfo>();
        //    string sql = "select a_smlpictx from album where a_id = '" + AID + "'";
        //    MyMySqlDataReader dr = DBHelper.ExecutReader(sql);
        //    while (dr.Read())
        //    {
        //        SingSongAlbumInfo ssai = new SingSongAlbumInfo();               
        //        //专辑歌单头像
        //        ssai.Asmlpictx = (string)dr["a_smlpictx"];
        //        SSAI.Add(ssai);
        //    }
        //    dr.Close();
        //    DBHelper.Closecon();
        //    return SSAI;
        //}

        #region 根据专辑ID获取专辑头像
        public static object SelectSingAlbumtxPic(string AID)
        {
            object oj = "";
            string sql = "select a_smlpictx from album where a_id = '" + AID + "'";
            oj = DBHelper.ExecuteScalar(sql);
            return oj;
        }
        #endregion


    }
}
