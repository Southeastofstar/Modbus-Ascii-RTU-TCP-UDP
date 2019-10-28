using System;
using System.Collections.Generic;
using System.Text;
using System.IO.Ports;
using System.Windows.Forms;
using System.Diagnostics;
using System.Net.Sockets;
using System.Net;
using ThreadLock;
using Converter;
using Converter.Modbus;

namespace Modbus
{
    /// <summary>
    /// Modbus Socket(TCP/UDP)通讯客户端类，支持跨线程读写操作；如果是单线程操作，请将 EnableThreadLock 设置为 false 以提升性能
    /// </summary>
    public sealed class CModbusSocket : IModbusComm
    {
        /// <summary>
        /// Modbus Socket(TCP/UDP)通讯客户端类的构造函数
        /// </summary>
        /// <param name="DLLPassword">使用此DLL的密码</param>
        /// <param name="TargetServerIPAddress">服务器的IP地址</param>
        /// <param name="TargetServerPort">服务器的端口号</param>
        /// <param name="socketType">套接字通讯类型</param>
        /// <param name="SendTimeout">写超时时间</param>
        /// <param name="ReceiveTimeout">读超时时间</param>
        /// <param name="ReadSlaveDataOnly">是否只读取从站返回的消息，默认：false</param>
        /// <param name="paraBytesFormat">命令参数：多字节数据的格式</param>
        /// <param name="writeKeepRegisterBytesFormat">写数据：多字节数据的格式</param>
        /// <param name="readRegisterBytesFormat">读输入和保持寄存器数据：多字节数据的格式</param>
        /// <param name="ThreadLockType">线程同步锁类型</param>
        public CModbusSocket(string DLLPassword, string TargetServerIPAddress, ushort TargetServerPort, SocketDefinition socketType,
        int SendTimeout = 500, int ReceiveTimeout = 500, bool ReadSlaveDataOnly = false, BytesFormat paraBytesFormat = BytesFormat.BADC,
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

                if (string.IsNullOrEmpty(TargetServerIPAddress) == true)
                {
                    return;
                }

                ParaBytesFormat = paraBytesFormat;
                WriteKeepRegisterBytesFormat = writeKeepRegisterBytesFormat;
                ReadInputRegisterBytesFormat = readRegisterBytesFormat;
                ReadKeepRegisterBytesFormat = readRegisterBytesFormat;
                ReadCoilBytesFormat = BytesFormat.ABCD;
                ReadInputIOBytesFormat = BytesFormat.ABCD;
                WriteCoilBytesFormat = BytesFormat.ABCD;
                #region "判断输入的服务器IP地址是否正确"
                string[] GetCorrectIPAddress = new string[4];
                UInt16[] TempGetIPAddress = new UInt16[4];
                if (!IPAddress.TryParse(TargetServerIPAddress, out ipServerIPAddress))
                {
                    MessageBox.Show("服务器IP地址格式错误，请检查后输入正确IP地址再重新建立新实例.", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                else
                {
                    GetCorrectIPAddress = Strings.Split(TargetServerIPAddress, ".");
                    for (Int16 a = 0; a < 4; a++)
                    {
                        TempGetIPAddress[a] = Convert.ToUInt16(GetCorrectIPAddress[a]);
                        if (TempGetIPAddress[a] > 254 | TempGetIPAddress[a] < 0)
                        {
                            string TempMsg = "";
                            TempMsg = "服务器IP地址: " + TempGetIPAddress[a] + " 超出有效范围【0~255】，请输入正确IP地址.";
                            MessageBox.Show(TempMsg, "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                    }

                    string Str = TempGetIPAddress[0].ToString() + "." + TempGetIPAddress[1].ToString() + "." + TempGetIPAddress[2].ToString() + "." + TempGetIPAddress[3].ToString();
                    ipServerIPAddress = IPAddress.Parse(Str);
                }

                #endregion


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

                SocketTypeForComm = socketType;
                iServerPort = TargetServerPort;
                if (SocketTypeForComm == SocketDefinition.TCP)
                {
                    ThreadConnectServer = new System.Threading.Thread(ConnectRemoteServer);
                    ThreadConnectServer.IsBackground = true;
                    ThreadConnectServer.Start();
                }
                else if (SocketTypeForComm == SocketDefinition.UDP)
                {
                    Client = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    Client.SendTimeout = SendTimeout;
                    Client.ReceiveTimeout = ReceiveTimeout;
                    Client.Connect(ipServerIPAddress, TargetServerPort);
                    bReadSlaveDataOnly = ReadSlaveDataOnly;
                    if (ReadSlaveDataOnly == true)
                    {
                        threadForReadonlyMode = new System.Threading.Thread(ReadonlyModeFunction);
                        threadForReadonlyMode.IsBackground = true;
                        threadForReadonlyMode.Start();
                    }
                }
            }
            catch (Exception ex)
            {
                Enqueue("Modbus Socket 通讯类初始化时发生错误：" + ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Socket 通讯类初始化时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        #region "变量定义"

        /// <summary>
        /// 套接字通讯类型
        /// </summary>
        SocketDefinition SocketTypeForComm = SocketDefinition.TCP;
        /// <summary>
        /// 是否启用心跳包功能，默认：false
        /// </summary>
        bool bEnableHeartBeat = false;
        /// <summary>
        /// 心跳间隔时间，2000ms
        /// </summary>
        int iHeartBeatInterval = 2000;
        /// <summary>
        /// 心跳间隔时间，2000ms，范围：100~200000
        /// </summary>
        int HeartBeatInterval
        {
            get { return iHeartBeatInterval; }
            set
            {
                if (value >= 100 && value <= 200000)
                {
                }
            }
        }

        /// <summary>
        /// 发送的心跳信息
        /// </summary>
        byte[] byHeartBeatMsg = Encoding.UTF8.GetBytes("00000000");
        /// <summary>
        /// 用于计算通讯空闲时间
        /// </summary>
        Stopwatch swUsedUnbusyTime = new Stopwatch();
        /// <summary>
        /// 通讯进行中标志备份
        /// </summary>
        bool bIsBusyBackup = false;
        /// <summary>
        /// 通讯进行中标志
        /// </summary>
        bool bIsBusy = false;
        /// <summary>
        /// 只读模式下读取消息的线程
        /// </summary>
        System.Threading.Thread threadForReadonlyMode = null;
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
        /// 协议标识
        /// </summary>
        public byte[] ProtocolIDCodeBytes
        {
            get { return new byte[] { 0x0, 0x0 }; }
        }

        /// <summary>
        /// 执行写操作时指定的消息ID
        /// </summary>
        public short MsgIDForWriting
        {
            get { return stMsgIDForWriting; }
        }

        /// <summary>
        /// 执行写操作时指定的消息ID
        /// </summary>
        short stMsgIDForWriting = 0;
        /// <summary>
        /// 执行读取操作时指定的消息ID
        /// </summary>
        public short MsgIDForReading
        {
            get { return stMsgIDForReading; }
        }

        /// <summary>
        /// 执行读取操作时指定的消息ID
        /// </summary>
        short stMsgIDForReading = 0;
        /// <summary>
        /// 是否已经与服务器建立连接
        /// </summary>
        public bool IsConnected
        {
            get { return bIsConnected; }
        }

        /// <summary>
        /// 是否已经与服务器建立连接
        /// </summary>
        private bool bIsConnected = false;
        /// <summary>
        /// 释放当前类的对象资源
        /// </summary>
        private bool bIsDisposing = false;
        /// <summary>
        /// 连接服务器端的线程
        /// </summary>
        System.Threading.Thread ThreadConnectServer = null;
        /// <summary>
        /// 目标服务器的IP地址
        /// </summary>
        public string ServerIPAddress
        {
            get { return ipServerIPAddress.ToString(); }
        }

        /// <summary>
        /// 目标服务器的监听端口
        /// </summary>
        public int ServerPort
        {
            get { return iServerPort; }
        }

        /// <summary>
        /// 服务器以太网端口号
        /// </summary>
        private int iServerPort = 8000;
        /// <summary>
        /// 服务器以太网IP地址
        /// </summary>
        private IPAddress ipServerIPAddress;

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
        /// 获取或设置写操作未完成时发生超时之前的毫秒数
        /// </summary>
        public int SendTimeout
        {
            get { return Client.SendTimeout; }
            set { Client.SendTimeout = value; }
        }

        /// <summary>
        /// 获取或设置读取操作未完成时发生超时之前的毫秒数
        /// </summary>
        public int ReceiveTimeout
        {
            get { return Client.ReceiveTimeout; }
            set { Client.ReceiveTimeout = value; }
        }

        /// <summary>
        /// 进行Modbus Socket通讯的Socket实例化对象
        /// </summary>
        Socket Client = null;

        #endregion

        #region "TCP/UDP返回字节信息定义"

        /// <summary>
        /// 接收到的字节数组中，数据长度的索引号[4]~[5]
        /// </summary>
        public const int PosIndexOfDataLengthInSocketReceivedBytes = CModbusFuncCode.PosIndexOfDataLengthInSocketReceivedBytes;
        /// <summary>
        /// 接收到的字节数组中，从站地址的索引号[6]
        /// </summary>
        public const int PosIndexOfSlaveAddressInSocketReceivedBytes = CModbusFuncCode.PosIndexOfSlaveAddressInSocketReceivedBytes;
        /// <summary>
        /// 接收到的字节数组中，功能码的索引号[7]
        /// </summary>
        public const int PosIndexOfFuncCodeInSocketReceivedBytes = CModbusFuncCode.PosIndexOfFuncCodeInSocketReceivedBytes;
        /// <summary>
        /// 接收到的字节数组中，错误码的索引号[7]
        /// </summary>
        public const int PosIndexOfErrorCodeInSocketReceivedBytes = CModbusFuncCode.PosIndexOfFuncCodeInSocketReceivedBytes;
        /// <summary>
        /// 执行读取操作时，接收到的字节数组中，接收到的数据开始的索引号[9]
        /// </summary>
        public const int PosIndexOfDataInSocketReceivedBytes = CModbusFuncCode.PosIndexOfDataInSocketReceivedBytes;

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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, 1);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        if (byReadData[PosIndexOfDataInSocketReceivedBytes] == 1)
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, ReadDataLength * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ByteToBitArray(CConverter.CopyBytes(byReadData, (uint)ReadDataLength, (uint)PosIndexOfDataInSocketReceivedBytes));
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 1 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, 1 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = byReadData[PosIndexOfDataInSocketReceivedBytes];
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 250)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~250(字节)");
                }

                if (BeginAddress + ReadDataLength * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, ReadDataLength * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.CopyBytes(byReadData, (uint)ReadDataLength, (uint)PosIndexOfDataInSocketReceivedBytes);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 1 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, 1 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = (sbyte)byReadData[PosIndexOfDataInSocketReceivedBytes];
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 250)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~250(字节)");
                }

                if (BeginAddress + ReadDataLength * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, ReadDataLength * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        sbyte[] byResultData = new sbyte[ReadDataLength];
                        for (int i = 0; i < ReadDataLength; i++)
                        {
                            byResultData[i] = (sbyte)byReadData[PosIndexOfDataInSocketReceivedBytes + i];
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, 2 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToInt16(CConverter.CopyBytes(byReadData, (uint)(1 * (byte)ByteCount.Short), (uint)PosIndexOfDataInSocketReceivedBytes), ReadCoilBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 125)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                }

                if (BeginAddress + ReadDataLength * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, ReadDataLength * 2 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToInt16Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (byte)ByteCount.Short), (uint)PosIndexOfDataInSocketReceivedBytes), ReadCoilBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, 2 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToUInt16(CConverter.CopyBytes(byReadData, (uint)(1 * (byte)ByteCount.UShort), (uint)PosIndexOfDataInSocketReceivedBytes), ReadCoilBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 125)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                }

                if (BeginAddress + ReadDataLength * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, ReadDataLength * 2 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToUInt16Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (byte)ByteCount.UShort), (uint)PosIndexOfDataInSocketReceivedBytes), ReadCoilBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, 4 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToInt32(CConverter.CopyBytes(byReadData, (uint)(1 * (byte)ByteCount.Int), (uint)PosIndexOfDataInSocketReceivedBytes), ReadCoilBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 125)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                }

