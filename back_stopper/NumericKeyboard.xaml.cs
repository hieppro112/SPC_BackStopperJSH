using MahApps.Metro.Controls;
using System;
using System.Windows;
using System.Windows.Controls;

namespace back_stopper
{
    public partial class NumericKeyboard : Window
    {
        private TextBox _targetTextBox;
        private NumericUpDown _targetNumeric;
        private string _originalText;

        // Constructor cho TextBox
        public NumericKeyboard(TextBox target)
        {
            InitializeComponent();
            _targetTextBox = target;
            _originalText = target.Text;
            txt_display.Text = target.Text;
            PositionNearTarget(target);
        }

        // Constructor cho NumericUpDown
        public NumericKeyboard(NumericUpDown target)
        {
            InitializeComponent();
            _targetNumeric = target;
            _originalText = target.Value?.ToString() ?? "";
            txt_display.Text = _originalText;
            PositionNearTarget(target);
        }

        private void PositionNearTarget(FrameworkElement target)
        {
            var pos = target.PointToScreen(new Point(0, target.ActualHeight + 4));
            Left = pos.X;
            Top = pos.Y;

            var screen = SystemParameters.WorkArea;
            if (Left + 240 > screen.Right)
                Left = screen.Right - 240;
            if (Top + 400 > screen.Bottom)
                Top = pos.Y - 400 - target.ActualHeight;
        }

        // Lấy text hiện tại từ control đang active
        private string GetCurrentText()
        {
            if (_targetTextBox != null) return _targetTextBox.Text;
            if (_targetNumeric != null) return _targetNumeric.Value?.ToString() ?? "";
            return "";
        }

        // Set text vào control đang active
        private void SetCurrentText(string val)
        {
            if (_targetTextBox != null)
            {
                _targetTextBox.Text = val;
            }
            else if (_targetNumeric != null)
            {
                if (double.TryParse(val, out double result))
                    _targetNumeric.Value = result;
            }
            txt_display.Text = val;
        }

        private void NumKey_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            string val = btn.Content.ToString();

            string current = GetCurrentText();

            // Chỉ cho 1 dấu chấm
            if (val == "." && current.Contains(".")) return;

            SetCurrentText(current + val);
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            string current = GetCurrentText();
            if (current.Length > 0)
                SetCurrentText(current.Substring(0, current.Length - 1));
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            _targetTextBox?.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            _targetNumeric?.GetBindingExpression(NumericUpDown.ValueProperty)?.UpdateSource();
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            // Khôi phục giá trị gốc
            if (_targetTextBox != null)
                _targetTextBox.Text = _originalText;

            if (_targetNumeric != null)
            {
                if (double.TryParse(_originalText, out double original))
                    _targetNumeric.Value = original;
            }

            txt_display.Text = _originalText;
            Close();
        }

        private void Border_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                DragMove();
        }
    }
}