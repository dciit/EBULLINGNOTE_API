namespace INVOICE_VENDER_API.Models
{
    public class DtTable
    {
    }

    public class AuthenRegis
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string Fname { get; set; }
        public string Lname { get; set; }
    }

    public class DataVender
    {
        public string VENDER { get; set; }
        public string VDNAME { get; set; }
        public string TLXNO { get; set; }
        public string VDABBR { get; set; }
        public string ADDR1 { get; set; }
        public string ADDR2 { get; set; }
    }


    public class DataForConfirmInvoice
    {
        public int No { get; set; }
        public string InvoiceNo { get; set; }
        public string InvoiceDate { get; set; }
        public string VenderCode { get; set; }
        public string VendorName { get; set; }
        public string PaymentTerms { get; set; }
        public string Duedate { get; set; }
        public string Currency { get; set; }
        public string AMTB { get; set; }
        public string Vat { get; set; }
        public string Rate { get; set; }
        public string WHTax { get; set; }
        public string TaxID { get; set; }
        public string GrandTotal { get; set; }
    }


    public class CreateBillingNote
    {

        public string? DOCUMENTNO { get; set; }
        public DateTime? DOCUMENTDATE { get; set; }
        public string? PAYMENT_TERMS { get; set; }
        public DateTime? DUEDATE { get; set; }
        public string? VENDORCODE { get; set; }
        public string? BILLERBY { get; set; }
        public string? RECEIVED_BILLERBY { get; set; }
        public string? CREATEBY { get; set; }
        public string? STATUS { get; set; }
        public string? INVOICENO { get; set; }
        public DateTime? INVOICEDATE { get; set; }
        public string? TAXID { get; set; }
        public string? CURRENCY { get; set; }
        public decimal AMTB { get; set; }
        public decimal VAT { get; set; }
        public decimal TOTALVAT { get; set; }
        public string? RATE { get; set; }
        public decimal WHTAX { get; set; }
        public decimal NETPAID { get; set; }
        public decimal BEFORVATAMOUNT { get; set; }
        public decimal TOTAL_AMOUNT { get; set; }

    }



    public class ReportHeader
    {

        public string? DOCUMENTNO { get; set; }
        public string? DATE { get; set; }
        public string? DUEDATE { get; set; }
        public string? TAXID { get; set; }
        public string? VENDORCODE { get; set; }
        public string? VENDORNAME { get; set; }
        public string? PAYMENT_TERMS { get; set; }
        public decimal TOTALVAT { get; set; }    
        public decimal TOTAL_AMOUNT { get; set; }
        public decimal WHTAX { get; set; }
        public decimal NETPAID { get; set; }
        public decimal PAYBIT { get; set; }
        public string? BILLERBY { get; set; }
        public string? BILLERDATE { get; set; }
        public string? RECEIVED_BILLERBY { get; set; }
        public string? RECEIVED_BILLERDATE { get; set; }
        public string? STATUS { get; set; }

        public string? ADDRES1 { get; set; }
        public string? ADDRES2 { get; set; }
        public string? ZIPCODE { get; set; }
        public string? TELNO { get; set; }
        public string? FAXNO { get; set; }
    }


    public class ReportDetail
    {

        public string? DOCUMENTNO { get; set; }
        public string? INVOICENO { get; set; }
        public string? INVOICEDATE { get; set; }
        public string? TAXID { get; set; }
        public string? DUEDATE { get; set; }
        public decimal AMTB { get; set; }
        public decimal VAT { get; set; }
        public decimal TOTALVAT { get; set; }
        public string? RATE { get; set; }
        public decimal WHTAX { get; set; }
        public decimal TOTALAMOUNT { get; set; }
    }


    public class MReceiveBuilling
    {
        public string? InvoiceNo { get; set; }
        public string? ReceiveBy { get; set; }
    }


    public class MPayment
    {
        public string? VendorCode { get; set; }
        public string? InvoiceNo { get; set; }
        public string? PayBy { get; set; }
    }



    public class ReportInvoiceByAC
    {

        public string? INVOICENO { get; set; }
        public string? INVOICEDATE { get; set; }
        public string? VENDORNAME { get; set; }
        public string? PAYMENT_TERMS { get; set; }
        public string? DUEDATE { get; set; }
        public string? CURRENCY { get; set; }
        public decimal AMTB { get; set; }
        public decimal TOTALVAT { get; set; }
        public string? WHTAX { get; set; }
        public decimal TOTAL_WHTAX { get; set; }
        public decimal TOTAL_AMOUNT { get; set; }
        public string? STATUS { get; set; }
    }

    public class BankAccount
    {
        public string ACCOUNTCODE { get; set; }
        public string ACCOUNT { get; set; }
        public string ACCOUNTNAME { get; set; }

    }

    public class Calendar
    {
        public string CLDYEAR { get; set; }
        public string CLDMONTH { get; set; }
        public string BILLING_START { get; set; }
        public string BILLING_END { get; set; }
        public string PAYMENT_START { get; set; }
        public string PAYMENT_END { get; set; }
    }
}