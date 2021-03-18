using DAL;
using Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL
{
    public class DBSelectSingSongAlbumInfo
    {

        public static List<SingSongAlbumInfo> SelectSingAlbumAll(string Name)
        {
            return SelectSingerSongSous.SelectSingAlbumAll(Name);
        }

        /// <summary>
        /// 返回一个值专辑图片
        /// </summary>
        /// <param name="AID"></param>
        /// <returns></returns>
        public static object SelectSingAlbumtxPic(string AID)
        {
            return SelectSingerSongSous.SelectSingAlbumtxPic(AID);
        }

    }
}
