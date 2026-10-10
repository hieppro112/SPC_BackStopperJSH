using back_stopper.Database;
using LiveCharts;
using LiveCharts.Wpf;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

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
            int Sluong_pr_7 = Sql.GetCountPO(-7);
            int Sluong_pr_6 = Sql.GetCountPO(-6);
            int Sluong_pr_5 = Sql.GetCountPO(-5);
            int Sluong_pr_4 = Sql.GetCountPO(-4);
            int Sluong_pr_3 = Sql.GetCountPO(-3);
            int Sluong_pr_2 = Sql.GetCountPO(-2);
            int Sluong_pr_1 = Sql.GetCountPO(-1);
            int Sluong_Now = Sql.GetCountPO(0);

            


            // Khởi tạo dữ liệu biểu đồ
            SeriesCollection = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Số lượng",
                    Values = new ChartValues<double>
                    {
                        Sluong_pr_7, Sluong_pr_6, Sluong_pr_5,Sluong_pr_4, Sluong_pr_3,Sluong_pr_2,Sluong_pr_1,Sluong_Now
                    },
                    DataLabels = true
                }
            };
            DateTime dateNow = DateTime.Now;
            Labels = new[]
                {
                dateNow.AddHours(-7).ToString("HH")+":00",
                dateNow.AddHours(-6).ToString("HH")+":00",
                dateNow.AddHours(-5).ToString("HH")+":00",
                dateNow.AddHours(-4).ToString("HH")+":00",
                dateNow.AddHours(-3).ToString("HH")+":00",
                dateNow.AddHours(-2).ToString("HH")+":00",
                dateNow.AddHours(-1).ToString("HH")+":00",
                dateNow.AddHours(0).ToString("HH")+":00",
            };

            Formatter = value => value.ToString("N0");
            

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
