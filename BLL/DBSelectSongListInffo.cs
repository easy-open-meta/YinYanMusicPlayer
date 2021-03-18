using DAL;
using Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL
{
    /// <summary>
    /// 查询歌单类BLL
    /// </summary>
    public class DBSelectSongListInffo
    {

        /// <summary>
        ///  查询所有歌单信息--BLL
        /// </summary>
        /// <returns></returns>
        public static List<UserSongListInfo> SelectUserSongListInfo()
        {
            return SelectSongListInfo.SelectUserSongListInfos();
        }
        /// <summary>
        ///  查询所有华语歌单信息--BLL
        /// </summary>
        /// <returns></returns>
        public static List<UserSongListInfo> SelectUserSongChinesetxtListInfo()
        {
            return SelectSongListInfo.SelectUserSongListInfos();
        }
        /// <summary>
        ///  查询所有现代歌单信息--BLL
        /// </summary>
        /// <returns></returns>
        public static List<UserSongListInfo> SelectUserSongPopulartxtListInfo()
        {
            return SelectSongListInfo.SelectUserSongListInfo("select * from user_song_list where par_id='1001'");
        }
        /// <summary>
        ///  查询所有古风歌单信息--BLL
        /// </summary>
        /// <returns></returns>
        public static List<UserSongListInfo> SelectUserSongBalladtxtListInfo()
        {
            return SelectSongListInfo.SelectUserSongListInfo("select * from user_song_list where par_id='1002'");
        }
        /// <summary>
        ///  查询所有摇滚歌单信息--BLL
        /// </summary>
        /// <returns></returns>
        public static List<UserSongListInfo> SelectUserSongRocktxtListInfo()
        {
            return SelectSongListInfo.SelectUserSongListInfo("select * from user_song_list where par_id='1003'");
        }
        /// <summary>
        ///  查询所有电子歌单信息--BLL
        /// </summary>
        /// <returns></returns>
        public static List<UserSongListInfo> SelectUserSongElectronicstxtListInfo()
        {
            return SelectSongListInfo.SelectUserSongListInfo("select * from user_song_list where par_id='1004'");
        }
        /// <summary>
        ///  查询所有另类/独立歌单信息--BLL
        /// </summary>
        /// <returns></returns>
        public static List<UserSongListInfo> SelectUserSongOffbeattxtListInfo()
        {
            return SelectSongListInfo.SelectUserSongListInfo("select * from user_song_list where par_id='1005'");
        }
        /// <summary>
        ///  查询所有轻音乐歌单信息--BLL
        /// </summary>
        /// <returns></returns>
        public static List<UserSongListInfo> SelectUserSongMovieReasontxtListInfo()
        {
            return SelectSongListInfo.SelectUserSongListInfo("select * from user_song_list where par_id='1006'");
        }
        /// <summary>
        ///  查询所有影视音乐歌单信息--BLL
        /// </summary>
        /// <returns></returns>
        public static List<UserSongListInfo> SelectUserSongLightMusictxtListInfo()
        {
            return SelectSongListInfo.SelectUserSongListInfo("select * from user_song_list where par_id='1007'");
        }
        /// <summary>
        ///  查询所有ACG歌单信息--BLL
        /// </summary>
        /// <returns></returns>
        public static List<UserSongListInfo> SelectUserSongACGtxtListInfo()
        {
            return SelectSongListInfo.SelectUserSongListInfo("select * from user_song_list where par_id='1008'");
        }
        /// <summary>
        ///  查询所有怀旧歌单信息--BLL
        /// </summary>
        /// <returns></returns>
        public static List<UserSongListInfo> SelectUserSongNostalgiatxtListInfo()
        {
            return SelectSongListInfo.SelectUserSongListInfo("select * from user_song_list where par_id='1009'");
        }

        /// <summary>
        ///  查询用户自己歌单信息--BLL
        /// </summary>
        /// <returns></returns>
        public static List<UserSongListInfo> SelectUserMeSongListInfo(string Uid)
        {
            return SelectSongListInfo.SelectUserMeSongListInfo(Uid);
        }
        
        

        /// <summary>
        ///  查询用户自己歌单信息--BLL
        /// </summary>
        /// <returns></returns>
        public static List<UserSongListInfo> SelectUserMeCollectionListInfo(string Uid)
        {
            return SelectSongListInfo.SelectUserMeCollectionListInfo(Uid);
        }

        /// <summary>
        ///  查询歌单单独信息--BLL
        /// </summary>
        /// <returns></returns>
        public static List<SongMusicInfo> SelectUserSongInfo(string SongId)
        {
            return SelectSongListInfo.SelectUserSongInfo(SongId);
        }

        /// <summary>
        ///  查询用户自己歌单用户添加--BLL
        /// </summary>
        /// <returns></returns>
        public static List<UserSongListInfo> SelectUserMeSongListTableInfo(string Uid)
        {
            return SelectSongListInfo.SelectUserMeSongListTableInfo(Uid);
        }

        /// <summary>
        /// 添加喜欢的歌曲进入自己的歌单
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static int InsertSpendInfo(SongMusicInfo s)
        {
            return SelectSongListInfo.InsertSongInfo(s);
        }

        /// <summary>
        /// 收藏喜欢的歌单
        /// </summary>
        /// <param name="u"></param>
        /// <returns></returns>
        public static int InsertUserSongSingtable(UserCollectionSongSheet u)
        {
            return SelectSongListInfo.InsertUserSongSingtable(u);
        }

        #region 根据歌单的账号来修改用户歌单热度
        /// <summary>
        /// 根据歌单的账号来修改用户歌单热度
        /// </summary>
        /// <param name="Song_collection"></param>
        /// <param name="Song_id"></param>
        /// <returns></returns>
        public static int UpdateUserSongListCollection(int Song_collection, string Song_id)
        {
            return SelectSongListInfo.UpdateUserSongListCollection(Song_collection+1, Song_id);
        }
        #endregion


        #region 查询歌曲是否存在
        /// <summary>
        /// 查询歌曲是否存在
        /// </summary>
        /// <returns></returns>
        public static object SingSongYesNo(string Aid, string Cdid, string Songid)
        {
            return SelectSongListInfo.SingSongYesNo(Aid,Cdid,Songid);
        }
        #endregion

        #region 查询单曲数
        /// <summary>
        /// 查询单曲数
        /// </summary>
        /// <returns></returns>
        public static object SelectSingsongoneNumAll(string Songid)
        {
            return SelectSongListInfo.SelectSingsongoneNumAll(Songid);
        }
        #endregion

        #region 判断单曲是否存在
        /// <summary>
        /// 判断单曲是否存在
        /// </summary>
        /// <returns></returns>
        public static object UserYesNoSingSongTable(string user_id, string song_id)
        {
            return SelectSongListInfo.UserYesNoSingSongTable(user_id, song_id);
        }
        #endregion


        #region 搜索对应歌单类型的名字
        /// <summary>
        /// 搜索对应歌单类型的名字
        /// </summary>
        /// <returns></returns>
        public static object SelectPartitionName(string par_id)
        {
            return SelectSongListInfo.SelectPartitionName(par_id);
        }
        #endregion

        #region 搜索对应歌单类型的id
        /// <summary>
        /// 搜索对应歌单类型的id
        /// </summary>
        /// <returns></returns>
        public static object SelectPartitionid(string par_name)
        {
            return SelectSongListInfo.SelectPartitionid(par_name);
        }
        #endregion


    }
}
