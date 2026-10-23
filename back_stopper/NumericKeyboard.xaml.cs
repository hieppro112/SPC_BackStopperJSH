using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace back_stopper
{
    public partial class NumericKeyboard : Window
    {
        private readonly FrameworkElement _target;
        private readonly int _maxLength;
        private readonly bool _isPassword;

        private string _buffer;
        // vi tri con tro trong _buffer (0 = dau chuoi)
        private int _caret;
        // giong SelectAll: so dau tien bam se thay the noi dung cu
        private bool _replaceOnFirstKey = true;

        private static readonly Brush CaretBrush =
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB"));

        private NumericKeyboard(FrameworkElement target, string initialText,
            int maxLength, bool isPassword)
        {
            InitializeComponent();
            _target = target;
            _maxLength = maxLength;
            _isPassword = isPassword;
            _buffer = initialText ?? "";
            _caret = _buffer.Length;

            Loaded += (s, e) => PositionNearTarget();
            UpdateDisplay();
        }

        // Mo ban phim so, tra ve chuoi da nhap (null neu bam Huy)
        public static string Show(Window owner, FrameworkElement target, string initialText,
            int maxLength = 0, bool isPassword = false)
        {
            var keyboard = new NumericKeyboard(target, initialText, maxLength, isPassword);
            keyboard.Owner = owner;
            return keyboard.ShowDialog() == true ? keyboard._buffer : null;
        }

        // Dat ban phim ngay duoi o nhap, khong cho tran ra ngoai man hinh
        private void PositionNearTarget()
        {
            PresentationSource source = _target != null ? PresentationSource.FromVisual(_target) : null;
            if (source == null || source.CompositionTarget == null)
            {
                Rect center = SystemParameters.WorkArea;
                Left = center.Left + (center.Width - ActualWidth) / 2;
                Top = center.Top + (center.Height - ActualHeight) / 2;
                return;
            }

            // PointToScreen tra ve pixel thiet bi -> doi sang DIP cho dung khi man hinh scale 125%/150%
            Matrix fromDevice = source.CompositionTarget.TransformFromDevice;
            Point targetTop = fromDevice.Transform(_target.PointToScreen(new Point(0, 0)));
            Point targetBottom = fromDevice.Transform(_target.PointToScreen(new Point(0, _target.ActualHeight)));

            Rect area = SystemParameters.WorkArea;

            double left = Math.Min(targetTop.X, area.Right - ActualWidth);
            double top = targetBottom.Y + 4;

            // khong du cho phia duoi -> hien phia tren o nhap
            if (top + ActualHeight > area.Bottom)
                top = targetTop.Y - ActualHeight - 4;

            Left = Math.Max(left, area.Left);
            Top = Math.Max(top, area.Top);
        }

        // Hien chuoi kem con tro "|" tai vi tri dang sua
        private void UpdateDisplay()
        {
            string shown = _isPassword ? new string('●', _buffer.Length) : _buffer;

            txt_display.Inlines.Clear();
            txt_display.Inlines.Add(new Run(shown.Substring(0, _caret)));
            txt_display.Inlines.Add(new Run("|") { Foreground = CaretBrush, FontWeight = FontWeights.Normal });
            txt_display.Inlines.Add(new Run(shown.Substring(_caret)));

            txt_hint.Text = _maxLength > 0 ? $"{_buffer.Length}/{_maxLength} số" : "";
        }

        // Chen ky tu tai vi tri con tro
        private void InsertKey(string key)
        {
            if (_replaceOnFirstKey)
            {
                _buffer = "";
                _caret = 0;
                _replaceOnFirstKey = false;
            }

            if (key == ".")
            {
                if (_buffer.Contains(".")) return;
                // ".5" -> "0.5"
                if (_caret == 0) key = "0.";
            }

            if (_maxLength > 0 && _buffer.Length + key.Length > _maxLength) return;

            _buffer = _buffer.Insert(_caret, key);
            _caret += key.Length;
            UpdateDisplay();
        }

        // Xoa ky tu ben trai con tro
        private void Backspace()
        {
            _replaceOnFirstKey = false;
            if (_caret > 0)
            {
                _buffer = _buffer.Remove(_caret - 1, 1);
                _caret--;
            }
            UpdateDisplay();
        }

        private void ClearAll()
        {
            _replaceOnFirstKey = false;
            _buffer = "";
            _caret = 0;
            UpdateDisplay();
        }

        private void MoveCaret(int newCaret)
        {
            // da di chuyen con tro = muon sua tiep, khong thay the noi dung cu nua
            _replaceOnFirstKey = false;
            _caret = Math.Max(0, Math.Min(_buffer.Length, newCaret));
            UpdateDisplay();
        }

        private void NumKey_Click(object sender, RoutedEventArgs e)
        {
            InsertKey(((System.Windows.Controls.Button)sender).Content.ToString());
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            Backspace();
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            ClearAll();
        }

        private void Left_Click(object sender, RoutedEventArgs e)
        {
            MoveCaret(_caret - 1);
        }

        private void Right_Click(object sender, RoutedEventArgs e)
        {
            MoveCaret(_caret + 1);
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        // Van nhan ban phim cung / may quet ma vach khi ban phim so dang mo
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key >= Key.D0 && e.Key <= Key.D9)
                InsertKey(((int)(e.Key - Key.D0)).ToString());
            else if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
                InsertKey(((int)(e.Key - Key.NumPad0)).ToString());
            else if (e.Key == Key.OemPeriod || e.Key == Key.Decimal)
                InsertKey(".");
            else if (e.Key == Key.Back)
                Backspace();
            else if (e.Key == Key.Delete)
            {
                if (_caret < _buffer.Length)
                {
                    _replaceOnFirstKey = false;
                    _buffer = _buffer.Remove(_caret, 1);
                    UpdateDisplay();
                }
            }
            else if (e.Key == Key.Left)
                MoveCaret(_caret - 1);
            else if (e.Key == Key.Right)
                MoveCaret(_caret + 1);
            else if (e.Key == Key.Home)
                MoveCaret(0);
            else if (e.Key == Key.End)
                MoveCaret(_buffer.Length);
            else if (e.Key == Key.Enter)
                DialogResult = true;
            else if (e.Key == Key.Escape)
                DialogResult = false;
            else
                return;

            e.Handled = true;
        }

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }
    }
}
