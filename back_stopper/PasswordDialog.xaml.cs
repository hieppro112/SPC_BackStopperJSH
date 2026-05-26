using MahApps.Metro.Controls;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace back_stopper
{
    public partial class PasswordDialog : MetroWindow
    {
        // Mật khẩu đúng — đổi thành mật khẩu thực tế của bạn
        private const string CORRECT_PASSWORD = "123";

        public bool IsConfirmed { get; private set; } = false;

        public PasswordDialog()
        {
            InitializeComponent();

            // Focus vào ô nhập khi dialog mở
            Loaded += (s, e) => txt_password.Focus();
        }

        // Bấm Enter trong ô mật khẩu = bấm Xác nhận
        private void txt_password_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                Verify();
            else if (e.Key == Key.Escape)
                Close();

            // Ẩn lỗi khi người dùng bắt đầu gõ lại
            if (txt_error.Visibility == Visibility.Visible)
            {
                txt_error.Visibility = Visibility.Collapsed;
                border_input.BorderBrush = new SolidColorBrush(Colors.Transparent);
            }
        }

        private void btn_confirm_Click(object sender, RoutedEventArgs e)
        {
            Verify();
        }

        private void btn_cancel_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            Close();
        }

        private void Verify()
        {
            string input = txt_password.Password;

            if (input == CORRECT_PASSWORD)
            {
                IsConfirmed = true;
                Close();
            }
            else
            {
                // Hiện lỗi + viền đỏ
                txt_error.Visibility = Visibility.Visible;
                border_input.BorderBrush = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#DC2626"));

                // Xóa ô nhập và focus lại
                txt_password.Clear();
                txt_password.Focus();

                // Rung nhẹ border (optional — dùng animation)
                ShakeInput();
            }
        }

        private void ShakeInput()
        {
            var anim = new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames();
            anim.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(0,
                System.Windows.Media.Animation.KeyTime.FromTimeSpan(System.TimeSpan.FromMilliseconds(0))));
            anim.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(8,
                System.Windows.Media.Animation.KeyTime.FromTimeSpan(System.TimeSpan.FromMilliseconds(50))));
            anim.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(-8,
                System.Windows.Media.Animation.KeyTime.FromTimeSpan(System.TimeSpan.FromMilliseconds(100))));
            anim.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(6,
                System.Windows.Media.Animation.KeyTime.FromTimeSpan(System.TimeSpan.FromMilliseconds(150))));
            anim.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(-6,
                System.Windows.Media.Animation.KeyTime.FromTimeSpan(System.TimeSpan.FromMilliseconds(200))));
            anim.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(0,
                System.Windows.Media.Animation.KeyTime.FromTimeSpan(System.TimeSpan.FromMilliseconds(250))));

            var transform = new TranslateTransform();
            border_input.RenderTransform = transform;
            transform.BeginAnimation(TranslateTransform.XProperty, anim);
        }
    }
}