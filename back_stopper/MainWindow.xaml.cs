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
        //value stopper den origin 
        private double value_stopper;
        //man hinh nhan 2 tay 
        private RunningDialog _runningDialog;
        //type blink 
        private BlinkSource _currentBlinkSource = BlinkSource.None;

        private int _blinkCount = 0;
        private readonly object _blinkLock = new object();
        //maneger sensor
        bool check_sst_0 = false;
        bool check_sst_1 = false;
        int outRelay = 0;
        byte stateRelay = 0;
        byte status_touch = 0;
        bool startBlink = true;
        //gia tri goc moi khi set 
        public int? Set_origin_number = null;
        //gioi han khong cho servo chay ra khoi 
        int lm_min = -50000;
        int lm_max = 41000;
        //doc khong bi nhanh hon 
        private TaskCompletionSource<double> positionTcs;
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
        private CancellationTokenSource _blinkLabelCts;
        private CancellationTokenSource cts;
        private Task ioTask;

        //toa do goc 
        int tdGoc = 0;
        //config ss control speed servo 
        int speed_servo = 30;
        int accel_servo = 50;
        int decel_servo = 50;
        double prev_post;
        private string namePort = "";

        bool check_pos_for_relay = false;

        double pos_target = 0;
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

            string s =  GetServoPortFromConfig("origin");
            if (!string.IsNullOrWhiteSpace(s))
            {
                border_main.IsEnabled = true;
                //btn_Stop.Background = new SolidColorBrush(
                //        (Color)ColorConverter.ConvertFromString("#DC2626"));
                //btn_restart.Background = new SolidColorBrush(
                //        (Color)ColorConverter.ConvertFromString("#FFF50B4B"));
                txt_origin_pos.Text = s;
                int.TryParse(s, out int num);
                Set_origin_number = num;
                //luu vao file 
                SaveOrigin(Set_origin_number ?? 0);
            }

            //connectPort(namePort_main);
        }


        #region Xy ly port

        //set gia tri config
        private void SaveOrigin(int originValue)
        {
            string path = @"C:\BackStopper_config\config.txt";

            // đọc toàn bộ line
            var lines = File.ReadAllLines(path).ToList();

            for (int i = 0; i < lines.Count; i++)
            {
                // tìm dòng origin:
                if (lines[i].StartsWith("origin:"))
                {
                    lines[i] = $"origin:{originValue}";
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

            //Dispatcher.Invoke(() =>
            //{
            //    list_log.Items.Insert(0,
            //        new LogItem
            //        {
            //            Message = logText,
            //            Color = color
            //        });

            //    // limit log
            //    if (list_log.Items.Count > 150)
            //    {
            //        list_log.Items.RemoveAt(
            //            list_log.Items.Count - 1);
            //    }
            //});
        }

        private void SendCommand(string cmd)
        {

            lock (serialLock)
            {
                if (!serialPort.IsOpen)
                    return;

                serialPort.Write(cmd + "\r\n");
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
                        //Dispatcher.BeginInvoke(
                        //    new Action(() =>
                        //    {
                        //        Log("POSITION => " +
                        //    curren_postion);
                        //    }));
                    }
                    if (matchLoad.Success)
                    {
                        currenLoaded =
                            int.Parse(
                                matchLoad.Groups[1].Value);
                        Console.WriteLine("loaded: " + currenLoaded);
                        //Dispatcher.BeginInvoke(
                        //    new Action(() =>
                        //    {
                        //        Log("Loaded => " +
                        //    currenLoaded);
                        //    }));
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
                check_pos_for_relay = (pos_target - curren_postion <= 5) && (pos_target - curren_postion >= 0);
                Console.WriteLine("pos: " + pos_target + "curr pos " + curren_postion + "result: "+check_pos_for_relay);
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
                await MoveToAsync(i==null?tdGoc:i??0.0);
                if(index_min == false)
                {
                    await MoveToAsync(Set_origin_number??lm_max,40);
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

                string nameContect = GetServoPortFromConfig("Name_Contect");
                int ret = cdio.Init($"{nameContect}", out m_Id);
                if (ret == (int)CdioConst.DIO_ERR_SUCCESS)
                {
                    cts = new CancellationTokenSource();
                    StartReadInput();
                    txt_ss1.Foreground = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#16A34A"));
                    Log("Contec OK", LogType.Success);
                    //lay speed servo config
                    int.TryParse(GetServoPortFromConfig("speed_servo"), out int sp_sv);
                    speed_servo = sp_sv;
                    var dialogConfirm = new ConfirmDialog(
                       message: "Bạn hãy Kiểm Tra gốc trước khi thao tác?",
                       title: "Success",
                       type: DialogType.Confirm);
                    dialogConfirm.Owner = this;
                    dialogConfirm.ShowDialog();

                    if (dialogConfirm.IsConfirmed)
                    {
                        Event_fab();
                        
                    }
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
            ioTask =  readinputloop(cts.Token);
        }



        //private async Task ReadInputLoop(CancellationToken token)
        //{
        //    bool isCheckStopMin = false;
        //    bool isCheckStopMax = false;
        //    bool isCheckStopHome = true;

        //    bool checkON_emg = false;//kiem tra emg da thong bao chua 
        //    bool checkON_tayNam = false;//kiem tra emg da thong bao chua 

        //    //while (!token.IsCancellationRequested)
        //    //{
        //    //    pauseEvent.Wait();
        //    //    cdio.InpBit(m_Id, 2, out byte inMin);//cong 2 min
        //    //    cdio.InpBit(m_Id, 0, out byte inMax);//cong 0 max
        //    //    cdio.InpBit(m_Id, 4, out byte in_taynam);//cong 4 tay gac 
        //    //    cdio.InpBit(m_Id, 3, out byte in_drill_water);//cong 3 dril, nuoc
        //    //    cdio.InpBit(m_Id, 5, out byte inEMG);//cong 5 nut Emergency

        //    //    //doc trang thai relay điện
        //    //    cdio.EchoBackByte(m_Id, 0, out byte state);
        //    //    stateRelay = state;



        //    //    Console.WriteLine("OUT cong 0: " + state);
        //    //    Console.WriteLine("cong 5 nut : " + inEMG);
        //    //    Console.WriteLine("cong :drill waater  " + in_drill_water);
        //    //    Console.WriteLine("cong tay nam: " + in_taynam);


        //    //    //relay dien on khi emergy =0, drill & water = 0 , đúng vị trí 
        //    //    bool check_post = true;
        //    //    outRelay = (inEMG == 0 && check_post) ? 1 : 0;
        //    //    Event_relay();

        //    //    ///ưu tiên cho nút dừng khẩn cấp
        //    //    ///tiếp theo là tay cầm 
        //    //    if (inEMG == 1)
        //    //    {
        //    //        await Dispatcher.BeginInvoke(new Action(() =>
        //    //        {
        //    //            StopPort();
        //    //            Event_relay(false);
        //    //            if (!checkON_emg)
        //    //            {
        //    //                StartBlinkServoBorder(LogType.Warning, 1);
        //    //                StartBlinkLabel("Dừng khẩn cấp", txt_notify);
        //    //                checkON_emg = true;
        //    //            }
        //    //        }));
        //    //    }
        //    //    else
        //    //    {
        //    //        checkON_emg = false; //tra ve trang thai cho thong bao emg
        //    //        _ = Dispatcher.BeginInvoke(new Action(() =>
        //    //        {
        //    //            StopBlinkLabel();
        //    //            StopBlinkServoBorder(1);
        //    //        }));


        //    //        if (in_taynam == 0)//tay cầm khi hoạt động 
        //    //        {
        //    //            _ = Dispatcher.BeginInvoke(new Action(() =>
        //    //            {
        //    //                StopPort();
        //    //                if (!checkON_tayNam)
        //    //                {
        //    //                    Dispatcher.BeginInvoke(new Action(() =>
        //    //                   {
        //    //                       StartBlinkServoBorder(LogType.Warning);
        //    //                       StartBlinkLabel("Đã kéo cần gạc ", txt_notify);
        //    //                   }));
        //    //                    checkON_tayNam = true;
        //    //                }
        //    //            }));
        //    //        }
        //    //        else
        //    //        {
        //    //            checkON_tayNam = false;
        //    //            _ = Dispatcher.BeginInvoke(new Action(() =>
        //    //            {
        //    //                StopBlinkLabel();
        //    //                StopBlinkServoBorder();
        //    //                //if (in_taynam == 1 && in_drill_water == 0) txt_notify.Text = "-";
        //    //                HandleSensorMin(inMin, ref isCheckStopMin, ref isCheckStopHome);
        //    //                HandleSensorMax(inMax, ref isCheckStopMax);
        //    //                HandleSensorHome(in_taynam, ref isCheckStopHome);
        //    //                HandleAlarm(inMin, inMax, state);
        //    //                //txt_status_servo.Text = GetServoStatus().ToString();
        //    //            }));
        //    //        }
        //    //    }

        //    //}




        //}











        private async Task readinputloop(CancellationToken token)
        {
            bool ischeckstopmin = false;
            bool ischeckstopmax = false;
            bool ischeckstophome = true;

            bool checkon_emg = false;
            bool checkon_taynam = false;

            while (!token.IsCancellationRequested)
            {
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


                cdio.EchoBackByte(m_Id, 0, out byte state);
                stateRelay = state;


                cdio.OutBit(m_Id, 2, 1);
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

                Event_relay();
                handle_Relay();
                // =========================
                // stt drill, water
                // =========================
                handDle_status_drill_water(in_drill_water);

                // ==================================================
                // priority 1 : emergency
                // ==================================================
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
                            StartBlinkServoBorder(
                                LogType.Warning,
                                1);

                            StartBlinkLabel(
                                "dừng khẩn cấp",
                                txt_notify);

                            DisnableScreen();
                        });
                    }

                    await Task.Delay(50);
                    continue;
                }

                // ==================================================
                // priority 2 : tay gạt
                // ==================================================
                else
                {
                   
                    
                    if (in_taynam == 0) {
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
                                    "đã kéo cần gạt",
                                    txt_notify);
                                DisnableScreen();
                            });
                            await Task.Delay(50);
                            continue;
                        }
                    }

                    else
                    {
                        checkon_taynam = false;
                        txt_notify.Text = "-";
                            StopBlinkLabel();
                            StopBlinkServoBorder();
                            StopBlinkServoBorder(1);
                            StopBlinkServoPanel();
                        EnableScreen();
                    }

                    //else
                    //{
                    //    StopBlinkLabel();
                    //    txt_notify
                    //}


                }

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


                //else
                //{
                //    if (checkon_taynam)
                //    {
                //        checkon_taynam = false;

                //        await dispatcher.invokeasync(() =>
                //        {
                //            stopblinkservoborder();

                //           stopblinklabel();
                //        });
                //    }
                //}

                // ==================================================
                // priority 3 : sensor
                // ==================================================


                await Task.Delay(50);
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
            txt_trongTai.Text = currenLoaded.ToString();
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
                    //BlinkLabel("OVER LIMIT !!!", txt_status_servo);
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
            
        }
        #endregion

        #region Event View
        //ham tao hieu ung mau nhap nhay khi servo di chuyen 


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
            _blinkLabelCts?.Cancel();

            _blinkLabelCts = new CancellationTokenSource();
            var token = _blinkLabelCts.Token;

            var originalText = txt.Text;
            var originalColor = txt.Foreground;

            var blinkColor = new SolidColorBrush(Colors.Red);
            var normalColor = new SolidColorBrush(Colors.White);

            Task.Run(async () =>
            {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        await Dispatcher.InvokeAsync(() =>
                        {
                            txt.Text = text;
                            txt.Foreground = blinkColor;
                        });

                        await Task.Delay(300, token);

                        await Dispatcher.InvokeAsync(() =>
                        {
                            txt.Foreground = normalColor;
                        });

                        await Task.Delay(300, token);
                    }
                }
                catch (TaskCanceledException)
                {
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    txt.Text = originalText;
                    txt.Foreground = originalColor;
                });
            });
        }

        private void StopBlinkLabel()
        {
            _blinkLabelCts?.Cancel();
        }

        double D = 0.0;
        double L = 0.0;
        double airHole = 0.0;

        // Thay thế toàn bộ hàm txt_po_KeyDown bằng đoạn này:


        //tao method cho nhan nut 
        // Thêm vào MainWindow.cs như 1 method mới
        //private async Task MoveToWithSignalCheckAsync(double target, int speed = 0)
        //{
        //    while (true)
        //    {
        //        // Chờ cho đến khi status_touch = 1 (cả 2 nút đang giữ)
        //        while (status_touch != 1)
        //        {
        //            StopPort();
        //            await Task.Delay(100);
        //        }

        //        // Đảm bảo isMoving = false trước khi gọi lại
        //        isMoving = false;

        //        // status_touch = 1 → chạy motor từ vị trí hiện tại đến target
        //        Task moveTask = (speed == 0)
        //            ? MoveToAsync(target)
        //            : MoveToAsync(target, speed);

        //        // Vừa chạy vừa monitor tín hiệu
        //        while (!moveTask.IsCompleted)
        //        {
        //            if (status_touch != 1)
        //            {
        //                StopPort();     // thả tay → dừng ngay
        //                await Task.Delay(200); // chờ servo dừng hẳn + isMoving = false
        //                break;          // quay lại vòng while chờ tín hiệu
        //            }
        //            await Task.Delay(50);
        //        }

        //        // Kiểm tra đã đến đích chưa (dùng curren_postion thay vì moveTask.IsCompleted)
        //        if (Math.Abs(curren_postion - target) <= 10) return; // đến đích → thoát
        //    }
        //}

        private async Task MoveToWithSignalCheckAsync(double target, int speed = 0)
        {
            while (true)
            {
                // Chờ signal = 1
                while (status_touch != 1)
                {
                    StopPort();
                    _runningDialog?.SetPaused(); // ← cập nhật UI dialog
                    await Task.Delay(100);
                }

                isMoving = false;
                _runningDialog?.SetRunning(); // ← cập nhật UI dialog

                Task moveTask = (speed == 0)
                    ? MoveToAsync(target)
                    : MoveToAsync(target, speed);

                while (!moveTask.IsCompleted)
                {
                    if (status_touch != 1)
                    {
                        StopPort();
                        await Task.Delay(200);
                        break;
                    }
                    await Task.Delay(50);
                }

                if (Math.Abs(curren_postion - target) <= 10) return;
            }
        }
        private async void txt_po_KeyDown(object sender, KeyEventArgs e)
        {
            if (txt_po.Text.Length > 12) txt_po.Clear();
            if (e.Key == Key.Enter)
            {
                double startPos = curren_postion;
                string po = txt_po.Text;
                productData product = Sql.GetProductInfo(po);

                if (product != null)
                {
                    check_pos_for_relay = false;
                    double ah_num = product.airhole??0.0;
                    string s_stopper = GetServoPortFromConfig("size_backstopper");
                    double.TryParse(s_stopper, out double n_stopper);
                    value_stopper = n_stopper;
                    border_txt_po.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFE4E4E4"));

                    if (ah_num != 0)
                    {
                        //txt_id.Text = product.Aufnr;
                        txt_PSTX.Text = product.Pstx;
                        txt_gamng.Text = product.Gamng.ToString();
                        txt_l.Text = product.C_L.ToString();
                        txt_d.Text = product.C_D.ToString();
                        txt_airhole.Text = ah_num.ToString();

                        double airhole = product.airhole ?? 0;
                        int n = Convert.ToInt32(
                            Set_origin_number - (n_stopper*500) - (ah_num * 1000 / 2.0));

                        pos_target = n;

                        bool checkData = airHole == product.airhole && L == product.C_L && curren_postion == n;

                        if (!checkData)
                        {
                            ///khu ro kieu moi khi cung chieu
                            _ = Dispatcher.BeginInvoke(new Action(async () =>
                            {
                                if (isMoving) return;

                                // Mở dialog
                                _runningDialog = new RunningDialog(po);
                                _runningDialog.Owner = this;
                                _runningDialog.Show();

                                StartBlinkServoPanel(LogType.Warning);

                                double step_backblask = 2000;
                                double step_value = n;

                                if (n > curren_postion)
                                {
                                    await MoveToWithSignalCheckAsync(step_value);
                                }
                                else if (n < curren_postion)
                                {
                                    await MoveToWithSignalCheckAsync(n - step_backblask);
                                    await MoveToWithSignalCheckAsync(step_value, 20);
                                   
                                }
                                else
                                {
                                    Log("Data trung voi data truoc khong can chay", LogType.Success);
                                }

                                StopBlinkServoPanel();
                                prev_post = startPos;
                                D = product.C_D ?? 0;
                                L = product.C_L ?? 0;
                                airHole = product.airhole ?? 0;

                                // Hết hành trình → hiện completed rồi tự đóng sau 1.5s
                                _runningDialog?.SetCompleted();
                                await Task.Delay(1500);
                                _runningDialog?.Close();
                                _runningDialog = null;
                            }));
                             
                            txt_po.Focus();
                            txt_po.SelectAll();


                            ///khu ro kieu cu => khi doi chieu 
                            //_ = Dispatcher.BeginInvoke(new Action(async () =>
                            //{
                            //    if (isMoving) return;

                            //    // Mở dialog
                            //    _runningDialog = new RunningDialog(po);
                            //    _runningDialog.Owner = this;
                            //    _runningDialog.Show();

                            //    StartBlinkServoPanel(LogType.Warning);

                            //    double step_backblask = 2000;
                            //    double step_value = n;

                            //    if (n <= curren_postion)
                            //    {
                            //        if (prev_post < curren_postion)
                            //        {
                            //            await MoveToWithSignalCheckAsync(n - step_backblask);
                            //            await MoveToWithSignalCheckAsync(step_value, 20);
                            //        }
                            //        else
                            //        {
                            //            await MoveToWithSignalCheckAsync(n);
                            //        }
                            //    }
                            //    else if (n > curren_postion)
                            //    {
                            //        if (prev_post > curren_postion)
                            //        {
                            //            await MoveToWithSignalCheckAsync(n + step_backblask);
                            //            await MoveToWithSignalCheckAsync(step_value, 20);
                            //        }
                            //        else
                            //        {
                            //            await MoveToWithSignalCheckAsync(n);
                            //        }
                            //    }
                            //    else
                            //    {
                            //        Log("Data trung voi data truoc khong can chay", LogType.Success);
                            //    }

                            //    StopBlinkServoPanel();
                            //    prev_post = startPos;
                            //    D = product.C_D ?? 0;
                            //    L = product.C_L ?? 0;
                            //    airHole = product.airhole ?? 0;

                            //    // Hết hành trình → hiện completed rồi tự đóng sau 1.5s
                            //    _runningDialog?.SetCompleted();
                            //    await Task.Delay(1500);
                            //    _runningDialog?.Close();
                            //    _runningDialog = null;
                            //}));

                            // Khi thả tay → stop ngay
                            //twoHand.OnAnyReleased = () =>
                            //{
                            //    Dispatcher.BeginInvoke(new Action(() =>
                            //    {
                            //        StopPort();
                            //        StopBlinkServoPanel();
                            //    }));
                            //};

                            //twoHand.ShowDialog();
                        }
                        else
                        {
                            Log("Data trùng với data trước không cần chạy", LogType.Success);

                            D = product.C_D ?? 0;
                            L = product.C_L ?? 0;
                            airHole = product.airhole ?? 0;
                        }
                    }
                    else
                    {
                        var dialogConfirm = new ConfirmDialog(
                            message: "PO này không có công đoạn AIR HOLE?",
                            title: "Cảnh báo!",
                            type: DialogType.Confirm);
                        dialogConfirm.Owner = this;
                        dialogConfirm.ShowDialog();
                    }
                }
                else
                {
                    // PO not found
                    //border_txt_po.BorderBrush = new SolidColorBrush(Colors.Red);
                    border_txt_po.Background = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#FEF2F2"));
                    TextBoxHelper.SetWatermark(txt_po, "PO NOT FOUND !!!");
                    txt_po.Clear();
                    txt_po.Focus();
                }
                //}
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
                    namePort = GetServoPortFromConfig("Name_Servo");
                    Task.Delay(500);
                    connectPort(namePort);
                   
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
            //bool checkIndex_ss = txt_ss1.Text.Contains("ON");
            //if (checkIndex_ss == true)
            //{
            //    RestartPortAsync(lm_max);
            //}
            //else
            //{
            //    RestartPortAsync(lm_min);
            //}

            //list_log.Items.Clear();

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

            // Start blink TRƯỚC khi chạy
            StartBlinkLabel("Lưu Ý: Servo đang di chuyển", txt_notify);

            bool checkIndex_ss = txt_ss1.Text.Contains("ON");

            if (checkIndex_ss)
                await RestartPortAsync(Set_origin_number, true);
            else
                await RestartPortAsync(lm_min);

            // Stop blink SAU khi chạy xong
            StopBlinkLabel();
        }
        #endregion
        private void GetValueStep()
        {
            double.TryParse(num_step.Value.ToString(), out double step);
            Post_Master = (int)(step * 500);
            if (txt_step_display != null)
            {
                //double sw_mm = step / 500.0;
                //double.TryParse(step,out double d);
                txt_step_display.Text = Post_Master.ToString() + " - " + step.ToString() + "mm";
            }
        }
        private void num_step_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double?> e)
        {
            //int.TryParse(num_step.Value.ToString(), out int n);
            //Post_Master = n;
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
        }
        private void btn_fab_Click(object sender, RoutedEventArgs e)
        {
            //popup_jog.IsOpen = !popup_jog.IsOpen;
            // Nếu popup đang mở → đóng luôn không cần hỏi mật khẩu

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
                border_main.IsEnabled = true;
                //btn_Stop.Background = new SolidColorBrush(
                //        (Color)ColorConverter.ConvertFromString("#DC2626"));
                //btn_restart.Background = new SolidColorBrush(
                //        (Color)ColorConverter.ConvertFromString("#FFF50B4B"));

                //luu vao file 
                SaveOrigin(Set_origin_number??0);
                txt_po.Focus();
                txt_po.SelectAll();
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
            border_employee.IsEnabled = false;
            border_control.IsEnabled = false;
            border_main.IsEnabled = false;
        }

        private void EnableScreen()
        {
            border_employee.IsEnabled = true;
            border_control.IsEnabled = true;
            border_main.IsEnabled = true;
        }
    }
}
