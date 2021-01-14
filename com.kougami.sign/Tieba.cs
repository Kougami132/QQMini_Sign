﻿using Newtonsoft.Json;
using QQMini.PluginSDK.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace com.kougami.sign
{
    public static class Tieba
    {
        public static string url_list = "https://tieba.baidu.com/mo/q/newmoindex";
        public static string url_tbs = "http://tieba.baidu.com/dc/common/tbs";
        public static string url_sign = "https://tieba.baidu.com/sign/add";
        public static Timer timer = new Timer();

        public static void Start()
        {
            timer.Enabled = true;
            timer.Interval = 600000; //间隔10分钟
            timer.Start();
            timer.Elapsed += new ElapsedEventHandler(Event_Timer);
        }

        private static void Event_Timer(object source, ElapsedEventArgs e)
        {
            if (!Program.enable) return;
            QMLog.CurrentApi.Debug("检测贴吧是否签到......");
            string[] member = Config.Get("tieba.ini", "all", "member").Split(',');
            foreach (string i in member)
            {
                string cookie = Config.Get("tieba.ini", i, "cookie");
                string result = Run(cookie);
                try
                {
                    QMApi.CurrentApi.SendFriendMessage(long.Parse(Config.Get("config.ini", "all", "robot")), long.Parse(i), result);
                    if (result.Contains("失败")) QMApi.CurrentApi.SendFriendMessage(long.Parse(Config.Get("config.ini", "all", "robot")), long.Parse(i), "将于 10 分钟后重试");
                }
                catch
                {
                    QMApi.CurrentApi.SendGroupTempMessage(long.Parse(Config.Get("config.ini", "all", "robot")), long.Parse(Config.Get("tieba.ini", i, "group")), long.Parse(i), result);
                    if (result.Contains("失败")) QMApi.CurrentApi.SendGroupTempMessage(long.Parse(Config.Get("config.ini", "all", "robot")), long.Parse(Config.Get("tieba.ini", i, "group")), long.Parse(i), "将于 10 分钟后重试");
                }
            }
        }

        public static string Run(string cookie)
        {
            Like_Forum[] info = GetTiebaInfo(cookie).data.like_forum;
            int signed = 0, success = 0, error = 0;
            foreach (Like_Forum i in info)
            {
                if (i.is_sign == 1)
                {
                    signed++;
                }
                else
                {
                    Response_Tieba_Sign sign_result = Sign(cookie, i.forum_name);
                    if (sign_result.no == 0)
                    {
                        success++;
                    }
                    else
                    {
                        error++;
                        QMLog.CurrentApi.Debug(i.forum_name + sign_result.error);
                    }
                }
            }
            string result = "总共关注了 " + info.Length + " 个吧";
            if (signed != 0) result += "\n" + signed + " 个吧已签";
            if (success != 0) result += "\n" + success + " 个吧签到成功";
            if (error != 0) result += "\n" + error + " 个吧签到失败";
            return result;
        }

        public static Tieba_Info GetTiebaInfo(string cookie)
        {
            Dictionary<string, string> header = new Dictionary<string, string>();
            header.Add("Cookie", cookie);
            return JsonConvert.DeserializeObject<Tieba_Info>(HTTP_GET(url_list, header));
        }

        public static Response_Tieba_Sign Sign(string cookie, string name)
        {
            Dictionary<string, string> header = new Dictionary<string, string>();
            header.Add("Cookie", cookie);
            Dictionary<string, string> body = new Dictionary<string, string>();
            body.Add("ie", "utf-8");
            body.Add("kw", name);
            body.Add("tbs", Get_tbs(cookie));
            return JsonConvert.DeserializeObject<Response_Tieba_Sign>(HTTP_POST(url_sign, body, header, 1));
        }

        public static string Get_tbs(string cookie)
        {
            Dictionary<string, string> header = new Dictionary<string, string>();
            header.Add("Cookie", cookie);
            return JsonConvert.DeserializeObject<Response_Tbs>(HTTP_GET(url_tbs, header)).tbs;
        }

        /// <summary>
        /// 取两端字符串中间的字符串
        /// </summary>
        /// <param name="source"></param>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <param name="position"></param>
        /// <returns></returns>
        public static string Mid(string source, string left, string right, int position = 0)
        {
            int start, end;
            string result;
            source = source.Substring(position, source.Length - position);
            start = source.IndexOf(left) + left.Length;
            if (start == -1 + left.Length)
            {
                return "*nothing*";
            }
            source = source.Substring(start, source.Length - start);
            end = source.IndexOf(right);
            if (end == -1)
            {
                return "*nothing*";
            }
            result = source.Substring(0, end);
            return result;
        }

        /// <summary>
        /// 发送GET请求 带请求头
        /// </summary>
        /// <param name="url"></param>
        /// <param name="header"></param>
        /// <returns></returns>
        public static string HTTP_GET(string url, Dictionary<string, string> header = null)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            foreach (KeyValuePair<string, string> i in header)
            {
                request.Headers[i.Key] = i.Value;
            }
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            Stream responseStream = response.GetResponseStream();
            StreamReader streamReader = new StreamReader(responseStream, Encoding.UTF8);
            string str = streamReader.ReadToEnd();
            return str;
        }

        /// <summary>
        /// 发送POST请求 带请求头、请求体
        /// </summary>
        /// <param name="url"></param>
        /// <param name="body"></param>
        /// <param name="header"></param>
        /// <param name="datatype"></param>
        /// <returns></returns>
        public static string HTTP_POST(string url, Dictionary<string, string> body, Dictionary<string, string> header = null, int datatype = 0)
        {
            if (datatype == 0)
            {
                string param = JsonConvert.SerializeObject(body);
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST";
                request.ContentType = "application/json;charset=UTF-8";
                if (header != null && header.Count != 0)
                {
                    foreach (var item in header)
                    {
                        request.Headers.Add(item.Key, item.Value);
                    }
                }
                byte[] payload = Encoding.UTF8.GetBytes(param);
                request.ContentLength = payload.Length;
                string strValue = "";
                try
                {
                    Stream writer = request.GetRequestStream();
                    writer.Write(payload, 0, payload.Length);
                    writer.Close();
                    HttpWebResponse response;
                    response = (HttpWebResponse)request.GetResponse();
                    Stream s;
                    s = response.GetResponseStream();
                    string StrDate = "";
                    StreamReader Reader = new StreamReader(s, Encoding.UTF8);
                    while ((StrDate = Reader.ReadLine()) != null)
                    {
                        strValue += StrDate;
                    }
                }
                catch (Exception e)
                {
                    strValue = e.Message;
                }
                return strValue;
            }
            else if (datatype == 1)
            {
                string PostData = "";
                foreach (KeyValuePair<string, string> i in body)
                {
                    if (PostData != "") PostData += "&";
                    PostData += i.Key + "=" + i.Value;
                }
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST";
                request.ContentType = "application/x-www-form-urlencoded;charset=UTF-8";
                if (header != null && header.Count != 0)
                {
                    foreach (var item in header)
                    {
                        request.Headers.Add(item.Key, item.Value);
                    }
                }
                byte[] byteArray = Encoding.UTF8.GetBytes(PostData);
                request.ContentLength = byteArray.Length;
                using (Stream newStream = request.GetRequestStream())
                {
                    newStream.Write(byteArray, 0, byteArray.Length);//写入参数
                    newStream.Close();
                }
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                string EndResult = "";
                Stream rspStream = response.GetResponseStream();
                using (StreamReader reader = new StreamReader(rspStream, Encoding.UTF8))
                {
                    EndResult = reader.ReadToEnd();
                    rspStream.Close();
                }
                response.Close();
                return EndResult;
            }
            return "";
        }
    }

    #region 贴吧信息实体类

    public class Tieba_Info
    {
        public int no { get; set; }
        public string error { get; set; }
        public Data_Tieba data { get; set; }
    }

    public class Data_Tieba
    {
        public int uid { get; set; }
        public string tbs { get; set; }
        public string itb_tbs { get; set; }
        public Like_Forum[] like_forum { get; set; }
    }

    public class Like_Forum
    {
        public string forum_name { get; set; }
        public string user_level { get; set; }
        public string user_exp { get; set; }
        public int forum_id { get; set; }
        public bool is_like { get; set; }
        public int favo_type { get; set; }
        public int is_sign { get; set; }
    }

    #endregion

    #region tbs实体类

    public class Response_Tbs
    {
        public string tbs { get; set; }
        public int is_login { get; set; }
    }

    #endregion

    #region 签到实体类

    public class Response_Tieba_Sign
    {
        public int no { get; set; }
        public string error { get; set; }
        public Data data { get; set; }
    }

    public class Data
    {
        public int errno { get; set; }
        public string errmsg { get; set; }
        public int sign_version { get; set; }
        public int is_block { get; set; }
        public Finfo finfo { get; set; }
        public Uinfo uinfo { get; set; }
    }

    public class Finfo
    {
        public Forum_Info forum_info { get; set; }
        public Current_Rank_Info current_rank_info { get; set; }
    }

    public class Forum_Info
    {
        public int forum_id { get; set; }
        public string forum_name { get; set; }
    }

    public class Current_Rank_Info
    {
        public int sign_count { get; set; }
    }

    public class Uinfo
    {
        public int user_id { get; set; }
        public int is_sign_in { get; set; }
        public int user_sign_rank { get; set; }
        public int sign_time { get; set; }
        public int cont_sign_num { get; set; }
        public int total_sign_num { get; set; }
        public int cout_total_sing_num { get; set; }
        public int hun_sign_num { get; set; }
        public int total_resign_num { get; set; }
        public int is_org_name { get; set; }
    }

    #endregion

}