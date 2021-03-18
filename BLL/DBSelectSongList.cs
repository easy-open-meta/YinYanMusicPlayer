using DAL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL
{
    /// <summary>
    /// 搜索数据库歌单表BLL
    /// </summary>
    public class DBSelectSongList
    {

        #region 将歌曲筛选前十进行导出
        /// <summary>
        /// 将歌曲筛选前十进行导出
        /// </summary>
        /// <returns></returns>
        public static object DBSelectSongListTopTen(string Name)
        {
            return SelectSingerSongSous.SelectReserAll(Name);
        }
        #endregion


        #region 将歌曲筛选前三进行导出
        /// <summary>
        /// 将歌曲筛选前三进行导出
        /// </summary>
        /// <returns></returns>
        public static object DBSelectSongListTopThree(string AID)
        {
            return SelectSingerSongSous.SelectReserThree(AID);
        }
        #endregion

        #region 将歌曲筛选对应的专辑
        /// <summary>
        /// 将歌曲筛选对应的专辑
        /// </summary>
        /// <returns></returns>
        public static object DBSelectSongListAlbum(string AID)
        {
            return SelectSingerSongSous.DBSelectSongListAlbum(AID);
        }
        #endregion

    }
}
