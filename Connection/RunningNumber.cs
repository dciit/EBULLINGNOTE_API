using System.Globalization;
using System.Text;

namespace INVOICE_VENDER_API.Services.Create
{
    public class RunningNumber
    {
        private int m_FormatId;
        private string m_Key;
        private string m_Prefix;
        private int m_YearNbrPrefix = 0;
        private int m_MonthNbrPrefix = 0;
        private int m_DayNbrPrefix = 0;
        private int m_LengthNbr = 0;
        private int m_Id;
        private string m_Remark;
        private string m_ResetOption;
        private DateTime m_ActiveDate = new DateTime();
        private DateTime m_Date = DateTime.Now;

        public RunningNumber()
        {
        }

        public int FormatId
        {
            get { return m_FormatId; }
            set { m_FormatId = value; }
        }

        public string Key
        {
            get { return m_Key; }
            set { m_Key = value; }
        }

        public string Prefix
        {
            get { return m_Prefix; }
            set { m_Prefix = value; }
        }

        public int LenYearPrefix
        {
            get { return m_YearNbrPrefix; }
            set { m_YearNbrPrefix = value; }
        }

        public int LenMonthPrefix
        {
            get { return m_MonthNbrPrefix; }
            set { m_MonthNbrPrefix = value; }
        }

        public int LenDayPrefix
        {
            get { return m_DayNbrPrefix; }
            set { m_DayNbrPrefix = value; }
        }

        public int LenRunId
        {
            get { return m_LengthNbr; }
            set { m_LengthNbr = value; }
        }

        public DateTime ActiveDate
        {
            get { return m_ActiveDate; }
            set { m_ActiveDate = value; }
        }

        public DateTime Date
        {
            get { return m_Date; }
            set { m_Date = value; }
        }

        public int NextId
        {
            get
            {
                if (MustReset)
                {
                    m_Id = 0;
                }
                return m_Id + 1;
            }
            set { m_Id = value; }
        }

        public string Remark
        {
            get { return m_Remark; }
            set { m_Remark = value; }
        }

        public string ResetOption
        {
            get { return m_ResetOption; }
            set { m_ResetOption = value; }
        }

        public override string ToString()
        {
            return ToString(false);
        }

        public string ToString(bool isIncludePrefixCode)
        {
            StringBuilder id = new StringBuilder();

            int year = Convert.ToInt32(Format(Date, "yy"));
            int month = Convert.ToInt32(Format(Date, "MM"));
            int day = Convert.ToInt32(Format(Date, "dd"));

            if (isIncludePrefixCode) id.Append(Prefix);

            id.Append(AddLength(year.ToString(), LenYearPrefix));
            id.Append(AddLength(month.ToString(), LenMonthPrefix));
            id.Append(AddLength(day.ToString(), LenDayPrefix));
            id.Append(AddLength(NextId.ToString(), LenRunId));

            return id.ToString();
        }

        private string Format(DateTime dt, string format)
        {
            return dt.ToString(format, new CultureInfo("en-US"));
        }

        private string AddLength(string text, int len)
        {
            if (len > 0)
            {
                if (text.Length < len)
                {
                    while (text.Length < len)
                    {
                        text = "0" + text;
                    }
                }
                else
                {
                    text = text.Substring(0, len);
                }
            }
            else
            {
                text = "";
            }
            return text;
        }

        public bool MustReset
        {
            get
            {
                bool isReset = false;

                int actY = Convert.ToInt32(ActiveDate.ToString("yy"));
                int curY = Convert.ToInt32(Date.ToString("yy"));

                int actM = Convert.ToInt32(ActiveDate.ToString("MM"));
                int curM = Convert.ToInt32(Date.ToString("MM"));

                int actD = Convert.ToInt32(ActiveDate.ToString("dd"));
                int curD = Convert.ToInt32(Date.ToString("dd"));

                if (ResetOption.Equals("Y"))
                {
                    if (curY > actY)
                    {
                        isReset = true;
                    }
                }
                else if (ResetOption.Equals("M"))
                {
                    if (curM > actM)
                    {
                        isReset = true;
                    }
                }
                else
                {
                    if (curD > actD)
                    {
                        isReset = true;
                    }
                }

                return isReset;
            }
        }
    }
}
