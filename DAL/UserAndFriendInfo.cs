using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL
{
    public class UserAndFriendInfo
    {

        #region 查询用户歌单数量
        /// <summary>
        /// 查询用户歌单数量
        /// </summary>
        /// <returns></returns>
        public static object SelectUFSongSheetNum(string Uid)
        {
            string sql = "select Count(*) from user_song_list where u_id = '" + Uid + "'";
            return DBHelper.ExecuteScalar(sql);
        }
        #endregion

        #region 查询用户收藏歌单数量
        /// <summary>
        /// 查询用户收藏歌单数量
        /// </summary>
        /// <returns></returns>
        public static object SelectUFCollectionSongSheetNum(string Uid)
        {
            string sql = "select Count(*) from user_collection_song_sheet where user_id = '" + Uid + "'";
            return DBHelper.ExecuteScalar(sql);
        }
        #endregion


        #region 查询关注数量
        /// <summary>
        /// 查询关注数量
        /// </summary>
        /// <returns></returns>
        public static object SelectUFfollowNum(string Uid)
        {
            string sql = "select Count(*) from user_followandFans where user_id = '" + Uid + "'";
            return DBHelper.ExecuteScalar(sql);
        }
        #endregion

        #region 查询粉丝数量
        /// <summary>
        /// 查询粉丝数量
        /// </summary>
        /// <returns></returns>
        public static object SelectUFFansNum(string Fuid)
        {
            string sql = "select Count(*) from user_followandFans where fuser_id = '" + Fuid + "'";
            return DBHelper.ExecuteScalar(sql);
        }
        #endregion

        #region 查询歌单歌曲数量
        /// <summary>
        /// 查询歌单歌曲数量
        /// </summary>
        /// <returns></returns>
        public static object SelectUSongNum(string Song_Id)
        {
            string sql = "select Count(*) from song_music where song_id = '"+Song_Id+"'";
            return DBHelper.ExecuteScalar(sql);
        }
        #endregion
    }

}

