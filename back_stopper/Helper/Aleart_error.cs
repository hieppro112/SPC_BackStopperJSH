using back_stopper.Database;
using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace back_stopper.Helper
{
    // 1 bao loi cua cong nhan: gui len bang BackStopperErrorReport (KVH),
    // mat mang thi luu tam file json tren may va tu gui lai sau
    public class Aleart_error
    {
        public string nameMachine { get; set; }
        public string idEmployee { get; set; }
        public string nameEmployee { get; set; }
        public string Title { get; set; }   //loai loi
        public string Message { get; set; } //chi tiet loi
        public string po { get; set; }      //PO dang chay luc bao loi
        public DateTime dateAleart
        {
            get;set;
        } = DateTime.Now;

        //thu muc luu bao loi chua gui duoc
        private const string PendingFolder = @"C:\BackStopper_config\alerts_pending";

        private static readonly SemaphoreSlim _resendLock = new SemaphoreSlim(1, 1);

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            // giu nguyen tieng Viet, khong bi doi thanh \u1EA1...
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        // Gui len server. Tra ve true neu da len database, false neu loi mang -> da luu tam tren may
        public async Task<bool> SendAsync()
        {
            try
            {
                await Sql.InsertErrorReport(this);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Loi gui bao loi, luu tam: " + ex.Message);
                SaveJson();
                return false;
            }
        }

        // Luu bao loi nay thanh 1 file json rieng trong thu muc cho gui
        public void SaveJson()
        {
            Directory.CreateDirectory(PendingFolder);
            string fileName = $"alert_{dateAleart:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}.json";
            File.WriteAllText(Path.Combine(PendingFolder, fileName), JsonSerializer.Serialize(this, _jsonOptions));
        }

        // Gui lai cac bao loi dang luu tam. Gui duoc file nao thi xoa file do; tra ve so bao loi da gui
        public static async Task<int> ResendPendingAsync()
        {
            if (!Directory.Exists(PendingFolder)) return 0;

            // dang co 1 lan gui lai khac chay thi bo qua
            if (!await _resendLock.WaitAsync(0)) return 0;

            int sent = 0;
            try
            {
                foreach (string path in Directory.GetFiles(PendingFolder, "alert_*.json"))
                {
                    Aleart_error alert;
                    try
                    {
                        alert = JsonSerializer.Deserialize<Aleart_error>(File.ReadAllText(path), _jsonOptions);
                    }
                    catch (JsonException)
                    {
                        // file hong -> doi ten de khong doc lai nua, van giu de xem
                        File.Move(path, path + ".broken");
                        continue;
                    }

                    if (alert == null) continue;

                    // loi mang thi dung, lan sau gui tiep
                    await Sql.InsertErrorReport(alert);
                    File.Delete(path);
                    sent++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Chua gui lai duoc bao loi: " + ex.Message);
            }
            finally
            {
                _resendLock.Release();
            }

            return sent;
        }
    }
}
