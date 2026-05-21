using back_stopper.Database;
using back_stopper.Model;
using CdioCs;
using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
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
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace back_stopper
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {

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

        // Biến lưu trạng thái hiện tại của Output (0 = OFF, 1 = ON)
        private byte stateOut0 = 0;
        private byte stateOut1 = 0;

        //bien doi tuong realtime 
        private CancellationTokenSource cts;
        private Task ioTask;

        //toa do goc 
        int tdGoc = 0;
        //config ss control speed servo 
        int speed_servo = 100;
        int accel_servo = 50;
        int decel_servo = 100;
        

        SerialPort serialPort = new SerialPort();
        int Post_Master = 1000;
        double speed_port = 100;
        double curren_postion = 0;
        public MainWindow()
        {
            InitializeComponent();
        }
        private void MetroWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("run programer");
            configPort();
            ConnectContec();

            //connectPort(namePort_main);
        }


        #region Xy ly port
        private double postision_temp = 0;
        private void configPort()
        {
            //cmbPort.Items.Add(SerialPort.GetPortNames());

            foreach (var item in SerialPort.GetPortNames())
            {
                cmbPort.Items.Add(item);
            }

            if (cmbPort.Items.Count > 0)
                cmbPort.SelectedIndex = 0;

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

                    txt_status_servo.Foreground =
                    new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#16A34A"));
                    Log("Connected");

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
                    await Task.Delay(50);

                    border_main.IsEnabled = true;
                    txt_po.Focus();
                    Log("Motor initialized");
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
                Log("Lỗi connect port: "+ex.Message);
            }
        }

        private void Log2(string msg)
        {
            //Console.WriteLine("log: "+msg + Environment.NewLine);

            string logText =
               $"[{DateTime.Now:HH:mm:ss}] {msg}";

            Console.WriteLine(logText);

            Dispatcher.Invoke(() =>
            {
                list_log.Items.Insert(0, logText);

                        // giới hạn 200 log
                        if (list_log.Items.Count > 200)
                {
                    list_log.Items.RemoveAt(
                        list_log.Items.Count - 1);
                }
            });
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
            //if (serialPort.IsOpen)
            //{
            //    serialPort.Write(cmd + "\r\n");
            //}

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
        //chay khi SenCommand duoc goi 
        //    private void SerialPort_DataReceived(
        //object sender,
        //SerialDataReceivedEventArgs e)
        //    {
        //        try
        //        {
        //            string data = serialPort.ReadExisting() ?? "";

        //            Dispatcher.Invoke(() =>
        //            {
        //                //Log("RX: " + data +" end ");
        //                Log("status: " + data + " end ");

        //                ParsePosition(data);
        //            });
        //        }
        //        catch(Exception ex)
        //        {
        //            Log("loi ket noi: "+ex.Message);
        //        }
        //    }

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

                    if (match.Success)
                    {
                        curren_postion =
                            int.Parse(
                                match.Groups[1].Value);
                        Console.WriteLine("cr po: "+curren_postion);
                        Dispatcher.BeginInvoke(
                            new Action(() =>
                            {
                                Log("POSITION => " +
                            curren_postion);
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
        private void ParsePosition(string data)
        {
            Match match = Regex.Match(data, @"Px\.\d+=(-?\d+)");
            if (match.Success)
            {
                curren_postion = int.Parse(match.Groups[1].Value);
            }
        }
       
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

                await Task.Delay(100);

                double now = curren_postion;

                Log("CURRENT POS = " + now);

                // position gần như đứng yên
                if (Math.Abs(now - lastPos) < 1)
                {
                    break;
                }

                lastPos = now;
            }
        }
        private async Task MoveToAsync(double pos)
        {
            if (!serialPort.IsOpen)
                return;

            // đang chạy thì bỏ qua
            if (isMoving)
                return;

            isMoving = true;

            try
            {
                Log("Current = " + curren_postion);
                Log("Target = " + pos);

                // STOP
                SendCommand("]");

                await Task.Delay(150);

                // acceleration
                SendCommand("A=" + accel_servo);

                await Task.Delay(50);

                // deceleration
                SendCommand("D=" + decel_servo);

                await Task.Delay(50);

                // SPEED
                //SendCommand("S=" + Math.Abs(speed_port));
                SendCommand("S=" + speed_servo);

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


        private async Task RestartPortAsync(double? i = null)
        {
            Log("isrestarting: "+isRestarting);
            if (isRestarting)
                return;

            isRestarting = true;

            try
            {
                Log("RESTART PORT");
                //await MoveServoAsync(tdGoc,100,100,50);
                await MoveToAsync(i==null?tdGoc:i??0.0);
            }
            finally
            {
                isRestarting = false;
            }
        }

        private async Task NextPortAsync()
        {
            int pos =
                Convert.ToInt32(curren_postion + Post_Master) + 2000;
            await MoveToAsync(pos);
            await MoveToAsync(pos - 2000);

            //int pos =
            //   Convert.ToInt32(curren_postion + 1);
            //await MoveToAsync(pos);

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
            // Mở kết nối với thiết bị "DIO000"
            int ret = cdio.Init("DIO000", out m_Id);

            if (ret == (int)CdioConst.DIO_ERR_SUCCESS)
            {
                cts = new CancellationTokenSource();
               StartReadInput();
                txt_ss1.Foreground  = 
                new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#16A34A"));
            }
            else
            {
                Log("loi kết nối Contec");
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

            bool isCheckStopHome = false;
            bool isCheckStopMin = false;
            bool isCheckStopMax = false;

            while (!token.IsCancellationRequested)
            {
                // pause lại
                pauseEvent.Wait();
                byte in0, in1, in2;

                //Console.WriteLine("ss postion : "+ss_pos);

                //doc cong trong contect
                int ret = cdio.InpBit(m_Id, 0, out in0);
                int ret1 = cdio.InpBit(m_Id, 1, out in2);
                int ret2 = cdio.InpBit(m_Id, 2, out in1);


                //lay trang thai input 0
                if (ret == (int)CdioConst.DIO_ERR_SUCCESS)
                {
                    bool sensorOn = in0 == 0;

                     _ = Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (sensorOn)
                        {
                            btn_start.IsEnabled = false;
                            btn_Stop.IsEnabled = false;
                            if (!serialPort.IsOpen) return;
                            if (isCheckStopMin == false)
                            {
                                AlarmSound.PlayAlarm();
                                BlinkLabel("OVER LIMIT !!!", txt_status_servo);
                                StopPort();
                                isCheckStopMin = true;
                                isCheckStopHome = false;
                                //xuly_chamvat(lm_max);   
                            }
                        }
                        else
                        {
                            btn_start.IsEnabled = true;
                            btn_Stop.IsEnabled = true;
                            isCheckStopMin = false;
                            AlarmSound.Stop();
                        }
                        
                        txt_ss1.Text = sensorOn ? "ON" : "OFF";
                        txt_ss1.Foreground = new SolidColorBrush(
                            (Color)ColorConverter.ConvertFromString(sensorOn ? "#16A34A" : "#DC2626")
                            );

                       

                        txt_current_position.Text = curren_postion.ToString();

                        btn_start.IsEnabled = sensorOn ? false : true;
                        btn_Stop.IsEnabled = sensorOn ? false : true;
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
                            btn_start.IsEnabled = false;
                            btn_Stop.IsEnabled = false;
                            if (!serialPort.IsOpen) return;
                            if (isCheckStopMax == false)
                            {
                                AlarmSound.PlayAlarmMax();
                                BlinkLabel("OVER LIMIT !!!", txt_status_servo);
                                StopPort();
                                //xuly_chamvat(-99999);

                                isCheckStopMax = true;
                            }
                        }
                        else
                        {
                            btn_start.IsEnabled = true;
                            btn_Stop.IsEnabled = true;
                            isCheckStopMax = false;
                            AlarmSound.StopMax();
                        }

                        txt_ss2.Text = sensorOn ? "ON" : "OFF";
                        txt_ss2.Foreground = new SolidColorBrush(
                            (Color)ColorConverter.ConvertFromString(sensorOn ? "#16A34A" : "#DC2626")
                            );

                        txt_current_position.Text = curren_postion.ToString();

                        btn_start.IsEnabled = sensorOn ? false : true;
                        btn_Stop.IsEnabled = sensorOn ? false : true;
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
                                StopPort();
                                isCheckStopHome = true;
                            }
                            //xuly_chamvat();
                        }
                        else
                        {
                            //isCheckStopHome = false;
                        }

                        txt_ss3.Text = sensorOn ? "ON" : "OFF";
                        txt_ss3.Foreground = new SolidColorBrush(
                            (Color)ColorConverter.ConvertFromString(sensorOn ? "#16A34A" : "#DC2626")
                            );

                        
                        txt_target_position.Text = isCheckStopHome.ToString();
                        btn_start.IsEnabled = true;
                        btn_Stop.IsEnabled = true;
                    }));
                }
                await Task.Delay(50);
            }
        }
          
        private void StopReadInput()
        {
            cts.Cancel();
            //serialPort?.Close();
            cdio.Exit(m_Id);
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

                txt.Text = text;
            }
        }

        private async void txt_po_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string po = txt_po.Text;
                productData product = Sql.GetProductInfo(po);
                if (product != null)
                {
                    double D = 0.0;
                    double L = 0.0;
                    double airHole = 0.0;

                    txt_id.Text = product.Aufnr;
                    txt_PSTX.Text = product.Pstx;
                    txt_gamng.Text = product.Gamng.ToString();
                    txt_l.Text = product.C_L.ToString();
                    txt_d.Text = product.C_D.ToString();
                    txt_airhole.Text = product.airhole.ToString();
                     int.TryParse((((product.airhole ?? 0) + (product.C_D ?? 0) * (product.C_L ?? 0)) * 10).ToString(),out int n);
                    //txt_target_position.Text = n.ToString();

                    bool checkData = D == product.C_D && L == product.C_L && airHole == product.airhole;

                    if (!checkData)
                    {
                        await MoveToAsync(n + 10000);
                    }
                    else
                    {
                        Log("Data trung voi data truoc khong can chay");
                    }

                    D = product.C_D??0;
                    L = product.C_L??0;
                    airHole = product.airhole??0;


                }
            }
        }

        private void txt_id_employee_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string idEmployee = txt_id_employee.Text.Trim();
                EmployeeData employee = Sql.GetEmployee(idEmployee);

                if (employee!=null)
                {
                    Console.WriteLine(employee.name);
                    lb_check_emplyee.Text = "OK";
                    Log("Login Success ", LogType.Success);
                    //border_main.IsEnabled = true;
                    border_control.IsEnabled = true;
                    txt_id_employee.IsEnabled = false;
                    //txt_po.Focus();
                    lb_check_emplyee.Foreground =
                    new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#16A34A"));
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

        private void cmbPort_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Task.Delay(500);
            int index = cmbPort.SelectedIndex;
            connectPort(cmbPort.Items.GetItemAt(index).ToString());
        }

        private async void btn_start_Click(object sender, RoutedEventArgs e)
        {
            //NextPort();
            await NextPortAsync();
            txt_current_position.Text = curren_postion.ToString();
        }

        private async void btn_Stop_Click(object sender, RoutedEventArgs e)
        {
            //PrevpPort();
             await PrevPortAsync();
        }

        private void btn_restart_Click(object sender, RoutedEventArgs e)
        {
            //RestartPort();
            //cho nop

            //bool checkIndex_ss = txt_ss1.Text.Contains("ON");
            //if (checkIndex_ss ==true)
            //{
            //    RestartPortAsync(lm_max);
            //}
            //else
            //{
                RestartPortAsync(2000);
            //}
        }

        private void MetroWindow_Closed(object sender, EventArgs e)
        {
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
            //RestartPort();
            bool checkIndex_ss = txt_ss1.Text.Contains("ON");
            if (checkIndex_ss == true)
            {
                RestartPortAsync(lm_max);
            }
            else
            {
                RestartPortAsync(lm_min);
            }
        }

        #endregion

    }
}
