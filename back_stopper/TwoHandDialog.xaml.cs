using MahApps.Metro.Controls;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace back_stopper
{
    public partial class TwoHandDialog : MetroWindow
    {
        public Action OnBothPressed;
        public Action OnAnyReleased;

        private bool _leftDown = false;
        private bool _rightDown = false;

        private CancellationTokenSource _holdCts;
        public TwoHandDialog(string poInfo)
        {
            InitializeComponent();
            txt_po_info.Text = $"PO: {poInfo}";

                // Cho phép nhận phím
                KeyDown += (s, e) =>
                {
                    if (e.Key == Key.A) { _leftDown = true; UpdateState(); }
                    if (e.Key == Key.L) { _rightDown = true; UpdateState(); }
                };

                KeyUp += (s, e) =>
                {
                    if (e.Key == Key.A) { _leftDown = false; UpdateState(); OnAnyReleased?.Invoke(); }
                    if (e.Key == Key.L) { _rightDown = false; UpdateState(); OnAnyReleased?.Invoke(); }
                };
            
        }

        // ===== TAY TRÁI — MOUSE =====
        private void btn_left_Down(object sender, MouseButtonEventArgs e)
        {
            _leftDown = true;
            UpdateState();
        }

        private void btn_left_Up(object sender, MouseButtonEventArgs e)
        {
            _leftDown = false;
            UpdateState();
            OnAnyReleased?.Invoke();
        }

        // ===== TAY PHẢI — MOUSE =====
        private void btn_right_Down(object sender, MouseButtonEventArgs e)
        {
            _rightDown = true;
            UpdateState();
        }

        private void btn_right_Up(object sender, MouseButtonEventArgs e)
        {
            _rightDown = false;
            UpdateState();
            OnAnyReleased?.Invoke();
        }

        // ===== TAY TRÁI — TOUCH =====
        private void btn_left_TouchDown(object sender, TouchEventArgs e)
        {
            e.Handled = true; // chặn convert sang mouse event
            _leftDown = true;
            UpdateState();
        }

        private void btn_left_TouchUp(object sender, TouchEventArgs e)
        {
            e.Handled = true;
            _leftDown = false;
            UpdateState();
            OnAnyReleased?.Invoke();
        }

        // ===== TAY PHẢI — TOUCH =====
        private void btn_right_TouchDown(object sender, TouchEventArgs e)
        {
            e.Handled = true; // chặn convert sang mouse event
            _rightDown = true;
            UpdateState();
        }

        private void btn_right_TouchUp(object sender, TouchEventArgs e)
        {
            e.Handled = true;
            _rightDown = false;
            UpdateState();
            OnAnyReleased?.Invoke();
        }

        // ===== CẬP NHẬT TRẠNG THÁI UI =====
        //private void UpdateState()
        //{
        //    if (_leftDown && _rightDown)
        //    {
        //        // Cả 2 đang giữ → RUN
        //        txt_status.Text = "▶  Đang chạy motor...";
        //        txt_status.Foreground = new SolidColorBrush(
        //            (Color)ColorConverter.ConvertFromString("#16A34A"));
        //        border_status.Background = new SolidColorBrush(
        //            (Color)ColorConverter.ConvertFromString("#F0FDF4"));

        //        HighlightButton(border_left, active: true);
        //        HighlightButton(border_right, active: true);

        //        OnBothPressed?.Invoke();
        //    }
        //    else if (_leftDown || _rightDown)
        //    {
        //        // Chỉ 1 nút → chờ nút còn lại
        //        txt_status.Text = "⏳  Giữ thêm nút còn lại...";
        //        txt_status.Foreground = new SolidColorBrush(
        //            (Color)ColorConverter.ConvertFromString("#F59E0B"));
        //        border_status.Background = new SolidColorBrush(
        //            (Color)ColorConverter.ConvertFromString("#FFFBEB"));

        //        HighlightButton(border_left, active: _leftDown);
        //        HighlightButton(border_right, active: _rightDown);
        //    }
        //    else
        //    {
        //        // Không giữ nút nào
        //        txt_status.Text = "⏸  Chưa sẵn sàng — giữ cả 2 nút bên dưới";
        //        txt_status.Foreground = new SolidColorBrush(
        //            (Color)ColorConverter.ConvertFromString("#888888"));
        //        border_status.Background = new SolidColorBrush(
        //            (Color)ColorConverter.ConvertFromString("#F3F4F6"));

        //        HighlightButton(border_left, active: false);
        //        HighlightButton(border_right, active: false);
        //    }
        //}

        private void UpdateState()
        {
            if (_leftDown && _rightDown)
            {
                // Bắt đầu đếm 3s
                if (_holdCts == null)
                {
                    _holdCts = new CancellationTokenSource();
                    var token = _holdCts.Token;

                    txt_status.Text = "⏳  Giữ trong 3 giây...";
                    txt_status.Foreground = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#F59E0B"));
                    border_status.Background = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#FFFBEB"));

                    HighlightButton(border_left, active: true);
                    HighlightButton(border_right, active: true);

                    Task.Run(async () =>
                    {
                        // Đếm ngược 3s hiển thị lên UI
                        for (int i = 3; i > 0; i--)
                        {
                            if (token.IsCancellationRequested) return;

                            int count = i;
                            await Dispatcher.InvokeAsync(() =>
                            {
                                txt_status.Text = $"⏳  Giữ trong {count} giây...";
                            });

                            await Task.Delay(1000, token).ContinueWith(_ => { });
                        }

                        if (token.IsCancellationRequested) return;

                        // Đủ 3s → kích hoạt
                        await Dispatcher.InvokeAsync(() =>
                        {
                            txt_status.Text = "▶  Đang chạy motor...";
                            txt_status.Foreground = new SolidColorBrush(
                                (Color)ColorConverter.ConvertFromString("#16A34A"));
                            border_status.Background = new SolidColorBrush(
                                (Color)ColorConverter.ConvertFromString("#F0FDF4"));

                            OnBothPressed?.Invoke();
                        });
                    });
                }
            }
            else
            {
                // Thả tay → hủy đếm ngược
                _holdCts?.Cancel();
                _holdCts = null;

                if (_leftDown || _rightDown)
                {
                    txt_status.Text = "⏳  Giữ thêm nút còn lại...";
                    txt_status.Foreground = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#F59E0B"));
                    border_status.Background = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#FFFBEB"));

                    HighlightButton(border_left, active: _leftDown);
                    HighlightButton(border_right, active: _rightDown);
                }
                else
                {
                    txt_status.Text = "⏸  Chưa sẵn sàng — giữ cả 2 nút bên dưới";
                    txt_status.Foreground = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#888888"));
                    border_status.Background = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#F3F4F6"));

                    HighlightButton(border_left, active: false);
                    HighlightButton(border_right, active: false);

                    OnAnyReleased?.Invoke();
                }
            }
        }



        private void HighlightButton(System.Windows.Controls.Border border, bool active)
        {
            border.Background = new SolidColorBrush(active
                ? (Color)ColorConverter.ConvertFromString("#DCFCE7")
                : (Color)ColorConverter.ConvertFromString("#F3F4F6"));
        }

        private void btn_cancel_Click(object sender, RoutedEventArgs e)
        {
            OnAnyReleased?.Invoke();
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            // Đảm bảo stop motor khi đóng bằng bất kỳ cách nào
            OnAnyReleased?.Invoke();
            base.OnClosed(e);
        }

        
    }
}