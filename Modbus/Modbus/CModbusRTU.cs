using System;
using System.Collections.Generic;
using System.Text;
using System.IO.Ports;
using System.Windows.Forms;
using System.Diagnostics;
using ThreadLock;
using Converter;
using Converter.Modbus;

namespace Modbus
{
    /// <summary>
    /// Modbus RTU通讯类，支持跨线程读写操作；如果是单线程操作，请将 EnableThreadLock 设置为 false 以提升性能
    /// 授权声明：本软件作者将代码开源，仅用于交流学习。如果有商用需求，请联系软件作者协商相关事宜；否则，软件作者保留相关法律赋予的权利。
    /// 免责声明：使用本软件的相关人员必须仔细检查代码并负全部责任，软件作者不承担任何可能的损失(包含可抗力和不可抗力因素)。
    /// </summary>
    public sealed class CModbusRTU : IModbusComm
    {
        /// <summary>
        /// Modbus RTU通讯类的构造函数
        /// </summary>
        /// <param name="DLLPassword">使用此DLL的密码</param>
        /// <param name="RS232CPortName">串口通讯的端口名称</param>
        /// <param name="baudrate">波段率</param>
        /// <param name="parity">奇偶校验</param>
        /// <param name="stopbits">停止位</param>
        /// <param name="writetimeout">写超时时间</param>
        /// <param name="readtimeout">读超时时间</param>
        /// <param name="ReadSlaveDataOnly">是否只读取从站返回的消息，默认：false</param>
        /// <param name="paraBytesFormat">命令参数：多字节数据的格式</param>
        /// <param name="writeKeepRegisterBytesFormat">写数据：多字节数据的格式</param>
        /// <param name="readRegisterBytesFormat">读输入和保持寄存器数据：多字节数据的格式</param>
        /// <param name="ThreadLockType">线程同步锁类型</param>
        public CModbusRTU(string DLLPassword, string RS232CPortName, eBaudRate baudrate = eBaudRate.Rate_19200,
        Parity parity = Parity.Even, StopBits stopbits = StopBits.One, int writetimeout = 1000,
        int readtimeout = 1000, bool ReadSlaveDataOnly = false, BytesFormat paraBytesFormat = BytesFormat.BADC, BytesFormat writeKeepRegisterBytesFormat = BytesFormat.BADC,
        BytesFormat readRegisterBytesFormat = BytesFormat.BADC, ThreadLock.LockerType ThreadLockType = LockerType.ExchangeLock)
        {
            try
            {
                if (DLLPassword == "ThomasPeng" || DLLPassword == "pengdongnan" || DLLPassword == "彭东南" || DLLPassword == "PDN")
                {
                }
                else
                {
                    for (; ; )
                    {
                    }
                }

                ParaBytesFormat = paraBytesFormat;
                WriteKeepRegisterBytesFormat = writeKeepRegisterBytesFormat;
                ReadInputRegisterBytesFormat = readRegisterBytesFormat;
                ReadKeepRegisterBytesFormat = readRegisterBytesFormat;
                ReadCoilBytesFormat = BytesFormat.ABCD;
                WriteCoilBytesFormat = BytesFormat.ABCD;
                bool bComPortIsAvailable = false;
                string[] AllAvailableComPorts = SerialPort.GetPortNames();
                if (null == AllAvailableComPorts || AllAvailableComPorts.Length <= 0)
                {
                    throw new Exception("计算机中没有任何可用串口通讯端口");
                }
                else
                {
                    for (int i = 0; i < AllAvailableComPorts.Length; i++)
                    {
                        if (RS232CPortName.ToUpper() == AllAvailableComPorts[i].ToUpper())
                        {
                            bComPortIsAvailable = true;
                        }

                    }
                }

                if (bComPortIsAvailable == false)
                {
                    throw new Exception("计算机中没有找到匹配的串口通讯端口：" + RS232CPortName);
                }

                switch (ThreadLockType)
                {
                    default:
                    case LockerType.AutoResetEventLock:
                        SyncLocker = new CAutoResetEventLock();
                        break;
                    case LockerType.ExchangeLock:
                        SyncLocker = new CExchangeLock();
                        break;
                }

                RS232CPort = new SerialPort();
                RS232CPort.PortName = RS232CPortName;
                BaudRateSetting(baudrate);
                RS232CPort.Encoding = Encoding.UTF8;
                RS232CPort.Parity = parity;
                RS232CPort.StopBits = stopbits;
                RS232CPort.WriteTimeout = writetimeout;
                RS232CPort.ReadTimeout = readtimeout;
                RS232CPort.Open();
                bReadSlaveDataOnly = ReadSlaveDataOnly;
                if (bReadSlaveDataOnly == true)
                {
                    RS232CPort.DataReceived += RS232CPort_DataReceived;
                }
            }
            catch (Exception ex)
            {
                Enqueue("通讯类初始化时发生错误：" + ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ")  通讯类初始化时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        #region "RTU-通讯方式下返回字符串中信息对应位置的定义"

        /// <summary>
        /// 接收到的字节数组中，从站地址的索引号[0]
        /// </summary>
        public const int PosIndexOfSlaveAddressInRTUReceivedBytes = 0x0;
        /// <summary>
        /// 接收到的字节数组中，功能码的索引号[1]
        /// </summary>
        public const int PosIndexOfFuncCodeInRTUReceivedBytes = 0x1;
        /// <summary>
        /// 接收到的字节数组中，数据字节长度(命令执行成功)的索引号[2]
        /// </summary>
        public const int PosIndexOfDataLengthInRTUReceivedBytes = 0x2;
        /// <summary>
        /// 接收到的字节数组中，错误码(命令执行失败)的索引号[2]
        /// </summary>
        public const int PosIndexOfErrorCodeInRTUReceivedBytes = 0x2;
        /// <summary>
        /// 接收到的字节数组中，接收到的有效数据开始的索引号[3]
        /// </summary>
        public const int PosIndexOfDataInRTUReceivedBytes = 0x3;

        #endregion

        #region "变量定义"

        /// <summary>
        /// 是否已经建立连接
        /// </summary>
        public bool IsConnected
        {
            get { return bIsConnected; }
        }

        /// <summary>
        /// 是否已经建立连接
        /// </summary>
        private bool bIsConnected = false;
        /// <summary>
        /// 释放资源标志
        /// </summary>
        bool bIsDisposing = false;
        /// <summary>
        /// 只读模式下，Modbus通讯返回数据的结果解析类对象的队列
        /// </summary>
        Queue<CReadbackData> qReceivedDataQueue = new Queue<CReadbackData>();
        /// <summary>
        /// 只读模式下，Modbus通讯返回数据的结果
        /// </summary>
        /// <returns></returns>
        public CReadbackData DequeueReceivedData()
        {
            if (qReceivedDataQueue.Count > 0)
            {
                return qReceivedDataQueue.Dequeue();
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// 是否只读取从站返回的消息，适用于从站主动发送的情况
        /// </summary>
        bool bReadSlaveDataOnly = false;
        /// <summary>
        /// 命令参数：多字节数据的格式
        /// </summary>
        public BytesFormat ParaBytesFormat { get; set; }
        /// <summary>
        /// 写线圈数据：多字节数据的格式
        /// </summary>
        public BytesFormat WriteCoilBytesFormat { get; set; }
        /// <summary>
        /// 写保持寄存器数据：多字节数据的格式
        /// </summary>
        public BytesFormat WriteKeepRegisterBytesFormat { get; set; }
        /// <summary>
        /// 读输入寄存器数据：多字节数据的格式
        /// </summary>
        public BytesFormat ReadInputRegisterBytesFormat { get; set; }
        /// <summary>
        /// 读保持寄存器数据：多字节数据的格式
        /// </summary>
        public BytesFormat ReadKeepRegisterBytesFormat { get; set; }
        /// <summary>
        /// 读线圈数据：多字节数据的格式，默认值：BytesFormat.ABCD
        /// </summary>
        public BytesFormat ReadCoilBytesFormat { get; set; }
        /// <summary>
        /// 读输入离散信号数据：多字节数据的格式，默认值：BytesFormat.ABCD
        /// </summary>
        public BytesFormat ReadInputIOBytesFormat { get; set; }
        /// <summary>
        /// 回复帧的最小长度:2
        /// </summary>
        const int iMinLengthOfResponse = 2;
        /// <summary>
        /// 同步线程锁
        /// </summary>
        ITheadLock SyncLocker = null;
        /// <summary>
        /// 启用线程锁标志
        /// </summary>
        bool bEnableThreadLock = true;
        /// <summary>
        /// 启用线程锁标志
        /// </summary>
        public bool EnableThreadLock
        {
            get { return bEnableThreadLock; }
            set { bEnableThreadLock = value; }
        }

        /// <summary>
        /// 软件作者：彭东南, southeastofstar@163.com
        /// </summary>
        public string Author
        {
            get { return "【软件作者：彭东南, southeastofstar@163.com】"; }
        }

        /// <summary>
        /// System.Threading.Thread.Sleep的等待时间(ms)
        /// </summary>
        int iSleepTime = 1;
        /// <summary>
        /// 等待从站返回信息的时间(ms)
        /// </summary>
        int iWaitFeedbackTime = 500;
        /// <summary>
        /// 等待从站返回信息的时间(ms)，范围：50~3000
        /// </summary>
        public int WaitFeedbackTime
        {
            get { return iWaitFeedbackTime; }
            set
            {
                if (value >= 50 && value <= 3000)
                {
                    iWaitFeedbackTime = value;
                }
            }
        }

        /// <summary>
        /// 保存接收到的信息到日志
        /// </summary>
        public bool bSaveReceivedStringToLog = true;
        /// <summary>
        /// 保存发送的信息到日志
        /// </summary>
        public bool bSaveSendStringToLog = true;
        /// <summary>
        /// 错误信息队列
        /// </summary>
        private Queue<string> qErrorMsg = new Queue<string>();
        /// <summary>
        /// 是否使用提示对话框显示信息，默认 false
        /// </summary>
        public bool ShowMessageDialog = false;
        /// <summary>
        /// 获取或设置奇偶校验检查协议
        /// </summary>
        public Parity Parity
        {
            get { return RS232CPort.Parity; }
            set { RS232CPort.Parity = value; }
        }

        /// <summary>
        /// 获取或设置每个字节的标准停止位数
        /// </summary>
        public StopBits StopBits
        {
            get { return RS232CPort.StopBits; }
            set { RS232CPort.StopBits = value; }
        }

        /// <summary>
        /// 获取或设置写操作未完成时发生超时之前的毫秒数
        /// </summary>
        public int WriteTimeout
        {
            get { return RS232CPort.WriteTimeout; }
            set { RS232CPort.WriteTimeout = value; }
        }

        /// <summary>
        /// 获取或设置读取操作未完成时发生超时之前的毫秒数
        /// </summary>
        public int ReadTimeout
        {
            get { return RS232CPort.ReadTimeout; }
            set { RS232CPort.ReadTimeout = value; }
        }

        /// <summary>
        /// 进行Modbus RTU通讯的串口实例化对象
        /// </summary>
        SerialPort RS232CPort = null;
        /// <summary>
        /// 波特率设置
        /// </summary>
        eBaudRate eBaudRateSetting = eBaudRate.Rate_19200;
        /// <summary>
        /// 串口是否已经打开
        /// </summary>
        public bool IsOpened
        {
            get
            {
                if (null == RS232CPort)
                {
                    return false;
                }
                else
                {
                    return RS232CPort.IsOpen;
                }
            }
        }

        #region "波特率设置"
        /// <summary>
        /// 获取当前的波特率(bps)；设置波特率时，先使用函数BaudRateSetting设置为eBaudRate.Rate_UserDefine
        /// </summary>
        public int BaudRate
        {
            get { return RS232CPort.BaudRate; }
            set
            {
                if (eBaudRateSetting == eBaudRate.Rate_UserDefine)
                {
                    RS232CPort.BaudRate = value;
                }
            }
        }

        /// <summary>
        /// 设置波特率(bps)
        /// </summary>
        /// <param name="BaudValue"></param>
        public void BaudRateSetting(eBaudRate BaudValue)
        {
            #region "波特率设置"
            eBaudRateSetting = BaudValue;
            switch (BaudValue)
            {
                case eBaudRate.Rate_75:
                    RS232CPort.BaudRate = 75;
                    break;
                case eBaudRate.Rate_110:
                    RS232CPort.BaudRate = 110;
                    break;
                case eBaudRate.Rate_134:
                    RS232CPort.BaudRate = 134;
                    break;
                case eBaudRate.Rate_150:
                    RS232CPort.BaudRate = 150;
                    break;
                case eBaudRate.Rate_300:
                    RS232CPort.BaudRate = 300;
                    break;
                case eBaudRate.Rate_600:
                    RS232CPort.BaudRate = 600;
                    break;
                case eBaudRate.Rate_1200:
                    RS232CPort.BaudRate = 1200;
                    break;
                case eBaudRate.Rate_1800:
                    RS232CPort.BaudRate = 1800;
                    break;
                case eBaudRate.Rate_2400:
                    RS232CPort.BaudRate = 2400;
                    break;
                case eBaudRate.Rate_4800:
                    RS232CPort.BaudRate = 4800;
                    break;
                case eBaudRate.Rate_7200:
                    RS232CPort.BaudRate = 7200;
                    break;
                case eBaudRate.Rate_9600:
                    RS232CPort.BaudRate = 9600;
                    break;
                case eBaudRate.Rate_14400:
                    RS232CPort.BaudRate = 14400;
                    break;
                case eBaudRate.Rate_19200:
                    RS232CPort.BaudRate = 19200;
                    break;
                case eBaudRate.Rate_38400:
                    RS232CPort.BaudRate = 38400;
                    break;
                case eBaudRate.Rate_57600:
                    RS232CPort.BaudRate = 57600;
                    break;
                case eBaudRate.Rate_115200:
                    RS232CPort.BaudRate = 115200;
                    break;
                case eBaudRate.Rate_128000:
                    RS232CPort.BaudRate = 128000;
                    break;
                default:
                    break;
            }

            #endregion
        }

        #endregion

        #endregion

        #region "读 - ok-ok"

        #region "读单个/多个线圈 - ok-ok"

        /// <summary>
        /// 读取从站单个线圈状态(位 - bit)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回线圈的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilBit(byte DeviceAddress, ushort BeginAddress, ref bool Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, 1);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        if (byReadData[PosIndexOfDataInRTUReceivedBytes] == 1)
                        {
                            Value = true;
                        }
                        else
                        {
                            Value = false;
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站多个字节的线圈状态(1个字节 = 8位 - bit)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字节</param>
        /// <param name="Value">返回线圈的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilBit(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref bool[] Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, ReadDataLength * 8);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ByteToBitArray(CConverter.CopyBytes(byReadData, (uint)ReadDataLength, (uint)PosIndexOfDataInRTUReceivedBytes));
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站1个字节的线圈状态(1个字节 = 8位 - bit)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回线圈的1个字节当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilByte(byte DeviceAddress, ushort BeginAddress, ref byte Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 1 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, 1 * 8);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = byReadData[PosIndexOfDataInRTUReceivedBytes];
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站多个字节的线圈状态(1个字节 = 8位 - bit)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字节</param>
        /// <param name="Value">返回线圈的字节当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilByte(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref byte[] Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, ReadDataLength * 8);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.CopyBytes(byReadData, (uint)ReadDataLength, (uint)PosIndexOfDataInRTUReceivedBytes);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站1个字节的线圈状态(1个字节 = 8位 - bit)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回线圈的1个字节当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilByte(byte DeviceAddress, ushort BeginAddress, ref sbyte Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 1 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, 1 * 8);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = (sbyte)byReadData[PosIndexOfDataInRTUReceivedBytes];
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站多个字节的线圈状态(1个字节 = 8位 - bit)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字节</param>
        /// <param name="Value">返回线圈的字节当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilByte(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref sbyte[] Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, ReadDataLength * 8);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        sbyte[] byResultData = new sbyte[ReadDataLength];
                        for (int i = 0; i < ReadDataLength; i++)
                        {
                            byResultData[i] = (sbyte)byReadData[PosIndexOfDataInRTUReceivedBytes + i];
                        }

                        Value = byResultData;
                        byResultData = null;
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站1个字的线圈状态(位 - bit)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回线圈的1个字当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilWord(byte DeviceAddress, ushort BeginAddress, ref short Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, 2 * 8);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToInt16(CConverter.CopyBytes(byReadData, (uint)2, (uint)PosIndexOfDataInRTUReceivedBytes), ReadCoilBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站多个字的线圈状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字</param>
        /// <param name="Value">返回线圈多个字的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilWord(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref short[] Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, ReadDataLength * 2 * 8);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToInt16Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (Byte)ByteCount.Short), (uint)PosIndexOfDataInRTUReceivedBytes), ReadCoilBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站1个字的线圈状态(位 - bit)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回线圈的1个字当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilWord(byte DeviceAddress, ushort BeginAddress, ref ushort Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, 2 * 8);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToUInt16(CConverter.CopyBytes(byReadData, (Byte)ByteCount.UShort, (uint)PosIndexOfDataInRTUReceivedBytes), ReadCoilBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站多个字的线圈状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字</param>
        /// <param name="Value">返回线圈多个字的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilWord(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref ushort[] Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, ReadDataLength * 2 * 8);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToUInt16Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (Byte)ByteCount.UShort), (uint)PosIndexOfDataInRTUReceivedBytes), ReadCoilBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站2个字的线圈状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回线圈的2个字当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilWord(byte DeviceAddress, ushort BeginAddress, ref int Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, 4 * 8);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToInt32(CConverter.CopyBytes(byReadData, (uint)(1 * (Byte)ByteCount.Int), (uint)PosIndexOfDataInRTUReceivedBytes), ReadCoilBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站多个双字的线圈状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：双字</param>
        /// <param name="Value">返回线圈多个双字的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilWord(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref int[] Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, ReadDataLength * 4 * 8);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToInt32Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (Byte)ByteCount.Int), (uint)PosIndexOfDataInRTUReceivedBytes), ReadCoilBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站2个字的线圈状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回线圈的2个字当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilWord(byte DeviceAddress, ushort BeginAddress, ref uint Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, 4 * 8);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToUInt32(CConverter.CopyBytes(byReadData, (uint)(1 * (Byte)ByteCount.UInt), (uint)PosIndexOfDataInRTUReceivedBytes), ReadCoilBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站多个双字的线圈状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：双字</param>
        /// <param name="Value">返回线圈多个双字的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilWord(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref uint[] Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, ReadDataLength * 4 * 8);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToUInt32Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (Byte)ByteCount.UInt), (uint)PosIndexOfDataInRTUReceivedBytes), ReadCoilBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }
        
        /// <summary>
        /// 读取从站2个双字的线圈状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回线圈的2个双字当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilWord(byte DeviceAddress, ushort BeginAddress, ref long Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, 8 * 8);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToInt64(CConverter.CopyBytes(byReadData, (uint)(1 * (Byte)ByteCount.Long), (uint)PosIndexOfDataInRTUReceivedBytes), ReadCoilBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站多个双字的线圈状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：双字</param>
        /// <param name="Value">返回线圈多个双字的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilWord(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref long[] Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, ReadDataLength * 8 * 8);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToInt64Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (Byte)ByteCount.Long), (uint)PosIndexOfDataInRTUReceivedBytes), ReadCoilBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站2个双字的线圈状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回线圈的2个双字当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilWord(byte DeviceAddress, ushort BeginAddress, ref ulong Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, 8 * 8);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToUInt64(CConverter.CopyBytes(byReadData, (uint)(1 * (Byte)ByteCount.ULong), (uint)PosIndexOfDataInRTUReceivedBytes), ReadCoilBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站多个双字的线圈状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：双字</param>
        /// <param name="Value">返回线圈多个双字的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilWord(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref ulong[] Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, ReadDataLength * 8 * 8);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToUInt64Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (Byte)ByteCount.ULong), (uint)PosIndexOfDataInRTUReceivedBytes), ReadCoilBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        #endregion

        #region "读单个/多个输入位的状态 - ok-ok"

        /// <summary>
        /// 读取从站单个输入位的状态(位 - bit)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回单个输入位的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputBit(byte DeviceAddress, ushort BeginAddress, ref bool Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, 1);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        if (byReadData[PosIndexOfDataInRTUReceivedBytes] == 1)
                        {
                            Value = true;
                        }
                        else
                        {
                            Value = false;
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站多个字节的输入状态(1个字节 = 8位 - bit)，函数是以字节为读取数据长度单位，返回的值是以字为单位(2个字节的倍数)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字节</param>
        /// <param name="Value">返回多个字节输入状态的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputBit(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref bool[] Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, ReadDataLength * 8);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ByteToBitArray(CConverter.CopyBytes(byReadData, (uint)ReadDataLength, (uint)PosIndexOfDataInRTUReceivedBytes));
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站1个字节的输入状态(位 - bit)，函数是以字节为读取数据长度单位，返回的值是以字为单位(2个字节的倍数)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回输入状态1个字节的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputByte(byte DeviceAddress, ushort BeginAddress, ref byte Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 1 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, 1 * 8);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = byReadData[PosIndexOfDataInRTUReceivedBytes];
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站多个字节的输入状态(位 - bit)，函数是以字节为读取数据长度单位，返回的值是以字为单位(2个字节的倍数)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字节</param>
        /// <param name="Value">返回输入状态多个字节的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputByte(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref byte[] Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, ReadDataLength * 8);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.CopyBytes(byReadData, (uint)ReadDataLength, (uint)PosIndexOfDataInRTUReceivedBytes);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站1个字节的输入状态(位 - bit)，函数是以字节为读取数据长度单位，返回的值是以字为单位(2个字节的倍数)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回输入状态1个字节的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputByte(byte DeviceAddress, ushort BeginAddress, ref sbyte Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 1 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, 1 * 8);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = (sbyte)byReadData[PosIndexOfDataInRTUReceivedBytes];
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站多个字节的输入状态(位 - bit)，函数是以字节为读取数据长度单位，返回的值是以字为单位(2个字节的倍数)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字节</param>
        /// <param name="Value">返回输入状态多个字节的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputByte(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref sbyte[] Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, ReadDataLength * 8);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        sbyte[] byResultData = new sbyte[ReadDataLength];
                        for (int i = 0; i < ReadDataLength; i++)
                        {
                            byResultData[i] = (sbyte)byReadData[PosIndexOfDataInRTUReceivedBytes + i];
                        }

                        Value = byResultData;
                        byResultData = null;
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站1个字的输入状态，函数是以字为读取数据长度单位，返回的值是以字为单位(2个字节的倍数)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回输入状态1个字的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputWord(byte DeviceAddress, ushort BeginAddress, ref short Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 1 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, 2 * 8);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToInt16(CConverter.CopyBytes(byReadData, (uint)(1 * (Byte)ByteCount.Short), (uint)PosIndexOfDataInRTUReceivedBytes), ReadInputIOBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站多个字的输入状态，函数是以字节为读取数据长度单位，返回的值是以字为单位(2个字节的倍数)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字</param>
        /// <param name="Value">返回输入状态多个字的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputWord(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref short[] Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, ReadDataLength * 2 * 8);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToInt16Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (Byte)ByteCount.Short), (uint)PosIndexOfDataInRTUReceivedBytes), ReadInputIOBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站1个字的输入状态，函数是以字为读取数据长度单位，返回的值是以字为单位(2个字节的倍数)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回输入状态1个字的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputWord(byte DeviceAddress, ushort BeginAddress, ref ushort Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 1 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, 2 * 8);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToUInt16(CConverter.CopyBytes(byReadData, (uint)(1 * (Byte)ByteCount.UShort), (uint)PosIndexOfDataInRTUReceivedBytes), ReadInputIOBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站多个字的输入状态，函数是以字节为读取数据长度单位，返回的值是以字为单位(2个字节的倍数)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字</param>
        /// <param name="Value">返回输入状态多个字的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputWord(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref ushort[] Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, ReadDataLength * 2 * 8);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToUInt16Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (Byte)ByteCount.UShort), (uint)PosIndexOfDataInRTUReceivedBytes), ReadInputIOBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站1个双字的输入状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回输入状态1个双字的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputWord(byte DeviceAddress, ushort BeginAddress, ref int Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, 4 * 8);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToInt32(CConverter.CopyBytes(byReadData, (uint)(1 * (Byte)ByteCount.Int), (uint)PosIndexOfDataInRTUReceivedBytes), ReadInputIOBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站多个双字的输入状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：双字</param>
        /// <param name="Value">返回输入状态多个双字的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputWord(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref int[] Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, ReadDataLength * 4 * 8);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToInt32Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (Byte)ByteCount.Int), (uint)PosIndexOfDataInRTUReceivedBytes), ReadInputIOBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站1个双字的输入状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回输入状态1个双字的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputWord(byte DeviceAddress, ushort BeginAddress, ref uint Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, 4 * 8);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToUInt32(CConverter.CopyBytes(byReadData, (uint)(1 * (Byte)ByteCount.UInt), (uint)PosIndexOfDataInRTUReceivedBytes), ReadInputIOBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站多个双字的输入状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：双字</param>
        /// <param name="Value">返回输入状态多个双字的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputWord(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref uint[] Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, ReadDataLength * 4 * 8);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToUInt32Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (Byte)ByteCount.UInt), (uint)PosIndexOfDataInRTUReceivedBytes), ReadInputIOBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站2个双字的输入状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回2个双字输入状态的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputWord(byte DeviceAddress, ushort BeginAddress, ref long Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, 8 * 8);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToInt64(CConverter.CopyBytes(byReadData, (uint)(1 * (Byte)ByteCount.Long), (uint)PosIndexOfDataInRTUReceivedBytes), ReadInputIOBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站多个双字的输入状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：双字</param>
        /// <param name="Value">返回输入状态多个双字的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputWord(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref long[] Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, ReadDataLength * 8 * 8);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToInt64Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (Byte)ByteCount.Long), (uint)PosIndexOfDataInRTUReceivedBytes), ReadInputIOBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站2个双字的输入状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回2个双字输入状态的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputWord(byte DeviceAddress, ushort BeginAddress, ref ulong Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, 8 * 8);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToUInt64(CConverter.CopyBytes(byReadData, (uint)(1 * (Byte)ByteCount.ULong), (uint)PosIndexOfDataInRTUReceivedBytes), ReadInputIOBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站多个双字的输入状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：双字</param>
        /// <param name="Value">返回输入状态多个双字的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputWord(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref ulong[] Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, ReadDataLength * 8 * 8);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToUInt64Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (Byte)ByteCount.ULong), (uint)PosIndexOfDataInRTUReceivedBytes), ReadInputIOBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        #endregion

        #region "读单个/多个输入寄存器的状态 - ok-ok"

        /// <summary>
        /// 读取从站输入寄存器的当前值(short: -32,768 到 32,767)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回输入寄存器的当前值(short: -32,768 到 32,767)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputRegister(byte DeviceAddress, ushort BeginAddress, ref short Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, 1);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToInt16(CConverter.CopyBytes(byReadData, (uint)(1 * (Byte)ByteCount.Short), (uint)PosIndexOfDataInRTUReceivedBytes), ReadInputRegisterBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站输入寄存器的当前值(short: -32,768 到 32,767)，函数是以字节为读取数据长度单位
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度</param>
        /// <param name="Value">返回输入寄存器的当前值(short: -32,768 到 32,767)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputRegister(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref short[] Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, ReadDataLength);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToInt16Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (Byte)ByteCount.Short), (uint)PosIndexOfDataInRTUReceivedBytes), ReadInputRegisterBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站输入寄存器的当前值(ushort: 0 到 65535)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回输入寄存器的当前值(ushort: 0 到 65535)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputRegister(byte DeviceAddress, ushort BeginAddress, ref ushort Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, 1);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToUInt16(CConverter.CopyBytes(byReadData, (uint)(1 * (Byte)ByteCount.UShort), (uint)PosIndexOfDataInRTUReceivedBytes), ReadInputRegisterBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站输入寄存器的当前值(ushort: 0 到 65535)，函数是以字节为读取数据长度单位
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度</param>
        /// <param name="Value">返回输入寄存器的当前值(ushort: 0 到 65535)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputRegister(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref ushort[] Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, ReadDataLength);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToUInt16Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (Byte)ByteCount.UShort), (uint)PosIndexOfDataInRTUReceivedBytes), ReadInputRegisterBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站输入寄存器的当前值(int)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回输入寄存器的当前值(int)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputRegister(byte DeviceAddress, ushort BeginAddress, ref int Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, 2);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToInt32(CConverter.CopyBytes(byReadData, (uint)(1 * (Byte)ByteCount.Int), (uint)PosIndexOfDataInRTUReceivedBytes), ReadInputRegisterBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站输入寄存器的当前值(int)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度</param>
        /// <param name="Value">返回输入寄存器的当前值(int)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputRegister(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref int[] Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, ReadDataLength * 2);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToInt32Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (Byte)ByteCount.Int), (uint)PosIndexOfDataInRTUReceivedBytes), ReadInputRegisterBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站输入寄存器的当前值(uint)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回输入寄存器的当前值(uint)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputRegister(byte DeviceAddress, ushort BeginAddress, ref uint Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, 2);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToUInt32(CConverter.CopyBytes(byReadData, (uint)(1 * (Byte)ByteCount.UInt), (uint)PosIndexOfDataInRTUReceivedBytes), ReadInputRegisterBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站输入寄存器的当前值(uint)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度</param>
        /// <param name="Value">返回输入寄存器的当前值(uint)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputRegister(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref uint[] Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, ReadDataLength * 2);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToUInt32Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (Byte)ByteCount.UInt), (uint)PosIndexOfDataInRTUReceivedBytes), ReadInputRegisterBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站输入寄存器的当前值(float)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回输入寄存器的当前值(float)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputRegister(byte DeviceAddress, ushort BeginAddress, ref float Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, 2);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToFloat(CConverter.CopyBytes(byReadData, (uint)(1 * (Byte)ByteCount.Float), (uint)PosIndexOfDataInRTUReceivedBytes), ReadInputRegisterBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站输入寄存器的当前值(float)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度</param>
        /// <param name="Value">返回输入寄存器的当前值(float)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputRegister(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref float[] Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, ReadDataLength * 2);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToFloatArray(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (Byte)ByteCount.Float), (uint)PosIndexOfDataInRTUReceivedBytes), ReadInputRegisterBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站输入寄存器的当前值(double)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回输入寄存器的当前值(double)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputRegister(byte DeviceAddress, ushort BeginAddress, ref double Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, 4);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToDouble(CConverter.CopyBytes(byReadData, (uint)(1 * (Byte)ByteCount.Double), (uint)PosIndexOfDataInRTUReceivedBytes), ReadInputRegisterBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站输入寄存器的当前值(double)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度</param>
        /// <param name="Value">返回输入寄存器的当前值(double)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputRegister(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref double[] Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 4 * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, ReadDataLength * 4);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToDoubleArray(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (Byte)ByteCount.Double), (uint)PosIndexOfDataInRTUReceivedBytes), ReadInputRegisterBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 【测试读取数据时不能像其它功能码一样正确读取long值，在int范围内可以正确读取，其它会对应不上，待更多测试】
        /// 读取从站2个双字的输入寄存器的当前值
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回2个双字输入寄存器的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputRegister(byte DeviceAddress, ushort BeginAddress, ref long Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, 4);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToInt64(CConverter.CopyBytes(byReadData, (uint)(1 * (Byte)ByteCount.Long), (uint)PosIndexOfDataInRTUReceivedBytes), ReadInputRegisterBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 【测试读取数据时不能像其它功能码一样正确读取long值，在int范围内可以正确读取，其它会对应不上，待更多测试】
        /// 读取从站多个双字的输入寄存器的当前值
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：双字</param>
        /// <param name="Value">返回输入寄存器多个双字的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputRegister(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref long[] Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, ReadDataLength * 4);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToInt64Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (Byte)ByteCount.Long), (uint)PosIndexOfDataInRTUReceivedBytes), ReadInputRegisterBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 【测试读取数据时不能像其它功能码一样正确读取long值，在int范围内可以正确读取，其它会对应不上，待更多测试】
        /// 读取从站2个双字的输入寄存器的当前值
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回2个双字输入寄存器的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputRegister(byte DeviceAddress, ushort BeginAddress, ref ulong Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, 4);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToUInt64(CConverter.CopyBytes(byReadData, (uint)(1 * (Byte)ByteCount.ULong), (uint)PosIndexOfDataInRTUReceivedBytes), ReadInputRegisterBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 【测试读取数据时不能像其它功能码一样正确读取long值，在int范围内可以正确读取，其它会对应不上，待更多测试】
        /// 读取从站多个双字的输入寄存器的当前值
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：双字</param>
        /// <param name="Value">返回输入寄存器多个双字的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputRegister(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref ulong[] Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, ReadDataLength * 4);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToUInt64Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (Byte)ByteCount.ULong), (uint)PosIndexOfDataInRTUReceivedBytes), ReadInputRegisterBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        #endregion

        #region "读单个/多个保持寄存器的状态 - ok-ok"

        /// <summary>
        /// 读取从站保持寄存器的当前值(short: -32,768 到 32,767)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回保持寄存器的当前值(short: -32,768 到 32,767)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadKeepRegister(byte DeviceAddress, ushort BeginAddress, ref short Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, 1);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToInt16(CConverter.CopyBytes(byReadData, (uint)(1 * (Byte)ByteCount.Short), (uint)PosIndexOfDataInRTUReceivedBytes), ReadKeepRegisterBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站保持寄存器的当前值(short: -32,768 到 32,767)，函数是以字节为读取数据长度单位
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字节</param>
        /// <param name="Value">返回保持寄存器的当前值(short: -32,768 到 32,767)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadKeepRegister(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref short[] Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, ReadDataLength);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToInt16Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (Byte)ByteCount.Short), (uint)PosIndexOfDataInRTUReceivedBytes), ReadKeepRegisterBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站保持寄存器的当前值(ushort: 0 到 65535)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回保持寄存器的当前值(ushort: 0 到 65535)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadKeepRegister(byte DeviceAddress, ushort BeginAddress, ref ushort Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, 1);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToUInt16(CConverter.CopyBytes(byReadData, (uint)(1 * (Byte)ByteCount.UShort), (uint)PosIndexOfDataInRTUReceivedBytes), ReadKeepRegisterBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站保持寄存器的当前值(ushort: 0 到 65535)，函数是以字节为读取数据长度单位
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字节</param>
        /// <param name="Value">返回保持寄存器的当前值(ushort: 0 到 65535)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadKeepRegister(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref ushort[] Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, ReadDataLength);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToUInt16Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (Byte)ByteCount.UShort), (uint)PosIndexOfDataInRTUReceivedBytes), ReadKeepRegisterBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站保持寄存器的当前值(int)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回保持寄存器的当前值(int)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadKeepRegister(byte DeviceAddress, ushort BeginAddress, ref int Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, 2);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToInt32(CConverter.CopyBytes(byReadData, (uint)(1 * (Byte)ByteCount.Int), (uint)PosIndexOfDataInRTUReceivedBytes), ReadKeepRegisterBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站保持寄存器的当前值(int)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字节</param>
        /// <param name="Value">返回保持寄存器的当前值(int)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadKeepRegister(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref int[] Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, ReadDataLength * 2);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToInt32Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (Byte)ByteCount.Int), (uint)PosIndexOfDataInRTUReceivedBytes), ReadKeepRegisterBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站保持寄存器的当前值(uint)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回保持寄存器的当前值(uint)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadKeepRegister(byte DeviceAddress, ushort BeginAddress, ref uint Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, 2);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToUInt32(CConverter.CopyBytes(byReadData, (uint)(1 * (Byte)ByteCount.UInt), (uint)PosIndexOfDataInRTUReceivedBytes), ReadKeepRegisterBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站保持寄存器的当前值(uint)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字节</param>
        /// <param name="Value">返回保持寄存器的当前值(uint)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadKeepRegister(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref uint[] Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, ReadDataLength * 2);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToUInt32Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (Byte)ByteCount.UInt), (uint)PosIndexOfDataInRTUReceivedBytes), ReadKeepRegisterBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站float保持寄存器的当前值(32位浮点值)(float  -3.4×10的38次方 到 +3.4×10的38次方, 精度：7 位)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回float保持寄存器的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadKeepRegister(byte DeviceAddress, ushort BeginAddress, ref float Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, 2);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToFloat(CConverter.CopyBytes(byReadData, (uint)(1 * (Byte)ByteCount.Float), (uint)PosIndexOfDataInRTUReceivedBytes), ReadKeepRegisterBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站float保持寄存器的当前值(32位浮点值)(float  -3.4×10的38次方 到 +3.4×10的38次方, 精度：7 位)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：float</param>
        /// <param name="Value">返回float保持寄存器的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadKeepRegister(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref float[] Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, ReadDataLength * 2);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToFloatArray(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (Byte)ByteCount.Float), (uint)PosIndexOfDataInRTUReceivedBytes), ReadKeepRegisterBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站double(64位浮点值)值到从站保持寄存器(±5.0×10的−324次方 到 ±1.7×10的308次方   精度:15到16位)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回double保持寄存器的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadKeepRegister(byte DeviceAddress, ushort BeginAddress, ref double Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, 4);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToDouble(CConverter.CopyBytes(byReadData, (uint)(1 * (Byte)ByteCount.Double), (uint)PosIndexOfDataInRTUReceivedBytes), ReadKeepRegisterBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站double(64位浮点值)值到从站保持寄存器(±5.0×10的−324次方 到 ±1.7×10的308次方   精度:15到16位)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：double</param>
        /// <param name="Value">返回double保持寄存器的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadKeepRegister(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref double[] Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, ReadDataLength * 4);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToDoubleArray(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (Byte)ByteCount.Double), (uint)PosIndexOfDataInRTUReceivedBytes), ReadKeepRegisterBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站long(64位整数值)值到从站保持寄存器(-9,223,372,036,854,775,808 到 9,223,372,036,854,775,807, 有符号 64 位整数)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回long保持寄存器的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadKeepRegister(byte DeviceAddress, ushort BeginAddress, ref long Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, 4);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToInt64(CConverter.CopyBytes(byReadData, (uint)(1 * (Byte)ByteCount.Long), (uint)PosIndexOfDataInRTUReceivedBytes), ReadKeepRegisterBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站long(64位整数值)值到从站保持寄存器(-9,223,372,036,854,775,808 到 9,223,372,036,854,775,807, 有符号 64 位整数)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：double</param>
        /// <param name="Value">返回long保持寄存器的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadKeepRegister(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref long[] Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, ReadDataLength * 4);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToInt64Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (Byte)ByteCount.Long), (uint)PosIndexOfDataInRTUReceivedBytes), ReadKeepRegisterBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站ulong值到从站保持寄存器(0 到 18,446,744,073,709,551,615)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回ulong保持寄存器的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadKeepRegister(byte DeviceAddress, ushort BeginAddress, ref ulong Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, 4);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToUInt64(CConverter.CopyBytes(byReadData, (uint)(1 * (Byte)ByteCount.ULong), (uint)PosIndexOfDataInRTUReceivedBytes), ReadKeepRegisterBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站ulong值到从站保持寄存器(0 到 18,446,744,073,709,551,615)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：double</param>
        /// <param name="Value">返回ulong保持寄存器的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadKeepRegister(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref ulong[] Value)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, ReadDataLength * 4);

                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        Value = CConverter.ToUInt64Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (Byte)ByteCount.ULong), (uint)PosIndexOfDataInRTUReceivedBytes), ReadKeepRegisterBytesFormat, 0);
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        #endregion

        #endregion

        #region "写 - ok-ok"

        #region "写单个/多个线圈 - ok-ok"

        /// <summary>
        /// 写从站线圈(位 - bit)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">起始地址</param>
        /// <param name="IsOn">设置线圈的当前值：true - On; false - Off</param>
        /// <returns>是否成功执行命令</returns>
        public bool WriteCoilBit(byte DeviceAddress, ushort BeginAddress, bool IsOn)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                byte[] byWriteData = new byte[2];
                if (IsOn == true)
                {
                    byWriteData[0] = 0xFF;
                    byWriteData[1] = 0X00;
                }
                else
                {
                    byWriteData[0] = 0x00;
                    byWriteData[1] = 0X00;
                }

                byWriteData = CConverter.Reorder2BytesData(byWriteData, WriteCoilBytesFormat, 0);
                byte[] byDataToBeSent = PackSingleWriteCmd(DeviceAddress, ModbusFuncCode.WriteCoil, BeginAddress, byWriteData);
                if (null == byDataToBeSent)
                {
                    return false;
                }


                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        /// <summary>
        /// 写从站线圈字节(位 - bit；1字节 = 8bit)，写线圈的布尔数组长度必须是8的整数倍
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">起始地址</param>
        /// <param name="SetValue">设置8*N个线圈的当前值数组：true - On; false - Off</param>
        /// <returns>是否成功执行命令</returns>
        public bool WriteCoilBit(byte DeviceAddress, ushort BeginAddress, bool[] SetValue)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (null == SetValue || SetValue.Length < 1)
                {
                    throw new Exception("设置值数组不能为空");
                }

                if (SetValue.Length % 8 != 0)
                {
                    throw new Exception("bool[]数组的长度不是8的整数，即不是以整字节的形式，请修改参数数组的长度");
                }

                if (BeginAddress + SetValue.Length > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byWriteData = CConverter.BitArrayToByte(SetValue);
                byWriteData = CConverter.ReorderBytesData(byWriteData, WriteCoilBytesFormat, 0);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length, byWriteData);
                if (null == byDataToBeSent)
                {
                    return false;
                }


                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ")  通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        /// <summary>
        /// 写从站1个字节的线圈
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">起始地址</param>
        /// <param name="SetValue">设置1个字节线圈的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool WriteCoilByte(byte DeviceAddress, ushort BeginAddress, byte SetValue)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress + 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byWriteData = new byte[] { SetValue };
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, 8, byWriteData);
                if (null == byDataToBeSent)
                {
                    return false;
                }


                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        /// <summary>
        /// 写从站N个字节的线圈
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">起始地址</param>
        /// <param name="SetValue">设置N个字节线圈的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool WriteCoilByte(byte DeviceAddress, ushort BeginAddress, byte[] SetValue)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (null == SetValue || SetValue.Length < 1)
                {
                    throw new Exception("设置值数组不能为空");
                }

                if (BeginAddress + SetValue.Length * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byWriteData = SetValue;
                byWriteData = CConverter.ReorderBytesData(byWriteData, WriteCoilBytesFormat, 0);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length * 8, byWriteData);
                if (null == byDataToBeSent)
                {
                    return false;
                }


                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        /// <summary>
        /// 写从站1个字节的线圈
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">起始地址</param>
        /// <param name="SetValue">设置1个字节线圈的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool WriteCoilByte(byte DeviceAddress, ushort BeginAddress, sbyte SetValue)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress + 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byWriteData = new byte[] { (byte)SetValue };
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, 8, byWriteData);
                if (null == byDataToBeSent)
                {
                    return false;
                }


                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        /// <summary>
        /// 写从站N个字节的线圈
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">起始地址</param>
        /// <param name="SetValue">设置N个字节线圈的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool WriteCoilByte(byte DeviceAddress, ushort BeginAddress, sbyte[] SetValue)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (null == SetValue || SetValue.Length < 1)
                {
                    throw new Exception("设置值数组不能为空");
                }

                if (BeginAddress + SetValue.Length * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byWriteData = new byte[SetValue.Length];
                for (int i = 0; i < SetValue.Length; i++)
                {
                    byWriteData[i] = (byte)SetValue[i];
                }
                byWriteData = CConverter.ReorderBytesData(byWriteData, WriteCoilBytesFormat, 0);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length * 8, byWriteData);
                if (null == byDataToBeSent)
                {
                    return false;
                }


                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        /// <summary>
        /// 写从站1个字的线圈
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">起始地址</param>
        /// <param name="SetValue">设置1个字线圈的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool WriteCoilWord(byte DeviceAddress, ushort BeginAddress, short SetValue)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, 2 * 8, byWriteData);
                if (null == byDataToBeSent)
                {
                    return false;
                }


                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        /// <summary>
        /// 写从站N个字的线圈
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">起始地址</param>
        /// <param name="SetValue">设置N个字线圈的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool WriteCoilWord(byte DeviceAddress, ushort BeginAddress, short[] SetValue)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (null == SetValue || SetValue.Length < 1)
                {
                    throw new Exception("设置值数组不能为空");
                }

                if (BeginAddress + SetValue.Length * 2 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length * 2 * 8, byWriteData);
                if (null == byDataToBeSent)
                {
                    return false;
                }


                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        /// <summary>
        /// 写从站1个字的线圈
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">起始地址</param>
        /// <param name="SetValue">设置1个字线圈的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool WriteCoilWord(byte DeviceAddress, ushort BeginAddress, ushort SetValue)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, 2 * 8, byWriteData);
                if (null == byDataToBeSent)
                {
                    return false;
                }


                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        /// <summary>
        /// 写从站N个字的线圈
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">起始地址</param>
        /// <param name="SetValue">设置N个字线圈的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool WriteCoilWord(byte DeviceAddress, ushort BeginAddress, ushort[] SetValue)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (null == SetValue || SetValue.Length < 1)
                {
                    throw new Exception("设置值数组不能为空");
                }

                if (BeginAddress + SetValue.Length * 2 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length * 2 * 8, byWriteData);
                if (null == byDataToBeSent)
                {
                    return false;
                }


                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        /// <summary>
        /// 写从站1个双字的线圈
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">起始地址</param>
        /// <param name="SetValue">设置1个双字线圈的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool WriteCoilWord(byte DeviceAddress, ushort BeginAddress, int SetValue)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, 8 * 4, byWriteData);
                if (null == byDataToBeSent)
                {
                    return false;
                }


                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        /// <summary>
        /// 写从站N个双字的线圈
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">起始地址</param>
        /// <param name="SetValue">设置N个双字线圈的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool WriteCoilWord(byte DeviceAddress, ushort BeginAddress, int[] SetValue)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (null == SetValue || SetValue.Length < 1)
                {
                    throw new Exception("设置值数组不能为空");
                }

                if (BeginAddress + SetValue.Length * 4 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length * 4 * 8, byWriteData);
                if (null == byDataToBeSent)
                {
                    return false;
                }


                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        /// <summary>
        /// 写从站1个双字的线圈
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">起始地址</param>
        /// <param name="SetValue">设置1个双字线圈的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool WriteCoilWord(byte DeviceAddress, ushort BeginAddress, uint SetValue)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, 8 * 4, byWriteData);
                if (null == byDataToBeSent)
                {
                    return false;
                }


                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        /// <summary>
        /// 写从站N个双字的线圈
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">起始地址</param>
        /// <param name="SetValue">设置N个双字线圈的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool WriteCoilWord(byte DeviceAddress, ushort BeginAddress, uint[] SetValue)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (null == SetValue || SetValue.Length < 1)
                {
                    throw new Exception("设置值数组不能为空");
                }

                if (BeginAddress + SetValue.Length * 4 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length * 4 * 8, byWriteData);
                if (null == byDataToBeSent)
                {
                    return false;
                }


                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        /// <summary>
        /// 写从站2个双字的线圈
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">起始地址</param>
        /// <param name="SetValue">设置2个双字线圈的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool WriteCoilWord(byte DeviceAddress, ushort BeginAddress, long SetValue)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, 8 * 8, byWriteData);
                if (null == byDataToBeSent)
                {
                    return false;
                }


                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        /// <summary>
        /// 写从站N个2个双字的线圈
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">起始地址</param>
        /// <param name="SetValue">设置N个2个双字线圈的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool WriteCoilWord(byte DeviceAddress, ushort BeginAddress, long[] SetValue)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (null == SetValue || SetValue.Length < 1)
                {
                    throw new Exception("设置值数组不能为空");
                }

                if (BeginAddress + SetValue.Length * 8 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length * 8 * 8, byWriteData);
                if (null == byDataToBeSent)
                {
                    return false;
                }


                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        /// <summary>
        /// 写从站2个双字的线圈
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">起始地址</param>
        /// <param name="SetValue">设置2个双字线圈的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool WriteCoilWord(byte DeviceAddress, ushort BeginAddress, ulong SetValue)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, 8 * 8, byWriteData);
                if (null == byDataToBeSent)
                {
                    return false;
                }


                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        /// <summary>
        /// 写从站N个2个双字的线圈
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">起始地址</param>
        /// <param name="SetValue">设置N个2个双字线圈的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool WriteCoilWord(byte DeviceAddress, ushort BeginAddress, ulong[] SetValue)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (null == SetValue || SetValue.Length < 1)
                {
                    throw new Exception("设置值数组不能为空");
                }

                if (BeginAddress + SetValue.Length * 8 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length * 8 * 8, byWriteData);
                if (null == byDataToBeSent)
                {
                    return false;
                }


                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        #endregion

        #region "写单个/多个保持寄存器 - ok-ok"

        /// <summary>
        /// 写从站单个保持寄存器的值(short:  -32,768 到 32,767)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="SetValue">设置单个保持寄存器的值(short:  -32,768 到 32,767)</param>
        /// <returns>是否成功执行命令</returns>
        public bool WriteKeepRegister(byte DeviceAddress, ushort BeginAddress, short SetValue)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackSingleWriteCmd(DeviceAddress, ModbusFuncCode.WriteRegister, BeginAddress, byWriteData);
                if (null == byDataToBeSent)
                {
                    return false;
                }


                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        /// <summary>
        /// 写从站多个保持寄存器的值(short:  -32,768 到 32,767)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="SetValue">设置多个保持寄存器的值(short:  -32,768 到 32,767)</param>
        /// <returns>是否成功执行命令</returns>
        public bool WriteKeepRegister(byte DeviceAddress, ushort BeginAddress, short[] SetValue)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (null == SetValue || SetValue.Length < 1)
                {
                    throw new Exception("设置值数组不能为空");
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + SetValue.Length * 2 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, SetValue.Length, byWriteData);
                if (null == byDataToBeSent)
                {
                    return false;
                }


                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        /// <summary>
        /// 写从站单个保持寄存器的值(ushort: 0 到 65535)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="SetValue">设置单个保持寄存器的值(ushort: 0 到 65535)</param>
        /// <returns>是否成功执行命令</returns>
        public bool WriteKeepRegister(byte DeviceAddress, ushort BeginAddress, ushort SetValue)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackSingleWriteCmd(DeviceAddress, ModbusFuncCode.WriteRegister, BeginAddress, byWriteData);
                if (null == byDataToBeSent)
                {
                    return false;
                }


                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        /// <summary>
        /// 写从站多个保持寄存器的值(ushort: 0 到 65535)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="SetValue">设置多个保持寄存器的值(ushort: 0 到 65535)</param>
        /// <returns>是否成功执行命令</returns>
        public bool WriteKeepRegister(byte DeviceAddress, ushort BeginAddress, ushort[] SetValue)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (null == SetValue || SetValue.Length < 1)
                {
                    throw new Exception("设置值数组不能为空");
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + SetValue.Length * 2 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, SetValue.Length, byWriteData);
                if (null == byDataToBeSent)
                {
                    return false;
                }


                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        /// <summary>
        /// 写从站多个保持寄存器的值(int)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="SetValue">设置保持寄存器的值(int)</param>
        /// <returns>是否成功执行命令</returns>
        public bool WriteKeepRegister(byte DeviceAddress, ushort BeginAddress, int SetValue)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, 2, byWriteData);
                if (null == byDataToBeSent)
                {
                    return false;
                }


                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        /// <summary>
        /// 写从站多个保持寄存器的值(int)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="SetValue">设置保持寄存器的值(int)</param>
        /// <returns>是否成功执行命令</returns>
        public bool WriteKeepRegister(byte DeviceAddress, ushort BeginAddress, int[] SetValue)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (null == SetValue || SetValue.Length < 1)
                {
                    throw new Exception("设置值数组不能为空");
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + SetValue.Length * 4 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, SetValue.Length * 2, byWriteData);
                if (null == byDataToBeSent)
                {
                    return false;
                }


                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        /// <summary>
        /// 写从站多个保持寄存器的值(uint)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="SetValue">设置保持寄存器的值(uint)</param>
        /// <returns>是否成功执行命令</returns>
        public bool WriteKeepRegister(byte DeviceAddress, ushort BeginAddress, uint SetValue)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, 2, byWriteData);
                if (null == byDataToBeSent)
                {
                    return false;
                }


                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        /// <summary>
        /// 写从站多个保持寄存器的值(uint)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="SetValue">设置保持寄存器的值(uint)</param>
        /// <returns>是否成功执行命令</returns>
        public bool WriteKeepRegister(byte DeviceAddress, ushort BeginAddress, uint[] SetValue)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (null == SetValue || SetValue.Length < 1)
                {
                    throw new Exception("设置值数组不能为空");
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + SetValue.Length * 4 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, SetValue.Length * 2, byWriteData);
                if (null == byDataToBeSent)
                {
                    return false;
                }


                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        /// <summary>
        /// 写float(32位浮点值)值到从站保持寄存器(float  -3.4×10的38次方 到 +3.4×10的38次方, 精度：7 位)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="SetValue">设置保持寄存器的值(float)</param>
        /// <returns>是否成功执行命令</returns>
        public bool WriteKeepRegister(byte DeviceAddress, ushort BeginAddress, float SetValue)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, 2, byWriteData);
                if (null == byDataToBeSent)
                {
                    return false;
                }


                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        /// <summary>
        /// 写float(32位浮点值)值到从站保持寄存器(float  -3.4×10的38次方 到 +3.4×10的38次方, 精度：7 位)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="SetValue">设置保持寄存器的值(float)</param>
        /// <returns>是否成功执行命令</returns>
        public bool WriteKeepRegister(byte DeviceAddress, ushort BeginAddress, float[] SetValue)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (null == SetValue || SetValue.Length < 1)
                {
                    throw new Exception("设置值数组不能为空");
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + SetValue.Length * 4 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, SetValue.Length * 2, byWriteData);
                if (null == byDataToBeSent)
                {
                    return false;
                }


                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        /// <summary>
        /// 写double(64位浮点值)值到从站保持寄存器(±5.0×10的−324次方 到 ±1.7×10的308次方   精度:15到16位)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="SetValue">设置保持寄存器的值(double)</param>
        /// <returns>是否成功执行命令</returns>
        public bool WriteKeepRegister(byte DeviceAddress, ushort BeginAddress, double SetValue)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, 4, byWriteData);
                if (null == byDataToBeSent)
                {
                    return false;
                }


                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        /// <summary>
        /// 写double(64位浮点值)值到从站保持寄存器(±5.0×10的−324次方 到 ±1.7×10的308次方   精度:15到16位)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="SetValue">设置保持寄存器的值(double)</param>
        /// <returns>是否成功执行命令</returns>
        public bool WriteKeepRegister(byte DeviceAddress, ushort BeginAddress, double[] SetValue)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (null == SetValue || SetValue.Length < 1)
                {
                    throw new Exception("设置值数组不能为空");
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + SetValue.Length * 8 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, SetValue.Length * 4, byWriteData);
                if (null == byDataToBeSent)
                {
                    return false;
                }


                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        /// <summary>
        /// 写long(64位整数值)值到从站保持寄存器(9,223,372,036,854,775,808 到 9,223,372,036,854,775,807, 有符号 64 位整数)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="SetValue">设置保持寄存器的值(long)</param>
        /// <returns>是否成功执行命令</returns>
        public bool WriteKeepRegister(byte DeviceAddress, ushort BeginAddress, long SetValue)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, 4, byWriteData);
                if (null == byDataToBeSent)
                {
                    return false;
                }


                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        /// <summary>
        /// 写long(64位整数值)值到从站保持寄存器(9,223,372,036,854,775,808 到 9,223,372,036,854,775,807, 有符号 64 位整数)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="SetValue">设置保持寄存器的值(long)</param>
        /// <returns>是否成功执行命令</returns>
        public bool WriteKeepRegister(byte DeviceAddress, ushort BeginAddress, long[] SetValue)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (null == SetValue || SetValue.Length < 1)
                {
                    throw new Exception("设置值数组不能为空");
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + SetValue.Length * 8 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, SetValue.Length * 4, byWriteData);
                if (null == byDataToBeSent)
                {
                    return false;
                }


                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        /// <summary>
        /// 写ulong值到从站保持寄存器(0 到 18,446,744,073,709,551,615)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="SetValue">设置保持寄存器的值(ulong)</param>
        /// <returns>是否成功执行命令</returns>
        public bool WriteKeepRegister(byte DeviceAddress, ushort BeginAddress, ulong SetValue)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, 4, byWriteData);
                if (null == byDataToBeSent)
                {
                    return false;
                }


                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        /// <summary>
        /// 写ulong值到从站保持寄存器(0 到 18,446,744,073,709,551,615)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="SetValue">设置保持寄存器的值(ulong)</param>
        /// <returns>是否成功执行命令</returns>
        public bool WriteKeepRegister(byte DeviceAddress, ushort BeginAddress, ulong[] SetValue)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (null == SetValue || SetValue.Length < 1)
                {
                    throw new Exception("设置值数组不能为空");
                }

                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + SetValue.Length * 8 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, SetValue.Length * 4, byWriteData);
                if (null == byDataToBeSent)
                {
                    return false;
                }


