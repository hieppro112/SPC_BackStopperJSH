using System;
using System.IO;
using System.Media;
using System.Text;
using System.Windows.Media;
using System.Windows.Threading;

namespace back_stopper.Model
{
    // Am thanh canh bao:
    //  - Servo dang chay (quet PO, ve Home): tieng "bip... bip..." lap lai (SoundPlayer)
    //  - Cham sensor MIN/MAX: coi bao dong mp3 khoang 3 giay (MediaPlayer)
    // 2 loai dung 2 trinh phat khac nhau nen keu cung luc duoc.
    // Loi am thanh (thieu file, khong co loa...) chi bo qua, khong anh huong dieu khien may.
    // Goi tren UI thread.
    public static class AlarmSound
    {
        private const int LimitAlarmSeconds = 3;

        private static readonly MediaPlayer _limitPlayer = new MediaPlayer();
        private static DispatcherTimer _limitStopTimer;

        private static SoundPlayer _movingPlayer;
        // so lenh chay dang dien ra: lenh cu chua ket thuc da co lenh moi thi van giu tieng bip
        private static int _movingCount = 0;

        // Bat dau 1 lenh chay -> bat tieng bip (neu chua bat)
        public static void StartMoving()
        {
            _movingCount++;
            if (_movingCount > 1) return;

            try
            {
                if (_movingPlayer == null)
                {
                    _movingPlayer = new SoundPlayer(new MemoryStream(BuildBeepWav(1200, 180, 420)));
                    _movingPlayer.Load();
                }
                _movingPlayer.PlayLooping();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Loi phat am thanh chay: " + ex.Message);
            }
        }

        // Ket thuc 1 lenh chay -> het lenh nao dang chay thi tat tieng bip
        public static void StopMoving()
        {
            if (_movingCount == 0) return;
            _movingCount--;
            if (_movingCount > 0) return;

            try
            {
                _movingPlayer?.Stop();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Loi tat am thanh chay: " + ex.Message);
            }
        }

        // Coi bao dong khi cham sensor MIN/MAX, tu tat sau LimitAlarmSeconds giay
        public static void PlayLimitAlarm()
        {
            try
            {
                _limitPlayer.Open(new Uri(Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, @"Assets\audio\coi_bao_dong.mp3")));
                _limitPlayer.Volume = 1.0;
                _limitPlayer.Play();

                if (_limitStopTimer == null)
                {
                    _limitStopTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(LimitAlarmSeconds) };
                    _limitStopTimer.Tick += (s, e) =>
                    {
                        _limitStopTimer.Stop();
                        _limitPlayer.Stop();
                    };
                }
                // cham lai trong luc dang keu -> dem lai tu dau
                _limitStopTimer.Stop();
                _limitStopTimer.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Loi phat coi MIN/MAX: " + ex.Message);
            }
        }

        // Tao file WAV trong bo nho: 1 tieng bip (co fade de khong bi "tach") + khoang lang
        private static byte[] BuildBeepWav(int frequency, int beepMs, int silenceMs)
        {
            const int sampleRate = 22050;
            int beepSamples = sampleRate * beepMs / 1000;
            int totalSamples = sampleRate * (beepMs + silenceMs) / 1000;
            int fadeSamples = sampleRate * 5 / 1000;
            int dataLength = totalSamples * 2;

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                // header WAV PCM 16-bit mono
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + dataLength);
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(sampleRate);
                writer.Write(sampleRate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(dataLength);

                for (int i = 0; i < totalSamples; i++)
                {
                    short sample = 0;
                    if (i < beepSamples)
                    {
                        double envelope = Math.Min(1.0, Math.Min(i, beepSamples - i) / (double)fadeSamples);
                        sample = (short)(Math.Sin(2 * Math.PI * frequency * i / sampleRate) * envelope * short.MaxValue * 0.6);
                    }
                    writer.Write(sample);
                }

                writer.Flush();
                return stream.ToArray();
            }
        }
    }
}
