using back_stopper.Helper;
using System;
using System.Windows;
using System.Windows.Controls;

namespace back_stopper
{
    /// <summary>
    /// Interaction logic for ErrorReportDialog.xaml
    /// </summary>

    public partial class ErrorReportDialog : Window
    {
        //thong tin luc bao loi, MainWindow truyen vao
        private readonly string _idEmployee;
        private readonly string _nameEmployee;
        private readonly string _po;

        public ErrorReportDialog(string idEmployee, string nameEmployee, string po)
        {
            InitializeComponent();
            _idEmployee = idEmployee;
            _nameEmployee = nameEmployee;
            _po = po;
        }



        // Nút Hủy
        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        // Nút Gửi
        private async void btnSubmit_Click(object sender, RoutedEventArgs e)
        {
            //if (string.IsNullOrWhiteSpace(txtErrorDetail.Text))
            //{
            //    MessageBox.Show("Vui lòng nhập chi tiết lỗi!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            //    return;
            //}

            // Lấy dữ liệu gửi đi
            string selectedType = (cboErrorType.SelectedItem as ComboBoxItem)?.Content.ToString();
            string detail = txtErrorDetail.Text;

            var alert = new Aleart_error
            {
                nameMachine = Environment.MachineName,
                idEmployee = _idEmployee,
                nameEmployee = _nameEmployee,
                Title = selectedType,
                Message = detail,
                po = _po
            };

            // khoa nut trong luc gui, tranh bam 2 lan thanh 2 bao loi
            btnSubmit.IsEnabled = false;
            btnCancel.IsEnabled = false;

            bool sent;
            try
            {
                sent = await alert.SendAsync();
            }
            catch (Exception ex)
            {
                // ca gui lan luu tam deu loi (vd khong ghi duoc o C)
                MessageBox.Show("Không gửi được báo cáo lỗi: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                btnSubmit.IsEnabled = true;
                btnCancel.IsEnabled = true;
                return;
            }

            if (sent)
            {
                MessageBox.Show("Đã gửi báo cáo lỗi thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                // co mang lai thi gui luon cac bao loi cu dang luu tam
                _ = Aleart_error.ResendPendingAsync();
            }
            else
            {
                MessageBox.Show("Không kết nối được server.\nBáo cáo lỗi đã được lưu trên máy và sẽ tự gửi lại khi có mạng.",
                    "Đã lưu tạm", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            this.DialogResult = true;
            this.Close();
        }


        private void txtErrorDetail_TextChanged(object sender, TextChangedEventArgs e)
        {
            lblCharCount.Text = $"Ký tự: {txtErrorDetail.Text.Length} / 500";
        }
    }
}

