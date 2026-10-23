using back_stopper.Database;
using LiveCharts;
using LiveCharts.Wpf;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace back_stopper.ViewModel
{
    public class BackStopper_model : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public SeriesCollection SeriesCollection { get; set; }

        public string[] Labels { get; set; }
        public Func<double, string> Formatter { get; set; }
        public BackStopper_model()
        {
            // Khởi tạo biểu đồ rỗng, dữ liệu nạp sau bằng LoadChartAsync()
            SeriesCollection = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Số lượng",
                    Values = new ChartValues<double>(),
                    DataLabels = true
                }
            };
            Labels = new string[0];

            Formatter = value => value.ToString("N0");
        }

        // lay so PO cua 8 gio gan nhat (-7h ... gio hien tai)
        public async Task LoadChartAsync()
        {
            var values = new ChartValues<double>();
            for (int h = -7; h <= 0; h++)
            {
                values.Add(await Sql.GetCountPO(h));
            }

            DateTime dateNow = DateTime.Now;
            Labels = Enumerable.Range(-7, 8)
                .Select(h => dateNow.AddHours(h).ToString("HH") + ":00")
                .ToArray();

            SeriesCollection[0].Values = values;
            OnPropertyChanged(nameof(Labels));
        }

        private string _currentPost = "-";
        public string CurrenPost
        {
            get => _currentPost;
            set
            {
                _currentPost = value;
                OnPropertyChanged();
            }
        }
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }
}
