using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace CrediFlow.Common.Utils
{
    public static class CommonLib
    {
        public static string ConvertObjectToJson(object obj)
        {
            return JsonConvert.SerializeObject(obj, new JsonSerializerSettings
            {
                ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy() },
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                Formatting = Formatting.Indented
            });
        }

        public static Guid? GetGUID(Guid? ipGUID)
        {
            return ipGUID != null && ipGUID != Guid.Empty ? ipGUID : Guid.Empty;
        }

        public static string ToMD5(string str)
        {
            //Encoder encoder = Encoding.Unicode.GetEncoder();
            byte[] array = new byte[str.Length * 2];
            //encoder.GetBytes(str.ToCharArray(), 0, str.Length, array, 0, flush: true);
            //MD5 mD = new MD5CryptoServiceProvider();
            //byte[] array2 = mD.ComputeHash(array);
            //StringBuilder stringBuilder = new StringBuilder();
            //byte[] array3 = array2;
            //foreach (byte b in array3)
            //{
            //    stringBuilder.Append(b.ToString("X2"));
            //}

            //return stringBuilder.ToString();
            using (MD5 mD = MD5.Create())
            {
                byte[] array2 = mD.ComputeHash(array);
                StringBuilder stringBuilder = new StringBuilder();
                byte[] array3 = array2;
                foreach (byte b in array3)
                {
                    stringBuilder.Append(b.ToString("X2"));
                }

                return stringBuilder.ToString();
            }
        }
        
        /// <summary>
        /// Lấy DateTime hiện tại Universal
        /// </summary>
        public static DateTime NowUniversal() => DateTime.Now;
    }
}
