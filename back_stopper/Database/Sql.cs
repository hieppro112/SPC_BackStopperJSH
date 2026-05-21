using back_stopper.Model;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace back_stopper.Database
{
    class Sql
    {
        public static string connectionString { get; } = "Data Source=192.168.122.2;Initial Catalog=MANUFASPCPD;User ID=kuser;Password=SPC123@";

        //        public static string getDataforPo { get;} = @"SELECT 
        //    req_hed.AUFNR,
        //    req_hed.PHCD,
        //	req_hed.GAMNG,

        //	pc_master.C_L,
        //    pc_master.C_D,
        //	pc_master.AirHole
        //FROM MANUFASPCPD.dbo.MANUFA_F_PD_DT_REQ_HED req_hed
        //LEFT JOIN F2Database.dbo.F2_PC_MASTER_2 pc_master
        //    ON req_hed.PHCD = pc_master.C_MATNR
        //WHERE req_hed.AUFNR = @po
        //";

        //dl nhan vien
        public static string connection_Employee { get; } = @"Data Source=192.168.0.11;Initial Catalog=SGPrecision;User ID=hrmsadmin;Password=adminhrms;Max Pool Size=50;Application Name=Molybden_DL;";
        public static string query_id_get_name { get; } = "select [Name] from DataSPC where [Code] = @idNV";

        public static EmployeeData GetEmployee(string id)
        {
            EmployeeData data = null;

            using (SqlConnection conn = new SqlConnection(connection_Employee))
            {
                conn.Open();

                using (SqlCommand cmd = new SqlCommand(query_id_get_name, conn))
                {
                    cmd.Parameters.AddWithValue("@idNV", id);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        try
                        {
                            if (reader.Read())
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

        //dl hang
        public static string getDataforPo { get; } = @"
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
                pc_master.AirHole

            FROM MANUFASPCPD.dbo.MANUFA_F_PD_DT_REQ_HED req

            LEFT JOIN MANUFASPCPD.dbo.MANUFA_F_PD_DT_ORDER_DTL DTL 
                ON req.KDAUF = DTL.VBELN

            LEFT JOIN MANUFASPCPD.dbo.MANUFA_F_PD_DT_REQ_CMT CMT 
                ON req.AUFNR = CMT.AUFNR

            LEFT JOIN F2Database.dbo.F2_PC_MASTER_2 pc_master
                ON req.PHCD = pc_master.C_MATNR

            WHERE req.AUFNR = @po
            ";

        public static productData GetProductInfo(string po)
        {
            productData data = null;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                using (SqlCommand cmd = new SqlCommand(getDataforPo, conn))
                {
                    cmd.Parameters.AddWithValue("@po", po);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            double.TryParse(reader["AirHole"]?.ToString(), out double airhole);
                            MatchCollection matches = Regex.Matches(reader["ProductName"]?.ToString(), @"\d+");
                            double.TryParse(matches[0].Value, out double d);
                            double.TryParse(matches[1].Value, out double l);


                            data = new productData
                            {
                                Aufnr = reader["AUFNR"]?.ToString(),

                                Gamng = reader["GAMNG"] != DBNull.Value
                                    ? Convert.ToInt32(reader["GAMNG"])
                                    : 0,

                                Prt_addcmt2 = reader["prt_addcmt2"]?.ToString(),

                                ProductName = reader["ProductName"]?.ToString(),

                                Pstx = reader["PSTX"]?.ToString(),
                                C_D=d,
                                C_L=l,

                                airhole = airhole
                            };
                        }
                    }
                }
            }

            return data;
        }

    }
}

