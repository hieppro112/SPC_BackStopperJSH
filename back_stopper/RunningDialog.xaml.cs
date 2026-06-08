using System;
using System.Windows;
using System.Windows.Media;

namespace back_stopper
{
    public partial class RunningDialog : Window
    {
        // MainWindow gán callback này để xử lý khi user nhấn Hủy
        public Action OnCancelled;

        public RunningDialog(string po)
        {
            InitializeComponent();
            txt_po_info.Text = $"PO: {po}";
        }

        public void SetRunning()
        {
            Dispatcher.InvokeAsync(() =>
            {
                txt_icon.Text = "▶️";
                txt_status.Text = "Đang chạy motor...";
                txt_status.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#efffe8"));
                border_status.Background = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#f9cb9c"));
                btn_cancel.IsEnabled = true;
            });
        }

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
                btn_cancel.IsEnabled = true;
            });
        }

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
                // Ẩn nút Hủy khi hoàn thành
                btn_cancel.Visibility = Visibility.Collapsed;
            });
        }

        private void btn_cancel_Click(object sender, RoutedEventArgs e)
        {
            OnCancelled?.Invoke();
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            OnCancelled?.Invoke();
            base.OnClosed(e);
        }
    }
}