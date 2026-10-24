using AutoUpdaterDotNET;
using back_stopper.Database;
using back_stopper.Model;
using back_stopper.ViewModel;
using CdioCs;
using MahApps.Metro.Controls;
using Modbus.Device;
using OpenQA.Selenium.Edge;
using Servoleadshine.Client;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using EdgeDriver = OpenQA.Selenium.Edge.EdgeDriver;
using EdgeOptions = OpenQA.Selenium.Edge.EdgeOptions;
using Selenium = OpenQA.Selenium;
using WebDriverWait = OpenQA.Selenium.Support.UI.WebDriverWait;

namespace back_stopper
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        private double ofset_number;
        //doi nhan vien 
        EmployeeData employee;
        //status drill và nút emergy
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
        public int? Set_origin_number = 0;
        //gioi han khong cho servo chay ra khoi 
        int lm_min = -30000;
        int lm_max = 450000;
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
        int Post_Master = 1000;
        double curren_postion = 57;
        double currenLoaded = 50;

        //gia tri stopper trong conffig
        double n_stopper;

        //bien luu trang thai tay cam 
        private byte temp_taycam;

        //effect light
        private enum LightState { Off, Blinking, On }
        private LightState _lightState = LightState.Off;
        private int _blinkCounter = 0; // đếm vòng lặp để nháy

        private bool isBlink = true;

        private int ret;//trang thai connect contect 
        private string idEmployee = String.Empty;

        //new servo
        private SerialPort serialPort;
        private IModbusSerialMaster modbusMaster;
        private byte slaveId = 1;
        private MyModbusClient myModBus;
        private EL6Servo myServo;

        //binding
        private BackStopper_model _binding = new BackStopper_model();

        public MainWindow()
        {
            InitializeComponent();
            myModBus = new MyModbusClient();
            myServo = new EL6Servo(myModBus);
            DataContext = _binding;
        }

        //info config 
        private string nameContect = "";
        private int times_restart = 0;
        private async void MetroWindow_Loaded(object sender, RoutedEventArgs e)
        {
            int.TryParse(num_step.Value.ToString(), out int n);
            Post_Master = n;

            DisnableScreen();

            string s_stopper = GetServoPortFromConfig("size_backstopper");//vi tri to lỗ đến vị trí cao nhất
            double.TryParse(s_stopper, out n_stopper);
            string s_ofset = GetServoPortFromConfig("offset");//số liệu upset cộng thêm 
            double.TryParse(s_ofset, out ofset_number);
            namePort = GetServoPortFromConfig("Name_Servo");
            connectPort(namePort);
            ConnectContec();


            //lay speed servo config
            int.TryParse(GetServoPortFromConfig("speed_servo"), out int sp_sv);
            speed_servo = sp_sv;

            
            //myModBus.limit();
            Console.WriteLine("offset: " + ofset_number);

            //nap du lieu bieu do sau khi mo app, mat mang thi bieu do de trong
            try
            {
                await _binding.LoadChartAsync();
            }
            catch (Exception ex)
            {
                Log("Lỗi tải biểu đồ: " + ex.Message, LogType.Warning);
            }
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
        private async void connectPort(string namePort)
        {
            try
            {
                myModBus.Connect(namePort, slaveId);
                if (myModBus.IsConnected)
                {
                    myServo.OnServo();
                    Console.WriteLine(namePort);
                    txt_status_servo.Text = "Sẵn sàng";
                    btn_fab.Visibility = Visibility.Visible;
                    txt_status_servo.Foreground =
                    new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#16A34A"));
                    Log("Mortor OK", LogType.Success);
                    //EnableScreen();
                    myServo.SetSoftLimit(lm_max, lm_min);
                    turnOff_blink_statusServo();
                    ConnectContec();
                }
                else
                {
                    await Task.Delay(50);
                    //serialPort.Close();
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
                Log("Lỗi connect port: " + ex.Message);

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
        #endregion

        #region control sensor

        private void StopPort()
        {
            if (!myModBus.IsConnected) return;

            myServo.StopEmergy();
            curren_postion = myServo.ReadPosition();
        }

        private async Task WaitServoStop()
        {
            double lastPos = -999999;
            while (true)
            {
                // yêu cầu position mới
                curren_postion = myServo.ReadPosition();
                //lấy tải trọng 

                await Task.Delay(100);

                double now = curren_postion;


                if (now - 5 > Set_origin_number && Set_origin_number != null)
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

        private async Task MoveToAsync(double pos, double? speedS = null)
        {
            if (!myModBus.IsConnected)
                return;

            // đang chạy thì bỏ qua
            if (isMoving)
                return;

            isMoving = true;


            try
            {
                speedS = (speedS != null) ? speedS : speed_servo;
                Log("Target = " + pos);

                myServo.StopEmergy();

                await myServo.MoveAbsolute((int)pos, (ushort)speedS);
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
                check_pos_for_relay = (Math.Abs(pos_target - curren_postion) <= 1600) && (Math.Abs(pos_target - curren_postion) >= 0);
                Console.WriteLine("pos: " + pos_target + "curr pos " + curren_postion + "result: " + check_pos_for_relay);

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
            Log("isrestarting: " + isRestarting);
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
                if (index_min == false)
                {
                    await MoveToAsync(curren_postion + 10000);
                    await MoveToAsync(Set_origin_number ?? 0,500);
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
            //check_pos_for_relay = pos == pos_target;
            check_pos_for_relay = Math.Abs(pos_target - pos) < 1510;

            await myServo.MoveRelative(Post_Master, (ushort)300);
        }

        private async Task PrevPortAsync()
        {
            int pos =
                Convert.ToInt32(curren_postion - Post_Master);
            //check_pos_for_relay = pos == pos_target;
                        check_pos_for_relay = Math.Abs(pos_target - pos) < 1510;

            await myServo.MoveRelative(Post_Master*-1, (ushort)300);
            //await MoveToAsync(pos,speedS:40);
        }
        #endregion

        #region xu ly nhan tin hieu CONTECT DIO dio
        private void ConnectContec()
        {
            try
            {
                nameContect = GetServoPortFromConfig("Name_Contect");
                cdio.QueryDeviceName(0, out string namecontec, out string nameDevice);
                Console.WriteLine($"name contect: {namecontec} & {nameDevice} ");
                ret = cdio.Init($"{nameContect}", out m_Id);
                if (ret == (int)CdioConst.DIO_ERR_SUCCESS)
                {
                    cts = new CancellationTokenSource();
                    txt_ss1.Foreground = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#16A34A"));
                    Log("Contec OK", LogType.Success);


                    cdio.OutBit(m_Id, 4, 1);
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
            // dang nhap lai (quet the NV khac) khong tao them vong quet thu 2
            if (ioTask != null && !ioTask.IsCompleted) return;

            cts = new CancellationTokenSource();
            //quet lien tuc 
            ioTask = readinputloop(cts.Token);
        }

        bool check_focus_id = false;
        private async Task readinputloop(CancellationToken token)
        {
            bool ischeckstopmin = false;
            bool ischeckstopmax = false;
            bool ischeckstophome = true;


            while (!token.IsCancellationRequested)
            {
                curren_postion = myServo.ReadPosition();
                _binding.CurrenPost = curren_postion.ToString();

                // =========================
                // Enable screen
                // =========================
                int ret = cdio.Init($"{nameContect}", out m_Id);

                // =========================
                // Đọc trạng thái servo realtime
                // =========================
                if (!myModBus.IsConnected)
                {
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
                    StopPort();
                    ConnectContec();
                }

                pauseEvent.Wait();

                 //=========================
                 //read input
                 //=========================
                cdio.InpBit(m_Id, 2, out byte inmin);
                cdio.InpBit(m_Id, 1, out byte inmax);
                cdio.InpBit(m_Id, 4, out byte in_taynam);
                cdio.InpBit(m_Id, 3, out byte in_drill_water);
                cdio.InpBit(m_Id, 5, out byte inemg);
                cdio.InpBit(m_Id, 6, out byte inTouch);

                //gia thong so max
                //inmax = (inmax == 0) ? (byte)1 : (byte)0;

                //gia thong so max
                //inmax = (inmax == 0) ? (byte)1 : (byte)0;

                //temp 

                temp_taycam = in_taynam;

                 cdio.EchoBackByte(m_Id, 0, out byte state);
                stateRelay = state;


                xuly_light_touch();

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
                _ = XylyCambien(inemg, in_taynam, ret);


                if (ret == (int)CdioConst.DIO_ERR_SUCCESS)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        HandleSensorMin(
                            inmin,
                            ref ischeckstopmin,
                            ref ischeckstophome);

                        HandleSensorMax(
                            inmax,
                            ref ischeckstopmax);

                        HandleAlarm(
                            inmin,
                            inmax,
                            state);

                    });
                }

                await Task.Delay(50);
            }
        }

        private async Task eventCancel(string toast, LogType type)
        {
            //DisnableScreen();
            _ = Dispatcher.InvokeAsync(() =>
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
                if (temp_taycam == 0 && ret == (int)CdioConst.DIO_ERR_SUCCESS)
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
                    if (employee != null && (ret == (int)CdioConst.DIO_ERR_SUCCESS) && myModBus.IsConnected)
                    {
                        EnableScreen();
                    }
                }
            }

        }

        private void HandleSensorMin(byte raw, ref bool isCheckStopMin, ref bool isCheckStopHome)
        {
            bool on = raw == 0;
            if (on)
            {
                if (!myModBus.IsConnected) return;
                if (!isCheckStopMin)
                {
                    //StopPort();
                    isCheckStopMin = true;
                    isCheckStopHome = false;
                    _ = eventCancel("Đã chạm MIN! Hành trình đã bị dừng", LogType.Error);
                }
            }
            else
            {
                isCheckStopMin = false;
            }

            txt_current_position.Text = curren_postion.ToString()
                + $"  ({( curren_postion/  5000) - value_stopper:F2}mm)";
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
                
                if (!myModBus.IsConnected) return;
                if (!isCheckStopMax)
                {
                    StopPort();
                    isCheckStopMax = true;
                    _ = eventCancel("Đã chạm MAX! Hành trình đã bị dừng", LogType.Error);
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
                _ = (in0 == 1) ? btn_Stop.IsEnabled = false
                           :
                          btn_start.IsEnabled = false;
                if (in0 == 1)
                {
                    border_txt_po.IsEnabled = false;
                    Blink_Min_Max(LogType.Warning, false);
                    //btn_po_restart.IsEnabled = true;
                }
                else
                {
                    Blink_Min_Max(LogType.Warning, true);
                    border_txt_po.IsEnabled = false;
                    //btn_po_restart.IsEnabled = false;
                }
            }
            else
            {
                //AlarmSound.Stop();
                btn_start.IsEnabled = true;
                btn_Stop.IsEnabled = true;
                border_txt_po.IsEnabled = true;
                StopBlink_Min_Max();
                EnableScreen();
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
            //cdio.OutBit(m_Id, 0, 1


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
        private void StartBlinkServoBorder(LogType type, int style = 0)
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
            if (style == 1)
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

        private void Blink_Min_Max(LogType type, bool isMin, int style = 0)
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

            ColorAnimation animation = new ColorAnimation
            {
                From = color,
                To = Colors.White,
                Duration = TimeSpan.FromMilliseconds(200),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };

            SolidColorBrush brush = new SolidColorBrush(Colors.Yellow);

            if (isMin)
            {
                border_ss_min.Background = brush;
            }
            else {
                border_ss_max.Background = brush; 
            }


            brush.BeginAnimation(
                SolidColorBrush.ColorProperty,
                animation);
        }

        private void StopBlink_Min_Max(int style = 0)
        {
            SolidColorBrush colorNormal = new SolidColorBrush(
    (Color)ColorConverter.ConvertFromString("#FFFFFF"));

            if (style == 0)
            {
                border_ss_max.Background = colorNormal;
                border_ss_min.Background = colorNormal;
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
                    productData product;
                    try
                    {
                        product = await Sql.GetProductInfo(po);
                    }
                    catch (Exception ex)
                    {
                        Log("Lỗi lấy data PO: " + ex.Message, LogType.Error);
                        _ = new ToastNotification("Không lấy được data PO (lỗi kết nối hoặc data sai): " + po, LogType.Error)
                                .ShowAndAutoClose();
                        txt_po.SelectAll();
                        txt_po.Focus();
                        return;
                    }

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


                            //bu tru theo size D
                            int alpha = 0;
                            if(product.C_D<=20 && product.C_D >=19)
                            {
                                //double.TryParse(product.C_D.ToString(), out double dValue);
                                //int.TryParse((dValue / 5).ToString(),out alpha);
                                //alpha *= 100;
                                alpha = 500;
                            }
                            else if (product.C_D>20)
                            {
                                //double.TryParse(product.C_D.ToString(), out double dValue);
                                //int.TryParse((dValue / 5).ToString(), out alpha);
                                //alpha *= 200;
                                alpha = 800;
                            }
                            int n = Convert.ToInt32((n_stopper * 5000) + (ah_num * 5000) + (ofset_number*5000) + alpha);
                            bool checkData = airHole == product.airhole && L == product.C_L && product.C_D == D;

                            if (!checkData)
                            {
                                //luu lai so lan quet PO 
                                //times_restart += 1;
                                //SaveOrigin(times_restart, "times_restart_home");

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
                                    //StartBlinkServoPanel(LogType.Warning);

                                    double step_backblask = 10000;
                                    //double step_value = n +  (((product.airhole_to_low ?? 0) + (product.airhole_to_up ?? 0))/2) ;
                                    //double step_value = n + (ofset_number * 5000);
                                    pos_target = n;

                                    Console.WriteLine("step: " + n + "  " + ofset_number );
                                    if (n <= curren_postion)
                                    {
                                        await MoveToWithSignalCheckAsync((n), 1000, token);
                                    }
                                    else if (n > curren_postion)
                                    {
                                        await MoveToWithSignalCheckAsync(n + step_backblask, speed_servo, token);
                                        await MoveToWithSignalCheckAsync(n , 300, token);
                                    }

                                    // Bị hủy giữa chừng → không làm gì thêm
                                    if (token.IsCancellationRequested) return;

                                    //StopBlinkServoPanel();
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


                                    //tự động nhập PO
                                    var dialogConfirm = new ConfirmDialog(
                                          message: "Bắt đầu thực hiện AUTO PO?",
                                          title: $"Chào! {employee.name}",
                                          type: DialogType.Confirm);
                                    dialogConfirm.Owner = this;
                                    dialogConfirm.ShowDialog();

                                    if (dialogConfirm.IsConfirmed)
                                    {
                                        AutoControl(txt_po.Text);
                                    }
                                    else
                                    {
                                        txt_po.SelectAll();
                                        txt_po.Focus();
                                    }

                                    //đưa dữ liệu hiện tại vào dtb
                                    await Sql.InsertDataHistory(txt_po.Text, product.airhole ?? 0.0, Set_origin_number ?? 0, int.Parse(curren_postion.ToString()), DateTime.Now, idEmployee);

                                }));

                                txt_po.Focus();
                                txt_po.SelectAll();
                            }
                            else
                            {
                                _ = new ToastNotification("Data trùng không cần chạy lại", LogType.Info)
                                        .ShowAndAutoClose();
                                var dialogConfirm = new ConfirmDialog(
                                          message: "Bắt đầu thực hiện AUTO PO?",
                                          title: $"Chào! {employee.name}",
                                          type: DialogType.Confirm);
                                dialogConfirm.Owner = this;
                                dialogConfirm.ShowDialog();

                                if (dialogConfirm.IsConfirmed)
                                {
                                    AutoControl(txt_po.Text);
                                }
                                else
                                {
                                    txt_po.SelectAll();
                                    txt_po.Focus();
                                }
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

        private bool _isAutoRunning = false;
        private async void AutoControl(string input_po)
        {
            // dang auto PO truoc thi bo qua, tranh 2 lan auto chong nhau
            if (_isAutoRunning)
            {
                _ = new ToastNotification("Đang auto PO trước, vui lòng chờ", LogType.Warning)
                        .ShowAndAutoClose();
                return;
            }

            _isAutoRunning = true;
            try
            {
                await event_auto_web(input_po);
                await event_auto_app(input_po);
            }
            catch (Exception ex)
            {
                Log("Lỗi auto PO: " + ex, LogType.Error);
                MessageBox.Show("Đã có lỗi xảy ra khi auto PO: " + ex.Message);
            }
            finally
            {
                _isAutoRunning = false;
            }
        }

        private async void txt_id_employee_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                idEmployee = txt_id_employee.Text.Trim();
                try
                {
                    employee = await Sql.GetEmployee(idEmployee);
                }
                catch (Exception ex)
                {
                    Log("Lỗi lấy data nhân viên: " + ex.Message, LogType.Error);
                    _ = new ToastNotification("Không kết nối được dữ liệu nhân viên, vui lòng thử lại", LogType.Error)
                            .ShowAndAutoClose();
                    return;
                }
                //event_auto_web_click_button();
                //tự động nhập PO


                if (employee != null)
                {
                    StartReadInput();
                    _ = new ToastNotification("Đăng nhập thành công, Xin Chào " + employee.name, LogType.Success)
                    .ShowAndAutoClose();
                    lb_check_emplyee.Text = "OK";
                    Log("Login Success ", LogType.Success);
                    //border_main.IsEnabled = true;
                    lb_check_emplyee.Foreground =
                    new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#16A34A"));
                    Task.Delay(500);

                    txt_id_employee.Text = employee.name;
                    txt_id_employee.Foreground = Brushes.BlueViolet;
                    txt_id_employee.FontSize = 25;
                    txt_id_employee.FontWeight = FontWeights.Bold;

                    Keyboard.ClearFocus();


                    if (ret == (int)CdioConst.DIO_ERR_SUCCESS && myModBus.IsConnected)
                    {
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
            ushort control =
                myModBus.ReadHoldingRegister(0x6000, 1)[0];

            ushort negHigh =
                myModBus.ReadHoldingRegister(0x6008, 1)[0];

            ushort negLow =
                myModBus.ReadHoldingRegister(0x6009, 1)[0];

            int negative =
    (int)((uint)negHigh << 16 | negLow);

            Console.WriteLine($"Pr8.00 = 0x{control:X4}");
            Console.WriteLine($"NegHigh = 0x{negHigh:X4}");
            Console.WriteLine($"NegLow = 0x{negLow:X4}");
            Console.WriteLine($"Negative = {negative}");
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
            myServo.OffServo();
            myModBus.Close();

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
                await RestartPortAsync(0, true);
            else
                await RestartPortAsync(0);

            // Xong → hiện completed rồi tự đóng sau 1.5s
            _restartingDialog?.SetCompleted();
            await Task.Delay(1500);
            _restartingDialog?.Close();
            _restartingDialog = null;
        }
        #endregion
        private void GetValueStep()
        {
            //decimal.TryParse(, out decimal step);

            double value = Math.Round(num_step.Value ?? 0, 1);

            decimal step = (decimal)value;

            Post_Master = (int)(step * 5000);

            if (txt_step_display != null)
            {
                txt_step_display.Text = Post_Master.ToString("F1") + " - " + step.ToString() + "mm";
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
            //if (txt_origin_pos.Text != "-")
            //{
            //luu vao file 
            myServo.Set_Current_homing();
            txt_po.Focus();
            txt_po.SelectAll();
            _ = new ToastNotification("Set Gốc Hoàn Tất", LogType.Success)
                             .ShowAndAutoClose();
            //}
            popup_jog.IsOpen = false;

        }

        #region xu ly slide bar
        private bool _isPanelOpen = true;

        private async void btn_toggle_panel_Click(object sender, RoutedEventArgs e)
        {
            //AutoControl();
            //AutoControl("520006648971");
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
                //txt_toggle_icon.Text = "◀";
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

                //txt_toggle_icon.Text = "▶";
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
            if (txt == null) return;

            if (!txt.IsKeyboardFocusWithin)
            {
                e.Handled = true;
                txt.Focus();
            }

            // PO luon 12 so, bam OK = bam Enter
            OpenNumericKeyboard(txt, txt.Text, value => EnterTextFromKeyboard(txt, value), maxLength: 12);
        }

        private void txt_id_employee_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!txt_id_employee.IsKeyboardFocusWithin)
            {
                e.Handled = true;
                txt_id_employee.Focus();//GotFocus doi ten NV -> ma NV
            }

            OpenNumericKeyboard(txt_id_employee, txt_id_employee.Text,
                value => EnterTextFromKeyboard(txt_id_employee, value));
        }

        private void num_step_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // bam nut +/- cua NumericUpDown thi giu hanh vi cu
            if (IsInsideButton(e.OriginalSource as DependencyObject, num_step)) return;

            e.Handled = true;
            string current = num_step.Value?.ToString(CultureInfo.InvariantCulture) ?? "";

            OpenNumericKeyboard(num_step, current, value =>
            {
                // dung InvariantCulture: may cai tieng Viet se hieu "0.5" thanh 5
                if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double step))
                {
                    num_step.Value = step;//NumericUpDown tu gioi han trong Minimum/Maximum
                }
            });
        }

        #region nhan ma tu may quet
        // may quet go moi ky tu cach nhau vai ms, nguoi go tay cham hon nhieu
        // -> chuoi ky tu den lien tuc (cach nhau <= ScanKeyGapMs) + Enter = 1 lan quet
        private const int ScanKeyGapMs = 80;
        private readonly StringBuilder _scanBuffer = new StringBuilder();
        private int _lastScanKeyTime;
        private TextBox _scanFocusedBox;
        private string _scanFocusedBoxText;

        private void MetroWindow_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (_scanBuffer.Length == 0 || e.Timestamp - _lastScanKeyTime > ScanKeyGapMs)
            {
                // bat dau chuoi moi: nho o dang focus de tra lai noi dung neu ma quet thuoc o khac
                _scanBuffer.Clear();
                _scanFocusedBox = Keyboard.FocusedElement as TextBox;
                _scanFocusedBoxText = _scanFocusedBox?.Text;
            }

            _scanBuffer.Append(e.Text);
            _lastScanKeyTime = e.Timestamp;
        }

        private void MetroWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            bool isScan = _scanBuffer.Length >= 5 && e.Timestamp - _lastScanKeyTime <= ScanKeyGapMs;
            string code = _scanBuffer.ToString().Trim();
            _scanBuffer.Clear();
            if (!isScan) return;

            // 12 ky tu = PO, 5 so = ma nhan vien
            TextBox target;
            if (code.Length == 12)
                target = txt_po;
            else if (code.Length == 5 && code.All(char.IsDigit))
                target = txt_id_employee;
            else
                return;//ma khac -> de o dang focus xu ly nhu cu

            e.Handled = true;

            // ky tu quet da lo go vao o khac -> tra lai noi dung cu cho o do
            if (_scanFocusedBox != null && _scanFocusedBox != target)
            {
                _scanFocusedBox.Text = _scanFocusedBoxText;
            }

            if (!target.IsEnabled)
            {
                _ = new ToastNotification("Chưa thể nhập PO lúc này (chưa đăng nhập hoặc đang chạm MIN/MAX)", LogType.Warning)
                        .ShowAndAutoClose();
                return;
            }

            if (target == txt_po)
            {
                txt_po.Focus();
            }
            EnterTextFromKeyboard(target, code);
        }
        #endregion

        #region ban phim so tren man hinh
        private bool _isKeyboardOpen = false;

        private void OpenNumericKeyboard(FrameworkElement target, string initialText, Action<string> onOk,
            int maxLength = 0)
        {
            if (_isKeyboardOpen) return;
            _isKeyboardOpen = true;

            // doi su kien cham xu ly xong roi moi mo ban phim
            Dispatcher.BeginInvoke(new Action(() =>
            {
                string value;
                try
                {
                    value = NumericKeyboard.Show(this, target, initialText, maxLength);
                }
                finally
                {
                    _isKeyboardOpen = false;
                }

                if (value != null)
                {
                    onOk(value);
                }
            }), DispatcherPriority.Input);
        }

        //gan gia tri vao o nhap roi gia lap phim Enter de chay dung logic cu
        private void EnterTextFromKeyboard(TextBox target, string value)
        {
            target.Text = value;
            target.CaretIndex = value.Length;

            PresentationSource source = PresentationSource.FromVisual(target);
            if (source == null) return;

            target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Enter)
            {
                RoutedEvent = Keyboard.KeyDownEvent
            });
        }

        private static bool IsInsideButton(DependencyObject source, DependencyObject container)
        {
            for (DependencyObject d = source; d != null && d != container;
                 d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
            {
                if (d is ButtonBase) return true;
            }
            return false;
        }
        #endregion

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


        private void btn_reconnect_Click(object sender, RoutedEventArgs e)
        {
            _ = new ToastNotification("Kết nối lại", LogType.Info)
                                 .ShowAndAutoClose();
            connectPort(namePort);
            ConnectContec();
        }



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
                    cdio.OutBit(m_Id, 2, (byte)(_blinkCounter % 10 < 5 ? 1 : 0));
                    break;
            }
        }

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

        #region Control screen
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

        #endregion

        #region effect blink touch and light
        private void EventLight(LogType type, byte inEmg = 0, byte in_tayCam = 1, byte in_drill_water = 0)
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


            for (int i = 3; i <= 5; i++)
            {
                byte value;
                cdio.EchoBackBit(m_Id, (byte)i, out value);
                if (value == 1)
                {
                    out_on = (byte)i;
                    break;
                }
            }

            if (inEmg == 1)
            {
                cdio.OutBit(m_Id, out_nm, 0);
                cdio.OutBit(m_Id, out_sc, 0);
                cdio.OutBit(m_Id, out_er, 1);
            }
            else if (isMoving == true || (in_tayCam == 0 && in_drill_water == 1))
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
        #endregion

        private void txt_id_employee_GotFocus(object sender, RoutedEventArgs e)
        {
            if (employee == null) return;
            txt_id_employee.Text = employee.id;
            txt_id_employee.Foreground = Brushes.Black;
            txt_id_employee.FontSize = 20;
            txt_id_employee.FontWeight = FontWeights.Normal;
        }

        private void txt_id_employee_LostFocus(object sender, RoutedEventArgs e)
        {

            // Không được focus
            if (employee == null) return;

            txt_id_employee.Text = employee.name;
            txt_id_employee.Foreground = Brushes.BlueViolet;
            txt_id_employee.FontSize = 25;
            txt_id_employee.FontWeight = FontWeights.Bold;
        }

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        private async Task event_auto_app(string numPO)
        {
            ////////////////////
            //tiep tuc qua app 2
            ///////////////////
            Process notepadProcess = null;
            Process[] processes = Process.GetProcessesByName("WorkSupportSystem");//kiem tra xem co đang được mở sẵn không

            if (processes.Length > 0)
            {
                notepadProcess = processes[0];//lay luôn cửa sổ đầu tiên
            }
            else
            {
                //khởi động notepad
                notepadProcess = Process.Start("WorkSupportSystem.exe");
                //cho app khoi dong o luong rieng, khong lam dung UI
                await Task.Run(() => notepadProcess.WaitForInputIdle(2000));
            }

            if (notepadProcess != null)
            {
                //để cửa sổ lên đầu
                SetForegroundWindow(notepadProcess.MainWindowHandle);
                //dung await Task.Delay thay Thread.Sleep: UI + vong doc cam bien van chay trong luc cho
                await Task.Delay(300);

                ////dán nội dung vào nontepad
                //System.Windows.Clipboard.SetText(idEmployee);
                //System.Threading.Thread.Sleep(300);

                //System.Windows.Forms.SendKeys.SendWait("^v");
                //System.Threading.Thread.Sleep(300);

                System.Windows.Forms.SendKeys.SendWait("^a");      // Ctrl + A
                await Task.Delay(300);

                System.Windows.Forms.SendKeys.SendWait("{DEL}");   // Xóa
                await Task.Delay(300);
                System.Windows.Forms.SendKeys.SendWait(numPO);
                System.Windows.Forms.SendKeys.SendWait("{ENTER}");
                await Task.Delay(300);

            }
        }

        private async Task event_auto_web(string s)
        {
            // Selenium chay o luong rieng: Edge loi/cham thi UI va vong doc cam bien (E-stop) van chay
            string error = await Task.Run(() => RunAutoWeb(s));

            this.Activate();
            if (error != null)
            {
                MessageBox.Show(error);
            }
        }

        // Chay o luong nen, KHONG dung control UI o day. Tra ve null neu OK, nguoc lai la noi dung loi
        private static string RunAutoWeb(string s)
        {
            // Edge chua mo cong debug -> bao loi ngay, khong phai doi driver timeout ~60s
            if (!IsPortOpen("127.0.0.1", 9222, 1000))
            {
                return "Không kết nối được Edge (port 9222).\nHãy mở Edge bằng shortcut có --remote-debugging-port=9222 rồi thử lại.";
            }

            bool found_tab = false;
            EdgeDriverService service = null;
            Selenium.IWebDriver driver = null;

            try
            {
                string appFolder = AppDomain.CurrentDomain.BaseDirectory;
                // 2. Ép Selenium sử dụng file msedgedriver.exe nằm ngay trong thư mục này
                service = EdgeDriverService.CreateDefaultService(appFolder);
                service.HideCommandPromptWindow = true;

                EdgeOptions options = new EdgeOptions();//cau hinh edg
                options.DebuggerAddress = "127.0.0.1:9222";
                driver = new EdgeDriver(service, options);//cau hinh cho driver edg

                //lay cac cua so
                var windowHandles = driver.WindowHandles;
                foreach (var handle in windowHandles)
                {
                    driver.SwitchTo().Window(handle);//chuyen sang tab
                    //kiem tra title cua tab
                    if (!driver.Title.Contains("Supro Standby"))
                        continue;

                    found_tab = true;
                    WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(5));//doi web load khoang 5s
                    try
                    {
                        Selenium.IWebElement clickVer = wait.Until(d => d.FindElement(Selenium.By.Id("formMask")));//tim ô này
                        clickVer.Click();

                        Selenium.IWebElement label_result = wait.Until(d => d.FindElement(Selenium.By.Id("lblDebug")));//tim ô này
                        string s_result = label_result.Text;
                        Console.WriteLine("s: " + s_result);

                        for (int i = 0; i < s_result.Length; i++)
                        {
                            driver.SwitchTo().ActiveElement().SendKeys(Selenium.Keys.Backspace);
                            Thread.Sleep(50);//luong nen nen Sleep khong lam dung UI
                        }

                        driver.SwitchTo().ActiveElement().SendKeys(s);
                        Thread.Sleep(300);
                        driver.SwitchTo().ActiveElement().SendKeys(Selenium.Keys.Enter);
                        Thread.Sleep(300);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Loi web auto: " + ex);
                        return "Đã có lỗi xảy ra khi nhập PO trên web: " + ex.Message;
                    }

                    // da xu ly xong tab Supro -> thoat vong lap, khong dung driver nua
                    break;
                }

                if (!found_tab)
                {
                    return "Không tìm thấy cửa sổ Supro Standby";
                }
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Loi web auto: " + ex);
                return "Đã có lỗi xảy ra khi chạy web auto: " + ex.Message;
            }
            finally
            {
                // dong driver 1 lan duy nhat, sau khi da duyet xong cac tab
                try
                {
                    driver?.Quit();
                    driver?.Dispose();
                    service?.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Loi dong driver: " + ex.Message);
                }
            }
        }

        private static bool IsPortOpen(string host, int port, int timeoutMs)
        {
            using (var client = new System.Net.Sockets.TcpClient())
            {
                try
                {
                    return client.ConnectAsync(host, port).Wait(timeoutMs) && client.Connected;
                }
                catch
                {
                    return false;
                }
            }
        }

        private void btn_done_Click(object sender, RoutedEventArgs e)
        {
            //event_auto_web_click_button();
            var reportScreen = new ErrorReportDialog();
            reportScreen.ShowDialog();
        }
    }
}
