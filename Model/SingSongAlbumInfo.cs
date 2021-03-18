using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model
{
    public class SingSongAlbumInfo
    {

        /// <summary>
        /// 歌手ID
        /// </summary>
        public string Sid { get; set;}
        /// <summary>
        /// 歌手姓名
        /// </summary>
        public string Sname { get; set; }
        /// <summary>
        /// 专辑ID
        /// </summary>
        public string Aid { get; set; }
        /// <summary>
        /// 专辑名字
        /// </summary>
        public string Aname { get; set; }
        /// <summary>
        /// 发布专辑时间
        /// </summary>
        public string Atime { get; set; }
        /// <summary>
        /// 专辑简介
        /// </summary>
        public string Adetails { get; set; }
        /// <summary>
        /// 专辑图片大图
        /// </summary>
        public string Abigpic { get; set; }
        /// <summary>
        /// 专辑图片小图
        /// </summary>
        public string Asmlpic { get; set; }
        /// <summary>
        /// 专辑歌单头像
        /// </summary>
        public string Asmlpictx { get;set; }


    }
}
