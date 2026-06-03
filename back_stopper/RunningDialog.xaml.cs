using System.Windows;
using System.Windows.Media;

namespace back_stopper
{
    public partial class RunningDialog : Window
    {
        public RunningDialog(string po)
        {
            InitializeComponent();
            txt_po_info.Text = $"PO: {po}";
        }

        // Gọi khi status_touch = 1 (đang chạy)
        public void SetRunning()
        {
            Dispatcher.InvokeAsync(() =>
            {
                txt_icon.Text = "▶️";
                txt_status.Text = "Đang chạy motor...";
                txt_status.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#16A34A"));
                border_status.Background = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#F0FDF4"));
            });
        }

        // Gọi khi status_touch = 0 (pause)
        public void SetPaused()
        {
            Dispatcher.InvokeAsync(() =>
            {
                txt_icon.Text = "✋";
                txt_status.Text = "Đã tạm dừng — giữ lại 2 nút để tiếp tục";
                txt_status.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#D97706"));
                border_status.Background = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#FFFBEB"));
            });
        }

        // Gọi khi hoàn thành hành trình
        public void SetCompleted()
        {
            Dispatcher.InvokeAsync(() =>
            {
                txt_icon.Text = "✅";
                txt_status.Text = "Hoàn thành!";
                txt_status.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#16A34A"));
                border_status.Background = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#F0FDF4"));
            });
        }
    }
}