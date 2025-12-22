using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using API_ITTakeOutComputer.Model;
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

                    SqlCommand authenvenderCmd = new SqlCommand();
                    authenvenderCmd.CommandText = @"
                    INSERT INTO [dbSCM].[dbo].[EBILLING_AUTHEN]
                    (USERNAME, PASSWORD, USERTYPE)
                    VALUES
                    (@Username, @Password, @Usertype)";

                    authenvenderCmd.Parameters.AddWithValue("@Username", mParam.Username);
                    authenvenderCmd.Parameters.AddWithValue("@Password", passwordHash);
                    authenvenderCmd.Parameters.AddWithValue("@Usertype", utype);

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
                            (USERNAME, PASSWORD, USERTYPE, PERSON_INCHARGE, EMAIL_INCHARGE, TEL_INCHARGE, CRDATE, STATUS)
                            VALUES
                            (@Username, @Password, @Usertype, @Personincharge, @Emailincharge, @Telincharge, GETDATE(), @Status)";

                        authenregisCmd.Parameters.AddWithValue("@Username", mParam.Username);
                        authenregisCmd.Parameters.AddWithValue("@Password", passwordHash); //new edit
                        authenregisCmd.Parameters.AddWithValue("@Usertype", utype);
                        authenregisCmd.Parameters.AddWithValue("@Personincharge", nameemp);
                        authenregisCmd.Parameters.AddWithValue("@Emailincharge", email);
                        authenregisCmd.Parameters.AddWithValue("@Telincharge", tel);

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
                    SELECT PASSWORD 
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

                bool isCorrect = BCrypt.Net.BCrypt.Verify(
                        mParam.Password,
                        passwordHash
                );

                if (!isCorrect)
                {
                    return Ok(new { result = -5, message = "รหัสผ่านไม่ถูกต้อง" });
                }



                //DateTime expirepass = Convert.ToDateTime(dtcheckexpirepass.Rows[0]["PASSWORD_EXPIRE"]);

                //if (expirepass <= DateTime.Now)
                //{
                //    res = -3;
                //    msg = "รหัสผ่านของท่านหมดอายุ กรุณาสร้างรหัสใหม่";
                //    return Ok(new { result = res, message = msg });
                //}

                string token = CreateToken(mParam.Username);
                return Ok(new { result = token });
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

            if (string.IsNullOrEmpty(mParam.Username) || string.IsNullOrEmpty(mParam.OldPassword))
            {
                res = -1;
                msg = "กรุณากรอกข้อมูลให้ครบถ้วน";
                return Ok(new { result = res, message = msg });
            }

            try
            {
                SqlCommand checkoldpassCmd = new SqlCommand();
                checkoldpassCmd.CommandText = @"
                    SELECT 1 
                    FROM [dbSCM].[dbo].[EBILLING_AUTHEN]
                    WHERE USERNAME = @Username";
                checkoldpassCmd.Parameters.AddWithValue("@Username", mParam.Username);
                DataTable dtcheckoldpass = dbSCM.Query(checkoldpassCmd);

                if (dtcheckoldpass.Rows.Count == 0)
                {
                    res = -2;
                    msg = "ไม่พบ username และ password นี้";
                    return Ok(new { result = res, message = msg });
                }

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
                res = -3;
                msg = ex.Message;
                return Ok(new { result = res, message = msg });
            }


        }

    }
}
