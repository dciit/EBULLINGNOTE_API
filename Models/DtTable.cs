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
        public string TAXID { get; set; }
        public string ZIPCODE { get; set; }
        public string TELNO { get; set; }
        public string FAXNO { get; set; }
    }

    public class DataInvoiceCheck
    {
        public string DICTTYPE { get; set; }
        public string DICTKEYNO { get; set; }
        public string DICTREFNO { get; set; }
        public string DICTTITLE { get; set; }
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
        public string InvoiceStatus { get; set; }
        public string DocumentNo { get; set; }
        public string ACTYPE { get; set; }

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
        public string? ACTYPE { get; set; }
        public string? IS_INVOICECORRECT { get; set; }
        public string? INVOICE_VERIFICATION_REMARK { get; set; }
    }



    public class ReportHeader
    {

        public string? DOCUMENTNO { get; set; }
        public string? DATE { get; set; }
        public string? DUEDATE { get; set; }
        public string? TAXID { get; set; }
        public string? VENDORCODE { get; set; }
        public string? VENDORNAME { get; set; }
        public string? INVOICENO { get; set; }
        public string? INVOICEDATE { get; set; }
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
        public string? PAYMENT_BY { get; set; }
        public string? PAYMENT_DATE { get; set; }
        public string? STATUS { get; set; }
        public string? ADDRES1 { get; set; }
        public string? ADDRES2 { get; set; }
        public string? ZIPCODE { get; set; }
        public string? TELNO { get; set; }
        public string? FAXNO { get; set; }
        public string? ACTYPE { get; set; }
        public string? FILE_NAME { get; set; }
        public string? REJECT_BY { get; set; }
        public string? REJECT_DATE { get; set; }

    }


    public class ReportDetail
    {

        public string? DOCUMENTNO { get; set; }
        public string? DOCUMENTDATE { get; set; }
        public string? INVOICENO { get; set; }
        public string? INVOICEDATE { get; set; }
        public string? TAXID { get; set; }
        public string? DUEDATE { get; set; }
        public string? VENDORCODE { get; set; }
        public string? VENDORNAME { get; set; }
        public string? PAYMENT_TERMS { get; set; }
        public string? CURRENCY { get; set; }
        public string? STATUS { get; set; }
        public string? REMARK { get; set; }
        public decimal AMTB { get; set; }
        public decimal VAT { get; set; }
        public decimal TOTALVAT { get; set; }
        public string? RATE { get; set; }
        public decimal WHTAX { get; set; }
        public decimal TOTALAMOUNT { get; set; }
        public string ADDR1 { get; set; }
        public string ADDR2 { get; set; }
        public string ZIPCODE { get; set; }
        public string TELNO { get; set; }
        public string FAXNO { get; set; }
        public string CREATEBY { get; set; }
        public string CREATEDATE { get; set; }
        public string BILLERBY { get; set; }
        public string BILLERDATE { get; set; }
        public string RECEIVED_BILLERBY { get; set; }
        public string RECEIVED_BILLERDATE { get; set; }

    }


    public class MReceiveBuilling
    {
        public string? DocumentNo { get; set; }
        public List<string> InvoiceNos { get; set; }
        public string? IssuedBy { get; set; }
        public List<string> Remarks { get; set; }
    }


    public class MPayment
    {
        public List<string> VendorCode { get; set; }
        public string? IssuedBy { get; set; }
        public List<string> status { get; set; }
        public string? InvoiceDateFrom { get; set; }
        public string? InvoiceDateTo { get; set; }
    }

    public class MCancelPayment
    {
        public string? VendorCode { get; set; }
        public string? IssuedBy { get; set; }
        public string? status { get; set; }
        public string? InvoiceDateFrom { get; set; }
        public string? InvoiceDateTo { get; set; }
    }





    public class ReportInvoiceByAC
    {

        public string? INVOICENO { get; set; }
        public string? INVOICEDATE { get; set; }
        public string? VENDORCODE { get; set; }
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
        public string? ACTYPE { get; set; }
    }

    public class BankAccount
    {
        public string ACCOUNTCODE { get; set; }
        public string ACCOUNT { get; set; }
        public string ACCOUNTNAME { get; set; }

    }

    public class Calendar
    {
        public string CLDCODE { get; set; }
        public string CLDYEAR { get; set; }
        public string CLDMONTH { get; set; }
        public string EVENTTYPE { get; set; }
        public string STARTDATE { get; set; }
        public string ENDDATE { get; set; }
        
    }

    public class Dictionary
    {
        public string DICTTYPE { get; set; }
        public string DICTKEYNO { get; set; }
        public string DICTREFNO { get; set; }
        public string DICTTITLE { get; set; }
        public string DICTVALUE { get; set; }
    }

    public class Vender
    {
        public string VENDER { get; set; }
        public string VDNAME { get; set; }
        public string ADDR1 { get; set; }
        public string ADDR2 { get; set; }
        public string TELNO { get; set; }
        public string FAXNO { get; set; }
        public string TLXNO { get; set; }
        public string AIEMAIL { get; set; }
    }

    public class AuthenInfo
    {
        public string USERNAME { get; set; }
        public string USERTYPE { get; set; }
        public string COMPANY_NAME { get; set; }
        public string EMAIL { get; set; }
        public string TELEPHONE { get; set; }
        public string TAXID { get; set; }
        public string FAX { get; set; }
        public string COMPANTBRANCH { get; set; }
        public string ADDRESS { get; set; }
        public string ACCOUNT_NAME { get; set; }
        public string ACCOUNT_NUMER { get; set; }
        public string BANK_NAME { get; set; }
        public string BANKBRANCH_NAME { get; set; }
        public string BANKBRANCH_NO { get; set; }
    }

    public class VendorinfoLog
    {
        public string ID { get; set; }
        public string USERNAME { get; set; }
        public string NAME { get; set; }
        public string COMPANYNAME { get; set; }
        public string EMAIL { get; set; }
        public string TAXID { get; set; }
        public string BRANCHNO { get; set; }
        public string FAX { get; set; }
        public string TELEPHONE { get; set; }
        public string ADDRESS { get; set; }
        public string ACCNAME { get; set; }
        public string ACCNO { get; set; }
        public string BANKNAME { get; set; }
        public string BANKBRANCHNAME { get; set; }
        public string BANKBRANCHNO { get; set; }
     }
}