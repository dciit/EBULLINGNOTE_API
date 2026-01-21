using System.Data;
using System.Globalization;
using API_ITTakeOutComputer.Model;
using INVOICE_VENDER_API.Models;
using INVOICE_VENDER_API.Services.Create;
using INVOICEBILLINENOTE_API.Connection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.VisualBasic;
using Oracle.ManagedDataAccess.Client;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace INVOICE_BILLINGNOTE_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ConfirmInvoiceController : ControllerBase
    {
        private OraConnectDB oOraAL02 = new OraConnectDB("ALPHA02");
        public string strRunningNbr = "";
        RunNumberService runNumberService = new RunNumberService();
        private SqlConnectDB oConSCM = new SqlConnectDB("dbSCM");


        [HttpGet("getNbr")]
        [AllowAnonymous]
        public ActionResult GetNbr()
        {
            List<MRunningNumber> resultNbr = new List<MRunningNumber>();
            MRunningNumber nbr = new MRunningNumber();
            strRunningNbr = runNumberService.NextId("BILLING_NOTE");
            string sub1 = strRunningNbr.Substring(0, 7);
            string sub2 = strRunningNbr.Substring(7, 4);


            string running = sub1 + "-" + sub2;

            nbr.Running = running;
            resultNbr.Add(nbr);

            return Ok(resultNbr);
        }


        [HttpGet("getVendor")]
        [AllowAnonymous]
        public ActionResult getVendor()
        {
            List<DataVender> data_List = new List<DataVender>();

            OracleCommand cmdVendor = new OracleCommand();
            cmdVendor.CommandText = $@"SELECT VENDER, VENDER || ' : ' || VDNAME  AS VENDORNAME , TAXID ,ADDR1 ,ADDR2 ,ZIPCODE, TELNO, FAXNO
                                        FROM DST_ACMVD1
                                        WHERE KAISEQ = '999'";
            DataTable dtVendor = oOraAL02.Query(cmdVendor);
            if (dtVendor.Rows.Count > 0)
            {
                foreach (DataRow item in dtVendor.Rows)
                {
                    DataVender model = new DataVender();
                    model.VENDER = item["VENDER"].ToString();
                    model.VDNAME = item["VENDORNAME"].ToString();
                    model.TAXID = item["TAXID"].ToString();
                    model.ADDR1 = item["ADDR1"].ToString();
                    model.ADDR2 = item["ADDR2"].ToString();
                    model.ZIPCODE = item["ZIPCODE"].ToString();
                    model.TELNO = item["TELNO"].ToString();
                    model.FAXNO = item["FAXNO"].ToString();


                    data_List.Add(model);
                }
            }

            return Ok(data_List);
        }




        [HttpPost]
        [Route("PostLoadInvoiceRequet")]
        public IActionResult PostLoadInvoiceRequet([FromBody] MParammeter obj)
        {
            List<DataForConfirmInvoice> Data_list = new List<DataForConfirmInvoice>();

            string invoiceNo = string.IsNullOrEmpty(obj.InvoiceNo) ? "%" : obj.InvoiceNo.Trim();
            bool isSearchInvoice = !string.IsNullOrEmpty(obj.InvoiceNo) && obj.InvoiceNo.Trim() != "%";


            string conditionInvDate = "";
            if (!string.IsNullOrEmpty(obj.InvoiceDateFrom) && !string.IsNullOrEmpty(obj.InvoiceDateTo))
            {
                conditionInvDate = " AND D.IVDATE BETWEEN :InvDateFrom AND :InvDateTo ";
            }

            OracleCommand sqlSelect = new OracleCommand();
            sqlSelect.CommandText = $@"SELECT DISTINCT 
                                                D.VENDER,
                                                D.IVNO,
                                                CASE
                                                    WHEN REGEXP_LIKE(TRIM(D.IVDATE), '^[0-9]{{8}}$')
                                                    THEN TO_CHAR(TO_DATE(D.IVDATE, 'YYYYMMDD'), 'DD/MM/YYYY')
                                                    ELSE NULL
                                                END AS IVDATE,
                                                D.VDNAME,
                                                D.CURR,
                                                D.AMTB,
                                                D.VATIN,
                                                V.PAYTRM,
                                                V.TAXID,
                                                D.ACTYPE
                                            FROM MC.DST_ACDAP1 D
                                            LEFT JOIN DST_ACMVD1 V ON V.VENDER = D.VENDER
                                            WHERE V.PAYTRM IS NOT NULL
                                              AND TRIM(D.VENDER) = :VENDER
                                              AND D.APBIT = 'F'
                                              AND D.PAYBIT IN ('U','F')
                                              AND TRIM(D.IVNO) LIKE :IVNO
                                              {conditionInvDate}";
            sqlSelect.Parameters.Add(new OracleParameter(":VENDER", obj.VenderCode));
            sqlSelect.Parameters.Add(new OracleParameter(":IVNO", invoiceNo));

            if (!string.IsNullOrEmpty(obj.InvoiceDateFrom) && !string.IsNullOrEmpty(obj.InvoiceDateTo))
            {
                sqlSelect.Parameters.Add(new OracleParameter(":InvDateFrom", obj.InvoiceDateFrom));
                sqlSelect.Parameters.Add(new OracleParameter(":InvDateTo", obj.InvoiceDateTo));
            }

            DataTable dtOracle = oOraAL02.Query(sqlSelect);

            // ===================== ดึง Invoice + DocumentNo จาก SQL Server =====================
            SqlCommand cmdDetail = new SqlCommand(@"SELECT INVOICENO, DOCUMENTNO
                                                    FROM EBILLING_DETAIL
                                                    WHERE INVOICENO IS NOT NULL");
            DataTable dtDetail = oConSCM.Query(cmdDetail);

            // Dictionary สำหรับ lookup
            var invoiceDocMap = dtDetail.AsEnumerable().GroupBy(r => r["INVOICENO"].ToString().Trim(), StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First()["DOCUMENTNO"]?.ToString(), StringComparer.OrdinalIgnoreCase);

            // ===================== กรอง Invoice =====================
            var filteredRows = dtOracle.AsEnumerable().Where(r => isSearchInvoice || !invoiceDocMap.ContainsKey(r["IVNO"].ToString().Trim())).GroupBy(r => r["IVNO"].ToString().Trim(), StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();

            // ===================== Mapping Data =====================
            int number = 1;

            foreach (var drow in filteredRows)
            {
                string ivno = drow["IVNO"].ToString().Trim();
                bool isExists = invoiceDocMap.ContainsKey(ivno);

                DataForConfirmInvoice MData = new DataForConfirmInvoice
                {
                    No = number++,
                    InvoiceNo = ivno,
                    InvoiceDate = drow["IVDATE"]?.ToString(),
                    VenderCode = drow["VENDER"]?.ToString(),
                    VendorName = drow["VDNAME"]?.ToString(),
                    PaymentTerms = drow["PAYTRM"]?.ToString(),
                    Currency = drow["CURR"]?.ToString(),
                    AMTB = drow["AMTB"]?.ToString(),
                    Vat = drow["VATIN"]?.ToString(),
                    TaxID = drow["TAXID"]?.ToString(),
                    InvoiceStatus = isExists ? "EXIST" : "NEW",
                    DocumentNo = isExists ? invoiceDocMap[ivno] : null,
                    ACTYPE = drow["ACTYPE"]?.ToString(),
                };

                // ===================== คำนวณ Due Date =====================
                if (!string.IsNullOrEmpty(MData.InvoiceDate) && int.TryParse(MData.PaymentTerms, out int payTerm) && DateTime.TryParseExact(MData.InvoiceDate, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime ivDate))
                {
                    // ===================== ดึงวันหยุดจาก AL_Calendar =====================
                    List<DateTime> holidayList = new List<DateTime>();

                    string sqlHoliday = $@"SELECT PDDATE
                                            FROM dbSCM.dbo.AL_Calendar
                                            WHERE HOLIDAY = 1
                                              AND YEAR(PDDATE) = {ivDate.Year}";
                    DataTable dtHoliday = oConSCM.Query(sqlHoliday);

                    foreach (DataRow row in dtHoliday.Rows)
                    {
                        holidayList.Add(Convert.ToDateTime(row["PDDATE"]).Date);
                    }



                    // ===================== 1️⃣ InvoiceDate + PaymentTerms =====================
                    DateTime calDate = ivDate.AddDays(payTerm);

                    // ===================== 2️⃣ วันสิ้นเดือน =====================
                    DateTime endOfMonth = new DateTime(calDate.Year, calDate.Month, DateTime.DaysInMonth(calDate.Year, calDate.Month));

                    // ===================== 3️⃣ ถ้าเป็นวันหยุด → ถอยย้อนหลัง =====================
                    while (holidayList.Contains(endOfMonth.Date))
                    {
                        endOfMonth = endOfMonth.AddDays(-1);
                    }

                    // ===================== ผลลัพธ์ =====================
                    MData.Duedate = endOfMonth.ToString("dd/MM/yyyy");
                }


                Data_list.Add(MData);
            }

            return Ok(Data_list);
        }




        [HttpPost]
        [Route("PostCreateInvoice")]
        public IActionResult PostCreateInvoice([FromBody] CreateBillingNote obj)
        {
            /****** HEAD ******/
            SqlCommand CreateBillingNoteHead = new SqlCommand();
            CreateBillingNoteHead.CommandText = @"INSERT INTO [EBILLING_HEADER] ([DOCUMENTNO]
                                                  ,[DOCUMENTDATE]
                                                  ,[PAYMENT_TERMS]
                                                  ,[DUEDATE]
                                                  ,[ACTYPE]
                                                  ,[VENDORCODE]
                                                  ,[INVOICENO]
                                                  ,[INVOICEDATE]
                                                  ,[TAXID]
                                                  ,[BILLERBY]
                                                  ,[BILLERDATE]
                                                  ,[CREATEBY]
                                                  ,[CREATEDATE]
                                                  ,[STATUS])
                                              VALUES (@DOCUMENTNO,@DOCUMENTDATE,@PAYMENT_TERMS,@DUEDATE,@ACTYPE
                                                    ,@VENDORCODE,@INVOICENO,@INVOICEDATE,@TAXID,@BILLERBY,GETDATE(),@CREATEBY,GETDATE(),@STATUS)";
            CreateBillingNoteHead.Parameters.Add(new SqlParameter("@DOCUMENTNO", obj.DOCUMENTNO));
            CreateBillingNoteHead.Parameters.Add(new SqlParameter("@DOCUMENTDATE", (object)obj.DOCUMENTDATE ?? DBNull.Value));
            CreateBillingNoteHead.Parameters.Add(new SqlParameter("@PAYMENT_TERMS", obj.PAYMENT_TERMS));
            CreateBillingNoteHead.Parameters.Add(new SqlParameter("@DUEDATE", (object)obj.DUEDATE ?? DBNull.Value));
            CreateBillingNoteHead.Parameters.Add(new SqlParameter("@ACTYPE", obj.ACTYPE));
            CreateBillingNoteHead.Parameters.Add(new SqlParameter("@VENDORCODE", obj.VENDORCODE));
            CreateBillingNoteHead.Parameters.Add(new SqlParameter("@INVOICENO", obj.INVOICENO));
            CreateBillingNoteHead.Parameters.Add(new SqlParameter("@INVOICEDATE", (object)obj.INVOICEDATE ?? DBNull.Value));
            CreateBillingNoteHead.Parameters.Add(new SqlParameter("@TAXID", obj.TAXID));
            CreateBillingNoteHead.Parameters.Add(new SqlParameter("@BILLERBY", obj.BILLERBY));
            CreateBillingNoteHead.Parameters.Add(new SqlParameter("@CREATEBY", obj.CREATEBY));
            CreateBillingNoteHead.Parameters.Add(new SqlParameter("@STATUS", obj.STATUS));

            oConSCM.Query(CreateBillingNoteHead);
            /****** END HEAD ******/


            /****** DETAIL ******/
            SqlCommand CreateBillingNoteDetail = new SqlCommand();
            CreateBillingNoteDetail.CommandText = @"INSERT INTO [EBILLING_DETAIL] ([DOCUMENTNO]
                                                                            ,[DOCUMENTDATE]
                                                                            ,[INVOICENO]
                                                                            ,[INVOICEDATE]
                                                                            ,[VENDORCODE]
                                                                            ,[TAXID]
                                                                            ,[PAYMENT_TERMS]
                                                                            ,[DUEDATE]
                                                                            ,[ACTYPE]
                                                                            ,[CURRENCY]
                                                                            ,[AMTB]
                                                                            ,[VAT]
                                                                            ,[TOTALVAT]
                                                                            ,[WHTAX]
                                                                            ,[TOTAL_WHTAX]
                                                                            ,[NETPAID]
                                                                            ,[BEFORVATAMOUNT]
                                                                            ,[TOTAL_AMOUNT]
                                                                            ,[CREATEBY]
                                                                            ,[CREATEDATE]
                                                                            ,[STATUS])
                                                        VALUES (@DOCUMENTNO,@DOCUMENTDATE,@INVOICENO,
                                                        @INVOICEDATE,@VENDORCODE,@TAXID,@PAYMENT_TERMS,
                                                        @DUEDATE,@ACTYPE,@CURRENCY,@AMTB,@VAT,@TOTALVAT,@WHTAX,
                                                        @TOTAL_WHTAX,@NETPAID,@BEFORVATAMOUNT,@TOTAL_AMOUNT,
                                                        @CREATEBY,GETDATE(),@STATUS)";
            CreateBillingNoteDetail.Parameters.Add(new SqlParameter("@DOCUMENTNO", obj.DOCUMENTNO));
            CreateBillingNoteDetail.Parameters.Add(new SqlParameter("@DOCUMENTDATE", obj.DOCUMENTDATE));
            CreateBillingNoteDetail.Parameters.Add(new SqlParameter("@INVOICENO", obj.INVOICENO));
            CreateBillingNoteDetail.Parameters.Add(new SqlParameter("@INVOICEDATE", obj.INVOICEDATE));
            CreateBillingNoteDetail.Parameters.Add(new SqlParameter("@VENDORCODE", obj.VENDORCODE));
            CreateBillingNoteDetail.Parameters.Add(new SqlParameter("@TAXID", obj.TAXID));
            CreateBillingNoteDetail.Parameters.Add(new SqlParameter("@PAYMENT_TERMS", obj.PAYMENT_TERMS));
            CreateBillingNoteDetail.Parameters.Add(new SqlParameter("@DUEDATE", obj.DUEDATE));
            CreateBillingNoteDetail.Parameters.Add(new SqlParameter("@ACTYPE", obj.ACTYPE));
            CreateBillingNoteDetail.Parameters.Add(new SqlParameter("@CURRENCY", obj.CURRENCY));
            CreateBillingNoteDetail.Parameters.Add(new SqlParameter("@AMTB", obj.AMTB));
            CreateBillingNoteDetail.Parameters.Add(new SqlParameter("@VAT", obj.VAT));
            CreateBillingNoteDetail.Parameters.Add(new SqlParameter("@TOTALVAT", obj.TOTALVAT));
            CreateBillingNoteDetail.Parameters.Add(new SqlParameter("@WHTAX", obj.RATE));
            CreateBillingNoteDetail.Parameters.Add(new SqlParameter("@TOTAL_WHTAX", obj.WHTAX));
            CreateBillingNoteDetail.Parameters.Add(new SqlParameter("@NETPAID", obj.NETPAID));
            CreateBillingNoteDetail.Parameters.Add(new SqlParameter("@BEFORVATAMOUNT", obj.BEFORVATAMOUNT));
            CreateBillingNoteDetail.Parameters.Add(new SqlParameter("@TOTAL_AMOUNT", obj.TOTAL_AMOUNT));
            CreateBillingNoteDetail.Parameters.Add(new SqlParameter("@CREATEBY", obj.CREATEBY));
            CreateBillingNoteDetail.Parameters.Add(new SqlParameter("@STATUS", obj.STATUS));

            oConSCM.Query(CreateBillingNoteDetail);
            /****** END DETAIL ******/

            return Ok();
        }


        [HttpPost]
        [Route("PostDeleteDocumentNo")]
        public IActionResult PostDeleteDocumentNo([FromBody] MReceiveBuilling obj)
        {

            SqlCommand deleteHead = new SqlCommand();
            deleteHead.CommandText = $@"DELETE
                                        FROM [dbSCM].[dbo].[EBILLING_HEADER]
                                        WHERE DOCUMENTNO = @DOCUMENTNO";
            deleteHead.Parameters.Add(new SqlParameter("@DOCUMENTNO", obj.DocumentNo));
            oConSCM.Query(deleteHead);


            SqlCommand deleteDetail = new SqlCommand();
            deleteDetail.CommandText = $@"DELETE
                                        FROM [dbSCM].[dbo].[EBILLING_DETAIL]
                                        WHERE DOCUMENTNO = @DOCUMENTNO";
            deleteDetail.Parameters.Add(new SqlParameter("@DOCUMENTNO", obj.DocumentNo));
            oConSCM.Query(deleteDetail);


            return Ok();
        }


        [HttpPost]
        [Route("PostReportVendorHeader")]
        public IActionResult PostReportVendorHeader([FromBody] MParammeter obj)
        {
            List<ReportHeader> Data_list = new List<ReportHeader>();
            string strVendorCode = "";

            if (obj.Role == "rol_accountant")
            {
                strVendorCode = "%";
            }
            else
            {
                strVendorCode = obj.VenderCode;
            }


            string conditionInvDate = "";
            if (!string.IsNullOrEmpty(obj.InvoiceDateFrom) && !string.IsNullOrEmpty(obj.InvoiceDateTo))
            {
                conditionInvDate = $" AND INVOICEDATE >= '{obj.InvoiceDateFrom}' AND INVOICEDATE <= '{obj.InvoiceDateTo}' ";
            }

            SqlCommand cmdHead = new SqlCommand();
            cmdHead.CommandText = $@"WITH RankedInvoices AS (
                                                SELECT *,
                                                        ROW_NUMBER() OVER(PARTITION BY VENDORCODE ORDER BY DOCUMENTDATE DESC) AS rn
                                                FROM [dbSCM].[dbo].[EBILLING_HEADER]
                                                WHERE STATUS LIKE @STATUS  AND VENDORCODE LIKE  @VENDORCODE AND DOCUMENTNO LIKE @DOCUMENTNO {conditionInvDate} 
                                            )
                                            SELECT 
                                                DOCUMENTNO,
                                                FORMAT(DOCUMENTDATE,'dd/MM/yyyy') AS DOCUMENTDATE,
                                                PAYMENT_TERMS,
                                                FORMAT(DUEDATE,'dd/MM/yyyy') AS DUEDATE,
                                                VENDORCODE,
                                                INVOICENO,
                                                FORMAT(INVOICEDATE,'dd/MM/yyyy') AS INVOICEDATE,
                                                TAXID,
                                                BILLERBY,
                                                FORMAT(BILLERDATE,'dd/MM/yyyy') AS BILLERDATE,
                                                RECEIVED_BILLERBY,
                                                FORMAT(RECEIVED_BILLERDATE,'dd/MM/yyyy') AS RECEIVED_BILLERDATE,
                                                CREATEBY,
                                                FORMAT(CREATEDATE,'dd/MM/yyyy') AS CREATEDATE,
                                                UPDATEBY,
                                                FORMAT(UPDATEDATE,'dd/MM/yyyy') AS UPDATEDATE,
                                                STATUS
                                            FROM RankedInvoices
                                           -- WHERE rn = 1";
            cmdHead.Parameters.Add(new SqlParameter("@VENDORCODE", strVendorCode));
            cmdHead.Parameters.Add(new SqlParameter("@STATUS", obj.status));
            cmdHead.Parameters.Add(new SqlParameter("@DOCUMENTNO", obj.DocumentNo));
            DataTable dtHead = oConSCM.Query(cmdHead);
            if (dtHead.Rows.Count > 0)
            {
                foreach (DataRow drow in dtHead.Rows)
                {
                    ReportHeader MData = new ReportHeader();
                    MData.DOCUMENTNO = drow["DOCUMENTNO"].ToString();
                    MData.DUEDATE = drow["DUEDATE"].ToString();
                    MData.TAXID = drow["TAXID"].ToString();
                    MData.PAYMENT_TERMS = drow["PAYMENT_TERMS"].ToString();
                    MData.VENDORCODE = drow["VENDORCODE"].ToString();
                    MData.DATE = drow["CREATEDATE"].ToString();
                    MData.BILLERBY = drow["BILLERBY"].ToString();
                    MData.BILLERDATE = drow["BILLERDATE"].ToString();
                    MData.RECEIVED_BILLERBY = drow["RECEIVED_BILLERBY"].ToString();
                    MData.RECEIVED_BILLERDATE = drow["RECEIVED_BILLERDATE"].ToString();
                    MData.STATUS = drow["STATUS"].ToString();
                    MData.INVOICENO = drow["INVOICENO"].ToString();
                    MData.INVOICEDATE = drow["INVOICEDATE"].ToString();

                    string vendorCode = MData.VENDORCODE;
                    string status = MData.STATUS;
                    string ivno = MData.INVOICENO;


                    OracleCommand cmdVDNAME = new OracleCommand();
                    cmdVDNAME.CommandText = @"SELECT *
                                                FROM DST_ACMVD1
                                                WHERE KAISEQ = '999' AND TRIM(VENDER) = :VENDER";
                    cmdVDNAME.Parameters.Add(new OracleParameter(":VENDER", vendorCode));
                    DataTable dtVDNAME = oOraAL02.Query(cmdVDNAME);
                    if (dtVDNAME.Rows.Count > 0)
                    {
                        MData.VENDORNAME = dtVDNAME.Rows[0]["VDNAME"].ToString();
                        MData.ADDRES1 = dtVDNAME.Rows[0]["ADDR1"].ToString();
                        MData.ADDRES2 = dtVDNAME.Rows[0]["ADDR2"].ToString();
                        MData.ZIPCODE = dtVDNAME.Rows[0]["ZIPCODE"].ToString();
                        MData.TELNO = dtVDNAME.Rows[0]["TELNO"].ToString();
                        MData.FAXNO = dtVDNAME.Rows[0]["FAXNO"].ToString();
                    }

                    OracleCommand selectACType = new OracleCommand();
                    selectACType.CommandText = @"SELECT IVNO,ACTYPE
                                                FROM MC.DST_ACDAP1 D
                                                where IVNO = :IVNO";
                    selectACType.Parameters.Add(new OracleParameter(":IVNO", ivno));
                    DataTable dtACType = oOraAL02.Query(selectACType);
                    if (dtACType.Rows.Count > 0)
                    {
                        MData.ACTYPE = dtACType.Rows[0]["ACTYPE"].ToString();
                    }


                    SqlCommand cmdTotal = new SqlCommand();
                    cmdTotal.CommandText = @"SELECT VENDORCODE,
                                                SUM(TOTAL_AMOUNT) AS TOTAL_AMOUNT,
                                                SUM(TOTAL_WHTAX) AS TOTAL_WHTAX,
                                                SUM(TOTAL_AMOUNT) - SUM(TOTAL_WHTAX) AS NETPAID
                                                FROM dbSCM.dbo.EBILLING_DETAIL
                                                WHERE [STATUS] LIKE @STATUS AND [VENDORCODE] = @VENDORCODE 
                                                GROUP BY VENDORCODE";
                    cmdTotal.Parameters.Add(new SqlParameter("@VENDORCODE", vendorCode));
                    cmdTotal.Parameters.Add(new SqlParameter("@STATUS", status));
                    DataTable dtTOTAL = oConSCM.Query(cmdTotal);
                    if (dtTOTAL.Rows.Count > 0)
                    {
                        DataRow t = dtTOTAL.Rows[0];

                        MData.TOTAL_AMOUNT = Convert.ToDecimal(t["TOTAL_AMOUNT"]);
                        MData.WHTAX = Convert.ToDecimal(t["TOTAL_WHTAX"]);
                        MData.NETPAID = Convert.ToDecimal(t["NETPAID"]);
                    }

                    Data_list.Add(MData);
                }
            }

            return Ok(Data_list);
        }


        [HttpPost]
        [Route("PostReportVendorDetail")]
        public IActionResult PostReportVendorDetail([FromBody] MParammeter obj)
        {
            List<ReportDetail> Data_list = new List<ReportDetail>();


            SqlCommand cmdHead = new SqlCommand();
            cmdHead.CommandText = $@"SELECT [DOCUMENTNO]
                                            ,FORMAT([DOCUMENTDATE],'dd/MM/yyyy') AS DOCUMENTDATE
                                            ,[INVOICENO]
                                            ,FORMAT([INVOICEDATE],'dd/MM/yyyy') AS INVOICEDATE
                                            ,[VENDORCODE]
                                            ,[TAXID]
                                            ,[PAYMENT_TERMS]
                                            ,FORMAT([DUEDATE],'dd/MM/yyyy') AS DUEDATE
                                            ,[CURRENCY]
                                            ,[AMTB]
                                            ,[VAT]
                                            ,[TOTALVAT]
                                            ,[WHTAX]
                                            ,[TOTAL_WHTAX]
                                            ,[NETPAID]
                                            ,[BEFORVATAMOUNT]
                                            ,[TOTAL_AMOUNT]
                                            ,[CREATEBY]
                                            ,FORMAT([CREATEDATE],'dd/MM/yyyy') AS CREATEDATE
                                            ,[UPDATEBY]
                                            ,FORMAT([UPDATEDATE],'dd/MM/yyyy') AS UPDATEDATE
                                            ,[STATUS]
                                            FROM [dbSCM].[dbo].[EBILLING_DETAIL]
                                            WHERE DOCUMENTNO = @DOCUMENTNO";
            cmdHead.Parameters.Add(new SqlParameter("@DOCUMENTNO", obj.DocumentNo));
            DataTable dtHead = oConSCM.Query(cmdHead);
            if (dtHead.Rows.Count > 0)
            {
                foreach (DataRow drow in dtHead.Rows)
                {
                    ReportDetail MData = new ReportDetail();
                    MData.DOCUMENTNO = drow["DOCUMENTNO"].ToString();
                    MData.INVOICENO = drow["INVOICENO"].ToString();
                    MData.INVOICEDATE = drow["INVOICEDATE"].ToString();
                    MData.TAXID = drow["TAXID"].ToString();
                    MData.DUEDATE = drow["DUEDATE"].ToString();
                    MData.AMTB = Convert.ToDecimal(drow["AMTB"].ToString());
                    MData.VAT = Convert.ToDecimal(drow["VAT"].ToString());
                    MData.TOTALVAT = Convert.ToDecimal(drow["TOTALVAT"].ToString());
                    MData.RATE = drow["WHTAX"].ToString();
                    MData.WHTAX = Convert.ToDecimal(drow["TOTAL_WHTAX"].ToString());
                    MData.TOTALAMOUNT = Convert.ToDecimal(drow["TOTAL_AMOUNT"].ToString());


                    Data_list.Add(MData);
                }
            }

            return Ok(Data_list);
        }



        [HttpPost]
        [Route("PostReportVendorDetailPrint")]
        public IActionResult PostReportVendorDetailPrint([FromBody] MParammeter obj)
        {
            List<ReportDetail> Data_list = new List<ReportDetail>();


            SqlCommand cmdHead = new SqlCommand();
            cmdHead.CommandText = $@"SELECT [DOCUMENTNO]
                                            ,FORMAT([DOCUMENTDATE],'dd/MM/yyyy') AS DOCUMENTDATE
                                            ,[INVOICENO]
                                            ,FORMAT([INVOICEDATE],'dd/MM/yyyy') AS INVOICEDATE
                                            ,[VENDORCODE]
                                            ,[TAXID]
                                            ,[PAYMENT_TERMS]
                                            ,FORMAT([DUEDATE],'dd/MM/yyyy') AS DUEDATE
                                            ,[CURRENCY]
                                            ,[AMTB]
                                            ,[VAT]
                                            ,[TOTALVAT]
                                            ,[WHTAX]
                                            ,[TOTAL_WHTAX]
                                            ,[NETPAID]
                                            ,[BEFORVATAMOUNT]
                                            ,[TOTAL_AMOUNT]
                                            ,[CREATEBY]
                                            ,FORMAT([CREATEDATE],'dd/MM/yyyy') AS CREATEDATE
                                            ,[UPDATEBY]
                                            ,FORMAT([UPDATEDATE],'dd/MM/yyyy') AS UPDATEDATE
                                            ,[STATUS]
                                            FROM [dbSCM].[dbo].[EBILLING_DETAIL]
                                            WHERE DOCUMENTNO = @DOCUMENTNO";
            cmdHead.Parameters.Add(new SqlParameter("@DOCUMENTNO", obj.DocumentNo));
            DataTable dtHead = oConSCM.Query(cmdHead);
            if (dtHead.Rows.Count > 0)
            {
                foreach (DataRow drow in dtHead.Rows)
                {
                    ReportDetail MData = new ReportDetail();
                    MData.INVOICENO = drow["INVOICENO"].ToString();
                    MData.INVOICEDATE = drow["INVOICEDATE"].ToString();
                    MData.TOTALVAT = Convert.ToDecimal(drow["TOTALVAT"].ToString());
                    MData.WHTAX = Convert.ToDecimal(drow["TOTAL_WHTAX"].ToString());
                    MData.TOTALAMOUNT = Convert.ToDecimal(drow["TOTAL_AMOUNT"].ToString());


                    Data_list.Add(MData);
                }
            }

            return Ok(Data_list);
        }



        [HttpPost]
        [Route("PostConfirmACHeader")]
        public IActionResult PostConfirmACHeader([FromBody] MParammeter obj)
        {
            List<ReportHeader> Data_list = new List<ReportHeader>();
            string strVendorCode = "";




            string conditionInvDate = "";
            if (!string.IsNullOrEmpty(obj.InvoiceDateFrom) && !string.IsNullOrEmpty(obj.InvoiceDateTo))
            {
                conditionInvDate = $" AND INVOICEDATE >= '{obj.InvoiceDateFrom}' AND INVOICEDATE <= '{obj.InvoiceDateTo}' ";
            }

            SqlCommand cmdHead = new SqlCommand();
            cmdHead.CommandText = $@"WITH RankedInvoices AS (
                                                SELECT *,
                                                        ROW_NUMBER() OVER(PARTITION BY VENDORCODE ORDER BY DOCUMENTDATE DESC) AS rn
                                                FROM [dbSCM].[dbo].[EBILLING_HEADER]
                                                WHERE STATUS LIKE @STATUS  AND VENDORCODE LIKE  @VENDORCODE AND ACTYPE LIKE @ACTYPE {conditionInvDate} 
                                            )
                                            SELECT 
                                                DOCUMENTNO,
                                                FORMAT(DOCUMENTDATE,'dd/MM/yyyy') AS DOCUMENTDATE,
                                                PAYMENT_TERMS,
                                                FORMAT(DUEDATE,'dd/MM/yyyy') AS DUEDATE,
                                                VENDORCODE,
                                                INVOICENO,
                                                FORMAT(INVOICEDATE,'dd/MM/yyyy') AS INVOICEDATE,
                                                TAXID,
                                                ACTYPE,
                                                BILLERBY,
                                                FORMAT(BILLERDATE,'dd/MM/yyyy') AS BILLERDATE,
                                                RECEIVED_BILLERBY,
                                                FORMAT(RECEIVED_BILLERDATE,'dd/MM/yyyy') AS RECEIVED_BILLERDATE,
                                                REJECT_BY,
                                                FORMAT(REJECT_DATE,'dd/MM/yyyy') AS REJECT_DATE,
                                                CREATEBY,
                                                FORMAT(CREATEDATE,'dd/MM/yyyy') AS CREATEDATE,
                                                UPDATEBY,
                                                FORMAT(UPDATEDATE,'dd/MM/yyyy') AS UPDATEDATE,
                                                STATUS
                                            FROM RankedInvoices
                                           -- WHERE rn = 1";
            cmdHead.Parameters.Add(new SqlParameter("@VENDORCODE", obj.VenderCode));
            cmdHead.Parameters.Add(new SqlParameter("@STATUS", obj.status));
            cmdHead.Parameters.Add(new SqlParameter("@ACTYPE", obj.ACTYPE));
            DataTable dtHead = oConSCM.Query(cmdHead);
            if (dtHead.Rows.Count > 0)
            {
                foreach (DataRow drow in dtHead.Rows)
                {
                    ReportHeader MData = new ReportHeader();
                    MData.DOCUMENTNO = drow["DOCUMENTNO"].ToString();
                    MData.DUEDATE = drow["DUEDATE"].ToString();
                    MData.TAXID = drow["TAXID"].ToString();
                    MData.PAYMENT_TERMS = drow["PAYMENT_TERMS"].ToString();
                    MData.VENDORCODE = drow["VENDORCODE"].ToString();
                    MData.DATE = drow["CREATEDATE"].ToString();
                    MData.BILLERBY = drow["BILLERBY"].ToString();
                    MData.BILLERDATE = drow["BILLERDATE"].ToString();
                    MData.RECEIVED_BILLERBY = drow["RECEIVED_BILLERBY"].ToString();
                    MData.RECEIVED_BILLERDATE = drow["RECEIVED_BILLERDATE"].ToString();
                    MData.REJECT_BY = drow["REJECT_BY"].ToString();
                    MData.REJECT_DATE = drow["REJECT_DATE"].ToString();
                    MData.STATUS = drow["STATUS"].ToString();
                    MData.INVOICENO = drow["INVOICENO"].ToString();
                    MData.INVOICEDATE = drow["INVOICEDATE"].ToString();
                    MData.ACTYPE = drow["ACTYPE"].ToString();

                    string documentNo = MData.DOCUMENTNO;
                    string vendorCode = MData.VENDORCODE;
                    string status = MData.STATUS;
                    string ivno = MData.INVOICENO;


                    OracleCommand cmdVDNAME = new OracleCommand();
                    cmdVDNAME.CommandText = @"SELECT *
                                                FROM DST_ACMVD1
                                                WHERE KAISEQ = '999' AND TRIM(VENDER) = :VENDER";
                    cmdVDNAME.Parameters.Add(new OracleParameter(":VENDER", vendorCode));
                    DataTable dtVDNAME = oOraAL02.Query(cmdVDNAME);
                    if (dtVDNAME.Rows.Count > 0)
                    {
                        MData.VENDORNAME = dtVDNAME.Rows[0]["VDNAME"].ToString();
                        MData.ADDRES1 = dtVDNAME.Rows[0]["ADDR1"].ToString();
                        MData.ADDRES2 = dtVDNAME.Rows[0]["ADDR2"].ToString();
                        MData.ZIPCODE = dtVDNAME.Rows[0]["ZIPCODE"].ToString();
                        MData.TELNO = dtVDNAME.Rows[0]["TELNO"].ToString();
                        MData.FAXNO = dtVDNAME.Rows[0]["FAXNO"].ToString();
                    }

                    //OracleCommand selectACType = new OracleCommand();
                    //selectACType.CommandText = @"SELECT IVNO,ACTYPE
                    //                            FROM MC.DST_ACDAP1 D
                    //                            where IVNO = :IVNO";
                    //selectACType.Parameters.Add(new OracleParameter(":IVNO", ivno));
                    //DataTable dtACType = oOraAL02.Query(selectACType);
                    //if (dtACType.Rows.Count > 0)
                    //{
                    //    MData.ACTYPE = dtACType.Rows[0]["ACTYPE"].ToString();
                    //}


                    SqlCommand cmdTotal = new SqlCommand();
                    cmdTotal.CommandText = @"SELECT VENDORCODE,
                                                SUM(TOTAL_AMOUNT) AS TOTAL_AMOUNT,
                                                SUM(TOTAL_WHTAX) AS TOTAL_WHTAX,
                                                SUM(TOTAL_AMOUNT) - SUM(TOTAL_WHTAX) AS NETPAID
                                                FROM dbSCM.dbo.EBILLING_DETAIL
                                                WHERE [STATUS] LIKE @STATUS AND [VENDORCODE] = @VENDORCODE 
                                                GROUP BY VENDORCODE";
                    cmdTotal.Parameters.Add(new SqlParameter("@VENDORCODE", vendorCode));
                    cmdTotal.Parameters.Add(new SqlParameter("@STATUS", status));
                    DataTable dtTOTAL = oConSCM.Query(cmdTotal);
                    if (dtTOTAL.Rows.Count > 0)
                    {
                        DataRow t = dtTOTAL.Rows[0];

                        MData.TOTAL_AMOUNT = Convert.ToDecimal(t["TOTAL_AMOUNT"]);
                        MData.WHTAX = Convert.ToDecimal(t["TOTAL_WHTAX"]);
                        MData.NETPAID = Convert.ToDecimal(t["NETPAID"]);
                    }


                    SqlCommand cmdFile = new SqlCommand();
                    cmdFile.CommandText = @"SELECT [DOCUMENTNO]
                                                ,[FILE_NAME]
                                                ,[FILE_PATH]
                                                ,[CREATE_BY]
                                                ,[CREATE_DATE]
                                                FROM [dbSCM].[dbo].[EBILLING_ATTACH_FILE]
                                                WHERE DOCUMENTNO = @DOCUMENTNO";
                    cmdFile.Parameters.Add(new SqlParameter("@DOCUMENTNO", documentNo));
                    DataTable dtFile = oConSCM.Query(cmdFile);
                    if (dtFile.Rows.Count > 0)
                    {
                        DataRow t = dtFile.Rows[0];

                        MData.FILE_NAME = t["FILE_NAME"].ToString();
                    }

                    Data_list.Add(MData);
                }
            }

            return Ok(Data_list);
        }



        [HttpPost]
        [Route("PostReportACAndVendorDetail")]
        public IActionResult PostReportACAndVendorDetail([FromBody] MParammeter obj)
        {
            List<ReportDetail> Data_list = new List<ReportDetail>();


            SqlCommand cmdHead = new SqlCommand();
            cmdHead.CommandText = $@"SELECT [DOCUMENTNO]
                                            ,FORMAT([DOCUMENTDATE],'dd/MM/yyyy') AS DOCUMENTDATE
                                            ,[INVOICENO]
                                            ,FORMAT([INVOICEDATE],'dd/MM/yyyy') AS INVOICEDATE
                                            ,[VENDORCODE]
                                            ,[TAXID]
                                            ,[PAYMENT_TERMS]
                                            ,FORMAT([DUEDATE],'dd/MM/yyyy') AS DUEDATE
                                            ,[CURRENCY]
                                            ,[AMTB]
                                            ,[VAT]
                                            ,[TOTALVAT]
                                            ,[WHTAX]
                                            ,[TOTAL_WHTAX]
                                            ,[NETPAID]
                                            ,[BEFORVATAMOUNT]
                                            ,[TOTAL_AMOUNT]
                                            ,[CREATEBY]
                                            ,FORMAT([CREATEDATE],'dd/MM/yyyy') AS CREATEDATE
                                            ,[UPDATEBY]
                                            ,FORMAT([UPDATEDATE],'dd/MM/yyyy') AS UPDATEDATE
                                            ,[STATUS]
                                            FROM [dbSCM].[dbo].[EBILLING_DETAIL]
                                            WHERE DOCUMENTNO = @DOCUMENTNO";
            cmdHead.Parameters.Add(new SqlParameter("@DOCUMENTNO", obj.DocumentNo));
            DataTable dtHead = oConSCM.Query(cmdHead);
            if (dtHead.Rows.Count > 0)
            {
                foreach (DataRow drow in dtHead.Rows)
                {
                    ReportDetail MData = new ReportDetail();
                    MData.DOCUMENTNO = drow["DOCUMENTNO"].ToString();
                    MData.INVOICENO = drow["INVOICENO"].ToString();
                    MData.INVOICEDATE = drow["INVOICEDATE"].ToString();
                    MData.TAXID = drow["TAXID"].ToString();
                    MData.DUEDATE = drow["DUEDATE"].ToString();
                    MData.AMTB = Convert.ToDecimal(drow["AMTB"].ToString());
                    MData.VAT = Convert.ToDecimal(drow["VAT"].ToString());
                    MData.TOTALVAT = Convert.ToDecimal(drow["TOTALVAT"].ToString());
                    MData.RATE = drow["WHTAX"].ToString();
                    MData.WHTAX = Convert.ToDecimal(drow["TOTAL_WHTAX"].ToString());
                    MData.TOTALAMOUNT = Convert.ToDecimal(drow["TOTAL_AMOUNT"].ToString());


                    Data_list.Add(MData);
                }
            }

            return Ok(Data_list);
        }


        [HttpPost]
        [Route("PostConfirmBilling")]
        public IActionResult PostConfirmBilling([FromBody] MReceiveBuilling obj)
        {
            if (obj.InvoiceNos == null || obj.InvoiceNos.Count == 0)
                return BadRequest("Invoice list is empty");

            // 🔹 สร้าง parameter list สำหรับ IN (...)
            var parameters = obj.InvoiceNos.Select((x, i) => $"@inv{i}").ToList();
            string inClause = string.Join(",", parameters);

            // ===== SELECT DETAIL =====
            SqlCommand selectDoc = new SqlCommand();
            selectDoc.CommandText = $@"SELECT *
                                FROM [dbSCM].[dbo].[EBILLING_DETAIL]
                                WHERE INVOICENO IN ({inClause}) AND STATUS = 'WAITING'";
            for (int i = 0; i < obj.InvoiceNos.Count; i++)
            {
                selectDoc.Parameters.AddWithValue(parameters[i], obj.InvoiceNos[i]);
            }

            DataTable dtDoc = oConSCM.Query(selectDoc);

            if (dtDoc.Rows.Count > 0)
            {
                // ===== UPDATE HEADER =====
                SqlCommand updateHeader = new SqlCommand();
                updateHeader.CommandText = @"UPDATE [EBILLING_HEADER]
                                     SET RECEIVED_BILLERBY = @RECEIVEDBY,
                                         RECEIVED_BILLERDATE = GETDATE(),
                                         [STATUS] = 'CONFIRM'
                                     WHERE DOCUMENTNO = @DOCUMENTNO";
                updateHeader.Parameters.AddWithValue("@DOCUMENTNO", obj.DocumentNo);
                updateHeader.Parameters.AddWithValue("@RECEIVEDBY", obj.IssuedBy);
                oConSCM.Query(updateHeader);

                // ===== UPDATE DETAIL พร้อม REMARK =====
                for (int i = 0; i < obj.InvoiceNos.Count; i++)
                {
                    string invoiceNo = obj.InvoiceNos[i];
                    string remark = obj.Remarks != null && obj.Remarks.Count > i ? obj.Remarks[i] : "";

                    SqlCommand updateDetail = new SqlCommand();
                    updateDetail.CommandText = @"UPDATE [EBILLING_DETAIL]
                                         SET [STATUS] = 'CONFIRM',
                                             [REMARK] = @remark
                                         WHERE INVOICENO = @invoiceNo";
                    updateDetail.Parameters.AddWithValue("@invoiceNo", invoiceNo);
                    updateDetail.Parameters.AddWithValue("@remark", remark);
                    oConSCM.Query(updateDetail);
                }
            }

            return Ok();
        }



        [HttpPost]
        [Route("PostRejectbilling")]
        public IActionResult PostRejectbilling([FromBody] MReceiveBuilling obj)
        {
            if (obj.InvoiceNos == null || obj.InvoiceNos.Count == 0)
                return BadRequest("Invoice list is empty");

            // 🔹 สร้าง parameter list สำหรับ IN (...)
            var parameters = obj.InvoiceNos.Select((x, i) => $"@inv{i}").ToList();
            string inClause = string.Join(",", parameters);

            // ===== SELECT DETAIL =====
            SqlCommand selectDoc = new SqlCommand();
            selectDoc.CommandText = $@"SELECT *
                                FROM [dbSCM].[dbo].[EBILLING_DETAIL]
                                WHERE INVOICENO IN ({inClause}) AND STATUS IN ('WAITING','CONFIRM','CANCEL')";
            for (int i = 0; i < obj.InvoiceNos.Count; i++)
            {
                selectDoc.Parameters.AddWithValue(parameters[i], obj.InvoiceNos[i]);
            }

            DataTable dtDoc = oConSCM.Query(selectDoc);

            if (dtDoc.Rows.Count > 0)
            {
                // ===== UPDATE HEADER =====
                SqlCommand updateHeader = new SqlCommand();
                updateHeader.CommandText = @"UPDATE [EBILLING_HEADER]
                                     SET REJECT_BY = @RECEIVEDBY,
                                         REJECT_DATE = GETDATE(),
                                         [STATUS] = 'REJECT'
                                     WHERE DOCUMENTNO = @DOCUMENTNO";
                updateHeader.Parameters.AddWithValue("@DOCUMENTNO", obj.DocumentNo);
                updateHeader.Parameters.AddWithValue("@RECEIVEDBY", obj.IssuedBy);
                oConSCM.Query(updateHeader);

                // ===== UPDATE DETAIL พร้อม REMARK =====
                for (int i = 0; i < obj.InvoiceNos.Count; i++)
                {
                    string invoiceNo = obj.InvoiceNos[i];
                    string remark = obj.Remarks != null && obj.Remarks.Count > i ? obj.Remarks[i] : "";

                    SqlCommand updateDetail = new SqlCommand();
                    updateDetail.CommandText = @"UPDATE [EBILLING_DETAIL]
                                         SET [STATUS] = 'REJECT',
                                             [REMARK] = @remark
                                         WHERE INVOICENO = @invoiceNo";
                    updateDetail.Parameters.AddWithValue("@invoiceNo", invoiceNo);
                    updateDetail.Parameters.AddWithValue("@remark", remark);
                    oConSCM.Query(updateDetail);
                }
            }

            return Ok();
        }



        [HttpPost]
        [Route("PostCancelConfirmBilling")]
        public IActionResult PostCancelConfirmBilling([FromBody] MReceiveBuilling obj)
        {
            if (obj.InvoiceNos == null || obj.InvoiceNos.Count == 0)
                return BadRequest("Invoice list is empty");

            // 🔹 สร้าง parameter list สำหรับ IN (...)
            var parameters = obj.InvoiceNos.Select((x, i) => $"@inv{i}").ToList();
            string inClause = string.Join(",", parameters);

            // ===== SELECT DETAIL =====
            SqlCommand selectDoc = new SqlCommand();
            selectDoc.CommandText = $@"SELECT *
                                FROM [dbSCM].[dbo].[EBILLING_DETAIL]
                                WHERE INVOICENO IN ({inClause}) AND STATUS = 'CONFIRM'";
            for (int i = 0; i < obj.InvoiceNos.Count; i++)
            {
                selectDoc.Parameters.AddWithValue(parameters[i], obj.InvoiceNos[i]);
            }

            DataTable dtDoc = oConSCM.Query(selectDoc);

            if (dtDoc.Rows.Count > 0)
            {
                // ===== UPDATE HEADER =====
                SqlCommand updateHeader = new SqlCommand();
                updateHeader.CommandText = @"UPDATE [EBILLING_HEADER]
                                     SET CANCEL_BY = @RECEIVEDBY,
                                         CANCEL_DATE = GETDATE(),
                                         [STATUS] = 'CANCEL'
                                     WHERE DOCUMENTNO = @DOCUMENTNO";
                updateHeader.Parameters.AddWithValue("@DOCUMENTNO", obj.DocumentNo);
                updateHeader.Parameters.AddWithValue("@RECEIVEDBY", obj.IssuedBy);
                oConSCM.Query(updateHeader);

                // ===== UPDATE DETAIL พร้อม REMARK =====
                for (int i = 0; i < obj.InvoiceNos.Count; i++)
                {
                    string invoiceNo = obj.InvoiceNos[i];
                    string remark = obj.Remarks != null && obj.Remarks.Count > i ? obj.Remarks[i] : "";

                    SqlCommand updateDetail = new SqlCommand();
                    updateDetail.CommandText = @"UPDATE [EBILLING_DETAIL]
                                         SET [STATUS] = 'CANCEL',
                                             [REMARK] = @remark
                                         WHERE INVOICENO = @invoiceNo";
                    updateDetail.Parameters.AddWithValue("@invoiceNo", invoiceNo);
                    updateDetail.Parameters.AddWithValue("@remark", remark);
                    oConSCM.Query(updateDetail);
                }
            }

            return Ok();
        }



        [HttpPost]
        [Route("PostRejectPaymentBilling")]
        public IActionResult PostRejectPaymentBilling([FromBody] MReceiveBuilling obj)
        {
            if (obj.InvoiceNos == null || obj.InvoiceNos.Count == 0)
                return BadRequest("Invoice list is empty");

            // 🔹 สร้าง parameter list สำหรับ IN (...)
            var parameters = obj.InvoiceNos.Select((x, i) => $"@inv{i}").ToList();
            string inClause = string.Join(",", parameters);

            // ===== SELECT DETAIL =====
            SqlCommand selectDoc = new SqlCommand();
            selectDoc.CommandText = $@"SELECT *
                                FROM [dbSCM].[dbo].[EBILLING_DETAIL]
                                WHERE INVOICENO IN ({inClause}) AND STATUS = 'PAYMENT'";
            for (int i = 0; i < obj.InvoiceNos.Count; i++)
            {
                selectDoc.Parameters.AddWithValue(parameters[i], obj.InvoiceNos[i]);
            }

            DataTable dtDoc = oConSCM.Query(selectDoc);

            if (dtDoc.Rows.Count > 0)
            {
                // ===== UPDATE HEADER =====
                SqlCommand updateHeader = new SqlCommand();
                updateHeader.CommandText = @"UPDATE [EBILLING_HEADER]
                                     SET REJECT_PAYMENT_BY = @RECEIVEDBY,
                                         REJECT_PAYMENT_DATE = GETDATE(),
                                         [STATUS] = 'REJECT_AC'
                                     WHERE DOCUMENTNO = @DOCUMENTNO";
                updateHeader.Parameters.AddWithValue("@DOCUMENTNO", obj.DocumentNo);
                updateHeader.Parameters.AddWithValue("@RECEIVEDBY", obj.IssuedBy);
                oConSCM.Query(updateHeader);

                // ===== UPDATE DETAIL พร้อม REMARK =====
                for (int i = 0; i < obj.InvoiceNos.Count; i++)
                {
                    string invoiceNo = obj.InvoiceNos[i];
                    string remark = obj.Remarks != null && obj.Remarks.Count > i ? obj.Remarks[i] : "";

                    SqlCommand updateDetail = new SqlCommand();
                    updateDetail.CommandText = @"UPDATE [EBILLING_DETAIL]
                                         SET [STATUS] = 'REJECT_AC',
                                             [REMARK] = @remark
                                         WHERE INVOICENO = @invoiceNo";
                    updateDetail.Parameters.AddWithValue("@invoiceNo", invoiceNo);
                    updateDetail.Parameters.AddWithValue("@remark", remark);
                    oConSCM.Query(updateDetail);
                }
            }

            return Ok();
        }




        [HttpPost]
        [Route("PostReportACHeader")]
        public IActionResult PostReportACPerVendor([FromBody] MParammeter obj)
        {
            List<ReportHeader> Data_list = new List<ReportHeader>();


            string conditionInvDate = "";
            if (!string.IsNullOrEmpty(obj.InvoiceDateFrom) && !string.IsNullOrEmpty(obj.InvoiceDateTo))
            {
                conditionInvDate = $" AND INVOICEDATE >= '{obj.InvoiceDateFrom}' AND INVOICEDATE <= '{obj.InvoiceDateTo}' ";
            }

            SqlCommand cmdHead = new SqlCommand();
            cmdHead.CommandText = $@"WITH RankedInvoices AS (
                                            SELECT *,
                                                   ROW_NUMBER() OVER(PARTITION BY VENDORCODE ORDER BY DOCUMENTDATE DESC) AS rn
                                            FROM [dbSCM].[dbo].[EBILLING_HEADER]
                                            WHERE STATUS <> 'WAITING' AND STATUS LIKE @STATUS  {conditionInvDate}
                                        )
                                        SELECT 
                                            DOCUMENTNO,
                                            FORMAT(DOCUMENTDATE,'dd/MM/yyyy') AS DOCUMENTDATE,
                                            PAYMENT_TERMS,
                                            FORMAT(DUEDATE,'dd/MM/yyyy') AS DUEDATE,
                                            VENDORCODE,
                                            FORMAT(INVOICEDATE,'dd/MM/yyyy') AS INVOICEDATE,
                                            TAXID,
                                            BILLERBY,
                                            FORMAT(BILLERDATE,'dd/MM/yyyy') AS BILLERDATE,
                                            RECEIVED_BILLERBY,
                                            FORMAT(RECEIVED_BILLERDATE,'dd/MM/yyyy') AS RECEIVED_BILLERDATE,
                                            [PAYBY],
                                            FORMAT([PAYDATE],'dd/MM/yyyy') AS [PAYDATE],
                                            CREATEBY,
                                            FORMAT(CREATEDATE,'dd/MM/yyyy') AS CREATEDATE,
                                            UPDATEBY,
                                            FORMAT(UPDATEDATE,'dd/MM/yyyy') AS UPDATEDATE,
                                            STATUS
                                        FROM RankedInvoices
                                        WHERE rn = 1";
            cmdHead.Parameters.Add(new SqlParameter("@STATUS", obj.status));
            DataTable dtHead = oConSCM.Query(cmdHead);
            if (dtHead.Rows.Count > 0)
            {
                foreach (DataRow drow in dtHead.Rows)
                {
                    ReportHeader MData = new ReportHeader();
                    MData.DOCUMENTNO = drow["DOCUMENTNO"].ToString();
                    MData.DUEDATE = drow["DUEDATE"].ToString();
                    MData.TAXID = drow["TAXID"].ToString();
                    MData.PAYMENT_TERMS = drow["PAYMENT_TERMS"].ToString();
                    MData.VENDORCODE = drow["VENDORCODE"].ToString();
                    MData.DATE = drow["CREATEDATE"].ToString();
                    MData.BILLERBY = drow["BILLERBY"].ToString();
                    MData.BILLERDATE = drow["BILLERDATE"].ToString();
                    MData.RECEIVED_BILLERBY = drow["RECEIVED_BILLERBY"].ToString();
                    MData.RECEIVED_BILLERDATE = drow["RECEIVED_BILLERDATE"].ToString();
                    MData.PAYMENT_BY = drow["PAYBY"].ToString();
                    MData.PAYMENT_DATE = drow["PAYDATE"].ToString();
                    MData.STATUS = drow["STATUS"].ToString();

                    string vendorCode = MData.VENDORCODE;
                    string status = MData.STATUS;


                    OracleCommand cmdVDNAME = new OracleCommand();
                    cmdVDNAME.CommandText = @"SELECT *
                                                FROM DST_ACMVD1
                                                WHERE KAISEQ = '999' AND TRIM(VENDER) = :VENDER";
                    cmdVDNAME.Parameters.Add(new OracleParameter(":VENDER", vendorCode));
                    DataTable dtVDNAME = oOraAL02.Query(cmdVDNAME);
                    if (dtVDNAME.Rows.Count > 0)
                    {
                        MData.VENDORNAME = dtVDNAME.Rows[0]["VDNAME"].ToString();
                        MData.ADDRES1 = dtVDNAME.Rows[0]["ADDR1"].ToString();
                        MData.ADDRES2 = dtVDNAME.Rows[0]["ADDR2"].ToString();
                        MData.ZIPCODE = dtVDNAME.Rows[0]["ZIPCODE"].ToString();
                        MData.TELNO = dtVDNAME.Rows[0]["TELNO"].ToString();
                        MData.FAXNO = dtVDNAME.Rows[0]["FAXNO"].ToString();
                    }


                    SqlCommand cmdTotal = new SqlCommand();
                    cmdTotal.CommandText = @"SELECT VENDORCODE,
                                                SUM(TOTALVAT) AS TOTALVAT,
                                                SUM(TOTAL_AMOUNT) AS TOTAL_AMOUNT,
                                                SUM(TOTAL_WHTAX) AS TOTAL_WHTAX,
                                                SUM(TOTAL_AMOUNT) - SUM(TOTAL_WHTAX) AS NETPAID
                                                FROM dbSCM.dbo.EBILLING_DETAIL
                                                WHERE [STATUS] LIKE @STATUS AND [VENDORCODE] = @VENDORCODE 
                                                GROUP BY VENDORCODE";
                    cmdTotal.Parameters.Add(new SqlParameter("@VENDORCODE", vendorCode));
                    cmdTotal.Parameters.Add(new SqlParameter("@STATUS", status));
                    DataTable dtTOTAL = oConSCM.Query(cmdTotal);
                    if (dtTOTAL.Rows.Count > 0)
                    {
                        DataRow t = dtTOTAL.Rows[0];

                        MData.TOTALVAT = Convert.ToDecimal(t["TOTALVAT"]);
                        MData.TOTAL_AMOUNT = Convert.ToDecimal(t["TOTAL_AMOUNT"]);
                        MData.WHTAX = Convert.ToDecimal(t["TOTAL_WHTAX"]);
                        MData.NETPAID = Convert.ToDecimal(t["NETPAID"]);
                    }

                    Data_list.Add(MData);
                }
            }

            return Ok(Data_list);
        }



        [HttpPost]
        [Route("PostReportACDetail")]
        public IActionResult PostReportACDetail([FromBody] MParammeter obj)
        {
            List<ReportDetail> Data_list = new List<ReportDetail>();


            SqlCommand cmdHead = new SqlCommand();
            cmdHead.CommandText = $@"SELECT [DOCUMENTNO]
                                            ,FORMAT([DOCUMENTDATE],'dd/MM/yyyy') AS DOCUMENTDATE
                                            ,[INVOICENO]
                                            ,FORMAT([INVOICEDATE],'dd/MM/yyyy') AS INVOICEDATE
                                            ,[VENDORCODE]
                                            ,[TAXID]
                                            ,[PAYMENT_TERMS]
                                            ,FORMAT([DUEDATE],'dd/MM/yyyy') AS DUEDATE
                                            ,[CURRENCY]
                                            ,[AMTB]
                                            ,[VAT]
                                            ,[TOTALVAT]
                                            ,[WHTAX]
                                            ,[TOTAL_WHTAX]
                                            ,[NETPAID]
                                            ,[BEFORVATAMOUNT]
                                            ,[TOTAL_AMOUNT]
                                            ,[CREATEBY]
                                            ,FORMAT([CREATEDATE],'dd/MM/yyyy') AS CREATEDATE
                                            ,[UPDATEBY]
                                            ,FORMAT([UPDATEDATE],'dd/MM/yyyy') AS UPDATEDATE
                                            ,[STATUS]
                                            FROM [dbSCM].[dbo].[EBILLING_DETAIL]
                                            WHERE [STATUS] = @STATUS AND  VENDORCODE = @VENDORCODE";
            cmdHead.Parameters.Add(new SqlParameter("@VENDORCODE", obj.VenderCode));
            cmdHead.Parameters.Add(new SqlParameter("@STATUS", obj.status));
            DataTable dtHead = oConSCM.Query(cmdHead);
            if (dtHead.Rows.Count > 0)
            {
                foreach (DataRow drow in dtHead.Rows)
                {
                    ReportDetail MData = new ReportDetail();
                    MData.DOCUMENTNO = drow["DOCUMENTNO"].ToString();
                    MData.INVOICENO = drow["INVOICENO"].ToString();
                    MData.TAXID = drow["TAXID"].ToString();
                    MData.DUEDATE = drow["DUEDATE"].ToString();
                    MData.AMTB = Convert.ToDecimal(drow["AMTB"].ToString());
                    MData.VAT = Convert.ToDecimal(drow["VAT"].ToString());
                    MData.TOTALVAT = Convert.ToDecimal(drow["TOTALVAT"].ToString());
                    MData.RATE = drow["WHTAX"].ToString();
                    MData.WHTAX = Convert.ToDecimal(drow["TOTAL_WHTAX"].ToString());
                    MData.TOTALAMOUNT = Convert.ToDecimal(drow["TOTAL_AMOUNT"].ToString());


                    Data_list.Add(MData);
                }
            }

            return Ok(Data_list);
        }



        [HttpPost]
        [Route("PostConfirmACDetail")]
        public IActionResult PostConfirmACDetail([FromBody] MParammeter obj)
        {
            List<ReportDetail> Data_list = new List<ReportDetail>();


            SqlCommand cmdDetail = new SqlCommand();
            cmdDetail.CommandText = $@"SELECT [DOCUMENTNO]
                                    ,FORMAT([DOCUMENTDATE],'dd/MM/yyyy') AS DOCUMENTDATE
                                    ,[INVOICENO]
                                    ,FORMAT([INVOICEDATE],'dd/MM/yyyy') AS INVOICEDATE
                                    ,[VENDORCODE]
                                    ,[TAXID]
                                    ,[PAYMENT_TERMS]
                                    ,FORMAT([DUEDATE],'dd/MM/yyyy') AS DUEDATE
                                    ,[ACTYPE]
                                    ,[CURRENCY]
                                    ,[AMTB]
                                    ,[VAT]
                                    ,[TOTALVAT]
                                    ,[WHTAX]
                                    ,[TOTAL_WHTAX]
                                    ,[NETPAID]
                                    ,[BEFORVATAMOUNT]
                                    ,[TOTAL_AMOUNT]
                                    ,[CREATEBY]
                                    ,[CREATEDATE]
                                    ,[UPDATEBY]
                                    ,[UPDATEDATE]
                                    ,[STATUS]
                                    ,[REMARK]
                                    FROM [dbSCM].[dbo].[EBILLING_DETAIL]
                                    WHERE DOCUMENTNO = @DOCUMENTNO";
            cmdDetail.Parameters.Add(new SqlParameter("@DOCUMENTNO", obj.DocumentNo));
            DataTable dtDetail = oConSCM.Query(cmdDetail);
            if (dtDetail.Rows.Count > 0)
            {
                foreach (DataRow drow in dtDetail.Rows)
                {
                    ReportDetail MData = new ReportDetail();
                    MData.DOCUMENTNO = drow["DOCUMENTNO"].ToString();
                    MData.DOCUMENTDATE = drow["DOCUMENTDATE"].ToString();
                    MData.INVOICENO = drow["INVOICENO"].ToString();
                    MData.VENDORCODE = drow["VENDORCODE"].ToString();
                    MData.INVOICEDATE = drow["INVOICEDATE"].ToString();
                    MData.PAYMENT_TERMS = drow["PAYMENT_TERMS"].ToString();
                    MData.DUEDATE = drow["DUEDATE"].ToString();
                    MData.CURRENCY = drow["CURRENCY"].ToString();
                    MData.AMTB = Convert.ToDecimal(drow["AMTB"].ToString());
                    MData.VAT = Convert.ToDecimal(drow["VAT"].ToString());
                    MData.TOTALVAT = Convert.ToDecimal(drow["TOTALVAT"].ToString());
                    MData.RATE = drow["WHTAX"].ToString();
                    MData.WHTAX = Convert.ToDecimal(drow["TOTAL_WHTAX"].ToString());
                    MData.TOTALAMOUNT = Convert.ToDecimal(drow["TOTAL_AMOUNT"].ToString());
                    MData.STATUS = drow["STATUS"].ToString();
                    MData.REMARK = drow["REMARK"].ToString();

                    string vendorCode = drow["VENDORCODE"].ToString();


                    SqlCommand cmdHead = new SqlCommand();
                    cmdHead.CommandText = $@"SELECT [DOCUMENTNO]
                                                    ,[RECEIVED_BILLERBY]
                                                    ,FORMAT([RECEIVED_BILLERDATE],'dd/MM/yyyy') AS RECEIVED_BILLERDATE
                                                    ,[CREATEBY]
                                                    ,FORMAT([CREATEDATE],'dd/MM/yyyy') AS CREATEDATE
                                                    FROM [dbSCM].[dbo].[EBILLING_HEADER]
                                                    WHERE DOCUMENTNO = @DOCUMENTNO";
                    cmdHead.Parameters.Add(new SqlParameter("@DOCUMENTNO", obj.DocumentNo));
                    DataTable dtHead = oConSCM.Query(cmdHead);
                    if (dtHead.Rows.Count > 0)
                    {
                        foreach (DataRow item in dtHead.Rows)
                        {
                            MData.RECEIVED_BILLERBY = item["RECEIVED_BILLERBY"].ToString();
                            MData.RECEIVED_BILLERDATE = item["RECEIVED_BILLERDATE"].ToString();
                            MData.CREATEBY = item["CREATEBY"].ToString();
                            MData.CREATEDATE = item["CREATEDATE"].ToString();
                        }
                    }

                    OracleCommand cmdVDNAME = new OracleCommand();
                    cmdVDNAME.CommandText = @"SELECT *
                                                FROM DST_ACMVD1
                                                WHERE KAISEQ = '999' AND TRIM(VENDER) = :VENDER";
                    cmdVDNAME.Parameters.Add(new OracleParameter(":VENDER", vendorCode));
                    DataTable dtVDNAME = oOraAL02.Query(cmdVDNAME);
                    if (dtVDNAME.Rows.Count > 0)
                    {
                        MData.VENDORNAME = dtVDNAME.Rows[0]["VDNAME"].ToString();
                        MData.ADDR1 = dtVDNAME.Rows[0]["ADDR1"].ToString();
                        MData.ADDR2 = dtVDNAME.Rows[0]["ADDR2"].ToString();
                        MData.ZIPCODE = dtVDNAME.Rows[0]["ZIPCODE"].ToString();
                        MData.TELNO = dtVDNAME.Rows[0]["TELNO"].ToString();
                        MData.FAXNO = dtVDNAME.Rows[0]["FAXNO"].ToString();
                    }


                    Data_list.Add(MData);
                }
            }

            return Ok(Data_list);
        }


        [HttpPost]
        [Route("PostReportInvoiceByAC")]
        public IActionResult PostReportInvoiceByAC([FromBody] MParammeter obj)
        {
            List<ReportInvoiceByAC> Data_list = new List<ReportInvoiceByAC>();

            string invoiceNos = string.IsNullOrEmpty(obj.InvoiceNo) ? "%" : obj.InvoiceNo.Trim();
            string conditionInvDate = "";
            if (!string.IsNullOrEmpty(obj.InvoiceDateFrom) && !string.IsNullOrEmpty(obj.InvoiceDateTo))
            {
                conditionInvDate = " AND D.IVDATE BETWEEN :InvDateFrom AND :InvDateTo ";
            }

            #region ===== Oracle : Invoice Master =====

            OracleCommand sqlSelect = new OracleCommand();
            sqlSelect.CommandText = $@"SELECT DISTINCT 
                                        D.VENDER,
                                        D.IVNO,
                                        CASE
                                            WHEN REGEXP_LIKE(TRIM(D.IVDATE), '^[0-9]{{8}}$')
                                            THEN TO_CHAR(TO_DATE(D.IVDATE, 'YYYYMMDD'), 'DD/MM/YYYY')
                                            ELSE NULL
                                        END AS IVDATE,
                                        D.VDNAME,
                                        D.CURR,
                                        D.AMTB,
                                        D.VATIN,
                                        V.PAYTRM,
                                        V.TAXID,
                                        D.ACTYPE
                                    FROM MC.DST_ACDAP1 D
                                    LEFT JOIN DST_ACMVD1 V ON V.VENDER = D.VENDER
                                    WHERE V.PAYTRM IS NOT NULL
                                      AND TRIM(D.VENDER) LIKE :VENDER
                                      AND D.APBIT = 'F'
                                      AND D.PAYBIT IN ('U','F')
                                      AND TRIM(D.ACTYPE) LIKE '{obj.ACTYPE}'
                                      AND TRIM(D.IVNO) LIKE :IVNO
                                      {conditionInvDate}";

            sqlSelect.Parameters.Add(new OracleParameter(":VENDER", obj.VenderCode));
            sqlSelect.Parameters.Add(new OracleParameter(":IVNO", invoiceNos));
            //  sqlSelect.Parameters.Add(new OracleParameter(":ACTYPE", obj.ACTYPE + "%"));

            if (!string.IsNullOrEmpty(obj.InvoiceDateFrom) && !string.IsNullOrEmpty(obj.InvoiceDateTo))
            {
                sqlSelect.Parameters.Add(new OracleParameter(":InvDateFrom", obj.InvoiceDateFrom));
                sqlSelect.Parameters.Add(new OracleParameter(":InvDateTo", obj.InvoiceDateTo));
            }

            DataTable dtOracle = oOraAL02.Query(sqlSelect);

            #endregion

            #region ===== Loop Oracle → Check EBILLING_DETAIL =====

            foreach (DataRow orow in dtOracle.Rows)
            {
                ReportInvoiceByAC MData = new ReportInvoiceByAC();

                string invoiceNo = orow["IVNO"].ToString();
                string vendorCode = orow["VENDER"].ToString();

                // ---------- Oracle ----------
                MData.INVOICENO = invoiceNo;
                MData.INVOICEDATE = orow["IVDATE"].ToString();
                MData.PAYMENT_TERMS = orow["PAYTRM"].ToString();
                MData.CURRENCY = orow["CURR"].ToString();
                MData.AMTB = Convert.ToDecimal(orow["AMTB"]);
                MData.TOTALVAT = Convert.ToDecimal(orow["VATIN"]);
                MData.STATUS = "NEW";
                MData.ACTYPE = orow["ACTYPE"].ToString();

                // ---------- SQL Server : EBILLING_DETAIL ----------
                SqlCommand cmdHead = new SqlCommand();
                cmdHead.CommandText = @"SELECT *
                                        FROM [dbSCM].[dbo].[EBILLING_DETAIL]
                                        WHERE VENDORCODE = @VENDORCODE
                                          AND INVOICENO = @INVOICENO";
                cmdHead.Parameters.Add(new SqlParameter("@VENDORCODE", vendorCode));
                cmdHead.Parameters.Add(new SqlParameter("@INVOICENO", invoiceNo));

                DataTable dtHead = oConSCM.Query(cmdHead);

                if (dtHead.Rows.Count > 0)
                {
                    DataRow drow = dtHead.Rows[0];

                    MData.DUEDATE = drow["DUEDATE"] == DBNull.Value ? "" : Convert.ToDateTime(drow["DUEDATE"]).ToString("dd/MM/yyyy");
                    MData.WHTAX = drow["WHTAX"].ToString();
                    MData.TOTAL_WHTAX = drow["TOTAL_WHTAX"] == DBNull.Value ? 0 : Convert.ToDecimal(drow["TOTAL_WHTAX"]);
                    MData.TOTAL_AMOUNT = drow["TOTAL_AMOUNT"] == DBNull.Value ? 0 : Convert.ToDecimal(drow["TOTAL_AMOUNT"]);
                    MData.STATUS = drow["STATUS"].ToString();
                }
                else
                {
                    // ---------- Default when not exist ----------
                    MData.DUEDATE = "";
                    MData.WHTAX = "0";
                    MData.TOTAL_WHTAX = 0;
                    MData.TOTAL_AMOUNT = 0;
                }

                // ---------- Vendor Name ----------
                OracleCommand cmdVDNAME = new OracleCommand();
                cmdVDNAME.CommandText = @"SELECT VENDER,VDNAME
                                                FROM DST_ACMVD1
                                                WHERE KAISEQ = '999' AND TRIM(VENDER) = :VENDER";

                cmdVDNAME.Parameters.Add(new OracleParameter(":VENDER", vendorCode.Trim()));
                DataTable dtVDNAME = oOraAL02.Query(cmdVDNAME);

                if (dtVDNAME.Rows.Count > 0)
                {
                    MData.VENDORCODE = dtVDNAME.Rows[0]["VENDER"].ToString();
                    MData.VENDORNAME = dtVDNAME.Rows[0]["VDNAME"].ToString();
                }

                Data_list.Add(MData);
            }

            #endregion

            return Ok(Data_list);
        }



        [HttpPost]
        [Route("PostPayment")]
        public IActionResult PostPayment([FromBody] MPayment obj)
        {

            SqlCommand selectDoc = new SqlCommand();
            selectDoc.CommandText = $@"SELECT *
                                            FROM [dbSCM].[dbo].[EBILLING_DETAIL]
                                            WHERE VENDORCODE LIKE {obj.VendorCode}";
            DataTable dtDoc = oConSCM.Query(selectDoc);
            if (dtDoc.Rows.Count > 0)
            {
                foreach (DataRow item in dtDoc.Rows)
                {
                    string docNo = item["DOCUMENTNO"].ToString();


                    SqlCommand updateStatusHead = new SqlCommand();
                    updateStatusHead.CommandText = $@"UPDATE [EBILLING_HEADER]
                                                SET PAYBY = @PAYBY , PAYDATE = GETDATE() , [STATUS] = 'PAYMENT'
                                                WHERE DOCUMENTNO = @DOCUMENTNO";
                    updateStatusHead.Parameters.Add(new SqlParameter("@DOCUMENTNO", docNo));
                    updateStatusHead.Parameters.Add(new SqlParameter("@PAYBY", obj.PayBy));
                    oConSCM.Query(updateStatusHead);


                    SqlCommand updateStatusDeatil = new SqlCommand();
                    updateStatusDeatil.CommandText = $@"UPDATE [EBILLING_DETAIL]
                                                          SET [STATUS] = 'PAYMENT'
                                                          WHERE [INVOICENO] IN {obj.InvoiceNo} AND DOCUMENTNO = @DOCUMENTNO";
                    updateStatusDeatil.Parameters.Add(new SqlParameter("@DOCUMENTNO", docNo));
                    oConSCM.Query(updateStatusDeatil);
                }
            }

            return Ok();
        }
    }
}
