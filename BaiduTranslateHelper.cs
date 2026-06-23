using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace OCRTranslator
{
    public class BaiduTranslateHelper
    {
        private const string _encodedAppId = "MjAyNjA2MDEwMDI2MjQwMTc=";
        private const string _encodedSecretKey = "RmlMYnFpWktLSmowcFFXcm00aTQ=";
        
        private const string GENERAL_API_URL = "https://fanyi-api.baidu.com/api/trans/vip/translate";
        private const string MT_API_URL = "https://fanyi-api.baidu.com/api/trans/vip/fieldtranslate";

        private static string GetAppId()
        {
            byte[] data = Convert.FromBase64String(_encodedAppId);
            return Encoding.UTF8.GetString(data);
        }

        private static string GetSecretKey()
        {
            byte[] data = Convert.FromBase64String(_encodedSecretKey);
            return Encoding.UTF8.GetString(data);
        }

        public static string TestConnection()
        {
            try
            {
                string result = Translate("hello", "en", "zh", "general");
                return "翻译测试成功: " + result;
            }
            catch (Exception ex)
            {
                return "翻译测试失败: " + ex.Message;
            }
        }

        public static string Translate(string text, string fromLang, string toLang, string translateType = "general")
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            try
            {
                string salt = DateTime.Now.Ticks.ToString();
                string sign = GenerateSign(GetAppId(), text, salt, GetSecretKey());

                string apiUrl = (translateType == "mt") ? MT_API_URL : GENERAL_API_URL;

                string queryString = string.Format(
                    "q={0}&from={1}&to={2}&appid={3}&salt={4}&sign={5}",
                    HttpUtility.UrlEncode(text),
                    fromLang,
                    toLang,
                    GetAppId(),
                    salt,
                    sign
                );

                string rawResponse = HttpRequest(apiUrl, queryString);
                
                // 检查错误
                string errorCode = GetErrorCode(rawResponse);
                if (!string.IsNullOrEmpty(errorCode) && errorCode != "52000")
                {
                    throw new Exception("翻译API错误码: " + errorCode + ", 错误信息: " + GetErrorMsg(rawResponse));
                }

                // 解析翻译结果
                string translated = ParseResponse(rawResponse);
                
                if (string.IsNullOrEmpty(translated))
                {
                    throw new Exception("翻译结果为空");
                }
                
                // 如果翻译结果与原文相同，说明API未翻译
                if (translated.Trim() == text.Trim())
                {
                    // 尝试用大模型翻译
                    if (translateType != "mt")
                    {
                        return Translate(text, fromLang, toLang, "mt");
                    }
                }
                
                return translated;
            }
            catch (Exception ex)
            {
                throw new Exception("翻译失败: " + ex.Message);
            }
        }

        private static string HttpRequest(string url, string postData)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.UserAgent = "Mozilla/5.0";
            request.Timeout = 30000;
            request.ServicePoint.Expect100Continue = false;
            
            byte[] data = Encoding.UTF8.GetBytes(postData);
            request.ContentLength = data.Length;
            
            using (Stream reqStream = request.GetRequestStream())
            {
                reqStream.Write(data, 0, data.Length);
            }

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        private static string ParseResponse(string json)
        {
            if (string.IsNullOrEmpty(json))
                return "";

            // 解码Unicode
            json = DecodeUnicode(json);

            // 提取所有dst值
            List<string> dstList = new List<string>();
            int searchFrom = 0;
            
            while (true)
            {
                int dstIndex = json.IndexOf("\"dst\"", searchFrom);
                if (dstIndex < 0)
                    break;

                // 找冒号位置
                int colonIndex = json.IndexOf(":", dstIndex);
                if (colonIndex < 0)
                    break;

                // 找字符串开始
                int startIndex = json.IndexOf("\"", colonIndex);
                if (startIndex < 0)
                    break;
                startIndex++; // 跳过开始引号

                // 找字符串结束
                int endIndex = json.IndexOf("\"", startIndex);
                if (endIndex < 0)
                    break;

                string dstValue = json.Substring(startIndex, endIndex - startIndex);
                dstList.Add(dstValue);

                searchFrom = endIndex + 1;
            }

            if (dstList.Count == 0)
                return "";

            return string.Join("\r\n", dstList.ToArray());
        }

        private static string DecodeUnicode(string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;

            return Regex.Replace(str, @"\\u([0-9a-fA-F]{4})", match =>
            {
                try
                {
                    int code = int.Parse(match.Groups[1].Value, System.Globalization.NumberStyles.HexNumber);
                    if (code >= 0 && code < 0xFFFF)
                        return char.ConvertFromUtf32(code);
                }
                catch { }
                return match.Value;
            });
        }

        private static string GetErrorCode(string json)
        {
            int pos = json.IndexOf("error_code");
            if (pos < 0) return "";
            int colon = json.IndexOf(":", pos);
            if (colon < 0) return "";
            int quote = json.IndexOf("\"", colon);
            if (quote < 0) return "";
            int endQuote = json.IndexOf("\"", quote + 1);
            if (endQuote < 0) return "";
            return json.Substring(quote + 1, endQuote - quote - 1);
        }

        private static string GetErrorMsg(string json)
        {
            int pos = json.IndexOf("error_msg");
            if (pos < 0) return "";
            int colon = json.IndexOf(":", pos);
            if (colon < 0) return "";
            int quote = json.IndexOf("\"", colon);
            if (quote < 0) return "";
            int endQuote = json.IndexOf("\"", quote + 1);
            if (endQuote < 0) return "";
            return json.Substring(quote + 1, endQuote - quote - 1);
        }

        private static string GenerateSign(string appId, string query, string salt, string secretKey)
        {
            string str = appId + query + salt + secretKey;
            using (MD5 md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(str);
                byte[] hashBytes = md5.ComputeHash(inputBytes);
                StringBuilder sb = new StringBuilder();
                foreach (byte b in hashBytes)
                    sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        public static string[] ParseLanguagePair(string pair)
        {
            string[] parts = pair.Split(new string[] { "2" }, StringSplitOptions.None);
            if (parts.Length == 2)
                return parts;
            return new string[] { "en", "zh" };
        }
    }
}
