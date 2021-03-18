using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model
{
    /// <summary>
    /// 用户信息表
    /// </summary>
    public class UserInfo
    {
        /// <summary>
        /// 用户编号
        /// </summary>
        public string Uid { get; set;}
        /// <summary>
        /// 用户账号名称
        /// </summary>
        public string UfullName { get; set; }
        /// <summary>
        /// 用户密码
        /// </summary>
        public string Upwd { get; set; }
        /// <summary>
        /// 用户名称
        /// </summary>
        public string Uname { get; set; }
        /// <summary>
        /// 注册时间
        /// </summary>
        public DateTime RegisterTime { get; set; }
        /// <summary>
        /// 生日
        /// </summary>
        public DateTime Ubirthday { get; set; }
        /// <summary>
        /// 性别
        /// </summary>
        public string Usex { get; set; }
        /// <summary>
        /// 头像图片地址
        /// </summary>
        public string Uimage { get; set; }
        /// <summary>
        /// 个性签名
        /// </summary>
        public string Usignature { get; set; }

    }
}
