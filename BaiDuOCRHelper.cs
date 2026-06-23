using System;
using System.Collections.Specialized;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace OCRTranslator
{
    public class BaiDuOCRHelper
    {
        // Base64编码后的API密钥（运行时解码）
        // IgNP9lkrBqPt4wAAYS8IHWmS -> SWdOUDlsa3JCcVB0NHdBQVlTOElIV21T
        // rYgZSksQAxexFOARR2YFbGXAlSzyv4ZC -> cllnWlNrc1FBeGV4Rk9BUlIyWUZiR1hBbFN6eXY0WkM=
        private const string _encodedApiKey = "SWdOUDlsa3JCcVB0NHdBQVlTOElIV21T";
        private const string _encodedSecretKey = "cllnWlNrc1FBeGV4Rk9BUlIyWUZiR1hBbFN6eXY0WkM=";
        
        private static string _accessToken = null;
        private static DateTime _tokenExpiry = DateTime.MinValue;

        private static string GetApiKey()
        {
            byte[] data = Convert.FromBase64String(_encodedApiKey);
            return Encoding.UTF8.GetString(data);
        }

        private static string GetSecretKey()
        {
            byte[] data = Convert.FromBase64String(_encodedSecretKey);
            return Encoding.UTF8.GetString(data);
        }

        private static readonly string[] OCR_ENDPOINTS = new string[]
        {
            "general_basic", "accurate", "general", "handwriting", "receipt",
            "webimage", "doc_analysis_office", "driving_license", "idcard", "qrcode", "seal", "numbers", "form"
        };

        public static string TestToken()
        {
            try
            {
                string token = GetAccessToken();
                return "Token成功: " + token.Substring(0, 30) + "...";
            }
            catch (Exception ex)
            {
                return "Token失败: " + ex.Message;
            }
        }

        public static string RecognizeText(Bitmap bitmap, string languageType = "CHN_ENG", string modelType = "general")
        {
            if (bitmap == null)
                throw new Exception("图片为空");

            try
            {
                byte[] jpegBytes = BitmapToJpegBytes(bitmap);
                string accessToken = GetAccessToken();
                
                int modelIndex = GetModelIndex(modelType);
                string ocrEndpoint = OCR_ENDPOINTS[modelIndex];
                string ocrUrl = "https://aip.baidubce.com/rest/2.0/ocr/v1/" + ocrEndpoint + "?access_token=" + accessToken;

                using (WebClient client = new WebClient())
                {
                    client.Encoding = Encoding.UTF8;
                    client.Proxy = null;
                    client.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";                    
                    
                    NameValueCollection data = new NameValueCollection();
                    data["image"] = Convert.ToBase64String(jpegBytes);
                    data["language_type"] = languageType;
                    
                    byte[] response = client.UploadValues(ocrUrl, "POST", data);
                    string result = Encoding.UTF8.GetString(response);

                    string errorCode = GetJsonValue(result, "error_code");
                    string errorMsg = GetJsonValue(result, "error_msg");                    
                    if (!string.IsNullOrEmpty(errorCode))
                    {
                        throw new Exception("OCR错误 " + errorCode + ": " + errorMsg);
                    }

                    StringBuilder sb = new StringBuilder();
                    
                    int wordsResultStart = result.IndexOf("words_result");
                    if (wordsResultStart > 0)
                    {
                        int arrayStart = result.IndexOf("[", wordsResultStart);
                        int arrayEnd = result.LastIndexOf("]");
                        
                        if (arrayStart >= 0 && arrayEnd > arrayStart)
                        {
                            string wordsArray = result.Substring(arrayStart + 1, arrayEnd - arrayStart - 1);
                            
                            foreach (string item in ParseJsonArray(wordsArray))
                            {
                                string words = GetJsonValue(item, "words");
                                if (!string.IsNullOrEmpty(words))
                                    sb.AppendLine(words);
                            }
                        }
                    }
                    else
                    {
                        string directText = ExtractDirectText(result);
                        if (!string.IsNullOrEmpty(directText))
                            sb.Append(directText);
                    }

                    string text = sb.ToString().Trim();
                    if (string.IsNullOrEmpty(text))
                        throw new Exception("未识别到文字内容");
                    
                    return text;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("OCR识别失败: " + ex.Message);
            }
        }

        private static int GetModelIndex(string modelType)
        {
            switch (modelType.ToLower())
            {
                case "general": return 0;
                case "accurate": return 1;
                case "handwriting": return 3;
                case "receipt": return 4;
                case "webimage": return 5;
                default: return 0;
            }
        }

        private static string ExtractDirectText(string json)
        {
            try
            {
                string pattern = "\"text\"\\s*:\\s*\"([^\"]+)\"";
                var match = System.Text.RegularExpressions.Regex.Match(json, pattern);
                if (match.Success)
                    return match.Groups[1].Value;
            }
            catch { }
            return "";
        }

        private static byte[] BitmapToJpegBytes(Bitmap bitmap)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                int maxSize = 1500;
                Bitmap toSave = bitmap;
                
                if (bitmap.Width > maxSize || bitmap.Height > maxSize)
                {
                    double ratio = Math.Min((double)maxSize / bitmap.Width, (double)maxSize / bitmap.Height);
                    int newWidth = (int)(bitmap.Width * ratio);
                    int newHeight = (int)(bitmap.Height * ratio);
                    
                    toSave = new Bitmap(newWidth, newHeight);
                    using (Graphics g = Graphics.FromImage(toSave))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        g.DrawImage(bitmap, 0, 0, newWidth, newHeight);
                    }
                }
                
                toSave.Save(ms, ImageFormat.Jpeg);
                
                if (toSave != bitmap)
                    toSave.Dispose();
                
                return ms.ToArray();
            }
        }

        private static string[] ParseJsonArray(string arrayStr)
        {
            System.Collections.Generic.List<string> items = new System.Collections.Generic.List<string>();
            int braceCount = 0;
            int start = -1;
            
            for (int i = 0; i < arrayStr.Length; i++)
            {
                if (arrayStr[i] == '{')
                {
                    if (braceCount == 0) start = i;
                    braceCount++;
                }
                else if (arrayStr[i] == '}')
                {
                    braceCount--;
                    if (braceCount == 0 && start >= 0)
                    {
                        items.Add(arrayStr.Substring(start, i - start + 1));
                        start = -1;
                    }
                }
            }
            
            return items.ToArray();
        }

        private static string GetAccessToken()
        {
            if (_accessToken != null && DateTime.Now < _tokenExpiry)
                return _accessToken;

            string tokenUrl = "https://aip.baidubce.com/oauth/2.0/token?grant_type=client_credentials&client_id=" + GetApiKey() + "&client_secret=" + GetSecretKey();
            
            using (WebClient client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                client.Proxy = null;
                string json = client.UploadString(tokenUrl, "POST", "");
                
                _accessToken = GetJsonValue(json, "access_token");
                
                if (string.IsNullOrEmpty(_accessToken))
                    throw new Exception("获取Token失败");
                
                int expiresIn = 2592000;
                int.TryParse(GetJsonValue(json, "expires_in"), out expiresIn);
                _tokenExpiry = DateTime.Now.AddSeconds(expiresIn - 300);
                
                return _accessToken;
            }
        }

        private static string GetJsonValue(string json, string key)
        {
            try
            {
                string pattern = "\"" + key + "\"" + @"\s*:\s*(?:""([^""]*)""([^,}\r\n]*)|(\S+))";
                Match match = Regex.Match(json, pattern);
                if (match.Success)
                {
                    if (!string.IsNullOrEmpty(match.Groups[1].Value))
                        return match.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(match.Groups[3].Value))
                        return match.Groups[3].Value.Trim().Trim('"', ',', ' ', '\r', '\n');
                }
            }
            catch { }
            return "";
        }
    }
}
