using System.Data;
using System.Text;
using Microsoft.Data.SqlClient;

namespace INVOICE_VENDER_API.Contexts
{
    public class ClsHelper
    {

        public string GenRunningNumber(string prefix, int idx)
        {
            Random rand = new Random();
            string nxt = rand.Next(1, 99999).ToString("00000");

            char[] chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray();
            Random random = new Random();

            int length = 6;
            StringBuilder stringBuilder = new StringBuilder();

            for (int i = 0; i < length; i++)
            {
                int index = random.Next(chars.Length);
                stringBuilder.Append(chars[index]);
            }

            return $"{prefix}{DateTime.Now.ToString("yyyMMdd")}{stringBuilder.ToString()}{nxt}{DateTime.Now.ToString("fffff")}{idx.ToString("00000")}";
        }
    }
}
