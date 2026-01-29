using System.Data;
using Microsoft.Data.SqlClient;
using INVOICE_VENDER_API.Contexts;
using INVOICE_VENDER_API.Models;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Net.Mail;
//using System.Data.SqlClient;

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
            datavdCmd.CommandText = @"SELECT VENDER, VDNAME, TAXID, VDABBR, ADDR1, ADDR2 FROM DST_ACMVD1";

            DataTable dtVd = oOraAL02.Query(datavdCmd);
            if (dtVd.Rows.Count > 0)
            {
                foreach (DataRow row in dtVd.Rows)
                {
                    DataVender VdData = new DataVender();
                    VdData.VENDER = row["VENDER"].ToString();
                    VdData.VDNAME = row["VDNAME"].ToString();
                    VdData.TLXNO = row["TAXID"].ToString();
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
	            FROM [dbSCM].[dbo].[EBILLING_DICT]
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
        public IActionResult CldBilling([FromBody] CreateCalendar mParam)
        {
            int res = 0;
            string msg = "";

            try
            {

                if (string.IsNullOrEmpty(mParam.CldType))
                {
                    res = -1;
                    msg = "กรูณาเลือกประเภทในการสร้างCalendar";
                    return Ok(new { reslut = res, message = msg });
                }

                if (string.IsNullOrEmpty(mParam.DateStart) || string.IsNullOrEmpty(mParam.DateEnd))
                {
                    res = -2;
                    msg = "กรูณาเลือกวันวางบิล";
                    return Ok(new { result = res, message = msg });
                }



                DateTime startDate = DateTime.Parse(mParam.DateStart);
                DateTime endDate = DateTime.Parse(mParam.DateEnd);

                if (startDate > endDate)
                {
                    res = -4;
                    msg = "วันที่เริ่มต้นต้องไม่มากกว่าวันที่สิ้นสุด";
                    return Ok(new { result = res, message = msg });
                }


                string cldcode = $"CLD-{mParam.CldYear}{mParam.CldMonth}";
                string description = mParam.CldType switch
                {
                    "billing" => "วันที่วางบิล",
                    "payment" => "วันที่จ่ายเงิน",
                    "cutoff" => "วันสุดท้ายของการส่งเอกสาร",
                    "vacation" => "วันหยุดบริษัท",
                    _ => "อื่นๆ",
                };


                SqlCommand chkCmd = new SqlCommand();
                chkCmd.CommandText = @"
                    SELECT CLDNO
                        ,CLDYEAR
                        ,CLDMONTH
                        ,CLDCODE
                    FROM [dbSCM].[dbo].[EBILLING_CALENDAR]
                    WHERE CLDYEAR = @CLDYEAR AND CLDMONTH = @CLDMONTH";

                chkCmd.Parameters.AddWithValue("@CLDYEAR", mParam.CldYear);
                chkCmd.Parameters.AddWithValue("@CLDMONTH", mParam.CldMonth);

                DataTable dtCld = dbSCM.Query(chkCmd);

                if (dtCld.Rows.Count == 0)
                {
                    SqlCommand insHeader = new SqlCommand();
                    insHeader.CommandText = @"
                            INSERT INTO [dbSCM].[dbo].[EBILLING_CALENDAR]
                            (CLDNO, CLDCODE, CLDYEAR, CLDMONTH, CRBY, CRDATE)
                            VALUES
                            (@CLDNO, @CLDCODE, @CLDYEAR, @CLDMONTH, @CRBY, GETDATE())";

                    insHeader.Parameters.AddWithValue("@CLDNO", oHelper.GenRunningNumber("CLD-HEAD-", (DateTime.Now.Hour * DateTime.Now.Minute * DateTime.Now.Second)));
                    insHeader.Parameters.AddWithValue("@CLDCODE", cldcode);
                    insHeader.Parameters.AddWithValue("@CLDYEAR", mParam.CldYear);
                    insHeader.Parameters.AddWithValue("@CLDMONTH", mParam.CldMonth);
                    insHeader.Parameters.AddWithValue("@CRBY", mParam.CrBy);

                    dbSCM.ExecuteCommand(insHeader);

                    SqlCommand eventcldCmd = new SqlCommand();
                    eventcldCmd.CommandText = @"
                            INSERT INTO [dbSCM].[dbo].[EBILLING_CALENDAR_EVENT]
                            (EVENTID, CLDCODE, EVENT_TYPE, START_DATE, END_DATE, DESCRIPTION)
                            VALUES
                            (@EVENTID, @CLDCODE, @EVENT_TYPE, @START_DATE, @END_DATE, @DESCRIPTION)";

                    eventcldCmd.Parameters.AddWithValue("@EVENTID", oHelper.GenRunningNumber("CLD-EVENT-", (DateTime.Now.Hour * DateTime.Now.Minute * DateTime.Now.Second)));
                    eventcldCmd.Parameters.AddWithValue("@CLDCODE", cldcode);
                    eventcldCmd.Parameters.AddWithValue("@EVENT_TYPE", mParam.CldType);
                    eventcldCmd.Parameters.AddWithValue("@START_DATE", mParam.DateStart);
                    eventcldCmd.Parameters.AddWithValue("@END_DATE", mParam.DateEnd);
                    eventcldCmd.Parameters.AddWithValue("@DESCRIPTION", description);

                    dbSCM.ExecuteCommand(eventcldCmd);

                    res = 1;
                    msg = $"สร้าาง {description} เรียบร้อย";
                }
                else
                {
                    SqlCommand chkeventCmd = new SqlCommand();
                    chkeventCmd.CommandText = @"
                        SELECT CLDCODE, EVENT_TYPE, START_DATE, END_DATE, DESCRIPTION
                        FROM [dbSCM].[dbo].[EBILLING_CALENDAR_EVENT]
                        WHERE CLDCODE = @CLDCODE AND EVENT_TYPE = @EVENT_TYPE";

                    chkeventCmd.Parameters.AddWithValue("@CLDCODE", cldcode);
                    chkeventCmd.Parameters.AddWithValue("@EVENT_TYPE", mParam.CldType);

                    DataTable eventdata = dbSCM.Query(chkeventCmd);

                    if (eventdata.Rows.Count > 0)
                    {
                        SqlCommand upeventCmd = new SqlCommand();
                        upeventCmd.CommandText = @"
                            UPDATE [dbSCM].[dbo].[EBILLING_CALENDAR_EVENT]
                            SET
                                START_DATE = @START_DATE,
                                END_DATE = @END_DATE
                            WHERE CLDCODE = @CLDCODE AND EVENT_TYPE = @EVENT_TYPE";

                        upeventCmd.Parameters.AddWithValue("@CLDCODE", cldcode);
                        upeventCmd.Parameters.AddWithValue("@EVENT_TYPE", mParam.CldType);

                        dbSCM.ExecuteCommand(upeventCmd);

                        res = 1;
                        msg = $"อัพเดพ {description} เรียบร้อย";

                    }
                    else
                    {
                        SqlCommand upcldevent = new SqlCommand();
                        upcldevent.CommandText = @"
                                INSERT INTO [dbSCM].[dbo].[EBILLING_CALENDAR_EVENT]
                                (EVENTID, CLDCODE, EVENT_TYPE, START_DATE, END_DATE, DESCRIPTION)
                                VALUES
                                (@EVENTID, @CLDCODE, @EVENT_TYPE, @START_DATE, @END_DATE, @DESCRIPTION)";

                        upcldevent.Parameters.AddWithValue("@EVENTID", oHelper.GenRunningNumber("CLD-EVENT-", (DateTime.Now.Hour * DateTime.Now.Minute * DateTime.Now.Second)));
                        upcldevent.Parameters.AddWithValue("@CLDCODE", cldcode);
                        upcldevent.Parameters.AddWithValue("@EVENT_TYPE", mParam.CldType);
                        upcldevent.Parameters.AddWithValue("@START_DATE", mParam.DateStart);
                        upcldevent.Parameters.AddWithValue("@END_DATE", mParam.DateEnd);
                        upcldevent.Parameters.AddWithValue("@DESCRIPTION", description);

                        dbSCM.ExecuteCommand(upcldevent);

                        res = 1;
                        msg = $"สร้าง {description} เรียบร้อย";

                    }

                }
            }
            catch (Exception ex)
            {
                res = -99;
                msg = ex.Message;
            }

            return Ok(new { result = res, message = msg });
        }


        [HttpGet]
        [Route("calendar")]
        public ActionResult CalendarList([FromQuery] int year)
        {
            List<Calendar> datacld = new List<Calendar>();

            try
            {
                //SqlCommand calendarCmd = new SqlCommand(@"
                //    SELECT BILLING_START, BILLING_END, PAYMENT_START, PAYMENT_END
                //    FROM [dbSCM].[dbo].[EBILLING_CALENDAR]
                //    WHERE CLDYEAR = @CLDYEAR");

                SqlCommand cldCmd = new SqlCommand(@"
                    SELECT cld.CLDCODE, cld.CLDYEAR, cld.CLDMONTH, cldevent.EVENT_TYPE, cldevent.START_DATE, cldevent.END_DATE
                    FROM [dbSCM].[dbo].[EBILLING_CALENDAR] cld
                    INNER JOIN [dbSCM].[dbo].[EBILLING_CALENDAR_EVENT] cldevent
                    ON cld.CLDCODE = cldevent.CLDCODE
                    WHERE cld.CLDYEAR = @CLDYEAR");

                cldCmd.Parameters.AddWithValue("@CLDYEAR", year);
                //calendarCmd.Parameters.AddWithValue("@CLDMONTH", month);

                DataTable dtCld = dbSCM.Query(cldCmd);

                if (dtCld.Rows.Count == 0)
                {
                    return Ok(new { result = -1, message = "ไม่พบevent ในเดือนนี้", data = new List<Calendar>() });
                }

                foreach (DataRow row in dtCld.Rows)
                {
                    Calendar CalendarData = new Calendar();
                    CalendarData.EVENTTYPE = row["EVENT_TYPE"].ToString();
                    CalendarData.STARTDATE = row["START_DATE"] == DBNull.Value ? null : row["START_DATE"].ToString();
                    CalendarData.ENDDATE = row["END_DATE"] == DBNull.Value ? null : row["END_DATE"].ToString();

                    datacld.Add(CalendarData);
                }

                return Ok(new { result = 1, message = "success", data = datacld });
            }
            catch (Exception ex)
            {
                return Ok(new { result = -99, message = ex.Message, data = new List<Calendar>() });
            }
        }

        [HttpGet]
        [Route("typecalendar")]
        public ActionResult TypeCalendar()
        {

            List<Dictionary> dictlist = new List<Dictionary>();

            SqlCommand typecldCmd = new SqlCommand();
            typecldCmd.CommandText = @"
                SELECT DICTTYPE, DICTKEYNO,DICTREFNO, DICTTITLE
                FROM [dbSCM].[dbo].[EBILLING_DICT]
                WHERE DICTTYPE = 'PV_MSTCLD'";

            DataTable dtcld = dbSCM.Query(typecldCmd);
            if (dtcld.Rows.Count > 0)
            {
                foreach (DataRow row in dtcld.Rows)
                {
                    Dictionary DictData = new Dictionary();
                    DictData.DICTTYPE = row["DICTTYPE"].ToString();
                    DictData.DICTKEYNO = row["DICTKEYNO"].ToString();
                    DictData.DICTREFNO = row["DICTREFNO"].ToString();
                    DictData.DICTTITLE = row["DICTTITLE"].ToString();

                    dictlist.Add(DictData);
                }
            }

            return Ok(dictlist);
        }

        [HttpPost]
        [Route("autheninfo")]
        public IActionResult BankInfo([FromBody] EmpName mParam)
        {
            int res = 0;
            string msg = "";

            if (string.IsNullOrEmpty(mParam.Username))
            {
                res = -1;
                msg = "ไม่พบข้อมูลuser";
                return Ok(new { result = res, message = msg });
            }

            SqlCommand bankinfoCmd = new SqlCommand();
            bankinfoCmd.CommandText = @"
                SELECT * FROM [dbSCM].[dbo].[EBILLING_AUTHEN] WHERE USERNAME = @USERNAME";

            bankinfoCmd.Parameters.AddWithValue("@USERNAME", mParam.Username);

            DataTable databankinfo = dbSCM.Query(bankinfoCmd);

            if (databankinfo.Rows.Count > 0)
            {
                DataRow row = databankinfo.Rows[0];

                AuthenInfo authen = new AuthenInfo()
                {
                    COMPANY_NAME = row["COMPANY_NAME"]?.ToString(),
                    EMAIL = row["EMAIL"].ToString(),
                    TAXID = row["TAXID"].ToString(),
                    FAX = row["FAX"].ToString(),
                    TELEPHONE = row["TELEPHONE"].ToString(),
                    COMPANTBRANCH = row["COMPANYBRANCH"].ToString(),
                    ADDRESS = row["ADDRESS"].ToString(),
                    ACCOUNT_NAME = row["ACCOUNT_NAME"].ToString(),
                    ACCOUNT_NUMER = row["ACCOUNT_NUMBER"].ToString(),
                    BANK_NAME = row["BANK_NAME"].ToString(),
                    BANKBRANCH_NAME = row["BANKBRANCH_NAME"].ToString(),
                    BANKBRANCH_NO = row["BANKBRANCH_NO"].ToString()
                };

                res = 1;
                msg = "Successfully";
                return Ok(new { result = res, message = msg, data = authen });
            }

            res = -99;
            msg = "error";
            return Ok(new { result = res, message = msg });

        }

        [HttpPost]
        [Route("accountsetting")]
        public IActionResult AccountSetting([FromBody] CreateVenderInfo mParam)
        {
            int res = 0;
            string msg = "";

            if (mParam == null)
            {
                res = -1;
                msg = "ไม่พบข้อมูลที่ส่งมา";

                return Ok(new { result = res, message = msg });
            }

            SqlCommand vdinfoCmd = new SqlCommand();
            vdinfoCmd.CommandText = @"INSERT INTO [dbSCM].[dbo].[EBILLING_VENDORINFO_LOG] 
                                      (ID, USERNAME, NAME, COMPANYNAME, EMAIL, TAXID, BRANCHNO, FAX, TELEPHONE, ADDRESS, ACCNAME, ACCNO, BANKNAME, BANKBRANCHNAME, BANKBRANCHNO, CRDDATE, STATE)
                                      VALUES
                                      (@ID, @USERNAME, @NAME, @COMPANYNAME, @EMAIL, @TAXID, @BRANCHNO, @FAX, @TELEPHONE, @ADDRESS, @ACCNAME, @ACCNO, @BANKNAME, @BANKBRANCHNAME, @BANKBRANCHNO, GETDATE(), @STATE)";

            vdinfoCmd.Parameters.AddWithValue("@ID", oHelper.GenRunningNumber("CLDVD-NEWINFO-", (DateTime.Now.Hour * DateTime.Now.Minute * DateTime.Now.Second)));
            vdinfoCmd.Parameters.AddWithValue("@USERNAME", mParam.Username);
            vdinfoCmd.Parameters.AddWithValue("@NAME", mParam.Name);
            vdinfoCmd.Parameters.AddWithValue("@COMPANYNAME", mParam.Compname);
            vdinfoCmd.Parameters.AddWithValue("@EMAIL", mParam.Email);
            vdinfoCmd.Parameters.AddWithValue("@TAXID", mParam.TaxID);
            vdinfoCmd.Parameters.AddWithValue("@BRANCHNO", mParam.Branchno);
            vdinfoCmd.Parameters.AddWithValue("@FAX", mParam.Fax);
            vdinfoCmd.Parameters.AddWithValue("@TELEPHONE", mParam.Telephone);
            vdinfoCmd.Parameters.AddWithValue("@ADDRESS", mParam.Address);
            vdinfoCmd.Parameters.AddWithValue("@ACCNAME", mParam.Accountname);
            vdinfoCmd.Parameters.AddWithValue("@ACCNO", mParam.Accountno);
            vdinfoCmd.Parameters.AddWithValue("@BANKNAME", mParam.BName);
            vdinfoCmd.Parameters.AddWithValue("@BANKBRANCHNAME", mParam.BBranchname);
            vdinfoCmd.Parameters.AddWithValue("@BANKBRANCHNO", mParam.BBranchno);
            vdinfoCmd.Parameters.AddWithValue("@STATE", "CREATE");

            dbSCM.ExecuteCommand(vdinfoCmd);

            string mMsgBody = "";
            try
            {
                mMsgBody = System.IO.File.ReadAllText("PatternVendorSetting.html");
            }
            catch (FileNotFoundException e)
            {
                Console.WriteLine(e.ToString());
            }

            //foreach (MEmail oMail in rMailTo)
            //{
            MailMessage mail = new MailMessage();
            SmtpClient SmtpServer = new SmtpClient("smtp.dci.daikin.co.jp");
            SmtpServer.Port = 25;
            SmtpServer.UseDefaultCredentials = false;
            mail.Priority = MailPriority.High;
            mail.Headers.Add("X-Message-Flag", "No Date");
            mail.From = new MailAddress("dci-noreply@dci.daikin.co.jp", "E-Billing");
            mail.Subject = "แจ้งเตือนจากระบบวางบิล vendor มีการ setting ข้อมูลใหม่";
            mail.IsBodyHtml = true;

            //mail.To.Add(oMail.Email);
            mail.To.Add("anuthida.w@dci.daikin.co.jp"); //wating testing and then I will change mail to accountant

            mail.Body = String.Format(mMsgBody, 
                mParam.Username,
                mParam.Name,
                mParam.Compname,
                mParam.Email,
                mParam.TaxID,
                mParam.Branchno,
                mParam.Fax,
                mParam.Telephone,
                mParam.Address,
                mParam.Accountname,
                mParam.Accountno,
                mParam.BName,
                mParam.BBranchname,
                mParam.BBranchno);

            try
            {
                SmtpServer.Send(mail);
                res = 1;
                msg = "successfully";
            }
            catch (Exception ex)
            {
                //Console.WriteLine($"ส่งเมลไม่สำเร็จ: {oMail.Email} - {ex.Message}");
            }

            return Ok(new { result = res, message = msg });
        }

        [HttpPost]
        [Route("confirmaccsetting")]
        public IActionResult ConfrimVenderinfo([FromBody] EmpName mParam)
        {
            int res = 0;
            string msg = "";

            if (string.IsNullOrEmpty(mParam.Username))
            {
                return Ok(new { result = -1, message = "ไม่พบข้อมูล vendor" });
            }

            // ===================== GET VENDOR LOG =====================
            SqlCommand logCmd = new SqlCommand(@"
                SELECT TOP 1 *
                FROM [dbSCM].[dbo].[EBILLING_VENDORINFO_LOG]
                WHERE USERNAME = @USERNAME AND STATE = 'CREATE'
                ORDER BY CRDDATE DESC");
            logCmd.Parameters.AddWithValue("@USERNAME", mParam.Username);

            DataTable logData = dbSCM.Query(logCmd);
            if (logData.Rows.Count == 0)
            {
                return Ok(new { result = -2, message = "ไม่พบข้อมูล vendor log" });
            }

            DataRow rowLog = logData.Rows[0];

            // ===================== GET AUTHEN =====================
            SqlCommand authenCmd = new SqlCommand(@"
                SELECT *
                FROM [dbSCM].[dbo].[EBILLING_AUTHEN]
                WHERE USERNAME = @USERNAME");
            authenCmd.Parameters.AddWithValue("@USERNAME", mParam.Username);

            DataTable authenData = dbSCM.Query(authenCmd);
            if (authenData.Rows.Count == 0)
            {
                return Ok(new { result = -3, message = "ไม่พบข้อมูล Authen" });
            }

            DataRow rowAuthen = authenData.Rows[0];

            // ===================== BUILD UPDATE =====================
            List<string> setList = new List<string>();
            SqlCommand updateCmd = new SqlCommand();

            void CheckAndUpdate(string logCol, string authenCol, string paramName)
            {
                if (rowLog[logCol] == DBNull.Value) return;

                if (rowAuthen[authenCol] == DBNull.Value ||
                    !rowAuthen[authenCol].Equals(rowLog[logCol]))
                {
                    setList.Add($"{authenCol} = @{paramName}");

                    string p = "@" + paramName;
                    if (!updateCmd.Parameters.Contains(p))
                    {
                        updateCmd.Parameters.AddWithValue(p, rowLog[logCol]);
                    }
                }
            }

            // ===================== FIELD MAPPING =====================
            CheckAndUpdate("COMPANYNAME", "COMPANY_NAME", "COMPANY_NAME");
            CheckAndUpdate("NAME", "PERSON_INCHARGE", "PERSON_INCHARGE");
            CheckAndUpdate("EMAIL", "EMAIL", "EMAIL");
            CheckAndUpdate("BRANCHNO", "COMPANYBRANCH", "COMPANYBRANCH");
            CheckAndUpdate("FAX", "FAX", "FAX");
            CheckAndUpdate("TELEPHONE", "TELEPHONE", "TELEPHONE");
            CheckAndUpdate("ADDRESS", "ADDRESS", "ADDRESS");
            CheckAndUpdate("ACCNAME", "ACCOUNT_NAME", "ACCOUNT_NAME");
            CheckAndUpdate("ACCNO", "ACCOUNT_NUMBER", "ACCOUNT_NUMBER");
            CheckAndUpdate("BANKNAME", "BANK_NAME", "BANK_NAME");
            CheckAndUpdate("BANKBRANCHNAME", "BANKBRANCH_NAME", "BANKBRANCH_NAME");
            CheckAndUpdate("BANKBRANCHNO", "BANKBRANCH_NO", "BANKBRANCH_NO");

            // ===================== UPDATE AUTHEN =====================
            if (setList.Count > 0)
            {
                updateCmd.CommandText = $@"
                    UPDATE [dbSCM].[dbo].[EBILLING_AUTHEN]
                    SET {string.Join(", ", setList)}
                    WHERE USERNAME = @USERNAME";
                updateCmd.Parameters.AddWithValue("@USERNAME", mParam.Username);

                dbSCM.ExecuteCommand(updateCmd);
            }

            // ===================== UPDATE LOG STATE =====================
                SqlCommand updateStateCmd = new SqlCommand(@"
                    UPDATE [dbSCM].[dbo].[EBILLING_VENDORINFO_LOG]
                    SET STATE = 'CONFIRMED'
                    WHERE USERNAME = @USERNAME AND STATE = 'CREATE'");
                updateStateCmd.Parameters.AddWithValue("@USERNAME", mParam.Username);
                dbSCM.ExecuteCommand(updateStateCmd);

            // ===================== RELOAD AUTHEN (ใช้ค่าล่าสุด) =====================
            authenCmd.Parameters.Clear();
            authenCmd.Parameters.AddWithValue("@USERNAME", mParam.Username);
            authenData = dbSCM.Query(authenCmd);
            rowAuthen = authenData.Rows[0];

            // ===================== SEND MAIL =====================
            string mailTemplate = "";
            try
            {
                mailTemplate = System.IO.File.ReadAllText("PatternAccConfirm.html");
            }
            catch
            {
                return Ok(new { result = -4, message = "ไม่พบไฟล์ email template" });
            }

            MailMessage mail = new MailMessage();
            SmtpClient smtp = new SmtpClient("smtp.dci.daikin.co.jp", 25);
            smtp.UseDefaultCredentials = false;

            mail.From = new MailAddress("dci-noreply@dci.daikin.co.jp", "E-Billing");
            mail.Subject = "แจ้งเตือนจากระบบวางบิล vendor มีการ setting ข้อมูลใหม่";
            mail.IsBodyHtml = true;
            mail.Priority = MailPriority.High;
            //mail.To.Add(rowLog["EMAIL"].ToString()); ใช้จริงให้เปลี่ยนกลับมาเป้นอันนี้
            mail.To.Add("anuthida.w@dci.daikin.co.jp");

            mail.Body = string.Format(mailTemplate,
                mParam.Username,
                rowLog["COMPANYNAME"],
                rowLog["NAME"],
                rowLog["EMAIL"],
                rowLog["TAXID"],
                rowLog["BRANCHNO"],
                rowLog["FAX"],
                rowLog["TELEPHONE"],
                rowLog["ADDRESS"],
                rowLog["ACCNAME"],
                rowLog["ACCNO"],
                rowLog["BANKNAME"],
                rowLog["BANKBRANCHNAME"],
                rowLog["BANKBRANCHNO"],
                rowAuthen["COMPANY_NAME"],
                rowAuthen["PERSON_INCHARGE"],
                rowAuthen["EMAIL"],
                rowAuthen["TELEPHONE"],
                rowAuthen["FAX"],
                rowAuthen["COMPANYBRANCH"],
                rowAuthen["ADDRESS"],
                rowAuthen["ACCOUNT_NAME"],
                rowAuthen["ACCOUNT_NUMBER"],
                rowAuthen["BANK_NAME"],
                rowAuthen["BANKBRANCH_NAME"],
                rowAuthen["BANKBRANCH_NO"],
                rowAuthen["TAXID"]
            );

            try
            {
                smtp.Send(mail);
                res = 1;
                msg = "successfully";
            }
            catch (Exception ex)
            {
                return Ok(new { result = -5, message = "ส่งเมลไม่สำเร็จ: " + ex.Message });
            }

            return Ok(new { result = res, message = msg });
        }

        [HttpPost]
        [Route("rejectaccsetting")]
        public IActionResult RejectVenderinfo([FromBody] RejectVdInfo mParam)
        {
            int res = 0;
            string msg = "";

            if (string.IsNullOrEmpty(mParam.Username))
            {
                return Ok(new { result = -1, message = "ไม่พบข้อมูล vendor" });
            }

            // ===================== GET VENDOR LOG =====================
            SqlCommand logCmd = new SqlCommand(@"
                SELECT TOP 1 *
                FROM [dbSCM].[dbo].[EBILLING_VENDORINFO_LOG]
                WHERE USERNAME = @USERNAME AND STATE = 'CREATE'
                ORDER BY CRDDATE DESC");
            logCmd.Parameters.AddWithValue("@USERNAME", mParam.Username);

            DataTable logData = dbSCM.Query(logCmd);
            if (logData.Rows.Count == 0)
            {
                return Ok(new { result = -2, message = "ไม่พบข้อมูล vendor log" });
            }

            DataRow rowLog = logData.Rows[0];

            // ===================== GET AUTHEN =====================
            SqlCommand authenCmd = new SqlCommand(@"
                SELECT *
                FROM [dbSCM].[dbo].[EBILLING_AUTHEN]
                WHERE USERNAME = @USERNAME");
            authenCmd.Parameters.AddWithValue("@USERNAME", mParam.Username);

            DataTable authenData = dbSCM.Query(authenCmd);
            if (authenData.Rows.Count == 0)
            {
                return Ok(new { result = -3, message = "ไม่พบข้อมูล Authen" });
            }

            // ===================== UPDATE LOG STATE =====================
            SqlCommand updateStateCmd = new SqlCommand(@"
                    UPDATE [dbSCM].[dbo].[EBILLING_VENDORINFO_LOG]
                    SET STATE = 'REJECT',
                        REMARK = @REMARK
                    WHERE USERNAME = @USERNAME AND STATE = 'CREATE'");
            updateStateCmd.Parameters.AddWithValue("@USERNAME", mParam.Username);
            updateStateCmd.Parameters.AddWithValue("@REMARK", mParam.Remark);
            dbSCM.ExecuteCommand(updateStateCmd);


            // ===================== SEND MAIL =====================
            string mailTemplate = "";
            try
            {
                mailTemplate = System.IO.File.ReadAllText("htmlpage.html");
            }
            catch
            {
                return Ok(new { result = -4, message = "ไม่พบไฟล์ email template" });
            }

            MailMessage mail = new MailMessage();
            SmtpClient smtp = new SmtpClient("smtp.dci.daikin.co.jp", 25);
            smtp.UseDefaultCredentials = false;

            mail.From = new MailAddress("dci-noreply@dci.daikin.co.jp", "E-Billing");
            mail.Subject = "แจ้งเตือนจากระบบวางบิล vendor มีการ setting ข้อมูลใหม่";
            mail.IsBodyHtml = true;
            mail.Priority = MailPriority.High;
            //mail.To.Add(rowLog["EMAIL"].ToString()); ใช้จริงให้เปลี่ยนกลับมาเป้นอันนี้
            mail.To.Add("anuthida.w@dci.daikin.co.jp");

            mail.Body = string.Format(mailTemplate,
                mParam.Username,
                rowLog["COMPANYNAME"],
                rowLog["NAME"],
                rowLog["EMAIL"],
                rowLog["TAXID"],
                rowLog["BRANCHNO"],
                rowLog["FAX"],
                rowLog["TELEPHONE"],
                rowLog["ADDRESS"],
                rowLog["ACCNAME"],
                rowLog["ACCNO"],
                rowLog["BANKNAME"],
                rowLog["BANKBRANCHNAME"],
                rowLog["BANKBRANCHNO"],
                mParam.Remark
            );

            try
            {
                smtp.Send(mail);
                res = 1;
                msg = "successfully";
            }
            catch (Exception ex)
            {
                return Ok(new { result = -5, message = "ส่งเมลไม่สำเร็จ: " + ex.Message });
            }

            return Ok(new { result = res, message = msg });
        }

        [HttpGet]
        [Route("accfromvendor")]
        public ActionResult AccInfoFromVendor()
        {
            int res = 0;
            string msg = "";

            List<VendorinfoLog> loglist = new List<VendorinfoLog>();

            SqlCommand logCmd = new SqlCommand();
            logCmd.CommandText = @"
                SELECT *
                FROM (
                    SELECT *,
                           ROW_NUMBER() OVER (
                               PARTITION BY USERNAME
                               ORDER BY CRDDATE DESC
                           ) AS rn
                    FROM [dbSCM].[dbo].[EBILLING_VENDORINFO_LOG]
                    WHERE STATE = 'CREATE'
                ) t
                WHERE rn = 1
                ORDER BY CRDDATE DESC";

            DataTable dtlog = dbSCM.Query(logCmd);

            if (dtlog.Rows.Count > 0 )
            {
                foreach (DataRow row in dtlog.Rows)
                {
                    VendorinfoLog LogData = new VendorinfoLog();
                    LogData.USERNAME = row["USERNAME"].ToString();
                    LogData.NAME = row["NAME"].ToString();
                    LogData.COMPANYNAME = row["COMPANYNAME"].ToString();
                    LogData.EMAIL = row["EMAIL"].ToString();
                    LogData.TAXID = row["TAXID"].ToString();
                    LogData.BRANCHNO = row["BRANCHNO"].ToString();
                    LogData.FAX = row["FAX"].ToString();
                    LogData.TELEPHONE = row["TELEPHONE"].ToString();
                    LogData.ADDRESS = row["ADDRESS"].ToString();
                    LogData.ACCNAME = row["ACCNAME"].ToString();
                    LogData.ACCNO = row["ACCNO"].ToString();
                    LogData.BANKNAME = row["BANKNAME"].ToString();
                    LogData.BANKBRANCHNAME = row["BANKBRANCHNAME"].ToString();
                    LogData.BANKBRANCHNO = row["BANKBRANCHNO"].ToString();

                    loglist.Add(LogData);
                }

            }

            return Ok(loglist);
        }
    }
}
