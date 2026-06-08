using System.Windows;
using System.Windows.Media;

namespace back_stopper
{
    public partial class RestartingDialog : Window
    {
        public RestartingDialog()
        {
            InitializeComponent();
        }

        // Gọi khi hoàn thành
        public void SetCompleted()
        {
            Dispatcher.InvokeAsync(() =>
            {
                txt_icon.Text = "✅";
                txt_status.Text = "Restart STOP!";
                txt_status.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#16A34A"));
                border_status.Background = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#F0FDF4"));
                progress_bar.IsIndeterminate = false;
                progress_bar.Value = 100;
                progress_bar.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#16A34A"));
            });
        }
    }
}