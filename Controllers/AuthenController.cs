using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using API_ITTakeOutComputer.Model;
using BCrypt.Net;
using INVOICE_VENDER_API.Contexts;
using INVOICE_VENDER_API.Models;
using INVOICE_VENDER_API.Services.Create;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using Oracle.ManagedDataAccess.Client;

namespace INVOICE_VENDER_API.Controllers
{
    [ApiController]
    [Route("api/authen")]
    public class EBullingController : ControllerBase
    {
        private ClsHelper oHelper = new ClsHelper();
        private SqlConnectDB dbSCM = new SqlConnectDB("dbSCM");
        private SqlConnectDB dbHRM = new SqlConnectDB("dbHRM");
        private OraConnectDB oOraAL02 = new OraConnectDB("ALPHA02");



        [HttpGet]
        [AllowAnonymous]
        public ActionResult AuthenInfo()
        {
            SqlCommand test = new SqlCommand(@"
            SELECT [USERNAME]
              ,[PASSWORD]
              ,[USERTYPE]
              ,[PERSON_INCHARGE]
              ,[EMAIL_INCHARGE]
              ,[TEL_INCHARGE]
              ,[TEXTID_INCHARGE]
              ,[FAX_INCHARGE]
              ,[PASSWORD_EXPIRE]
              ,[CRDATE]
              ,[STATUS]
              ,[ADDRESS_INCHARGE]
          FROM [dbSCM].[dbo].[EBULLING_AUTHEN]");

            DataTable dt = dbSCM.Query(test);

            var rows = new List<Dictionary<string, object>>();
            foreach (DataRow dr in dt.Rows)
            {
                var row = new Dictionary<string, object>();
                foreach (DataColumn col in dt.Columns)
                {
                    row[col.ColumnName] = dr[col];
                }
                rows.Add(row);
            }

            return Ok(rows);
        }


        [HttpPost]
        [Route("register")]
        public IActionResult Register([FromBody] RegisRequest mParam)
        {
            int res = 0;
            string msg = "";

            if (mParam == null)
            {
                res = -1;
                msg = "กรุณากรอกข้อมูลให้ครบถ้วน";
                return Ok(new { result = res, message = msg });
            }

            string requestRole = (mParam.Role ?? "").Trim().ToUpper();
            string rolRef;
            if (requestRole == "ADMIN") rolRef = "rol_admin";
            else if (requestRole == "ACCOUNTANT") rolRef = "rol_accountant";
            else rolRef = "rol_vender";

            string requestUtype = (mParam.Usertype ?? "").Trim().ToUpper();
            string utype;
            if (requestUtype == "ADMIN" || requestUtype == "ACCOUNTANT") utype = "DCI";
            else utype = "VENDER";


            try
            {
                SqlCommand authenCmd = new SqlCommand();
                authenCmd.CommandText = @"
                    SELECT 1 
                    FROM [dbSCM].[dbo].[EBILLING_AUTHEN]
                    WHERE USERNAME = @Username";
                authenCmd.Parameters.AddWithValue("@Username", mParam.Username);

                //authenCmd.Parameters.AddWithValue("@Password", mParam.Password);
                //authenCmd.Parameters.AddWithValue("@Personincharge", mParam.Incharge);

                DataTable dtauthenRegis = dbSCM.Query(authenCmd);

                if (dtauthenRegis.Rows.Count > 0)
                {
                    res = -2;
                    msg = "มีผู้ใช้งานนี้ในระบบแล้ว";
                    return Ok(new { result = res, message = msg });
                }

                string passwordHash = BCrypt.Net.BCrypt.HashPassword(mParam.Password);

                if (rolRef == "rol_vender")
                {

                    OracleCommand vdCmd = new OracleCommand();
                    vdCmd.CommandText = $@"SELECT * FROM DST_ACMVD1 WHERE TAXID = '" + mParam.Username?.Trim() + "' AND KAISEQ = '999'";

                    DataTable dtOracle = oOraAL02.Query(vdCmd);

                    if (dtOracle.Rows.Count > 0)
                    {
                        //string nameemp = dtNameEmp.Rows[0]["NAME"].ToString() + " " + dtNameEmp.Rows[0]["SURN"].ToString();
                        string companyname = dtOracle.Rows[0]["VDNAME"].ToString();
                        string email = dtOracle.Rows[0]["POEMAIL"].ToString();
                        string tel = dtOracle.Rows[0]["TELNO"].ToString();
                        string taxid = dtOracle.Rows[0]["TAXID"].ToString();
                        string fax = dtOracle.Rows[0]["FAXNO"].ToString();
                        string address = dtOracle.Rows[0]["ADDR1"].ToString() + " " + dtOracle.Rows[0]["ADDR2"].ToString();
                        string branchno = dtOracle.Rows[0]["BRANCHNO"].ToString();

                        SqlCommand authenvenderCmd = new SqlCommand();
                        authenvenderCmd.CommandText = @"
                        INSERT INTO [dbSCM].[dbo].[EBILLING_AUTHEN]
                        (USERNAME, PASSWORD, USERTYPE, COMPANY_NAME, EMAIL, TELEPHONE, TAXID, FAX, COMPANYBRANCH, CRDATE, STATUS, ADDRESS)
                        VALUES
                        (@Username, @Password, @Usertype, @Companyname, @Email, @Telephone, @Taxid, @Fax, @Companybranch, GETDATE(), @Status, @Address)";

                        authenvenderCmd.Parameters.AddWithValue("@Username", mParam.Username);
                        authenvenderCmd.Parameters.AddWithValue("@Password", passwordHash);
                        authenvenderCmd.Parameters.AddWithValue("@Usertype", utype);
                        authenvenderCmd.Parameters.AddWithValue("@Companyname", companyname);
                        authenvenderCmd.Parameters.AddWithValue("@Email", email);
                        authenvenderCmd.Parameters.AddWithValue("@Telephone", tel);
                        authenvenderCmd.Parameters.AddWithValue("@Taxid", !string.IsNullOrWhiteSpace(taxid) ? taxid : mParam.Username);
                        authenvenderCmd.Parameters.AddWithValue("@Fax", fax);
                        authenvenderCmd.Parameters.AddWithValue("@Companybranch", branchno);
                        authenvenderCmd.Parameters.AddWithValue("@Status", "ACTIVE");
                        authenvenderCmd.Parameters.AddWithValue("@Address", address);


                        dbSCM.ExecuteCommand(authenvenderCmd);

                        SqlCommand roleCmd = new SqlCommand();
                        roleCmd.CommandText = @"
                                INSERT INTO [dbSCM].[dbo].[EBILLING_DICT]
                                (DICTTYPE, DICTKEYNO, DICTREFNO)
                                VALUES
                                (@Dicttype, @Dictkeyno, @Dictrefno)";


                        roleCmd.Parameters.AddWithValue("@Dicttype", "PV_MSTUSR");
                        roleCmd.Parameters.AddWithValue("@Dictkeyno", mParam.Username);
                        roleCmd.Parameters.AddWithValue("@Dictrefno", rolRef);

                        dbSCM.ExecuteCommand(roleCmd);

                        res = 1;
                        msg = "success";
                    }



                }

                else if (rolRef == "rol_admin" || rolRef == "rol_accountant")
                {
                    SqlCommand nameempCmd = new SqlCommand();
                    nameempCmd.CommandText = @"
                    SELECT NAME, SURN, MAIL, TELEPHONE FROM [dbHRM].[dbo].[Employee] WHERE CODE = @CODE";

                    nameempCmd.Parameters.AddWithValue(@"CODE", mParam.Username);

                    DataTable dtNameEmp = dbHRM.Query(nameempCmd);

                    if (dtNameEmp.Rows.Count > 0)
                    {
                        string nameemp = dtNameEmp.Rows[0]["NAME"].ToString() + " " + dtNameEmp.Rows[0]["SURN"].ToString();
                        string email = dtNameEmp.Rows[0]["MAIL"].ToString();
                        string tel = dtNameEmp.Rows[0]["TELEPHONE"].ToString();

                        SqlCommand authenregisCmd = new SqlCommand();
                        authenregisCmd.CommandText = @"
                            INSERT INTO [dbSCM].[dbo].[EBILLING_AUTHEN]
                            (USERNAME, PASSWORD, USERTYPE, PERSON_INCHARGE, EMAIL, TELEPHONE, CRDATE, STATUS)
                            VALUES
                            (@Username, @Password, @Usertype, @Personincharge, @Emaile, @Tel, GETDATE(), @Status)";

                        authenregisCmd.Parameters.AddWithValue("@Username", mParam.Username);
                        authenregisCmd.Parameters.AddWithValue("@Password", passwordHash);
                        authenregisCmd.Parameters.AddWithValue("@Usertype", utype);
                        authenregisCmd.Parameters.AddWithValue("@Personincharge", nameemp);
                        authenregisCmd.Parameters.AddWithValue("@Emaile", email);
                        authenregisCmd.Parameters.AddWithValue("@Tel", tel);

                        // DateTime passwordexp = DateTime.Now.AddMonths(3);
                        // authenregisCmd.Parameters.AddWithValue("@Passwordexpire", passwordexp);
                        authenregisCmd.Parameters.AddWithValue("@Status", "ACTIVE");

                        dbSCM.ExecuteCommand(authenregisCmd);


                        SqlCommand roleCmd = new SqlCommand();
                        roleCmd.CommandText = @"
                            INSERT INTO [dbSCM].[dbo].[EBILLING_DICT]
                            (DICTTYPE, DICTKEYNO, DICTREFNO)
                            VALUES
                            (@Dicttype, @Dictkeyno, @Dictrefno)";


                        roleCmd.Parameters.AddWithValue("@Dicttype", "PV_MSTUSR");
                        roleCmd.Parameters.AddWithValue("@Dictkeyno", mParam.Username);
                        roleCmd.Parameters.AddWithValue("@Dictrefno", rolRef);

                        dbSCM.ExecuteCommand(roleCmd);

                        res = 1;
                        msg = "success";
                    }

                }

            }
            catch (Exception ex)
            {
                res = -3;
                msg = ex.Message;
            }

            return Ok(new { result = res, message = msg });
        }


        [HttpPost]
        [Route("login")]
        public IActionResult Login([FromBody] LoginRequest mParam)
        {
            int res = 0;
            string msg = "";

            if (string.IsNullOrEmpty(mParam.Username) ||
                string.IsNullOrEmpty(mParam.Password))
            {
                res = -1;
                msg = "กรุณากรอกข้อมูลให้ครบถ้วน";
                return Ok(new { result = res, message = msg });
            }

            try
            {
                SqlCommand checkexpirepassCmd = new SqlCommand();
                checkexpirepassCmd.CommandText = @"
                    SELECT PASSWORD, FAILEDLOGINCOUNT
                    FROM [dbSCM].[dbo].[EBILLING_AUTHEN]
                    WHERE USERNAME = @Username";

                checkexpirepassCmd.Parameters.AddWithValue("@Username", mParam.Username);
                //checkexpirepassCmd.Parameters.AddWithValue("@Password", mParam.Password);

                DataTable dtcheckexpirepass = dbSCM.Query(checkexpirepassCmd);

                if (dtcheckexpirepass.Rows.Count == 0)
                {
                    res = -2;
                    msg = "ไม่พบผู้ใช้งานในระบบ";
                    return Ok(new { result = res, message = msg });

                }

                string passwordHash = dtcheckexpirepass.Rows[0]["PASSWORD"].ToString();
                int failedCount = dtcheckexpirepass.Rows[0]["FAILEDLOGINCOUNT"] == DBNull.Value 
                    ? 0 
                    : Convert.ToInt32(dtcheckexpirepass.Rows[0]["FAILEDLOGINCOUNT"]);
                bool isCorrect = BCrypt.Net.BCrypt.Verify(
                        mParam.Password,
                        passwordHash
                );

                if (!isCorrect)
                {
                    failedCount++;
                    SqlCommand failloginCmd = new SqlCommand();
                    failloginCmd.CommandText = $@"
                        UPDATE [dbSCM].[dbo].[EBILLING_AUTHEN]
                        SET FAILEDLOGINCOUNT = @FAILEDLOGINCOUNT
                        WHERE USERNAME = @USERNAME";

                    failloginCmd.Parameters.AddWithValue("@FAILEDLOGINCOUNT", failedCount);
                    failloginCmd.Parameters.AddWithValue("@USERNAME", mParam.Username);

                    dbSCM.ExecuteCommand(failloginCmd);

                    string msgErr = "รหัสผ่านไม่ถูกต้อง";

                    if (failedCount >= 3)
                    {
                        msgErr += "กรุณารีเซ็ตรหัสผ่านใหม่";
                    }

                    return Ok(new { result = -5, message = msgErr, failedCount });
                }

                //DateTime expirepass = Convert.ToDateTime(dtcheckexpirepass.Rows[0]["PASSWORD_EXPIRE"]);

                //if (expirepass <= DateTime.Now)
                //{
                //    res = -3;
                //    msg = "รหัสผ่านของท่านหมดอายุ กรุณาสร้างรหัสใหม่";
                //    return Ok(new { result = res, message = msg });
                //}

                if (isCorrect)
                {
                    SqlCommand resetfail = new SqlCommand();
                    resetfail.CommandText = @"
                        UPDATE [dbSCM].[dbo].[EBILLING_AUTHEN]
                        SET FAILEDLOGINCOUNT = @FAILEDLOGINCOUNT
                        WHERE USERNAME = @USERNAME";
                    resetfail.Parameters.AddWithValue("@FAILEDLOGINCOUNT", DBNull.Value);
                    resetfail.Parameters.AddWithValue("@USERNAME", mParam.Username);
                    dbSCM.ExecuteCommand(resetfail);

                    string token = CreateToken(mParam.Username);
                    return Ok(new { result = token });
                }

                res = -88;
                msg = "It have someting wrong!";
                return Ok(new { result = res, message = msg });
            }
            catch (Exception ex)
            {
                res = -99;
                msg = ex.Message;
                return Ok(new { result = res, message = msg });
            }
        }

        [HttpPost]
        [Authorize]
        [Route("checkauthen")]
        public IActionResult Checkauthen([FromBody] LoginRequest mParam)
        {

            int res = 0;
            string msg = "";
            string tokenKey = "dci.daikin.co.jp";

            string pwd = Encrypt(mParam.Password, tokenKey);
            bool isMatch = (pwd == "KiW1mg/z+3XpGORdA65JxQ==");

            SqlCommand infoCmd = new SqlCommand(@"
                SELECT 
                    auth.USERNAME,
                    auth.PASSWORD,
                    auth.PERSON_INCHARGE,
                    vnd.VenderName,
                    dict.DICTREFNO
                FROM [dbSCM].[dbo].[EBILLING_AUTHEN] auth
                LEFT JOIN [dbSCM].[dbo].[EBILLING_DICT] dict
                    ON auth.USERNAME = dict.DICTKEYNO
                LEFT JOIN [dbSCM].[dbo].[AL_Vendor] vnd
                    ON auth.USERNAME = vnd.Vender
                WHERE auth.USERNAME = @Username
                  AND dict.DICTTYPE = 'PV_MSTUSR';");

            infoCmd.Parameters.AddWithValue("@Username", mParam.Username);
            //infoCmd.Parameters.AddWithValue("@Password", mParam.Password);

            DataTable dt = dbSCM.Query(infoCmd);

            if (dt.Rows.Count == 0)
            {
                res = -1;
                msg = "ไม่พบข้อมูลผู้ใช้งาน";
                return Ok(new { result = res, message = msg });
            }

            var user = dt.Rows[0];

            // ตรวจสอบรหัสผ่าน: bcrypt หรือ master password
            string passwordHash = user["PASSWORD"].ToString();
            bool isPasswordCorrect = BCrypt.Net.BCrypt.Verify(mParam.Password, passwordHash);

            if (!isPasswordCorrect && !isMatch)
            {
                res = -2;
                msg = "รหัสผ่านไม่ถูกต้อง";
                return Ok(new { result = res, message = msg });
            }

            return Ok(new
            {
                result = "OK",
                pwd = pwd,
                isMatch = isMatch.ToString(),
                username = user["USERNAME"].ToString(),
                incharge = user["PERSON_INCHARGE"].ToString(),
                vendername = user["VenderName"].ToString(),
                role = user["DICTREFNO"].ToString()
            });
        }

        private string CreateToken(string username)
        {
            var claims = new List<Claim> { new Claim(ClaimTypes.Name, username) };

            string tokenKey = "daikincompressorindustriesthailand";

            var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(tokenKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature);
            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.Now.AddHours(1),
                signingCredentials: creds);

            var jwt = new JwtSecurityTokenHandler().WriteToken(token);

            return jwt;
        }

        private static string Encrypt(string data, string tokenKey)
        {
            string empty = string.Empty;
            try
            {
                Encryptor encryptor = new Encryptor(EncryptionAlgorithm.Rijndael);
                byte[] bytes = Encoding.ASCII.GetBytes(data);
                byte[] bytesKey = (encryptor.IV = Encoding.ASCII.GetBytes(tokenKey));
                byte[] inArray = encryptor.Encrypt(bytes, bytesKey);
                return Convert.ToBase64String(inArray);

            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private static string Decrypt(string data, string tokenKey)
        {
            string empty = string.Empty;
            try
            {
                Decryptor decryptor = new Decryptor(EncryptionAlgorithm.Rijndael);
                byte[] inArray = Convert.FromBase64String(data);
                byte[] bytesKey = (decryptor.IV = Encoding.ASCII.GetBytes(tokenKey));
                byte[] bytes = decryptor.Decrypt(inArray, bytesKey);
                return Encoding.ASCII.GetString(bytes);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        [HttpPost]
        [Route("editpass")]
        public IActionResult EditPassexp([FromBody] EditPassExpire mParam)
        {
            int res = 0;
            string msg = "";

            if (
                string.IsNullOrEmpty(mParam.Username) ||
                string.IsNullOrEmpty(mParam.OldPassword) ||
                string.IsNullOrEmpty(mParam.NewPassword) ||
                string.IsNullOrEmpty(mParam.ConfirmPassword)
                )
            {
                res = -1;
                msg = "กรุณากรอกข้อมูลให้ครบถ้วน";
                return Ok(new { result = res, message = msg });
            }

            if (mParam.NewPassword != mParam.ConfirmPassword)
            {
                res = -2;
                msg = "รหัสผ่านใหม่และยืนยันรหัสไม่ตรงกันกรุณาใส่รหัสผ่านให้ตรงกัน";
                return Ok(new { result = res, message = msg });
            }

            try
            {

                SqlCommand checkoldpassCmd = new SqlCommand();
                checkoldpassCmd.CommandText = @"
                    SELECT PASSWORD
                    FROM [dbSCM].[dbo].[EBILLING_AUTHEN]
                    WHERE USERNAME = @Username";
                checkoldpassCmd.Parameters.AddWithValue("@Username", mParam.Username);
                //checkoldpassCmd.Parameters.AddWithValue("@Password", mParam.OldPassword);

                DataTable dtcheckoldpass = dbSCM.Query(checkoldpassCmd);

                if (dtcheckoldpass.Rows.Count == 0)
                {
                    res = -3;
                    msg = "ไม่พบ username นี้";
                    return Ok(new { result = res, message = msg });
                }

                string oldPasswordHash = dtcheckoldpass.Rows[0]["PASSWORD"].ToString();

                //check old pass
                bool isOldPasswordCorrect = BCrypt.Net.BCrypt.Verify(
                        mParam.OldPassword,
                        oldPasswordHash
                );

                if (!isOldPasswordCorrect)
                {
                    res = -4;
                    msg = "รหัสผ่านเดิมไม่ถูกต้อง";
                    return Ok(new { result = res, message = msg });
                }

                //check new pass with old pass that are duplicate
                bool isSameAsOld = BCrypt.Net.BCrypt.Verify(
                    mParam.ConfirmPassword,
                    oldPasswordHash
                );

                if (isSameAsOld)
                {
                    res = -5;
                    msg = "รหัสผ่านใหม่ต้องไม่ซ้ำกับรหัสเดิม";
                    return Ok(new { result = res, message = msg });
                }

                //new pass hash
                string passwordHash = BCrypt.Net.BCrypt.HashPassword(mParam.NewPassword);

                SqlCommand editpassexpCmd = new SqlCommand();
                editpassexpCmd.CommandText = @"
                    UPDATE [dbSCM].[dbo].[EBILLING_AUTHEN]
                    SET PASSWORD = @NewPassword
                    WHERE USERNAME = @Username";
                editpassexpCmd.Parameters.AddWithValue("@NewPassword", passwordHash);
                editpassexpCmd.Parameters.AddWithValue("@Username", mParam.Username);
                dbSCM.ExecuteCommand(editpassexpCmd);
                res = 1;
                msg = "success";
                return Ok(new { result = res, message = msg });
            }

            catch (Exception ex)
            {
                res = -99;
                msg = ex.Message;
                return Ok(new { result = res, message = msg });
            }


        }

        [HttpPost]
        [Route("repassword")]
        public IActionResult Repassword([FromBody] LoginRequest mParam)
        {
            int res = 0;
            string msg = "";

            if (mParam == null)
            {
                res = -1;
                msg = "ไม่พบผู้ใช้งาน";
                return Ok(new { result = res, message = msg });
            }

            string passwordHash = BCrypt.Net.BCrypt.HashPassword(mParam.Password);
            SqlCommand repassCmd = new SqlCommand();
            repassCmd.CommandText = $@"
                UPDATE [dbSCM].[dbo].[EBILLING_AUTHEN]
                SET PASSWORD = @PASSWORD
                WHERE USERNAME = @USERNAME";

            repassCmd.Parameters.AddWithValue("@PASSWORD", passwordHash);
            repassCmd.Parameters.AddWithValue("@USERNAME", mParam.Username);
            dbSCM.ExecuteCommand(repassCmd);

            res = 1;
            msg = "รีเซ็ตรหัสผ่านใหม่สำเร็จ";
            return Ok(new { result = res, message = msg });
        }
    }
}
