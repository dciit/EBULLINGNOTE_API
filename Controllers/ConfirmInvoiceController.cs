using System.Data;
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
            string sub1 = strRunningNbr.Substring(0,7);
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
        [Route("PostSearchInvoiceRequet")]
        public IActionResult PostSearchInvoiceRequet([FromBody] MParammeter obj)
        {
            List<DataForConfirmInvoice> Data_list = new List<DataForConfirmInvoice>();
            string invoiceNo = string.IsNullOrEmpty(obj.InvoiceNo) ? "%" : obj.InvoiceNo;

        
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
                                                D.VATCALC,
                                                D.VATIN,
                                                D.TOTAMT,
                                                D.APBIT,
                                                D.SLBIT,
                                                D.CHQBIT,
                                                D.PAYBIT,
                                                D.HTANTO,
                                                D.CDATE,
                                                D.INCOTERM,
                                                D.DOCNO_RUNNO,
                                                D.ACBIT,
                                                V.PAYTRM,
                                                V.TAXID
                                            FROM MC.DST_ACDAP1 D
                                            LEFT JOIN DST_ACMVD1 V 
                                                ON V.VENDER = D.VENDER
                                            WHERE V.PAYTRM IS NOT NULL
                                              AND TRIM(D.VENDER) = :VENDER
                                              AND D.APBIT = 'F'
                                              AND D.PAYBIT IN ('U')
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

          
            SqlCommand cmdHeader = new SqlCommand("SELECT DISTINCT INVOICENO FROM EBILLING_HEADER WHERE INVOICENO IS NOT NULL");
            DataTable dtHeader = oConSCM.Query(cmdHeader);
            var headerInvoiceList = dtHeader.AsEnumerable().Select(r => r["INVOICENO"].ToString()).ToHashSet();

         
            var filteredRows = dtOracle.AsEnumerable()
                                       .Where(r => !headerInvoiceList.Contains(r["IVNO"].ToString()))
                                       .GroupBy(r => r["IVNO"].ToString()) 
                                       .Select(g => g.First())
                                       .ToList();
            int number = 1;

            foreach (var drow in filteredRows)
            {
                DataForConfirmInvoice MData = new DataForConfirmInvoice
                {
                    No = number++,
                    InvoiceNo = drow["IVNO"].ToString(),
                    InvoiceDate = drow["IVDATE"].ToString(),
                    VenderCode = drow["VENDER"].ToString(),
                    VendorName = drow["VDNAME"].ToString(),
                    PaymentTerms = drow["PAYTRM"].ToString(),
                    Currency = drow["CURR"].ToString(),
                    AMTB = drow["AMTB"].ToString(),
                    Vat = drow["VATIN"].ToString(),
                    TaxID = drow["TAXID"].ToString()
                };

             
                string dueDateStr = DateTime.Now.ToString("yyyy-MM-dd"); // default
                if (!string.IsNullOrEmpty(drow["PAYTRM"].ToString()) && !string.IsNullOrEmpty(drow["IVDATE"].ToString()))
                {
                    if (DateTime.TryParseExact(drow["IVDATE"].ToString(), "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out DateTime ivDate) &&
                        int.TryParse(drow["PAYTRM"].ToString(), out int payTerm))
                    {
                        dueDateStr = ivDate.AddDays(payTerm).ToString("yyyy-MM-dd");
                    }
                }

          
                SqlCommand cmdCalendar = new SqlCommand(@"WITH RECURSIVE_DATE AS (
                                                                SELECT PDDATE, HOLIDAY
                                                                FROM dbSCM.dbo.AL_Calendar
                                                                WHERE PDDATE = @PDDATE
                                                                UNION ALL
                                                                SELECT C.PDDATE, C.HOLIDAY
                                                                FROM dbSCM.dbo.AL_Calendar C
                                                                INNER JOIN RECURSIVE_DATE R ON C.PDDATE = DATEADD(DAY, -1, R.PDDATE)
                                                                WHERE R.HOLIDAY = 1
                                                            )
                                                            SELECT TOP 1 PDDATE
                                                            FROM RECURSIVE_DATE
                                                            WHERE HOLIDAY = 0
                                                            ORDER BY PDDATE DESC");
                cmdCalendar.Parameters.Add(new SqlParameter("@PDDATE", dueDateStr));
                DataTable dtCalendar = oConSCM.Query(cmdCalendar);

                if (dtCalendar.Rows.Count > 0)
                {
                    MData.Duedate = Convert.ToDateTime(dtCalendar.Rows[0]["PDDATE"]).ToString("dd/MM/yyyy");
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
                                                  ,[VENDORCODE]
                                                  ,[INVOICENO]
                                                  ,[INVOICEDATE]
                                                  ,[TAXID]
                                                  ,[BILLERBY]
                                                  ,[BILLERDATE]
                                                  ,[CREATEBY]
                                                  ,[CREATEDATE]
                                                  ,[STATUS])
                                              VALUES (@DOCUMENTNO,@DOCUMENTDATE,@PAYMENT_TERMS,@DUEDATE
                                                    ,@VENDORCODE,@INVOICENO,@INVOICEDATE,@TAXID,@BILLERBY,GETDATE(),@CREATEBY,GETDATE(),@STATUS)";
            CreateBillingNoteHead.Parameters.Add(new SqlParameter("@DOCUMENTNO", obj.DOCUMENTNO));
            CreateBillingNoteHead.Parameters.Add(new SqlParameter("@DOCUMENTDATE", (object)obj.DOCUMENTDATE ?? DBNull.Value));
            CreateBillingNoteHead.Parameters.Add(new SqlParameter("@PAYMENT_TERMS", obj.PAYMENT_TERMS));
            CreateBillingNoteHead.Parameters.Add(new SqlParameter("@DUEDATE", (object)obj.DUEDATE ?? DBNull.Value));
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
                                                        @DUEDATE,@CURRENCY,@AMTB,@VAT,@TOTALVAT,@WHTAX,
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
        [Route("PostReportACAndVendorHeader")]
        public IActionResult PostReportACAndVendorHeader([FromBody] MParammeter obj)
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
                                                WHERE STATUS LIKE @STATUS  AND VENDORCODE LIKE  @VENDORCODE {conditionInvDate} 
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
                                            WHERE rn = 1";
            cmdHead.Parameters.Add(new SqlParameter("@VENDORCODE", strVendorCode));
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
                    MData.STATUS = drow["STATUS"].ToString();
                    MData.INVOICENO = drow["INVOICENO"].ToString();
                    MData.INVOICEDATE = drow["INVOICEDATE"].ToString();

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
                                            WHERE [STATUS] = @STATUS AND  VENDORCODE LIKE @VENDORCODE";
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
                                            WHERE [STATUS] = @STATUS AND  VENDORCODE LIKE @VENDORCODE";
            cmdHead.Parameters.Add(new SqlParameter("@VENDORCODE", obj.VenderCode));
            cmdHead.Parameters.Add(new SqlParameter("@STATUS", obj.status));
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
        [Route("PostReceivebilling")]
        public IActionResult PostReceivebilling([FromBody] MReceiveBuilling obj)
        {
           
            SqlCommand selectDoc = new SqlCommand();
            selectDoc.CommandText = $@"SELECT *
                                            FROM [dbSCM].[dbo].[EBILLING_DETAIL]
                                            WHERE INVOICENO IN {obj.InvoiceNo}";
            DataTable dtDoc = oConSCM.Query(selectDoc);
            if (dtDoc.Rows.Count > 0)
            {
                foreach (DataRow item in dtDoc.Rows)
                {
                    string docNo = item["DOCUMENTNO"].ToString();


                    SqlCommand updateStatusHead = new SqlCommand();
                    updateStatusHead.CommandText = $@"UPDATE [EBILLING_HEADER]
                                                SET RECEIVED_BILLERBY = @RECEIVEDBY , RECEIVED_BILLERDATE = GETDATE() , [STATUS] = 'CONFIRM'
                                                WHERE DOCUMENTNO = @DOCUMENTNO";
                    updateStatusHead.Parameters.Add(new SqlParameter("@DOCUMENTNO", docNo));
                    updateStatusHead.Parameters.Add(new SqlParameter("@RECEIVEDBY", obj.ReceiveBy));
                    oConSCM.Query(updateStatusHead);


                    SqlCommand updateStatusDeatil = new SqlCommand();
                    updateStatusDeatil.CommandText = $@"UPDATE [EBILLING_DETAIL]
                                                          SET [STATUS] = 'CONFIRM'
                                                          WHERE [INVOICENO] IN {obj.InvoiceNo}";
                    oConSCM.Query(updateStatusDeatil);
                }
            }

            return Ok();
        }



        [HttpPost]
        [Route("PostRejectbilling")]
        public IActionResult PostRejectbilling([FromBody] MReceiveBuilling obj)
        {

            SqlCommand selectDoc = new SqlCommand();
            selectDoc.CommandText = $@"SELECT *
                                            FROM [dbSCM].[dbo].[EBILLING_DETAIL]
                                            WHERE INVOICENO IN {obj.InvoiceNo}";
            DataTable dtDoc = oConSCM.Query(selectDoc);
            if (dtDoc.Rows.Count > 0)
            {
                foreach (DataRow item in dtDoc.Rows)
                {
                    string docNo = item["DOCUMENTNO"].ToString();


                    SqlCommand updateStatusHead = new SqlCommand();
                    updateStatusHead.CommandText = $@"UPDATE [EBILLING_HEADER]
                                                SET RECEIVED_BILLERBY = @RECEIVEDBY , RECEIVED_BILLERDATE = GETDATE() , [STATUS] = 'REJECT'
                                                WHERE DOCUMENTNO = @DOCUMENTNO";
                    updateStatusHead.Parameters.Add(new SqlParameter("@DOCUMENTNO", docNo));
                    updateStatusHead.Parameters.Add(new SqlParameter("@RECEIVEDBY", obj.ReceiveBy));
                    oConSCM.Query(updateStatusHead);


                    SqlCommand updateStatusDeatil = new SqlCommand();
                    updateStatusDeatil.CommandText = $@"UPDATE [EBILLING_DETAIL]
                                                          SET [STATUS] = 'REJECT'
                                                          WHERE [INVOICENO] IN {obj.InvoiceNo}";
                    oConSCM.Query(updateStatusDeatil);
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
        [Route("PostReportInvoiceByAC")]
        public IActionResult PostReportInvoiceByAC([FromBody] MParammeter obj)
        {
            List<ReportInvoiceByAC> Data_list = new List<ReportInvoiceByAC>();

            string conditionInvDate = "";
            if (!string.IsNullOrEmpty(obj.InvoiceDateFrom) && !string.IsNullOrEmpty(obj.InvoiceDateTo))
            {
                conditionInvDate = $" AND INVOICEDATE >= '{obj.InvoiceDateFrom}' AND INVOICEDATE <= '{obj.InvoiceDateTo}' ";
            }


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
                                        WHERE VENDORCODE LIKE @VENDORCODE AND STATUS LIKE @STATUS  AND INVOICENO LIKE @INVOICENO {conditionInvDate}";
            cmdHead.Parameters.Add(new SqlParameter("@VENDORCODE", obj.VenderCode));
            cmdHead.Parameters.Add(new SqlParameter("@INVOICENO", obj.InvoiceNo));
            cmdHead.Parameters.Add(new SqlParameter("@STATUS", obj.status));
            DataTable dtHead = oConSCM.Query(cmdHead);
            if (dtHead.Rows.Count > 0)
            {
                foreach (DataRow drow in dtHead.Rows)
                {
                    ReportInvoiceByAC MData = new ReportInvoiceByAC();
                    MData.INVOICENO = drow["INVOICENO"].ToString();
                    MData.INVOICEDATE = drow["INVOICEDATE"].ToString();
                    MData.PAYMENT_TERMS = drow["PAYMENT_TERMS"].ToString();
                    MData.DUEDATE = drow["DUEDATE"].ToString();
                    MData.CURRENCY = drow["CURRENCY"].ToString();
                    MData.AMTB = Convert.ToDecimal(drow["AMTB"].ToString());
                    MData.TOTALVAT = Convert.ToDecimal(drow["TOTALVAT"].ToString());
                    MData.WHTAX = drow["WHTAX"].ToString();
                    MData.TOTAL_WHTAX = Convert.ToDecimal(drow["TOTAL_WHTAX"].ToString());
                    MData.TOTAL_AMOUNT = Convert.ToDecimal(drow["TOTAL_AMOUNT"].ToString());
                    MData.STATUS = drow["STATUS"].ToString();


                    string vendorCode = drow["VENDORCODE"].ToString();


                    OracleCommand cmdVDNAME = new OracleCommand();
                    cmdVDNAME.CommandText = @"SELECT VDNAME, 
                                                    ADDR1 || ' ' || ADDR2 || ' ' || ZIPCODE || ' Tel ' || TELNO || ' Fax ' || FAXNO AS ADDR
                                                    FROM DST_ACMVD1
                                                    WHERE KAISEQ = '999' AND TRIM(VENDER) = :VENDER";
                    cmdVDNAME.Parameters.Add(new OracleParameter(":VENDER", vendorCode));
                    DataTable dtVDNAME = oOraAL02.Query(cmdVDNAME);
                    if (dtVDNAME.Rows.Count > 0)
                    {
                        MData.VENDORNAME = dtVDNAME.Rows[0]["VDNAME"].ToString();
                    }




                    Data_list.Add(MData);
                }
            }

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
