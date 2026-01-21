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
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features; // <-- สำหรับ FormOptions
using System.IO;
using System.Threading.Tasks;
using INVOICEBILLINENOTE_API.Models;



namespace INVOICE_VENDER_API.Controllers
{

    [Route("api/[controller]")]
    [ApiController]
    public class AttachfileController : ControllerBase
    {
        private SqlConnectDB dbSCM = new SqlConnectDB("dbSCM");

        private readonly string UploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");

        public AttachfileController()
        {
            if (!Directory.Exists(UploadFolder))
                Directory.CreateDirectory(UploadFolder);
        }


        [HttpPost("PostUploadPDF")]
        [RequestSizeLimit(50_000_000)]
        public async Task<IActionResult> PostUploadPDF([FromForm] IFormFile file, [FromForm] string DocumentNo)
        {
            if (file == null)
                return BadRequest(new { message = "File is null" });

            if (file.Length == 0)
                return BadRequest(new { message = "File is empty" });

            if (Path.GetExtension(file.FileName).ToLower() != ".pdf")
                return BadRequest(new { message = "Only PDF files are allowed" });

            if (string.IsNullOrEmpty(DocumentNo))
                return BadRequest(new { message = "DocumentNo is missing" });

            var fileName = $"{DocumentNo}.pdf";
            var filePath = Path.Combine(UploadFolder, fileName);

            // Save/overwrite file on disk
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Get VENDORCODE
            SqlCommand cmdCode = new SqlCommand();
            cmdCode.CommandText = @"SELECT [VENDORCODE]
                            FROM [dbSCM].[dbo].[EBILLING_HEADER]
                            WHERE DOCUMENTNO = @DocumentNo";
            cmdCode.Parameters.AddWithValue("@DocumentNo", DocumentNo);
            DataTable dtCode = dbSCM.Query(cmdCode);

            if (dtCode.Rows.Count > 0)
            {
                foreach (DataRow iCode in dtCode.Rows)
                {
                    string code = iCode["VENDORCODE"].ToString();

                    // Get PERSON_INCHARGE
                    SqlCommand cmdIncharge = new SqlCommand();
                    cmdIncharge.CommandText = @"SELECT [PERSON_INCHARGE]
                                        FROM [dbSCM].[dbo].[EBILLING_AUTHEN]
                                        WHERE USERNAME = @USERNAME";
                    cmdIncharge.Parameters.AddWithValue("@USERNAME", code);
                    DataTable dtIncharge = dbSCM.Query(cmdIncharge);

                    if (dtIncharge.Rows.Count > 0)
                    {
                        foreach (DataRow iIncharge in dtIncharge.Rows)
                        {
                            string createBy = iIncharge["PERSON_INCHARGE"].ToString();

                            // Check if file already exists for this DocumentNo
                            SqlCommand checkFile = new SqlCommand();
                            checkFile.CommandText = @"SELECT *
                                             FROM [EBILLING_ATTACH_FILE] 
                                             WHERE DOCUMENTNO = @DOCUMENTNO";
                            checkFile.Parameters.AddWithValue("@DOCUMENTNO", DocumentNo);
                            DataTable dtcount = dbSCM.Query(checkFile);
                            if (dtcount.Rows.Count == 0)
                            {
                                // Insert new record
                                SqlCommand addFile = new SqlCommand();
                                addFile.CommandText = @"INSERT INTO [EBILLING_ATTACH_FILE] 
                                                ([DOCUMENTNO],[FILE_NAME],[FILE_PATH],[CREATE_BY],[CREATE_DATE])
                                                VALUES (@DOCUMENTNO,@FILE_NAME,@FILE_PATH,@CREATE_BY,GETDATE())";
                                addFile.Parameters.AddWithValue("@DOCUMENTNO", DocumentNo);
                                addFile.Parameters.AddWithValue("@FILE_NAME", fileName);
                                addFile.Parameters.AddWithValue("@FILE_PATH", filePath);
                                addFile.Parameters.AddWithValue("@CREATE_BY", createBy);
                                dbSCM.Query(addFile);
                            }
                            else
                            {
                                // Update existing record
                                SqlCommand updateFile = new SqlCommand();
                                updateFile.CommandText = @"UPDATE [EBILLING_ATTACH_FILE]
                                                   SET FILE_NAME = @FILE_NAME,
                                                       FILE_PATH = @FILE_PATH,
                                                       CREATE_BY = @CREATE_BY,
                                                       CREATE_DATE = GETDATE()
                                                   WHERE DOCUMENTNO = @DOCUMENTNO";
                                updateFile.Parameters.AddWithValue("@DOCUMENTNO", DocumentNo);
                                updateFile.Parameters.AddWithValue("@FILE_NAME", fileName);
                                updateFile.Parameters.AddWithValue("@FILE_PATH", filePath);
                                updateFile.Parameters.AddWithValue("@CREATE_BY", createBy);
                                dbSCM.Query(updateFile);
                            }
                        }
                    }
                }
            }

            return Ok(new { message = "PDF uploaded successfully!", fileName });
        }




        [HttpPost]
        [Route("PostShowFile")]
        public IActionResult PostShowFile([FromBody] MAttachfile obj)
        {
          List<MShowAttachfile> MShowAttachfile_List = new List<MShowAttachfile>();

            SqlCommand cmdFile = new SqlCommand();
            cmdFile.CommandText = @"SELECT  [DOCUMENTNO]
                                                    ,[FILE_NAME]
                                                    ,[FILE_PATH]
                                                    ,[CREATE_BY]
                                                    ,FORMAT(CREATE_DATE,'yyyy-MM-dd') as CREATEDATE
                                                    FROM [dbSCM].[dbo].[EBILLING_ATTACH_FILE]
                                                    WHERE DOCUMENTNO = @DOCUMENTNO";
            cmdFile.Parameters.Add(new SqlParameter("@DOCUMENTNO", obj.DOCUMENTNO));
           DataTable dtFile =  dbSCM.Query(cmdFile);
            if (dtFile.Rows.Count > 0)
            {
                foreach (DataRow item in dtFile.Rows)
                {
                    MShowAttachfile model = new MShowAttachfile();
                    model.DOCUMENTNO = item["DOCUMENTNO"].ToString();
                    model.FILE_NAME = item["FILE_NAME"].ToString();
                    model.FILE_PATH = item["FILE_PATH"].ToString();
                    model.CREATEDATE = item["CREATEDATE"].ToString();

                    MShowAttachfile_List.Add(model);
                }
            }


            return Ok(MShowAttachfile_List);
        }
    }

}