                if (BeginAddress + ReadDataLength * 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, ReadDataLength * 4 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToInt32Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (byte)ByteCount.Int), (uint)PosIndexOfDataInSocketReceivedBytes), ReadCoilBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, 4 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToUInt32(CConverter.CopyBytes(byReadData, (uint)(1 * (byte)ByteCount.UInt), (uint)PosIndexOfDataInSocketReceivedBytes), ReadCoilBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 125)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                }

                if (BeginAddress + ReadDataLength * 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, ReadDataLength * 4 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToUInt32Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (byte)ByteCount.UInt), (uint)PosIndexOfDataInSocketReceivedBytes), ReadCoilBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, 8 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToInt64(CConverter.CopyBytes(byReadData, (uint)(1 * (byte)ByteCount.Long), (uint)PosIndexOfDataInSocketReceivedBytes), ReadCoilBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 125)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                }

                if (BeginAddress + ReadDataLength * 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, ReadDataLength * 8 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToInt64Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (byte)ByteCount.Long), (uint)PosIndexOfDataInSocketReceivedBytes), ReadCoilBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, 8 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToUInt64(CConverter.CopyBytes(byReadData, (uint)(1 * (byte)ByteCount.ULong), (uint)PosIndexOfDataInSocketReceivedBytes), ReadCoilBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 125)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                }

                if (BeginAddress + ReadDataLength * 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, ReadDataLength * 8 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToUInt64Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (byte)ByteCount.ULong), (uint)PosIndexOfDataInSocketReceivedBytes), ReadCoilBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, 1);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        if (byReadData[PosIndexOfDataInSocketReceivedBytes] == 1)
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 125)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~250(字节)");
                }

                if (BeginAddress + ReadDataLength * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, ReadDataLength * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ByteToBitArray(CConverter.CopyBytes(byReadData, (uint)ReadDataLength, (uint)PosIndexOfDataInSocketReceivedBytes));
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 1 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, 1 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = byReadData[PosIndexOfDataInSocketReceivedBytes];
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 125)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~250(字节)");
                }

                if (BeginAddress + ReadDataLength * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, ReadDataLength * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (byte)ByteCount.Short), (uint)PosIndexOfDataInSocketReceivedBytes);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 1 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, 1 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = (sbyte)byReadData[PosIndexOfDataInSocketReceivedBytes];
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 125)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~250(字节)");
                }

                if (BeginAddress + ReadDataLength * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, ReadDataLength * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        sbyte[] byResultData = new sbyte[ReadDataLength];
                        for (int i = 0; i < ReadDataLength; i++)
                        {
                            byResultData[i] = (sbyte)byReadData[PosIndexOfDataInSocketReceivedBytes + i];
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 1 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, 2 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToInt16(CConverter.CopyBytes(byReadData, (uint)(1 * (byte)ByteCount.Short), (uint)PosIndexOfDataInSocketReceivedBytes), ReadInputIOBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 125)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                }

                if (BeginAddress + ReadDataLength * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, ReadDataLength * 2 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToInt16Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (byte)ByteCount.Short), (uint)PosIndexOfDataInSocketReceivedBytes), ReadInputIOBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 1 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, 2 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToUInt16(CConverter.CopyBytes(byReadData, (uint)(1 * (byte)ByteCount.UShort), (uint)PosIndexOfDataInSocketReceivedBytes), ReadInputIOBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 125)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                }

                if (BeginAddress + ReadDataLength * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, ReadDataLength * 2 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToUInt16Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (byte)ByteCount.UShort), (uint)PosIndexOfDataInSocketReceivedBytes), ReadInputIOBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, 4 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToInt32(CConverter.CopyBytes(byReadData, (uint)(1 * (byte)ByteCount.Int), (uint)PosIndexOfDataInSocketReceivedBytes), ReadInputIOBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 125)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                }

                if (BeginAddress + ReadDataLength * 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, ReadDataLength * 4 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToInt32Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (byte)ByteCount.Int), (uint)PosIndexOfDataInSocketReceivedBytes), ReadInputIOBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, 4 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToUInt32(CConverter.CopyBytes(byReadData, (uint)(1 * (byte)ByteCount.UInt), (uint)PosIndexOfDataInSocketReceivedBytes), ReadInputIOBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 125)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                }

                if (BeginAddress + ReadDataLength * 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, ReadDataLength * 4 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToUInt32Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (byte)ByteCount.UInt), (uint)PosIndexOfDataInSocketReceivedBytes), ReadInputIOBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, 8 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToInt64(CConverter.CopyBytes(byReadData, (uint)(1 * (byte)ByteCount.Long), (uint)PosIndexOfDataInSocketReceivedBytes), ReadInputIOBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 125)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                }

                if (BeginAddress + ReadDataLength * 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, ReadDataLength * 8 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToInt64Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (byte)ByteCount.Long), (uint)PosIndexOfDataInSocketReceivedBytes), ReadInputIOBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, 8 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToUInt64(CConverter.CopyBytes(byReadData, (uint)(1 * (byte)ByteCount.ULong), (uint)PosIndexOfDataInSocketReceivedBytes), ReadInputIOBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 125)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                }

                if (BeginAddress + ReadDataLength * 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, ReadDataLength * 8 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToUInt64Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (byte)ByteCount.ULong), (uint)PosIndexOfDataInSocketReceivedBytes), ReadInputIOBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, 1);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToInt16(CConverter.CopyBytes(byReadData, (uint)(1 * (byte)ByteCount.Short), (uint)PosIndexOfDataInSocketReceivedBytes), ReadInputRegisterBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 125)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~250(字节)");
                }

                if (BeginAddress + ReadDataLength * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, ReadDataLength);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToInt16Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (byte)ByteCount.Short), (uint)PosIndexOfDataInSocketReceivedBytes), ReadInputRegisterBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, 1);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToUInt16(CConverter.CopyBytes(byReadData, (uint)(1 * (byte)ByteCount.UShort), (uint)PosIndexOfDataInSocketReceivedBytes), ReadInputRegisterBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 125)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~250(字节)");
                }

                if (BeginAddress + ReadDataLength * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, ReadDataLength);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToUInt16Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (byte)ByteCount.UShort), (uint)PosIndexOfDataInSocketReceivedBytes), ReadInputRegisterBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, 2);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToInt32(CConverter.CopyBytes(byReadData, (uint)(1 * (byte)ByteCount.Int), (uint)PosIndexOfDataInSocketReceivedBytes), ReadInputRegisterBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 125)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                }

                if (BeginAddress + ReadDataLength * 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, ReadDataLength * 2);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToInt32Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (byte)ByteCount.Int), (uint)PosIndexOfDataInSocketReceivedBytes), ReadInputRegisterBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, 2);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToUInt32(CConverter.CopyBytes(byReadData, (uint)(1 * (byte)ByteCount.UInt), (uint)PosIndexOfDataInSocketReceivedBytes), ReadInputRegisterBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 125)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                }

                if (BeginAddress + ReadDataLength * 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, ReadDataLength * 2);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToUInt32Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (byte)ByteCount.UInt), (uint)PosIndexOfDataInSocketReceivedBytes), ReadInputRegisterBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, 2);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToFloat(CConverter.CopyBytes(byReadData, (uint)(1 * (byte)ByteCount.Float), (uint)PosIndexOfDataInSocketReceivedBytes), ReadInputRegisterBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, ReadDataLength * 2);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToFloatArray(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (byte)ByteCount.Float), (uint)PosIndexOfDataInSocketReceivedBytes), ReadInputRegisterBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 【?? 测试读取数据时不能像其它功能码一样正确读取double值，在float范围内可以正确读取，其它会对应不上，待更多测试】
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, 4);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToDouble(CConverter.CopyBytes(byReadData, (uint)(1 * (byte)ByteCount.Double), (uint)PosIndexOfDataInSocketReceivedBytes), ReadInputRegisterBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 【?? 测试读取数据时不能像其它功能码一样正确读取double值，在float范围内可以正确读取，其它会对应不上，待更多测试】
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 4 * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, ReadDataLength * 4);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToDoubleArray(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (byte)ByteCount.Double), (uint)PosIndexOfDataInSocketReceivedBytes), ReadInputRegisterBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, 4);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToInt64(CConverter.CopyBytes(byReadData, (uint)(1 * (byte)ByteCount.Long), (uint)PosIndexOfDataInSocketReceivedBytes), ReadInputRegisterBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, ReadDataLength * 4);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToInt64Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (byte)ByteCount.Long), (uint)PosIndexOfDataInSocketReceivedBytes), ReadInputRegisterBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 【测试读取数据时不能像其它功能码一样正确读取ulong值，在int范围内可以正确读取，其它会对应不上，待更多测试】
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, 4);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToUInt64(CConverter.CopyBytes(byReadData, (uint)(1 * (byte)ByteCount.ULong), (uint)PosIndexOfDataInSocketReceivedBytes), ReadInputRegisterBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }
        }

        /// <summary>
        /// 【测试读取数据时不能像其它功能码一样正确读取ulong值，在int范围内可以正确读取，其它会对应不上，待更多测试】
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, ReadDataLength * 4);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToUInt64Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (byte)ByteCount.ULong), (uint)PosIndexOfDataInSocketReceivedBytes), ReadInputRegisterBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, 1);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToInt16(CConverter.CopyBytes(byReadData, (uint)(1 * (byte)ByteCount.Short), (uint)PosIndexOfDataInSocketReceivedBytes), ReadKeepRegisterBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 125)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~250(字节)");
                }

                if (BeginAddress + ReadDataLength * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, ReadDataLength);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToInt16Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (byte)ByteCount.Short), (uint)PosIndexOfDataInSocketReceivedBytes), ReadKeepRegisterBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, 1);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToUInt16(CConverter.CopyBytes(byReadData, (uint)(1 * (byte)ByteCount.UShort), (uint)PosIndexOfDataInSocketReceivedBytes), ReadKeepRegisterBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 125)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~250(字节)");
                }

                if (BeginAddress + ReadDataLength * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, ReadDataLength);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToUInt16Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (byte)ByteCount.UShort), (uint)PosIndexOfDataInSocketReceivedBytes), ReadKeepRegisterBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, 2);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToInt32(CConverter.CopyBytes(byReadData, (uint)(1 * (byte)ByteCount.Int), (uint)PosIndexOfDataInSocketReceivedBytes), ReadKeepRegisterBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 125)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                }

                if (BeginAddress + ReadDataLength * 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, ReadDataLength * 2);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToInt32Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (byte)ByteCount.Int), (uint)PosIndexOfDataInSocketReceivedBytes), ReadKeepRegisterBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, 2);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToUInt32(CConverter.CopyBytes(byReadData, (uint)(1 * (byte)ByteCount.UInt), (uint)PosIndexOfDataInSocketReceivedBytes), ReadKeepRegisterBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 125)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                }

                if (BeginAddress + ReadDataLength * 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, ReadDataLength * 2);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToUInt32Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (byte)ByteCount.UInt), (uint)PosIndexOfDataInSocketReceivedBytes), ReadKeepRegisterBytesFormat, 0);
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, 2);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToFloat(CConverter.CopyBytes(byReadData, (uint)(1 * (byte)ByteCount.Float), (uint)PosIndexOfDataInSocketReceivedBytes), ReadKeepRegisterBytesFormat, 0);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, ReadDataLength * 2);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToFloatArray(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (byte)ByteCount.Float), (uint)PosIndexOfDataInSocketReceivedBytes), ReadKeepRegisterBytesFormat, 0);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, 4);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToDouble(CConverter.CopyBytes(byReadData, (uint)(1 * (byte)ByteCount.Double), (uint)PosIndexOfDataInSocketReceivedBytes), ReadKeepRegisterBytesFormat, 0);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, ReadDataLength * 4);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToDoubleArray(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (byte)ByteCount.Double), (uint)PosIndexOfDataInSocketReceivedBytes), ReadKeepRegisterBytesFormat, 0);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, 4);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToInt64(CConverter.CopyBytes(byReadData, (uint)(1 * (byte)ByteCount.Long), (uint)PosIndexOfDataInSocketReceivedBytes), ReadKeepRegisterBytesFormat, 0);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, ReadDataLength * 4);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToInt64Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (byte)ByteCount.Long), (uint)PosIndexOfDataInSocketReceivedBytes), ReadKeepRegisterBytesFormat, 0);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, 4);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToUInt64(CConverter.CopyBytes(byReadData, (uint)(1 * (byte)ByteCount.ULong), (uint)PosIndexOfDataInSocketReceivedBytes), ReadKeepRegisterBytesFormat, 0);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, ReadDataLength * 4);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        Value = CConverter.ToUInt64Array(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (byte)ByteCount.ULong), (uint)PosIndexOfDataInSocketReceivedBytes), ReadKeepRegisterBytesFormat, 0);
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

        #region "写 - ok"

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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                byte[] byData = new byte[2];
                if (IsOn == true)
                {
                    byData[0] = 0xFF;
                    byData[1] = 0X00;
                }
                else
                {
                    byData[0] = 0x00;
                    byData[1] = 0X00;
                }

                byData = CConverter.Reorder2BytesData(byData, WriteCoilBytesFormat, 0);
                byte[] byDataToBeSent = PackSingleWriteCmd(DeviceAddress, ModbusFuncCode.WriteCoil, BeginAddress, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                byte[] byBitArrayToBytes = CConverter.BitArrayToByte(SetValue);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length, byBitArrayToBytes);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
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
                    MessageBox.Show("Modbus Socket 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress + 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, 8, new byte[] { SetValue });
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (null == SetValue || SetValue.Length < 1)
                {
                    throw new Exception("设置值数组不能为空");
                }

                if (BeginAddress + SetValue.Length * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length * 8, SetValue);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress + 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, 8, new byte[] { (byte)SetValue });
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (null == SetValue || SetValue.Length < 1)
                {
                    throw new Exception("设置值数组不能为空");
                }

                if (BeginAddress + SetValue.Length * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byTemp = new byte[SetValue.Length];
                for (int i = 0; i < byTemp.Length; i++)
                {
                    byTemp[i] = (byte)SetValue[i];
                }

                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, byTemp.Length * 8, byTemp);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, 2 * 8, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (null == SetValue || SetValue.Length < 1)
                {
                    throw new Exception("设置值数组不能为空");
                }

                if (BeginAddress + SetValue.Length * 2 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length * 2 * 8, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, 2 * 8, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (null == SetValue || SetValue.Length < 1)
                {
                    throw new Exception("设置值数组不能为空");
                }

                if (BeginAddress + SetValue.Length * 2 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length * 2 * 8, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, 4 * 8, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (null == SetValue || SetValue.Length < 1)
                {
                    throw new Exception("设置值数组不能为空");
                }

                if (BeginAddress + SetValue.Length * 4 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length * 4 * 8, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, 4 * 8, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (null == SetValue || SetValue.Length < 1)
                {
                    throw new Exception("设置值数组不能为空");
                }

                if (BeginAddress + SetValue.Length * 4 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length * 4 * 8, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, 8 * 8, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (null == SetValue || SetValue.Length < 1)
                {
                    throw new Exception("设置值数组不能为空");
                }

                if (BeginAddress + SetValue.Length * 8 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length * 8 * 8, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, 8 * 8, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (null == SetValue || SetValue.Length < 1)
                {
                    throw new Exception("设置值数组不能为空");
                }

                if (BeginAddress + SetValue.Length * 8 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length * 8 * 8, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackSingleWriteCmd(DeviceAddress, ModbusFuncCode.WriteRegister, BeginAddress, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                byte[] byData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, SetValue.Length, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackSingleWriteCmd(DeviceAddress, ModbusFuncCode.WriteRegister, BeginAddress, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                byte[] byData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, SetValue.Length, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, 2, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                byte[] byData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, SetValue.Length * 2, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, 2, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                byte[] byData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, SetValue.Length * 2, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, 2, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                byte[] byData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, SetValue.Length * 2, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, 4, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                byte[] byData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, SetValue.Length * 4, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, 4, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                byte[] byData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, SetValue.Length * 4, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (BeginAddress < 0 || BeginAddress > 65535)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                byte[] byData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, 4, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                byte[] byData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, SetValue.Length * 4, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "以字节方式处理从站的返回结果"
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                    && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
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
                    MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

        }

        #endregion

        #endregion

        #region "函数代码"

        #region "操作"

        /// <summary>
        /// 清除发送前的接收缓冲区
        /// </summary>
        private void ClearReceiveBuffer()
        {
            try
            {
                if (Client.Available > 0)
                {
                    byte[] byTemp2 = new byte[Client.Available];
                    Client.Receive(byTemp2);
                    byTemp2 = null;
                }
            }
            catch (Exception)
            {
            }
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
        ///
        /// </summary>
        bool bDisconnect = false;

        /// <summary>
        /// 已经
        /// </summary>
        bool bBackupOfConnectedFlag = false;

        private void ConnectRemoteServer()
        {
            while (bIsDisposing == false)
            {
                try
                {
                    if (null == Client)
                    {
                        bIsConnected = false;
                    }
                    else
                    {
                        bIsConnected = Client.Connected;
                    }

                    if (bEnableHeartBeat == true)
                    {
                        #region "心跳信息"
                        if (bIsBusyBackup != bIsBusy)
                        {
                            bIsBusyBackup = bIsBusy;
                            swUsedUnbusyTime.Restart();
                        }

                        if (swUsedUnbusyTime.ElapsedMilliseconds >= iHeartBeatInterval)
                        {
                            if (null != Client && Client.Connected == true)
                            {
                                try
                                {
                                    swUsedUnbusyTime.Restart();
                                    if (bReadSlaveDataOnly == false)
                                    {
                                    }
                                }
                                catch (Exception)
                                {
                                }
                            }
                        }

                        #endregion
                    }

                    if (bBackupOfConnectedFlag != bIsConnected)
                    {
                        if (bBackupOfConnectedFlag == true && bIsConnected == false)
                        {
                            bDisconnect = true;
                        }
                        else
                        {
                            bDisconnect = false;
                        }

                        bBackupOfConnectedFlag = bIsConnected;
                    }

                    if (bDisconnect == true && null != Client)
                    {
                        bIsConnected = false;
                        bDisconnect = false;
                        try
                        {
                            Client.Shutdown(SocketShutdown.Both);
                            Client.Close();
                            Client.Dispose();
                        }
                        catch (Exception)
                        {
                        }
                        Client = null;
                    }

                    if (null == Client)
                    {
                        bIsConnected = false;
                        Client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        Client.SendTimeout = SendTimeout;
                        Client.ReceiveTimeout = ReceiveTimeout;
                        Client.LingerState = new LingerOption(false, 1);
                        Client.Blocking = true;
                        while (bIsConnected == false && bIsDisposing == false)
                        {
                            try
                            {
                                Client.Connect(ipServerIPAddress, iServerPort);
                                bIsConnected = true;
                                bIsBusy = true;
                            }
                            catch (Exception)
                            {
                                bIsConnected = false;
                            }
                        }
                    }

                    if (null != Client)
                    {
                        if (Client.Connected == false)
                        {
                            System.Threading.Thread.Sleep(10);
                            bIsConnected = false;
                            try
                            {
                            }
                            catch (Exception)
                            {
                            }
                        }
                        else
                        {
                            if (bReadSlaveDataOnly == true)
                            {
                                try
                                {
                                    if (Client.Available > 2)
                                    {
                                        bIsBusy = true;
                                        byte[] byReadData = new byte[Client.Available];
                                        Client.Receive(byReadData);
                                        if (bSaveReceivedStringToLog == true)
                                        {
                                            string sFeedBackFromSlave = CConverter.Bytes1To2HexStr(byReadData);
                                            if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                                            {
                                                Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                                            }
                                        }

                                        qReceivedDataQueue.Enqueue(UnpackReceivedRTUMsg(byReadData));
                                    }
                                }
                                catch (Exception)
                                {
                                    bIsBusy = false;
                                }
                            }
                            else
                            {
                                bIsConnected = true;
                                System.Threading.Thread.Sleep(10);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (sErrorMsgForThread != ex.Message)
                    {
                        sErrorMsgForThread = ex.Message;
                        Enqueue(ex.Message);
                    }

                }
            }
        }

        string sErrorMsgForThread = "";

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            try
            {
                bIsDisposing = true;
                Close();
                if (null != Client)
                {
                    Client.Dispose();
                    Client = null;
                }

                if (null != ThreadConnectServer)
                {
                    ThreadConnectServer.Abort();
                    ThreadConnectServer = null;
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 打开Socket端口
        /// </summary>
        /// <returns></returns>
        private bool Open()
        {
            try
            {
                if (Client.Connected == false)
                {
                    Client.Connect(ipServerIPAddress, iServerPort);
                }
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 关闭Socket端口
        /// </summary>
        /// <returns></returns>
        private bool Close()
        {
            try
            {
                if (Client.Connected == true)
                {
                    Client.Disconnect(true);
                    Client.Close();
                }
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 过滤掉重复信息
        /// </summary>
        string sFilterRepeatMsg = "";

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

                if (sFilterRepeatMsg == Msg)
                {
                    return true;
                }
                else
                {
                    sFilterRepeatMsg = Msg;
                }

                if (qErrorMsg.Count < int.MaxValue)
                {
                    qErrorMsg.Enqueue("(" + ServerIPAddress + ":" + iServerPort.ToString() + ")" + Msg);
                }
                else
                {
                    qErrorMsg.Dequeue();
                    qErrorMsg.Enqueue("(" + ServerIPAddress + ":" + iServerPort.ToString() + ")" + Msg);
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

        private void ReadonlyModeFunction()
        {
            while (bIsDisposing == false)
            {
                try
                {
                    while (Client.Available < 2)
                    {
                        Application.DoEvents();
                        System.Threading.Thread.Sleep(iSleepTime);
                    }

                    byte[] byReadData = new byte[Client.Available];
                    Client.Receive(byReadData);
                    if (bSaveReceivedStringToLog == true)
                    {
                        string sFeedBackFromSlave = CConverter.Bytes1To2HexStr(byReadData);
                        if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                        {
                            Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                        }
                    }

                    qReceivedDataQueue.Enqueue(UnpackReceivedRTUMsg(byReadData));
                }
                catch (Exception)
                {
                }
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

                gotData.SlaveAddress = ReceivedMsg[PosIndexOfSlaveAddressInSocketReceivedBytes];
                gotData.FuncCode = ReceivedMsg[PosIndexOfFuncCodeInSocketReceivedBytes];
                gotData.FuncDescription = CModbusFuncCode.FuncInfo((ModbusFuncCode)gotData.FuncCode);
                gotData.ErrorCode = ReceivedMsg[PosIndexOfErrorCodeInSocketReceivedBytes];
                gotData.ErrorMsg = AnalysisErrorCode(ReceivedMsg);
                int iLength = ReceivedMsg.Length;
                int iCopyDataLength = iLength - PosIndexOfDataInSocketReceivedBytes;
                if (iCopyDataLength > 0)
                {
                    gotData.DataBytes = new byte[iCopyDataLength];
                    Array.Copy(ReceivedMsg, PosIndexOfDataInSocketReceivedBytes, gotData.DataBytes, 0, iCopyDataLength);
                }
            }
            catch (Exception ex)
            {
                gotData.ErrorMsg = ex.Message + "; " + ex.StackTrace;
            }

            return gotData;
        }

        /// <summary>
        /// 解析返回信息的错误代码
        /// </summary>
        /// <param name="MsgWithErrorCode">从站返回的完整字节数组(含错误信息)</param>
        /// <returns></returns>
        public string AnalysisErrorCode(byte[] MsgWithErrorCode)
        {
            CReadbackData Msg = CModbusErrorCode.AnalysisErrorCode(MsgWithErrorCode, ModbusCommType.Socket);
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
        /// 执行读写时的错误信息
        /// </summary>
        string sErrorMsgForReadWrite = "";

        /// <summary>
        /// 发送数据到Socket并接收Socket返回的数据
        /// </summary>
        /// <param name="byResult">要发送的字节数据</param>
        /// <param name="DeviceAddress">目标从站地址</param>
        /// <returns></returns>
        private byte[] WriteAndReadBytes(byte[] byResult, byte DeviceAddress)
        {
            byte[] byReadData = null;
            try
            {
                if (bReadSlaveDataOnly == true || (Client.ProtocolType == ProtocolType.Tcp && bIsConnected == false))
                {
                    return null;
                }

                Lock();
                ClearReceiveBuffer();
                Client.Send(byResult);
                bIsBusy = true;
                if (bSaveSendStringToLog == true)
                {
                    string sTemp = CConverter.Bytes1To2HexStr(byResult);
                    Enqueue("发送字节转换为16进制 - " + sTemp);
                }

                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();
                while (Client.Available < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        bIsConnected = false;
                        swUsedTime = null;
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byReadData = new byte[Client.Available];
                Client.Receive(byReadData);
                bIsConnected = true;
                swUsedTime = null;
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
                if (sErrorMsgForReadWrite != ex.Message)
                {
                    sErrorMsgForReadWrite = ex.Message;
                    Enqueue("通讯时发生错误：" + ex.Message + "  " + ex.StackTrace);
                }
            }

            bIsBusy = false;
            Unlock();
            return byReadData;
        }

        /// <summary>
        /// 创建写保持寄存器命令的字节数组，可以直接发送这个字节数组到Socket端口
        /// </summary>
        /// <param name="DeviceAddress">从站地址</param>
        /// <param name="FuncCode">功能码</param>
        /// <param name="BeginAddress">起始地址</param>
        /// <param name="Data">发送的数据(字节)</param>
        /// <param name="DataLength">要发送的数据的数量：线圈 -- 位(bool)；寄存器 -- 字(short)</param>
        /// <returns></returns>
        private byte[] PackMultiWriteCmd(byte DeviceAddress, ModbusFuncCode FuncCode, ushort BeginAddress, int DataLength, byte[] Data)
        {
            if (null == Data || Data.Length < 1)
            {
                return null;
            }

            if (FuncCode != ModbusFuncCode.WriteMultiCoil && FuncCode != ModbusFuncCode.WriteMultiRegister)
            {
                return null;
            }

            if (stMsgIDForWriting >= short.MaxValue)
            {
                stMsgIDForWriting = 0;
            }

            stMsgIDForWriting++;
            return Converter.Modbus.ModbusSocket.PackMultiWriteCmd(DeviceAddress, FuncCode, BeginAddress, DataLength, Data, stMsgIDForWriting, ProtocolIDCodeBytes, ParaBytesFormat);
        }

        /// <summary>
        /// 创建写保持寄存器命令的字节数组，可以直接发送这个字节数组到Socket端口
        /// </summary>
        /// <param name="DeviceAddress">从站地址</param>
        /// <param name="FuncCode">功能码</param>
        /// <param name="BeginAddress">起始地址</param>
        /// <param name="Data">发送的数据(字节)</param>
        /// <returns></returns>
        private byte[] PackSingleWriteCmd(byte DeviceAddress, ModbusFuncCode FuncCode, ushort BeginAddress, byte[] Data)
        {
            if (null == Data || Data.Length != 2)
            {
                return null;
            }

            if (FuncCode != ModbusFuncCode.WriteCoil && FuncCode != ModbusFuncCode.WriteRegister)
            {
                return null;
            }

            if (stMsgIDForWriting >= short.MaxValue)
            {
                stMsgIDForWriting = 0;
            }

            stMsgIDForWriting++;
            return Converter.Modbus.ModbusSocket.PackSingleWriteCmd(DeviceAddress, FuncCode, BeginAddress, Data, stMsgIDForWriting, ProtocolIDCodeBytes, ParaBytesFormat);
        }

        /// <summary>
        /// 创建读取命令的字节数组，可以直接发送这个字节数组到Socket端口
        /// </summary>
        /// <param name="DeviceAddress">从站地址</param>
        /// <param name="FuncCode">功能码</param>
        /// <param name="BeginAddress">起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，有效值范围：1~2000(位)</param>
        /// <returns></returns>
        private byte[] PackReadCmd(byte DeviceAddress, ModbusFuncCode FuncCode, ushort BeginAddress, int ReadDataLength)
        {
            if (FuncCode != ModbusFuncCode.ReadCoil && FuncCode != ModbusFuncCode.ReadInputRegister
            && FuncCode != ModbusFuncCode.ReadInputSignal && FuncCode != ModbusFuncCode.ReadRegister)
            {
                return null;
            }

            if (ReadDataLength < 1)
            {
                ReadDataLength = 1;
            }

            if (stMsgIDForReading >= short.MaxValue)
            {
                stMsgIDForReading = 0;
            }

            stMsgIDForReading++;
            return Converter.Modbus.ModbusSocket.PackReadCmd(DeviceAddress, FuncCode, BeginAddress, ReadDataLength, stMsgIDForReading, ProtocolIDCodeBytes, ParaBytesFormat);
        }

        #endregion

        #endregion

    }//class

}//namespace