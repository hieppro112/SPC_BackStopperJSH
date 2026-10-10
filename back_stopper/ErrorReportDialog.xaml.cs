using System.Windows;
using System.Windows.Controls;

namespace back_stopper
{
    /// <summary>
    /// Interaction logic for ErrorReportDialog.xaml
    /// </summary>

    public partial class ErrorReportDialog : Window
    {
        public ErrorReportDialog()
        {
            InitializeComponent();
        }



        // Nút Hủy
        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        // Nút Gửi
        private void btnSubmit_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtErrorDetail.Text))
            {
                MessageBox.Show("Vui lòng nhập chi tiết lỗi!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Lấy dữ liệu gửi đi
            string selectedType = (cboErrorType.SelectedItem as ComboBoxItem)?.Content.ToString();
            string detail = txtErrorDetail.Text;

            // TODO: Gửi dữ liệu về Server hoặc lưu Log ở đây
            MessageBox.Show("Đã gửi báo cáo lỗi thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);

            this.DialogResult = true;
            this.Close();
        }


        private void txtErrorDetail_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtErrorDetail.Text))
            {
                lblCharCount.Text = $"Ký tự: {txtErrorDetail.Text.Length} / 500";
            }
        }
    }
}

