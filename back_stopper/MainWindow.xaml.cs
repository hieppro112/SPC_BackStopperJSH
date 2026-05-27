using back_stopper.Database;
using back_stopper.Model;
using CdioCs;
using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using AutoUpdaterDotNET;


namespace back_stopper
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        //maneger sensor
        bool check_sst_0 = false;
        bool check_sst_1 = false;

        //gia tri goc moi khi set 
        public int? Set_origin_number = null;
        //gioi han khong cho servo chay ra khoi 
        int lm_min = -50000;
        int lm_max = 41000;
        //doc khong bi nhanh hon 
        private TaskCompletionSource<double> positionTcs;

        private double targetPosition = 0;
        //bien dung lock khong cho cmd chay
        private readonly object serialLock = new object();

        private volatile bool isMoving = false;
        private volatile bool isRestarting = false;

        private bool lastSensorState = false;

        //bien dung de pausse luong sensor
        private ManualResetEventSlim pauseEvent =
    new ManualResetEventSlim(true);
        // Khai báo đối tượng API của Contec
        private Cdio cdio = new Cdio();

        // Biến lưu ID phiên làm việc với thiết bị
        private short m_Id;

        //bien doi tuong realtime 
        private CancellationTokenSource cts;
        private Task ioTask;

        //toa do goc 
        int tdGoc = 0;
        //config ss control speed servo 
        int speed_servo = 100;
        int accel_servo = 50;
        int decel_servo = 0;
        double prev_post;
        private string namePort = "";


        SerialPort serialPort = new SerialPort();
        int Post_Master = 1000;
        double curren_postion = 50;
        double currenLoaded = 50;
        public MainWindow()
        {
            InitializeComponent();
        }


        
        private void MetroWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("run programer");
            configPort();
            
            int.TryParse(num_step.Value.ToString(),out int n);
            Post_Master = n;

            //connectPort(namePort_main);
        }


        #region Xy ly port

        private string GetServoPortFromConfig()
        {
            string configPath = @"C:\BackStopper_config\config.txt";

            try
            {
                // Kiểm tra file có tồn tại không
                if (!File.Exists(configPath))
                {
                    MessageBox.Show($"Không tìm thấy file config tại: {configPath}");
                    return null;
                }

                // Đọc tất cả các dòng trong file
                string[] lines = File.ReadAllLines(configPath);

                // Tìm dòng bắt đầu bằng "Name_Servo:"
                foreach (string line in lines)
                {
                    if (line.StartsWith("Name_Servo:"))
                    {
                        // Lấy phần sau dấu :
                        //string port = line.Substring("Name_Servo:".Length);
                        //return port.Trim(); // Trim để xóa khoảng trắng


                        string port = line.Split(':')[1];
                        return port;
                    }
                }

                // Không tìm thấy dòng Name_Servo
                MessageBox.Show("Không tìm thấy cấu hình Name_Servo trong file config");
                return null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi đọc file config: {ex.Message}");
                return null;
            }
        }
        private void configPort()
        {
            //cmbPort.Items.Add(SerialPort.GetPortNames());

            // Serial config
            serialPort.BaudRate = 38400;
            serialPort.DataBits = 8;
            serialPort.Parity = Parity.None;
            serialPort.StopBits = StopBits.One;
            serialPort.Handshake = Handshake.None;
            serialPort.NewLine = "\r";
            serialPort.DataReceived += SerialPort_DataReceived;

        }
        private async void connectPort(string namePort)
        {
            try
            {
                if (!serialPort.IsOpen)
                {
                    Console.WriteLine(namePort);
                    serialPort.PortName = namePort;
                    serialPort.Open();
                    SendCommand("?96");
                    txt_status_servo.Text = "Ready";
                    btn_fab.Visibility = Visibility.Visible;
                    txt_status_servo.Foreground =
                    new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#16A34A"));

                    // Reset communication state
                    SendCommand("ECHO=0");
                    await Task.Delay(50);

                    // STOP mạnh
                    SendCommand("STOP=1");
                    await Task.Delay(50);

                    SendCommand("ABORT");
                    await Task.Delay(50);

                    SendCommand("C");
                    await Task.Delay(50);

                    // Servo ON
                    SendCommand("SVON");
                    await Task.Delay(200);

                    // Cấu hình motion chuẩn
                    SendCommand("MS=100");   // max speed
                    SendCommand("A=50");     // acceleration
                    SendCommand("D=50");     // deceleration
                    await Task.Delay(50);
                    
                    // Quan trọng: chuyển về velocity mode
                    SendCommand("V=0");
                    border_control.IsEnabled = true;
                    await Task.Delay(50);
                    Log("Mortor OK",LogType.Success);
                    Log("Start connect contect",LogType.Success);
                    ConnectContec();
                }
                else
                {
                    SendCommand("STOP=1");
                    await Task.Delay(50);
                    serialPort.Close();
                    txt_status_servo.Text = "Connect Fail!";
                    txt_status_servo.Foreground =
                    new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#DC2626"));
                    border_main.IsEnabled = false;
                    //btn_fab.Visibility = Visibility.Hidden;
                    border_control.IsEnabled = false;
                    Log("Disconnected");
                }
            }
            catch (Exception ex)
            {
                txt_status_servo.Text = "Please Connection";
                txt_status_servo.Foreground =
                    new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#DC2626"));
                border_main.IsEnabled = false;
                //btn_fab.Visibility = Visibility.Hidden;
                border_control.IsEnabled = false;
                Log("Lỗi connect port: "+ex.Message);
            }
        }

        private void Log(string msg,
                 LogType type = LogType.Info)
        {
            Brush color = Brushes.Black;

            switch (type)
            {
                case LogType.Info:
                    color =
                        (Brush)new BrushConverter()
                        .ConvertFromString("#2563EB");
                    break;

                case LogType.Warning:
                    color =
                        (Brush)new BrushConverter()
                        .ConvertFromString("#F59E0B");
                    break;

                case LogType.Error:
                    color =
                        (Brush)new BrushConverter()
                        .ConvertFromString("#DC2626");
                    break;

                case LogType.Success:
                    color =
                        (Brush)new BrushConverter()
                        .ConvertFromString("#16A34A");
                    break;
            }

            string logText =
                $"[{DateTime.Now:HH:mm:ss}] {msg}";

            Console.WriteLine(logText);

            Dispatcher.Invoke(() =>
            {
                list_log.Items.Insert(0,
                    new LogItem
                    {
                        Message = logText,
                        Color = color
                    });

                // limit log
                if (list_log.Items.Count > 200)
                {
                    list_log.Items.RemoveAt(
                        list_log.Items.Count - 1);
                }
            });
        }

        private void SendCommand(string cmd)
        {

            lock (serialLock)
            {
                if (!serialPort.IsOpen)
                    return;

                serialPort.Write(cmd + "\r\n");

                Log("SEND => " + cmd);
            }
        }

        //bien xu ly bi tach chuoi data
        private string serialBuffer = "";

        private void SerialPort_DataReceived(
    object sender,
    SerialDataReceivedEventArgs e)
        {
            try
            {
                string incoming =
                    serialPort.ReadExisting();

                serialBuffer += incoming;

                // xử lý từng packet hoàn chỉnh
                while (serialBuffer.Contains("\r"))
                {
                    int index =
                        serialBuffer.IndexOf("\r");

                    // lấy 1 line
                    string line =
                        serialBuffer
                        .Substring(0, index)
                        .Trim();

                    // remove khỏi buffer
                    serialBuffer =
                        serialBuffer
                        .Substring(index + 1);

                    // chỉ xử lý position
                    Match match =
                        Regex.Match(
                            line,
                            @"Px\.\d+=(-?\d+)");
                    // chỉ xử lý position
                    Match matchLoad =
                        Regex.Match(
                            line,
                           @"Ix\.1=(\d+)");


                    if (match.Success)
                    {
                        curren_postion =
                            int.Parse(
                                match.Groups[1].Value);
                        //if (curren_postion >= Set_origin_number)
                        //{
                        //    StopPort();
                        //}
                        Console.WriteLine("cr po: "+curren_postion);
                        Dispatcher.BeginInvoke(
                            new Action(() =>
                            {
                                Log("POSITION => " +
                            curren_postion);
                            }));
                    }
                    if (matchLoad.Success)
                    {
                        currenLoaded =
                            int.Parse(
                                matchLoad.Groups[1].Value);
                        Console.WriteLine("loaded: " + currenLoaded);
                        Dispatcher.BeginInvoke(
                            new Action(() =>
                            {
                                Log("Loaded => " +
                            currenLoaded);
                            }));
                    }
                }
            }
            catch (Exception ex)
            {
                Log("Serial Error: " +
                    ex.Message);
            }
        }
        #endregion

        #region control sensor
       
        private void StopPort()
        {
            if (!serialPort.IsOpen) return;

            SendCommand("]");   // Lệnh STOP đúng theo manual
            Log("MOTOR STOP");
            SendCommand("?96");
        }

        private async Task WaitServoStop()
        {
            double lastPos = -999999;
            while (true)
            {
                // yêu cầu position mới
                SendCommand("?96");
                //lấy tải trọng 
                SendCommand("?79");
                SendCommand("?98");

                await Task.Delay(100);

                double now = curren_postion;

                Log("CURRENT POS = " + now);

                if (now > Set_origin_number && Set_origin_number != null)
                {
                    StopPort();
                    btn_start.IsEnabled = false;
                    Log("STOP — đã đạt origin: " + Set_origin_number, LogType.Error);
                    break;
                }
                else
                {
                    btn_start.IsEnabled = true;
                }

                // position gần như đứng yên
                if (Math.Abs(now - lastPos) < 1)
                {
                    break;
                }

                lastPos = now;
               
            }
        }
        private async Task MoveToAsync(double pos,double? speedS=null)
        {
            if (!serialPort.IsOpen)
                return;

            // đang chạy thì bỏ qua
            if (isMoving)
                return;

            isMoving = true;


            try
            {
                speedS = (speedS!=null)?speedS:speed_servo;
                Log("Current = " + curren_postion);
                Log("Target = " + pos);

                // STOP
                SendCommand("]");

                await Task.Delay(150);

                // acceleration
                SendCommand("A=" + accel_servo);

                await Task.Delay(50);

                //deceleration
                SendCommand("D=" + decel_servo);

                await Task.Delay(50);

                // SPEED
                //SendCommand("S=" + Math.Abs(speed_port));
                SendCommand("S=" + speedS);

                await Task.Delay(50);

                // POSITION
                SendCommand("P=" + pos);

                await Task.Delay(50);

                // START MOVE
                SendCommand("^");

                Log($"MOVE => {pos}");

                // đợi chạy xong
                await WaitServoStop();
            }
            catch (Exception ex)
            {
                Log(ex.Message);
            }
            finally
            {
                isMoving = false;
            }
        }


        private async Task RestartPortAsync(double? i = null, bool index_min = false)
        {
            Log("isrestarting: "+isRestarting);
            if (isRestarting)
                return;

            isRestarting = true;

            try
            {
                StartBlinkServoPanel();
                Log("RESTART PORT");
                //await MoveServoAsync(tdGoc,100,100,50);
                await MoveToAsync(i==null?tdGoc:i??0.0);
                if(index_min == false)
                {
                    await MoveToAsync(Set_origin_number??lm_max,20);
                }
            }
            finally
            {
                isRestarting = false;
                StopBlinkServoPanel();
            }
        }

        private async Task NextPortAsync()
        {
            int pos =
                Convert.ToInt32(curren_postion + Post_Master);
            Log("next: " + Post_Master);
            await MoveToAsync(pos);
            //await MoveToAsync(pos - 2000);
        }

        private async Task PrevPortAsync()
        {
            int pos =
                Convert.ToInt32(curren_postion - Post_Master);

            await MoveToAsync(pos);
        }
        #endregion

        #region xu ly nhan tin hieu CONTECT DIO dio


        private void ConnectContec()
        {
            try
            {
                int ret = cdio.Init("DIO000", out m_Id);

                if (ret == (int)CdioConst.DIO_ERR_SUCCESS)
                {
                    cts = new CancellationTokenSource();
                    StartReadInput();
                    txt_ss1.Foreground = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#16A34A"));
                    Log("Contec OK", LogType.Success);
                }
                else
                {
                    // Không crash — chỉ log lỗi, app vẫn chạy bình thường
                    Log($"Contec không kết nối được (mã lỗi: 0x{ret:X}) — kiểm tra driver và device name DIO000", LogType.Warning);

                    // Disable các label sensor cho rõ
                    txt_ss1.Text = "N/A";
                    txt_ss2.Text = "N/A";
                }
            }
            catch (Exception ex)
            {
                // Bắt cả trường hợp DLL không tìm thấy
                Log($"Lỗi khởi tạo Contec: {ex.Message}", LogType.Error);
                txt_ss1.Text = "N/A";
                txt_ss2.Text = "N/A";
            }
        }

        private async void StartReadInput()
        {
            cts = new CancellationTokenSource();
            //quet lien tuc 
            ioTask = Task.Run(() => ReadInputLoop(cts.Token));
        }

        private async void ReadInputLoop(CancellationToken token)
        {

            bool isCheckStopHome = true;
            bool isCheckStopMin = false;
            bool isCheckStopMax = false;

            while (!token.IsCancellationRequested)
            {
                // pause lại
                pauseEvent.Wait();
                byte in0, in1, in2;

                //doc cong trong contect
                int ret = cdio.InpBit(m_Id, 0, out in0);
                int ret1 = cdio.InpBit(m_Id, 1, out in2);
                int ret2 = cdio.InpBit(m_Id, 2, out in1);
                
                //lay trang thai input 0
                if (ret == (int)CdioConst.DIO_ERR_SUCCESS)
                {
                    check_sst_0 = in0 == 0;
                     _ = Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (sensorOn)
                        {
                            if (!serialPort.IsOpen) return;
                            
                            if (isCheckStopMin == false)
                            {
                                BlinkLabel("OVER LIMIT !!!", txt_status_servo);
                                StopPort();
                                isCheckStopMin = true;
                                isCheckStopHome = false;
                                //xuly_chamvat(lm_max);   
                            }
                        }
                        else
                        {
                            isCheckStopMin = false;
                        }
                        //double index_origin_into_target = ((Set_origin_number??0 - curren_postion) / 500.0);

                        txt_current_position.Text = curren_postion.ToString()
                            + $"  ({((Set_origin_number - curren_postion) / 500)-5:F2}mm)";

                        //Console.WriteLine($"origin={Set_origin_number}  current={curren_postion}  diff={Set_origin_number - curren_postion}");
                        
                        txt_trongTai.Text = currenLoaded.ToString();
                        //txt_status_servo.Text = currenLoaded.ToString();

                        txt_ss1.Text = sensorOn ? "ON" : "OFF";
                        txt_ss1.Foreground = new SolidColorBrush(
                            (Color)ColorConverter.ConvertFromString(sensorOn ? "#16A34A" : "#DC2626")
                            );
                    }));
                }
                
                //lay trang thai input 1
                if (ret == (int)CdioConst.DIO_ERR_SUCCESS)
                {
                    bool sensorOn = in1 == 0;

                    _ = Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (sensorOn) 
                        { 
                            if (!serialPort.IsOpen) return;
                            if (isCheckStopMax == false)
                            { 
                                BlinkLabel("OVER LIMIT !!!", txt_status_servo);
                                isCheckStopMax = true;
                            }
                        }
                        else
                        {
                            isCheckStopMax = false;
                        }

                        txt_ss2.Text = sensorOn ? "ON" : "OFF";
                        txt_ss2.Foreground = new SolidColorBrush(
                            (Color)ColorConverter.ConvertFromString(sensorOn ? "#16A34A" : "#DC2626")
                            );
                    }));
                }
                await Task.Delay(50);

                //lay trang thai input 2
                if (ret == (int)CdioConst.DIO_ERR_SUCCESS)
                {
                    bool sensorOn = in2 == 1;
                    _ = Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (sensorOn)
                        {
                            if (!serialPort.IsOpen) return;
                            BlinkLabel("INDEX HOME", txt_status_servo);
                            if (isCheckStopHome==false)
                            {
                                //StopPort();
                                isCheckStopHome = true;
                            }
                            //xuly_chamvat();
                        }
                        else
                        {
                            //isCheckStopHome = false;
                        }

                        //txt_ss3.Text = sensorOn ? "ON" : "OFF";
                        //txt_ss3.Foreground = new SolidColorBrush(
                        //    (Color)ColorConverter.ConvertFromString(sensorOn ? "#16A34A" : "#DC2626")
                        //    );
                    }));
                }


                // xu ly input 0 1
                if (in0 == 0 || in1 == 0)
                {

                    _ = Dispatcher.BeginInvoke(new Action(() =>
                      {
                          AlarmSound.PlayAlarm();
                          BlinkLabel("OVER LIMT !!!", txt_notify);
                          _ = (in0 == 1) ?
                          btn_start.IsEnabled = false :
                          btn_Stop.IsEnabled = false;
                      }));
                }
                else
                {
                    _ = Dispatcher.BeginInvoke(new Action(() =>
                      {
                          AlarmSound.Stop();
                          btn_start.IsEnabled = true;
                          btn_Stop.IsEnabled = true;
                      }));
                }
                await Task.Delay(50);
            }
        }
          
        private void StopReadInput()
        {
            //cts.Cancel();
            //serialPort?.Close();
            //cdio.Exit(m_Id);
        }

        private async void xuly_chamvat(double indexTo)
        {
            // PAUSE realtime read
            pauseEvent.Reset();

            Console.WriteLine("Pause read realtime");
            //RestartPort();
            RestartPortAsync(indexTo);
            
            //MoveToAsync(indexTo);
            await Task.Delay(1500);

            Console.WriteLine("Resume realtime");

            // chạy lại
            pauseEvent.Set();
        }
        #endregion

        #region Event View
        //ham tao hieu ung mau nhap nhay khi servo di chuyen 
        private void StartBlinkServoPanel()
        {
            btn_fab.IsEnabled = false;
            btn_po_restart.IsEnabled = false;
            txt_po.IsEnabled = false;
            BlinkLabel("CẢNH BÁO NGUY HIỂM",txt_notify);
            ColorAnimation animation =
                new ColorAnimation
                {
                    From = Colors.Red,
                    To = Colors.White,
                    Duration = TimeSpan.FromMilliseconds(300),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever
                };

            SolidColorBrush brush =
                new SolidColorBrush(Colors.Red);

            border_control.Background = brush;
            border_main.Background = brush;
            border_employee.Background = brush;

            brush.BeginAnimation(
                SolidColorBrush.ColorProperty,
                animation);
        }

        private void StopBlinkServoPanel()
        {
            SolidColorBrush brush =
                new SolidColorBrush(Colors.White);

            border_control.Background = brush;
            border_main.Background = brush;
            border_employee.Background = brush;

            btn_fab.IsEnabled = true;
            btn_po_restart.IsEnabled = true;
            txt_po.IsEnabled = true;

            BlinkLabel("-", txt_notify);
        }


        private async void BlinkLabel(string text, TextBlock  txt)
        {
            var originalColor = txt.Foreground; // Lưu màu gốc
            var blinkColor = new SolidColorBrush(Colors.Red);
            
            for (int i = 0; i < 6; i++) // 3s với 0.5s mỗi lần = 6 lần đổi màu
            {
                // Đổi sang đỏ
                txt.Foreground = blinkColor;
                await Task.Delay(250); // Chờ 0.25s

                // Đổi về màu gốc
                txt.Foreground = originalColor;
                await Task.Delay(250); // Chờ 0.25s
            }
            txt.Text = text;
            txt.Foreground = originalColor;
        }


        double D = 0.0;
        double L = 0.0;
        double airHole = 0.0;
        private async void txt_po_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var dialogConfirm = new ConfirmDialog(
                    message: "Bạn có chắc muốn chạy lệnh này?",
                    title: "Xác nhận",
                    type: DialogType.Confirm
                    );
                dialogConfirm.Owner = this;
                dialogConfirm.ShowDialog();
                //if (MessageBox.Show("Bắt đầu chạy hàng ?", "Xác nhận thông tin", MessageBoxButton.YesNo,MessageBoxImage.Question) == MessageBoxResult.Yes )
                if (dialogConfirm.IsConfirmed)
                {
                    double startPos = curren_postion;
                    string po = txt_po.Text;
                    productData product = Sql.GetProductInfo(po);
                    if (product != null)
                    { 
                        txt_id.Text = product.Aufnr;
                        txt_PSTX.Text = product.Pstx;
                        txt_gamng.Text = product.Gamng.ToString();
                        txt_l.Text = product.C_L.ToString();
                        txt_d.Text = product.C_D.ToString();
                        txt_airhole.Text = product.airhole.ToString();
                        double airhole = product.airhole ?? 0;
                        int n = Convert.ToInt32(Set_origin_number- 2500 - (airhole * 1000 / 2.0 ));
                        Console.WriteLine("check data :"+Set_origin_number +" - "+ product.airhole+" - "+n  );

                        bool checkData = /*D == product.C_D && L == product.C_L &&*/ airHole == product.airhole;

                        if (!checkData)
                        {
                            double step_backblask = 4000;
                            double step_value = n-3;
                            StartBlinkServoPanel();
                            // if (n <= curren_postion)
                            //{
                            //Console.WriteLine("check post: " + curren_postion + " " + prev_post);
                            //if (prev_post < curren_postion)
                            //{
                            //await MoveToAsync(n - step_backblask);
                            //await MoveToAsync(step_value, 20);
                            //}
                            //else
                            //{
                            //    await MoveToAsync(n);
                            //}
                            //}
                            //else if (n > curren_postion)
                            //{
                            //Console.WriteLine("check post: " + curren_postion + " " + prev_post);
                            //if (prev_post > curren_postion)
                            //{
                            //await MoveToAsync(n + step_backblask);
                            //await MoveToAsync(step_value, 20);
                            //}
                            //else
                            //{
                            //    await MoveToAsync(n);
                            //}

                            //}
                            await MoveToAsync(n + 2000);
                            await MoveToAsync(n, 20);
                            //await MoveToAsync(n);

                            prev_post = startPos;
                            StopBlinkServoPanel();
                        }
                        else
                        {
                            Log("Data trung voi data truoc khong can chay",LogType.Success);
                        }

                        D = product.C_D ?? 0;
                        L = product.C_L ?? 0;
                        airHole = product.airhole ?? 0;
                    }
                    //khong có data
                    else
                    {
                        border_txt_po.BorderBrush =
                            new SolidColorBrush(Colors.Red);

                        border_txt_po.Background =
                            new SolidColorBrush(
                                (Color)ColorConverter.ConvertFromString("#FEF2F2"));

                        TextBoxHelper.SetWatermark(
                            txt_po,"PO NOT FOUND !!!");
                        txt_po.Clear();
                        txt_po.Focus();
                    }
                }
            }
        }

        private void txt_id_employee_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {

                string idEmployee = txt_id_employee.Text.Trim();
                EmployeeData employee = Sql.GetEmployee(idEmployee);

                if (employee != null)
                {
                    Console.WriteLine(employee.name);
                    lb_check_emplyee.Text = "OK";
                    Log("Login Success ", LogType.Success);
                    //border_main.IsEnabled = true;
                    border_control.IsEnabled = true;
                    lb_check_emplyee.Foreground =
                    new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#16A34A"));
                    namePort = GetServoPortFromConfig();
                    Task.Delay(500);
                    connectPort(namePort);

                    txt_notify.Text = "Bạn hãy set gốc trước khi thao tác !!";
                }
                else
                {
                    Log("Login Fail ", LogType.Error);
                    txt_id_employee.Clear();
                    //border_main.IsEnabled = false;
                    border_control.IsEnabled = false;
                    lb_check_emplyee.Foreground =
                    new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#DC2626"));
                    BlinkLabel("NO!", lb_check_emplyee);
                }
            }
        }

        private async void btn_start_Click(object sender, RoutedEventArgs e)
        {
            //NextPort();
            D = 0;
            L = 0;
            airHole = 0;
            await NextPortAsync();
            //txt_current_position.Text = curren_postion.ToString();
        }

        private async void btn_Stop_Click(object sender, RoutedEventArgs e)
        {
            //PrevpPort();
            D = 0;
            L = 0;
            airHole = 0;
            await PrevPortAsync();
        }

        private void btn_restart_Click(object sender, RoutedEventArgs e)
        {
            //bool checkIndex_ss = txt_ss1.Text.Contains("ON");
            //if (checkIndex_ss == true)
            //{
            //    RestartPortAsync(lm_max);
            //}
            //else
            //{
            //    RestartPortAsync(lm_min);
            //}

            list_log.Items.Clear();

            txt_po.Clear();
            txt_po.Focus();
            txt_id.Text = "";
            txt_PSTX.Text = "";
            txt_gamng.Text = "";
            txt_l.Text = "";
            txt_d.Text = "";
            txt_airhole.Text = "";
            //D = 0;
            //L = 0;
            //airHole = 0;
        }

        private async void MetroWindow_Closed(object sender, EventArgs e)
        {
            //StopPort();
            bool checkIndex_ss = txt_ss1.Text.Contains("ON");
            if (checkIndex_ss == true)
            {
                RestartPortAsync(lm_max);
            }
            else
            {
                RestartPortAsync(lm_min);
            }

            StopReadInput();
        }

        private void btn_po_restart_Click(object sender, RoutedEventArgs e)
        {
            txt_po.Clear();
            txt_po.Focus();
            txt_id.Text = "";
            txt_PSTX.Text = "";
            txt_gamng.Text = "";
            txt_l.Text = "";
            txt_d.Text = "";
            txt_airhole.Text = "";
            D = 0;
            L = 0;
            airHole = 0;
            //RestartPort();
            bool checkIndex_ss = txt_ss1.Text.Contains("ON");
            if (checkIndex_ss == true)
            {
                RestartPortAsync(Set_origin_number, true);
            }
            else
            {
                RestartPortAsync(lm_min);
            }
        }

        #endregion

        private void num_step_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double?> e)
        {
            //int.TryParse(num_step.Value.ToString(), out int n);
            //Post_Master = n;

            int step = (int)(num_step.Value ?? 1000);
            Post_Master = step;
            if (txt_step_display != null)
            {
                double sw_mm = step / 500.0;
                txt_step_display.Text = step.ToString() +" - "+ sw_mm.ToString() + "mm";
            }
        }

        private void btn_fab_Click(object sender, RoutedEventArgs e)
        {
            //popup_jog.IsOpen = !popup_jog.IsOpen;
            // Nếu popup đang mở → đóng luôn không cần hỏi mật khẩu
            if (popup_jog.IsOpen)
            {
                popup_jog.IsOpen = false;
                return;
            }

            // Popup đang đóng → hỏi mật khẩu trước
            var dialog = new PasswordDialog();
            dialog.Owner = this;
            dialog.ShowDialog();

            if (dialog.IsConfirmed)
            {
                popup_jog.IsOpen = true;
            }

        }

        private void btn_stop_main_Click(object sender, RoutedEventArgs e)
        {
            StopPort();
        }

        private void btn_set_origin_Click(object sender, RoutedEventArgs e)
        {
            Set_origin_number = Convert.ToInt32(curren_postion);
            Log("get origin: "+Set_origin_number);
            txt_origin_pos.Text = Set_origin_number.ToString();
            if (txt_origin_pos.Text != "-")
            {
                border_main.IsEnabled = true;
                btn_Stop.Background = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#F59E0B"));
                btn_restart.Background = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#FFF50B4B"));
            }
        }
    }
}
