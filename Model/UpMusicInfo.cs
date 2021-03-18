using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model
{
    public class UpMusicInfo
    {
        /// <summary>
        /// 用户编号
        /// </summary>
        public string Uid { get; set; }
        /// <summary>
        /// 用户姓名
        /// </summary>
        public string Uname { get; set; }
        /// <summary>
        /// 上传时间
        /// </summary>
        public DateTime Uptime { get; set; }
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
        /// 歌曲类型ID
        /// </summary>
        public string Parid { get; set; }
        /// <summary>
        /// 歌曲类型名字
        /// </summary>
        public string Parname { get; set; }
        /// <summary>
        /// 歌曲lrc文件位置
        /// </summary>
        public string Parlyric { get; set; }
        /// <summary>
        /// 歌曲音乐文件位置
        /// </summary>
        public string Pusicaddress{ get; set; }
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
