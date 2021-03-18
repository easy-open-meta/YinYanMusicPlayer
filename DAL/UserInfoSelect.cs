using Model;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL
{
    public class UserInfoSelect
    {

        

        #region 根据用户输入的账号和密码获取数据库中的账号密码进行比对
        public static UserInfo SelectUserPwdOKNO(string UfuName,string Pwd)
        {
            UserInfo u = null;
            string sql = "select * from user_info where u_full_name = '{0}'and u_pwd = '{1}'";
            sql = string.Format(sql, UfuName, Pwd);
            MySqlDataReader dr = DBHelper.ExecutReader(sql);
            while (dr.Read())
            {
                u = new UserInfo();
                u.Uid = dr["u_id"].ToString();
                u.UfullName = (string)dr["u_full_name"];
                u.Upwd = (string)dr["u_pwd"];
                u.Uname = (string)dr["u_name"];
                u.RegisterTime = (DateTime)dr["register_time"];
                u.Ubirthday = (DateTime)dr["u_birthday"];
                u.Usex = (string)dr["u_sex"];
                u.Uimage = (string)dr["u_image"];
                u.Usignature = (string)dr["u_signature"];
            }
            dr.Close();
            DBHelper.Closecon();
            return u;
        }
        #endregion

        #region 根据用户输入的账号和数据库中的账号做比对
        /// <summary>
        /// 根据歌手名字获取不同的值
        /// </summary>
        /// <param name="s_name"></param>
        /// <returns></returns>
        public static UserInfo SelectUserfullName(string UfuName)
        {
            UserInfo u = null;
            string sql = "select * from user_info where u_full_name = '" + UfuName + "'";
            MySqlDataReader dr = DBHelper.ExecutReader(sql);
            while (dr.Read())
            {
                u = new UserInfo();
                u.Uid = dr["u_id"].ToString();
                u.UfullName = (string)dr["u_full_name"];
                u.Upwd = (string)dr["u_pwd"];
                u.Uname = (string)dr["u_name"];
                u.RegisterTime = (DateTime)dr["register_time"];
                u.Ubirthday = (DateTime)dr["u_birthday"];
                u.Usex = (string)dr["u_sex"];
                u.Uimage = (string)dr["u_image"];
                u.Usignature = (string)dr["u_signature"];
    }
            dr.Close();
            DBHelper.Closecon();
            return u;

        }
        #endregion

        #region 添加用户注册信息
        /// <summary>
        /// 添加用户信息
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static int InsertUserInfo(UserInfo u)
        {
            int n = 0;
            string sql = "insert user_info values('{0}','{1}','{2}','{3}','{4}','{5}','{6}','{7}')";
            sql = string.Format(sql,u.UfullName,u.Upwd,u.Uname,u.RegisterTime, u.Ubirthday, u.Usex,u.Uimage,u.Usignature);
            n = DBHelper.ExecuteNonQuery(sql);
            return n;
        }
        #endregion

        #region 用户修改自己歌单信息

        /// <summary>
        /// 用户修改自己歌单信息
        /// </summary>
        /// <param name="song_collection"></param>
        /// <param name="song_id"></param>
        /// <returns></returns>
        public static int UpdateUserSongInfo(string Song_Name, string Par_id,string Song_Info,int Song_Id) 
        {
            string sql = "update user_song_list set song_name = '" + Song_Name + "', par_id = '" + Par_id + "' , song_info = '"+Song_Info+"' where song_id = '" + Song_Id + "'";
            return DBHelper.ExecuteNonQuery(sql);
        }

        #endregion

        #region 用户新建歌单
        /// <summary>
        /// 用户新建歌单
        /// </summary>
        /// <param name="usli"></param>
        /// <returns></returns>
        public static int InsertUpNewSongList(UserSongListInfo usli)
        {
            string sql = "insert user_song_list values('{0}','{1}','{2}','{3}','{4}','{5}','{6}','{7}','{8}')";
            sql = string.Format(sql, usli.Uid, usli.Uname, usli.ParId, usli.SongName,usli.SongCollection, usli.SongSingU, usli.SongInfo, usli.SongTime, usli.SongPic);
            return DBHelper.ExecuteNonQuery(sql);
        }
        #endregion

    }
}
