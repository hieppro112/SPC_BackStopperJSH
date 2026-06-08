using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Threading.Tasks;
using back_stopper.Model;

namespace back_stopper
{


    public partial class ToastNotification : Window
    {
        private Color _colorA; // màu sáng
        private Color _colorB; // màu đậm (nháy qua lại)

        public ToastNotification(string message, LogType type = LogType.Error)
        {
            InitializeComponent();

            txt_message.Text = message;

            if (type == LogType.Error)
            {
                _colorA = (Color)ColorConverter.ConvertFromString("#FEF2F2");
                _colorB = (Color)ColorConverter.ConvertFromString("#F87171");
                txt_icon.Text = "❌";
                txt_message.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#404040"));
            }
            else if(type == LogType.Warning)
            {
                _colorA = (Color)ColorConverter.ConvertFromString("#ffff33");
                _colorB = (Color)ColorConverter.ConvertFromString("#ffcb99");
                txt_icon.Text = "⚠️";
                txt_message.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#404040"));
            }
            else if (type == LogType.Info)
            {
                _colorA = (Color)ColorConverter.ConvertFromString("#99ffff");
                _colorB = (Color)ColorConverter.ConvertFromString("#ffffff");
                txt_icon.Text = "！";
                txt_message.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#404040"));
            }
            else
            {
                _colorA = (Color)ColorConverter.ConvertFromString("#b3ff66");
                _colorB = (Color)ColorConverter.ConvertFromString("#ffffff");
                txt_icon.Text = "✅";
                txt_message.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#404040"));
            }

            border_main.Background = new SolidColorBrush(_colorA);
        }

        public async Task ShowAndAutoClose(int durationMs = 2000)
        {
            Show();
            StartBlink();
            await Task.Delay(durationMs);
            Close();
        }

        private void StartBlink()
        {
            // Animation nháy background qua lại giữa 2 màu
            var brush = new SolidColorBrush(_colorA);
            border_main.Background = brush;

            var anim = new ColorAnimation
            {
                From = _colorA,
                To = _colorB,
                Duration = TimeSpan.FromMilliseconds(300),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };

            brush.BeginAnimation(SolidColorBrush.ColorProperty, anim);
        }
    }
}