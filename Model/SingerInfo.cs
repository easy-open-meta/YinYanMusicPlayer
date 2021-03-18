using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model
{
    /// <summary>
    /// 歌手表
    /// </summary>
    public class SingerInfo
    {
        /// <summary>
        /// 用户编号
        /// </summary>
        public string Uid { get; set; }
        /// <summary>
        /// 歌手编号
        /// </summary>
        public string Sid { get; set; }
        /// <summary>
        /// 歌手类型
        /// </summary>
        public string Stype { get; set; }
        /// <summary>
        /// 歌手性别
        /// </summary>
        public string Ssex { get; set; }
        /// <summary>
        /// 歌手姓名
        /// </summary>
        public string Sname { get; set; }
        /// <summary>
        /// 单曲数量
        /// </summary>
        public int CDu { get; set; }
        /// <summary>
        /// 专辑数量
        /// </summary>
        public int Albumu { get; set; }
        /// <summary>
        /// MV数量
        /// </summary>
        public int MVu { get; set; }
        /// <summary>
        /// 歌手图片
        /// </summary>
        public string Spic { get; set; }
        /// <summary>
        /// 歌手简介
        /// </summary>
        public string Sinfo { get; set; }

    }
}
