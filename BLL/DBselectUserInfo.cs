using DAL;
using Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL
{
    public class DBselectUserInfo
    {
        /// <summary>
        /// 根据用户输入账号进行判断是否存在
        /// </summary>
        /// <param name="AID"></param>
        /// <returns></returns>
        public static UserInfo SelectUserfullName(string UfuName)
        {
            return UserInfoSelect.SelectUserfullName(UfuName);
        }

        /// <summary>
        /// 根据用户输入这返回进行判断
        /// </summary>
        /// <param name="UfuName"></param>
        /// <returns></returns>
        public static UserInfo SelectUserPwdOKNO(string UfuName,string Pwd)
        {
            return UserInfoSelect.SelectUserPwdOKNO(UfuName,Pwd);
        }

        /// <summary>
        /// 添加用户信息
        /// </summary>
        /// <param name="u"></param>
        /// <returns></returns>
        public static int InsertUserInfo(UserInfo u)
        {
            return UserInfoSelect.InsertUserInfo(u);
        }

        /// <summary>
        /// 用户新建歌单
        /// </summary>
        /// <param name="usli"></param>
        /// <returns></returns>
        public static int InsertUpNewSongList(UserSongListInfo usli)
        {
            return UserInfoSelect.InsertUpNewSongList(usli);
        }


    }
}
