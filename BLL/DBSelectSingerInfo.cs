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
    /// 查询歌手代码类
    /// </summary>
    public class DBSelectSingerInfo
    {

        /// <summary>
        ///  查询所有歌手信息--BLL
        /// </summary>
        /// <returns></returns>
        public static List<SingerInfo> SelectSingerInfoAll()
        {
            return SelectSingerInfo.SelectSingerInfos();
        }

        /// <summary>
        /// 根据歌手名字查歌手--BLL
        /// </summary>
        /// <param name="s_name"></param>
        /// <returns></returns>
        public static List<SingerInfo> SelectSingerInfose(string s_name)
        { 
                return SelectSingerInfo.SelectSingerInfose(s_name);        
        }

        #region 全部系列单击事件
        /// <summary>
        /// 华语系列
        /// </summary>
        /// <param name="s_name"></param>
        /// <returns></returns>
        public static List<SingerInfo> SelectChineselbInfose()
        {
            return SelectSingerInfo.SelectallbInfose("select * from singer where s_type='华语'");
        }
        /// <summary>
        /// 华语系列
        /// </summary>
        /// <param name="s_name"></param>
        /// <returns></returns>
        public static List<SingerInfo> SelectEandAlbInfose()
        {
            return SelectSingerInfo.SelectallbInfose("select * from singer where s_type='欧美'");
        }
        /// <summary>
        /// 日本系列
        /// </summary>
        /// <param name="s_name"></param>
        /// <returns></returns>
        public static List<SingerInfo> SelectJapanlbInfose()
        {
            return SelectSingerInfo.SelectallbInfose("select * from singer where s_type='日本'");
        }
        /// <summary>
        /// 韩国系列
        /// </summary>
        /// <param name="s_name"></param>
        /// <returns></returns>
        public static List<SingerInfo> SelectKorealbInfose()
        {
            return SelectSingerInfo.SelectallbInfose("select * from singer where s_type='韩国'");
        }
        /// <summary>
        /// 其他系列
        /// </summary>
        /// <param name="s_name"></param>
        /// <returns></returns>
        public static List<SingerInfo> SelectBosslbInfose()
        {
            return SelectSingerInfo.SelectallbInfose("select * from singer where s_type='其他'");
        }
        /// <summary>
        /// 其他系列
        /// </summary>
        /// <param name="s_name"></param>
        /// <returns></returns>
        public static List<SingerInfo> SelectMaleSingerlbInfose()
        {
            return SelectSingerInfo.SelectallbInfose("select * from singer where s_sex='男歌手'");
        }
        /// <summary>
        /// 其他系列
        /// </summary>
        /// <param name="s_name"></param>
        /// <returns></returns>
        public static List<SingerInfo> SelectFemaleSingerlbInfose()
        {
            return SelectSingerInfo.SelectallbInfose("select * from singer where s_sex='女歌手'");
        }
        /// <summary>
        /// 其他系列
        /// </summary>
        /// <param name="s_name"></param>
        /// <returns></returns>
        public static List<SingerInfo> SelectBandCombolbInfose()
        {
            return SelectSingerInfo.SelectallbInfose("select * from singer where s_sex='乐队组合'");
        }

        #endregion
        /// <summary>
        /// 搜索歌手名字--BLL
        /// </summary>
        /// <returns></returns>
        public static List<string> SelectSingerName()
        {
            return SelectSingerInfo.SelectSingerName();
        }

        #region 歌曲、MV、专辑数量记载
        /// <summary>
        /// 歌曲、MV、专辑数量记载
        /// </summary>
        /// <returns></returns>
        public static object DBSelectCDSingAll(string Name,int i)
        {
            return SelectSingerInfo.SelectCDSingAll(Name,i);
        }
        #endregion

        #region 专辑歌曲数量记载
        /// <summary>
        /// 歌曲、MV、专辑数量记载
        /// </summary>
        /// <returns></returns>
        public static object DBSelectSingAll(string Aid)
        {
            return SelectSingerInfo.SelectAlbumSingAll(Aid);
        }
        #endregion


    }
}
