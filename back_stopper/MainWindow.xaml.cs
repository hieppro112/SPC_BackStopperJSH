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
using System.Diagnostics;

namespace back_stopper
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        //doi tuong man hinh ban phim 
        private NumericKeyboard _numericKeyboard;
        //doi nhan vien 
        EmployeeData employee;
        //status drill và nút êmrgy
        private bool checkon_emg = false;
        private bool checkon_taynam = false;
        //value stopper den origin 
        private double value_stopper;
        //man hinh nhan 2 tay 
        private RunningDialog _runningDialog;
        // Dialog restảrt
        private RestartingDialog _restartingDialog;
        //type blink 
        private readonly object _blinkLock = new object();
        int outRelay = 0;
        byte stateRelay = 0;
        byte status_touch = 0;
        bool startBlink = true;
        //gia tri goc moi khi set 
        public int? Set_origin_number = null;
        //gioi han khong cho servo chay ra khoi 
        int lm_min = -50000;
        int lm_max = 41000;
        //bien dung lock khong cho cmd chay
        private readonly object serialLock = new object();

        private volatile bool isMoving = false;
        private volatile bool isRestarting = false;

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
        int speed_servo = 85;
        int accel_servo = 50;
        int decel_servo = 50;
        double prev_post;
        private string namePort = "";

        bool check_pos_for_relay = false;

        double pos_target = -99999999;
        SerialPort serialPort = new SerialPort();
        int Post_Master = 1000;
        double curren_postion = 50;
        double currenLoaded = 50;

        //gia tri stopper trong conffig
        double n_stopper;

        //bien luu trang thai tay cam 
        private byte temp_taycam;
        public MainWindow()
        {
            InitializeComponent();
        }

        //info config 
        private string nameContect = "";
        private int times_restart = 0;
        private void MetroWindow_Loaded(object sender, RoutedEventArgs e)
        {
            configPort();
            
            int.TryParse(num_step.Value.ToString(),out int n);
            Post_Master = n;
            string s =  GetServoPortFromConfig("origin");
            if (!string.IsNullOrWhiteSpace(s))
            {
                int.TryParse(s, out int num);
                Set_origin_number = num;
                txt_origin_pos.Text = num.ToString();
            }
            DisnableScreen();

            ConnectContec();
            connectPort(namePort);
            StartReadInput();
            string s_stopper = GetServoPortFromConfig("size_backstopper");
            double.TryParse(s_stopper, out n_stopper);

        }


        #region Xy ly port

        //set gia tri config
        private void SaveOrigin(int originValue, string row_content)
        {
            string path = @"C:\BackStopper_config\config.txt";

            // đọc toàn bộ line
            var lines = File.ReadAllLines(path).ToList();

            for (int i = 0; i < lines.Count; i++)
            {
                // tìm dòng origin:
                if (lines[i].StartsWith(row_content))
                {
                    lines[i] = $"{row_content}:{originValue}";
                }
            }

            // ghi đè lại file
            File.WriteAllLines(path, lines);
        }
        //lay gia tri config
        private string GetServoPortFromConfig(String s_search)
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
                    if (line.StartsWith($"{s_search}:"))
                    {

                        string port = line.Split(':')[1];
                        return port;
                    }
                }
                // Không tìm thấy dòng Name_Servo
                MessageBox.Show($"Không tìm thấy cấu hình {s_search} trong file config");
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
                    namePort = GetServoPortFromConfig("Name_Servo");
                    serialPort.PortName = namePort;
                    serialPort.Open();
                    SendCommand("?96");
                    txt_status_servo.Text = "Sẵn sàng";
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

                    turnOff_blink_statusServo();
                    //ConnectContec();
                }
                else
                {
                    SendCommand("STOP=1");
                    await Task.Delay(50);
                    serialPort.Close();
                    txt_status_servo.Text = "Connect Thất Bại!";
                    txt_status_servo.Foreground =
                    new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#DC2626"));
                    DisnableScreen();
                    Log("Disconnected");

                    if (isBlink)
                    {
                        turnOn_blink_statusServo();
                    }
                }
            }
            catch (Exception ex)
            {
                    txt_status_servo.Text = "Vui lòng, Kết nối servo";

                txt_status_servo.Foreground =
                    new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#DC2626"));
                DisnableScreen();
                //btn_fab.Visibility = Visibility.Hidden;
                border_control.IsEnabled = false;
                Log("Lỗi connect port: "+ex.Message);
              
                temp_taycam = 1;

                if (isBlink)
                {
                    turnOn_blink_statusServo();
                }

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
        }

        private void SendCommand(string cmd)
        {
            try
            {

                lock (serialLock)
                {
                    if (!serialPort.IsOpen)
                        return;

                    serialPort.Write(cmd + "\r\n");
                }
            }
            catch (Exception ex)
            {
                _ = new ToastNotification("Lỗi Exception tại nơi nhân dữ liệu của servo { SendCommand }: "+ex.Message, LogType.Info)
                                 .ShowAndAutoClose();
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
                        Console.WriteLine("cr po: "+curren_postion);
                    }
                    if (matchLoad.Success)
                    {
                        currenLoaded =
                            int.Parse(
                                matchLoad.Groups[1].Value);
                        Console.WriteLine("loaded: " + currenLoaded);
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


                if (now-5 > Set_origin_number && Set_origin_number != null)
                {
                    //StopPort();
                    //btn_start.IsEnabled = false;
                    Log("STOP — đã đạt origin: " + Set_origin_number, LogType.Error);
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
                Log("Target = " + pos);

                // STOP
                SendCommand("]");

                await Task.Delay(150);

                // acceleration
                SendCommand("A=" + accel_servo);

                await Task.Delay(50);

                //deceleration
                //SendCommand("D=" + decel_servo);

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
                //kiem tra dung vi tri chay chua 
                check_pos_for_relay = (Math.Abs(pos_target - curren_postion) <= 505) && (Math.Abs(pos_target - curren_postion) >= 0);
                Console.WriteLine("pos: " + pos_target + "curr pos " + curren_postion + "result: "+check_pos_for_relay);

                if (check_pos_for_relay)
                {
                    _lightState = LightState.On;
                }
                else
                {
                    _lightState = LightState.Off;

                }
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
                StartBlinkServoPanel(LogType.Warning);
                //await MoveServoAsync(tdGoc,100,100,50);
                //khi trong min  = ON 
                if (index_min)
                {
                    await MoveToAsync(i == null ? tdGoc : i ?? 0.0);
                }
                //khi khong dang trong min 
                if(index_min == false)
                {
                    await MoveToAsync(curren_postion -2000, 40);
                    await MoveToAsync(Set_origin_number  ??lm_max,40);
                }
            }
            finally
            {
                isRestarting = false;
                //StopBlinkLabel("-",txt_notify);
                StopBlinkServoPanel();
            }
        }

        private async Task NextPortAsync()
        {
            int pos =
                Convert.ToInt32(curren_postion + Post_Master);
            check_pos_for_relay = pos == pos_target;
           
            await MoveToAsync(pos);
            //await MoveToAsync(pos - 2000);
        }

        private async Task PrevPortAsync()
        {
            int pos =
                Convert.ToInt32(curren_postion - Post_Master);
            check_pos_for_relay = pos == pos_target;
            await MoveToAsync(pos);
        }
        #endregion

        #region xu ly nhan tin hieu CONTECT DIO dio
        private void ConnectContec()
        {
            try
            {
                nameContect = GetServoPortFromConfig("Name_Contect");
                int ret = cdio.Init($"{nameContect}", out m_Id);
                if (ret == (int)CdioConst.DIO_ERR_SUCCESS)
                {
                    cts = new CancellationTokenSource();
                    txt_ss1.Foreground = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#16A34A"));
                    Log("Contec OK", LogType.Success);
                    //lay speed servo config
                    int.TryParse(GetServoPortFromConfig("speed_servo"), out int sp_sv);
                    speed_servo = sp_sv;

                    cdio.OutBit(m_Id,4,1);
                }
                else
                {
                    // Không crash — chỉ log lỗi, app vẫn chạy bình thường
                    Log($"Contec không kết nối được (mã lỗi: {ret}) — kiểm tra driver và device name DIO000", LogType.Warning);

                    // Disable các label sensor cho rõ
                    txt_ss1.Text = "N/A";
                    txt_ss2.Text = "N/A";
                    DisnableScreen();

                }
            }
            catch (Exception ex)
            {
                // Bắt cả trường hợp DLL không tìm thấy
                Log($"Lỗi khởi tạo Contec: {ex.Message}", LogType.Error);
                txt_ss1.Text = "N/A";
                txt_ss2.Text = "N/A";
                DisnableScreen();
            }
        }

        private async void StartReadInput()
        {
            cts = new CancellationTokenSource();
            //quet lien tuc 
            ioTask =  readinputloop(cts.Token);
        }


        private async Task readinputloop(CancellationToken token)
        {
            bool ischeckstopmin = false;
            bool ischeckstopmax = false;
            bool ischeckstophome = true;


            while (!token.IsCancellationRequested)
            {
                // =========================
                // Enable screen
                // =========================
                int ret = cdio.Init($"{nameContect}", out m_Id);
                

                // =========================
                // Đọc trạng thái servo realtime
                // =========================
                if (!serialPort.IsOpen)
                {
                    _ = Dispatcher.BeginInvoke(new Action(async () =>
                           {
                               StopPort();
                           }));
                    txt_status_servo.Text = "Servo bị ngắt kết nối !!!";
                    SolidColorBrush brush =
                new SolidColorBrush(Colors.Red);
                    txt_status_servo.Foreground = brush;
                    connectPort(namePort);

                    if (isBlink)
                    {
                        turnOn_blink_statusServo();
                    }
                }

                if (!(ret == (int)CdioConst.DIO_ERR_SUCCESS))
                {
                    ConnectContec();
                }

               
                pauseEvent.Wait();

                // =========================
                // read input
                // =========================
                cdio.InpBit(m_Id, 2, out byte inmin);
                cdio.InpBit(m_Id, 0, out byte inmax);
                cdio.InpBit(m_Id, 4, out byte in_taynam);
                cdio.InpBit(m_Id, 3, out byte in_drill_water);
                cdio.InpBit (m_Id, 5, out byte inemg);
                cdio.InpBit (m_Id, 6, out byte inTouch);

                //temp 
                
                temp_taycam = in_taynam;

                cdio.EchoBackByte(m_Id, 0, out byte state);
                stateRelay = state;


                xuly_light_touch();

                //cdio.OutBit(m_Id, 2, 0);
                //cdio.OutBit(m_Id, 3, 0);
                //cdio.OutBit(m_Id, 4, 1);
                //cdio.OutBit(m_Id, 5, 0);
                byte value;
                cdio.EchoBackBit(m_Id, 2, out value);
                status_touch = inTouch;
                //Console.WriteLine(value);

                // =========================
                // relay
                // =========================
                outRelay = (inemg == 0 && check_pos_for_relay)
                    ? 1
                    : 0;
                //console.writeline("out relay: "+outrelay );

                //check relay hoat dong nhun khoan va nuuoc off
                

                Event_relay();
                handle_Relay();

                // =========================
                // stt drill, water
                // =========================
                handDle_status_drill_water(in_drill_water);


                // =========================
                // xu ly den tin hieu 
                // =========================

                EventLight(LogType.Error, inemg, in_taynam, in_drill_water);


                // =========================
                // sx logic uu tien 
                // =========================
                //Console.WriteLine("check inEMG = "+checkon_emg);
                _ = XylyCambien(inemg,in_taynam,ret);

                
                await Dispatcher.InvokeAsync(() =>
                {
                    HandleSensorMin(
                        inmin,
                        ref ischeckstopmin,
                        ref ischeckstophome);

                    HandleSensorMax(
                        inmax,
                        ref ischeckstopmax);

                    HandleSensorHome(
                        in_taynam,
                        ref ischeckstophome);

                    HandleAlarm(
                        inmin,
                        inmax,
                        state);

                });

                await Task.Delay(50);
            }
        }

        private async Task eventCancel(string toast, LogType type)
        {
            DisnableScreen();
            _ =  Dispatcher.InvokeAsync(() =>
            {
                // Trigger y hệt như nhấn nút Hủy trong RunningDialog
                if (!check_pos_for_relay)
                {
                    _moveCts?.Cancel();
                    _lightState = LightState.Off;
                }
                StopBlinkServoPanel();
                StopPort();

               

                // Đóng dialog nếu đang mở
                if (_runningDialog != null)
                {
                    _runningDialog.OnCancelled = null; // tránh double trigger
                    _runningDialog.Close();
                    _runningDialog = null;
                }

                // Clear data
                //txt_PSTX.Text = "";
                //txt_gamng.Text = "";
                //txt_l.Text = "";
                //txt_d.Text = "";
                //txt_airhole.Text = "";
                airHole = 0;
                L = 0;
                D = 0;
                //txt_po.Clear();
                txt_po.Focus();

                Log("EMERGENCY! Hành trình bị dừng khẩn cấp", LogType.Error);

                switch (type)
                {
                    case LogType.Error:
                        _ = new ToastNotification(toast, LogType.Error)
                        .ShowAndAutoClose(3000);
                        break;
                    case LogType.Warning:
                        _ = new ToastNotification(toast, LogType.Warning)
                        .ShowAndAutoClose(3000);
                        break;
                    default:
                        _ = new ToastNotification(toast, LogType.Error)
                        .ShowAndAutoClose(3000);
                        break;
                }
            });
        }

        private async Task XylyCambien(byte inemg, byte in_taynam, int stt_ret)
        {
            // ==================================================
            // priority 1 : emergency
            // ==================================================


            int ret = cdio.Init($"{nameContect}", out m_Id);
            if (inemg == 1)
            {
                StopPort();
                if (!checkon_emg)
                {
                    checkon_taynam = false;
                    checkon_emg = true;

                    await Dispatcher.InvokeAsync(() =>
                    {
                        Event_relay(false);//dung relay
                        StopBlinkServoBorder();//stop blink cua thang khoan neu co 
                        StartBlinkServoBorder(
                            LogType.Warning,
                            1);

                        StartBlinkLabel(
                            "DỪNG KHẨN CẤP",
                            txt_notify);
                        _ = eventCancel("⚠ EMERGENCY! Hành trình đã bị dừng", LogType.Error);

                    });
                }

                await Task.Delay(50);
                //continue;

            }

            // ==================================================
            // priority 2 : tay gạt
            // ==================================================
            else
            {
                if (temp_taycam ==0 && ret == (int)CdioConst.DIO_ERR_SUCCESS)
                {
                    StopPort();
                    if (!checkon_taynam)
                    {
                        checkon_emg = false;
                        checkon_taynam = true;
                        await Dispatcher.InvokeAsync(() =>
                        {
                            StopBlinkServoBorder(1);//tat luoon cua nut dung khan cap neu co blink 

                            StartBlinkServoBorder(
                                LogType.Warning);

                            StartBlinkLabel(
                                "ĐÃ KÉO KHOAN",
                                txt_notify);
                            DisnableScreen();

                            _ = eventCancel("⚠ Khoan đã kéo! Hành trình đã bị dừng", LogType.Warning);
                        });
                        await Task.Delay(50);
                        //continue;
                    }
                }

                else
                {
                    checkon_taynam = false;
                    checkon_emg = false;
                    txt_notify.Text = "-";
                    StopBlinkLabel();
                    StopBlinkServoBorder();
                    StopBlinkServoBorder(1);
                    StopBlinkServoPanel();
                    if (employee != null && (ret == (int)CdioConst.DIO_ERR_SUCCESS) && serialPort.IsOpen)
                    {
                        EnableScreen();
                    }
                }
            }

        }
        private async Task<int> GetServoStatus()
        {
            serialPort.WriteLine("?99");

            await Task.Delay(50);

            string response = serialPort.ReadLine();

            Match match =
                Regex.Match(response, @"Ux\.1=(\d+)");

            if (match.Success)
            {
                return int.Parse(match.Groups[1].Value);
            }

            return 0;
        }

        private void HandleSensorMin(byte raw, ref bool isCheckStopMin, ref bool isCheckStopHome)
        {
            
            bool on = raw == 0;
            if (on)
            {
               
                if (!serialPort.IsOpen) return;
                if (!isCheckStopMin)
                {
                    StopPort();
                    isCheckStopMin = true;
                    isCheckStopHome = false;
                }
            }
            else
            {
                isCheckStopMin = false;
            }

            txt_current_position.Text = curren_postion.ToString()
                + $"  ({((Set_origin_number - curren_postion) / 500) - value_stopper:F2}mm)";
            //txt_trongTai.Text = currenLoaded.ToString();
            txt_ss1.Text = on ? "ON" : "OFF";
            txt_ss1.Foreground = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(on ? "#16A34A" : "#DC2626"));
        }


        private void HandleSensorMax(byte raw, ref bool isCheckStopMax)
        {
            bool on = raw == 0;
            if (on)
            {
                if (!serialPort.IsOpen) return;
                if (!isCheckStopMax)
                {
                    StopPort();
                    isCheckStopMax = true;
                }
            }
            else
            {
                isCheckStopMax = false;
            }

            txt_ss2.Text = on ? "ON" : "OFF";
            txt_ss2.Foreground = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(on ? "#16A34A" : "#DC2626"));
        }

        private void HandleSensorHome(byte raw, ref bool isCheckStopHome)
        {
        }

        private void HandleAlarm(byte in0, byte in1, byte outRepaly)
        {
            if (in0 == 0 || in1 == 0)
            {
                AlarmSound.PlayAlarm();
                //BlinkLabel("OVER LIMIT !!!", txt_notify);
                _ = (in0 == 1) ?
                          btn_start.IsEnabled = false :
                          btn_Stop.IsEnabled = false;
            }
            else
            {
                AlarmSound.Stop();
                btn_start.IsEnabled = true;
                btn_Stop.IsEnabled = true;
            }
        
        }

        private void handDle_status_drill_water(byte status)
        {
            if (status == 1)
            {
                txt_status_drill.Text = "ON";
                txt_status_drill.Foreground = new SolidColorBrush(
                    Colors.Green);

                txt_status_water.Text = "ON";
                txt_status_water.Foreground = new SolidColorBrush(
                    Colors.Green);
            }
            else
            {
                txt_status_drill.Text = "OFF";
                txt_status_drill.Foreground = new SolidColorBrush(
                    Colors.Red);

                txt_status_water.Text = "OFF";
                txt_status_water.Foreground = new SolidColorBrush(
                    Colors.Red);
            }
        }

        private void handle_Relay()
        {
            if (outRelay == 0)
            {
                txt_out_relay.Text = "OFF";
                txt_out_relay.Foreground = new SolidColorBrush(
                    Colors.Red);
            }
            else
            {
                txt_out_relay.Text = "ON";
                txt_out_relay.Foreground = new SolidColorBrush(
                    Colors.Green);
            }
        }

        private void StopReadInput()
        {
            //cts.Cancel();
            //tat den 
            cdio.OutBit(m_Id, 3, 0);
            cdio.OutBit(m_Id, 4, 0);
            cdio.OutBit(m_Id, 5, 0);

            //tat relay
            cdio.OutBit(m_Id, 0, 0);

            //tat den cua nut 
            cdio.OutBit(m_Id, 2, 0);
        }
        #endregion

        #region Event View
        //thay doi mau cho panel 
        private void StartBlinkServoPanel(LogType type)
        {
            btn_fab.IsEnabled = false;
            btn_po_restart.IsEnabled = false;
            txt_po.IsEnabled = false;
            //BlinkLabel("CẢNH BÁO NGUY HIỂM", txt_notify);

            Color color;
            switch (type)
            {
                case LogType.Error:
                    color = Colors.Red;
                    break;
                case LogType.Warning:
                    color = Colors.Yellow;
                    break;
                case LogType.Info:
                    color = Colors.Blue;
                    break;
                default:
                    color = Colors.White;
                    break;
            }

            ColorAnimation animation =
                new ColorAnimation
                {
                    From = color,
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
        }


        //thay doi mau cho panel con
        private void StartBlinkServoBorder(LogType type,int style = 0)
        {
            Color color;
            switch (type)
            {
                case LogType.Error:
                    color = Colors.Red;
                    break;
                case LogType.Warning:
                    color = Colors.Yellow;
                    break;
                case LogType.Info:
                    color = Colors.Blue;
                    break;
                default:
                    color = Colors.White;
                    break;
            }

            ColorAnimation animation =
                new ColorAnimation
                {
                    From = color,
                    To = Colors.White,
                    Duration = TimeSpan.FromMilliseconds(300),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever
                };

            SolidColorBrush brush =
                new SolidColorBrush(Colors.Yellow);

            if (style == 0)
            {
                border_water.Background = brush;
                border_drill.Background = brush;
            }
            if(style == 1)
            {
                btn_stop_main.Background = brush;
                btn_stop_main.Foreground = new SolidColorBrush(Colors.Red);
            }

            brush.BeginAnimation(
                SolidColorBrush.ColorProperty,
                animation);
        }

        private void StopBlinkServoBorder(int style = 0)
        {
            SolidColorBrush brush_water = new SolidColorBrush(
    (Color)ColorConverter.ConvertFromString("#FFAFD7FF"));

            SolidColorBrush brush_drill = new SolidColorBrush(
    (Color)ColorConverter.ConvertFromString("#FF8E949B")
);

            if (style == 0)
            {
                border_water.Background = brush_water;
                border_drill.Background = brush_drill;
            }
            else if (style == 1)
            {
                
                btn_stop_main.Background = new SolidColorBrush(Colors.Red);
                btn_stop_main.Foreground = new SolidColorBrush(Colors.White);
            }
        }

        private void StartBlinkLabel(string text, TextBlock txt)
        {
            txt.Text = text;
        }

        private void StopBlinkLabel()
        {
            txt_notify.Text = "  ";
        }

        double D = 0.0;
        double L = 0.0;
        double airHole = 0.0;


        private async Task MoveToWithSignalCheckAsync(double target, int speed = 0,
    CancellationToken cancellationToken = default)
        {
            while (true)
            {
                // Chờ signal = 1 → đèn NHÁY
                while (status_touch != 1)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _lightState = LightState.Off;
                        StopPort();
                        return;
                    }
                    _lightState = LightState.Blinking; // ← đợi nhấn → nháy
                    StopPort();
                    _runningDialog?.SetPaused();
                    await Task.Delay(100);
                }

                isMoving = false;
                _lightState = LightState.Off; // ← đang chạy → tắt đèn
                _runningDialog?.SetRunning();

                Task moveTask = (speed == 0)
                    ? MoveToAsync(target)
                    : MoveToAsync(target, speed);

                while (!moveTask.IsCompleted)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _lightState = LightState.Off;
                        StopPort();
                        await moveTask;
                        return;
                    }
                    if (status_touch != 1)
                    {
                        _lightState = LightState.Blinking; // ← thả tay → nháy lại
                        StopPort();
                        await Task.Delay(200);
                        break;
                    }
                    await Task.Delay(50);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    _lightState = LightState.Off;
                    return;
                }

                if (Math.Abs(curren_postion - target) <= 10)
                {
                    _lightState = LightState.On; //  hoàn thành -> sáng
                    return;
                }
            }
        }
        
        private CancellationTokenSource _moveCts; 
        private async void txt_po_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (txt_po.Text.Length == 12)
                {
                    double startPos = curren_postion;
                    string po = txt_po.Text.Trim();
                    productData product = Sql.GetProductInfo(po);

                    if (product != null)
                    {
                        double ah_num = product.airhole ?? 0.0;

                        value_stopper = n_stopper;
                        border_txt_po.Background = new SolidColorBrush(
                            (Color)ColorConverter.ConvertFromString("#FFE4E4E4"));

                        if (ah_num != 0)
                        {
                            txt_PSTX.Text = product.Pstx;
                            txt_gamng.Text = product.Gamng.ToString() + " Cái";
                            txt_l.Text = product.C_L.ToString() + " mm";
                            txt_d.Text = product.C_D.ToString() + " mm";
                            txt_airhole.Text = ah_num.ToString() + " mm";
                            //Console.WriteLine($"AH low: {product.airhole_to_low} - AH up: {product.airhole_to_up}");

                            double airhole = product.airhole ?? 0;
                            int n = Convert.ToInt32(
                                Set_origin_number - (n_stopper * 500) - (ah_num * 500));
                            //Console.WriteLine("number origin: "+Set_origin_number);


                            bool checkData = airHole == product.airhole && L == product.C_L;

                            if (!checkData)
                            {
                                //luu lai so lan quet PO 
                                times_restart += 1;
                                SaveOrigin(times_restart, "times_restart_home");

                                check_pos_for_relay = false;

                                _ = Dispatcher.BeginInvoke(new Action(async () =>
                                {
                                    if (isMoving) return;

                                    // Kill task cũ nếu còn sống ngầm
                                    _moveCts?.Cancel();
                                    _moveCts = new CancellationTokenSource();
                                    var token = _moveCts.Token;

                                    // Mở dialog
                                    _runningDialog?.Close();
                                    _runningDialog = new RunningDialog(po);
                                    _runningDialog.Owner = this;

                                    // Gán callback hủy
                                    _runningDialog.OnCancelled = () =>
                                    {
                                        _moveCts?.Cancel();
                                        StopPort();
                                        StopBlinkServoPanel();
                                        _lightState = LightState.Off;

                                        Dispatcher.InvokeAsync(() =>
                                        {
                                            txt_PSTX.Text = "";
                                            txt_gamng.Text = "";
                                            txt_l.Text = "";
                                            txt_d.Text = "";
                                            txt_airhole.Text = "";
                                            airHole = 0;
                                            L = 0;
                                            D = 0;
                                            txt_po.Clear();
                                            txt_po.Focus();
                                            Log("Hành trình đã bị hủy — vui lòng quét PO mới", LogType.Warning);
                                        });
                                    };

                                    _runningDialog.Show();
                                    StartBlinkServoPanel(LogType.Warning);

                                    double step_backblask = 2000 ;
                                    //double step_value = n +  (((product.airhole_to_low ?? 0) + (product.airhole_to_up ?? 0))/2) ;
                                    double step_value = n - 300;
                                    pos_target = step_value;
                                    if (n >= curren_postion)
                                    {
                                        await MoveToWithSignalCheckAsync(step_value, 0, token);
                                    }
                                    else if (n < curren_postion)
                                    {
                                        await MoveToWithSignalCheckAsync(step_value - step_backblask, 0, token);
                                        await MoveToWithSignalCheckAsync(step_value , 70, token);
                                    }

                                    // Bị hủy giữa chừng → không làm gì thêm
                                    if (token.IsCancellationRequested) return;

                                    StopBlinkServoPanel();
                                    prev_post = startPos;
                                    D = product.C_D ?? 0;
                                    L = product.C_L ?? 0;
                                    airHole = product.airhole ?? 0;

                                    // Hoàn thành → tắt callback trước khi close
                                    _runningDialog.OnCancelled = null;
                                    _runningDialog?.SetCompleted();
                                    await Task.Delay(1500);
                                    _runningDialog?.Close();
                                    _runningDialog = null;
                                }));

                                txt_po.Focus();
                                txt_po.SelectAll();
                            }
                            else
                            {
                                _ = new ToastNotification("Data trùng không cần chạy lại", LogType.Info)
                                        .ShowAndAutoClose();
                                D = product.C_D ?? 0;
                                L = product.C_L ?? 0;
                                airHole = product.airhole ?? 0;
                            }
                        }
                        else
                        {
                            _ = new ToastNotification("PO " + po + " Không có công đoạn AirHole", LogType.Info)
                                    .ShowAndAutoClose();
                        }
                    }
                    else
                    {
                        // PO not found
                        _ = new ToastNotification("Không tìm thấy Data: " + po, LogType.Error)
                                .ShowAndAutoClose();
                        txt_po.Clear();
                        txt_po.Focus();
                    }
                }
                else
                {
                    txt_po.Clear();
                    txt_po.Focus();
                }
            }
        }

        private void txt_id_employee_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || txt_id_employee.Text.Length>=5)
            {
                string idEmployee = txt_id_employee.Text.Trim();
                employee = Sql.GetEmployee(idEmployee);

                if (employee != null)
                {
                    _ = new ToastNotification("Đăng nhập thành công, Xin Chào " + employee.name, LogType.Success)
                    .ShowAndAutoClose();
                    lb_check_emplyee.Text = "OK";
                    Log("Login Success ", LogType.Success);
                    //border_main.IsEnabled = true;
                    lb_check_emplyee.Foreground =
                    new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#16A34A"));
                    Task.Delay(500);
                    var dialogConfirm = new ConfirmDialog(
                       message: "Bạn hãy Kiểm Tra gốc trước khi thao tác?",
                       title: $"Chào! {employee.name}",
                       type: DialogType.Confirm);
                    dialogConfirm.Owner = this;
                    dialogConfirm.ShowDialog();

                    if (dialogConfirm.IsConfirmed)
                    {
                        Event_fab();
                    }
                    else
                    {
                        txt_po.Focus();
                        txt_po.SelectAll();
                    }
                }
                else
                {
                    Log("Login Fail ", LogType.Error);
                    txt_id_employee.Clear();
                    //border_main.IsEnabled = false;
                    lb_check_emplyee.Foreground =
                    new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#DC2626"));
                    lb_check_emplyee.Text = "NO";
                }
            }
        }

        private async void btn_start_Click(object sender, RoutedEventArgs e)
        {
            //NextPort();
            D = 0;
            L = 0;
            airHole = 0;
            check_pos_for_relay = false;
            GetValueStep();
            await NextPortAsync();
            //txt_current_position.Text = curren_postion.ToString();
        }

        private async void btn_Stop_Click(object sender, RoutedEventArgs e)
        {
            //PrevpPort();
            D = 0;
            L = 0;
            airHole = 0;
            check_pos_for_relay = false;
            GetValueStep();
            await PrevPortAsync();
        }

        private void btn_restart_Click(object sender, RoutedEventArgs e)
        {
            txt_po.Clear();
            txt_po.Focus();
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
            StopPort();
            StopReadInput();

        }

        private async void btn_po_restart_Click(object sender, RoutedEventArgs e)
        {
            txt_po.Clear();
            txt_po.Focus();
            prev_post = 0;
            txt_PSTX.Text = "";
            txt_gamng.Text = "";
            txt_l.Text = "";
            txt_d.Text = "";
            txt_airhole.Text = "";
            D = 0;
            L = 0;
            airHole = 0;
            _lightState = LightState.Off;

            // Đóng dialog cũ nếu còn
            _restartingDialog?.Close();

            // Mở dialog restart
            _restartingDialog = new RestartingDialog();
            _restartingDialog.Owner = this;
            _restartingDialog.Show();

            bool checkIndex_ss = txt_ss1.Text.Contains("ON");
            if (checkIndex_ss)
                await RestartPortAsync(Set_origin_number, true);
            else
                await RestartPortAsync(lm_min);

            // Xong → hiện completed rồi tự đóng sau 1.5s
            _restartingDialog?.SetCompleted();
            await Task.Delay(1500);
            _restartingDialog?.Close();
            _restartingDialog = null;
        }
        #endregion
        private void GetValueStep()
        {
            double.TryParse(num_step.Value.ToString(), out double step);
            Post_Master = (int)(step * 500);
            if (txt_step_display != null)
            {
                txt_step_display.Text = Post_Master.ToString() + " - " + step.ToString() + "mm";
            }
        }
        private void num_step_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double?> e)
        {
            GetValueStep();
        }

        //event btn fab
        void Event_fab()
        {
            if (popup_jog.IsOpen)
            {
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
            else
            {
                txt_po.Focus();
                txt_po.SelectAll();
            }
        }
        private void btn_fab_Click(object sender, RoutedEventArgs e)
        {
            Event_fab();
        }

        private void btn_stop_main_Click(object sender, RoutedEventArgs e)
        {
            StopPort();
        }

        private void btn_set_origin_Click(object sender, RoutedEventArgs e)
        {
            Set_origin_number = Convert.ToInt32(curren_postion);
            txt_origin_pos.Text = Set_origin_number.ToString();
            if (txt_origin_pos.Text != "-")
            {
                //luu vao file 
                SaveOrigin(Set_origin_number??0, "origin");
                txt_po.Focus();
                txt_po.SelectAll();
                _ = new ToastNotification("Set Gốc Hoàn Tất", LogType.Success)
                                 .ShowAndAutoClose();
            }
            popup_jog.IsOpen = false;

        }

        #region xu ly slide bar
        private bool _isPanelOpen = true;

        private void btn_toggle_panel_Click(object sender, RoutedEventArgs e)
        {
            if (_isPanelOpen)
            {
                // Ẩn panel — animate Width về 0
                var anim = new GridLengthAnimation
                {
                    From = col_right.Width,
                    To = new GridLength(0),
                    Duration = TimeSpan.FromMilliseconds(250)
                };
                col_right.BeginAnimation(ColumnDefinition.WidthProperty, anim);

                border_control.Visibility = Visibility.Collapsed;
                txt_toggle_icon.Text = "◀";
                _isPanelOpen = false;
            }
            else
            {
                // Hiện panel — animate Width về 1*
                border_control.Visibility = Visibility.Visible;

                var anim = new GridLengthAnimation
                {
                    From = new GridLength(0),
                    To = new GridLength(1, GridUnitType.Star),
                    Duration = TimeSpan.FromMilliseconds(250)
                };
                col_right.BeginAnimation(ColumnDefinition.WidthProperty, anim);

                txt_toggle_icon.Text = "▶";
                _isPanelOpen = true;
            }
        }

        #endregion

        private void txt_po_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox txt)
            {
                txt.SelectAll();
            }
        }

        private void txt_po_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            TextBox txt = sender as TextBox;

            if (txt != null && !txt.IsKeyboardFocusWithin)
            {
                e.Handled = true;
                txt.Focus();
            }
        }

        private void Event_relay(bool b = true)
        {
            if (b)
            {
                //bat relay
                if (outRelay == 0)
                {
                    cdio.OutBit(m_Id, 0, 0);
                }
                else
                {
                    cdio.OutBit(m_Id, 0, 1);
                }
            }
            else
            {
                cdio.OutBit(m_Id, 0, 0);
            }
        }
        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            //Event_relay();
        }

        private void btn_cancel_control_Click(object sender, RoutedEventArgs e)
        {
            popup_jog.IsOpen = false;
            txt_po.Focus();
            txt_po.SelectAll();
        }

        private void DisnableScreen()
        {
            //border_employee.IsEnabled = false;
            border_control.IsEnabled = false;
            border_main.IsEnabled = false;
        }

        private void EnableScreen()
        {
            //border_employee.IsEnabled = true;
            border_control.IsEnabled = true;
            border_main.IsEnabled = true;
        }

        private void btn_reconnect_Click(object sender, RoutedEventArgs e)
        {
            _ = new ToastNotification("Kết nối lại", LogType.Info)
                                 .ShowAndAutoClose();
            connectPort(namePort);
            ConnectContec();
        }

        private void EventLight(LogType type, byte inEmg = 0 , byte in_tayCam = 1 , byte in_drill_water = 0)
        {
            //cac cong  den mau 
            short out_sc = 4;
            short out_er = 3;
            short out_nm = 5;

            //cong dien dang hoat dong 
            short out_on;

            cdio.EchoBackByte(m_Id, 0, out byte state);
            stateRelay = state;


            //cdio.OutBit(m_Id, 2, 1);
            

            for(int i =3; i <= 5; i++)
            {
                byte value;
                cdio.EchoBackBit(m_Id, (byte)i, out value);
                if (value == 1)
                {
                    out_on = (byte)i;
                    break;
                }
            }

            if (inEmg == 1 && inEmg == 1)
            {
                cdio.OutBit(m_Id, out_nm, 0);
                cdio.OutBit(m_Id, out_sc, 0);
                cdio.OutBit(m_Id, out_er, 1);
            }
            else if(isMoving == true || (in_tayCam==0 && in_drill_water == 1))
            {
                cdio.OutBit(m_Id, out_nm, 0);
                cdio.OutBit(m_Id, out_sc, 1);
                cdio.OutBit(m_Id, out_er, 0);
            }
            else
            {
                cdio.OutBit(m_Id, out_nm, 1);
                cdio.OutBit(m_Id, out_sc, 0);
                cdio.OutBit(m_Id, out_er, 0);
            }
        }


        private enum LightState { Off, Blinking, On }
        private LightState _lightState = LightState.Off;
        private int _blinkCounter = 0; // đếm vòng lặp để nháy

        private void xuly_light_touch()
        {
           
        // Trong vòng while của readinputloop, thay dòng OutBit cũ bằng:
         _blinkCounter++;

        switch (_lightState)
        {
            case LightState.Off:
                cdio.OutBit(m_Id, 2, 0);
                break;

            case LightState.On:
                cdio.OutBit(m_Id, 2, 1);
                break;

            case LightState.Blinking:
                // Nháy mỗi 5 vòng lặp (~500ms tùy tốc độ loop)
                cdio.OutBit(m_Id, 2, (byte) (_blinkCounter % 10 < 5 ? 1 : 0));
                break;
            }
        }

        private bool isBlink = true;
        private void turnOn_blink_statusServo()
        {
            Storyboard blink =
            (Storyboard)txt_status_servo.Resources["BlinkStoryboard"];

            blink.Begin(txt_status_servo, true);

            isBlink = false;
        }

        private void turnOff_blink_statusServo()
        {
            Storyboard blink =
    (Storyboard)txt_status_servo.Resources["BlinkStoryboard"];

            blink.Stop(txt_status_servo);
            isBlink = true;
            //txt_status_servo.Opacity = 1;
        }
    }
}
