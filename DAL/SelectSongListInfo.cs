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
    /// 查询歌单代码类
    /// </summary>
    public class SelectSongListInfo
    {
        /// <summary>
        /// 查找所有歌单信息
        /// </summary>
        /// <returns></returns>
        public static List<UserSongListInfo> SelectUserSongListInfos()
        {
            //实例化范型
            List<UserSongListInfo> ListUserSLI = new List<UserSongListInfo>();
            //数据库语句
            string sql = "select * from user_song_list";
            //调用方法
            MySqlDataReader dr = DBHelper.ExecutReader(sql);
            while (dr.Read())
            {
                UserSongListInfo USLI = new UserSongListInfo();
                USLI.Uid = (string)dr["u_id"];
                USLI.Uname = (string)dr["u_name"];
                USLI.ParId = (string)dr["par_id"];
                USLI.SongId = (int)dr["song_id"];
                USLI.SongName = (string)dr["song_name"];
                USLI.SongCollection = (int)dr["song_collection"];
                USLI.SongSingU = (int)dr["song_sing_u"];
                USLI.SongInfo = (string)dr["song_info"];
                USLI.SongTime = (DateTime)dr["song_time"];
                USLI.SongPic = (string)dr["song_pic"];
                ListUserSLI.Add(USLI);
            }
            dr.Close();
            DBHelper.Closecon();
            return ListUserSLI;
        }

        /// <summary>
        /// 查找所有歌单信息
        /// </summary>
        /// <returns></returns>
        public static List<UserSongListInfo> SelectUserSongListInfo(string sqls)
        {
            //实例化范型
            List<UserSongListInfo> ListUserSLI = new List<UserSongListInfo>();
            //数据库语句
            string sql = sqls;
            //调用方法
            MySqlDataReader dr = DBHelper.ExecutReader(sql);
            while (dr.Read())
            {
                UserSongListInfo USLI = new UserSongListInfo();
                USLI.Uid = (string)dr["u_id"];
                USLI.Uname = (string)dr["u_name"];
                USLI.ParId = (string)dr["par_id"];
                USLI.SongId = (int)dr["song_id"];
                USLI.SongName = (string)dr["song_name"];
                USLI.SongCollection = (int)dr["song_collection"];
                USLI.SongSingU = (int)dr["song_sing_u"];
                USLI.SongInfo = (string)dr["song_info"];
                USLI.SongTime = (DateTime)dr["song_time"];
                USLI.SongPic = (string)dr["song_pic"];
                ListUserSLI.Add(USLI);
            }
            dr.Close();
            DBHelper.Closecon();
            return ListUserSLI;
        }

        /// <summary>
        /// 查找用户自己的歌单信息
        /// </summary>
        /// <returns></returns>
        public static List<UserSongListInfo> SelectUserMeSongListInfo(string Uid)
        {
            //实例化范型
            List<UserSongListInfo> ListUserSLI = new List<UserSongListInfo>();
            //数据库语句
            string sql = "select * from user_song_list where u_id = "+Uid+"";
            //调用方法
            MySqlDataReader dr = DBHelper.ExecutReader(sql);
            while (dr.Read())
            {
                UserSongListInfo USLI = new UserSongListInfo();
                USLI.Uid = (string)dr["u_id"];
                USLI.Uname = (string)dr["u_name"];
                USLI.ParId = (string)dr["par_id"];
                USLI.SongId = (int)dr["song_id"];
                USLI.SongName = (string)dr["song_name"];
                USLI.SongCollection = (int)dr["song_collection"];
                USLI.SongSingU = (int)dr["song_sing_u"];
                USLI.SongInfo = (string)dr["song_info"];
                USLI.SongTime = (DateTime)dr["song_time"];
                USLI.SongPic = (string)dr["song_pic"];
                ListUserSLI.Add(USLI);
            }
            dr.Close();
            DBHelper.Closecon();
            return ListUserSLI;
        }

        /// <summary>
        /// 查找用户收藏的歌单信息
        /// </summary>
        /// <returns></returns>
        public static List<UserSongListInfo> SelectUserMeCollectionListInfo(string Uid)
        {
            //实例化范型
            List<UserSongListInfo> ListUserSLI = new List<UserSongListInfo>();
            //数据库语句
            string sql = "select u_id,u_name,par_id,b.song_id,song_name,song_collection,song_sing_u,song_info,song_time,song_pic from user_collection_song_sheet a left join user_song_list b on a.song_id = b.song_id where a.user_id = " + Uid+"";
            //调用方法
            MySqlDataReader dr = DBHelper.ExecutReader(sql);
            while (dr.Read())
            {
                UserSongListInfo USLI = new UserSongListInfo();
                USLI.Uid = dr["u_id"].ToString();
                USLI.Uname = (string)dr["u_name"];
                USLI.ParId = (string)dr["par_id"];
                USLI.SongId = (int)dr["song_id"];
                USLI.SongName = (string)dr["song_name"];
                USLI.SongCollection = (int)dr["song_collection"];
                USLI.SongSingU = (int)dr["song_sing_u"];
                USLI.SongInfo = (string)dr["song_info"];
                USLI.SongTime = (DateTime)dr["song_time"];
                USLI.SongPic = (string)dr["song_pic"];
                ListUserSLI.Add(USLI);
            }
            dr.Close();
            DBHelper.Closecon();
            return ListUserSLI;
        }

        /// <summary>
        /// 查找歌单单独信息
        /// </summary>
        /// <returns></returns>
        public static List<SongMusicInfo> SelectUserSongInfo(string SongId)
        {
            //实例化范型
            List<SongMusicInfo> SMI = new List<SongMusicInfo>();
            //数据库语句
            string sql = "select a_id,cd_name,m_singer,a_name,song_time,par_lyric,music_address,cd_id,songone_id from song_music where song_id = '" + SongId+"'";
            //调用方法
            MySqlDataReader dr = DBHelper.ExecutReader(sql);
            while (dr.Read())
            {
                SongMusicInfo smi = new SongMusicInfo();
                //专辑ID
                smi.Aid = (string)dr["a_id"];
                //歌曲名字
                smi.Cdname = (string)dr["cd_name"];
                //歌手名字
                smi.Usinger = dr["m_singer"].ToString();
                //专辑名字
                smi.Aname = (string)dr["a_name"];
                //歌曲时长
                smi.Songtime = (string)dr["song_time"];
                //歌曲歌词链接
                smi.Parlyric = (string)dr["par_lyric"];
                //歌曲音乐链接
                smi.Pusicaddress = (string)dr["music_address"];
                //歌曲ID
                smi.Cdid = (string)dr["cd_id"];
                //歌单内歌曲ID
                smi.SongoneId= (string)dr["songone_id"];
                SMI.Add(smi);
            }
            dr.Close();
            DBHelper.Closecon();
            return SMI;

        }

        //select a_id,cd_name,m_singer,a_name,song_time,par_lyric,music_address,cd_id from song_music where song_id = 10000001

        /// <summary>
        /// 查询自己的歌单
        /// </summary>
        /// <returns></returns>
        public static List<UserSongListInfo> SelectUserMeSongListTableInfo(string Uid)
        {
            //实例化范型
            List<UserSongListInfo> ListUserSLI = new List<UserSongListInfo>();
            //数据库语句
            string sql = "select song_id , song_name from user_song_list where u_id = " + Uid + "";
            //调用方法
            MySqlDataReader dr = DBHelper.ExecutReader(sql);
            while (dr.Read())
            {
                UserSongListInfo USLI = new UserSongListInfo();
                USLI.SongId = (int)dr["song_id"];
                USLI.SongName = (string)dr["song_name"];
                ListUserSLI.Add(USLI);
            }
            dr.Close();
            DBHelper.Closecon();
            return ListUserSLI;
        }

        #region 添加喜欢的歌曲到自己的歌单
        /// <summary>
        /// 添加喜欢的歌曲到自己的歌单
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static int InsertSongInfo(SongMusicInfo s)
        {
            int n = 0;
            string sql = "insert song_music values('{0}','{1}','{2}','{3}','{4}','{5}','{6}','{7}','{8}','{9}')";
            sql = string.Format(sql, s.SongId,s.SongoneId,s.Aid,s.Aname,s.Usinger,s.Parlyric,s.Pusicaddress,s.Songtime,s.Cdid,s.Cdname);
            n = DBHelper.ExecuteNonQuery(sql);
            return n;
        }
        #endregion

        #region 收藏别人的歌单
        /// <summary>
        /// 添加喜欢的歌曲到自己的歌单
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static int InsertUserSongSingtable(UserCollectionSongSheet u)
        {
            int n = 0;
            string sql = "insert user_collection_song_sheet values('{0}','{1}')";
            sql = string.Format(sql, u.Userid, u.Songid);
            n = DBHelper.ExecuteNonQuery(sql);
            return n;
        }
        #endregion

        #region 根据歌单的账号来修改用户歌单热度
        /// <summary>
        /// 根据歌单的账号来修改用户歌单热度
        /// </summary>
        /// <param name="song_collection"></param>
        /// <param name="song_id"></param>
        /// <returns></returns>
        public static int UpdateUserSongListCollection(int Song_collection, string Song_id)
        {
            string sql = "update user_song_list set song_collection='" + Song_collection + "' where song_id='" + Song_id + "'";
            return DBHelper.ExecuteNonQuery(sql);
        }
        #endregion
        //

        //#region 根据歌单的账号来修改用户歌单热度All
        ///// <summary>
        ///// 根据歌单的账号来修改用户歌单热度
        ///// </summary>
        ///// <param name="song_collection"></param>
        ///// <param name="song_id"></param>
        ///// <returns></returns>
        //public static int UpdateUserSongListCollectionAll(int Song_collection, string Song_id)
        //{
        //    string sql = "update user_song_list set song_collection = '" + Song_collection + "' and song_id = '" + Song_id + "'";
        //    return DBHelper.ExecuteNonQuery(sql);
        //}
        //#endregion



        #region 查询歌单内是否有该歌曲
        /// <summary>
        /// 查询歌单内是否有该歌曲
        /// </summary>
        /// <returns></returns>
        public static object SingSongYesNo(string Aid,string Cdid,string Songid)
        {
            string sql = "select count(*) from song_music where a_id = '"+Aid+"' and cd_id = '"+Cdid+"' and song_id = '"+Songid+"'";
            return DBHelper.ExecuteScalar(sql);
        }
        #endregion

        #region 查询单曲数
        /// <summary>
        /// 查询单曲数
        /// </summary>
        /// <returns></returns>
        public static object SelectSingsongoneNumAll(string SongId)
        {
            string sql = "select Count(*) from song_music where song_id = '" + SongId + "'";
            return DBHelper.ExecuteScalar(sql);
        }
        #endregion

        #region 判断右键添加的歌单是否存在
        /// <summary>
        /// 判断右键添加的歌单是否存在
        /// </summary>
        /// <returns></returns>
        public static object UserYesNoSingSongTable(string user_id, string song_id)
        {
            string sql = "select Count(*) from user_collection_song_sheet where user_id = '" + user_id + "' and song_id = '" + song_id + "'";
            return DBHelper.ExecuteScalar(sql);
        }
        #endregion

        #region 搜索对应歌单类型的名字
        /// <summary>
        /// 搜索对应歌单类型的名字
        /// </summary>
        /// <returns></returns>
        public static object SelectPartitionName(string par_id)
        {
            string sql = "select par_name from t_partition where par_id = '"+ par_id + "'";
            return DBHelper.ExecuteScalar(sql);
        }
        #endregion

        #region 搜索对应歌单类型的名字
        /// <summary>
        /// 搜索对应歌单类型的名字
        /// </summary>
        /// <returns></returns>
        public static object SelectPartitionid(string par_name)
        {
            string sql = "select par_id from t_partition where par_name = '" + par_name + "'";
            return DBHelper.ExecuteScalar(sql);
        }
        #endregion


    }
}