                byte[] byReadData = null;
                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (CheckCRCOfReceivedData(byReadData) == false)
                    {
                        if (bSaveReceivedStringToLog == true)
                        {
                            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                        }

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }

                    if (byReadData[PosIndexOfSlaveAddressInRTUReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInRTUReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInRTUReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInRTUReceivedBytes])
                    {
                        byDataToBeSent = null;
                        byReadData = null;
                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);
                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        #endregion

        #endregion

        #region "函数代码"

        #region "操作"

        /// <summary>
        /// 清除发送前的接收和发送缓冲区
        /// </summary>
        void ClearReceiveBuffer()
        {
            RS232CPort.DiscardInBuffer();
            RS232CPort.DiscardOutBuffer();
        }

        /// <summary>
        /// 出现线程锁异常时，用于执行强制清除线程锁
        /// </summary>
        /// <returns></returns>
        public bool ForceUnlock()
        {
            try
            {
                SyncLocker.Unlock();
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 锁定线程锁
        /// </summary>
        private void Lock()
        {
            if (bEnableThreadLock == true)
            {
                SyncLocker.Lock();
            }
        }

        /// <summary>
        /// 释放线程锁
        /// </summary>
        private void Unlock()
        {
            if (bEnableThreadLock == true || SyncLocker.IsLocked == true)
            {
                SyncLocker.Unlock();
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            bIsDisposing = true;
            Close();
            if (null != RS232CPort)
            {
                RS232CPort.Dispose();
                RS232CPort = null;
            }
        }

        /// <summary>
        /// 打开串口端口
        /// </summary>
        /// <returns></returns>
        public bool Open()
        {
            try
            {
                if (RS232CPort.IsOpen == false)
                {
                    RS232CPort.Open();
                }
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 关闭串口端口
        /// </summary>
        /// <returns></returns>
        public bool Close()
        {
            try
            {
                if (RS232CPort.IsOpen == true)
                {
                    RS232CPort.Close();
                }
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 保存信息到队列
        /// </summary>
        /// <param name="Msg">信息</param>
        /// <returns></returns>
        private bool Enqueue(string Msg)
        {
            try
            {
                if (string.IsNullOrEmpty(Msg) == true)
                {
                    return false;
                }

                if (qErrorMsg.Count < int.MaxValue)
                {
                    qErrorMsg.Enqueue("Modbus RTU(" + RS232CPort.PortName + ")" + Msg);
                }
                else
                {
                    qErrorMsg.Dequeue();
                    qErrorMsg.Enqueue("Modbus RTU(" + RS232CPort.PortName + ")" + Msg);
                }
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 获取通讯的记录信息
        /// </summary>
        /// <returns></returns>
        public string GetInfo()
        {
            try
            {
                if (qErrorMsg.Count > 0)
                {
                    return qErrorMsg.Dequeue();
                }
                else
                {
                    return "";
                }
            }
            catch (Exception)
            {
                return "";
            }
        }

        #endregion

        #region "换算、计算、处理"

        void RS232CPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (bReadSlaveDataOnly == false || bIsDisposing == true)
            {
                return;
            }

            try
            {
                byte[] byReadData = new byte[RS232CPort.BytesToRead];
                RS232CPort.Read(byReadData, 0, byReadData.Length);
                qReceivedDataQueue.Enqueue(UnpackReceivedRTUMsg(byReadData));
            }
            catch (Exception ex)
            {
                Enqueue("接收数据发生错误：" + ex.Message + "; " + ex.StackTrace);
            }

        }

        /// <summary>
        /// 将Modbus-RTU接收到的字节信息进行解析
        /// </summary>
        /// <param name="ReceivedMsg">接收到的字节信息</param>
        /// <returns></returns>
        private CReadbackData UnpackReceivedRTUMsg(byte[] ReceivedMsg)
        {
            CReadbackData gotData = new CReadbackData();
            gotData.ReceivedDateTime = DateTime.Now;
            try
            {
                gotData.ReceivedBytes = ReceivedMsg;
                string sFeedBackFromSlave = CConverter.Bytes1To2HexStr(ReceivedMsg);
                gotData.ReceivedString = sFeedBackFromSlave;
                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                    }
                }

                if (CheckCRCOfReceivedData(ReceivedMsg) == false)
                {
                    gotData.IsCRCOK = false;
                    if (bSaveReceivedStringToLog == true)
                    {
                        Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                    }
                }
                else
                {
                    gotData.IsCRCOK = true;
                }

                gotData.SlaveAddress = ReceivedMsg[PosIndexOfSlaveAddressInRTUReceivedBytes];
                gotData.FuncCode = ReceivedMsg[PosIndexOfFuncCodeInRTUReceivedBytes];
                gotData.FuncDescription = CModbusFuncCode.FuncInfo((ModbusFuncCode)gotData.FuncCode);
                gotData.ErrorCode = ReceivedMsg[PosIndexOfErrorCodeInRTUReceivedBytes];
                gotData.ErrorMsg = AnalysisErrorCode(ReceivedMsg);
                int iLength = ReceivedMsg.Length;
                int iCopyDataLength = iLength - PosIndexOfDataInRTUReceivedBytes - 2;
                if (iCopyDataLength > 0)
                {
                    gotData.DataBytes = new byte[iCopyDataLength];
                    Array.Copy(ReceivedMsg, PosIndexOfDataInRTUReceivedBytes, gotData.DataBytes, 0, iCopyDataLength);
                }
            }
            catch (Exception ex)
            {
                gotData.ErrorMsg = ex.Message + "; " + ex.StackTrace;
            }

            return gotData;
        }

        /// <summary>
        /// 执行读写时的错误信息
        /// </summary>
        string sErrorMsgForReadWrite = "";
        /// <summary>
        /// 发送数据到串口并接收串口返回的数据
        /// </summary>
        /// <param name="byResult">要发送的字节数据</param>
        /// <param name="DeviceAddress">目标从站地址</param>
        /// <returns></returns>
        private byte[] WriteAndReadBytes(byte[] byResult, byte DeviceAddress)
        {
            byte[] byReadData = null;
            try
            {
                Lock();
                ClearReceiveBuffer();
                RS232CPort.Write(byResult, 0, byResult.Length);
                bIsConnected = true;
                if (bSaveSendStringToLog == true)
                {
                    string sTemp = CConverter.Bytes1To2HexStr(byResult);
                    Enqueue("发送字节转换为16进制 - " + sTemp);
                }

                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();
                while (RS232CPort.BytesToRead < iMinLengthOfResponse)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        Unlock();
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byReadData = new byte[RS232CPort.BytesToRead];
                RS232CPort.Read(byReadData, 0, byReadData.Length);
                if (bSaveReceivedStringToLog == true)
                {
                    string sFeedBackFromSlave = CConverter.Bytes1To2HexStr(byReadData);
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                    }
                }
            }
            catch (Exception ex)
            {
                bIsConnected = RS232CPort.IsOpen;
                if (sErrorMsgForReadWrite != ex.Message)
                {
                    sErrorMsgForReadWrite = ex.Message;
                    Enqueue("通讯时发生错误：" + ex.Message + "  " + ex.StackTrace);
                }
            }

            Unlock();
            return byReadData;
        }

        /// <summary>
        /// 封装读取命令，返回处理好的字节数组，然后可以直接将字节数组发送到串口
        /// </summary>
        /// <param name="DeviceAddress">从站地址</param>
        /// <param name="FuncCode">功能码</param>
        /// <param name="BeginAddress">起始地址</param>
        /// <param name="ReadDataLength">读取数据长度</param>
        /// <returns></returns>
        private byte[] PackReadCmd(byte DeviceAddress, ModbusFuncCode FuncCode, ushort BeginAddress, int ReadDataLength)
        {
            return Converter.Modbus.ModbusRTU.PackReadCmd(DeviceAddress, FuncCode, BeginAddress, ReadDataLength, ParaBytesFormat, WriteCoilBytesFormat);

        }

        /// <summary>
        /// 封装写命令，返回处理好的字节数组
        /// </summary>
        /// <param name="DeviceAddress">从站地址</param>
        /// <param name="FuncCode">功能码</param>
        /// <param name="BeginAddress">起始地址</param>
        /// <param name="Data">要发送的数据的字节数组</param>
        /// <returns></returns>
        private byte[] PackSingleWriteCmd(byte DeviceAddress, ModbusFuncCode FuncCode, ushort BeginAddress, byte[] Data)
        {
            return Converter.Modbus.ModbusRTU.PackSingleWriteCmd(DeviceAddress, FuncCode, BeginAddress, Data, ParaBytesFormat, WriteCoilBytesFormat);
        }

        /// <summary>
        /// 封装写命令，返回处理好的字节数组
        /// </summary>
        /// <param name="DeviceAddress">从站地址</param>
        /// <param name="FuncCode">功能码</param>
        /// <param name="BeginAddress">起始地址</param>
        /// <param name="DataLength">要发送的数据的数量：线圈 -- 位(bool)；寄存器 -- 字(short)</param>
        /// <param name="Data">要发送的数据的字节数组</param>
        /// <returns></returns>
        private byte[] PackMultiWriteCmd(byte DeviceAddress, ModbusFuncCode FuncCode, ushort BeginAddress, int DataLength, byte[] Data)
        {
            return Converter.Modbus.ModbusRTU.PackMultiWriteCmd(DeviceAddress, FuncCode, BeginAddress, DataLength, Data, ParaBytesFormat, WriteCoilBytesFormat);
        }

        /// <summary>
        /// 解析返回信息的错误代码
        /// </summary>
        /// <param name="MsgWithErrorCode">从站返回的完整字节数组(含错误信息)</param>
        /// <returns></returns>
        public string AnalysisErrorCode(byte[] MsgWithErrorCode)
        {
            CReadbackData Msg = CModbusErrorCode.AnalysisErrorCode(MsgWithErrorCode, ModbusCommType.RTU);
            if (null == Msg)
            {
                return "";
            }
            else
            {
                Enqueue(Msg.ErrorMsg);
                return Msg.ErrorMsg;
            }
        }

        /// <summary>
        /// 计算循环冗余码校验值(2个字节) - 发送时要将CRC校验值的高低字节交换位置：[1][0]，不能按照原始顺序：[0][1]；
        /// CRC - Cyclical Redundancy Check
        /// </summary>
        /// <param name="BytesData">用来计算CRC的字节数组值</param>
        /// <param name="SetCalcLengthOfBytes"></param>
        /// <returns>循环冗余码校验值(2个字节)</returns>
        public static byte[] CalcCRC(byte[] BytesData, int SetCalcLengthOfBytes = 0)
        {
            return Converter.Modbus.CCRC.CalcCRC(BytesData, SetCalcLengthOfBytes);
        }

        /// <summary>
        /// 检查接收到的数据帧里面的CRC是否匹配OK，如果不匹配就代表数据发生错误，计算方式：将收到的字节数组长度减去2，然后计算CRC
        /// </summary>
        /// <param name="ReceivedRTUFrame">接收到的数据帧字节数组</param>
        /// <returns>true - 接收到的数据帧无错误; false - 接收到的数据帧有错误</returns>
        public static bool CheckCRCOfReceivedData(byte[] ReceivedRTUFrame)
        {
            int iLength = ReceivedRTUFrame.Length;
            byte[] byCRCResult = CalcCRC(ReceivedRTUFrame, iLength - 2);
            if (byCRCResult[0] == ReceivedRTUFrame[iLength - 2]
            && byCRCResult[1] == ReceivedRTUFrame[iLength - 1])
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        #endregion

        #endregion

    }//class

}//namespace