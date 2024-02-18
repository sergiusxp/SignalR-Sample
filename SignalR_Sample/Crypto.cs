using System.Security.Cryptography;
using System.Text;

namespace SignalR_Sample
{
    public static class Crypto
    {
        public static string CalculateSHA1(string text)
        {
            using (SHA1 sha1 = SHA1.Create())
            {
                byte[] textBytes = Encoding.UTF8.GetBytes(text);
                byte[] hashBytes = sha1.ComputeHash(textBytes);

                StringBuilder sb = new StringBuilder();
                foreach (byte b in hashBytes)
                {
                    sb.Append(b.ToString("x2"));
                }

                return sb.ToString();
            }
        }

        public static string GetRandomGuid()
        {
            Guid newGuid = Guid.NewGuid();
            return newGuid.ToString();
        }
    }
}
