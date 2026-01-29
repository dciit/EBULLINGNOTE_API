using INVOICE_VENDER_API.Models;
using System.Data;
using INVOICEBILLINENOTE_API.Connection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using Microsoft.Data.SqlClient;

namespace INVOICEBILLINENOTE_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private OraConnectDB oOraAL02 = new OraConnectDB("ALPHA02");
        RunNumberService runNumberService = new RunNumberService();
        private SqlConnectDB oConSCM = new SqlConnectDB("dbSCM");


        [HttpPost]
        [Route("SummaryAllinvoice")]
        public IActionResult SummaryAllinvoice([FromBody] MParammeter obj)
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
                    MData.DUEDATE = "-";
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
        [Route("ReportConfirmHeader")]
        public IActionResult ReportConfirmHeader([FromBody] MParammeter obj)
        {
            List<ReportHeader> Data_list = new List<ReportHeader>();


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
                                    ,[PAYMENT_TERMS]
                                    ,FORMAT([DUEDATE],'dd/MM/yyyy') AS DUEDATE
                                    ,[ACTYPE]
                                    ,[VENDORCODE]
                                    ,[TAXID]
                                    ,[STATUS]
                                    FROM [dbSCM].[dbo].[EBILLING_HEADER]
                                    WHERE VENDORCODE LIKE @VENDORCODE AND ACTYPE LIKE @ACTYPE AND [STATUS] LIKE @STATUS {conditionInvDate}";
            cmdHead.Parameters.Add(new SqlParameter("@VENDORCODE", obj.VenderCode));
            cmdHead.Parameters.Add(new SqlParameter("@ACTYPE", obj.ACTYPE));
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
                    MData.VENDORCODE = drow["VENDORCODE"].ToString();
                    MData.PAYMENT_TERMS = drow["PAYMENT_TERMS"].ToString();
                    MData.STATUS = drow["STATUS"].ToString();
                    MData.ACTYPE = drow["ACTYPE"].ToString();

                    string vendorCode = MData.VENDORCODE;
                    string status = MData.STATUS;
                    string document = MData.DOCUMENTNO;


                    OracleCommand cmdVDNAME = new OracleCommand();
                    cmdVDNAME.CommandText = @"SELECT *
                                                FROM DST_ACMVD1
                                                WHERE KAISEQ = '999' AND TRIM(VENDER) = :VENDER";
                    cmdVDNAME.Parameters.Add(new OracleParameter(":VENDER", vendorCode));
                    DataTable dtVDNAME = oOraAL02.Query(cmdVDNAME);
                    if (dtVDNAME.Rows.Count > 0)
                    {
                        MData.VENDORNAME = dtVDNAME.Rows[0]["VDNAME"].ToString();
                    }


                    SqlCommand cmdTotal = new SqlCommand();
                    cmdTotal.CommandText = @"SELECT VENDORCODE,
                                                SUM(TOTALVAT) AS TOTALVAT,
                                                SUM(TOTAL_AMOUNT) AS TOTAL_AMOUNT,
                                                SUM(TOTAL_WHTAX) AS TOTAL_WHTAX,
                                                SUM(TOTAL_AMOUNT) - SUM(TOTAL_WHTAX) AS NETPAID
                                                FROM dbSCM.dbo.EBILLING_DETAIL
                                                WHERE [STATUS] LIKE @STATUS AND [DOCUMENTNO] = @DOCUMENTNO 
                                                GROUP BY VENDORCODE";
                    cmdTotal.Parameters.Add(new SqlParameter("@DOCUMENTNO", document));
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
        [Route("ReportConfirmDetail")]
        public IActionResult ReportConfirmDetail([FromBody] MParammeter obj)
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
                    MData.VENDORCODE = drow["VENDORCODE"].ToString();
                    MData.PAYMENT_TERMS = drow["PAYMENT_TERMS"].ToString();
                    MData.TAXID = drow["TAXID"].ToString();
                    MData.DUEDATE = drow["DUEDATE"].ToString();
                    MData.AMTB = Convert.ToDecimal(drow["AMTB"].ToString());
                    MData.VAT = Convert.ToDecimal(drow["VAT"].ToString());
                    MData.TOTALVAT = Convert.ToDecimal(drow["TOTALVAT"].ToString());
                    MData.RATE = drow["WHTAX"].ToString();
                    MData.WHTAX = Convert.ToDecimal(drow["TOTAL_WHTAX"].ToString());
                    MData.TOTALAMOUNT = Convert.ToDecimal(drow["TOTAL_AMOUNT"].ToString());
                    MData.CURRENCY = drow["CURRENCY"].ToString();
                    MData.STATUS = drow["STATUS"].ToString();

                    OracleCommand cmdVDNAME = new OracleCommand();
                    cmdVDNAME.CommandText = @"SELECT *
                                                FROM DST_ACMVD1
                                                WHERE KAISEQ = '999' AND TRIM(VENDER) = :VENDER";
                    cmdVDNAME.Parameters.Add(new OracleParameter(":VENDER", MData.VENDORCODE));
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
        [Route("ReportConfirmPrint")]
        public IActionResult ReportConfirmPrint([FromBody] MParammeter obj)
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
                    MData.INVOICEDATE = drow["INVOICEDATE"].ToString();
                    MData.VENDORCODE = drow["VENDORCODE"].ToString();
                    MData.PAYMENT_TERMS = drow["PAYMENT_TERMS"].ToString();
                    MData.TAXID = drow["TAXID"].ToString();
                    MData.DUEDATE = drow["DUEDATE"].ToString();
                    MData.AMTB = Convert.ToDecimal(drow["AMTB"].ToString());
                    MData.VAT = Convert.ToDecimal(drow["VAT"].ToString());
                    MData.TOTALVAT = Convert.ToDecimal(drow["TOTALVAT"].ToString());
                    MData.RATE = drow["WHTAX"].ToString();
                    MData.WHTAX = Convert.ToDecimal(drow["TOTAL_WHTAX"].ToString());
                    MData.TOTALAMOUNT = Convert.ToDecimal(drow["TOTAL_AMOUNT"].ToString());
                    MData.CURRENCY = drow["CURRENCY"].ToString();
                    MData.STATUS = drow["STATUS"].ToString();
                    MData.CURRENCY = drow["CURRENCY"].ToString();
                    MData.STATUS = drow["STATUS"].ToString();


                    SqlCommand cmdHead = new SqlCommand();
                    cmdHead.CommandText = $@"SELECT [DOCUMENTNO]
                                                    ,[BILLERBY]
                                                    ,FORMAT([BILLERDATE],'dd/MM/yyyy') AS BILLERDATE
                                                    ,[RECEIVED_BILLERBY]
                                                    ,FORMAT([RECEIVED_BILLERDATE],'dd/MM/yyyy') AS RECEIVED_BILLERDATE
                                                    ,[PAYBY]
                                                    ,FORMAT([PAYDATE],'dd/MM/yyyy') AS PAYDATE
                                                    ,[CREATEBY]
                                                    ,FORMAT([CREATEDATE],'dd/MM/yyyy') AS CREATEDATE
                                                    FROM [dbSCM].[dbo].[EBILLING_HEADER]
                                                    WHERE DOCUMENTNO = @DOCUMENTNO";
                    cmdHead.Parameters.Add(new SqlParameter("@DOCUMENTNO", obj.DocumentNo));
                    DataTable dtHead = oConSCM.Query(cmdHead);
                    if (dtHead.Rows.Count > 0)
                    {
                        foreach (DataRow iHead in dtHead.Rows)
                        {
                            MData.BILLERBY = iHead["BILLERBY"].ToString();
                            MData.BILLERDATE = iHead["BILLERDATE"].ToString();
                            MData.RECEIVED_BILLERBY = iHead["RECEIVED_BILLERBY"].ToString();
                            MData.RECEIVED_BILLERDATE = iHead["RECEIVED_BILLERDATE"].ToString();
                        }
                    }

                    OracleCommand cmdVDNAME = new OracleCommand();
                    cmdVDNAME.CommandText = @"SELECT *
                                                FROM DST_ACMVD1
                                                WHERE KAISEQ = '999' AND TRIM(VENDER) = :VENDER";
                    cmdVDNAME.Parameters.Add(new OracleParameter(":VENDER", MData.VENDORCODE));
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
        [Route("ConfirmBilling")]
        public IActionResult ConfirmBilling([FromBody] MReceiveBuilling obj)
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
                                WHERE INVOICENO IN ({inClause}) AND STATUS = 'WAITING_DCI'";
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
        [Route("Rejectbilling")]
        public IActionResult Rejectbilling([FromBody] MReceiveBuilling obj)
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
                                WHERE INVOICENO IN ({inClause}) AND STATUS IN ('WAITING_DCI','CONFIRM','CANCEL')";
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
        [Route("CancelBilling")]
        public IActionResult CancelBilling([FromBody] MReceiveBuilling obj)
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
                                         [STATUS] = 'CANCEL_CONFIRM'
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
                                         SET [STATUS] = 'CANCEL_CONFIRM',
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
        [Route("HeaderPayment")]
        public IActionResult HeaderPayment([FromBody] MParammeter obj)
        {
            List<ReportHeader> Data_list = new List<ReportHeader>();
            string strVendor = "";
            string conditionInvDate = "";


            if (!string.IsNullOrEmpty(obj.InvoiceDateFrom) && !string.IsNullOrEmpty(obj.InvoiceDateTo))
            {
                conditionInvDate = $" AND INVOICEDATE >= '{obj.InvoiceDateFrom}' AND INVOICEDATE <= '{obj.InvoiceDateTo}' ";
            }



            SqlCommand selectDoc = new SqlCommand();
            selectDoc.CommandText = $@"SELECT VENDORCODE,
                                                STRING_AGG(DOCUMENTNO, ', ') AS DOCUMENT_LIST
                                            FROM dbSCM.dbo.EBILLING_HEADER
                                            WHERE [STATUS] IN ('CONFIRM','CANCEL_PAYMENT','PAYMENT') 
                                            AND [STATUS] LIKE @STATUS
                                            AND ACTYPE LIKE @ACTYPE  
                                            AND VENDORCODE LIKE @VENDORCODE
                                            {conditionInvDate}
                                            GROUP BY VENDORCODE
                                            ORDER BY VENDORCODE";
            selectDoc.Parameters.Add(new SqlParameter("@STATUS", obj.status));
            selectDoc.Parameters.Add(new SqlParameter("@ACTYPE", obj.ACTYPE));
            selectDoc.Parameters.Add(new SqlParameter("@VENDORCODE", obj.VenderCode));
            DataTable dtDoc = oConSCM.Query(selectDoc);
            if (dtDoc.Rows.Count > 0)
            {
                foreach (DataRow item in dtDoc.Rows)
                {
                    ReportHeader MData = new ReportHeader();
                    string vender = item["VENDORCODE"].ToString();
                    string documentNoList = item["DOCUMENT_LIST"].ToString();
                    string firstDoc = documentNoList.Split(',')[0].Trim();
                    var docs = documentNoList.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim());
                    string sqlIn = "('" + string.Join("','", docs) + "')";

                    //**** HEADER ****//
                    SqlCommand selectHead = new SqlCommand();
                    selectHead.CommandText = @"SELECT [VENDORCODE],DOCUMENTNO
                                                            ,FORMAT([DUEDATE],'dd/MM/yyyy') AS DUEDATE
                                                            ,[TAXID]
                                                            ,[PAYMENT_TERMS]
                                                            ,[STATUS]
                                                            FROM [dbSCM].[dbo].[EBILLING_HEADER]
                                                            WHERE DOCUMENTNO = @DOCUMENTNO";
                    selectHead.Parameters.Add(new SqlParameter("@DOCUMENTNO", firstDoc));
                    DataTable dtHead = oConSCM.Query(selectHead);
                    if (dtHead.Rows.Count > 0)
                    {
                        foreach (DataRow iHead in dtHead.Rows)
                        {
                            MData.DOCUMENTNO = iHead["DOCUMENTNO"].ToString();
                            MData.DUEDATE = iHead["DUEDATE"].ToString();
                            MData.TAXID = iHead["TAXID"].ToString();
                            MData.PAYMENT_TERMS = iHead["PAYMENT_TERMS"].ToString();
                            MData.STATUS = iHead["STATUS"].ToString();
                            MData.VENDORCODE = iHead["VENDORCODE"].ToString();
                        }
                    }


                    //****VENDER NAME ***//
                    OracleCommand cmdVDNAME = new OracleCommand();
                    cmdVDNAME.CommandText = @"SELECT *
                                                FROM DST_ACMVD1
                                                WHERE KAISEQ = '999' AND TRIM(VENDER) = :VENDER";
                    cmdVDNAME.Parameters.Add(new OracleParameter(":VENDER", vender));
                    DataTable dtVDNAME = oOraAL02.Query(cmdVDNAME);
                    if (dtVDNAME.Rows.Count > 0)
                    {
                        MData.VENDORNAME = dtVDNAME.Rows[0]["VDNAME"].ToString();
                    }


                    //**** DETAIL ****//
                    SqlCommand selectTotal = new SqlCommand();
                    selectTotal.CommandText = $@"SELECT VENDORCODE,
                                                            SUM(TOTAL_WHTAX)   AS TOTAL_WHTAX,
                                                            SUM(NETPAID)       AS NETPAID,
                                                            SUM(TOTAL_AMOUNT) AS TOTAL_AMOUNT
                                                        FROM dbSCM.dbo.EBILLING_DETAIL
                                                        WHERE DOCUMENTNO IN {sqlIn}
                                                        GROUP BY VENDORCODE";
                    selectTotal.Parameters.Add(new SqlParameter("@DOCUMENTNO", sqlIn));
                    DataTable dtTOTAL = oConSCM.Query(selectTotal);
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
        [Route("DetailPayment")]
        public IActionResult DetailPayment([FromBody] MParammeter obj)
        {
            List<ReportDetail> Data_list = new List<ReportDetail>();



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
                                            WHERE VENDORCODE = @VENDORCODE AND [STATUS] = @STATUS {conditionInvDate}";
            cmdHead.Parameters.Add(new SqlParameter("@VENDORCODE", obj.VenderCode));
            cmdHead.Parameters.Add(new SqlParameter("@STATUS", obj.status));
            DataTable dtHead = oConSCM.Query(cmdHead);
            if (dtHead.Rows.Count > 0)
            {
                foreach (DataRow drow in dtHead.Rows)
                {
                    ReportDetail MData = new ReportDetail();
                    string venderCode = drow["VENDORCODE"].ToString();
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


                    OracleCommand cmdVDNAME = new OracleCommand();
                    cmdVDNAME.CommandText = @"SELECT *
                                                FROM DST_ACMVD1
                                                WHERE KAISEQ = '999' AND TRIM(VENDER) = :VENDER";
                    cmdVDNAME.Parameters.Add(new OracleParameter(":VENDER", venderCode));
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
        [Route("PaymentBilling")]
        public IActionResult PaymentBilling([FromBody] MPayment obj)
        {

            if (obj.VendorCode == null || obj.VendorCode.Count == 0)
                return BadRequest("VendorCode list is empty");

            if (obj.status == null || obj.status.Count != obj.VendorCode.Count)
                return BadRequest("Status list must match VendorCode list");


            string conditionInvDate = "";
            if (!string.IsNullOrEmpty(obj.InvoiceDateFrom) && !string.IsNullOrEmpty(obj.InvoiceDateTo))
            {
                conditionInvDate = $" AND INVOICEDATE >= '{obj.InvoiceDateFrom}' AND INVOICEDATE <= '{obj.InvoiceDateTo}' ";
            }


            List<string> skippedVendors = new List<string>();

            // Loop vendor + status ทีละตัว
            for (int i = 0; i < obj.VendorCode.Count; i++)
            {
                var vendor = obj.VendorCode[i];
                var status = obj.status[i];

                try
                {
                    // ===== UPDATE HEADER =====
                    SqlCommand updateHeader = new SqlCommand();
                    updateHeader.CommandText = $@"UPDATE [dbSCM].[dbo].[EBILLING_HEADER]
                                                SET [STATUS] = 'PAYMENT', [PAYBY] = @PAYBY, [PAYDATE] = GETDATE()
                                                WHERE VENDORCODE = @VENDORCODE AND [STATUS] = @OLDSTATUS {conditionInvDate}";
                    updateHeader.Parameters.AddWithValue("@VENDORCODE", vendor);
                    updateHeader.Parameters.AddWithValue("@OLDSTATUS", status);
                    updateHeader.Parameters.AddWithValue("@PAYBY", obj.IssuedBy);

                    oConSCM.Query(updateHeader);

                    // ===== UPDATE DETAIL =====
                    SqlCommand updateDetail = new SqlCommand();
                    updateDetail.CommandText = $@"UPDATE [dbSCM].[dbo].[EBILLING_DETAIL]
                                                SET [STATUS] = 'PAYMENT'
                                                WHERE VENDORCODE = @VENDORCODE AND [STATUS] = @OLDSTATUS {conditionInvDate}";
                    updateDetail.Parameters.AddWithValue("@VENDORCODE", vendor);
                    updateDetail.Parameters.AddWithValue("@OLDSTATUS", status);

                    oConSCM.Query(updateDetail); // execute void
                }
                catch (Exception ex)
                {
                    // ถ้ามี error บันทึก vendor skip
                    skippedVendors.Add(vendor);
                    Console.WriteLine($"Error updating vendor {vendor}: {ex.Message}");
                }
            }


            if (skippedVendors.Count > 0)
            {
                return Ok(new
                {
                    message = "Update completed, but some vendors were skipped due to errors.",
                    skippedVendors
                });
            }

            return Ok(new { message = "Update completed successfully" });
        }



        [HttpPost]
        [Route("CancelPayment")]
        public IActionResult CancelPayment([FromBody] MCancelPayment obj)
        {

            string conditionInvDate = "";
            if (!string.IsNullOrEmpty(obj.InvoiceDateFrom) && !string.IsNullOrEmpty(obj.InvoiceDateTo))
            {
                conditionInvDate = $" AND INVOICEDATE >= '{obj.InvoiceDateFrom}' AND INVOICEDATE <= '{obj.InvoiceDateTo}' ";
            }



            // ===== CANCEL HEADER =====
            SqlCommand updateHeader = new SqlCommand();
            updateHeader.CommandText = $@"UPDATE [dbSCM].[dbo].[EBILLING_HEADER]
                                                SET [STATUS] = 'CANCEL_PAYMENT', [CANCEL_PAYMENT_BY] = @CANCELBY, [CANCEL_PAYMENT_DATE] = GETDATE()
                                                WHERE VENDORCODE = @VENDORCODE AND [STATUS] = @OLDSTATUS {conditionInvDate}";
            updateHeader.Parameters.AddWithValue("@VENDORCODE", obj.VendorCode);
            updateHeader.Parameters.AddWithValue("@OLDSTATUS", obj.status);
            updateHeader.Parameters.AddWithValue("@CANCELBY", obj.IssuedBy);

            oConSCM.Query(updateHeader);


            // ===== CANCEL DETAIL =====
            SqlCommand updateDetail = new SqlCommand();
            updateDetail.CommandText = $@"UPDATE [dbSCM].[dbo].[EBILLING_DETAIL]
                                                SET [STATUS] = 'CANCEL_PAYMENT'
                                                WHERE VENDORCODE = @VENDORCODE AND [STATUS] = @OLDSTATUS {conditionInvDate}";
            updateDetail.Parameters.AddWithValue("@VENDORCODE", obj.VendorCode);
            updateDetail.Parameters.AddWithValue("@OLDSTATUS", obj.status);

            oConSCM.Query(updateDetail); // execute void




            return Ok(new { message = "Cancel completed successfully" });
        }


    }
}
