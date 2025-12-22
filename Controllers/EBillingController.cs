using System.Data;
using Microsoft.Data.SqlClient;
using INVOICE_VENDER_API.Contexts;
using INVOICE_VENDER_API.Models;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;

namespace INVOICEBILLINENOTE_API.Controllers
{
    [ApiController]
    [Route("api/ebilling")]
    public class EBillingController : ControllerBase
    {

        private ClsHelper oHelper = new ClsHelper();
        private SqlConnectDB dbSCM = new SqlConnectDB("dbSCM");
        private SqlConnectDB dbHRM = new SqlConnectDB("dbHRM");
        private OraConnectDB oOraAL02 = new OraConnectDB("ALPHA02");


        [HttpGet]
        [Route("vdname")]
        public ActionResult VenderName()
        {
            List<DataVender> listvd = new List<DataVender>();

            OracleCommand datavdCmd = new OracleCommand();
            datavdCmd.CommandText = @"SELECT VENDER, VDNAME, TLXNO, VDABBR, ADDR1, ADDR2 FROM DST_ACMVD1";

            DataTable dtVd = oOraAL02.Query(datavdCmd);
            if (dtVd.Rows.Count > 0)
            {
                foreach (DataRow row in dtVd.Rows)
                {
                    DataVender VdData = new DataVender();
                    VdData.VENDER = row["VENDER"].ToString();
                    VdData.VDNAME = row["VDNAME"].ToString();
                    VdData.TLXNO = row["TLXNO"].ToString();
                    VdData.VDABBR = row["VDABBR"].ToString();
                    VdData.ADDR1 = row["ADDR1"].ToString();
                    VdData.ADDR2 = row["ADDR2"].ToString();

                    listvd.Add(VdData);
                }
            }

            return Ok(listvd);
        }


        [HttpGet]
        [Route("backaccount")]
        public ActionResult BackAccount()
        {
            List<BankAccount> databank = new List<BankAccount>();

            SqlCommand bankaccCmd = new SqlCommand();
            bankaccCmd.CommandText = @"
                SELECT DICTKEYNO, DICTREFNO, DICTTITLE
	            FROM [dbSCM].[dbo].[EBULLING_DICT]
	            WHERE DICTTYPE = 'PV_MSTACC'";

            DataTable dtbank = dbSCM.Query(bankaccCmd);
            if (dtbank.Rows.Count > 0)
            {
                foreach (DataRow row in dtbank.Rows)
                {
                    BankAccount BankData = new BankAccount();
                    BankData.ACCOUNTCODE = row["DICTKEYNO"].ToString();
                    BankData.ACCOUNT = row["DICTREFNO"].ToString();
                    BankData.ACCOUNTNAME = row["DICTTITLE"].ToString();

                    databank.Add(BankData);
                }
            }

            return Ok(databank);
        }

        [HttpPost]
        [Route("cldbilling")]
        public IActionResult CldBilling([FromBody] CalendarBilling mParam)
        {
            int res = 0;
            string msg = "";

            try
            {
                if (string.IsNullOrEmpty(mParam.BillingStart) || string.IsNullOrEmpty(mParam.BillingEnd))
                {
                    res = -1;
                    msg = "กรูณาเลือกวันวางบิล";
                    return Ok(new { result = res, message = msg });
                }

                SqlCommand cldbillingCmd = new SqlCommand();
                cldbillingCmd.CommandText = @"
                    INSERT INTO [dbSCM].[dbo].[EBILLING_CALENDAR]
                    (CLDNO, CLDYEAR, CLDMONTH, BILLING_START, BILLING_END, PAYMENT_START, PAYMENT_END, CRBY, CRDATE)
                    VALUES
                    (@CLDNO, @CLDYEAR, @CLDMONTH, @BILLING_START, @BILLING_END, @PAYMENT_START, @PAYMENT_END, @CRBY, GETDATE())";

                // Gen CldNo
                cldbillingCmd.Parameters.AddWithValue("@CLDNO", oHelper.GenRunningNumber("CLD-BILL-", (DateTime.Now.Hour * DateTime.Now.Minute * DateTime.Now.Second)));

                // แก้ชื่อ parameter ให้ตรง
                cldbillingCmd.Parameters.AddWithValue("@CLDYEAR", mParam.CldYear);
                cldbillingCmd.Parameters.AddWithValue("@CLDMONTH", mParam.CldMonth);
                cldbillingCmd.Parameters.AddWithValue("@BILLING_START", mParam.BillingStart);
                cldbillingCmd.Parameters.AddWithValue("@BILLING_END", mParam.BillingEnd);

                // เพิ่ม Payment fields
                cldbillingCmd.Parameters.AddWithValue(
                     "@PAYMENT_START",
                     string.IsNullOrEmpty(mParam.PaymentStart) ? (object)DBNull.Value : mParam.PaymentStart
                );
                cldbillingCmd.Parameters.AddWithValue(
                    "@PAYMENT_END",
                    string.IsNullOrEmpty(mParam.PaymentEnd) ? (object)DBNull.Value : mParam.PaymentEnd
                );


                cldbillingCmd.Parameters.AddWithValue("@CRBY", mParam.CrBy);


                dbSCM.ExecuteCommand(cldbillingCmd);

                res = 1;
                msg = "success";

            }
            catch (Exception ex)
            {
                res = -3;
                msg = ex.Message;
            }

            return Ok(new { resulะ = res, message = msg });
        }

        //[HttpGet]
        //[Route("listcalendar")]
        //public ActionResult ListCalendar()
        //{
        //    List<Calendar> listcld= new List<Calendar>();

         //   SqlCommand calendarCmd = new SqlCommand();
         //   calendarCmd.CommandText = @""
        //}
    }
}
