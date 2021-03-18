using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL
{
    public class ListNextPlayMusicSelect
    {

        #region 上一首和下一首  普通歌单
        /// <summary>
        /// 用户歌单导入小歌单(歌单内单曲ID)
        /// </summary>
        /// <param name="Song_id"></param>
        /// <param name="Songone_id"></param>
        /// <returns></returns>
        public static object SelectSingSongSheetSongoneId(string Song_id,string Songone_id)
        {
            string sql = "select Songone_id from song_music where song_id = '" + Song_id + "' and songone_id = '" + Songone_id + "'";
            return DBHelper.ExecuteScalar(sql);
        }
        /// <summary>
        /// 用户歌单导入小歌单(歌单内音乐文件)
        /// </summary>
        /// <param name="Song_id"></param>
        /// <param name="Songone_id"></param>
        /// <returns></returns>
        public static object SelectSingSongSheetPusicaddress(string Song_id, string Songone_id)
        {
            string sql = "select music_address from song_music where song_id = '" + Song_id + "' and songone_id = '" + Songone_id + "'";
            return DBHelper.ExecuteScalar(sql);
        }
        /// <summary>
        /// 用户歌单导入小歌单(歌单内单曲的专辑ID)
        /// </summary>
        /// <param name="Song_id"></param>
        /// <param name="Songone_id"></param>
        /// <returns></returns>
        public static object SelectSingSongSheetAid(string Song_id, string Songone_id)
        {
            string sql = "select a_id from song_music where song_id = '" + Song_id + "' and songone_id = '" + Songone_id + "'";
            return DBHelper.ExecuteScalar(sql);
        }
        /// <summary>
        /// 用户歌单导入小歌单(歌单内单曲的歌手名字)
        /// </summary>
        /// <param name="Song_id"></param>
        /// <param name="Songone_id"></param>
        /// <returns></returns>
        public static object SelectSingSongSheetSname(string Song_id, string Songone_id)
        {
            string sql = "select m_singer from song_music where song_id = '" + Song_id + "' and songone_id = '" + Songone_id + "'";
            return DBHelper.ExecuteScalar(sql);
        }
        #endregion

        #region 上一首和下一首 歌手专辑
        /// <summary>
        /// 专辑导入专辑单曲的id
        /// </summary>
        /// <param name="A_id"></param>
        /// <param name="Songone_id"></param>
        /// <returns></returns>
        public static object SelectAlbumSingSongSheetCDid(string A_id, string CD_id)
        {
            string sql = "select cd_id from upload_music where a_id = '" + A_id + "' and cd_id = '" + CD_id + "'";
            return DBHelper.ExecuteScalar(sql);
        }
        /// <summary>
        /// 专辑导入专辑音乐的播放路径
        /// </summary>
        /// <param name="A_id"></param>
        /// <param name="Songone_id"></param>
        /// <returns></returns>
        public static object SelectAlbumSingSongSheetPusicaddress(string A_id, string CD_id)
        {
            string sql = "select music_address from upload_music where a_id = '" + A_id + "' and cd_id = '" + CD_id + "'";
            return DBHelper.ExecuteScalar(sql);
        }
        /// <summary>
        /// 专辑导入专辑的ID
        /// </summary>
        /// <param name="A_id"></param>
        /// <param name="Songone_id"></param>
        /// <returns></returns>
        public static object SelectAlbumSingSongSheetAid(string A_id, string CD_id)
        {
            string sql = "select a_id from upload_music where a_id = '" + A_id + "' and cd_id = '" + CD_id + "'";
            return DBHelper.ExecuteScalar(sql);
        }
        /// <summary>
        /// 专辑导入歌手名字
        /// </summary>
        /// <param name="A_id"></param>
        /// <param name="Songone_id"></param>
        /// <returns></returns>
        public static object SelectAlbumSingSongSheetSname(string A_id, string CD_id)
        {
            string sql = "select m_singer from upload_music where a_id = '" + A_id + "' and cd_id = '" + CD_id + "'";
            return DBHelper.ExecuteScalar(sql);
        }
        #endregion



    }
}
