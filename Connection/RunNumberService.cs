using System.Data;
using INVOICE_VENDER_API.Services.Create;
using Microsoft.Data.SqlClient;

namespace INVOICEBILLINENOTE_API.Connection
{
    public class RunNumberService
    {
        private SqlConnectDB oCon = new SqlConnectDB("dbSCM");


        public string NextId(string key)
        {
            RunningNumber num = LoadUnique(key);

            SqlCommand sqlUpdate = new SqlCommand();
            sqlUpdate.CommandText = @"UPDATE DCRunNbr SET NextID = @p_NextId, ActiveDate = @p_ActivaDate WHERE (DocKey = @p_DocKey)";
            sqlUpdate.Parameters.Add(new SqlParameter("@p_NextId", num.NextId));
            sqlUpdate.Parameters.Add(new SqlParameter("@p_ActivaDate", DateTime.Now));
            sqlUpdate.Parameters.Add(new SqlParameter("@p_DocKey", key));
            oCon.Query(sqlUpdate);

            return num.ToString(true);
        }

        public RunningNumber LoadUnique(string key)
        {
            try
            {
                RunningNumber item = new RunningNumber();

                SqlCommand sqlSelect = new SqlCommand();
                sqlSelect.CommandText = @"SELECT * FROM DCRunNbr WHERE (DocKey = @p_Dockey)";
                sqlSelect.Parameters.Add(new SqlParameter("@p_Dockey", key));
                sqlSelect.CommandTimeout = 180;
                DataTable tb = oCon.Query(sqlSelect);

                if (tb.Rows.Count != 0)
                {
                    item = (RunningNumber)QueryForObject(tb.Rows[0], typeof(RunningNumber));

                    return item;

                }
                return null;
            }
            catch
            {
                return null;

            }
        }


        public object QueryForObject(DataRow row, Type t)
        {
            if (t == typeof(RunningNumber))
            {
                RunningNumber item = new RunningNumber();


                try
                {
                    item.Key = Convert.ToString(row["DocKey"]);
                }
                catch
                {
                }
                try
                {
                    item.Prefix = Convert.ToString(row["DocPrefix"]);
                }
                catch { }
                try
                {
                    item.LenYearPrefix = Convert.ToInt32(row["YearNbrPrefix"]);
                }
                catch { }
                try
                {
                    item.LenMonthPrefix = Convert.ToInt32(row["MonthNbrPrefix"]);
                }
                catch { }
                try
                {
                    item.LenDayPrefix = Convert.ToInt32(row["DayNbrPrefix"]);
                }
                catch { }
                try
                {
                    item.LenRunId = Convert.ToInt32(row["FormatNbr"]);
                }
                catch { }
                try
                {
                    item.NextId = Convert.ToInt32(row["NextId"]);
                }
                catch { }
                try
                {
                    item.ActiveDate = Convert.ToDateTime(row["ActiveDate"]);
                }
                catch { }
                try
                {
                    item.Date = Convert.ToDateTime(row["CurDate"]);
                }
                catch { }
                try
                {
                    item.Remark = Convert.ToString(row["Remark"]);
                }
                catch { }
                try
                {
                    item.ResetOption = Convert.ToString(row["ResetOption"]);
                }
                catch { }
                return item;
            }
            else
            {
                return null;
            }
        }

    }
}
