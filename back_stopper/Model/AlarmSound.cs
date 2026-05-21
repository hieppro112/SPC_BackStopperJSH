using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace back_stopper.Model
{
        public static class AlarmSound
        {
            private static MediaPlayer playerMin = new MediaPlayer();
            private static MediaPlayer playerMax = new MediaPlayer();

        public static void PlayAlarm()
        {
            playerMin.Open(
                    new Uri(
                        AppDomain.CurrentDomain.BaseDirectory +
                        @"Assets\coi_bao_dong.mp3"));

            playerMin.Volume = 1.0;

            playerMin.Play();
        }

        public static void Stop()
        {
            playerMin.Stop();
        }

        public static void PlayAlarmMax()
        {
            playerMax.Open(
                    new Uri(
                        AppDomain.CurrentDomain.BaseDirectory +
                        @"Assets\coi_bao_dong.mp3"));

            playerMax.Volume = 1.0;

            playerMax.Play();
        }

        public static void StopMax()
        {
            playerMax.Stop();
        }




    }

    }

