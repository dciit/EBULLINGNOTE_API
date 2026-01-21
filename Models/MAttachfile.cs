namespace INVOICEBILLINENOTE_API.Models
{
    public class MAttachfile
    {
        public string DOCUMENTNO { get; set; }
    }


    public class MShowAttachfile
    {
        public string DOCUMENTNO { get; set; }
        public string FILE_NAME { get; set; }
        public string FILE_PATH { get; set; }
        public string CREATEDATE { get; set; }
    }
}
