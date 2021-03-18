using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model
{
    /// <summary>
    /// 歌单内单曲类
    /// </summary>
    public class SongMusicInfo
    {
        /// <summary>
        /// 歌单的ID
        /// </summary>
        public string SongId { get; set; }
        /// <summary>
        /// 歌单内单曲的ID
        /// </summary>
        public string SongoneId { get; set; }
        /// <summary>
        /// 专辑编号
        /// </summary>
        public string Aid { get; set; }
        /// <summary>
        /// 专辑名字
        /// </summary>
        public string Aname { get; set; }
        /// <summary>
        /// 歌手名字
        /// </summary>
        public string Usinger { get; set; }
        /// <summary>
        /// 歌曲lrc文件位置
        /// </summary>
        public string Parlyric { get; set; }
        /// <summary>
        /// 歌曲音乐文件位置
        /// </summary>
        public string Pusicaddress { get; set; }
        /// <summary>
        /// 歌曲音乐时间
        /// </summary>
        public string Songtime { get; set; }
        /// <summary>
        /// 单张专辑歌曲编号
        /// </summary>
        public string Cdid { get; set; }
        /// <summary>
        /// 歌曲名字
        /// </summary>
        public string Cdname { get; set; }


    }
}
