using Modbus.Device;
using System;
using System.IO.Ports;
using System.Windows.Forms;

namespace Servoleadshine.Client
{
    public class MyModbusClient
    {
        //cổng COM
        private SerialPort serialPort;
        //Imodbus
        private IModbusSerialMaster modbusMaster;
        //slave
        private byte slaveID;
        //kiemtra trang thai ket noi
        public bool IsConnected
        {
            get
            {
                return serialPort != null && serialPort.IsOpen;
            }
        }
        public MyModbusClient()
        {

        }

        public bool Connect(string port, byte slaveID)
        {
            //serialPort = new SerialPort(cbPorts.SelectedItem.ToString(), 38400, Parity.None, 8, StopBits.Two);
            try
            {
                this.slaveID = slaveID;

                serialPort = new SerialPort();
                serialPort.PortName = port;
                serialPort.BaudRate = 38400;
                serialPort.Parity = Parity.None;
                serialPort.DataBits = 8;
                serialPort.StopBits = StopBits.Two;

                serialPort.ReadTimeout = 500;
                serialPort.WriteTimeout = 500;

                serialPort.Open();

                modbusMaster = ModbusSerialMaster.CreateRtu(serialPort);
                modbusMaster.Transport.ReadTimeout = 500;
                modbusMaster.Transport.WriteTimeout = 500;

                return serialPort.IsOpen;
            }
            catch (Exception ex)
            {
                Console.WriteLine("loi khi connect: " + ex);
                return false;
            }
        }

        public void Close()
        {
            if (serialPort != null && serialPort.IsOpen)
            {
                serialPort.Close();
            }
        }

        public ushort[] ReadHoldingRegister(ushort address, ushort quantity)
        {
            if (!IsConnected) throw new Exception("Thiết bị chưa được kết nối");

            try
            {
                return modbusMaster.ReadHoldingRegisters(slaveID, address, quantity);
            }
            catch (Exception ex)
            {
                throw new Exception($"Đọc Holding thất bại: {ex.ToString()}");
            }
        }

        public void WriteSingleRegister(ushort address, ushort value)
        {
            if (!IsConnected)
            {
                MessageBox.Show("Thiết bị chưa được kết nối");
                return;
            } 

            try
            {
                modbusMaster.WriteSingleRegister(slaveID, address, value);
            }
            catch (Exception ex)
            {
                throw new Exception("Lỗi khi ghi Modbus: " + ex.Message);
            }
        }

        public void WriteMultipleRegisters(ushort address, ushort[] value)
        {
            if (!IsConnected) throw new Exception("Thiết bị chưa được kết nối");

            try
            {
                modbusMaster.WriteMultipleRegisters(slaveID, address, value);
            }
            catch (Exception ex)
            {
                throw new Exception("Lỗi khi ghi Multip: " + ex.Message);

            }
        }

        public void limit()
        {
            try
            {
                // 1. Bật tính năng Giới hạn mềm (Pr5.21 = 1)
                modbusMaster.WriteSingleRegister(slaveID, 0x0515, 1);

                // 2. Cài Giới hạn Dương Pr5.22 = 100,000 xung
                int maxLimit = 10000;
                ushort maxHigh = (ushort)((maxLimit >> 16) & 0xFFFF);
                ushort maxLow = (ushort)(maxLimit & 0xFFFF);
                modbusMaster.WriteMultipleRegisters(slaveID, 0x0516, new ushort[] { maxHigh, maxLow });

                // 3. Cài Giới hạn Âm Pr5.23 = -10,000 xung
                int minLimit = -10000;
                ushort minHigh = (ushort)((minLimit >> 16) & 0xFFFF);
                ushort minLow = (ushort)(minLimit & 0xFFFF);
                modbusMaster.WriteMultipleRegisters(slaveID, 0x0518, new ushort[] { minHigh, minLow });
            }
            catch (Exception ex)
            {

            }
        }

        
    }
}
