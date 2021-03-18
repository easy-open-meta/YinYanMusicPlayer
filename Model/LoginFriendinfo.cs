using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Model
{
    public class LoginFriendinfo
    {

        //根据用户点击除自己以外的用户时
        //判断是进入自己的主页还是别人的主页
        //根据在进入主页界面的时候判断FUserID和UserID的值是否一样
        //只有当FUserID和UserID一致或FUserID为空时才能进入自己的主页

        /// <summary>
        /// 用户ID
        /// </summary>
        public static string FUserID = "";

    }
}