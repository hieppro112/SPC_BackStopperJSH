using MahApps.Metro.Controls;
using System.Windows;
using System.Windows.Media;

namespace back_stopper
{
    public enum DialogType
    {
        Confirm,  // Tím  — xác nhận thông thường
        Warning,  // Vàng — cảnh báo
        Danger    // Đỏ   — hành động nguy hiểm (xóa, reset...)
    }

    public partial class ConfirmDialog : MetroWindow
    {
        // Kết quả người dùng chọn
        public bool IsConfirmed { get; private set; } = false;

        public ConfirmDialog(
            string message,
            string title = "Xác nhận",
            string subtitle = "Hành động này cần xác nhận",
            string confirmText = "Xác nhận",
            string cancelText = "Hủy",
            DialogType type = DialogType.Confirm)
        {
            InitializeComponent();

            // Gán nội dung
            txt_message.Text = message;
            txt_title.Text = title;
            txt_subtitle.Text = subtitle;
            btn_confirm.Content = confirmText;
            btn_cancel.Content = cancelText;

            // Áp màu theo loại dialog
            ApplyType(type);
        }

        private void ApplyType(DialogType type)
        {
            switch (type)
            {
                case DialogType.Warning:
                    iconBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFBEB"));
                    iconText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
                    iconText.Text = "⚠";
                    btn_confirm.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
                    break;

                case DialogType.Danger:
                    iconBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEF2F2"));
                    iconText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));
                    iconText.Text = "!";
                    btn_confirm.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));
                    break;

                default: // Confirm — tím
                    iconBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EEF2FF"));
                    iconText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4F46E5"));
                    iconText.Text = "?";
                    btn_confirm.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4F46E5"));
                    break;
            }
        }

        private void btn_confirm_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = true;
            Close();
        }

        private void btn_cancel_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            Close();
        }
    }
}