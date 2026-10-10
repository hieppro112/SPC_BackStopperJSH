using Servoleadshine.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Servoleadshine.Client
{
    public class EL6Servo
    {
        private readonly MyModbusClient modbus;
        public EL6Servo(MyModbusClient modbus)
        {
            this.modbus = modbus;
        }

        public int ReadPosition()
        {
            ushort[] data = modbus.ReadHoldingRegister(EL6Register.CurrentPosition,2);
            return (data[0] << 16 | data[1]);
        }

        public ushort ReadAlarm()
        {
            ushort[] data = modbus.ReadHoldingRegister(EL6Register.Alarm, 1);
            return data[0];
        }

        public ushort ReadStatusServo()
        {
            ushort[] daata = modbus.ReadHoldingRegister(EL6Register.statusMortor, 1);
            return daata[0];
        }

        public void OnServo()
        {
            modbus.WriteSingleRegister(EL6Register.AddressServo, EL6Register.ServoOn);
        }
        public void OffServo()
        {
            modbus.WriteSingleRegister(EL6Register.AddressServo, EL6Register.ServoOff);
        }

        public void StopEmergy()
        {
            modbus.WriteSingleRegister(EL6Register.AddressStop, EL6Register.AddressStopValue);
        }

        private void Excute() 
        {
            modbus.WriteSingleRegister(
                EL6Register.AddressExecute,EL6Register.Execute);
        }

        private void WritePath(int target,ushort speed,ushort acc, ushort dec,ushort typeRun)
        {
            ushort Hight = (ushort)(target >> 16);
            ushort Low = (ushort)(target);

            ushort[] data =
            {
                typeRun,Hight,Low,speed,acc,dec,0,0
            };

            modbus.WriteMultipleRegisters(EL6Register.AddressPath,data);
        }

        public async Task MoveAbsolute(int target,ushort speed)//chạy tuyệt đối
        {
            WritePath(target, speed, 100, 100,EL6Register.Absolute_run);

            await Task.Delay(100);
            Excute();
        }

        public async Task MoveRelative(int pules, ushort speed)
        {
            int current_pos = ReadPosition();
            int target = current_pos + pules;
            //await MoveAbsolute(target, speed,typeRun: EL6Register.Relative_run);  
            WritePath(pules, speed,  100, 100, EL6Register.Relative_run);
            await Task.Delay(100);
            Excute();
        } 

        public void Set_Current_homing()
        {
            modbus.WriteSingleRegister(EL6Register.AddressExecute, EL6Register.Current_set_homing);
        }

        //public void SetLimitSoftware(int limitDuong, int limitAm)
        //{
           
        //}

        public void SetSoftLimit(int positiveLimit, int negativeLimit)
        {
            // Bật Software Position Limit
            modbus.WriteSingleRegister(
                EL6Register.PRControl,
                0x0002);

            // Positive limit
            ushort positiveHigh = (ushort)((positiveLimit >> 16) & 0xFFFF);
            ushort positiveLow = (ushort)(positiveLimit & 0xFFFF);

            modbus.WriteSingleRegister(
                EL6Register.SoftwarePositiveLimitHigh,
                positiveHigh);

            modbus.WriteSingleRegister(
                EL6Register.SoftwarePositiveLimitLow,
                positiveLow);

            // Negative limit
            ushort negativeHigh = (ushort)((negativeLimit >> 16) & 0xFFFF);
            ushort negativeLow = (ushort)(negativeLimit & 0xFFFF);

            modbus.WriteSingleRegister(
                EL6Register.SoftwareNegativeLimitHigh,
                negativeHigh);

            modbus.WriteSingleRegister(
                EL6Register.SoftwareNegativeLimitLow,
                negativeLow);
        }

    }
}
