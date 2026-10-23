using back_stopper.Model;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Markup;

namespace back_stopper.Database
{
    class Sql
    {
        private static string connectionString { get; } = "Data Source=192.168.122.2;Initial Catalog=MANUFASPCPD;User ID=kuser;Password=SPC123@";
        //dl nhan vien
        private static string connection_Employee { get; } = @"Data Source=192.168.0.11;Initial Catalog=SGPrecision;User ID=hrmsadmin;Password=adminhrms;Max Pool Size=50;Application Name=Molybden_DL;";
        private static string query_id_get_name { get; } = "select [Name] from DataSPC where [Code] = @idNV";
        private static string query_connect_data_history { get; } = "Data Source=192.168.122.2;Initial Catalog=F2Database;User ID=admin;Password=Buitanphat0201@";

        public static async Task<EmployeeData> GetEmployee(string id)
        {
            EmployeeData data = null;

            using (SqlConnection conn = new SqlConnection(connection_Employee))
            {
                await conn.OpenAsync();

                using (SqlCommand cmd = new SqlCommand(query_id_get_name, conn))
                {
                    cmd.Parameters.AddWithValue("@idNV", id);

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        try
                        {
                            if (await reader.ReadAsync())
                            {
                                data = new EmployeeData
                                {
                                    id = id,
                                    name = reader["Name"].ToString()
                                };
                            }
                        }
                        catch
                        {
                            Console.WriteLine("err load employee");

                        }
                    }
                }
            }

            return data;
        }

        //lay so luong don hang moi gio 
        private static string query_get_Count_Po { get; } = @"
              SELECT COUNT(DISTINCT AUFNR) AS SoLuongDonHang
            FROM [KVH].[dbo].[BackStopperHistoryMain]
            WHERE TIME_RUN >= DATEADD(HOUR, @Time_p, GETDATE())
              AND TIME_RUN <= DATEADD(HOUR, @Time_n, GETDATE());";
        public static async Task<int> GetCountPO(int hour)
        {
            using (SqlConnection conn = new SqlConnection(query_connect_data_history))
            {
                await conn.OpenAsync();

                using (SqlCommand cmd = new SqlCommand(query_get_Count_Po, conn))
                {
                    cmd.Parameters.AddWithValue("@Time_p", hour-1);
                    cmd.Parameters.AddWithValue("@Time_n", hour);


                    using (SqlDataReader reader =  await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            int.TryParse(reader["SoLuongDonHang"]?.ToString(), out int sluong_po);
                            return sluong_po;
                        }
                    }
                }
            }
            return 0;
        }

        //dl hang
        private static string getDataforPo { get; } = @"
            SELECT 
                req.AUFNR,
                req.GAMNG,
                cmt.prt_addcmt2,

                CASE 
                    WHEN DTL.RONAME IS NOT NULL 
                         AND LTRIM(RTRIM(DTL.RONAME)) <> '' 
                        THEN DTL.RONAME 
                    ELSE req.PHTX 
                END AS ProductName,

                req.PSTX,
                pc_master.AirHole,
                pc_master.AirHole_Tol_Low,
	            pc_master.AirHole_Tol_Up

            FROM MANUFASPCPD.dbo.MANUFA_F_PD_DT_REQ_HED req

            LEFT JOIN MANUFASPCPD.dbo.MANUFA_F_PD_DT_ORDER_DTL DTL 
                ON req.KDAUF = DTL.VBELN

            LEFT JOIN MANUFASPCPD.dbo.MANUFA_F_PD_DT_REQ_CMT CMT 
                ON req.AUFNR = CMT.AUFNR

            LEFT JOIN F2Database.dbo.F2_PC_MASTER_2 pc_master
                ON req.PHCD = pc_master.C_MATNR

            WHERE req.AUFNR = @po
            ";

        public static async Task<productData> GetProductInfo(string po)
        {
            productData data = null;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();

                using (SqlCommand cmd = new SqlCommand(getDataforPo, conn))
                {
                    cmd.Parameters.AddWithValue("@po", po);

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            double.TryParse(reader["AirHole"]?.ToString(), out double airhole);
                            MatchCollection matches = Regex.Matches(reader["ProductName"]?.ToString(), @"\d+");
                            double.TryParse(matches[0].Value, out double d);
                            double.TryParse(matches[1].Value, out double l);
                            double.TryParse(reader["AirHole_Tol_Low"]?.ToString(), out double ah_low);
                            double.TryParse(reader["AirHole_Tol_Up"]?.ToString(), out double ah_up);


                            data = new productData
                            {
                                Aufnr = reader["AUFNR"]?.ToString(),

                                Gamng = reader["GAMNG"] != DBNull.Value
                                    ? Convert.ToInt32(reader["GAMNG"])
                                    : 0,
                                Prt_addcmt2 = reader["prt_addcmt2"]?.ToString(),

                                ProductName = reader["ProductName"]?.ToString(),

                                Pstx = reader["PSTX"]?.ToString(),
                                C_D = d,
                                C_L = l,
                                airhole_to_low = ah_low,
                                airhole_to_up = ah_up,


                                airhole = airhole
                            };
                        }
                    }
                }
            }

            return data;
        }

        //đưa dữ liệu vào history
        public static async Task InsertDataHistory(string po, double ah, int origin_num, int indexNow, DateTime time, string msnv)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(query_connect_data_history))
                {
                    await conn.OpenAsync();
                    string query = "INSERT INTO [KVH].[dbo].[BackStopperHistoryMain]\r\n      " +
                        "([AUFNR], [AH], [ORIGIN], [INDEX_NOW], [TIME_RUN], [MSNV])\r\n" +
                        "VALUES (@po, @ah, @origin, @index, @time,@msnv)";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@po", po);
                        cmd.Parameters.AddWithValue("@ah", ah);
                        cmd.Parameters.AddWithValue("@origin", origin_num);
                        cmd.Parameters.AddWithValue("@index", indexNow);
                        cmd.Parameters.AddWithValue("@time", time);
                        cmd.Parameters.AddWithValue("@msnv", msnv);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine("Lỗi ex: "+ex);
            }

        }
    }
}

