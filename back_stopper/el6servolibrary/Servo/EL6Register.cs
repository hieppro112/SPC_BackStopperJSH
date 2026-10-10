namespace Servoleadshine.Model
{
    public class EL6Register
    {
        //type run 
        public const ushort Absolute_run = 0x0001;
        public const ushort Relative_run = 0x0041;


        public const ushort CurrentPosition = 0x0B16;

        public const ushort Command = 0x6002;

        public const ushort PathData = 0x6200;
        public const ushort Alarm = 0x0B03;
        //status mortor 
        public const ushort statusMortor = 0x0B08;

        //OnSerrvo 
        public const ushort AddressServo = 0x0035;
        public const ushort ServoOn = 0x0010;
        public const ushort ServoOff = 0x0000;

        //stop emergy 
        public const ushort AddressStop = 0x6002;
        public const ushort AddressStopValue = 0x0040;

        //motion
        public const ushort AddressPath = 0x6200;
        public const ushort AddressExecute = 0x6002;
        public const ushort Execute = 0x0010;

        //set homing 
        public const ushort Current_set_homing = 0x021;

        //read complate run 
        //public const ushort read_status_run = 0x000P;

        //limit software position
        private const ushort AddressLimitSoftware = 0x0515;
        private const ushort Limit_duong = 0x0516;
        private const ushort Limit_am = 0x0518;
        public const ushort PRControl = 0x6000;

        public const ushort SoftwarePositiveLimitHigh = 0x6006;
        public const ushort SoftwarePositiveLimitLow = 0x6007;

        public const ushort SoftwareNegativeLimitHigh = 0x6008;
        public const ushort SoftwareNegativeLimitLow = 0x6009;








    }
}
