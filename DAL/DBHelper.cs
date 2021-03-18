using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DAL
{
    public class DBHelper
    {
        private static readonly string conStr = "";
        private static MySqlConnection con = null;
        public static MySqlConnection GetConnection()
        {
            if (con == null || con.ConnectionString == "")
            {
                con = new MySqlConnection(conStr);
            }
            return con;
        }
        public static void Opencon()
        {
            if (con.State == ConnectionState.Closed)
            {
                con.Open();
            }
        }

        //关闭连接
        public static void Closecon()
        {
            if (con.State == ConnectionState.Open)
            {
                con.Close();
            }
        }

        #region 返回多行多列
        public static MySqlDataReader ExecutReader(string sql,
            params MySqlParameter[] para)
        {
            MySqlConnection con = GetConnection();
            Opencon();
            MySqlCommand com = new MySqlCommand(sql, con);
            com.Parameters.AddRange(para);
            MySqlDataReader dr = com.ExecuteReader();
            return dr;
        }
        #endregion

        //public static int ExecuteNonQuery(string sql)
        //{
        //    MySqlConnection con = GetConnection();
        //    Opencon();
        //    MySqlCommand com = new MySqlCommand(sql, con);
        //    int n = com.ExecuteNonQuery();
        //    Closecon();
        //    return n;
        //}

        #region 执行动作查询：添加,修改,删除
        public static int ExecuteNonQuery(string sql, //SQL语句
           CommandType type = CommandType.Text,   //命令类型：SQL文本，存储过程，表
           params SqlParameter[] para)
        {
            int n = 0;
            MySqlConnection con = GetConnection();
            Opencon();
            MySqlCommand com = new MySqlCommand(sql, con);
            com.CommandType = type;
            com.Parameters.AddRange(para);
            n = com.ExecuteNonQuery();
            Closecon();
            return n;
        }
        #endregion

        public static object ExecuteScalar(string sql,
            CommandType type = CommandType.Text,
            params MySqlParameter[] para)
        {
            object obj = 0;
            MySqlConnection con = GetConnection();
            Opencon();
            MySqlCommand com = new MySqlCommand(sql, con);
            com.CommandType = type;
            com.Parameters.AddRange(para);
            obj = com.ExecuteScalar();
            Closecon();
            return obj;
        }


        #region 生成随机数
        /// <summary>
        /// 使用Guid产生的种子生成真随机数
        /// </summary>
        public static string GetRandomNumber(int min, int max, int len)
        {
            string RandomNum = "";
            for (int i = 0; i < len; i++)
            {
                int rtn = 0;
                Random r = new Random();
                byte[] buffer = Guid.NewGuid().ToByteArray();
                int iSeed = BitConverter.ToInt32(buffer, 0);
                r = new Random(iSeed);
                rtn = r.Next(min, max + 1);
                RandomNum += rtn;
            }
            return RandomNum;
        }
        #endregion

        #region 获取网上时间
        public static string GetNetDateTime()
        {//获取网络时间
            WebRequest request = null;
            WebResponse response = null;
            WebHeaderCollection headerCollection = null;
            string datetime = string.Empty;
            try
            {
                request = WebRequest.Create("https://www.baidu.com");
                request.Timeout = 3000;
                request.Credentials = CredentialCache.DefaultCredentials;
                response = request.GetResponse();
                headerCollection = response.Headers;
                foreach (var h in headerCollection.AllKeys)
                {
                    if (h == "Date")
                    {
                        datetime = headerCollection[h];
                    }
                }
                return datetime;
            }
            catch (Exception) { return datetime; }
            finally
            {
                if (request != null)
                { request.Abort(); }
                if (response != null)
                { response.Close(); }
                if (headerCollection != null)
                { headerCollection.Clear(); }
            }
        }
        #endregion
    }
}
