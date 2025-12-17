using System.Data;
using System.Text;
using Microsoft.Data.SqlClient;

namespace INVOICE_VENDER_API.Contexts
{
    public class ClsHelper
    {

        private SqlConnectDB dbSCM = new SqlConnectDB("dbSCM");
        //public string GenRunningRegis(string perfix)
        //{
        //   string sql = @"
        //        SELECT TOP 1 USECODE
        //        FROM [dbSCM].[dbo].[INV_AuthenRegis]
        //        WHERE USERCODE LIKE @perfix + '%'
        //        ORDER BY USERCODE DESC";

        //    SqlCommand cmd = new SqlCommand(sql);
        //    cmd.Parameters.AddWithValue("@perfix", perfix);

        //    DataTable dt = dbSCM.Query(cmd);

        //    int nextNumber = 1;

        //    if (dt.Rows.Count > 0)
        //    {
        //        string lastCode = dt.Rows[0]["USERCODE"].ToString();
        //        string numberPart = lastCode.Replace(perfix, ""); //ตัด perfix ออกก่อน
        //        nextNumber = int.Parse(numberPart)+1;
        //    }

        //    return $"{perfix}{nextNumber.ToString("D6")}";
        //}
    }
}
