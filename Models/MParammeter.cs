namespace INVOICE_VENDER_API.Models
{
    public class MParammeter
    {
        public string? VenderCode { get; set; }
        public string? InvoiceNo { get; set; }
        public string? InvoiceDateFrom { get; set; }
        public string? InvoiceDateTo { get; set; }
        public string? DocumentNo { get; set; }
        public string? status { get; set; }
        public string? Role { get; set; }
        public string? ACTYPE { get; set; }
    }

    public class RegisRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string Usertype { get; set; }
        public string Role { get; set; }
    }

    public class LoginRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class EditPassExpire
    {
        public string Username { get; set; }
        public string OldPassword { get; set; }
        public string NewPassword { get; set; }
        public string ConfirmPassword { get; set; }
    }


    public class EmpName
    {
        public string Username { get; set; }
    }

    public class RejectVdInfo
    {
        public string Username { get; set; }
        public string Remark { get; set; }
    }

    //public class Createcalendar
    //{
    //    public string CrBy { get; set; }
    //    public string CrDate { get; set; }
    //    public string StartDate { get; set; }
    //    public string EndDate { get; set; }

    //}

    public class CreateCalendar
    {
        public string CldYear { get; set; }
        public string CldMonth { get; set; }
        public string DateStart { get; set; }
        public string DateEnd { get; set; }
        public string CldType { get; set; }
        public string CrBy { get; set; }
    }

    public class CreateVenderInfo
    {
        public string Username { get; set; }
        public string Name { get; set; }
        public string Compname { get; set; }
        public string Email { get; set; }
        public string TaxID { get; set; }
        public string Branchno { get; set; }
        public string Fax { get; set; }
        public string Telephone { get; set; }
        public string Address { get; set; }
        public string Accountname { get; set; }
        public string Accountno { get; set; }
        public string BName { get; set; }
        public string BBranchname { get; set; }
        public string BBranchno { get; set; }
    }
}
