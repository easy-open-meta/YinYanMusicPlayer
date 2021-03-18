using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model
{
    /// <summary>
    /// 用户歌单表
    /// </summary>
    public class UserSongListInfo
    {
        /// <summary>
        /// 用户编号
        /// </summary>
        public string Uid { get; set; }
        /// <summary>
        /// 用户名称
        /// </summary>
        public string Uname { get; set; }
        /// <summary>
        /// 类型ID
        /// </summary>
        public string ParId { get; set; }
        /// <summary>
        /// 歌单编号
        /// </summary>
        public int SongId { get; set; }
        /// <summary>
        /// 歌单名称
        /// </summary>
        public string SongName { get; set; }
        /// <summary>
        /// 歌单热度
        /// </summary>
        public int SongCollection { get; set; }
        /// <summary>
        /// 歌单歌的数量
        /// </summary>
        public int SongSingU { get; set; }
        /// <summary>
        /// 歌单简介
        /// </summary>
        public string SongInfo { get; set; }
        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime SongTime { get; set; }
        /// <summary>
        /// 歌单头像地址
        /// </summary>
        public string SongPic { get; set; }
        
    }
}
