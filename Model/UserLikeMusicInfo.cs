using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model
{
    /// <summary>
    /// 用户喜好音乐表
    /// </summary>
    public class UserLikeMusicInfo
    {
        /// <summary>
        /// 用户编号
        /// </summary>
        public string Uid { get; set; }
        /// <summary>
        /// 类型名称
        /// </summary>
        public string ParName { get; set; }
        /// <summary>
        /// 类型编号
        /// </summary>
        public string ParId { get; set; }
        /// <summary>
        /// 歌手名称
        /// </summary>
        public string Sname { get; set; }
        /// <summary>
        /// 歌手编号
        /// </summary>
        public string Sid { get; set; } 
    }
}
