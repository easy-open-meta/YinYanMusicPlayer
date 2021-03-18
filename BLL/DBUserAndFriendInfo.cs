using DAL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL
{
    public class DBUserAndFriendInfo
    {

        #region 查询用户歌单的数量
        /// <summary>
        /// 查询用户歌单的数量
        /// </summary>
        /// <returns></returns>
        public static object SelectUFSongSheetNum(string Uid)
        {
            return UserAndFriendInfo.SelectUFSongSheetNum(Uid);
        }
        #endregion

        #region 查询用户收藏歌单的数量
        /// <summary>
        /// 查询用户收藏歌单的数量
        /// </summary>
        /// <returns></returns>
        public static object SelectUFCollectionSongSheetNum(string Uid)
        {
            return UserAndFriendInfo.SelectUFCollectionSongSheetNum(Uid);
        }
        #endregion

        

        #region 查询关注数量
        /// <summary>
        /// 查询关注数量
        /// </summary>
        /// <returns></returns>
        public static object SelectUFfollowNum(string Uid)
        {
            return UserAndFriendInfo.SelectUFfollowNum(Uid);
        }
        #endregion

        #region 查询粉丝数量
        /// <summary>
        /// 查询粉丝数量
        /// </summary>
        /// <returns></returns>
        public static object SelectUFFansNum(string Fuid)
        {
            return UserAndFriendInfo.SelectUFFansNum(Fuid);
        }
        #endregion


        #region 查询粉丝数量
        /// <summary>
        /// 查询粉丝数量
        /// </summary>
        /// <returns></returns>
        public static object SelectUSongNum(string Song_Id)
        {
            return UserAndFriendInfo.SelectUSongNum(Song_Id);
        }
        #endregion
        

    }
}
