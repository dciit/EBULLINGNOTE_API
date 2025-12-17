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
    }

    public class RegisRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string Usertype { get; set; }
        public string Incharge { get; set; }
        public string? Email { get; set; }
        public string? Tel { get; set; }
        public string? Textid { get; set; }
        public string? Fax { get; set; }
        public string? Address { get; set; }
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
    }


    public class EmpName
    {
        public string Username { get; set; }
    }



}
