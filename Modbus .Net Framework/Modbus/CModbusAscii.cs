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
    /// Modbus Ascii通讯类，支持跨线程读写操作；如果是单线程操作，请将 EnableThreadLock 设置为 false 以提升性能
    /// 授权声明：本软件作者将代码开源，仅用于交流学习。如果有商用需求，请联系软件作者协商相关事宜；否则，软件作者保留相关法律赋予的权利。
    /// 免责声明：使用本软件的相关人员必须仔细检查代码并负全部责任，软件作者不承担任何可能的损失(包含可抗力和不可抗力因素)。
    /// </summary>
    public sealed class CModbusAscii : IModbusComm
    {
        /// <summary>
        /// Modbus Ascii通讯类的构造函数
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
        public CModbusAscii(string DLLPassword, string RS232CPortName, eBaudRate baudrate = eBaudRate.Rate_19200,
        Parity parity = Parity.Even, StopBits stopbits = StopBits.One, int writetimeout = 1000,
        int readtimeout = 1000, bool ReadSlaveDataOnly = false, BytesFormat paraBytesFormat = BytesFormat.BADC,
        BytesFormat writeKeepRegisterBytesFormat = BytesFormat.BADC, BytesFormat readRegisterBytesFormat = BytesFormat.BADC,
        ThreadLock.LockerType ThreadLockType = LockerType.ExchangeLock)
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
                ReadKeepRegisterBytesFormat = BytesFormat.BADC;
                ReadCoilBytesFormat = BytesFormat.ABCD;
                ReadInputIOBytesFormat = BytesFormat.ABCD;
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
                RS232CPort.NewLine = "\r\n";
                RS232CPort.Open();
                if (bReadSlaveDataOnly == true)
                {
                    RS232CPort.DataReceived += RS232CPort_DataReceived;
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯类初始化时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        #region "Ascii-通讯方式下返回字符串中信息对应位置的定义"

        /// <summary>
        /// Ascii-通讯方式，从站地址：第2~3字符(如果用字节处理，相应值/2)，收到从站返回的字符串中，从站地址码的位置[2] -- 第2个字符，用于Strings.Mid() 函数
        /// </summary>
        public const int PosOfSlaveAddressInAsciiReceivedString = 0x2;
        /// <summary>
        /// 接Ascii-通讯方式，功能码：第4~5字符(如果用字节处理，相应值/2)，收到从站返回的字符串中，功能码的位置[4] -- 第4个字符，用于Strings.Mid() 函数
        /// </summary>
        public const int PosOfFuncCodeInAsciiReceivedString = 0x4;
        /// <summary>
        /// 接Ascii-通讯方式，错误码：第6~7字符(如果用字节处理，相应值/2)，收到从站返回的字符串中，错误码的位置[6] -- 第6个字符，用于Strings.Mid() 函数
        /// </summary>
        public const int PosOfErrorCodeInAsciiReceivedString = 0x6;
        /// <summary>
        /// 接Ascii-通讯方式，数据字节长度：第6~7字符(如果用字节处理，相应值/2)，收到从站返回的字符串中，数据字节长度的位置[6] -- 第6个字符，用于Strings.Mid() 函数
        /// </summary>
        public const int PosOfDataLengthInAsciiReceivedString = 0x6;
        /// <summary>
        /// 接Ascii-通讯方式，有效数据起始位置：第8字符(如果用字节处理，相应值/2)，收到从站返回的字符串中，有效数据的起始位置，默认值8
        /// </summary>
        public const int StartPosOfDataInAsciiReceivedString = 0x8;

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
        /// 处理从站返回的信息时使用处理字节的方式，默认值：false
        /// </summary>
        bool ProcessFeedbackDataByBytes = false;
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
        /// 起始位字符(:)
        /// </summary>
        public const string Prefix = ":";
        /// <summary>
        /// 结束符(回车+换行)
        /// </summary>
        public const string Suffix = "\r\n";
        /// <summary>
        /// 起始位字符(:)的字节
        /// </summary>
        public const byte PrefixByte = 0x3A;
        /// <summary>
        /// 结束符(回车+换行)的字节数组
        /// </summary>
        public static byte[] SuffixBytes
        {
            get { return new byte[] { 0x0D, 0x0A }; }
        }

        /// <summary>
        /// 进行Modbus Ascii通讯的串口实例化对象
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
        /// <param name="BitValue">返回线圈的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilBit(byte DeviceAddress, ushort BeginAddress, ref bool BitValue)
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, 1);
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);

                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }

                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }

                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {

                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadCoil.ToString("X2"))
                        {
                            if (Strings.Mid(sFeedBackFromSlave, 9, 1) == "0")
                            {
                                BitValue = false;
                            }
                            else
                            {
                                BitValue = true;
                            }

                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站多个字节的线圈状态，函数是以字节为读取数据长度单位
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字节</param>
        /// <param name="BitValue">返回线圈的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilBit(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref bool[] BitValue)
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, Convert.ToUInt16(ReadDataLength * 8));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }

                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {

                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadCoil.ToString("X2"))
                        {
                            BitValue = CConverter.ByteToBitArray(CConverter.Hex2StrTo1Byte(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, ReadDataLength * (byte)HexStringCount.Byte)));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站1个字节的线圈状态(1个字节 = 8位 - bit)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ByteValue">返回线圈的1个字节当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilByte(byte DeviceAddress, ushort BeginAddress, ref byte ByteValue)
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, Convert.ToUInt16(1 * 8));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }

                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {

                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadCoil.ToString("X2"))
                        {
                            string sTemp = Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, (byte)HexStringCount.Byte);
                            ByteValue = CConverter.TwoHexCharsToByte(sTemp);
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站多个字节的线圈状态(1个字节 = 位 - bit)，函数是以字节为读取数据长度单位
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字节</param>
        /// <param name="ByteValue">返回线圈的字节当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilByte(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref byte[] ByteValue)
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, Convert.ToUInt16(ReadDataLength * 8));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }

                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {

                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadCoil.ToString("X2"))
                        {
                            ByteValue = CConverter.Hex2StrTo1Byte(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, ReadDataLength * (byte)HexStringCount.Byte));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站1个字节的线圈状态(1个字节 = 8位 - bit)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ByteValue">返回线圈的1个字节当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilByte(byte DeviceAddress, ushort BeginAddress, ref sbyte ByteValue)
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, Convert.ToUInt16(1 * 8));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }

                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {

                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadCoil.ToString("X2"))
                        {
                            string sTemp = Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, (byte)HexStringCount.SByte);
                            ByteValue = (sbyte)CConverter.TwoHexCharsToByte(sTemp);
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站多个字节的线圈状态(1个字节 = 位 - bit)，函数是以字节为读取数据长度单位
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字节</param>
        /// <param name="ByteValue">返回线圈的字节当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilByte(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref sbyte[] ByteValue)
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, Convert.ToUInt16(ReadDataLength * 8));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }

                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {

                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadCoil.ToString("X2"))
                        {
                            ByteValue = new sbyte[ReadDataLength];
                            byte[] byTemp = CConverter.Hex2StrTo1Byte(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, ReadDataLength * (byte)HexStringCount.SByte));
                            for (int i = 0; i < byTemp.Length; i++)
                            {
                                ByteValue[i] = (sbyte)byTemp[i];
                            }

                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, Convert.ToUInt16(2 * 8));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }

                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {

                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadCoil.ToString("X2"))
                        {
                            Value = CConverter.ToInt16(CConverter.Hex4StrTo2Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, (byte)HexStringCount.Short), ReadCoilBytesFormat));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站多个字的线圈状态，函数是以字为读取数据长度单位
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, Convert.ToUInt16(ReadDataLength * 2 * 8));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }

                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {

                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadCoil.ToString("X2"))
                        {
                            Value = CConverter.ToInt16Array(CConverter.Hex4StrTo2Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, ReadDataLength * (byte)HexStringCount.Short), ReadCoilBytesFormat));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, Convert.ToUInt16(2 * 8));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }

                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {

                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadCoil.ToString("X2"))
                        {
                            Value = CConverter.ToUInt16(CConverter.Hex4StrTo2Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, (byte)HexStringCount.UShort), ReadCoilBytesFormat));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站多个字的线圈状态，函数是以字为读取数据长度单位
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, Convert.ToUInt16(ReadDataLength * 2 * 8));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }

                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {

                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadCoil.ToString("X2"))
                        {
                            Value = CConverter.ToUInt16Array(CConverter.Hex4StrTo2Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, ReadDataLength * (byte)HexStringCount.UShort), ReadCoilBytesFormat));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站1个双字的线圈状态(位 - bit)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回线圈的1个双字当前值</param>
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, Convert.ToUInt16(4 * 8));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }

                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {

                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadCoil.ToString("X2"))
                        {
                            Value = CConverter.ToInt32(CConverter.Hex8StrTo4Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, 1 * (byte)HexStringCount.Int), ReadCoilBytesFormat));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站多个双字的线圈状态，函数是以字为读取数据长度单位
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字</param>
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, Convert.ToUInt16(ReadDataLength * 4 * 8));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadCoil.ToString("X2"))
                        {
                            Value = CConverter.ToInt32Array(CConverter.Hex8StrTo4Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, ReadDataLength * (byte)HexStringCount.Int), ReadCoilBytesFormat));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站1个双字的线圈状态(位 - bit)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回线圈的1个双字当前值</param>
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, Convert.ToUInt16(4 * 8));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }

                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {

                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadCoil.ToString("X2"))
                        {
                            Value = CConverter.ToUInt32(CConverter.Hex8StrTo4Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, 1 * (byte)HexStringCount.UInt), ReadCoilBytesFormat));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站多个双字的线圈状态，函数是以字为读取数据长度单位
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字</param>
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, Convert.ToUInt16(ReadDataLength * 4 * 8));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadCoil.ToString("X2"))
                        {
                            Value = CConverter.ToUInt32Array(CConverter.Hex8StrTo4Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, ReadDataLength * (byte)HexStringCount.UInt), ReadCoilBytesFormat));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站2个双字的线圈状态(位 - bit)
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, Convert.ToUInt16(8 * 8));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {

                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadCoil.ToString("X2"))
                        {
                            Value = CConverter.ToInt64(CConverter.Hex16StrTo8Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, 1 * (byte)HexStringCount.Long), ReadCoilBytesFormat));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站N个2个双字的线圈状态，函数是以字为读取数据长度单位
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字</param>
        /// <param name="Value">返回线圈N个2个双字的当前值</param>
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, Convert.ToUInt16(ReadDataLength * 8 * 8));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {

                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadCoil.ToString("X2"))
                        {
                            Value = CConverter.ToInt64Array(CConverter.Hex16StrTo8Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, ReadDataLength * (byte)HexStringCount.Long), ReadCoilBytesFormat));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站2个双字的线圈状态(位 - bit)
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, Convert.ToUInt16(8 * 8));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {

                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadCoil.ToString("X2"))
                        {
                            Value = CConverter.ToUInt64(CConverter.Hex16StrTo8Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, 1 * (byte)HexStringCount.ULong), ReadCoilBytesFormat));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站N个2个双字的线圈状态，函数是以字为读取数据长度单位
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字</param>
        /// <param name="Value">返回线圈N个2个双字的当前值</param>
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, Convert.ToUInt16(ReadDataLength * 8 * 8));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {

                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadCoil.ToString("X2"))
                        {
                            Value = CConverter.ToUInt64Array(CConverter.Hex16StrTo8Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, ReadDataLength * (byte)HexStringCount.ULong), ReadCoilBytesFormat));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, 1);
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {

                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadInputSignal.ToString("X2"))
                        {
                            if (Strings.Mid(sFeedBackFromSlave, 9, 1) == "0")
                            {
                                Value = false;
                            }
                            else
                            {
                                Value = true;
                            }

                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站多个字节的输入状态(1个字节 = 8位 - bit)，函数是以字节为读取数据长度单位
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, Convert.ToUInt16(ReadDataLength * 8));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadInputSignal.ToString("X2"))
                        {
                            Value = CConverter.ByteToBitArray(CConverter.Hex2StrTo1Byte(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, ReadDataLength * (byte)HexStringCount.Byte)));
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站1个字节的输入状态(1个字节 = 8位 - bit)
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, Convert.ToUInt16(1 * 8));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadInputSignal.ToString("X2"))
                        {
                            string sTemp = Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, (byte)HexStringCount.Byte);
                            Value = CConverter.TwoHexCharsToByte(sTemp);
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站多个字节的输入状态(1个字节 = 8位 - bit)
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, Convert.ToUInt16(ReadDataLength * 8));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadInputSignal.ToString("X2"))
                        {
                            Value = CConverter.Hex2StrTo1Byte(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, ReadDataLength * (byte)HexStringCount.Byte));
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站1个字节的输入状态(1个字节 = 8位 - bit)
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, Convert.ToUInt16(1 * 8));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadInputSignal.ToString("X2"))
                        {
                            string sTemp = Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, (byte)HexStringCount.Byte);
                            Value = (sbyte)CConverter.TwoHexCharsToByte(sTemp);
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站多个字节的输入状态(1个字节 = 8位 - bit)
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, Convert.ToUInt16(ReadDataLength * 8));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadInputSignal.ToString("X2"))
                        {
                            Value = new sbyte[ReadDataLength];
                            byte[] byTemp = CConverter.Hex2StrTo1Byte(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, ReadDataLength * (byte)HexStringCount.Byte));
                            for (int i = 0; i < byTemp.Length; i++)
                            {
                                Value[i] = (sbyte)byTemp[i];
                            }

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站1个字的输入状态
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

                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, Convert.ToUInt16(2 * 8));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadInputSignal.ToString("X2"))
                        {
                            Value = CConverter.ToInt16(CConverter.Hex4StrTo2Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, (byte)HexStringCount.Short), ReadInputIOBytesFormat));
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站多个字的输入状态
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, Convert.ToUInt16(ReadDataLength * 2 * 8));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadInputSignal.ToString("X2"))
                        {
                            Value = CConverter.ToInt16Array(CConverter.Hex4StrTo2Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, ReadDataLength * (byte)HexStringCount.Short), ReadInputIOBytesFormat));
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站1个字的输入状态
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

                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, Convert.ToUInt16(2 * 8));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadInputSignal.ToString("X2"))
                        {
                            Value = CConverter.ToUInt16(CConverter.Hex4StrTo2Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, (byte)HexStringCount.UShort), ReadInputIOBytesFormat));
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站多个字的输入状态
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, Convert.ToUInt16(ReadDataLength * 2 * 8));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadInputSignal.ToString("X2"))
                        {
                            Value = CConverter.ToUInt16Array(CConverter.Hex4StrTo2Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, ReadDataLength * (byte)HexStringCount.UShort), ReadInputIOBytesFormat));
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, Convert.ToUInt16(4 * 8));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadInputSignal.ToString("X2"))
                        {
                            Value = CConverter.ToInt32(CConverter.Hex8StrTo4Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, (byte)HexStringCount.Int), ReadInputIOBytesFormat));
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站多个双字的输入状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字</param>
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, Convert.ToUInt16(ReadDataLength * 4 * 8));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadInputSignal.ToString("X2"))
                        {
                            Value = CConverter.ToInt32Array(CConverter.Hex8StrTo4Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, ReadDataLength * (byte)HexStringCount.Int), ReadInputIOBytesFormat));
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, Convert.ToUInt16(4 * 8));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadInputSignal.ToString("X2"))
                        {
                            Value = CConverter.ToUInt32(CConverter.Hex8StrTo4Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, 1 * (byte)HexStringCount.UInt), ReadInputIOBytesFormat));
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站多个双字的输入状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字</param>
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, Convert.ToUInt16(ReadDataLength * 4 * 8));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadInputSignal.ToString("X2"))
                        {
                            Value = CConverter.ToUInt32Array(CConverter.Hex8StrTo4Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, ReadDataLength * (byte)HexStringCount.UInt), ReadInputIOBytesFormat));
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站2个双字的输入状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回输入状态2个双字的当前值</param>
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, Convert.ToUInt16(8 * 8));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadInputSignal.ToString("X2"))
                        {
                            Value = CConverter.ToInt64(CConverter.Hex16StrTo8Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, 1 * (byte)HexStringCount.Long), ReadInputIOBytesFormat));
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站多个2双字的输入状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度</param>
        /// <param name="Value">返回输入状态多个2双字的当前值</param>
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, Convert.ToUInt16(ReadDataLength * 8 * 8));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadInputSignal.ToString("X2"))
                        {
                            Value = CConverter.ToInt64Array(CConverter.Hex16StrTo8Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, ReadDataLength * (byte)HexStringCount.Long), ReadInputIOBytesFormat));
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站2个双字的输入状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回输入状态2个双字的当前值</param>
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, Convert.ToUInt16(8 * 8));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadInputSignal.ToString("X2"))
                        {
                            Value = CConverter.ToUInt64(CConverter.Hex16StrTo8Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, 1 * (byte)HexStringCount.ULong), ReadInputIOBytesFormat));
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站多个2双字的输入状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度</param>
        /// <param name="Value">返回输入状态多个2双字的当前值</param>
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, Convert.ToUInt16(ReadDataLength * 8 * 8));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadInputSignal.ToString("X2"))
                        {
                            Value = CConverter.ToUInt64Array(CConverter.Hex16StrTo8Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, ReadDataLength * (byte)HexStringCount.ULong), ReadInputIOBytesFormat));
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, Convert.ToUInt16(1));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadInputRegister.ToString("X2"))
                        {
                            Value = CConverter.ToInt16(CConverter.Hex4StrTo2Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, 1 * (byte)HexStringCount.Short), ReadInputRegisterBytesFormat));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站输入寄存器的当前值(short: -32,768 到 32,767)，函数是以字节为读取数据长度单位
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字</param>
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, Convert.ToUInt16(ReadDataLength));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadInputRegister.ToString("X2"))
                        {
                            Value = CConverter.ToInt16Array(CConverter.Hex4StrTo2Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, ReadDataLength * (byte)HexStringCount.Short), ReadInputRegisterBytesFormat));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, Convert.ToUInt16(1));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadInputRegister.ToString("X2"))
                        {
                            Value = CConverter.ToUInt16(CConverter.Hex4StrTo2Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, 1 * (byte)HexStringCount.UShort), ReadInputRegisterBytesFormat));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站输入寄存器的当前值(ushort: 0 到 65535)，函数是以字节为读取数据长度单位
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字</param>
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, Convert.ToUInt16(ReadDataLength));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadInputRegister.ToString("X2"))
                        {
                            Value = CConverter.ToUInt16Array(CConverter.Hex4StrTo2Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, ReadDataLength * (byte)HexStringCount.UShort), ReadInputRegisterBytesFormat));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                if (BeginAddress + 2 * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, Convert.ToUInt16(2));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadInputRegister.ToString("X2"))
                        {
                            Value = CConverter.ToInt32(CConverter.Hex8StrTo4Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, 1 * (byte)HexStringCount.Int), ReadInputRegisterBytesFormat));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站输入寄存器的当前值(int)，函数是以字节为读取数据长度单位
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：双字</param>
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

                if (BeginAddress + ReadDataLength * 2 * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, Convert.ToUInt16(ReadDataLength * 2));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadInputRegister.ToString("X2"))
                        {
                            Value = CConverter.ToInt32Array(CConverter.Hex8StrTo4Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, ReadDataLength * (byte)HexStringCount.Int), ReadInputRegisterBytesFormat));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                if (BeginAddress + 2 * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, Convert.ToUInt16(2));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadInputRegister.ToString("X2"))
                        {
                            Value = CConverter.ToUInt32(CConverter.Hex8StrTo4Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, 1 * (byte)HexStringCount.UInt), ReadInputRegisterBytesFormat));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站输入寄存器的当前值(uint)，函数是以字节为读取数据长度单位
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：双字</param>
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

                if (BeginAddress + ReadDataLength * 2 * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, Convert.ToUInt16(ReadDataLength * 2));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadInputRegister.ToString("X2"))
                        {
                            Value = CConverter.ToUInt32Array(CConverter.Hex8StrTo4Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, ReadDataLength * (byte)HexStringCount.UInt), ReadInputRegisterBytesFormat));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                if (BeginAddress + 2 * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, Convert.ToUInt16(2));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadInputRegister.ToString("X2"))
                        {
                            Value = CConverter.ToFloat(CConverter.Hex8StrTo4Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, 1 * (byte)HexStringCount.Float), ReadInputRegisterBytesFormat));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站输入寄存器的当前值(float)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：双字</param>
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

                if (BeginAddress + ReadDataLength * 2 * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, Convert.ToUInt16(ReadDataLength * 2));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadInputRegister.ToString("X2"))
                        {
                            Value = CConverter.ToFloatArray(CConverter.Hex8StrTo4Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, ReadDataLength * (byte)HexStringCount.Float), ReadInputRegisterBytesFormat));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// ?? 测试读取数据时不能像其它功能码一样正确读取double值，在float范围内可以正确读取，其它会对应不上，待更多测试
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, Convert.ToUInt16(4));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadInputRegister.ToString("X2"))
                        {
                            Value = CConverter.ToDouble(CConverter.Hex16StrTo8Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, 1 * (byte)HexStringCount.Double), ReadInputRegisterBytesFormat));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// ?? 测试读取数据时不能像其它功能码一样正确读取double值，在float范围内可以正确读取，其它会对应不上，待更多测试
        /// 读取从站输入寄存器的当前值(double)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：4字</param>
        /// <param name="Value">返回输入寄存器的当前值(float)</param>
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, Convert.ToUInt16(ReadDataLength * 4));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadInputRegister.ToString("X2"))
                        {
                            Value = CConverter.ToDoubleArray(CConverter.Hex16StrTo8Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, ReadDataLength * (byte)HexStringCount.Double), ReadInputRegisterBytesFormat));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站输入寄存器的当前值(long)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回输入寄存器的当前值(long)</param>
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

                if (BeginAddress + 4 * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, Convert.ToUInt16(4));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadInputRegister.ToString("X2"))
                        {
                            Value = CConverter.ToInt64(CConverter.Hex16StrTo8Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, 1 * (byte)HexStringCount.Long), ReadInputRegisterBytesFormat));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站输入寄存器的当前值(long)，函数是以字节为读取数据长度单位
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：2双字</param>
        /// <param name="Value">返回输入寄存器的当前值(long)</param>
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

                if (BeginAddress + ReadDataLength * 4 * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, Convert.ToUInt16(ReadDataLength * 4));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadInputRegister.ToString("X2"))
                        {
                            Value = CConverter.ToInt64Array(CConverter.Hex16StrTo8Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, ReadDataLength * (byte)HexStringCount.Long), ReadInputRegisterBytesFormat));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站输入寄存器的当前值(ulong)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回输入寄存器的当前值(ulong)</param>
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

                if (BeginAddress + 4 * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, Convert.ToUInt16(4));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadInputRegister.ToString("X2"))
                        {
                            Value = CConverter.ToUInt64(CConverter.Hex16StrTo8Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, 1 * (byte)HexStringCount.ULong), ReadInputRegisterBytesFormat));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站输入寄存器的当前值(ulong)，函数是以字节为读取数据长度单位
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：2双字</param>
        /// <param name="Value">返回输入寄存器的当前值(ulong)</param>
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

                if (BeginAddress + ReadDataLength * 4 * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, Convert.ToUInt16(ReadDataLength * 4));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadInputRegister.ToString("X2"))
                        {
                            Value = CConverter.ToUInt64Array(CConverter.Hex16StrTo8Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, ReadDataLength * (byte)HexStringCount.ULong), ReadInputRegisterBytesFormat));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, Convert.ToUInt16(1));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadRegister.ToString("X2"))
                        {
                            Value = CConverter.ToInt16(CConverter.Hex4StrTo2Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, (byte)HexStringCount.Short), ReadKeepRegisterBytesFormat));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站保持寄存器的当前值(short: -32,768 到 32,767)，函数是以字节为读取数据长度单位
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：short</param>
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, Convert.ToUInt16(ReadDataLength));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadRegister.ToString("X2"))
                        {
                            Value = CConverter.ToInt16Array(CConverter.Hex4StrTo2Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, ReadDataLength * (byte)HexStringCount.Short), ReadKeepRegisterBytesFormat));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, Convert.ToUInt16(1));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadRegister.ToString("X2"))
                        {
                            Value = CConverter.ToUInt16(CConverter.Hex4StrTo2Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, (byte)HexStringCount.UShort), ReadKeepRegisterBytesFormat));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站保持寄存器的当前值(ushort: 0 到 65535)，函数是以字节为读取数据长度单位
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：short</param>
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, Convert.ToUInt16(ReadDataLength));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadRegister.ToString("X2"))
                        {
                            Value = CConverter.ToUInt16Array(CConverter.Hex4StrTo2Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, ReadDataLength * (byte)HexStringCount.UShort), ReadKeepRegisterBytesFormat));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                if (BeginAddress + 2 * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, Convert.ToUInt16(2));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadRegister.ToString("X2"))
                        {
                            Value = CConverter.ToInt32(CConverter.Hex8StrTo4Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, (byte)HexStringCount.Int), ReadKeepRegisterBytesFormat));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站保持寄存器的当前值(int)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：short</param>
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

                if (BeginAddress + ReadDataLength * 2 * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, Convert.ToUInt16(ReadDataLength * 2));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadRegister.ToString("X2"))
                        {
                            Value = CConverter.ToInt32Array(CConverter.Hex8StrTo4Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, ReadDataLength * (byte)HexStringCount.Int), ReadKeepRegisterBytesFormat));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                if (BeginAddress + 2 * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, Convert.ToUInt16(2));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadRegister.ToString("X2"))
                        {
                            Value = CConverter.ToUInt32(CConverter.Hex8StrTo4Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, 1 * (byte)HexStringCount.UInt), ReadKeepRegisterBytesFormat));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站保持寄存器的当前值(uint)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：short</param>
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

                if (BeginAddress + ReadDataLength * 2 * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, Convert.ToUInt16(ReadDataLength * 2));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadRegister.ToString("X2"))
                        {
                            Value = CConverter.ToUInt32Array(CConverter.Hex8StrTo4Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, ReadDataLength * (byte)HexStringCount.UInt), ReadKeepRegisterBytesFormat));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, Convert.ToUInt16(2));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadRegister.ToString("X2"))
                        {
                            Value = CConverter.ToFloat(CConverter.Hex8StrTo4Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, 1 * (byte)HexStringCount.Float), ReadKeepRegisterBytesFormat));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, Convert.ToUInt16(ReadDataLength * 2));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadRegister.ToString("X2"))
                        {
                            Value = CConverter.ToFloatArray(CConverter.Hex8StrTo4Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, ReadDataLength * (byte)HexStringCount.Float), ReadKeepRegisterBytesFormat));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, Convert.ToUInt16(4));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadRegister.ToString("X2"))
                        {
                            Value = CConverter.ToDouble(CConverter.Hex16StrTo8Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, 1 * (byte)HexStringCount.Double), ReadKeepRegisterBytesFormat));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, Convert.ToUInt16(ReadDataLength * 4));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadRegister.ToString("X2"))
                        {
                            Value = CConverter.ToDoubleArray(CConverter.Hex16StrTo8Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, ReadDataLength * (byte)HexStringCount.Double), ReadKeepRegisterBytesFormat));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, Convert.ToUInt16(4));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadRegister.ToString("X2"))
                        {
                            Value = CConverter.ToInt64(CConverter.Hex16StrTo8Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, 1 * (byte)HexStringCount.Long), ReadKeepRegisterBytesFormat));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, Convert.ToUInt16(ReadDataLength * 4));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadRegister.ToString("X2"))
                        {
                            Value = CConverter.ToInt64Array(CConverter.Hex16StrTo8Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, ReadDataLength * (byte)HexStringCount.Long), ReadKeepRegisterBytesFormat));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站ulong(64位无符号整数值)值到从站保持寄存器(0 到 18,446,744,073,709,551,615)
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, Convert.ToUInt16(4));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadRegister.ToString("X2"))
                        {
                            Value = CConverter.ToUInt64(CConverter.Hex16StrTo8Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, 1 * (byte)HexStringCount.ULong), ReadKeepRegisterBytesFormat));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 读取从站ulong(64位无符号整数值)值到从站保持寄存器(0 到 18,446,744,073,709,551,615)
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

                byte[] byResult = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, Convert.ToUInt16(ReadDataLength * 4));
                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.ReadRegister.ToString("X2"))
                        {
                            Value = CConverter.ToUInt64Array(CConverter.Hex16StrTo8Bytes(Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, ReadDataLength * (byte)HexStringCount.ULong), ReadKeepRegisterBytesFormat));
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                string sTemp = IsOn == true ? "FF00" : "0000";
                string sData = CConverter.Reorder2HexStrData(sTemp, WriteCoilBytesFormat);
                byte[] byResult = PackSingleWriteCmd(DeviceAddress, ModbusFuncCode.WriteCoil, BeginAddress, sData);

                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }

                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {

                        string sFeedBack = Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2);
                        string sCmd = CModbusFuncCode.WriteCoil.ToString("X2");
                        if (sFeedBack == sCmd)
                        {
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }

            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                string sData = CConverter.Bytes2To4HexStr(CConverter.BitArrayToByte(SetValue), WriteCoilBytesFormat);
                byte[] byResult = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length, SetValue.Length / 8, sData);
                if (null == byResult)
                {
                    return false;
                }


                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }

                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {

                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.WriteMultiCoil.ToString("X2"))
                        {
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                string sData = CConverter.Byte1To2HexStr(SetValue);
                byte[] byResult = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, 1 * 8, 1, sData);
                if (null == byResult)
                {
                    return false;
                }


                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.WriteMultiCoil.ToString("X2"))
                        {
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                string sData = CConverter.Bytes1To2HexStr(SetValue);
                byte[] byResult = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length * 8, SetValue.Length, sData);
                if (null == byResult)
                {
                    return false;
                }


                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.WriteMultiCoil.ToString("X2"))
                        {
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                string sData = CConverter.Byte1To2HexStr((byte)SetValue);
                byte[] byResult = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, 1 * 8, 1, sData);
                if (null == byResult)
                {
                    return false;
                }


                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.WriteMultiCoil.ToString("X2"))
                        {
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                string sData = CConverter.Bytes1To2HexStr(SetValue);
                byte[] byResult = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length * 8, SetValue.Length, sData);
                if (null == byResult)
                {
                    return false;
                }


                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.WriteMultiCoil.ToString("X2"))
                        {
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                string sData = CConverter.Bytes2To4HexStr(CConverter.ToBytes(SetValue, WriteCoilBytesFormat));
                byte[] byResult = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, 2 * 8, 2, sData);
                if (null == byResult)
                {
                    return false;
                }


                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.WriteMultiCoil.ToString("X2"))
                        {
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                string sData = CConverter.Bytes2To4HexStr(CConverter.ToBytes(SetValue, WriteCoilBytesFormat));
                byte[] byResult = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length * 2 * 8, SetValue.Length * 2, sData);
                if (null == byResult)
                {
                    return false;
                }


                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.WriteMultiCoil.ToString("X2"))
                        {
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                string sData = CConverter.Bytes2To4HexStr(CConverter.ToBytes(SetValue, WriteCoilBytesFormat));
                byte[] byResult = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, 2 * 8, 2, sData);
                if (null == byResult)
                {
                    return false;
                }


                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.WriteMultiCoil.ToString("X2"))
                        {
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                string sData = CConverter.Bytes2To4HexStr(CConverter.ToBytes(SetValue, WriteCoilBytesFormat));
                byte[] byResult = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length * 2 * 8, SetValue.Length * 2, sData);
                if (null == byResult)
                {
                    return false;
                }


                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.WriteMultiCoil.ToString("X2"))
                        {
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                string sData = CConverter.Bytes4To8HexStr(CConverter.ToBytes(SetValue, WriteCoilBytesFormat));
                byte[] byResult = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, 4 * 8, 4, sData);
                if (null == byResult)
                {
                    return false;
                }


                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.WriteMultiCoil.ToString("X2"))
                        {
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                string sData = CConverter.Bytes4To8HexStr(CConverter.ToBytes(SetValue, WriteCoilBytesFormat));
                byte[] byResult = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length * 4 * 8, SetValue.Length * 4, sData);
                if (null == byResult)
                {
                    return false;
                }


                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.WriteMultiCoil.ToString("X2"))
                        {
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                string sData = CConverter.Bytes4To8HexStr(CConverter.ToBytes(SetValue, WriteCoilBytesFormat));
                byte[] byResult = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, 4 * 8, 4, sData);
                if (null == byResult)
                {
                    return false;
                }


                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.WriteMultiCoil.ToString("X2"))
                        {
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                string sData = CConverter.Bytes4To8HexStr(CConverter.ToBytes(SetValue, WriteCoilBytesFormat));
                byte[] byResult = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length * 4 * 8, SetValue.Length * 4, sData);
                if (null == byResult)
                {
                    return false;
                }


                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.WriteMultiCoil.ToString("X2"))
                        {
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                string sData = CConverter.Bytes8To16HexStr(CConverter.ToBytes(SetValue, WriteCoilBytesFormat));
                byte[] byResult = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, 8 * 8, 8, sData);
                if (null == byResult)
                {
                    return false;
                }


                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.WriteMultiCoil.ToString("X2"))
                        {
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                string sData = CConverter.Bytes8To16HexStr(CConverter.ToBytes(SetValue, WriteCoilBytesFormat));
                byte[] byResult = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length * 8 * 8, SetValue.Length * 8, sData);
                if (null == byResult)
                {
                    return false;
                }


                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.WriteMultiCoil.ToString("X2"))
                        {
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                string sData = CConverter.Bytes8To16HexStr(CConverter.ToBytes(SetValue, WriteCoilBytesFormat));
                byte[] byResult = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, 8 * 8, 8, sData);
                if (null == byResult)
                {
                    return false;
                }


                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.WriteMultiCoil.ToString("X2"))
                        {
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                string sData = CConverter.Bytes8To16HexStr(CConverter.ToBytes(SetValue, WriteCoilBytesFormat));
                byte[] byResult = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length * 8 * 8, SetValue.Length * 8, sData);
                if (null == byResult)
                {
                    return false;
                }


                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2) == CModbusFuncCode.WriteMultiCoil.ToString("X2"))
                        {
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                string sData = CConverter.Bytes2To4HexStr(CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat));
                byte[] byResult = PackSingleWriteCmd(DeviceAddress, ModbusFuncCode.WriteRegister, BeginAddress, sData);

                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        string sFeedBack = Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2);
                        string sCmd = CModbusFuncCode.WriteRegister.ToString("X2");
                        if (sFeedBack == sCmd)
                        {
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                string sData = CConverter.Bytes2To4HexStr(CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat));
                byte[] byResult = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, SetValue.Length, SetValue.Length * 2, sData);
                if (null == byResult)
                {
                    return false;
                }


                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        string sFeedBack = Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2);
                        string sCmd = CModbusFuncCode.WriteMultiRegister.ToString("X2");
                        if (sFeedBack == sCmd)
                        {
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        /// <summary>
        /// 写从站单个保持寄存器的值(ushort:  0 到 65535)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="SetValue">设置单个保持寄存器的值(ushort:  0 到 65535)</param>
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

                string sData = CConverter.Bytes2To4HexStr(CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat));
                byte[] byResult = PackSingleWriteCmd(DeviceAddress, ModbusFuncCode.WriteRegister, BeginAddress, sData);

                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        string sFeedBack = Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2);
                        string sCmd = CModbusFuncCode.WriteRegister.ToString("X2");
                        if (sFeedBack == sCmd)
                        {
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        /// <summary>
        /// 写从站多个保持寄存器的值(ushort:  0 到 65535)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="SetValue">设置多个保持寄存器的值(ushort:  0 到 65535)</param>
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

                string sData = CConverter.Bytes2To4HexStr(CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat));
                byte[] byResult = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, SetValue.Length, SetValue.Length * 2, sData);
                if (null == byResult)
                {
                    return false;
                }


                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        string sFeedBack = Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2);
                        string sCmd = CModbusFuncCode.WriteMultiRegister.ToString("X2");
                        if (sFeedBack == sCmd)
                        {
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii(" + RS232CPort.PortName + ")通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        /// <summary>
        /// 写int((-2,147,483,648 到 2,147,483,647, 有符号, 32 位整数)
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

                string sData = CConverter.Bytes4To8HexStr(CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat));
                byte[] byResult = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, 2, 4, sData);
                if (null == byResult)
                {
                    return false;
                }


                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        string sFeedBack = Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2);
                        string sCmd = CModbusFuncCode.WriteMultiRegister.ToString("X2");
                        if (sFeedBack == sCmd)
                        {
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        /// <summary>
        /// 写int(-2,147,483,648 到 2,147,483,647, 有符号, 32 位整数)
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

                string sData = CConverter.Bytes4To8HexStr(CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat));
                byte[] byResult = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, SetValue.Length * 2, SetValue.Length * 2 * 2, sData);
                if (null == byResult)
                {
                    return false;
                }


                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        string sFeedBack = Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2);
                        string sCmd = CModbusFuncCode.WriteMultiRegister.ToString("X2");
                        if (sFeedBack == sCmd)
                        {
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        /// <summary>
        /// 写uint(0 到 4,294,967,295)
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

                string sData = CConverter.Bytes4To8HexStr(CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat));
                byte[] byResult = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, 2, 2 * 2, sData);
                if (null == byResult)
                {
                    return false;
                }


                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        string sFeedBack = Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2);
                        string sCmd = CModbusFuncCode.WriteMultiRegister.ToString("X2");
                        if (sFeedBack == sCmd)
                        {
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        /// <summary>
        /// 写uint(0 到 4,294,967,295)
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

                string sData = CConverter.Bytes4To8HexStr(CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat));
                byte[] byResult = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, SetValue.Length * 2, SetValue.Length * 2 * 2, sData);
                if (null == byResult)
                {
                    return false;
                }


                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        string sFeedBack = Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2);
                        string sCmd = CModbusFuncCode.WriteMultiRegister.ToString("X2");
                        if (sFeedBack == sCmd)
                        {
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                string sData = "";
                sData = CConverter.Bytes4To8HexStr(CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat));
                byte[] byResult = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, 2, 4, sData);
                if (null == byResult)
                {
                    return false;
                }


                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        string sFeedBack = Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2);
                        string sCmd = CModbusFuncCode.WriteMultiRegister.ToString("X2");
                        if (sFeedBack == sCmd)
                        {
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                string sData = CConverter.Bytes4To8HexStr(CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat));
                byte[] byResult = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, SetValue.Length * 2, SetValue.Length * 2 * 2, sData);
                if (null == byResult)
                {
                    return false;
                }


                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        string sFeedBack = Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2);
                        string sCmd = CModbusFuncCode.WriteMultiRegister.ToString("X2");
                        if (sFeedBack == sCmd)
                        {
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                string sData = CConverter.Bytes8To16HexStr(CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat));
                byte[] byResult = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, 4, 4 * 2, sData);
                if (null == byResult)
                {
                    return false;
                }


                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        string sFeedBack = Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2);
                        string sCmd = CModbusFuncCode.WriteMultiRegister.ToString("X2");
                        if (sFeedBack == sCmd)
                        {
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                string sData = CConverter.Bytes8To16HexStr(CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat));
                byte[] byResult = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, SetValue.Length * 4, SetValue.Length * 4 * 2, sData);
                if (null == byResult)
                {
                    return false;
                }


                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        string sFeedBack = Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2);
                        string sCmd = CModbusFuncCode.WriteMultiRegister.ToString("X2");
                        if (sFeedBack == sCmd)
                        {
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        /// <summary>
        /// 写long(64位整数值)值到从站保持寄存器(-9,223,372,036,854,775,808 到 9,223,372,036,854,775,807, 有符号 64 位整数)
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

                string sData = CConverter.Bytes8To16HexStr(CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat));
                byte[] byResult = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, 4, 4 * 2, sData);
                if (null == byResult)
                {
                    return false;
                }


                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        string sFeedBack = Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2);
                        string sCmd = CModbusFuncCode.WriteMultiRegister.ToString("X2");
                        if (sFeedBack == sCmd)
                        {
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        /// <summary>
        /// 写long(64位整数值)值到从站保持寄存器(-9,223,372,036,854,775,808 到 9,223,372,036,854,775,807, 有符号 64 位整数)
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

                string sData = CConverter.Bytes8To16HexStr(CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat));
                byte[] byResult = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, SetValue.Length * 4, SetValue.Length * 4 * 2, sData);
                if (null == byResult)
                {
                    return false;
                }


                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        string sFeedBack = Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2);
                        string sCmd = CModbusFuncCode.WriteMultiRegister.ToString("X2");
                        if (sFeedBack == sCmd)
                        {
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        /// <summary>
        /// 写ulong(64位无符号整数值)值到从站保持寄存器(0 到 18,446,744,073,709,551,615)
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

                string sData = CConverter.Bytes8To16HexStr(CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat));
                byte[] byResult = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, 4, 4 * 2, sData);
                if (null == byResult)
                {
                    return false;
                }


                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        string sFeedBack = Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2);
                        string sCmd = CModbusFuncCode.WriteMultiRegister.ToString("X2");
                        if (sFeedBack == sCmd)
                        {
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        /// <summary>
        /// 写ulong(64位无符号整数值)值到从站保持寄存器(0 到 18,446,744,073,709,551,615)
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

                string sData = CConverter.Bytes8To16HexStr(CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat));
                byte[] byResult = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, SetValue.Length * 4, SetValue.Length * 4 * 2, sData);
                if (null == byResult)
                {
                    return false;
                }


                string sFeedBackFromSlave = WriteAndReadBytes(byResult, DeviceAddress);
                if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                {
                    return false;
                }


                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字符串 - " + sFeedBackFromSlave);
                    }
                }

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    return false;
                }


                if (false)
                {
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        string sFeedBack = Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2);
                        string sCmd = CModbusFuncCode.WriteMultiRegister.ToString("X2");
                        if (sFeedBack == sCmd)
                        {
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus RTU通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                    qErrorMsg.Enqueue("Modbus Ascii(" + RS232CPort.PortName + ")" + Msg);
                }
                else
                {
                    qErrorMsg.Dequeue();
                    qErrorMsg.Enqueue("Modbus Ascii(" + RS232CPort.PortName + ")" + Msg);
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
                qReceivedDataQueue.Enqueue(UnpackReceivedAsciiMsg(byReadData));
            }
            catch (Exception ex)
            {
                Enqueue("接收数据发生错误：" + ex.Message + "; " + ex.StackTrace);
            }

        }

        /// <summary>
        /// 将Modbus-Ascii接收到的字节信息进行解析
        /// </summary>
        /// <param name="ReceivedMsg">接收到的字节信息</param>
        /// <returns></returns>
        private CReadbackData UnpackReceivedAsciiMsg(byte[] ReceivedMsg)
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

                if (CalcLRCForReceivedStringFromSlave(sFeedBackFromSlave) == false)
                {
                    gotData.IsLRCOK = false;
                    if (bSaveReceivedStringToLog == true)
                    {
                        Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
                    }
                }
                else
                {
                    gotData.IsLRCOK = true;
                }

                gotData.SlaveAddress = Convert.ToByte(Strings.Mid(sFeedBackFromSlave, PosOfSlaveAddressInAsciiReceivedString, 2));
                gotData.FuncCode = Convert.ToByte(Strings.Mid(sFeedBackFromSlave, PosOfFuncCodeInAsciiReceivedString, 2));
                gotData.FuncDescription = CModbusFuncCode.FuncInfo((ModbusFuncCode)gotData.FuncCode);
                gotData.ErrorCode = Convert.ToByte(Strings.Mid(sFeedBackFromSlave, PosOfErrorCodeInAsciiReceivedString, 2));
                gotData.ErrorMsg = AnalysisErrorCode(ReceivedMsg);
                int iLength = sFeedBackFromSlave.Length;
                int iCopyDataLength = iLength - StartPosOfDataInAsciiReceivedString - 4;
                if (iCopyDataLength > 0)
                {
                    gotData.DataString = Strings.Mid(sFeedBackFromSlave, StartPosOfDataInAsciiReceivedString, iCopyDataLength);
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
        private string WriteAndReadBytes(byte[] byResult, byte DeviceAddress)
        {
            string sReadData = "";
            try
            {
                Lock();
                ClearReceiveBuffer();
                RS232CPort.Write(byResult, 0, byResult.Length);
                bIsConnected = true;
                if (bSaveSendStringToLog == true)
                {
                    string sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);
                    Enqueue("发送字符串 - " + sCmdDataToBeSent);
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

                sReadData = RS232CPort.ReadExisting();
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
            return sReadData;
        }

        /// <summary>
        /// TBD -- 解析返回信息的错误代码
        /// </summary>
        /// <param name="MsgWithErrorCode">从站返回的完整字符串(含错误信息)</param>
        /// <returns></returns>
        public string AnalysisErrorCode(string MsgWithErrorCode)
        {
            if (MsgWithErrorCode.IndexOf(':') != -1)
            {
                string sTemp = Strings.Mid(MsgWithErrorCode, 2, MsgWithErrorCode.Length - 1);
                MsgWithErrorCode = sTemp;
            }

            int iPos = MsgWithErrorCode.IndexOf(RS232CPort.NewLine);
            if (iPos != -1)
            {
                string sTemp = Strings.Mid(MsgWithErrorCode, 1, iPos);
                MsgWithErrorCode = sTemp;
            }

            return AnalysisErrorCode(CConverter.Hex2StrTo1Byte(MsgWithErrorCode));
        }

        /// <summary>
        /// 解析返回信息的错误代码，此字节数组必须是去掉首字符 ':' 和结束符的字符串按照 2个字符1个字节的方式转化为字节数组
        /// </summary>
        /// <param name="MsgWithErrorCode">从站返回的完整字节数组(含错误信息)</param>
        /// <returns></returns>
        public string AnalysisErrorCode(byte[] MsgWithErrorCode)
        {
            CReadbackData Msg = CModbusErrorCode.AnalysisErrorCode(MsgWithErrorCode, ModbusCommType.Ascii);
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
        /// 创建读取命令的字节数组，可以直接发送这个字节数组到串口端口
        /// </summary>
        /// <param name="DeviceAddress">从站地址</param>
        /// <param name="ReadFunctionCode">读取功能码</param>
        /// <param name="BeginReadAddress">读取的起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，有效值范围：1~2000(位)</param>
        /// <returns></returns>
        private static byte[] PackReadCmd(byte DeviceAddress, ModbusFuncCode ReadFunctionCode, ushort BeginReadAddress, ushort ReadDataLength)
        {
            return Converter.Modbus.ModbusAscii.PackReadCmd(DeviceAddress, ReadFunctionCode, BeginReadAddress, ReadDataLength);
        }

        /// <summary>
        /// 封装写单个线圈/保持寄存器命令，返回处理好的字节数组
        /// </summary>
        /// <param name="DeviceAddress">从站地址</param>
        /// <param name="FuncCode">功能码</param>
        /// <param name="BeginAddress">起始地址</param>
        /// <param name="Data">要发送的数据的字节数组</param>
        /// <returns></returns>
        private byte[] PackSingleWriteCmd(byte DeviceAddress, ModbusFuncCode FuncCode, ushort BeginAddress, string Data)
        {
            return Converter.Modbus.ModbusAscii.PackSingleWriteCmd(DeviceAddress, FuncCode, BeginAddress, Data, ParaBytesFormat);

        }

        /// <summary>
        /// 封装写多个线圈/保持寄存器命令，返回处理好的字节数组
        /// </summary>
        /// <param name="DeviceAddress">从站地址</param>
        /// <param name="FuncCode">功能码</param>
        /// <param name="BeginAddress">起始地址</param>
        /// <param name="DataLength">要发送的数据的数量：线圈 -- 位(bool)；寄存器 -- 字(short)</param>
        /// <param name="BytesCount">要发送数据的字节数</param>
        /// <param name="Data">要发送的数据的字节数组</param>
        /// <returns></returns>
        private byte[] PackMultiWriteCmd(byte DeviceAddress, ModbusFuncCode FuncCode, ushort BeginAddress, int DataLength, int BytesCount, string Data)
        {
            return Converter.Modbus.ModbusAscii.PackMultiWriteCmd(DeviceAddress, FuncCode, BeginAddress, DataLength, BytesCount, Data, ParaBytesFormat);

        }

        /// <summary>
        /// 计算LRC值，返回长度为2的字节数组 -- 格式：地址(2个字符) + 功能码(2个字符) + 起始地址(4个字符) + 数据长度(4个字符)
        /// </summary>
        /// <param name="ModbusAsciiCommand">16进制字符串</param>
        /// <returns></returns>
        public static byte[] CalcLRCBytes(string ModbusAsciiCommand)
        {
            return Converter.Modbus.CLRC.CalcLRCBytes(ModbusAsciiCommand);
        }

        /// <summary>
        /// 计算LRC值，返回长度为2的字节数组 -- 格式：地址(2个字符) + 功能码(2个字符) + 起始地址(4个字符) + 数据长度(4个字符)
        /// </summary>
        /// <param name="DataUsedToCalcLRC">用于计算LRC的字节数组</param>
        /// <returns></returns>
        public static byte[] CalcLRCBytes(byte[] DataUsedToCalcLRC)
        {
            return Converter.Modbus.CLRC.CalcLRCBytes(DataUsedToCalcLRC);
        }

        /// <summary>
        /// 计算LRC值，返回值的16进制字符串 -- 格式：地址(2个字符) + 功能码(2个字符) + 起始地址(4个字符) + 数据长度(4个字符)
        /// </summary>
        /// <param name="ModbusAsciiCommand">16进制字符串</param>
        /// <returns></returns>
        public static string CalcLRCString(string ModbusAsciiCommand)
        {
            return Converter.Modbus.CLRC.CalcLRCString(ModbusAsciiCommand);
        }

        /// <summary>
        /// 将客户端返回的字节信息进行计算LRC，确认匹配OK就返回true，否则返回 false
        /// </summary>
        /// <param name="ReceivedBytesFromSlave">客户端返回的字节信息</param>
        /// <returns>匹配OK就返回true，否则返回 false</returns>
        public static bool CalcLRCForReceivedBytesFromSlave(byte[] ReceivedBytesFromSlave)
        {
            return Converter.Modbus.CLRC.CalcLRCForReceivedBytesFromSlave(ReceivedBytesFromSlave);
        }

        /// <summary>
        /// 将客户端返回的字符串信息进行计算LRC，确认匹配OK就返回true，否则返回 false
        /// </summary>
        /// <param name="ReceivedStringFromSlave">客户端返回的字符串信息</param>
        /// <returns>匹配OK就返回true，否则返回 false</returns>
        public static bool CalcLRCForReceivedStringFromSlave(string ReceivedStringFromSlave)
        {
            return Converter.Modbus.CLRC.CalcLRCForReceivedStringFromSlave(ReceivedStringFromSlave);
        }

        #endregion

        #endregion

    }//class

}//namespace