#define TEST

using System;
using System.Collections.Generic;
using System.Text;
using System.IO.Ports;
//using System.Windows.Forms;
using System.Diagnostics;
using Microsoft.VisualBasic;
using System.Net.Sockets;
using System.Net;

using ThreadLock;
using Converter;
using Converter.Modbus;

//-1, 
namespace ModbusComm
{
    //后续可以考虑在收到从站返回的信息后，处理数据前先添加CRC校验检查，以及处理返回结果的字节顺序：ABCD/BADC/CDAB/DCBA

    #region "MODBUS Socket 报文格式 -- 使用二进制码进行发送和接收 - 不需要CRC"

    //读取错误
    //Tx:00 08 00 00 00 06 01 03 00 00 00 0A
    //Rx:00 08 00 00 00 03 01 83 01

    //*********************************************
    //读取单个线圈的通信记录：
    //读取结果 - OFF
    //Tx:00 15 00 00 00 06 01 01 00 00 00 01
    //Rx:00 15 00 00 00 04 01 01 01 00

    //读取结果 - ON
    //Tx:00 16 00 00 00 06 01 01 00 00 00 01
    //Rx:00 16 00 00 00 04 01 01 01 01

    //读取格式：
    //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
    //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
    //ushort				        一般为0										     			                            ushort
    //高低字节交换			        	        								                                            高低字节交换

    //*********************************************
    //写错误
    //Tx:00 0B 00 00 00 06 01 03 00 00 00 0A
    //Rx:00 0B 00 00 00 03 01 83 01

    //写单个线圈的通信记录：
    //Tx:00 25 00 00 00 06 01 05 00 00 FF 00
    //Rx:00 25 00 00 00 06 01 05 00 00 FF 00
    //Tx:00 26 00 00 00 06 01 05 00 00 00 00
    //Rx:00 26 00 00 00 06 01 05 00 00 00 00

    //写格式：
    //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2N个字节(序号：10~11)
    //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  写数据
    //ushort				        一般为0										     			                            ushort
    //高低字节交换			        	        								                                            高低字节交换

    //写多个线圈格式：
    //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  2个字节(序号：10~11)  1个字节(序号：12)  N个字节(序号：13~(N-1))
    //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  数据位数量            字节数量           写数据
    //ushort				      一般为0										     			              ushort
    //高低字节交换			        	        								                              高低字节交换

    //写单个保持寄存器格式：
    //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  N个字节(序号：10~(N-1))
    //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  写数据
    //ushort				      一般为0										     			              ushort
    //高低字节交换			        	        								                              高低字节交换

    //写多个保持寄存器格式：
    //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  2个字节(序号：10~11)  1个字节(序号：12)  N个字节(序号：13~(N-1))
    //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  数据位数量            字节数量           写数据
    //ushort				      一般为0										     			              ushort
    //高低字节交换			        	        								                              高低字节交换

    #endregion

    /// <summary>
    /// Modbus Socket(TCP/UDP)通讯客户端类，支持跨线程读写操作；如果是单线程操作，请将 EnableThreadLock 设置为 false 以提升性能
    /// </summary>
    public sealed class CModbusSocket : IModbusComm
    {
        //, uint LocalStationNo

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

                //
                if (!IPAddress.TryParse(TargetServerIPAddress, out ipServerIPAddress))
                {
                    //The format of IP address for the Socket/IP Server is not correct, please correct it.\r\n
                    //MessageBox.Show("服务器IP地址格式错误，请检查后输入正确IP地址再重新建立新实例.", "", //MessageBoxButtons.OK, //MessageBoxIcon.Error);
                    return;
                }
                else
                {
                    //此处解析IP地址不对："192.168.000.024"解析为"192.168.000.20"
                    GetCorrectIPAddress = Strings.Split(TargetServerIPAddress, ".");
                    for (Int16 a = 0; a < 4; a++)
                    {
                        TempGetIPAddress[a] = Convert.ToUInt16(GetCorrectIPAddress[a]);
                        if (TempGetIPAddress[a] > 254 | TempGetIPAddress[a] < 0)
                        {
                            string TempMsg = "";
                            //The IP address of server: " + TempGetIPAddress[a] + " is over the range, the correct range for IP address is between 0~255, please correct it.\r\n
                            TempMsg = "服务器IP地址: " + TempGetIPAddress[a] + " 超出有效范围【0~255】，请输入正确IP地址.";
                            //MessageBox.Show(TempMsg, "", //MessageBoxButtons.OK, //MessageBoxIcon.Error);
                            return;
                        }
                    }

                    //设置IP地址时如果某节点不是全部为零0值，则必须去掉此节点的前缀0，否则会报错
                    string Str = TempGetIPAddress[0].ToString() + "." + TempGetIPAddress[1].ToString() + "." + TempGetIPAddress[2].ToString() + "." + TempGetIPAddress[3].ToString();
                    ipServerIPAddress = IPAddress.Parse(Str);
                }

                #endregion

                #region "保活时间"

                ////命名空间:           System.Net
                ////程序集：   System在 System.dll 中
                ////语法
                ////Public Shared Sub SetSocketKeepAlive(ByVal enabled As Boolean, ByVal keepAliveTime As Integer, ByVal keepAliveInterval As Integer)
                ////参数
                ////enabled
                ////类型:     System.Boolean
                ////如果设置为 true，则将使用指定的 keepAliveTime 和 keepAliveInterval 值启用 Socket 连接上的 Socket keep-alive 选项。 
                ////如果设置为 false，则将禁用 Socket keep-alive 选项，并忽略剩余参数。
                ////默认值为 false。
                ////keepAliveTime
                ////类型:     System.Int32
                ////指定发送第一个 keep-alive 数据包之前没有活动的超时时间（以毫秒为单位）。
                ////该值必须大于 0。如果传递的值小于或等于零，则会引发 ArgumentOutOfRangeException。 
                ////keepAliveInterval
                ////类型:     System.Int32
                ////指定当未收到确认消息时发送连续 keep-alive 数据包之间的间隔（以毫秒为单位）。
                ////该值必须大于 0。如果传递的值小于或等于零，则会引发 ArgumentOutOfRangeException。 
                //System.Net.ServicePointManager.SetSocketKeepAlive(true, 10, 100);

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

                    //case LockerType.CountdownEventLock:
                    //    SyncLocker = new CCountdownEventLock();

                    //    break;
                }

                SocketTypeForComm = socketType;
                iServerPort = TargetServerPort;

                if (SocketTypeForComm == SocketDefinition.TCP)
                {
                    //Client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Socket);
                    //Client.SendTimeout = SendTimeout;
                    //Client.ReceiveTimeout = ReceiveTimeout;

                    ////以太网的有效端口从0~65535，刚好是UShort的值范围，所以不需要进行验证有效性
                    //iServerPort = TargetServerPort;

                    //Client.Connect(TargetServerIPAddress, TargetServerPort);
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
                    //MessageBox.Show("Modbus Socket 通讯类初始化时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
        public BytesFormat ParaBytesFormat { get; set; }//internal

        /// <summary>
        /// 写线圈数据：多字节数据的格式
        /// </summary>
        public BytesFormat WriteCoilBytesFormat { get; set; }//internal

        /// <summary>
        /// 写保持寄存器数据：多字节数据的格式
        /// </summary>
        public BytesFormat WriteKeepRegisterBytesFormat { get; set; }//internal

        /// <summary>
        /// 读输入寄存器数据：多字节数据的格式
        /// </summary>
        public BytesFormat ReadInputRegisterBytesFormat { get; set; }

        /// <summary>
        /// 读保持寄存器数据：多字节数据的格式
        /// </summary>
        public BytesFormat ReadKeepRegisterBytesFormat { get; set; }//internal

        /// <summary>
        /// 读线圈数据：多字节数据的格式，默认值：BytesFormat.ABCD
        /// </summary>
        public BytesFormat ReadCoilBytesFormat { get; set; }//internal

        /// <summary>
        /// 读输入离散信号数据：多字节数据的格式，默认值：BytesFormat.ABCD
        /// </summary>
        public BytesFormat ReadInputIOBytesFormat { get; set; }//internal

        /// <summary>
        /// 回复帧的最小长度:2
        /// </summary>
        const int iMinLengthOfResponse = 2;

        /// <summary>
        /// 同步线程锁
        /// </summary>
        ITheadLock SyncLocker = null;// new CAutoResetEventLock();//CAutoResetEventLock

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

        #region "No use"
        
        //弄错误了，这里应该是读写操作的起始地址
        ///// <summary>
        ///// 本地Modbus站号地址
        ///// </summary>
        //public byte LocalModbusStationAddress
        //{
        //    get
        //    {
        //        try
        //        {
        //            return byLocalModbusStationAddress;
        //            //return (byte)BitConverter.ToInt16(byLocalModbusStationAddress, 0);
        //        }
        //        catch (Exception)
        //        {
        //            return 0x0;
        //        }
        //    }

        //    set
        //    {
        //        try
        //        {
        //            byLocalModbusStationAddress = value;
        //            //byLocalModbusStationAddress = CConverter.ToBytes((short)value, ParaBytesFormat);

        //            //byte[] byTemp = BitConverter.GetBytes((short)value);
        //            //byLocalModbusStationAddress[0] = byTemp[0];
        //            //byLocalModbusStationAddress[1] = byTemp[1];
        //        }
        //        catch (Exception)
        //        {
        //        }
        //    }
        //}

        ///// <summary>
        ///// 本地Modbus站号地址
        ///// </summary>
        //byte byLocalModbusStationAddress =  0x0;//byte[] byLocalModbusStationAddress = new byte[] { 0x0, 0x0 }

        #endregion
        
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

        //写单个线圈的通信记录：
        //Tx:00 25 00 00 00 06 01 05 00 00 FF 00
        //Rx:00 25 00 00 00 06 01 05 00 00 FF 00
        //Tx:00 26 00 00 00 06 01 05 00 00 00 00
        //Rx:00 26 00 00 00 06 01 05 00 00 00 00
        //   0  1  2  3  4  5  6  7  8  9  10 11

        //********************** 
        //TCP/UDP返回字节信息定义
        //[0] - 消息ID[0]
        //[1] - 消息ID[1]
        //[2] - 协议标识[0]
        //[3] - 协议标识[1]
        //[4] - 数据长度[0](索引号从0开始计算)
        //[5] - 数据长度[1](索引号从0开始计算)
        //[6] - 从站地址(索引号从0开始计算)
        //[7] - 功能码(成功)(索引号从0开始计算)；失败的错误码 | 功能码(索引号从0开始计算) = 0x8*，此处的 * 代表功能码
        //[8] - 失败的错误码
        //[9] - 有效数据起始索引号
        //**********************

        /// <summary>
        /// 接收到的字节数组中，数据长度的索引号[4]~[5]
        /// </summary>
        public const int PosIndexOfDataLengthInSocketReceivedBytes = CModbusFuncCode.PosIndexOfDataLengthInSocketReceivedBytes;//0x4;

        /// <summary>
        /// 接收到的字节数组中，从站地址的索引号[6]
        /// </summary>
        public const int PosIndexOfSlaveAddressInSocketReceivedBytes = CModbusFuncCode.PosIndexOfSlaveAddressInSocketReceivedBytes;//0x6;

        /// <summary>
        /// 接收到的字节数组中，功能码的索引号[7]
        /// </summary>
        public const int PosIndexOfFuncCodeInSocketReceivedBytes = CModbusFuncCode.PosIndexOfFuncCodeInSocketReceivedBytes;//0x7;

        /// <summary>
        /// 接收到的字节数组中，错误码的索引号[7]
        /// </summary>
        public const int PosIndexOfErrorCodeInSocketReceivedBytes = CModbusFuncCode.PosIndexOfFuncCodeInSocketReceivedBytes;//0x7;

        /// <summary>
        /// 执行读取操作时，接收到的字节数组中，接收到的数据开始的索引号[9]
        /// </summary>
        public const int PosIndexOfDataInSocketReceivedBytes = CModbusFuncCode.PosIndexOfDataInSocketReceivedBytes;//0x9;

        #endregion

        #region "读 - ok-ok"

        // 起始地址：0x0000~0xFFFF -- 0~65535
        // 读取数量：0x001~0x7D0 -- 1~2000

        #region "读单个/多个线圈 - ok-ok"

        #region "通讯记录"

        //成功执行写命令:False
        //写操作记录：(127.0.0.1:502)发送字节转换为16进制 - 00 05 00 00 00 06 01 05 00 00 00 00 
        //写操作记录：(127.0.0.1:502)收到字节转换为16进制 - 00 05 00 00 00 06 01 05 00 00 00 00 

        //成功执行读命令，结果值：False
        //(127.0.0.1:502)发送字节转换为16进制 - 00 04 00 00 00 06 01 01 00 00 00 01 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 04 00 00 00 04 01 01 01 00 

        //成功执行写命令:True
        //写操作记录：(127.0.0.1:502)发送字节转换为16进制 - 00 05 00 00 00 06 01 05 00 00 FF 00 
        //写操作记录：(127.0.0.1:502)收到字节转换为16进制 - 00 05 00 00 00 06 01 05 00 00 FF 00 

        //成功执行读命令，结果值：True
        //(127.0.0.1:502)发送字节转换为16进制 - 00 05 00 00 00 06 01 01 00 00 00 01 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 05 00 00 00 04 01 01 01 01 

        //成功执行写命令208
        //写操作记录：(127.0.0.1:502)发送字节转换为16进制 - 00 06 00 00 00 08 01 0F 00 00 00 08 01 D0 
        //写操作记录：(127.0.0.1:502)收到字节转换为16进制 - 00 06 00 00 00 06 01 0F 00 00 00 08 

        //成功执行读命令，结果值：208
        //(127.0.0.1:502)发送字节转换为16进制 - 00 06 00 00 00 06 01 01 00 00 00 08 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 06 00 00 00 04 01 01 01 D0 

        //成功执行写命令17131
        //写操作记录：(127.0.0.1:502)发送字节转换为16进制 - 00 07 00 00 00 09 01 0F 00 00 00 10 02 EB 42 
        //写操作记录：(127.0.0.1:502)收到字节转换为16进制 - 00 07 00 00 00 06 01 0F 00 00 00 10 

        //成功执行读命令，结果值：17131
        //(127.0.0.1:502)发送字节转换为16进制 - 00 07 00 00 00 06 01 01 00 00 00 10 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 07 00 00 00 05 01 01 02 EB 42 

        //成功执行写命令1234793950
        //写操作记录：(127.0.0.1:502)发送字节转换为16进制 - 00 08 00 00 00 0B 01 0F 00 00 00 20 04 DE 75 99 49 
        //写操作记录：(127.0.0.1:502)收到字节转换为16进制 - 00 08 00 00 00 06 01 0F 00 00 00 20 

        //成功执行读命令，结果值：1234793950
        //(127.0.0.1:502)发送字节转换为16进制 - 00 08 00 00 00 06 01 01 00 00 00 20 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 08 00 00 00 07 01 01 04 DE 75 99 49 

        //成功执行写命令-145218291
        //写操作记录：(127.0.0.1:502)发送字节转换为16进制 - 00 09 00 00 00 0F 01 0F 00 00 00 40 08 0D 25 58 F7 FF FF FF FF 
        //写操作记录：(127.0.0.1:502)收到字节转换为16进制 - 00 09 00 00 00 06 01 0F 00 00 00 40 

        //成功执行读命令，结果值：-145218291
        //(127.0.0.1:502)发送字节转换为16进制 - 00 09 00 00 00 06 01 01 00 00 00 40 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 09 00 00 00 0B 01 01 08 0D 25 58 F7 FF FF FF FF 

        //发送值[0~15]：False False False False False True True False False True False True True False False False 
        //成功执行写命令
        //写操作记录：(127.0.0.1:502)发送字节转换为16进制 - 00 0A 00 00 00 09 01 0F 00 00 00 10 02 60 1A 
        //写操作记录：(127.0.0.1:502)收到字节转换为16进制 - 00 0A 00 00 00 06 01 0F 00 00 00 10 

        //成功执行读命令，结果值：False False False False False True True False False True False True True False False False 
        //(127.0.0.1:502)发送字节转换为16进制 - 00 0A 00 00 00 06 01 01 00 00 00 10 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 0A 00 00 00 05 01 01 02 60 1A 

        //发送值[0~1]：55 178 
        //成功执行写命令
        //写操作记录：(127.0.0.1:502)发送字节转换为16进制 - 00 0B 00 00 00 09 01 0F 00 00 00 10 02 37 B2 
        //写操作记录：(127.0.0.1:502)收到字节转换为16进制 - 00 0B 00 00 00 06 01 0F 00 00 00 10 

        //成功执行读命令，结果值：55 178 
        //(127.0.0.1:502)发送字节转换为16进制 - 00 0B 00 00 00 06 01 01 00 00 00 10 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 0B 00 00 00 05 01 01 02 37 B2 

        //发送值[0~1]：-11432 -21040 
        //成功执行写命令
        //写操作记录：(127.0.0.1:502)发送字节转换为16进制 - 00 0C 00 00 00 0B 01 0F 00 00 00 20 04 58 D3 D0 AD 
        //写操作记录：(127.0.0.1:502)收到字节转换为16进制 - 00 0C 00 00 00 06 01 0F 00 00 00 20 

        //成功执行读命令，结果值：-11432 -21040 
        //(127.0.0.1:502)发送字节转换为16进制 - 00 0C 00 00 00 06 01 01 00 00 00 20 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 0C 00 00 00 07 01 01 04 58 D3 D0 AD 

        //发送值[0~1]：-1540227799 -1012004631 
        //成功执行写命令
        //写操作记录：(127.0.0.1:502)发送字节转换为16进制 - 00 0D 00 00 00 0F 01 0F 00 00 00 40 08 29 FD 31 A4 E9 08 AE C3 
        //写操作记录：(127.0.0.1:502)收到字节转换为16进制 - 00 0D 00 00 00 06 01 0F 00 00 00 40 

        //成功执行读命令，结果值：-1540227799 -1012004631 
        //(127.0.0.1:502)发送字节转换为16进制 - 00 0D 00 00 00 06 01 01 00 00 00 40 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 0D 00 00 00 0B 01 01 08 29 FD 31 A4 E9 08 AE C3 

        //发送值[0~1]：-895895293 1902323140 
        //成功执行写命令
        //写操作记录：(127.0.0.1:502)发送字节转换为16进制 - 00 0E 00 00 00 17 01 0F 00 00 00 80 10 03 B9 99 CA FF FF FF FF C4 25 63 71 00 00 00 00 
        //写操作记录：(127.0.0.1:502)收到字节转换为16进制 - 00 0E 00 00 00 06 01 0F 00 00 00 80 

        //成功执行读命令，结果值：-895895293 1902323140 
        //(127.0.0.1:502)发送字节转换为16进制 - 00 0E 00 00 00 06 01 01 00 00 00 80 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 0E 00 00 00 13 01 01 10 03 B9 99 CA FF FF FF FF C4 25 63 71 00 00 00 00 

        #endregion

        // ok-ok
        /// <summary>
        /// 读取从站单个线圈状态(位 - bit)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回线圈的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilBit(byte DeviceAddress, ushort BeginAddress, ref bool Value)//ushort ReadDataLength 读取数据长度,   , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, 1);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"

                    //成功执行读命令，结果值：False
                    //(127.0.0.1:502)发送字节转换为16进制 - 00 01 00 00 00 06 01 01 00 00 00 01 
                    //(127.0.0.1:502)收到字节转换为16进制 - 00 01 00 00 00 04 01 01 01 00 

                    //成功执行读命令，结果值：True
                    //(127.0.0.1:502)发送字节转换为16进制 - 00 02 00 00 00 06 01 01 00 00 00 01 
                    //(127.0.0.1:502)收到字节转换为16进制 - 00 02 00 00 00 04 01 01 01 01 

                    #endregion

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站多个字节的线圈状态(1个字节 = 8位 - bit)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字节</param>
        /// <param name="Value">返回线圈的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilBit(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref bool[] Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                //if (ReadDataLength < 1 || ReadDataLength > 250)//读取数据长度，有效值范围：1~250(字节)
                //{
                //    throw new Exception("读取数据长度的值不正确，有效值范围：1~250(字节)");
                //}

                if (BeginAddress + ReadDataLength * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, ReadDataLength * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();
                
                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion
                
                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"

                    //成功执行读命令，结果值：True True False False False False False False False False False False False False False False 
                    //(127.0.0.1:502)发送字节转换为16进制 - 00 05 00 00 00 06 01 01 00 00 00 10 
                    //(127.0.0.1:502)收到字节转换为16进制 - 00 05 00 00 00 05 01 01 02 03 00 
                    //成功执行读命令，结果值：True True False False False False False False False False False False False False True True 
                    //(127.0.0.1:502)发送字节转换为16进制 - 00 06 00 00 00 06 01 01 00 00 00 10 
                    //(127.0.0.1:502)收到字节转换为16进制 - 00 06 00 00 00 05 01 01 02 03 C0 

                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //bool[] bResultData = new bool[ReadDataLength * 8];

                        //for (int i = 0; i < ReadDataLength; i++)
                        //{
                        //    bool[] bTempData = CConverter.ByteToBitArray(byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i]);
                        //    for (int j = 0; j < bTempData.Length; j++)
                        //    {
                        //        bResultData[i * 8 + j] = bTempData[j];
                        //    }

                        //    bTempData = null;
                        //}

                        //Value = bResultData;
                        //bResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站1个字节的线圈状态(1个字节 = 8位 - bit)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回线圈的1个字节当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilByte(byte DeviceAddress, ushort BeginAddress, ref byte Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 1 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, 1 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();
                
                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion
                
                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站多个字节的线圈状态(1个字节 = 8位 - bit)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字节</param>
        /// <param name="Value">返回线圈的字节当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilByte(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref byte[] Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 250)//读取数据长度，有效值范围：1~250(字节)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~250(字节)");
                }

                if (BeginAddress + ReadDataLength * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, ReadDataLength * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion
                
                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //byte[] byResultData = new byte[ReadDataLength];
                        //for (int i = 0; i < ReadDataLength; i++)
                        //{
                        //    byResultData[i] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i];
                        //}

                        //Value = byResultData;
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站1个字节的线圈状态(1个字节 = 8位 - bit)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回线圈的1个字节当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilByte(byte DeviceAddress, ushort BeginAddress, ref sbyte Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 1 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, 1 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();
                
                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion
                
                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站多个字节的线圈状态(1个字节 = 8位 - bit)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字节</param>
        /// <param name="Value">返回线圈的字节当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilByte(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref sbyte[] Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 250)//读取数据长度，有效值范围：1~250(字节)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~250(字节)");
                }

                if (BeginAddress + ReadDataLength * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, ReadDataLength * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion
                
                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站1个字的线圈状态(位 - bit)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回线圈的1个字当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilWord(byte DeviceAddress, ushort BeginAddress, ref short Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, 2 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion
                
                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //byte[] byResultData = new byte[2];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes];
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];

                        //Value = BitConverter.ToInt16(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站多个字的线圈状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字</param>
        /// <param name="Value">返回线圈多个字的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilWord(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref short[] Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 125)//读取数据长度，有效值范围：1~125(字)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                }

                if (BeginAddress + ReadDataLength * 2 * 8 > 65535)//1个字 = 2 个字节 = 16位
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, ReadDataLength * 2 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //Value = new short[ReadDataLength];

                        //for (int i = 0; i < ReadDataLength; i++)
                        //{
                        //    byte[] byResultData = new byte[2];
                        //    byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 2 + 0];
                        //    byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 2 + 1];

                        //    Value[i] = BitConverter.ToInt16(byResultData, 0);
                        //    byResultData = null;
                        //}
                        
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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站1个字的线圈状态(位 - bit)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回线圈的1个字当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilWord(byte DeviceAddress, ushort BeginAddress, ref ushort Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, 2 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion
                
                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //byte[] byResultData = new byte[2];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes];
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];

                        //Value = BitConverter.ToUInt16(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站多个字的线圈状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字</param>
        /// <param name="Value">返回线圈多个字的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilWord(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref ushort[] Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 125)//读取数据长度，有效值范围：1~125(字)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                }

                if (BeginAddress + ReadDataLength * 2 * 8 > 65535)//1个字 = 2 个字节 = 16位
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, ReadDataLength * 2 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //Value = new ushort[ReadDataLength];

                        //for (int i = 0; i < ReadDataLength; i++)
                        //{
                        //    byte[] byResultData = new byte[2];
                        //    byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 2 + 0];
                        //    byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 2 + 1];

                        //    Value[i] = BitConverter.ToUInt16(byResultData, 0);
                        //    byResultData = null;
                        //}

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站2个字的线圈状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回线圈的2个字当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilWord(byte DeviceAddress, ushort BeginAddress, ref int Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, 4 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion
                
                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"


                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //byte[] byResultData = new byte[4];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];
                        //byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 2];
                        //byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 3];

                        //Value = BitConverter.ToInt32(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站多个双字的线圈状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：双字</param>
        /// <param name="Value">返回线圈多个双字的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilWord(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref int[] Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 125)//读取数据长度，有效值范围：1~125(字)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                }

                if (BeginAddress + ReadDataLength * 4 * 8 > 65535)//1个字 = 2 个字节 = 16位
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, ReadDataLength * 4 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //int[] iResultData = new int[ReadDataLength];

                        //for (int i = 0; i < ReadDataLength; i++)
                        //{
                        //    byte[] byResultData = new byte[4];
                        //    byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 4 + 0];
                        //    byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 4 + 1];
                        //    byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 4 + 2];
                        //    byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 4 + 3];

                        //    iResultData[i] = BitConverter.ToInt32(byResultData, 0);
                        //    byResultData = null;
                        //}

                        //Value = iResultData;
                        //iResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站2个字的线圈状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回线圈的2个字当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilWord(byte DeviceAddress, ushort BeginAddress, ref uint Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, 4 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion
                
                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"


                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //byte[] byResultData = new byte[4];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];
                        //byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 2];
                        //byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 3];

                        //Value = BitConverter.ToUInt32(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站多个双字的线圈状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：双字</param>
        /// <param name="Value">返回线圈多个双字的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilWord(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref uint[] Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 125)//读取数据长度，有效值范围：1~125(字)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                }

                if (BeginAddress + ReadDataLength * 4 * 8 > 65535)//1个字 = 2 个字节 = 16位
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, ReadDataLength * 4 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //uint[] iResultData = new uint[ReadDataLength];

                        //for (int i = 0; i < ReadDataLength; i++)
                        //{
                        //    byte[] byResultData = new byte[4];
                        //    byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 4 + 0];
                        //    byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 4 + 1];
                        //    byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 4 + 2];
                        //    byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 4 + 3];

                        //    iResultData[i] = BitConverter.ToUInt32(byResultData, 0);
                        //    byResultData = null;
                        //}

                        //Value = iResultData;
                        //iResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        #region "Hide"

#if TEST

        // -
        /// <summary>
        /// 读取从站2个字的线圈状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回线圈的2个字当前值</param>
        /// <returns>是否成功执行命令</returns>
        private bool ReadCoilWord(byte DeviceAddress, ushort BeginAddress, ref float Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, 4 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

        #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion
                
        #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
        #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //byte[] byResultData = new byte[4];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];
                        //byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 2];
                        //byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 3];

                        //Value = BitConverter.ToSingle(byResultData, 0);
                        //byResultData = null;

                        Value = CConverter.ToFloat(CConverter.CopyBytes(byReadData, (uint)(1 * (byte)ByteCount.Float), (uint)PosIndexOfDataInSocketReceivedBytes), ReadCoilBytesFormat, 0);

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // -
        /// <summary>
        /// 读取从站多个双字的线圈状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：双字</param>
        /// <param name="Value">返回线圈多个双字的当前值</param>
        /// <returns>是否成功执行命令</returns>
        private bool ReadCoilWord(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref float[] Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 125)//读取数据长度，有效值范围：1~125(字)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                }

                if (BeginAddress + ReadDataLength * 4 * 8 > 65535)//1个字 = 2 个字节 = 16位
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, ReadDataLength * 4 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

        #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

        #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
        #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //float[] fResultData = new float[ReadDataLength];

                        //for (int i = 0; i < ReadDataLength; i++)
                        //{
                        //    byte[] byResultData = new byte[4];
                        //    byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 4 + 0];
                        //    byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 4 + 1];
                        //    byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 4 + 2];
                        //    byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 4 + 3];

                        //    fResultData[i] = BitConverter.ToInt16(byResultData, 0);
                        //    byResultData = null;
                        //}

                        //Value = fResultData;
                        //fResultData = null;

                        Value = CConverter.ToFloatArray(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (byte)ByteCount.Float), (uint)PosIndexOfDataInSocketReceivedBytes), ReadCoilBytesFormat, 0);
                        
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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // -
        /// <summary>
        /// 读取从站2个双字的线圈状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回线圈的2个双字当前值</param>
        /// <returns>是否成功执行命令</returns>
        private bool ReadCoilWord(byte DeviceAddress, ushort BeginAddress, ref double Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, 8 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

        #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion
                
        #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
        #region "通讯记录"




                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //byte[] byResultData = new byte[8];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];
                        //byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 2];
                        //byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 3];
                        //byResultData[4] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 4];
                        //byResultData[5] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 5];
                        //byResultData[6] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 6];
                        //byResultData[7] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 7];

                        //Value = BitConverter.ToDouble(byResultData, 0);
                        //byResultData = null;

                        Value = CConverter.ToDouble(CConverter.CopyBytes(byReadData, (uint)(1 * (byte)ByteCount.Double), (uint)PosIndexOfDataInSocketReceivedBytes), ReadCoilBytesFormat, 0);

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // -
        /// <summary>
        /// 读取从站多个双字的线圈状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：双字</param>
        /// <param name="Value">返回线圈多个双字的当前值</param>
        /// <returns>是否成功执行命令</returns>
        private bool ReadCoilWord(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref double[] Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 125)//读取数据长度，有效值范围：1~125(字)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                }

                if (BeginAddress + ReadDataLength * 8 * 8 > 65535)//1个字 = 2 个字节 = 16位
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, ReadDataLength * 8 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

        #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();
                
                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

        #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
        #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //double[] dResultData = new double[ReadDataLength];

                        //for (int i = 0; i < ReadDataLength; i++)
                        //{
                        //    byte[] byResultData = new byte[8];
                        //    byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 0];
                        //    byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 1];
                        //    byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 2];
                        //    byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 3];
                        //    byResultData[4] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 4];
                        //    byResultData[5] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 5];
                        //    byResultData[6] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 6];
                        //    byResultData[7] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 7];

                        //    dResultData[i] = BitConverter.ToDouble(byResultData, 0);
                        //    byResultData = null;
                        //}

                        //Value = dResultData;
                        //dResultData = null;

                        Value = CConverter.ToDoubleArray(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (byte)ByteCount.Double), (uint)PosIndexOfDataInSocketReceivedBytes), ReadCoilBytesFormat, 0);
                        
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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

#endif

        #endregion

        // ok-ok
        /// <summary>
        /// 读取从站2个双字的线圈状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回线圈的2个双字当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilWord(byte DeviceAddress, ushort BeginAddress, ref long Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, 8 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();
                
                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion
                
                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"




                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //byte[] byResultData = new byte[8];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];
                        //byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 2];
                        //byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 3];
                        //byResultData[4] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 4];
                        //byResultData[5] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 5];
                        //byResultData[6] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 6];
                        //byResultData[7] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 7];

                        //Value = BitConverter.ToInt64(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站多个双字的线圈状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：双字</param>
        /// <param name="Value">返回线圈多个双字的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilWord(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref long[] Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 125)//读取数据长度，有效值范围：1~125(字)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                }

                if (BeginAddress + ReadDataLength * 8 * 8 > 65535)//1个字 = 2 个字节 = 16位
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, ReadDataLength * 8 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //long[] lResultData = new long[ReadDataLength];

                        //for (int i = 0; i < ReadDataLength; i++)
                        //{
                        //    byte[] byResultData = new byte[8];
                        //    byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 0];
                        //    byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 1];
                        //    byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 2];
                        //    byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 3];
                        //    byResultData[4] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 4];
                        //    byResultData[5] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 5];
                        //    byResultData[6] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 6];
                        //    byResultData[7] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 7];

                        //    lResultData[i] = BitConverter.ToInt64(byResultData, 0);
                        //    byResultData = null;
                        //}

                        //Value = lResultData;
                        //lResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站2个双字的线圈状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回线圈的2个双字当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilWord(byte DeviceAddress, ushort BeginAddress, ref ulong Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, 8 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();
                
                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion
                
                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"




                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //byte[] byResultData = new byte[8];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];
                        //byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 2];
                        //byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 3];
                        //byResultData[4] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 4];
                        //byResultData[5] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 5];
                        //byResultData[6] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 6];
                        //byResultData[7] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 7];

                        //Value = BitConverter.ToUInt64(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站多个双字的线圈状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：双字</param>
        /// <param name="Value">返回线圈多个双字的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilWord(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref ulong[] Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 125)//读取数据长度，有效值范围：1~125(字)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                }

                if (BeginAddress + ReadDataLength * 8 * 8 > 65535)//1个字 = 2 个字节 = 16位
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, ReadDataLength * 8 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //ulong[] lResultData = new ulong[ReadDataLength];

                        //for (int i = 0; i < ReadDataLength; i++)
                        //{
                        //    byte[] byResultData = new byte[8];
                        //    byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 0];
                        //    byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 1];
                        //    byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 2];
                        //    byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 3];
                        //    byResultData[4] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 4];
                        //    byResultData[5] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 5];
                        //    byResultData[6] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 6];
                        //    byResultData[7] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 7];

                        //    lResultData[i] = BitConverter.ToUInt64(byResultData, 0);
                        //    byResultData = null;
                        //}

                        //Value = lResultData;
                        //lResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        #endregion

        #region "读单个/多个输入位的状态 - ok-ok"

        #region "通讯记录"

        //成功执行读命令，结果值：False
        //(127.0.0.1:502)发送字节转换为16进制 - 00 01 00 00 00 06 01 02 00 00 00 01 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 01 00 00 00 04 01 02 01 00 

        //成功执行读命令，结果值：True
        //(127.0.0.1:502)发送字节转换为16进制 - 00 02 00 00 00 06 01 02 00 00 00 01 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 02 00 00 00 04 01 02 01 01 

        //成功执行读命令，结果值：1
        //(127.0.0.1:502)发送字节转换为16进制 - 00 03 00 00 00 06 01 02 00 00 00 08 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 03 00 00 00 04 01 02 01 01 

        //成功执行读命令，结果值：129
        //(127.0.0.1:502)发送字节转换为16进制 - 00 04 00 00 00 06 01 02 00 00 00 08 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 04 00 00 00 04 01 02 01 81 

        //成功执行读命令，结果值：129
        //(127.0.0.1:502)发送字节转换为16进制 - 00 05 00 00 00 06 01 02 00 00 00 10 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 05 00 00 00 05 01 02 02 81 00 

        //成功执行读命令，结果值：-32639
        //(127.0.0.1:502)发送字节转换为16进制 - 00 06 00 00 00 06 01 02 00 00 00 10 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 06 00 00 00 05 01 02 02 81 80 

        //成功执行读命令，结果值：32897
        //(127.0.0.1:502)发送字节转换为16进制 - 00 07 00 00 00 06 01 02 00 00 00 20 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 07 00 00 00 07 01 02 04 81 80 00 00 

        //成功执行读命令，结果值：-2147450751
        //(127.0.0.1:502)发送字节转换为16进制 - 00 08 00 00 00 06 01 02 00 00 00 20 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 08 00 00 00 07 01 02 04 81 80 00 80 

        //成功执行读命令，结果值：2147516545
        //(127.0.0.1:502)发送字节转换为16进制 - 00 09 00 00 00 06 01 02 00 00 00 40 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 09 00 00 00 0B 01 02 08 81 80 00 80 00 00 00 00 

        //成功执行读命令，结果值：2305843011361210497
        //(127.0.0.1:502)发送字节转换为16进制 - 00 0A 00 00 00 06 01 02 00 00 00 40 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 0A 00 00 00 0B 01 02 08 81 80 00 80 00 00 00 20 

        //成功执行读命令，结果值：True False False False False False False True False False False False False False False True 
        //(127.0.0.1:502)发送字节转换为16进制 - 00 0B 00 00 00 06 01 02 00 00 00 10 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 0B 00 00 00 05 01 02 02 81 80 

        //成功执行读命令，结果值：129 128 
        //(127.0.0.1:502)发送字节转换为16进制 - 00 0C 00 00 00 06 01 02 00 00 00 10 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 0C 00 00 00 05 01 02 02 81 80 

        //成功执行读命令，结果值：-32639 -32768 
        //(127.0.0.1:502)发送字节转换为16进制 - 00 0D 00 00 00 06 01 02 00 00 00 20 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 0D 00 00 00 07 01 02 04 81 80 00 80 

        //成功执行读命令，结果值：-2147450751 536870912 
        //(127.0.0.1:502)发送字节转换为16进制 - 00 0E 00 00 00 06 01 02 00 00 00 40 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 0E 00 00 00 0B 01 02 08 81 80 00 80 00 00 00 20 

        //成功执行读命令，结果值：2305843011361210497 0 
        //(127.0.0.1:502)发送字节转换为16进制 - 00 0F 00 00 00 06 01 02 00 00 00 80 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 0F 00 00 00 13 01 02 10 81 80 00 80 00 00 00 20 00 00 00 00 00 00 00 00 

        //成功执行读命令，结果值：2305843011361210497 -9223372036854775808 
        //(127.0.0.1:502)发送字节转换为16进制 - 00 10 00 00 00 06 01 02 00 00 00 80 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 10 00 00 00 13 01 02 10 81 80 00 80 00 00 00 20 00 00 00 00 00 00 00 80 

        #endregion

        // ok-ok
        /// <summary>
        /// 读取从站单个输入位的状态(位 - bit)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回单个输入位的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputBit(byte DeviceAddress, ushort BeginAddress, ref bool Value)//ushort ReadDataLength 读取数据长度,   , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, 1);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        // 单个位的值
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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站多个字节的输入状态(1个字节 = 8位 - bit)，函数是以字节为读取数据长度单位，返回的值是以字为单位(2个字节的倍数)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字节</param>
        /// <param name="Value">返回多个字节输入状态的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputBit(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref bool[] Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 125)//读取数据长度，有效值范围：1~250(字节)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~250(字节)");
                }

                if (BeginAddress + ReadDataLength * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, ReadDataLength * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                //sFeedBackFromSlave = null;

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //bool[] bResultData = new bool[ReadDataLength * 8];

                        //for (int i = 0; i < ReadDataLength; i++)
                        //{
                        //    bool[] bTempData = CConverter.ByteToBitArray(byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i]);
                        //    for (int j = 0; j < bTempData.Length; j++)
                        //    {
                        //        bResultData[i * 8 + j] = bTempData[j];
                        //    }

                        //    bTempData = null;
                        //}

                        //Value = bResultData;
                        //bResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站1个字节的输入状态(位 - bit)，函数是以字节为读取数据长度单位，返回的值是以字为单位(2个字节的倍数)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回输入状态1个字节的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputByte(byte DeviceAddress, ushort BeginAddress, ref byte Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 1 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, 1 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站多个字节的输入状态(位 - bit)，函数是以字节为读取数据长度单位，返回的值是以字为单位(2个字节的倍数)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字节</param>
        /// <param name="Value">返回输入状态多个字节的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputByte(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref byte[] Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 125)//读取数据长度，有效值范围：1~250(字节)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~250(字节)");
                }

                if (BeginAddress + ReadDataLength * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, ReadDataLength * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();
                
                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //byte[] byResultData = new byte[ReadDataLength];
                        //for (int i = 0; i < ReadDataLength; i++)
                        //{
                        //    byResultData[i] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i];
                        //}

                        //Value = byResultData;
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站1个字节的输入状态(位 - bit)，函数是以字节为读取数据长度单位，返回的值是以字为单位(2个字节的倍数)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回输入状态1个字节的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputByte(byte DeviceAddress, ushort BeginAddress, ref sbyte Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 1 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, 1 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站多个字节的输入状态(位 - bit)，函数是以字节为读取数据长度单位，返回的值是以字为单位(2个字节的倍数)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字节</param>
        /// <param name="Value">返回输入状态多个字节的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputByte(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref sbyte[] Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 125)//读取数据长度，有效值范围：1~250(字节)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~250(字节)");
                }

                if (BeginAddress + ReadDataLength * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, ReadDataLength * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();
                
                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站1个字的输入状态，函数是以字为读取数据长度单位，返回的值是以字为单位(2个字节的倍数)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回输入状态1个字的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputWord(byte DeviceAddress, ushort BeginAddress, ref short Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 1 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, 2 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //byte[] byResultData = new byte[2];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];

                        //Value = BitConverter.ToInt16(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站多个字的输入状态，函数是以字节为读取数据长度单位，返回的值是以字为单位(2个字节的倍数)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字</param>
        /// <param name="Value">返回输入状态多个字的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputWord(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref short[] Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
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

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, ReadDataLength * 2 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //Value = new short[ReadDataLength];

                        //for (int i = 0; i < ReadDataLength; i++)
                        //{
                        //    byte[] byResultData = new byte[2];
                        //    byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 2 + 0];
                        //    byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 2 + 1];

                        //    Value[i] = BitConverter.ToInt16(byResultData, 0);
                        //    byResultData = null;
                        //}

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站1个字的输入状态，函数是以字为读取数据长度单位，返回的值是以字为单位(2个字节的倍数)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回输入状态1个字的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputWord(byte DeviceAddress, ushort BeginAddress, ref ushort Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 1 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, 2 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //byte[] byResultData = new byte[2];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];

                        //Value = BitConverter.ToUInt16(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站多个字的输入状态，函数是以字节为读取数据长度单位，返回的值是以字为单位(2个字节的倍数)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字</param>
        /// <param name="Value">返回输入状态多个字的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputWord(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref ushort[] Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
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

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, ReadDataLength * 2 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //Value = new ushort[ReadDataLength];

                        //for (int i = 0; i < ReadDataLength; i++)
                        //{
                        //    byte[] byResultData = new byte[2];
                        //    byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 2 + 0];
                        //    byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 2 + 1];

                        //    Value[i] = BitConverter.ToUInt16(byResultData, 0);
                        //    byResultData = null;
                        //}

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站1个双字的输入状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回输入状态1个双字的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputWord(byte DeviceAddress, ushort BeginAddress, ref int Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, 4 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //byte[] byResultData = new byte[4];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];
                        //byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 2];
                        //byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 3];

                        //Value = BitConverter.ToInt32(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站多个双字的输入状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：双字</param>
        /// <param name="Value">返回输入状态多个双字的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputWord(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref int[] Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 125)//读取数据长度，有效值范围：1~125(字)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                }

                if (BeginAddress + ReadDataLength * 4 * 8 > 65535)//1个字 = 2 个字节 = 16位
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, ReadDataLength * 4 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //int[] iResultData = new int[ReadDataLength];

                        //for (int i = 0; i < ReadDataLength; i++)
                        //{
                        //    byte[] byResultData = new byte[4];
                        //    byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 4 + 0];
                        //    byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 4 + 1];
                        //    byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 4 + 2];
                        //    byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 4 + 3];

                        //    iResultData[i] = BitConverter.ToInt32(byResultData, 0);
                        //    byResultData = null;
                        //}

                        //Value = iResultData;
                        //iResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站1个双字的输入状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回输入状态1个双字的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputWord(byte DeviceAddress, ushort BeginAddress, ref uint Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, 4 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //byte[] byResultData = new byte[4];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];
                        //byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 2];
                        //byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 3];

                        //Value = BitConverter.ToUInt32(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站多个双字的输入状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：双字</param>
        /// <param name="Value">返回输入状态多个双字的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputWord(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref uint[] Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 125)//读取数据长度，有效值范围：1~125(字)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                }

                if (BeginAddress + ReadDataLength * 4 * 8 > 65535)//1个字 = 2 个字节 = 16位
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, ReadDataLength * 4 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //uint[] iResultData = new uint[ReadDataLength];

                        //for (int i = 0; i < ReadDataLength; i++)
                        //{
                        //    byte[] byResultData = new byte[4];
                        //    byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 4 + 0];
                        //    byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 4 + 1];
                        //    byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 4 + 2];
                        //    byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 4 + 3];

                        //    iResultData[i] = BitConverter.ToUInt32(byResultData, 0);
                        //    byResultData = null;
                        //}

                        //Value = iResultData;
                        //iResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站2个双字的输入状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回2个双字输入状态的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputWord(byte DeviceAddress, ushort BeginAddress, ref long Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, 8 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //byte[] byResultData = new byte[8];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];
                        //byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 2];
                        //byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 3];
                        //byResultData[4] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 4];
                        //byResultData[5] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 5];
                        //byResultData[6] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 6];
                        //byResultData[7] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 7];

                        //Value = BitConverter.ToInt64(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站多个双字的输入状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：双字</param>
        /// <param name="Value">返回输入状态多个双字的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputWord(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref long[] Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 125)//读取数据长度，有效值范围：1~125(字)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                }

                if (BeginAddress + ReadDataLength * 8 * 8 > 65535)//1个字 = 2 个字节 = 16位
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, ReadDataLength * 8 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //long[] lResultData = new long[ReadDataLength];

                        //for (int i = 0; i < ReadDataLength; i++)
                        //{
                        //    byte[] byResultData = new byte[8];
                        //    byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 0];
                        //    byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 1];
                        //    byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 2];
                        //    byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 3];
                        //    byResultData[4] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 4];
                        //    byResultData[5] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 5];
                        //    byResultData[6] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 6];
                        //    byResultData[7] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 7];

                        //    lResultData[i] = BitConverter.ToInt64(byResultData, 0);
                        //    byResultData = null;
                        //}

                        //Value = lResultData;
                        //lResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站2个双字的输入状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回2个双字输入状态的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputWord(byte DeviceAddress, ushort BeginAddress, ref ulong Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, 8 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //byte[] byResultData = new byte[8];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];
                        //byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 2];
                        //byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 3];
                        //byResultData[4] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 4];
                        //byResultData[5] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 5];
                        //byResultData[6] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 6];
                        //byResultData[7] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 7];

                        //Value = BitConverter.ToUInt64(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站多个双字的输入状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：双字</param>
        /// <param name="Value">返回输入状态多个双字的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputWord(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref ulong[] Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 125)//读取数据长度，有效值范围：1~125(字)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                }

                if (BeginAddress + ReadDataLength * 8 * 8 > 65535)//1个字 = 2 个字节 = 16位
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, ReadDataLength * 8 * 8);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //ulong[] lResultData = new ulong[ReadDataLength];

                        //for (int i = 0; i < ReadDataLength; i++)
                        //{
                        //    byte[] byResultData = new byte[8];
                        //    byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 0];
                        //    byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 1];
                        //    byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 2];
                        //    byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 3];
                        //    byResultData[4] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 4];
                        //    byResultData[5] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 5];
                        //    byResultData[6] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 6];
                        //    byResultData[7] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 7];

                        //    lResultData[i] = BitConverter.ToUInt64(byResultData, 0);
                        //    byResultData = null;
                        //}

                        //Value = lResultData;
                        //lResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        #endregion

        #region "读单个/多个输入寄存器的状态 - ok-ok"

        #region "通讯记录"

        //成功执行读命令，结果值：-32654
        //(127.0.0.1:502)发送字节转换为16进制 - 00 01 00 00 00 06 01 04 00 00 00 01 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 01 00 00 00 05 01 04 02 80 72 

        //成功执行读命令，结果值：-32654 3265 
        //(127.0.0.1:502)发送字节转换为16进制 - 00 02 00 00 00 06 01 04 00 00 00 02 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 02 00 00 00 07 01 04 04 80 72 0C C1 

        //成功执行读命令，结果值：65535
        //(127.0.0.1:502)发送字节转换为16进制 - 00 03 00 00 00 06 01 04 00 00 00 02 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 03 00 00 00 07 01 04 04 FF FF 00 00 

        //成功执行读命令，结果值：-65535
        //(127.0.0.1:502)发送字节转换为16进制 - 00 04 00 00 00 06 01 04 00 00 00 02 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 04 00 00 00 07 01 04 04 00 01 FF FF 

        //成功执行读命令，结果值：-65535 65535 
        //(127.0.0.1:502)发送字节转换为16进制 - 00 05 00 00 00 06 01 04 00 00 00 04 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 05 00 00 00 0B 01 04 08 00 01 FF FF FF FF 00 00 

        //成功执行读命令，结果值：-665.321
        //(127.0.0.1:502)发送字节转换为16进制 - 00 06 00 00 00 06 01 04 00 00 00 02 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 06 00 00 00 07 01 04 04 54 8B C4 26 

        //成功执行读命令，结果值：-665.321 -6548.32 
        //(127.0.0.1:502)发送字节转换为16进制 - 00 08 00 00 00 06 01 04 00 00 00 04 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 08 00 00 00 0B 01 04 08 54 8B C4 26 A2 8F C5 CC 

        //成功执行读命令，结果值：-665.321
        //(127.0.0.1:502)发送字节转换为16进制 - 00 09 00 00 00 06 01 04 00 00 00 02 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 09 00 00 00 07 01 04 04 54 8B C4 26 

        #endregion

        // ok-ok
        /// <summary>
        /// 读取从站输入寄存器的当前值(short: -32,768 到 32,767)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回输入寄存器的当前值(short: -32,768 到 32,767)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputRegister(byte DeviceAddress, ushort BeginAddress, ref short Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, 1);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //byte[] byResultData = new byte[2];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];//接收数据 - 高字节
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];//接收数据 - 低字节

                        //Value = BitConverter.ToInt16(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站输入寄存器的当前值(short: -32,768 到 32,767)，函数是以字节为读取数据长度单位
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度</param>
        /// <param name="Value">返回输入寄存器的当前值(short: -32,768 到 32,767)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputRegister(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref short[] Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 125)//读取数据长度，有效值范围：1~250(字节)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~250(字节)");
                }

                if (BeginAddress + ReadDataLength * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, ReadDataLength);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //short[] iResultData = new short[ReadDataLength];

                        //for (int i = 0; i < ReadDataLength; i++)
                        //{
                        //    byte[] byResultData = new byte[2];
                        //    byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 2 + 1];//接收数据 - 高字节
                        //    byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 2 + 0];//接收数据 - 低字节

                        //    iResultData[i] = BitConverter.ToInt16(byResultData, 0);
                        //    byResultData = null;
                        //}

                        //Value = iResultData;
                        //iResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站输入寄存器的当前值(ushort: 0 到 65535)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回输入寄存器的当前值(ushort: 0 到 65535)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputRegister(byte DeviceAddress, ushort BeginAddress, ref ushort Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, 1);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //byte[] byResultData = new byte[2];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];//接收数据 - 高字节
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];//接收数据 - 低字节

                        //Value = BitConverter.ToUInt16(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站输入寄存器的当前值(ushort: 0 到 65535)，函数是以字节为读取数据长度单位
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度</param>
        /// <param name="Value">返回输入寄存器的当前值(ushort: 0 到 65535)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputRegister(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref ushort[] Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 125)//读取数据长度，有效值范围：1~250(字节)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~250(字节)");
                }

                if (BeginAddress + ReadDataLength * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, ReadDataLength);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //ushort[] iResultData = new ushort[ReadDataLength];

                        //for (int i = 0; i < ReadDataLength; i++)
                        //{
                        //    byte[] byResultData = new byte[2];
                        //    byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 2 + 1];//接收数据 - 高字节
                        //    byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 2 + 0];//接收数据 - 低字节

                        //    iResultData[i] = BitConverter.ToUInt16(byResultData, 0);
                        //    byResultData = null;
                        //}

                        //Value = iResultData;
                        //iResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站输入寄存器的当前值(int)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回输入寄存器的当前值(int)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputRegister(byte DeviceAddress, ushort BeginAddress, ref int Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, 2);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //byte[] byResultData = new byte[4];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];
                        //byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 3];
                        //byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 2];

                        //Value = BitConverter.ToInt32(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站输入寄存器的当前值(int)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度</param>
        /// <param name="Value">返回输入寄存器的当前值(int)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputRegister(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref int[] Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
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

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, ReadDataLength * 2);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();
                
                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //int[] iResultData = new int[ReadDataLength];

                        //for (int i = 0; i < ReadDataLength; i++)
                        //{
                        //    byte[] byResultData = new byte[4];
                        //    byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 4 + 1];
                        //    byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 4 + 0];
                        //    byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 4 + 3];
                        //    byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 4 + 2];

                        //    iResultData[i] = BitConverter.ToInt32(byResultData, 0);
                        //    byResultData = null;
                        //}

                        //Value = iResultData;
                        //iResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站输入寄存器的当前值(uint)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回输入寄存器的当前值(uint)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputRegister(byte DeviceAddress, ushort BeginAddress, ref uint Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, 2);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //byte[] byResultData = new byte[4];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];
                        //byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 3];
                        //byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 2];

                        //Value = BitConverter.ToUInt32(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站输入寄存器的当前值(uint)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度</param>
        /// <param name="Value">返回输入寄存器的当前值(uint)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputRegister(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref uint[] Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
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

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, ReadDataLength * 2);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();
                
                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //uint[] iResultData = new uint[ReadDataLength];

                        //for (int i = 0; i < ReadDataLength; i++)
                        //{
                        //    byte[] byResultData = new byte[4];
                        //    byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 4 + 1];
                        //    byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 4 + 0];
                        //    byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 4 + 3];
                        //    byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 4 + 2];

                        //    iResultData[i] = BitConverter.ToUInt32(byResultData, 0);
                        //    byResultData = null;
                        //}

                        //Value = iResultData;
                        //iResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站输入寄存器的当前值(float)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回输入寄存器的当前值(float)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputRegister(byte DeviceAddress, ushort BeginAddress, ref float Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, 2);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //byte[] byResultData = new byte[4];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];
                        //byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 3];
                        //byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 2];

                        //Value = BitConverter.ToSingle(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站输入寄存器的当前值(float)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度</param>
        /// <param name="Value">返回输入寄存器的当前值(float)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputRegister(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref float[] Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, ReadDataLength * 2);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"




                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //float[] fResultData = new float[ReadDataLength];

                        //for (int i = 0; i < ReadDataLength; i++)
                        //{
                        //    byte[] byResultData = new byte[4];
                        //    byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 4 + 1];
                        //    byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 4 + 0];
                        //    byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 4 + 3];
                        //    byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 4 + 2];

                        //    fResultData[i] = BitConverter.ToSingle(byResultData, 0);

                        //    byResultData = null;
                        //}

                        //Value = fResultData;
                        //fResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        // ?? 测试读取数据时不能像其它功能码一样正确读取double值，在float范围内可以正确读取，其它会对应不上，待更多测试
        /// <summary>
        /// 【?? 测试读取数据时不能像其它功能码一样正确读取double值，在float范围内可以正确读取，其它会对应不上，待更多测试】
        /// 读取从站输入寄存器的当前值(double)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回输入寄存器的当前值(double)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputRegister(byte DeviceAddress, ushort BeginAddress, ref double Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, 4);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"




                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //byte[] byResultData = new byte[8];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];
                        //byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 3];
                        //byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 2];

                        //byResultData[4] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 5];
                        //byResultData[5] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 4];
                        //byResultData[6] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 7];
                        //byResultData[7] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 6];

                        //Value = BitConverter.ToDouble(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        // ?? 测试读取数据时不能像其它功能码一样正确读取double值，在float范围内可以正确读取，其它会对应不上，待更多测试
        /// <summary>
        /// 【?? 测试读取数据时不能像其它功能码一样正确读取double值，在float范围内可以正确读取，其它会对应不上，待更多测试】
        /// 读取从站输入寄存器的当前值(double)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度</param>
        /// <param name="Value">返回输入寄存器的当前值(double)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputRegister(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref double[] Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 4 * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, ReadDataLength * 4);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //double[] dResultData = new double[ReadDataLength];

                        //for (int i = 0; i < ReadDataLength; i++)
                        //{
                        //    byte[] byResultData = new byte[8];
                        //    byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 1];
                        //    byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 0];
                        //    byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 3];
                        //    byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 2];

                        //    byResultData[4] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 5];
                        //    byResultData[5] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 4];
                        //    byResultData[6] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 7];
                        //    byResultData[7] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 6];

                        //    dResultData[i] = BitConverter.ToDouble(byResultData, 0);

                        //    byResultData = null;
                        //}

                        //Value = dResultData;
                        //dResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        // ?? 测试读取数据时不能像其它功能码一样正确读取long值，在int范围内可以正确读取，其它会对应不上，待更多测试
        /// <summary>
        /// 【测试读取数据时不能像其它功能码一样正确读取long值，在int范围内可以正确读取，其它会对应不上，待更多测试】
        /// 读取从站2个双字的输入寄存器的当前值
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回2个双字输入寄存器的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputRegister(byte DeviceAddress, ushort BeginAddress, ref long Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, 4);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //byte[] byResultData = new byte[8];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];
                        //byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 3];
                        //byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 2];

                        //byResultData[4] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 5];
                        //byResultData[5] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 4];
                        //byResultData[6] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 7];
                        //byResultData[7] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 6];

                        //Value = BitConverter.ToInt64(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        // ?? 测试读取数据时不能像其它功能码一样正确读取long值，在int范围内可以正确读取，其它会对应不上，待更多测试
        /// <summary>
        /// 【测试读取数据时不能像其它功能码一样正确读取long值，在int范围内可以正确读取，其它会对应不上，待更多测试】
        /// 读取从站多个双字的输入寄存器的当前值
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：双字</param>
        /// <param name="Value">返回输入寄存器多个双字的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputRegister(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref long[] Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                //if (ReadDataLength < 1 || ReadDataLength > 125)//读取数据长度，有效值范围：1~125(字)
                //{
                //    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                //}

                if (BeginAddress + ReadDataLength * 8 * 8 > 65535)//1个字 = 2 个字节 = 16位
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, ReadDataLength * 4);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"




                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //long[] lResultData = new long[ReadDataLength];

                        //for (int i = 0; i < ReadDataLength; i++)
                        //{
                        //    byte[] byResultData = new byte[8];

                        //    byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 1];
                        //    byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 0];
                        //    byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 3];
                        //    byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 2];
                        //    byResultData[4] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 5];
                        //    byResultData[5] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 4];
                        //    byResultData[6] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 7];
                        //    byResultData[7] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 6];

                        //    lResultData[i] = BitConverter.ToInt64(byResultData, 0);
                        //    byResultData = null;
                        //}

                        //Value = lResultData;
                        //lResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        // ?? 测试读取数据时不能像其它功能码一样正确读取ulong值，在int范围内可以正确读取，其它会对应不上，待更多测试
        /// <summary>
        /// 【测试读取数据时不能像其它功能码一样正确读取ulong值，在int范围内可以正确读取，其它会对应不上，待更多测试】
        /// 读取从站2个双字的输入寄存器的当前值
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回2个双字输入寄存器的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputRegister(byte DeviceAddress, ushort BeginAddress, ref ulong Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, 4);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //byte[] byResultData = new byte[8];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];
                        //byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 3];
                        //byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 2];

                        //byResultData[4] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 5];
                        //byResultData[5] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 4];
                        //byResultData[6] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 7];
                        //byResultData[7] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 6];

                        //Value = BitConverter.ToUInt64(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        // ?? 测试读取数据时不能像其它功能码一样正确读取ulong值，在int范围内可以正确读取，其它会对应不上，待更多测试
        /// <summary>
        /// 【测试读取数据时不能像其它功能码一样正确读取ulong值，在int范围内可以正确读取，其它会对应不上，待更多测试】
        /// 读取从站多个双字的输入寄存器的当前值
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：双字</param>
        /// <param name="Value">返回输入寄存器多个双字的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputRegister(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref ulong[] Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                //if (ReadDataLength < 1 || ReadDataLength > 125)//读取数据长度，有效值范围：1~125(字)
                //{
                //    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                //}

                if (BeginAddress + ReadDataLength * 8 * 8 > 65535)//1个字 = 2 个字节 = 16位
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, ReadDataLength * 4);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"




                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //ulong[] lResultData = new ulong[ReadDataLength];

                        //for (int i = 0; i < ReadDataLength; i++)
                        //{
                        //    byte[] byResultData = new byte[8];

                        //    byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 1];
                        //    byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 0];
                        //    byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 3];
                        //    byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 2];
                        //    byResultData[4] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 5];
                        //    byResultData[5] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 4];
                        //    byResultData[6] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 7];
                        //    byResultData[7] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 6];

                        //    lResultData[i] = BitConverter.ToUInt64(byResultData, 0);
                        //    byResultData = null;
                        //}

                        //Value = lResultData;
                        //lResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        #endregion

        #region "读单个/多个保持寄存器的状态 - ok-ok"

        // ok-ok
        /// <summary>
        /// 读取从站保持寄存器的当前值(short: -32,768 到 32,767)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回保持寄存器的当前值(short: -32,768 到 32,767)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadKeepRegister(byte DeviceAddress, ushort BeginAddress, ref short Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, 1);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();
                
                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"




                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //byte[] byResultData = new byte[2];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];//接收数据 - 高字节
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];//接收数据 - 低字节

                        //Value = BitConverter.ToInt16(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站保持寄存器的当前值(short: -32,768 到 32,767)，函数是以字节为读取数据长度单位
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字节</param>
        /// <param name="Value">返回保持寄存器的当前值(short: -32,768 到 32,767)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadKeepRegister(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref short[] Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 125)//读取数据长度，有效值范围：1~250(字节)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~250(字节)");
                }

                if (BeginAddress + ReadDataLength * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, ReadDataLength);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //short[] iResultData = new short[ReadDataLength];

                        //for (int i = 0; i < ReadDataLength; i++)
                        //{
                        //    byte[] byResultData = new byte[2];
                        //    byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 2 + 1];//接收数据 - 高字节
                        //    byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 2 + 0];//接收数据 - 低字节

                        //    iResultData[i] = BitConverter.ToInt16(byResultData, 0);
                        //    byResultData = null;
                        //}

                        //Value = iResultData;
                        //iResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站保持寄存器的当前值(ushort: 0 到 65535)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回保持寄存器的当前值(ushort: 0 到 65535)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadKeepRegister(byte DeviceAddress, ushort BeginAddress, ref ushort Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, 1);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();
                
                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"




                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //byte[] byResultData = new byte[2];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];//接收数据 - 高字节
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];//接收数据 - 低字节

                        //Value = BitConverter.ToUInt16(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站保持寄存器的当前值(ushort: 0 到 65535)，函数是以字节为读取数据长度单位
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字节</param>
        /// <param name="Value">返回保持寄存器的当前值(ushort: 0 到 65535)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadKeepRegister(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref ushort[] Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (ReadDataLength < 1 || ReadDataLength > 125)//读取数据长度，有效值范围：1~250(字节)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~250(字节)");
                }

                if (BeginAddress + ReadDataLength * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, ReadDataLength);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //ushort[] iResultData = new ushort[ReadDataLength];

                        //for (int i = 0; i < ReadDataLength; i++)
                        //{
                        //    byte[] byResultData = new byte[2];
                        //    byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 2 + 1];//接收数据 - 高字节
                        //    byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 2 + 0];//接收数据 - 低字节

                        //    iResultData[i] = BitConverter.ToUInt16(byResultData, 0);
                        //    byResultData = null;
                        //}

                        //Value = iResultData;
                        //iResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站保持寄存器的当前值(int)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回保持寄存器的当前值(int)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadKeepRegister(byte DeviceAddress, ushort BeginAddress, ref int Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, 2);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //byte[] byResultData = new byte[4];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];
                        //byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 3];
                        //byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 2];

                        //Value = BitConverter.ToInt32(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站保持寄存器的当前值(int)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字节</param>
        /// <param name="Value">返回保持寄存器的当前值(int)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadKeepRegister(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref int[] Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
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

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, ReadDataLength * 2);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //int[] iResultData = new int[ReadDataLength];

                        //for (int i = 0; i < ReadDataLength; i++)
                        //{
                        //    byte[] byResultData = new byte[4];
                        //    byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 4 + 1];
                        //    byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 4 + 0];
                        //    byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 4 + 3];
                        //    byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 4 + 2];

                        //    iResultData[i] = BitConverter.ToInt32(byResultData, 0);
                        //    byResultData = null;
                        //}

                        //Value = iResultData;
                        //iResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站保持寄存器的当前值(uint)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回保持寄存器的当前值(uint)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadKeepRegister(byte DeviceAddress, ushort BeginAddress, ref uint Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, 2);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //byte[] byResultData = new byte[4];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];
                        //byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 3];
                        //byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 2];

                        //Value = BitConverter.ToUInt32(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站保持寄存器的当前值(uint)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字节</param>
        /// <param name="Value">返回保持寄存器的当前值(uint)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadKeepRegister(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref uint[] Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
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

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, ReadDataLength * 2);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //uint[] iResultData = new uint[ReadDataLength];

                        //for (int i = 0; i < ReadDataLength; i++)
                        //{
                        //    byte[] byResultData = new byte[4];
                        //    byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 4 + 1];
                        //    byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 4 + 0];
                        //    byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 4 + 3];
                        //    byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 4 + 2];

                        //    iResultData[i] = BitConverter.ToUInt32(byResultData, 0);
                        //    byResultData = null;
                        //}

                        //Value = iResultData;
                        //iResultData = null;

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站float保持寄存器的当前值(32位浮点值)(float  -3.4×10的38次方 到 +3.4×10的38次方, 精度：7 位)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回float保持寄存器的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadKeepRegister(byte DeviceAddress, ushort BeginAddress, ref float Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, 2);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //byte[] byResultData = new byte[4];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];
                        //byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 3];
                        //byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 2];

                        //Value = BitConverter.ToSingle(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站float保持寄存器的当前值(32位浮点值)(float  -3.4×10的38次方 到 +3.4×10的38次方, 精度：7 位)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：float</param>
        /// <param name="Value">返回float保持寄存器的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadKeepRegister(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref float[] Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }
                
                if (BeginAddress + ReadDataLength * 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, ReadDataLength * 2);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //float [] iResultData = new float[ReadDataLength];

                        //for (int i = 0; i < ReadDataLength; i++)
                        //{
                        //    byte[] byResultData = new byte[4];
                        //    byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 4 + 1];
                        //    byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 4 + 0];
                        //    byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 4 + 3];
                        //    byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 4 + 2];

                        //    iResultData[i] = BitConverter.ToSingle(byResultData, 0);
                        //    byResultData = null;
                        //}

                        //Value = iResultData;
                        //iResultData = null;

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
                    //MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站double(64位浮点值)值到从站保持寄存器(±5.0×10的−324次方 到 ±1.7×10的308次方   精度:15到16位)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回double保持寄存器的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadKeepRegister(byte DeviceAddress, ushort BeginAddress, ref double Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, 4);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //byte[] byResultData = new byte[8];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];
                        //byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 3];
                        //byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 2];
                        //byResultData[4] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 5];
                        //byResultData[5] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 4];
                        //byResultData[6] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 7];
                        //byResultData[7] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 6];

                        //Value = BitConverter.ToDouble(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站double(64位浮点值)值到从站保持寄存器(±5.0×10的−324次方 到 ±1.7×10的308次方   精度:15到16位)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：double</param>
        /// <param name="Value">返回double保持寄存器的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadKeepRegister(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref double[] Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                //if (ReadDataLength < 1 || ReadDataLength > 125)//读取数据长度，有效值范围：1~125(字)
                //{
                //    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                //}

                if (BeginAddress + ReadDataLength * 8 * 8 > 65535)//1个字 = 2 个字节 = 16位
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, ReadDataLength * 4);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //double[] lResultData = new double[ReadDataLength];

                        //for (int i = 0; i < ReadDataLength; i++)
                        //{
                        //    byte[] byResultData = new byte[8];

                        //    byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 1];
                        //    byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 0];
                        //    byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 3];
                        //    byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 2];
                        //    byResultData[4] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 5];
                        //    byResultData[5] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 4];
                        //    byResultData[6] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 7];
                        //    byResultData[7] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 6];

                        //    lResultData[i] = BitConverter.ToDouble(byResultData, 0);
                        //    byResultData = null;
                        //}

                        //Value = lResultData;
                        //lResultData = null;

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
                    //MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站long(64位整数值)值到从站保持寄存器(-9,223,372,036,854,775,808 到 9,223,372,036,854,775,807, 有符号 64 位整数)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回long保持寄存器的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadKeepRegister(byte DeviceAddress, ushort BeginAddress, ref long Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, 4);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //byte[] byResultData = new byte[8];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];
                        //byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 3];
                        //byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 2];
                        //byResultData[4] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 5];
                        //byResultData[5] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 4];
                        //byResultData[6] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 7];
                        //byResultData[7] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 6];

                        //Value = BitConverter.ToInt64(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站long(64位整数值)值到从站保持寄存器(-9,223,372,036,854,775,808 到 9,223,372,036,854,775,807, 有符号 64 位整数)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：double</param>
        /// <param name="Value">返回long保持寄存器的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadKeepRegister(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref long[] Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                //if (ReadDataLength < 1 || ReadDataLength > 125)//读取数据长度，有效值范围：1~125(字)
                //{
                //    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                //}

                if (BeginAddress + ReadDataLength * 8 * 8 > 65535)//1个字 = 2 个字节 = 16位
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, ReadDataLength * 4);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //Int64[] lResultData = new Int64[ReadDataLength];

                        //for (int i = 0; i < ReadDataLength; i++)
                        //{
                        //    byte[] byResultData = new byte[8];

                        //    byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 1];
                        //    byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 0];
                        //    byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 3];
                        //    byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 2];
                        //    byResultData[4] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 5];
                        //    byResultData[5] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 4];
                        //    byResultData[6] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 7];
                        //    byResultData[7] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 6];

                        //    lResultData[i] = BitConverter.ToInt64(byResultData, 0);
                        //    byResultData = null;
                        //}

                        //Value = lResultData;
                        //lResultData = null;

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
                    //MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok 
        /// <summary>
        /// 读取从站ulong值到从站保持寄存器(0 到 18,446,744,073,709,551,615)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回ulong保持寄存器的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadKeepRegister(byte DeviceAddress, ushort BeginAddress, ref ulong Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, 4);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //byte[] byResultData = new byte[8];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];
                        //byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 3];
                        //byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 2];
                        //byResultData[4] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 5];
                        //byResultData[5] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 4];
                        //byResultData[6] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 7];
                        //byResultData[7] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 6];

                        //Value = BitConverter.ToUInt64(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
        /// 读取从站ulong值到从站保持寄存器(0 到 18,446,744,073,709,551,615)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：double</param>
        /// <param name="Value">返回ulong保持寄存器的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadKeepRegister(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref ulong[] Value)//  , out string ReadBackData)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                //if (ReadDataLength < 1 || ReadDataLength > 125)//读取数据长度，有效值范围：1~125(字)
                //{
                //    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                //}

                if (BeginAddress + ReadDataLength * 8 * 8 > 65535)//1个字 = 2 个字节 = 16位
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //读取格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, ReadDataLength * 4);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

                    if (byReadData[PosIndexOfSlaveAddressInSocketReceivedBytes] == byDataToBeSent[PosIndexOfSlaveAddressInSocketReceivedBytes]
                        && byReadData[PosIndexOfFuncCodeInSocketReceivedBytes] == byDataToBeSent[PosIndexOfFuncCodeInSocketReceivedBytes])
                    {
                        //UInt64[] lResultData = new UInt64[ReadDataLength];

                        //for (int i = 0; i < ReadDataLength; i++)
                        //{
                        //    byte[] byResultData = new byte[8];

                        //    byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 1];
                        //    byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 0];
                        //    byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 3];
                        //    byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 2];
                        //    byResultData[4] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 5];
                        //    byResultData[5] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 4];
                        //    byResultData[6] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 7];
                        //    byResultData[7] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 8 + 6];

                        //    lResultData[i] = BitConverter.ToUInt64(byResultData, 0);
                        //    byResultData = null;
                        //}

                        //Value = lResultData;
                        //lResultData = null;

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
                    //MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        #endregion

        #endregion

        #region "写 - ok"

        #region "写单个/多个线圈 - ok-ok"

        #region "测试记录"

        //成功执行写命令:True
        //写操作记录：(127.0.0.1:502)发送字节转换为16进制 - 00 00 00 00 00 06 01 05 00 00 FF 00 
        //写操作记录：(127.0.0.1:502)收到字节转换为16进制 - 00 00 00 00 00 06 01 05 00 00 FF 00 

        //成功执行读命令，结果值：True
        //(127.0.0.1:502)发送字节转换为16进制 - 00 01 00 00 00 06 01 01 00 00 00 01 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 01 00 00 00 04 01 01 01 01 

        //成功执行写命令91
        //写操作记录：(127.0.0.1:502)发送字节转换为16进制 - 00 01 00 00 00 08 01 0F 00 00 00 08 01 5B 
        //写操作记录：(127.0.0.1:502)收到字节转换为16进制 - 00 01 00 00 00 06 01 0F 00 00 00 08 

        //成功执行读命令，结果值：91
        //(127.0.0.1:502)发送字节转换为16进制 - 00 02 00 00 00 06 01 01 00 00 00 08 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 02 00 00 00 04 01 01 01 5B 

        //成功执行写命令:False
        //写操作记录：(127.0.0.1:502)发送字节转换为16进制 - 00 01 00 00 00 06 01 05 00 00 00 00 
        //写操作记录：(127.0.0.1:502)收到字节转换为16进制 - 00 01 00 00 00 06 01 05 00 00 00 00 

        //成功执行读命令，结果值：False
        //(127.0.0.1:502)发送字节转换为16进制 - 00 03 00 00 00 06 01 01 00 00 00 01 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 03 00 00 00 04 01 01 01 00 

        //成功执行写命令:True
        //写操作记录：(127.0.0.1:502)发送字节转换为16进制 - 00 01 00 00 00 06 01 05 00 00 FF 00 
        //写操作记录：(127.0.0.1:502)收到字节转换为16进制 - 00 01 00 00 00 06 01 05 00 00 FF 00
 
        //成功执行读命令，结果值：True
        //(127.0.0.1:502)发送字节转换为16进制 - 00 04 00 00 00 06 01 01 00 00 00 01 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 04 00 00 00 04 01 01 01 01 

        //成功执行写命令134
        //写操作记录：(127.0.0.1:502)发送字节转换为16进制 - 00 02 00 00 00 08 01 0F 00 00 00 08 01 86 
        //写操作记录：(127.0.0.1:502)收到字节转换为16进制 - 00 02 00 00 00 06 01 0F 00 00 00 08 

        //成功执行读命令，结果值：134
        //(127.0.0.1:502)发送字节转换为16进制 - 00 05 00 00 00 06 01 01 00 00 00 08 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 05 00 00 00 04 01 01 01 86 

        //成功执行写命令20178
        //写操作记录：(127.0.0.1:502)发送字节转换为16进制 - 00 03 00 00 00 09 01 0F 00 00 00 10 02 D2 4E 
        //写操作记录：(127.0.0.1:502)收到字节转换为16进制 - 00 03 00 00 00 06 01 0F 00 00 00 10 

        //成功执行读命令，结果值：20178
        //(127.0.0.1:502)发送字节转换为16进制 - 00 06 00 00 00 06 01 01 00 00 00 10 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 06 00 00 00 05 01 01 02 D2 4E 

        //成功执行写命令-210089264
        //写操作记录：(127.0.0.1:502)发送字节转换为16进制 - 00 04 00 00 00 0B 01 0F 00 00 00 20 04 D0 4A 7A F3 
        //写操作记录：(127.0.0.1:502)收到字节转换为16进制 - 00 04 00 00 00 06 01 0F 00 00 00 20 

        //成功执行读命令，结果值：-210089264
        //(127.0.0.1:502)发送字节转换为16进制 - 00 07 00 00 00 06 01 01 00 00 00 20 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 07 00 00 00 07 01 01 04 D0 4A 7A F3 

        //成功执行写命令1365108224
        //写操作记录：(127.0.0.1:502)发送字节转换为16进制 - 00 05 00 00 00 0F 01 0F 00 00 00 40 08 00 E6 5D 51 00 00 00 00 
        //写操作记录：(127.0.0.1:502)收到字节转换为16进制 - 00 05 00 00 00 06 01 0F 00 00 00 40 

        //成功执行读命令，结果值：1365108224
        //(127.0.0.1:502)发送字节转换为16进制 - 00 08 00 00 00 06 01 01 00 00 00 40 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 08 00 00 00 0B 01 01 08 00 E6 5D 51 00 00 00 00 

        //发送值[0~15]：False False True True True True True False False True False False False True False False
        //写操作记录：(127.0.0.1:502)发送字节转换为16进制 - 00 06 00 00 00 09 01 0F 00 00 00 10 02 7C 22 
        //写操作记录：(127.0.0.1:502)收到字节转换为16进制 - 00 06 00 00 00 06 01 0F 00 00 00 10 

        //成功执行读命令，结果值：False False True True True True True False False True False False False True False False 
        //(127.0.0.1:502)发送字节转换为16进制 - 00 09 00 00 00 06 01 01 00 00 00 10 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 09 00 00 00 05 01 01 02 7C 22 

        //发送值[0~1]：91 95
        //写操作记录：(127.0.0.1:502)发送字节转换为16进制 - 00 07 00 00 00 09 01 0F 00 00 00 10 02 5B 5F 
        //写操作记录：(127.0.0.1:502)收到字节转换为16进制 - 00 07 00 00 00 06 01 0F 00 00 00 10 

        //成功执行读命令，结果值：91 95 
        //(127.0.0.1:502)发送字节转换为16进制 - 00 0A 00 00 00 06 01 01 00 00 00 10 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 0A 00 00 00 05 01 01 02 5B 5F 

        //发送值[0~1]：-9096 3138
        //写操作记录：(127.0.0.1:502)发送字节转换为16进制 - 00 08 00 00 00 0B 01 0F 00 00 00 20 04 78 DC 42 0C 
        //写操作记录：(127.0.0.1:502)收到字节转换为16进制 - 00 08 00 00 00 06 01 0F 00 00 00 20 

        //成功执行读命令，结果值：-9096 3138 
        //(127.0.0.1:502)发送字节转换为16进制 - 00 0B 00 00 00 06 01 01 00 00 00 20 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 0B 00 00 00 07 01 01 04 78 DC 42 0C 

        //发送值[0~1]：-321615187 1928742516
        //写操作记录：(127.0.0.1:502)发送字节转换为16进制 - 00 09 00 00 00 0F 01 0F 00 00 00 40 08 AD 8A D4 EC 74 46 F6 72 
        //写操作记录：(127.0.0.1:502)收到字节转换为16进制 - 00 09 00 00 00 06 01 0F 00 00 00 40 

        //成功执行读命令，结果值：-321615187 1928742516 
        //(127.0.0.1:502)发送字节转换为16进制 - 00 0C 00 00 00 06 01 01 00 00 00 40 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 0C 00 00 00 0B 01 01 08 AD 8A D4 EC 74 46 F6 72 

        //发送值[0~1]：-1334865154 -602528704
        //写操作记录：(127.0.0.1:502)发送字节转换为16进制 - 00 0A 00 00 00 17 01 0F 00 00 00 80 10 FE 92 6F B0 FF FF FF FF 40 24 16 DC FF FF FF FF 
        //写操作记录：(127.0.0.1:502)收到字节转换为16进制 - 00 0A 00 00 00 06 01 0F 00 00 00 80 

        //成功执行读命令，结果值：-1334865154 -602528704 
        //(127.0.0.1:502)发送字节转换为16进制 - 00 0D 00 00 00 06 01 01 00 00 00 80 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 0D 00 00 00 13 01 01 10 FE 92 6F B0 FF FF FF FF 40 24 16 DC FF FF FF FF 

        #endregion

        // ok-ok
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
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                //写格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2N个字节(序号：10~N)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  写数据
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                byte[] byData = new byte[2];
                // 单个线圈值的设置
                //0x0000	释放继电器线圈
                //0xFF00	吸合继电器线圈
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

                #region "Old codes -- No use"

                //byDataToBeSent = new byte[10 + 2];//固定字节长度 10 加上发送字节数据长度

                ////消息ID
                //stMsgIDForWriting++;
                //byte[] byMsgID = BitConverter.GetBytes(stMsgIDForWriting);
                //byDataToBeSent[0] = byMsgID[1];
                //byDataToBeSent[1] = byMsgID[0];

                ////协议标识
                //byte[] byProtocolID = BitConverter.GetBytes(0);
                //byDataToBeSent[2] = byProtocolID[1];
                //byDataToBeSent[3] = byProtocolID[0];

                ////命令字节的长度
                //byte[] byCmdBytesLength = BitConverter.GetBytes(4 + 2);//固定字节长度 4 加上发送字节数据长度
                //byDataToBeSent[4] = byCmdBytesLength[1];
                //byDataToBeSent[5] = byCmdBytesLength[0];

                ////读取的从站编号
                //byDataToBeSent[6] = DeviceAddress;

                ////功能码
                //byDataToBeSent[7] = CModbusFunctionCode.WriteCoil;

                ////起始地址
                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress, ParaBytesFormat);
                //byDataToBeSent[8] = byBeginAddress[0]; // byLocalModbusStationAddress[0];
                //byDataToBeSent[9] = byBeginAddress[1]; // byLocalModbusStationAddress[1];

                ////发送数据
                //for (int i = 0; i < byData.Length; i++)
                //{
                //    byDataToBeSent[10 + i] = byData[i];
                //}

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}
                
                #endregion

                #region "以字节方式处理从站的返回结果"

                //sFeedBackFromSlave = null;

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {
                    #region "通讯记录"




                    #endregion

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        // ok-ok
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
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (null == SetValue || SetValue.Length < 1)
                {
                    throw new Exception("设置值数组不能为空");
                }

                if (SetValue.Length % 8 != 0)
                {
                    throw new Exception("bool[]数组的长度不是8的整数，即不是以整字节的形式，请修改参数数组的长度");
                }

                //if (SetValue.Length > 1968)
                //{
                //    throw new Exception("设置值bool[]数组的长度超出范围(1~1968)，请修改参数数组的长度");
                //}

                if (BeginAddress + SetValue.Length > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //写格式：
                //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2N个字节(序号：10~N)
                //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  写数据
                //ushort				        一般为0										     			                            ushort
                //高低字节交换			        	        								                                            高低字节交换

                //写多个线圈格式：
                //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  2个字节(序号：10~11)  1个字节(序号：12)  N个字节(序号：13~(N-1))
                //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  数据位数量            字节数量           写数据
                //ushort				      一般为0										     			              ushort
                //高低字节交换			        	        								                              高低字节交换

                // 线圈值的设置 - 将布尔数组转换为字节数组
                byte[] byBitArrayToBytes = CConverter.BitArrayToByte(SetValue);
                
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length, byBitArrayToBytes);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = PackMultiWriteCoilCmd(DeviceAddress, byBitArrayToBytes, SetValue.Length);//CModbusFunctionCode.WriteMultiCoil, 

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}
                
                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //         Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                //sFeedBackFromSlave = null;

                #endregion
                
                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

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
                    //MessageBox.Show("Modbus Socket 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        // ok-ok
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
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress + 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //写格式：
                //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  2个字节(序号：10~11)  1个字节(序号：12)  N个字节(序号：13~(N-1))
                //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  数据位数量            字节数量           写数据
                //ushort				      一般为0										     			              ushort
                //高低字节交换			        	        								                              高低字节交换

                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, 8, new byte[]{ SetValue });
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);
     
                #region "Old codes -- No use"

                //Rx:00 10 00 00 00 08 01 0F 00 00 00 08 01 2F
                //Tx:00 10 00 00 00 06 01 0F 00 00 00 08

                //byDataToBeSent = new byte[10 + 2 + 1 + 1];//固定字节长度 10 加上发送字节数据长度

                ////消息ID
                //byte[] byMsgID = BitConverter.GetBytes(stMsgIDForWriting);
                //byDataToBeSent[0] = byMsgID[1];
                //byDataToBeSent[1] = byMsgID[0];

                ////协议标识
                //byte[] byProtocolID = BitConverter.GetBytes(0);
                //byDataToBeSent[2] = byProtocolID[1];
                //byDataToBeSent[3] = byProtocolID[0];

                ////命令字节的长度
                //byte[] byCmdBytesLength = BitConverter.GetBytes(7 + 1);//固定字节长度 7 加上发送字节数据长度
                //byDataToBeSent[4] = byCmdBytesLength[1];
                //byDataToBeSent[5] = byCmdBytesLength[0];

                ////读取的从站编号
                //byDataToBeSent[6] = DeviceAddress;

                ////功能码
                //byDataToBeSent[7] = CModbusFunctionCode.WriteMultiCoil;

                ////起始地址
                //byDataToBeSent[8] = byLocalModbusStationAddress[0];
                //byDataToBeSent[9] = byLocalModbusStationAddress[1];

                ////数据长度 - 功能码的数据长度计数
                //byte[] byBitDataLength = BitConverter.GetBytes((short)1 * 8);
                //byDataToBeSent[10] = byBitDataLength[1];
                //byDataToBeSent[11] = byBitDataLength[0];

                ////数据长度 - 字节计数
                //byte[] byByteDataLength = BitConverter.GetBytes((short)1);
                //byDataToBeSent[12] = byByteDataLength[0];

                ////发送数据
                //byDataToBeSent[13] = SetValue;

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                //sFeedBackFromSlave = null;
                
                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        // ok-ok
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
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (null == SetValue || SetValue.Length < 1)
                {
                    throw new Exception("设置值数组不能为空");
                }

                //if (SetValue.Length * 8 > 1968)
                //{
                //    throw new Exception("设置值byte[]数组的长度超出范围(1~246)，请修改参数数组的长度");
                //}

                if (BeginAddress + SetValue.Length * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //写格式：
                //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  2个字节(序号：10~11)  1个字节(序号：12)  N个字节(序号：13~(N-1))
                //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  数据位数量            字节数量           写数据
                //ushort				      一般为0										     			              ushort
                //高低字节交换			        	        								                              高低字节交换

                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length * 8, SetValue);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = PackMultiWriteCoilCmd(DeviceAddress, SetValue, SetValue.Length * 8);//CModbusFunctionCode.WriteMultiCoil, 

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();
                
                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                //sFeedBackFromSlave = null;

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        // ok-ok
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
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress + 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //写格式：
                //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  2个字节(序号：10~11)  1个字节(序号：12)  N个字节(序号：13~(N-1))
                //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  数据位数量            字节数量           写数据
                //ushort				      一般为0										     			              ushort
                //高低字节交换			        	        								                              高低字节交换
                
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, 8, new byte[]{ (byte)SetValue });
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //Rx:00 10 00 00 00 08 01 0F 00 00 00 08 01 2F
                //Tx:00 10 00 00 00 06 01 0F 00 00 00 08

                //byDataToBeSent = new byte[10 + 2 + 1 + 1];//固定字节长度 10 加上发送字节数据长度

                ////消息ID
                //byte[] byMsgID = BitConverter.GetBytes(stMsgIDForWriting);
                //byDataToBeSent[0] = byMsgID[1];
                //byDataToBeSent[1] = byMsgID[0];

                ////协议标识
                //byte[] byProtocolID = BitConverter.GetBytes(0);
                //byDataToBeSent[2] = byProtocolID[1];
                //byDataToBeSent[3] = byProtocolID[0];

                ////命令字节的长度
                //byte[] byCmdBytesLength = BitConverter.GetBytes(7 + 1);//固定字节长度 7 加上发送字节数据长度
                //byDataToBeSent[4] = byCmdBytesLength[1];
                //byDataToBeSent[5] = byCmdBytesLength[0];

                ////读取的从站编号
                //byDataToBeSent[6] = DeviceAddress;

                ////功能码
                //byDataToBeSent[7] = CModbusFunctionCode.WriteMultiCoil;

                ////起始地址
                //byDataToBeSent[8] = byLocalModbusStationAddress[0];
                //byDataToBeSent[9] = byLocalModbusStationAddress[1];

                ////数据长度 - 功能码的数据长度计数
                //byte[] byBitDataLength = BitConverter.GetBytes((short)1 * 8);
                //byDataToBeSent[10] = byBitDataLength[1];
                //byDataToBeSent[11] = byBitDataLength[0];

                ////数据长度 - 字节计数
                //byte[] byByteDataLength = BitConverter.GetBytes((short)1);
                //byDataToBeSent[12] = byByteDataLength[0];

                ////发送数据
                //byDataToBeSent[13] = SetValue;

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                //sFeedBackFromSlave = null;

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        // ok-ok
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
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (null == SetValue || SetValue.Length < 1)
                {
                    throw new Exception("设置值数组不能为空");
                }

                //if (SetValue.Length * 8 > 1968)
                //{
                //    throw new Exception("设置值byte[]数组的长度超出范围(1~246)，请修改参数数组的长度");
                //}

                if (BeginAddress + SetValue.Length * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //写格式：
                //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  2个字节(序号：10~11)  1个字节(序号：12)  N个字节(序号：13~(N-1))
                //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  数据位数量            字节数量           写数据
                //ushort				      一般为0										     			              ushort
                //高低字节交换			        	        								                              高低字节交换
                
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

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = PackMultiWriteCoilCmd(DeviceAddress, byTemp, byTemp.Length * 8);//SetValue.Length * 8);//CModbusFunctionCode.WriteMultiCoil, 

                //byTemp = null;

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();
                
                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                //sFeedBackFromSlave = null;

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        // ok-ok
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
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //写格式：
                //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  2个字节(序号：10~11)  1个字节(序号：12)  N个字节(序号：13~(N-1))
                //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  数据位数量            字节数量           写数据
                //ushort				      一般为0										     			              ushort
                //高低字节交换			        	        								                              高低字节交换
                
                byte[] byData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, 2 * 8, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //// 线圈值的设置 - 字节值
                //byte[] byData = CConverter.ToBytes(SetValue);

                //byte[] byDataToBeSent = PackMultiWriteCoilCmd(DeviceAddress, byData, 2 * 8);//CModbusFunctionCode.WriteMultiCoil, 

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                //sFeedBackFromSlave = null;

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        // ok-ok
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
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (null == SetValue || SetValue.Length < 1)
                {
                    throw new Exception("设置值数组不能为空");
                }

                //if (SetValue.Length * 2 * 8 > 1968)
                //{
                //    throw new Exception("设置值byte[]数组的长度超出范围(1~123)，请修改参数数组的长度");
                //}

                if (BeginAddress + SetValue.Length * 2 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //写格式：
                //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  2个字节(序号：10~11)  1个字节(序号：12)  N个字节(序号：13~(N-1))
                //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  数据位数量            字节数量           写数据
                //ushort				      一般为0										     			              ushort
                //高低字节交换			        	        								                              高低字节交换
                
                byte[] byData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length * 2 * 8, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //// 线圈值的设置 - 字节值
                //byte[] byData = new byte[SetValue.Length * 2];
                //for (int i = 0; i < SetValue.Length; i++)
                //{
                //    byte[] byShortToBytes = CConverter.ToBytes(SetValue[i]);

                //    byData[i * 2 + 0] = byShortToBytes[0];
                //    byData[i * 2 + 1] = byShortToBytes[1];

                //    byShortToBytes = null;
                //}

                //byte[] byDataToBeSent = PackMultiWriteCoilCmd(DeviceAddress, byData, SetValue.Length * 2 * 8);//CModbusFunctionCode.WriteMultiCoil, 

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                //sFeedBackFromSlave = null;

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        // ok-ok
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
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //写格式：
                //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  2个字节(序号：10~11)  1个字节(序号：12)  N个字节(序号：13~(N-1))
                //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  数据位数量            字节数量           写数据
                //ushort				      一般为0										     			              ushort
                //高低字节交换			        	        								                              高低字节交换
                
                byte[] byData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, 2 * 8, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //// 线圈值的设置 - 字节值
                //byte[] byData = CConverter.ToBytes((short)SetValue);

                //byte[] byDataToBeSent = PackMultiWriteCoilCmd(DeviceAddress, byData, 2 * 8);//CModbusFunctionCode.WriteMultiCoil, 

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                //sFeedBackFromSlave = null;

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        // ok-ok
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
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (null == SetValue || SetValue.Length < 1)
                {
                    throw new Exception("设置值数组不能为空");
                }

                //if (SetValue.Length * 2 * 8 > 1968)
                //{
                //    throw new Exception("设置值byte[]数组的长度超出范围(1~123)，请修改参数数组的长度");
                //}

                if (BeginAddress + SetValue.Length * 2 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //写格式：
                //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  2个字节(序号：10~11)  1个字节(序号：12)  N个字节(序号：13~(N-1))
                //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  数据位数量            字节数量           写数据
                //ushort				      一般为0										     			              ushort
                //高低字节交换			        	        								                              高低字节交换
                
                byte[] byData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length * 2 * 8, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //// 线圈值的设置 - 字节值
                //byte[] byData = new byte[SetValue.Length * 2];
                //for (int i = 0; i < SetValue.Length; i++)
                //{
                //    byte[] byShortToBytes = CConverter.ToBytes((short)SetValue[i]);

                //    byData[i * 2 + 0] = byShortToBytes[0];
                //    byData[i * 2 + 1] = byShortToBytes[1];

                //    byShortToBytes = null;
                //}

                //byte[] byDataToBeSent = PackMultiWriteCoilCmd(DeviceAddress, byData, SetValue.Length * 2 * 8);//CModbusFunctionCode.WriteMultiCoil, 

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                //sFeedBackFromSlave = null;

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        // ok-ok
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
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //写格式：
                //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  2个字节(序号：10~11)  1个字节(序号：12)  N个字节(序号：13~(N-1))
                //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  数据位数量            字节数量           写数据
                //ushort				      一般为0										     			              ushort
                //高低字节交换			        	        								                              高低字节交换
                
                byte[] byData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, 4 * 8, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //// 线圈值的设置 - 字节值
                //byte[] byData = BitConverter.GetBytes(SetValue);

                //byte[] byDataToBeSent = PackMultiWriteCoilCmd(DeviceAddress, byData, 4 * 8);//CModbusFunctionCode.WriteMultiCoil, 

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                //sFeedBackFromSlave = null;

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {
                    #region "通讯记录"




                    #endregion

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        // ok-ok
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
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (null == SetValue || SetValue.Length < 1)
                {
                    throw new Exception("设置值数组不能为空");
                }

                //if (SetValue.Length * 4 * 8 > 1968)
                //{
                //    throw new Exception("设置值byte[]数组的长度超出范围(1~123)，请修改参数数组的长度");
                //}

                if (BeginAddress + SetValue.Length * 4 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //写格式：
                //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  2个字节(序号：10~11)  1个字节(序号：12)  N个字节(序号：13~(N-1))
                //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  数据位数量            字节数量           写数据
                //ushort				      一般为0										     			              ushort
                //高低字节交换			        	        								                              高低字节交换
                
                byte[] byData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length * 4 * 8, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //// 线圈值的设置 - 字节值
                //byte[] byData = new byte[SetValue.Length * 4];
                //for (int i = 0; i < SetValue.Length; i++)
                //{
                //    byte[] byShortToBytes = BitConverter.GetBytes(SetValue[i]);

                //    byData[i * 4 + 0] = byShortToBytes[0];
                //    byData[i * 4 + 1] = byShortToBytes[1];
                //    byData[i * 4 + 2] = byShortToBytes[2];
                //    byData[i * 4 + 3] = byShortToBytes[3];

                //    byShortToBytes = null;
                //}

                //byte[] byDataToBeSent = PackMultiWriteCoilCmd(DeviceAddress, byData, SetValue.Length * 4 * 8);//CModbusFunctionCode.WriteMultiCoil, 

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                //sFeedBackFromSlave = null;

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        // ok-ok
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
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //写格式：
                //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  2个字节(序号：10~11)  1个字节(序号：12)  N个字节(序号：13~(N-1))
                //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  数据位数量            字节数量           写数据
                //ushort				      一般为0										     			              ushort
                //高低字节交换			        	        								                              高低字节交换
                
                byte[] byData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, 4 * 8, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //// 线圈值的设置 - 字节值
                //byte[] byData = BitConverter.GetBytes(SetValue);

                //byte[] byDataToBeSent = PackMultiWriteCoilCmd(DeviceAddress, byData, 4 * 8);//CModbusFunctionCode.WriteMultiCoil, 

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                //sFeedBackFromSlave = null;

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {
                    #region "通讯记录"




                    #endregion

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        // ok-ok
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
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (null == SetValue || SetValue.Length < 1)
                {
                    throw new Exception("设置值数组不能为空");
                }

                //if (SetValue.Length * 4 * 8 > 1968)
                //{
                //    throw new Exception("设置值byte[]数组的长度超出范围(1~123)，请修改参数数组的长度");
                //}

                if (BeginAddress + SetValue.Length * 4 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //写格式：
                //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  2个字节(序号：10~11)  1个字节(序号：12)  N个字节(序号：13~(N-1))
                //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  数据位数量            字节数量           写数据
                //ushort				      一般为0										     			              ushort
                //高低字节交换			        	        								                              高低字节交换
                
                byte[] byData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length * 4 * 8, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //// 线圈值的设置 - 字节值
                //byte[] byData = new byte[SetValue.Length * 4];
                //for (int i = 0; i < SetValue.Length; i++)
                //{
                //    byte[] byShortToBytes = BitConverter.GetBytes(SetValue[i]);

                //    byData[i * 4 + 0] = byShortToBytes[0];
                //    byData[i * 4 + 1] = byShortToBytes[1];
                //    byData[i * 4 + 2] = byShortToBytes[2];
                //    byData[i * 4 + 3] = byShortToBytes[3];

                //    byShortToBytes = null;
                //}

                //byte[] byDataToBeSent = PackMultiWriteCoilCmd(DeviceAddress, byData, SetValue.Length * 4 * 8);//CModbusFunctionCode.WriteMultiCoil, 

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                //sFeedBackFromSlave = null;

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        #region "Hide"

#if TEST

        // -
        /// <summary>
        /// 写从站1个双字的线圈
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">起始地址</param>
        /// <param name="SetValue">设置1个双字线圈的当前值</param>
        /// <returns>是否成功执行命令</returns>
        private bool WriteCoilWord(byte DeviceAddress, ushort BeginAddress, float SetValue)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //写格式：
                //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  2个字节(序号：10~11)  1个字节(序号：12)  N个字节(序号：13~(N-1))
                //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  数据位数量            字节数量           写数据
                //ushort				      一般为0										     			              ushort
                //高低字节交换			        	        								                              高低字节交换
                
                byte[] byData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, 4 * 8, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

        #region "Old codes -- No use"

                //// 线圈值的设置 - 字节值
                //byte[] byData = BitConverter.GetBytes(SetValue);

                //byte[] byDataToBeSent = PackMultiWriteCoilCmd(DeviceAddress, byData, 4 * 8);//CModbusFunctionCode.WriteMultiCoil, 

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                //sFeedBackFromSlave = null;

                #endregion

        #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {
        #region "通讯记录"



                    #endregion

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        // -
        /// <summary>
        /// 写从站N个双字的线圈
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">起始地址</param>
        /// <param name="SetValue">设置N个双字线圈的当前值</param>
        /// <returns>是否成功执行命令</returns>
        private bool WriteCoilWord(byte DeviceAddress, ushort BeginAddress, float[] SetValue)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (null == SetValue || SetValue.Length < 1)
                {
                    throw new Exception("设置值数组不能为空");
                }

                //if (SetValue.Length * 4 * 8 > 1968)
                //{
                //    throw new Exception("设置值byte[]数组的长度超出范围(1~123)，请修改参数数组的长度");
                //}

                if (BeginAddress + SetValue.Length * 4 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //写格式：
                //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  2个字节(序号：10~11)  1个字节(序号：12)  N个字节(序号：13~(N-1))
                //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  数据位数量            字节数量           写数据
                //ushort				      一般为0										     			              ushort
                //高低字节交换			        	        								                              高低字节交换
                
                byte[] byData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length * 4 * 8, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

        #region "Old codes -- No use"

                //// 线圈值的设置 - 字节值
                //byte[] byData = new byte[SetValue.Length * 4];
                //for (int i = 0; i < SetValue.Length; i++)
                //{
                //    byte[] byShortToBytes = BitConverter.GetBytes(SetValue[i]);

                //    byData[i * 4 + 0] = byShortToBytes[0];
                //    byData[i * 4 + 1] = byShortToBytes[1];
                //    byData[i * 4 + 2] = byShortToBytes[2];
                //    byData[i * 4 + 3] = byShortToBytes[3];

                //    byShortToBytes = null;
                //}

                //byte[] byDataToBeSent = PackMultiWriteCoilCmd(DeviceAddress, byData, SetValue.Length * 4 * 8);//CModbusFunctionCode.WriteMultiCoil, 

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                //sFeedBackFromSlave = null;

                #endregion

        #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {
        #region "通讯记录"

                    

                    #endregion

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        // -
        /// <summary>
        /// 写从站2个双字的线圈
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">起始地址</param>
        /// <param name="SetValue">设置2个双字线圈的当前值</param>
        /// <returns>是否成功执行命令</returns>
        private bool WriteCoilWord(byte DeviceAddress, ushort BeginAddress, double SetValue)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //写格式：
                //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  2个字节(序号：10~11)  1个字节(序号：12)  N个字节(序号：13~(N-1))
                //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  数据位数量            字节数量           写数据
                //ushort				      一般为0										     			              ushort
                //高低字节交换			        	        								                              高低字节交换
                
                byte[] byData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, 8 * 8, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

        #region "Old codes -- No use"

                //// 线圈值的设置 - 字节值
                //byte[] byData = BitConverter.GetBytes(SetValue);

                //byte[] byDataToBeSent = PackMultiWriteCoilCmd(DeviceAddress, byData, 8 * 8);//CModbusFunctionCode.WriteMultiCoil, 

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                //sFeedBackFromSlave = null;

                #endregion

        #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {
        #region "通讯记录"



                    #endregion

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        //- 
        /// <summary>
        /// 写从站N个2个双字的线圈
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">起始地址</param>
        /// <param name="SetValue">设置N个2个双字线圈的当前值</param>
        /// <returns>是否成功执行命令</returns>
        private bool WriteCoilWord(byte DeviceAddress, ushort BeginAddress, double[] SetValue)
        {
            try
            {
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (null == SetValue || SetValue.Length < 1)
                {
                    throw new Exception("设置值数组不能为空");
                }

                //if (SetValue.Length * 8 * 8 > 1968)
                //{
                //    throw new Exception("设置值byte[]数组的长度超出范围(1~123)，请修改参数数组的长度");
                //}

                if (BeginAddress + SetValue.Length * 8 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //写格式：
                //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  2个字节(序号：10~11)  1个字节(序号：12)  N个字节(序号：13~(N-1))
                //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  数据位数量            字节数量           写数据
                //ushort				      一般为0										     			              ushort
                //高低字节交换			        	        								                              高低字节交换
                
                byte[] byData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length * 8 * 8, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

        #region "Old codes -- No use"

                //// 线圈值的设置 - 字节值
                //byte[] byData = new byte[SetValue.Length * 8];
                //for (int i = 0; i < SetValue.Length; i++)
                //{
                //    byte[] byShortToBytes = BitConverter.GetBytes(SetValue[i]);

                //    byData[i * 8 + 0] = byShortToBytes[0];
                //    byData[i * 8 + 1] = byShortToBytes[1];
                //    byData[i * 8 + 2] = byShortToBytes[2];
                //    byData[i * 8 + 3] = byShortToBytes[3];
                //    byData[i * 8 + 4] = byShortToBytes[4];
                //    byData[i * 8 + 5] = byShortToBytes[5];
                //    byData[i * 8 + 6] = byShortToBytes[6];
                //    byData[i * 8 + 7] = byShortToBytes[7];

                //    byShortToBytes = null;
                //}

                //byte[] byDataToBeSent = PackMultiWriteCoilCmd(DeviceAddress, byData, SetValue.Length * 8 * 8);//CModbusFunctionCode.WriteMultiCoil, 

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                //sFeedBackFromSlave = null;

                #endregion

        #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {
        #region "通讯记录"

                    

                    #endregion

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

#endif

        #endregion

        // ok-ok
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
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //写格式：
                //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  2个字节(序号：10~11)  1个字节(序号：12)  N个字节(序号：13~(N-1))
                //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  数据位数量            字节数量           写数据
                //ushort				      一般为0										     			              ushort
                //高低字节交换			        	        								                              高低字节交换
                
                byte[] byData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, 8 * 8, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //// 线圈值的设置 - 字节值
                //byte[] byData = BitConverter.GetBytes(SetValue);

                //byte[] byDataToBeSent = PackMultiWriteCoilCmd(DeviceAddress, byData, 8 * 8);//CModbusFunctionCode.WriteMultiCoil, 

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                //sFeedBackFromSlave = null;

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        // ok-ok
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
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (null == SetValue || SetValue.Length < 1)
                {
                    throw new Exception("设置值数组不能为空");
                }

                //if (SetValue.Length * 8 * 8 > 1968)
                //{
                //    throw new Exception("设置值byte[]数组的长度超出范围(1~123)，请修改参数数组的长度");
                //}

                if (BeginAddress + SetValue.Length * 8 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //写格式：
                //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  2个字节(序号：10~11)  1个字节(序号：12)  N个字节(序号：13~(N-1))
                //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  数据位数量            字节数量           写数据
                //ushort				      一般为0										     			              ushort
                //高低字节交换			        	        								                              高低字节交换
                
                byte[] byData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length * 8 * 8, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //// 线圈值的设置 - 字节值
                //byte[] byData = new byte[SetValue.Length * 8];
                //for (int i = 0; i < SetValue.Length; i++)
                //{
                //    byte[] byShortToBytes = BitConverter.GetBytes(SetValue[i]);

                //    byData[i * 8 + 0] = byShortToBytes[0];
                //    byData[i * 8 + 1] = byShortToBytes[1];
                //    byData[i * 8 + 2] = byShortToBytes[2];
                //    byData[i * 8 + 3] = byShortToBytes[3];
                //    byData[i * 8 + 4] = byShortToBytes[4];
                //    byData[i * 8 + 5] = byShortToBytes[5];
                //    byData[i * 8 + 6] = byShortToBytes[6];
                //    byData[i * 8 + 7] = byShortToBytes[7];

                //    byShortToBytes = null;
                //}

                //byte[] byDataToBeSent = PackMultiWriteCoilCmd(DeviceAddress, byData, SetValue.Length * 8 * 8);//CModbusFunctionCode.WriteMultiCoil, 

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                //sFeedBackFromSlave = null;

                #endregion

                #region "以字节方式处理从站的返回结果"
                
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        // ok-ok
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
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //写格式：
                //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  2个字节(序号：10~11)  1个字节(序号：12)  N个字节(序号：13~(N-1))
                //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  数据位数量            字节数量           写数据
                //ushort				      一般为0										     			              ushort
                //高低字节交换			        	        								                              高低字节交换

                byte[] byData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, 8 * 8, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //// 线圈值的设置 - 字节值
                //byte[] byData = BitConverter.GetBytes(SetValue);

                //byte[] byDataToBeSent = PackMultiWriteCoilCmd(DeviceAddress, byData, 8 * 8);//CModbusFunctionCode.WriteMultiCoil, 

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                //sFeedBackFromSlave = null;

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        // ok-ok
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
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (null == SetValue || SetValue.Length < 1)
                {
                    throw new Exception("设置值数组不能为空");
                }

                //if (SetValue.Length * 8 * 8 > 1968)
                //{
                //    throw new Exception("设置值byte[]数组的长度超出范围(1~123)，请修改参数数组的长度");
                //}

                if (BeginAddress + SetValue.Length * 8 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //写格式：
                //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  2个字节(序号：10~11)  1个字节(序号：12)  N个字节(序号：13~(N-1))
                //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  数据位数量            字节数量           写数据
                //ushort				      一般为0										     			              ushort
                //高低字节交换			        	        								                              高低字节交换
                
                byte[] byData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length * 8 * 8, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //// 线圈值的设置 - 字节值
                //byte[] byData = new byte[SetValue.Length * 8];
                //for (int i = 0; i < SetValue.Length; i++)
                //{
                //    byte[] byShortToBytes = BitConverter.GetBytes(SetValue[i]);

                //    byData[i * 8 + 0] = byShortToBytes[0];
                //    byData[i * 8 + 1] = byShortToBytes[1];
                //    byData[i * 8 + 2] = byShortToBytes[2];
                //    byData[i * 8 + 3] = byShortToBytes[3];
                //    byData[i * 8 + 4] = byShortToBytes[4];
                //    byData[i * 8 + 5] = byShortToBytes[5];
                //    byData[i * 8 + 6] = byShortToBytes[6];
                //    byData[i * 8 + 7] = byShortToBytes[7];

                //    byShortToBytes = null;
                //}

                //byte[] byDataToBeSent = PackMultiWriteCoilCmd(DeviceAddress, byData, SetValue.Length * 8 * 8);//CModbusFunctionCode.WriteMultiCoil, 

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                //sFeedBackFromSlave = null;

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        #endregion

        #region "写单个/多个保持寄存器 - ok-ok"

        #region "通讯记录"

        //发送值[0~3]：1441031722 1760396733 -529649845 1165606092 
        //成功执行写命令
        //写操作记录：(127.0.0.1:502)发送字节转换为16进制 - 00 16 00 00 00 27 01 10 00 00 00 10 20 66 2A 55 E4 00 00 00 00 85 BD 68 ED 00 00 00 00 2F 4B E0 6E FF FF FF FF BC CC 45 79 00 00 00 00 
        //写操作记录：(127.0.0.1:502)收到字节转换为16进制 - 00 16 00 00 00 06 01 10 00 00 00 10 

        //成功执行读命令，结果值：1441031722 1760396733 
        //(127.0.0.1:502)发送字节转换为16进制 - 00 08 00 00 00 06 01 03 00 00 00 08 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 08 00 00 00 13 01 03 10 66 2A 55 E4 00 00 00 00 85 BD 68 ED 00 00 00 00 

        //发送值[0~3]：3706.57153320313 -5075.71435546875 7673.14306640625 -3167.71435546875 
        //成功执行写命令
        //写操作记录：(127.0.0.1:502)发送字节转换为16进制 - 00 17 00 00 00 27 01 10 00 00 00 10 20 00 00 A0 00 F5 24 40 AC 00 00 E0 00 D3 B6 C0 B3 00 00 A0 00 F9 24 40 BD 00 00 C0 00 BF 6D C0 A8 
        //写操作记录：(127.0.0.1:502)收到字节转换为16进制 - 00 17 00 00 00 06 01 10 00 00 00 10 

        //成功执行读命令，结果值：3706.57153320313 -5075.71435546875 
        //(127.0.0.1:502)发送字节转换为16进制 - 00 09 00 00 00 06 01 03 00 00 00 08 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 09 00 00 00 13 01 03 10 00 00 A0 00 F5 24 40 AC 00 00 E0 00 D3 B6 C0 B3 

        //发送值[0~3]：-485.7143 -1274.286 7623.714 9108.857 
        //成功执行写命令
        //写操作记录：(127.0.0.1:502)发送字节转换为16进制 - 00 18 00 00 00 17 01 10 00 00 00 08 10 DB 6E C3 F2 49 25 C4 9F 3D B7 45 EE 53 6E 46 0E 
        //写操作记录：(127.0.0.1:502)收到字节转换为16进制 - 00 18 00 00 00 06 01 10 00 00 00 08 

        //成功执行读命令，结果值：-485.7143 -1274.286 
        //(127.0.0.1:502)发送字节转换为16进制 - 00 0A 00 00 00 06 01 03 00 00 00 04 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 0A 00 00 00 0B 01 03 08 DB 6E C3 F2 49 25 C4 9F 

        //发送值[0~3]：535802649 1944875991 -146776081 757365993 
        //成功执行写命令
        //写操作记录：(127.0.0.1:502)发送字节转换为16进制 - 00 19 00 00 00 17 01 10 00 00 00 08 10 B3 19 1F EF 73 D7 73 EC 5F EF F7 40 7C E9 2D 24 
        //写操作记录：(127.0.0.1:502)收到字节转换为16进制 - 00 19 00 00 00 06 01 10 00 00 00 08 

        //成功执行读命令，结果值：535802649 1944875991 
        //(127.0.0.1:502)发送字节转换为16进制 - 00 0B 00 00 00 06 01 03 00 00 00 04 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 0B 00 00 00 0B 01 03 08 B3 19 1F EF 73 D7 73 EC 

        //发送值[0~4]：-24192 -23507 12017 32253 27699 
        //成功执行写命令
        //写操作记录：(127.0.0.1:502)发送字节转换为16进制 - 00 1A 00 00 00 11 01 10 00 00 00 05 0A A1 80 A4 2D 2E F1 7D FD 6C 33 
        //写操作记录：(127.0.0.1:502)收到字节转换为16进制 - 00 1A 00 00 00 06 01 10 00 00 00 05 

        //成功执行读命令，结果值：-24192 -23507 12017 32253 
        //(127.0.0.1:502)发送字节转换为16进制 - 00 0C 00 00 00 06 01 03 00 00 00 04 
        //(127.0.0.1:502)收到字节转换为16进制 - 00 0C 00 00 00 0B 01 03 08 A1 80 A4 2D 2E F1 7D FD 

        #endregion

        // ok-ok
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
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //写格式：
                //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  2个字节(序号：10~11)  1个字节(序号：12)  N个字节(序号：13~(N-1))
                //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  数据位数量            字节数量           写数据
                //ushort				      一般为0										     			              ushort
                //高低字节交换			        	        								                              高低字节交换
                
                byte[] byData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackSingleWriteCmd(DeviceAddress, ModbusFuncCode.WriteRegister, BeginAddress, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                ////值 - 字节值 - 高低字节互换
                //byte[] byData = CConverter.ToBytes(SetValue);

                //byte[] byTemp = new byte[byData.Length];

                //byTemp[0] = byData[1];
                //byTemp[1] = byData[0];

                //byData[0] = byTemp[0];
                //byData[1] = byTemp[1];

                //byte[] byDataToBeSent = PackSingleWriteCmd(DeviceAddress, byData);

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                //sFeedBackFromSlave = null;

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        // ok-ok
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
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (null == SetValue || SetValue.Length < 1)
                {
                    throw new Exception("设置值数组不能为空");
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                //if (SetValue.Length * 2 * 8 > 1968)
                //{
                //    throw new Exception("设置值byte[]数组的长度超出范围(1~123)，请修改参数数组的长度");
                //}

                if (BeginAddress + SetValue.Length * 2 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //写格式：
                //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  2个字节(序号：10~11)  1个字节(序号：12)  N个字节(序号：13~(N-1))
                //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  数据位数量            字节数量           写数据
                //ushort				      一般为0										     			              ushort
                //高低字节交换			        	        								                              高低字节交换

                byte[] byData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, SetValue.Length, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //// 线圈值的设置 - 字节值
                //byte[] byData = new byte[SetValue.Length * 2];
                //for (int i = 0; i < SetValue.Length; i++)
                //{
                //    byte[] byShortToBytes = CConverter.ToBytes(SetValue[i]);
                //    byData[i * 2 + 0] = byShortToBytes[1];
                //    byData[i * 2 + 1] = byShortToBytes[0];
                //}

                //byte[] byDataToBeSent = PackWriteMultiRegisterCmd(DeviceAddress, byData, SetValue.Length);

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                //sFeedBackFromSlave = null;

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        // ok-ok
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
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //写格式：
                //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  2个字节(序号：10~11)  1个字节(序号：12)  N个字节(序号：13~(N-1))
                //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  数据位数量            字节数量           写数据
                //ushort				      一般为0										     			              ushort
                //高低字节交换			        	        								                              高低字节交换


                byte[] byData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackSingleWriteCmd(DeviceAddress, ModbusFuncCode.WriteRegister, BeginAddress, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                ////值 - 字节值 - 高低字节互换
                //byte[] byData = CConverter.ToBytes((short)SetValue);

                //byte[] byTemp = new byte[byData.Length];

                //byTemp[0] = byData[1];
                //byTemp[1] = byData[0];

                //byData[0] = byTemp[0];
                //byData[1] = byTemp[1];

                //byte[] byDataToBeSent = PackSingleWriteCmd(DeviceAddress, byData);

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                //sFeedBackFromSlave = null;

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        // ok-ok
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
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (null == SetValue || SetValue.Length < 1)
                {
                    throw new Exception("设置值数组不能为空");
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                //if (SetValue.Length * 2 * 8 > 1968)
                //{
                //    throw new Exception("设置值byte[]数组的长度超出范围(1~123)，请修改参数数组的长度");
                //}

                if (BeginAddress + SetValue.Length * 2 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //写格式：
                //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  2个字节(序号：10~11)  1个字节(序号：12)  N个字节(序号：13~(N-1))
                //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  数据位数量            字节数量           写数据
                //ushort				      一般为0										     			              ushort
                //高低字节交换			        	        								                              高低字节交换
                
                byte[] byData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, SetValue.Length, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                //// 线圈值的设置 - 字节值
                //byte[] byData = new byte[SetValue.Length * 2];
                //for (int i = 0; i < SetValue.Length; i++)
                //{
                //    byte[] byShortToBytes = CConverter.ToBytes((short)SetValue[i]);
                //    byData[i * 2 + 0] = byShortToBytes[1];
                //    byData[i * 2 + 1] = byShortToBytes[0];
                //}

                //byte[] byDataToBeSent = PackWriteMultiRegisterCmd(DeviceAddress, byData, SetValue.Length);

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                //sFeedBackFromSlave = null;

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        // ok-ok
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
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //写格式：
                //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  2个字节(序号：10~11)  1个字节(序号：12)  N个字节(序号：13~(N-1))
                //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  数据位数量            字节数量           写数据
                //ushort				      一般为0										     			              ushort
                //高低字节交换			        	        								                              高低字节交换
                
                byte[] byData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, 2, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                ////值 - 字节值
                //byte[] byData = BitConverter.GetBytes(SetValue);

                //byte[] byDataToBeSent = PackWriteMultiRegisterCmd(DeviceAddress, byData, 2);

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                //sFeedBackFromSlave = null;

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        // ok-ok
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
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (null == SetValue || SetValue.Length < 1)
                {
                    throw new Exception("设置值数组不能为空");
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + SetValue.Length * 4 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //写格式：
                //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  2个字节(序号：10~11)  1个字节(序号：12)  N个字节(序号：13~(N-1))
                //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  数据位数量            字节数量           写数据
                //ushort				      一般为0										     			              ushort
                //高低字节交换			        	        								                              高低字节交换
                
                byte[] byData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, SetValue.Length * 2, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                ////值 - 字节值
                //byte[] byData = new byte[SetValue.Length * 4];
                //// 线圈值的设置 - 字节值
                //for (int j = 0; j < SetValue.Length; j++)
                //{
                //    byte[] byFloatToBytes = BitConverter.GetBytes(SetValue[j]);

                //    for (int i = 0; i < byFloatToBytes.Length / 2; i++)
                //    {
                //        byData[j * 4 + i * 2 + 0] = byFloatToBytes[i * 2 + 1];
                //        byData[j * 4 + i * 2 + 1] = byFloatToBytes[i * 2];
                //    }                    
                //}

                //byte[] byDataToBeSent = PackWriteMultiRegisterCmd(DeviceAddress, byData, SetValue.Length * 2);

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                //sFeedBackFromSlave = null;

                #endregion

                #region "以字节方式处理从站的返回结果"
                
                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        // ok-ok
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
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //写格式：
                //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  2个字节(序号：10~11)  1个字节(序号：12)  N个字节(序号：13~(N-1))
                //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  数据位数量            字节数量           写数据
                //ushort				      一般为0										     			              ushort
                //高低字节交换			        	        								                              高低字节交换
                
                byte[] byData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, 2, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                ////值 - 字节值
                //byte[] byData = BitConverter.GetBytes(SetValue);

                //byte[] byDataToBeSent = PackWriteMultiRegisterCmd(DeviceAddress, byData, 2);

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                //sFeedBackFromSlave = null;

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        // ok-ok
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
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (null == SetValue || SetValue.Length < 1)
                {
                    throw new Exception("设置值数组不能为空");
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + SetValue.Length * 4 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //写格式：
                //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  2个字节(序号：10~11)  1个字节(序号：12)  N个字节(序号：13~(N-1))
                //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  数据位数量            字节数量           写数据
                //ushort				      一般为0										     			              ushort
                //高低字节交换			        	        								                              高低字节交换
                
                byte[] byData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, SetValue.Length * 2, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                ////值 - 字节值
                //byte[] byData = new byte[SetValue.Length * 4];
                //// 线圈值的设置 - 字节值
                //for (int j = 0; j < SetValue.Length; j++)
                //{
                //    byte[] byFloatToBytes = BitConverter.GetBytes(SetValue[j]);

                //    for (int i = 0; i < byFloatToBytes.Length / 2; i++)
                //    {
                //        byData[j * 4 + i * 2 + 0] = byFloatToBytes[i * 2 + 1];
                //        byData[j * 4 + i * 2 + 1] = byFloatToBytes[i * 2];
                //    }                    
                //}

                //byte[] byDataToBeSent = PackWriteMultiRegisterCmd(DeviceAddress, byData, SetValue.Length * 2);

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                //sFeedBackFromSlave = null;

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        // ok-ok
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
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //写格式：
                //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  2个字节(序号：10~11)  1个字节(序号：12)  N个字节(序号：13~(N-1))
                //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  数据位数量            字节数量           写数据
                //ushort				      一般为0										     			              ushort
                //高低字节交换			        	        								                              高低字节交换
                
                byte[] byData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, 2, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                ////值 - 字节值
                //byte[] byData = BitConverter.GetBytes(SetValue);

                //byte[] byDataToBeSent = PackWriteMultiRegisterCmd(DeviceAddress, byData, 2);

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                //sFeedBackFromSlave = null;

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        // ok-ok
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
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (null == SetValue || SetValue.Length < 1)
                {
                    throw new Exception("设置值数组不能为空");
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + SetValue.Length * 4 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //写格式：
                //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  2个字节(序号：10~11)  1个字节(序号：12)  N个字节(序号：13~(N-1))
                //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  数据位数量            字节数量           写数据
                //ushort				      一般为0										     			              ushort
                //高低字节交换			        	        								                              高低字节交换
                
                byte[] byData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, SetValue.Length * 2, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                ////值 - 字节值
                //byte[] byData = new byte[SetValue.Length * 4];
                //for (int j = 0; j < SetValue.Length; j++)
                //{
                //    byte[] byFloatToBytes = BitConverter.GetBytes(SetValue[j]);

                //    for (int i = 0; i < byFloatToBytes.Length / 2; i++)
                //    {
                //        byData[j * 4 + i * 2] = byFloatToBytes[i * 2 + 1];
                //        byData[j * 4 + i * 2 + 1] = byFloatToBytes[i * 2];
                //    }
                //}

                //byte[] byDataToBeSent = PackWriteMultiRegisterCmd(DeviceAddress, byData, SetValue.Length * 2);

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                //sFeedBackFromSlave = null;

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        // ok-ok
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
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //写格式：
                //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  2个字节(序号：10~11)  1个字节(序号：12)  N个字节(序号：13~(N-1))
                //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  数据位数量            字节数量           写数据
                //ushort				      一般为0										     			              ushort
                //高低字节交换			        	        								                              高低字节交换
                
                byte[] byData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, 4, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                ////值 - 字节值
                //byte[] byData = BitConverter.GetBytes(SetValue);

                //byte[] byDataToBeSent = PackWriteMultiRegisterCmd(DeviceAddress, byData, 4);

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                //sFeedBackFromSlave = null;

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        // ok-ok
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
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (null == SetValue || SetValue.Length < 1)
                {
                    throw new Exception("设置值数组不能为空");
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + SetValue.Length * 8 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //写格式：
                //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  2个字节(序号：10~11)  1个字节(序号：12)  N个字节(序号：13~(N-1))
                //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  数据位数量            字节数量           写数据
                //ushort				      一般为0										     			              ushort
                //高低字节交换			        	        								                              高低字节交换
                
                byte[] byData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, SetValue.Length * 4, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                ////值 - 字节值
                //byte[] byData = new byte[SetValue.Length * 8];
                //for (int j = 0; j < SetValue.Length; j++)
                //{
                //    byte[] byDoubleToBytes = BitConverter.GetBytes(SetValue[j]);

                //    for (int i = 0; i < byDoubleToBytes.Length / 2; i++)
                //    {
                //        byData[j * 8 + i * 2 + 0] = byDoubleToBytes[i * 2 + 1];// 数据 - 高字节
                //        byData[j * 8 + i * 2 + 1] = byDoubleToBytes[i * 2 + 0];    // 数据 - 低字节
                //    }
                //}

                //byte[] byDataToBeSent = PackWriteMultiRegisterCmd(DeviceAddress, byData, SetValue.Length * 4);

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                //sFeedBackFromSlave = null;

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        // ok-ok
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
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //写格式：
                //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  2个字节(序号：10~11)  1个字节(序号：12)  N个字节(序号：13~(N-1))
                //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  数据位数量            字节数量           写数据
                //ushort				      一般为0										     			              ushort
                //高低字节交换			        	        								                              高低字节交换
                
                byte[] byData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, 4, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                ////值 - 字节值
                //byte[] byData = BitConverter.GetBytes(SetValue);
                ////byte[] byDoubleToBytes = BitConverter.GetBytes(SetValue);                
                ////for (int i = 0; i < byDoubleToBytes.Length / 2; i++)
                ////{
                ////    byDataToBeSent[7 + i * 2] = byDoubleToBytes[i * 2 + 1];// 数据 - 高字节
                ////    byDataToBeSent[7 + i * 2 + 1] = byDoubleToBytes[i * 2];// 数据 - 低字节
                ////}

                //byte[] byDataToBeSent = PackWriteMultiRegisterCmd(DeviceAddress, byData, 4);

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();
                
                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                //sFeedBackFromSlave = null;

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        // ok-ok
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
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (null == SetValue || SetValue.Length < 1)
                {
                    throw new Exception("设置值数组不能为空");
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + SetValue.Length * 8 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //写格式：
                //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  2个字节(序号：10~11)  1个字节(序号：12)  N个字节(序号：13~(N-1))
                //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  数据位数量            字节数量           写数据
                //ushort				      一般为0										     			              ushort
                //高低字节交换			        	        								                              高低字节交换
                
                byte[] byData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, SetValue.Length * 4, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                ////值 - 字节值
                //byte[] byData = new byte[SetValue.Length * 8];
                //for (int j = 0; j < SetValue.Length; j++)
                //{
                //    byte[] byDoubleToBytes = BitConverter.GetBytes(SetValue[j]);

                //    for (int i = 0; i < byDoubleToBytes.Length / 2; i++)
                //    {
                //        byData[j * 8 + i * 2 + 0] = byDoubleToBytes[i * 2 + 1];// 数据 - 高字节
                //        byData[j * 8 + i * 2 + 1] = byDoubleToBytes[i * 2 + 0];// 数据 - 低字节
                //    }
                //}

                //byte[] byDataToBeSent = PackWriteMultiRegisterCmd(DeviceAddress, byData, SetValue.Length * 4);

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                //sFeedBackFromSlave = null;

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        // ok-ok
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
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //写格式：
                //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  2个字节(序号：10~11)  1个字节(序号：12)  N个字节(序号：13~(N-1))
                //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  数据位数量            字节数量           写数据
                //ushort				      一般为0										     			              ushort
                //高低字节交换			        	        								                              高低字节交换

                byte[] byData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, 4, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                ////值 - 字节值
                //byte[] byData = BitConverter.GetBytes(SetValue);
                ////byte[] byDoubleToBytes = BitConverter.GetBytes(SetValue);                
                ////for (int i = 0; i < byDoubleToBytes.Length / 2; i++)
                ////{
                ////    byDataToBeSent[7 + i * 2] = byDoubleToBytes[i * 2 + 1];// 数据 - 高字节
                ////    byDataToBeSent[7 + i * 2 + 1] = byDoubleToBytes[i * 2];// 数据 - 低字节
                ////}

                //byte[] byDataToBeSent = PackWriteMultiRegisterCmd(DeviceAddress, byData, 4);

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();
                
                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                //sFeedBackFromSlave = null;

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        // ok-ok
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
                //if (bIsConnected == false)
                //{
                //    return false;
                //}

                if (null == SetValue || SetValue.Length < 1)
                {
                    throw new Exception("设置值数组不能为空");
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + SetValue.Length * 8 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //写格式：
                //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  2个字节(序号：10~11)  1个字节(序号：12)  N个字节(序号：13~(N-1))
                //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  数据位数量            字节数量           写数据
                //ushort				      一般为0										     			              ushort
                //高低字节交换			        	        								                              高低字节交换
                
                byte[] byData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);
                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, SetValue.Length * 4, byData);
                if (null == byDataToBeSent)
                {
                    return false;
                }

                byte[] byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                #region "Old codes -- No use"

                ////值 - 字节值
                //byte[] byData = new byte[SetValue.Length * 8];
                //for (int j = 0; j < SetValue.Length; j++)
                //{
                //    byte[] byDoubleToBytes = BitConverter.GetBytes(SetValue[j]);

                //    for (int i = 0; i < byDoubleToBytes.Length / 2; i++)
                //    {
                //        byData[j * 8 + i * 2 + 0] = byDoubleToBytes[i * 2 + 1];// 数据 - 高字节
                //        byData[j * 8 + i * 2 + 1] = byDoubleToBytes[i * 2 + 0];// 数据 - 低字节
                //    }
                //}

                //byte[] byDataToBeSent = PackWriteMultiRegisterCmd(DeviceAddress, byData, SetValue.Length * 4);

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过Socket发送指令数据给客户端
                //Client.Send(byDataToBeSent);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (Client.Available < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                //    {
                //        Unlock();
                //        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                //        //break;
                //    }

                //    //Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = new byte[Client.Available];
                //Client.Receive(byReadData);

                //Unlock();

                //string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);

                //if (bSaveReceivedStringToLog == true)
                //{
                //    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                //    {
                //        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                //    }
                //}

                //sFeedBackFromSlave = null;

                #endregion

                #region "以字节方式处理从站的返回结果"

                if (null == byReadData || byReadData.Length < iMinLengthOfResponse)
                {
                    return false;
                }
                else
                {
                    #region "通讯记录"



                    #endregion

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
                    //MessageBox.Show("Modbus Socket通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
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
                //清除发送前的接收缓冲区
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

                        //当通讯停止后，超过一定时间就发送心跳信息给服务器
                        if (bIsBusyBackup != bIsBusy)
                        {
                            bIsBusyBackup = bIsBusy;

                            //if (bIsBusyBackup == false)
                            //{
                            swUsedUnbusyTime.Restart();
                            //}
                            //else
                            //{

                            //}
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
                                        //if (Client.Available > 2)
                                        //{

                                        //}
                                        //Client.Send(byHeartBeatMsg);
                                        //byte[] byTemp = new byte[2];
                                        //Client.Receive(byTemp);
                                        //if (Client.Poll(10, SelectMode.SelectRead) == true && Client.Available < 1)//Client.Poll(10, SelectMode.SelectWrite) == true
                                        //{

                                        //}
                                        //else
                                        //{
                                        //    bIsConnected = false;
                                        //}
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
                        //判断是否已经断开连接
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
                            //Client.Disconnect(true);//
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

                        //在 getsockopt 或 setsockopt 调用中指定的一个未知的、无效的或不受支持的选项或层次。
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
                                //Client.Disconnect(true);//
                                //Client.Shutdown(SocketShutdown.Both);
                                //Client.Close();
                            }
                            catch (Exception)
                            {
                                //由于套接字没有连接并且(当使用一个 sendto 调用发送数据报套接字时)没有提供地址，发送或接收数据的请求没有被接受。
                            }
                        }
                        else
                        {
                            if (bReadSlaveDataOnly == true)
                            {
                                try
                                {
                                    if(Client.Available > 2)
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
                    //由于目标计算机积极拒绝，无法连接。 127.0.0.1:502

                    //********************
                    //Client.Disconnect(false);
                    //断开套接字连接后，只能通过异步方式再次重新连接，而且只能连接到不同的 EndPoint。  在操作完成前，在将不会退出的线程上必须调用 BeginConnect。

                    //由于套接字没有连接并且(当使用一个 sendto 调用发送数据报套接字时)没有提供地址，发送或接收数据的请求没有被接受
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

                //throw new NotImplementedException();
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
                    qErrorMsg.Enqueue("(" + ServerIPAddress + ":" + iServerPort.ToString() + ")" + Msg);//发生错误
                }
                else
                {
                    qErrorMsg.Dequeue();
                    qErrorMsg.Enqueue("(" + ServerIPAddress + ":" + iServerPort.ToString() + ")" + Msg);//发生错误
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
                        //Application.DoEvents();
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
            //原始字符串、原始字节数组、从站地址、功能码、数据字符串/字节数组、日期和时间

            CReadbackData gotData = new CReadbackData();
            gotData.ReceivedDateTime = DateTime.Now;//UtcNow  日期和时间

            try
            {
                gotData.ReceivedBytes = ReceivedMsg;//原始字节数组

                //将字节数组转化为16进制字符串
                string sFeedBackFromSlave = CConverter.Bytes1To2HexStr(ReceivedMsg);
                gotData.ReceivedString = sFeedBackFromSlave;//原始字符串

                if (bSaveReceivedStringToLog == true)
                {
                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
                    {
                        Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
                    }
                }

                gotData.SlaveAddress = ReceivedMsg[PosIndexOfSlaveAddressInSocketReceivedBytes];//从站地址

                gotData.FuncCode = ReceivedMsg[PosIndexOfFuncCodeInSocketReceivedBytes];//功能码
                gotData.FuncDescription = CModbusFuncCode.FuncInfo((ModbusFuncCode)gotData.FuncCode);//功能码的描述信息

                gotData.ErrorCode = ReceivedMsg[PosIndexOfErrorCodeInSocketReceivedBytes];//错误码
                gotData.ErrorMsg = AnalysisErrorCode(ReceivedMsg);//错误码的描述信息

                //数据字符串/字节数组
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
                //string sTemp = "从站[" + Msg.SlaveAddress.ToString() + "], 功能码[" + Msg.FuncCode.ToString() + "](" + Msg.FuncDescription + "), 错误码[" + Msg.ErrorCode.ToString() + "], 错误信息[" + Msg.ErrorMsg + "]";

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
            //, string sCmdDataToBeSent   <param name="sCmdDataToBeSent">要发送的数据字符串，用于保存至日志</param>
            byte[] byReadData = null;

            try
            {
                if (bReadSlaveDataOnly == true || (Client.ProtocolType == ProtocolType.Tcp && bIsConnected == false))
                {
                    return null;
                }

                Lock();

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过Socket发送指令数据给客户端
                Client.Send(byResult);

                bIsBusy = true;

                if (bSaveSendStringToLog == true)
                {
                    string sTemp = CConverter.Bytes1To2HexStr(byResult);
                    Enqueue("发送字节转换为16进制 - " + sTemp);
                }

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (Client.Available < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        bIsConnected = false;
                        swUsedTime = null;
                        //Unlock();
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    //Application.DoEvents();
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
            //写数据的长度(单位：位、字节、字、双字、四字)
            if (null == Data || Data.Length < 1)//Data.Length < 2
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

            #region "Old codes"

            ////写多个保持寄存器格式：
            ////2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  2个字节(序号：10~11)  1个字节(序号：12)  N个字节(序号：13~(N-1))
            ////ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  数据位数量            字节数量           写数据
            ////ushort				      一般为0										     			              ushort
            ////高低字节交换			        	        								                              高低字节交换

            //byte[] byResultData = byResultData = new byte[13 + Data.Length];//固定字节长度 13 加上发送字节数据长度

            ////*********************
            ////消息ID
            //byte[] byMsgID = CConverter.ToBytes(stMsgIDForReading, ParaBytesFormat);
            //byResultData[0] = byMsgID[0];
            //byResultData[1] = byMsgID[1];

            ////byte[] byMsgID = BitConverter.GetBytes(stMsgIDForWriting);
            ////byResultData[0] = byMsgID[1];
            ////byResultData[1] = byMsgID[0];

            ////*********************
            ////协议标识
            //byte[] byProtocolID = CConverter.Reorder2BytesData(ProtocolIDCodeBytes, ParaBytesFormat, 0);
            //byResultData[2] = byProtocolID[0]; //byProtocolID[1];
            //byResultData[3] = byProtocolID[1]; //byProtocolID[0];

            //////byte[] byProtocolID = BitConverter.GetBytes(0);
            ////byResultData[2] = ProtocolIDCodeBytes[1]; //byProtocolID[1];
            ////byResultData[3] = ProtocolIDCodeBytes[0]; //byProtocolID[0];

            ////*********************
            ////命令字节的长度
            //byte[] byCmdBytesLength = CConverter.ToBytes((short)(7 + Data.Length), ParaBytesFormat);
            //byResultData[4] = byCmdBytesLength[0];
            //byResultData[5] = byCmdBytesLength[1];

            ////byte[] byCmdBytesLength = BitConverter.GetBytes(7 + DataToBeSent.Length);//固定字节长度 7 加上发送字节数据长度
            ////byResultData[4] = byCmdBytesLength[1];
            ////byResultData[5] = byCmdBytesLength[0];

            ////*********************
            ////读取的从站编号
            //byResultData[6] = DeviceAddress;

            ////*********************
            ////功能码
            //byResultData[7] = (byte)FuncCode;// CModbusFunctionCode.WriteMultiRegister;

            ////*********************
            ////起始地址
            //byte[] byBeginAddresss = CConverter.ToBytes((short)BeginAddress, ParaBytesFormat);
            //byResultData[8] = byBeginAddresss[0];
            //byResultData[9] = byBeginAddresss[1];

            ////*********************
            ////数据长度 - 功能码的数据长度计数
            //byte[] byBitDataLength = CConverter.ToBytes((short)DataLength, ParaBytesFormat);
            //byResultData[10] = byBitDataLength[0];
            //byResultData[11] = byBitDataLength[1];

            ////byte[] byBitDataLength = BitConverter.GetBytes((short)DataLengthToBeWrote);
            ////byResultData[10] = byBitDataLength[1];
            ////byResultData[11] = byBitDataLength[0];

            ////*********************
            ////数据长度 - 字节计数
            //byte[] byByteDataLength = BitConverter.GetBytes((short)Data.Length);
            //byResultData[12] = byByteDataLength[0];
            
            ////*********************
            ////发送数据
            //for (int i = 0; i < Data.Length; i++)
            //{
            //    byResultData[13 + i] = Data[i];
            //}

            //return byResultData;

            #endregion
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
            //Rx:00 08 00 00 00 06 01 06 00 00 FD 66
            //Tx:00 08 00 00 00 06 01 06 00 00 FD 66

            //Rx:00 09 00 00 00 06 01 06 00 00 00 2C
            //Tx:00 09 00 00 00 06 01 06 00 00 00 2C

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

            #region "Old codes"

            ////写单个保持寄存器格式：
            ////2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  N个字节(序号：10~(N-1))
            ////ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  写数据
            ////ushort				      一般为0										     			              ushort
            ////高低字节交换			        	        								                              高低字节交换

            //byte[] byResultData = byResultData = new byte[10 + Data.Length];//固定字节长度 10 加上发送字节数据长度

            ////*******************
            ////消息ID
            //byte[] byMsgID = CConverter.ToBytes(stMsgIDForReading, ParaBytesFormat);
            //byResultData[0] = byMsgID[0];
            //byResultData[1] = byMsgID[1];

            ////byte[] byMsgID = BitConverter.GetBytes(stMsgIDForWriting);
            ////byResultData[0] = byMsgID[1];
            ////byResultData[1] = byMsgID[0];

            ////*******************
            ////协议标识
            //byte[] byProtocolID = CConverter.Reorder2BytesData(ProtocolIDCodeBytes, ParaBytesFormat, 0);
            //byResultData[2] = byProtocolID[0]; //byProtocolID[1];
            //byResultData[3] = byProtocolID[1]; //byProtocolID[0];

            //////byte[] byProtocolID = BitConverter.GetBytes(0);
            ////byResultData[2] = ProtocolIDCodeBytes[1]; //byProtocolID[1];
            ////byResultData[3] = ProtocolIDCodeBytes[0]; //byProtocolID[0];

            ////*******************
            ////命令字节的长度
            //byte[] byCmdBytesLength = CConverter.ToBytes(4 + Data.Length, ParaBytesFormat);
            //byResultData[4] = byCmdBytesLength[0];
            //byResultData[5] = byCmdBytesLength[1];

            ////byte[] byCmdBytesLength = BitConverter.GetBytes(4 + DataToBeSent.Length);//固定字节长度 7 加上发送字节数据长度
            ////byResultData[4] = byCmdBytesLength[1];
            ////byResultData[5] = byCmdBytesLength[0];

            ////*******************
            ////读取的从站编号
            //byResultData[6] = DeviceAddress;

            ////*******************
            ////功能码
            //byResultData[7] = (byte)FuncCode;

            ////*******************
            ////起始地址
            //byte[] byBeginAddresss = CConverter.ToBytes((short)BeginAddress, ParaBytesFormat);
            //byResultData[8] = byBeginAddresss[0];
            //byResultData[9] = byBeginAddresss[1];
 
            ////*******************           
            ////发送数据
            //for (int i = 0; i < 2; i++)//DataToBeSent.Length
            //{
            //    byResultData[10 + i] = Data[i];
            //}

            //return byResultData;

            #endregion
        }

        /// <summary>
        /// 创建读取命令的字节数组，可以直接发送这个字节数组到Socket端口
        /// </summary>
        /// <param name="DeviceAddress">从站地址</param>
        /// <param name="FuncCode">功能码</param>
        /// <param name="BeginAddress">起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，有效值范围：1~2000(位)</param>
        /// <returns></returns>
        private byte[] PackReadCmd(byte DeviceAddress, ModbusFuncCode FuncCode, ushort BeginAddress, int ReadDataLength)//ushort
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

            //读取单个线圈的通信记录：
            //读取结果 - OFF
            //Tx:00 15 00 00 00 06 01 01 00 00 00 01
            //Rx:00 15 00 00 00 04 01 01 01 00

            //读取结果 - ON
            //Tx:00 16 00 00 00 06 01 01 00 00 00 01
            //Rx:00 16 00 00 00 04 01 01 01 01

            //读取格式：
            //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
            //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
            //ushort				        一般为0										     			                            ushort
            //高低字节交换			        	        								                                            高低字节交换
            
            if (stMsgIDForReading >= short.MaxValue)
            {
                stMsgIDForReading = 0;
            }

            stMsgIDForReading++;

            return Converter.Modbus.ModbusSocket.PackReadCmd(DeviceAddress, FuncCode, BeginAddress, ReadDataLength, stMsgIDForReading, ProtocolIDCodeBytes, ParaBytesFormat);

            #region "Old codes"

            //byte[] byResultData = new byte[12];

            ////消息ID
            //byte[] byMsgID = CConverter.ToBytes(stMsgIDForReading, ParaBytesFormat);
            //byResultData[0] = byMsgID[0];
            //byResultData[1] = byMsgID[1];

            ////byte[] byMsgID =  BitConverter.GetBytes(stMsgIDForReading);
            ////byResultData[0] = byMsgID[1];
            ////byResultData[1] = byMsgID[0];

            ////*******************
            ////协议标识
            //byte[] byProtocolID = CConverter.Reorder2BytesData(ProtocolIDCodeBytes, ParaBytesFormat, 0);
            //byResultData[2] = byProtocolID[0]; //byProtocolID[1];
            //byResultData[3] = byProtocolID[1]; //byProtocolID[0];

            //////byte[] byProtocolID = BitConverter.GetBytes(0);
            ////byResultData[2] = ProtocolIDCodeBytes[1]; //byProtocolID[1];
            ////byResultData[3] = ProtocolIDCodeBytes[0]; //byProtocolID[0];

            ////*******************
            ////命令字节的长度
            //byte[] byCmdBytesLength = CConverter.ToBytes((ushort)0x6, ParaBytesFormat);
            //byResultData[4] = byCmdBytesLength[0];
            //byResultData[5] = byCmdBytesLength[1];

            ////byte[] byCmdBytesLength = BitConverter.GetBytes(6);
            ////byResultData[4] = byCmdBytesLength[1];
            ////byResultData[5] = byCmdBytesLength[0];

            ////*******************
            ////读取的从站编号
            //byResultData[6] = DeviceAddress;

            ////*******************
            ////功能码
            //byResultData[7] = (byte)FuncCode;

            ////*******************
            ////起始地址
            //byte[] byBeginAddresss = CConverter.ToBytes((short)BeginAddress, ParaBytesFormat);
            //byResultData[8] = byBeginAddresss[0];
            //byResultData[9] = byBeginAddresss[1];
            
            ////*******************
            ////读取数据长度
            //byte[] byReadDataLength = CConverter.ToBytes((short)ReadDataLength, ParaBytesFormat);
            //byResultData[10] = byReadDataLength[0];
            //byResultData[11] = byReadDataLength[1];

            ////byte[] byReadDataLength = BitConverter.GetBytes((short)ReadDataLength);
            ////byResultData[10] = byReadDataLength[1];
            ////byResultData[11] = byReadDataLength[0];

            //return byResultData;

            #endregion
        }

        #region "Codes"

        ///// <summary>
        ///// 创建写保持寄存器命令的字节数组，可以直接发送这个字节数组到Socket端口
        ///// </summary>
        ///// <param name="DeviceAddress">从站地址</param>
        ///// <param name="FuncCode">功能码</param>
        ///// <param name="BeginAddress">起始地址</param>
        ///// <param name="Data">发送的数据(字节)</param>
        ///// <param name="DataLength">要发送的数据的数量：线圈 -- 位(bool)；寄存器 -- 字(short)</param>
        ///// <param name="MsgID">消息ID</param>
        ///// <param name="ProtocolIDCodeBytes">协议标识</param>
        ///// <param name="ParaBytesFormat">参数的字节编码格式</param>
        ///// <returns></returns>
        //public static byte[] PackMultiWriteCmd(byte DeviceAddress, ModbusFuncCode FuncCode, ushort BeginAddress, int DataLength, byte[] Data, short MsgID, byte[] ProtocolIDCodeBytes, BytesFormat ParaBytesFormat)
        //{
        //    //写数据的长度(单位：位、字节、字、双字、四字)
        //    if (null == Data || Data.Length < 1)//Data.Length < 2
        //    {
        //        return null;
        //    }

        //    if (FuncCode != ModbusFuncCode.WriteMultiCoil && FuncCode != ModbusFuncCode.WriteMultiRegister)
        //    {
        //        return null;
        //    }

        //    //if (MsgID >= short.MaxValue)
        //    //{
        //    //    MsgID = 0;
        //    //}

        //    //MsgID++;

        //    //写多个保持寄存器格式：
        //    //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  2个字节(序号：10~11)  1个字节(序号：12)  N个字节(序号：13~(N-1))
        //    //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  数据位数量            字节数量           写数据
        //    //ushort				      一般为0										     			              ushort
        //    //高低字节交换			        	        								                              高低字节交换

        //    byte[] byResultData = byResultData = new byte[13 + Data.Length];//固定字节长度 13 加上发送字节数据长度

        //    //*********************
        //    //消息ID
        //    byte[] byMsgID = CConverter.ToBytes(MsgID, ParaBytesFormat);
        //    byResultData[0] = byMsgID[0];
        //    byResultData[1] = byMsgID[1];

        //    //byte[] byMsgID = BitConverter.GetBytes(stMsgIDForWriting);
        //    //byResultData[0] = byMsgID[1];
        //    //byResultData[1] = byMsgID[0];

        //    //*********************
        //    //协议标识
        //    if (ProtocolIDCodeBytes == null || ProtocolIDCodeBytes.Length < 2)
        //    {
        //        ProtocolIDCodeBytes = new byte[] { 0x0, 0x0 };
        //    }
        //    byte[] byProtocolID = CConverter.Reorder2BytesData(ProtocolIDCodeBytes, ParaBytesFormat, 0);
        //    byResultData[2] = byProtocolID[0]; //byProtocolID[1];
        //    byResultData[3] = byProtocolID[1]; //byProtocolID[0];

        //    ////byte[] byProtocolID = BitConverter.GetBytes(0);
        //    //byResultData[2] = ProtocolIDCodeBytes[1]; //byProtocolID[1];
        //    //byResultData[3] = ProtocolIDCodeBytes[0]; //byProtocolID[0];

        //    //*********************
        //    //命令字节的长度
        //    byte[] byCmdBytesLength = CConverter.ToBytes((short)(7 + Data.Length), ParaBytesFormat);
        //    byResultData[4] = byCmdBytesLength[0];
        //    byResultData[5] = byCmdBytesLength[1];

        //    //byte[] byCmdBytesLength = BitConverter.GetBytes(7 + DataToBeSent.Length);//固定字节长度 7 加上发送字节数据长度
        //    //byResultData[4] = byCmdBytesLength[1];
        //    //byResultData[5] = byCmdBytesLength[0];

        //    //*********************
        //    //读取的从站编号
        //    byResultData[6] = DeviceAddress;

        //    //*********************
        //    //功能码
        //    byResultData[7] = (byte)FuncCode;// CModbusFunctionCode.WriteMultiRegister;

        //    //*********************
        //    //起始地址
        //    byte[] byBeginAddresss = CConverter.ToBytes((short)BeginAddress, ParaBytesFormat);
        //    byResultData[8] = byBeginAddresss[0];
        //    byResultData[9] = byBeginAddresss[1];

        //    //*********************
        //    //数据长度 - 功能码的数据长度计数
        //    byte[] byBitDataLength = CConverter.ToBytes((short)DataLength, ParaBytesFormat);
        //    byResultData[10] = byBitDataLength[0];
        //    byResultData[11] = byBitDataLength[1];

        //    //byte[] byBitDataLength = BitConverter.GetBytes((short)DataLengthToBeWrote);
        //    //byResultData[10] = byBitDataLength[1];
        //    //byResultData[11] = byBitDataLength[0];

        //    //*********************
        //    //数据长度 - 字节计数
        //    byte[] byByteDataLength = BitConverter.GetBytes((short)Data.Length);
        //    byResultData[12] = byByteDataLength[0];

        //    //*********************
        //    //发送数据
        //    for (int i = 0; i < Data.Length; i++)
        //    {
        //        byResultData[13 + i] = Data[i];
        //    }

        //    return byResultData;
        //}

        ///// <summary>
        ///// 创建写保持寄存器命令的字节数组，可以直接发送这个字节数组到Socket端口
        ///// </summary>
        ///// <param name="DeviceAddress">从站地址</param>
        ///// <param name="FuncCode">功能码</param>
        ///// <param name="BeginAddress">起始地址</param>
        ///// <param name="Data">发送的数据(字节)</param>
        ///// <param name="MsgID">消息ID</param>
        ///// <param name="ProtocolIDCodeBytes">协议标识</param>
        ///// <param name="ParaBytesFormat">参数的字节编码格式</param>
        ///// <returns></returns>
        //public static byte[] PackSingleWriteCmd(byte DeviceAddress, ModbusFuncCode FuncCode, ushort BeginAddress, byte[] Data, short MsgID, byte[] ProtocolIDCodeBytes, BytesFormat ParaBytesFormat)
        //{
        //    //Rx:00 08 00 00 00 06 01 06 00 00 FD 66
        //    //Tx:00 08 00 00 00 06 01 06 00 00 FD 66

        //    //Rx:00 09 00 00 00 06 01 06 00 00 00 2C
        //    //Tx:00 09 00 00 00 06 01 06 00 00 00 2C

        //    if (null == Data || Data.Length != 2)
        //    {
        //        return null;
        //    }

        //    if (FuncCode != ModbusFuncCode.WriteCoil && FuncCode != ModbusFuncCode.WriteRegister)
        //    {
        //        return null;
        //    }

        //    //if (MsgID >= short.MaxValue)
        //    //{
        //    //    MsgID = 0;
        //    //}

        //    //MsgID++;

        //    //写单个保持寄存器格式：
        //    //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  N个字节(序号：10~(N-1))
        //    //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  写数据
        //    //ushort				      一般为0										     			              ushort
        //    //高低字节交换			        	        								                              高低字节交换

        //    byte[] byResultData = byResultData = new byte[10 + Data.Length];//固定字节长度 10 加上发送字节数据长度

        //    //*******************
        //    //消息ID
        //    byte[] byMsgID = CConverter.ToBytes(MsgID, ParaBytesFormat);
        //    byResultData[0] = byMsgID[0];
        //    byResultData[1] = byMsgID[1];

        //    //byte[] byMsgID = BitConverter.GetBytes(stMsgIDForWriting);
        //    //byResultData[0] = byMsgID[1];
        //    //byResultData[1] = byMsgID[0];

        //    //*******************
        //    //协议标识
        //    if (ProtocolIDCodeBytes == null || ProtocolIDCodeBytes.Length < 2)
        //    {
        //        ProtocolIDCodeBytes = new byte[] { 0x0, 0x0 };
        //    }
        //    byte[] byProtocolID = CConverter.Reorder2BytesData(ProtocolIDCodeBytes, ParaBytesFormat, 0);
        //    byResultData[2] = byProtocolID[0]; //byProtocolID[1];
        //    byResultData[3] = byProtocolID[1]; //byProtocolID[0];

        //    ////byte[] byProtocolID = BitConverter.GetBytes(0);
        //    //byResultData[2] = ProtocolIDCodeBytes[1]; //byProtocolID[1];
        //    //byResultData[3] = ProtocolIDCodeBytes[0]; //byProtocolID[0];

        //    //*******************
        //    //命令字节的长度
        //    byte[] byCmdBytesLength = CConverter.ToBytes(4 + Data.Length, ParaBytesFormat);
        //    byResultData[4] = byCmdBytesLength[0];
        //    byResultData[5] = byCmdBytesLength[1];

        //    //byte[] byCmdBytesLength = BitConverter.GetBytes(4 + DataToBeSent.Length);//固定字节长度 7 加上发送字节数据长度
        //    //byResultData[4] = byCmdBytesLength[1];
        //    //byResultData[5] = byCmdBytesLength[0];

        //    //*******************
        //    //读取的从站编号
        //    byResultData[6] = DeviceAddress;

        //    //*******************
        //    //功能码
        //    byResultData[7] = (byte)FuncCode;

        //    //*******************
        //    //起始地址
        //    byte[] byBeginAddresss = CConverter.ToBytes((short)BeginAddress, ParaBytesFormat);
        //    byResultData[8] = byBeginAddresss[0];
        //    byResultData[9] = byBeginAddresss[1];

        //    //*******************           
        //    //发送数据
        //    for (int i = 0; i < 2; i++)//DataToBeSent.Length
        //    {
        //        byResultData[10 + i] = Data[i];
        //    }

        //    return byResultData;
        //}

        ///// <summary>
        ///// 创建读取命令的字节数组，可以直接发送这个字节数组到Socket端口
        ///// </summary>
        ///// <param name="DeviceAddress">从站地址</param>
        ///// <param name="FuncCode">功能码</param>
        ///// <param name="BeginAddress">起始地址</param>
        ///// <param name="ReadDataLength">读取数据长度，有效值范围：1~2000(位)</param>
        ///// <param name="MsgID">消息ID</param>
        ///// <param name="ProtocolIDCodeBytes">协议标识</param>
        ///// <param name="ParaBytesFormat">参数的字节编码格式</param>
        ///// <returns></returns>
        //public static byte[] PackReadCmd(byte DeviceAddress, ModbusFuncCode FuncCode, ushort BeginAddress, int ReadDataLength, short MsgID, byte[] ProtocolIDCodeBytes, BytesFormat ParaBytesFormat)//ushort
        //{
        //    if (FuncCode != ModbusFuncCode.ReadCoil && FuncCode != ModbusFuncCode.ReadInputRegister
        //        && FuncCode != ModbusFuncCode.ReadInputSignal && FuncCode != ModbusFuncCode.ReadRegister)
        //    {
        //        return null;
        //    }

        //    if (ReadDataLength < 1)
        //    {
        //        ReadDataLength = 1;
        //    }

        //    //读取单个线圈的通信记录：
        //    //读取结果 - OFF
        //    //Tx:00 15 00 00 00 06 01 01 00 00 00 01
        //    //Rx:00 15 00 00 00 04 01 01 01 00

        //    //读取结果 - ON
        //    //Tx:00 16 00 00 00 06 01 01 00 00 00 01
        //    //Rx:00 16 00 00 00 04 01 01 01 01

        //    //读取格式：
        //    //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
        //    //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
        //    //ushort				        一般为0										     			                            ushort
        //    //高低字节交换			        	        								                                            高低字节交换

        //    //if (MsgID >= short.MaxValue)
        //    //{
        //    //    MsgID = 0;
        //    //}

        //    //MsgID++;

        //    byte[] byResultData = new byte[12];

        //    //消息ID
        //    byte[] byMsgID = CConverter.ToBytes(MsgID, ParaBytesFormat);
        //    byResultData[0] = byMsgID[0];
        //    byResultData[1] = byMsgID[1];

        //    //byte[] byMsgID =  BitConverter.GetBytes(stMsgIDForReading);
        //    //byResultData[0] = byMsgID[1];
        //    //byResultData[1] = byMsgID[0];

        //    //*******************
        //    //协议标识
        //    if (ProtocolIDCodeBytes == null || ProtocolIDCodeBytes.Length < 2)
        //    {
        //        ProtocolIDCodeBytes = new byte[] { 0x0, 0x0 };
        //    }
        //    byte[] byProtocolID = CConverter.Reorder2BytesData(ProtocolIDCodeBytes, ParaBytesFormat, 0);
        //    byResultData[2] = byProtocolID[0]; //byProtocolID[1];
        //    byResultData[3] = byProtocolID[1]; //byProtocolID[0];

        //    ////byte[] byProtocolID = BitConverter.GetBytes(0);
        //    //byResultData[2] = ProtocolIDCodeBytes[1]; //byProtocolID[1];
        //    //byResultData[3] = ProtocolIDCodeBytes[0]; //byProtocolID[0];

        //    //*******************
        //    //命令字节的长度
        //    byte[] byCmdBytesLength = CConverter.ToBytes((ushort)0x6, ParaBytesFormat);
        //    byResultData[4] = byCmdBytesLength[0];
        //    byResultData[5] = byCmdBytesLength[1];

        //    //byte[] byCmdBytesLength = BitConverter.GetBytes(6);
        //    //byResultData[4] = byCmdBytesLength[1];
        //    //byResultData[5] = byCmdBytesLength[0];

        //    //*******************
        //    //读取的从站编号
        //    byResultData[6] = DeviceAddress;

        //    //*******************
        //    //功能码
        //    byResultData[7] = (byte)FuncCode;

        //    //*******************
        //    //起始地址
        //    byte[] byBeginAddresss = CConverter.ToBytes((short)BeginAddress, ParaBytesFormat);
        //    byResultData[8] = byBeginAddresss[0];
        //    byResultData[9] = byBeginAddresss[1];

        //    //*******************
        //    //读取数据长度
        //    byte[] byReadDataLength = CConverter.ToBytes((short)ReadDataLength, ParaBytesFormat);
        //    byResultData[10] = byReadDataLength[0];
        //    byResultData[11] = byReadDataLength[1];

        //    //byte[] byReadDataLength = BitConverter.GetBytes((short)ReadDataLength);
        //    //byResultData[10] = byReadDataLength[1];
        //    //byResultData[11] = byReadDataLength[0];

        //    return byResultData;
        //}

        #endregion

        #region "Old codes -- No use"

        ///// <summary>
        ///// 创建写多个线圈命令的字节数组，可以直接发送这个字节数组到Socket端口
        ///// </summary>
        ///// <param name="DeviceAddress">从站地址</param>
        ///// <param name="DataToBeSent">发送的数据(字节)</param>
        ///// <param name="DataLengthToBeWrote">写数据的长度(单位：位、字节、字、双字、四字)</param>
        ///// <returns></returns>
        //private byte[] PackMultiWriteCoilCmd(byte DeviceAddress, ModbusFuncCode FuncCode, ushort BeginAddress, byte[] DataToBeSent, int DataLengthToBeWrote)
        //{
        //    //byte WriteFunctionCode, <param name="WriteFunctionCode">写功能码</param> , bool MultiData = true)//ushort  <param name="MultiData">是否为写多个数据</param>
        //    //Rx:00 06 00 00 00 06 01 05 00 00 00 00
        //    //Tx:00 06 00 00 00 06 01 05 00 00 00 00

        //    //Rx:00 07 00 00 00 06 01 05 00 00 FF 00
        //    //Tx:00 07 00 00 00 06 01 05 00 00 FF 00

        //    //Rx:00 08 00 00 00 08 01 0F 00 00 00 01 01 01
        //    //Tx:00 08 00 00 00 06 01 0F 00 00 00 01

        //    //Rx:00 09 00 00 00 08 01 0F 00 00 00 01 01 00
        //    //Tx:00 09 00 00 00 06 01 0F 00 00 00 01

        //    //写错误
        //    //Tx:00 0B 00 00 00 06 01 03 00 00 00 0A
        //    //Rx:00 0B 00 00 00 03 01 83 01

        //    //写单个线圈的通信记录：
        //    //Tx:00 25 00 00 00 06 01 05 00 00 FF 00
        //    //Rx:00 25 00 00 00 06 01 05 00 00 FF 00
        //    //Tx:00 26 00 00 00 06 01 05 00 00 00 00
        //    //Rx:00 26 00 00 00 06 01 05 00 00 00 00

        //    if (null == DataToBeSent)// || DataToBeSent.Length < 2)
        //    {
        //        return null;
        //    }

        //    if (stMsgIDForWriting >= short.MaxValue)
        //    {
        //        stMsgIDForWriting = 0;
        //    }

        //    stMsgIDForWriting++;

        //    byte[] byResultData = null;

        //    //Rx:00 10 00 00 00 08 01 0F 00 00 00 08 01 2F
        //    //Tx:00 10 00 00 00 06 01 0F 00 00 00 08

        //    //写多个线圈格式：
        //    //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  2个字节(序号：10~11)  1个字节(序号：12)  N个字节(序号：13~(N-1))
        //    //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  数据位数量            字节数量           写数据
        //    //ushort				      一般为0										     			              ushort
        //    //高低字节交换			        	        								                              高低字节交换

        //    byResultData = new byte[10 + 2 + 1 + DataToBeSent.Length];//固定字节长度 10 加上发送字节数据长度

        //    //*********************
        //    //消息ID
        //    byte[] byMsgID = CConverter.ToBytes(stMsgIDForReading, ParaBytesFormat);
        //    byResultData[0] = byMsgID[0];
        //    byResultData[1] = byMsgID[1];

        //    //byte[] byMsgID = BitConverter.GetBytes(stMsgIDForWriting);
        //    //byResultData[0] = byMsgID[1];
        //    //byResultData[1] = byMsgID[0];

        //    //*********************
        //    //协议标识
        //    byte[] byProtocolID = CConverter.Reorder2BytesData(ProtocolIDCodeBytes, ParaBytesFormat, 0);
        //    byResultData[2] = byProtocolID[0]; //byProtocolID[1];
        //    byResultData[3] = byProtocolID[1]; //byProtocolID[0];

        //    ////byte[] byProtocolID = BitConverter.GetBytes(0);
        //    //byResultData[2] = ProtocolIDCodeBytes[1]; //byProtocolID[1];
        //    //byResultData[3] = ProtocolIDCodeBytes[0]; //byProtocolID

        //    //*********************
        //    //命令字节的长度
        //    byte[] byCmdBytesLength = CConverter.ToBytes(7 + DataToBeSent.Length, ParaBytesFormat);
        //    byResultData[4] = byCmdBytesLength[0];
        //    byResultData[5] = byCmdBytesLength[1];

        //    //byte[] byCmdBytesLength = BitConverter.GetBytes(7 + DataToBeSent.Length);//固定字节长度 7 加上发送字节数据长度
        //    //byResultData[4] = byCmdBytesLength[1];
        //    //byResultData[5] = byCmdBytesLength[0];

        //    //*********************
        //    //读取的从站编号
        //    byResultData[6] = DeviceAddress;

        //    //*********************
        //    //功能码
        //    byResultData[7] = (byte)FuncCode;// CModbusFunctionCode.WriteMultiCoil;

        //    //*********************
        //    //起始地址
        //    byte[] byLocalAddresss = CConverter.ToBytes(BeginAddress, ParaBytesFormat);// new byte[] { byLocalModbusStationAddress, 0x0 };
        //    //byLocalAddresss = CConverter.Reorder2BytesData(byLocalAddresss, ParaBytesFormat, 0);
        //    byResultData[8] = byLocalAddresss[0];
        //    byResultData[9] = byLocalAddresss[1];

        //    //byResultData[8] = byLocalModbusStationAddress[0];
        //    //byResultData[9] = byLocalModbusStationAddress[1];

        //    //*********************
        //    //数据长度 - 功能码的数据长度计数
        //    byte[] byBitDataLength = CConverter.ToBytes((short)DataLengthToBeWrote, ParaBytesFormat);
        //    byResultData[10] = byBitDataLength[0];
        //    byResultData[11] = byBitDataLength[1];

        //    //byte[] byBitDataLength = BitConverter.GetBytes((short)DataLengthToBeWrote);
        //    //byResultData[10] = byBitDataLength[1];
        //    //byResultData[11] = byBitDataLength[0];

        //    //*********************
        //    //数据长度 - 字节计数
        //    byte[] byByteDataLength = BitConverter.GetBytes((short)DataToBeSent.Length);
        //    byResultData[12] = byByteDataLength[0];

        //    //*********************
        //    //发送数据
        //    for (int i = 0; i < DataToBeSent.Length; i++)
        //    {
        //        byResultData[13 + i] = DataToBeSent[i];
        //    }

        //    return byResultData;

        //    #region "Old codes"

        //    //if (MultiData == true)
        //    //{
        //    //    //Rx:00 10 00 00 00 08 01 0F 00 00 00 08 01 2F
        //    //    //Tx:00 10 00 00 00 06 01 0F 00 00 00 08

        //    //    //写格式：
        //    //    //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  2个字节(序号：10~11)  1个字节(序号：12)  N个字节(序号：13~(N-1))
        //    //    //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  数据位数量            字节数量           写数据
        //    //    //ushort				      一般为0										     			              ushort
        //    //    //高低字节交换			        	        								                              高低字节交换

        //    //    byResultData = new byte[10 + 2 + 1 + DataToBeSent.Length];//固定字节长度 10 加上发送字节数据长度

        //    //    //消息ID
        //    //    byte[] byMsgID = BitConverter.GetBytes(stMsgIDForWriting);
        //    //    byResultData[0] = byMsgID[1];
        //    //    byResultData[1] = byMsgID[0];

        //    //    //协议标识
        //    //    byte[] byProtocolID = BitConverter.GetBytes(0);
        //    //    byResultData[2] = byProtocolID[1];
        //    //    byResultData[3] = byProtocolID[0];

        //    //    //命令字节的长度
        //    //    byte[] byCmdBytesLength = BitConverter.GetBytes(7 + DataToBeSent.Length);//固定字节长度 7 加上发送字节数据长度
        //    //    byResultData[4] = byCmdBytesLength[1];
        //    //    byResultData[5] = byCmdBytesLength[0];

        //    //    //读取的从站编号
        //    //    byResultData[6] = DeviceAddress;

        //    //    //功能码
        //    //    byResultData[7] = WriteFunctionCode;

        //    //    //起始地址
        //    //    byResultData[8] = byLocalModbusStationAddress[0];
        //    //    byResultData[9] = byLocalModbusStationAddress[1];

        //    //    //数据长度 - 功能码的数据长度计数
        //    //    byte[] byBitDataLength = BitConverter.GetBytes((short)DataToBeSent.Length * 8);
        //    //    byResultData[10] = byBitDataLength[1];
        //    //    byResultData[11] = byBitDataLength[0];

        //    //    //数据长度 - 字节计数
        //    //    byte[] byByteDataLength = BitConverter.GetBytes((short)DataToBeSent.Length);
        //    //    byResultData[12] = byByteDataLength[0];

        //    //    //发送数据
        //    //    for (int i = 0; i < DataToBeSent.Length; i++)
        //    //    {
        //    //        byResultData[13 + i] = DataToBeSent[i];
        //    //    }
        //    //}
        //    //else
        //    //{
        //    //    //写格式：
        //    //    //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2N个字节(序号：10~N)
        //    //    //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  写数据
        //    //    //ushort				        一般为0										     			                            ushort
        //    //    //高低字节交换			        	        								                                            高低字节交换

        //    //    byResultData = new byte[10 + DataToBeSent.Length];//固定字节长度 10 加上发送字节数据长度

        //    //    //消息ID
        //    //    byte[] byMsgID = BitConverter.GetBytes(stMsgIDForWriting);
        //    //    byResultData[0] = byMsgID[1];
        //    //    byResultData[1] = byMsgID[0];

        //    //    //协议标识
        //    //    byte[] byProtocolID = BitConverter.GetBytes(0);
        //    //    byResultData[2] = byProtocolID[1];
        //    //    byResultData[3] = byProtocolID[0];

        //    //    //命令字节的长度
        //    //    byte[] byCmdBytesLength = BitConverter.GetBytes(4 + DataToBeSent.Length);//固定字节长度 4 加上发送字节数据长度
        //    //    byResultData[4] = byCmdBytesLength[1];
        //    //    byResultData[5] = byCmdBytesLength[0];

        //    //    //读取的从站编号
        //    //    byResultData[6] = DeviceAddress;

        //    //    //功能码
        //    //    byResultData[7] = WriteFunctionCode;

        //    //    //起始地址
        //    //    byResultData[8] = byLocalModbusStationAddress[0];
        //    //    byResultData[9] = byLocalModbusStationAddress[1];

        //    //    //发送数据
        //    //    for (int i = 0; i < DataToBeSent.Length; i++)
        //    //    {
        //    //        byResultData[10 + i] = DataToBeSent[i];
        //    //    }
        //    //}

        //    //return byResultData;

        //    #endregion
        //}

        ///// <summary>
        ///// 创建写保持寄存器命令的字节数组，可以直接发送这个字节数组到Socket端口
        ///// </summary>
        ///// <param name="DeviceAddress">从站地址</param>
        ///// <param name="DataToBeSent">发送的数据(字节)</param>
        ///// <returns></returns>
        //private byte[] PackWriteOneRegisterCmd(byte DeviceAddress, byte[] DataToBeSent)
        //{
        //    //Rx:00 08 00 00 00 06 01 06 00 00 FD 66
        //    //Tx:00 08 00 00 00 06 01 06 00 00 FD 66

        //    //Rx:00 09 00 00 00 06 01 06 00 00 00 2C
        //    //Tx:00 09 00 00 00 06 01 06 00 00 00 2C

        //    if (null == DataToBeSent || DataToBeSent.Length != 2)
        //    {
        //        return null;
        //    }

        //    if (stMsgIDForWriting >= short.MaxValue)
        //    {
        //        stMsgIDForWriting = 0;
        //    }

        //    stMsgIDForWriting++;

        //    //写单个保持寄存器格式：
        //    //2个字节(字节数组序号：0~1)  2个字节(序号：2~3)  2个字节(序号：4~5)  1个字节(序号：6)  1个字节(序号：7)  2个字节(序号：8~9)  N个字节(序号：10~(N-1))
        //    //ID						  协议标识            命令字节的长度	  读取的从站编号    功能码		      起始地址			  写数据
        //    //ushort				      一般为0										     			              ushort
        //    //高低字节交换			        	        								                              高低字节交换

        //    byte[] byResultData = byResultData = new byte[10 + DataToBeSent.Length];//固定字节长度 10 加上发送字节数据长度

        //    //*******************
        //    //消息ID
        //    byte[] byMsgID = CConverter.ToBytes(stMsgIDForReading, ParaBytesFormat);
        //    byResultData[0] = byMsgID[0];
        //    byResultData[1] = byMsgID[1];

        //    //byte[] byMsgID = BitConverter.GetBytes(stMsgIDForWriting);
        //    //byResultData[0] = byMsgID[1];
        //    //byResultData[1] = byMsgID[0];

        //    //*******************
        //    //协议标识
        //    byte[] byProtocolID = CConverter.Reorder2BytesData(ProtocolIDCodeBytes, ParaBytesFormat, 0);
        //    byResultData[2] = byProtocolID[0]; //byProtocolID[1];
        //    byResultData[3] = byProtocolID[1]; //byProtocolID[0];

        //    ////byte[] byProtocolID = BitConverter.GetBytes(0);
        //    //byResultData[2] = ProtocolIDCodeBytes[1]; //byProtocolID[1];
        //    //byResultData[3] = ProtocolIDCodeBytes[0]; //byProtocolID[0];

        //    //*******************
        //    //命令字节的长度
        //    byte[] byCmdBytesLength = CConverter.ToBytes(4 + DataToBeSent.Length, ParaBytesFormat);
        //    byResultData[4] = byCmdBytesLength[0];
        //    byResultData[5] = byCmdBytesLength[1];

        //    //byte[] byCmdBytesLength = BitConverter.GetBytes(4 + DataToBeSent.Length);//固定字节长度 7 加上发送字节数据长度
        //    //byResultData[4] = byCmdBytesLength[1];
        //    //byResultData[5] = byCmdBytesLength[0];

        //    //*******************
        //    //读取的从站编号
        //    byResultData[6] = DeviceAddress;

        //    //*******************
        //    //功能码
        //    byResultData[7] = CModbusFunctionCode.WriteRegister;

        //    //*******************
        //    //起始地址
        //    byte[] byBeginAddresss = CConverter.ToBytes(BeginAddress, ParaBytesFormat);
        //    byResultData[8] = byBeginAddresss[0];
        //    byResultData[9] = byBeginAddresss[1];

        //    //byResultData[8] = byLocalModbusStationAddress[0];
        //    //byResultData[9] = byLocalModbusStationAddress[1];

        //    //*******************           
        //    //发送数据
        //    for (int i = 0; i < 2; i++)//DataToBeSent.Length
        //    {
        //        byResultData[10 + i] = DataToBeSent[i];
        //    }

        //    return byResultData;
        //}

        ///// <summary>
        ///// 创建读取命令的字节数组，可以直接发送这个字节数组到Socket端口
        ///// </summary>
        ///// <param name="DeviceAddress">从站地址</param>
        ///// <param name="ReadFunctionCode">读取功能码</param>
        ///// <param name="ReadDataLength">读取数据长度，有效值范围：1~2000(位)</param>
        ///// <returns></returns>
        //private byte[] PackReadCmd(byte DeviceAddress, byte ReadFunctionCode, int ReadDataLength)//ushort
        //{
        //    //读取单个线圈的通信记录：
        //    //读取结果 - OFF
        //    //Tx:00 15 00 00 00 06 01 01 00 00 00 01
        //    //Rx:00 15 00 00 00 04 01 01 01 00

        //    //读取结果 - ON
        //    //Tx:00 16 00 00 00 06 01 01 00 00 00 01
        //    //Rx:00 16 00 00 00 04 01 01 01 01

        //    //读取格式：
        //    //2个字节(字节数组序号：0~1)	2个字节(序号：2~3)	  2个字节(序号：4~5)	1个字节(序号：6)        1个字节(序号：7)    2个字节(序号：8~9)	  2个字节(序号：10~11)
        //    //ID							协议标识              命令字节的长度		读取的从站编号		    功能码		        起始地址			  读取数据长度
        //    //ushort				        一般为0										     			                            ushort
        //    //高低字节交换			        	        								                                            高低字节交换

        //    if (stMsgIDForReading >= short.MaxValue)
        //    {
        //        stMsgIDForReading = 0;
        //    }

        //    stMsgIDForReading++;

        //    byte[] byResultData = new byte[12];

        //    //消息ID
        //    byte[] byMsgID = CConverter.ToBytes(stMsgIDForReading, ParaBytesFormat);
        //    byResultData[0] = byMsgID[0];
        //    byResultData[1] = byMsgID[1];

        //    //byte[] byMsgID =  BitConverter.GetBytes(stMsgIDForReading);
        //    //byResultData[0] = byMsgID[1];
        //    //byResultData[1] = byMsgID[0];

        //    //*******************
        //    //协议标识
        //    byte[] byProtocolID = CConverter.Reorder2BytesData(ProtocolIDCodeBytes, ParaBytesFormat, 0);
        //    byResultData[2] = byProtocolID[0]; //byProtocolID[1];
        //    byResultData[3] = byProtocolID[1]; //byProtocolID[0];

        //    ////byte[] byProtocolID = BitConverter.GetBytes(0);
        //    //byResultData[2] = ProtocolIDCodeBytes[1]; //byProtocolID[1];
        //    //byResultData[3] = ProtocolIDCodeBytes[0]; //byProtocolID[0];

        //    //*******************
        //    //命令字节的长度
        //    byte[] byCmdBytesLength = CConverter.ToBytes(6, ParaBytesFormat);
        //    byResultData[4] = byCmdBytesLength[0];
        //    byResultData[5] = byCmdBytesLength[1];

        //    //byte[] byCmdBytesLength = BitConverter.GetBytes(6);
        //    //byResultData[4] = byCmdBytesLength[1];
        //    //byResultData[5] = byCmdBytesLength[0];

        //    //*******************
        //    //读取的从站编号
        //    byResultData[6] = DeviceAddress;

        //    //*******************
        //    //功能码
        //    byResultData[7] = ReadFunctionCode;

        //    //*******************
        //    //起始地址
        //    byte[] byBeginAddresss = CConverter.ToBytes(BeginAddress, ParaBytesFormat);
        //    byResultData[8] = byBeginAddresss[0];
        //    byResultData[9] = byBeginAddresss[1];

        //    //byResultData[8] = byLocalModbusStationAddress[0];
        //    //byResultData[9] = byLocalModbusStationAddress[1];

        //    //*******************
        //    //读取数据长度
        //    byte[] byReadDataLength = CConverter.ToBytes((short)ReadDataLength, ParaBytesFormat);
        //    byResultData[10] = byReadDataLength[1];
        //    byResultData[11] = byReadDataLength[0];

        //    //byte[] byReadDataLength = BitConverter.GetBytes((short)ReadDataLength);
        //    //byResultData[10] = byReadDataLength[1];
        //    //byResultData[11] = byReadDataLength[0];

        //    return byResultData;
        //}

        #endregion

        #region "下面代码移到 Converter 项目下面的 Converter 类"

        ///// <summary>
        ///// 将字节数组转换为16进制字符串，且用字符进行分隔
        ///// </summary>
        ///// <param name="ByteData">字节数组</param>
        ///// <param name="SplitChar">分割字符</param>
        ///// <returns></returns>
        //public static string CConverter.BytesToHexStringSplitByChar(byte[] ByteData, char SplitChar = ' ')
        //{
        //    string sResult = "";

        //    try
        //    {
        //        if (null != ByteData && ByteData.Length > 0)
        //        {
        //            for (int i = 0; i < ByteData.Length; i++)
        //            {
        //                sResult += ByteData[i].ToString("X2") + SplitChar;
        //            }
        //        }
        //    }
        //    catch (Exception)// ex)
        //    {
        //        //Enqueue(ex.Message + "  " + ex.StackTrace);
        //    }

        //    return sResult;
        //}

        ///// <summary>
        ///// 将16进制字符串(用字符进行分隔)转换为Socket码字符串
        ///// </summary>
        ///// <param name="HexStringSplitByChar">16进制字符串(用字符进行分隔)</param>
        ///// <param name="SplitChar">分割字符</param>
        ///// <returns></returns>
        //public static string HexStringSplitByCharToSocketString(string HexStringSplitByChar, char SplitChar = ' ')
        //{
        //    string sResult = "";

        //    try
        //    {
        //        if (string.IsNullOrEmpty(HexStringSplitByChar) == true)
        //        {
        //            return "";
        //        }

        //        string[] sHexString = HexStringSplitByChar.Split(SplitChar);
        //        byte[] byConvertData = new byte[sHexString.Length];
        //        if (null == sHexString && sHexString.Length < 1)
        //        {
        //            return "";   
        //        }
        //        else
        //        {
        //            for (int i = 0; i < sHexString.Length; i++)
        //            {
        //                byConvertData[i] = TwoHexCharsToByte(sHexString[i]);
        //            }

        //            sResult = CConverter.BytesToHexStringSplitByChar(byConvertData);
        //        }
        //    }
        //    catch (Exception)// ex)
        //    {
        //        //Enqueue(ex.Message + "  " + ex.StackTrace);
        //    }

        //    return sResult;
        //}

        ///// <summary>
        ///// 拼接两个字节数组，将第二个字节数组拼接在第一个字节数组后面
        ///// </summary>
        ///// <param name="FirstBytes"></param>
        ///// <param name="SecondBytes"></param>
        ///// <returns></returns>
        //public static byte[] JoinTwoByteArrays(byte[] FirstBytes, byte[] SecondBytes)
        //{
        //    if (null == FirstBytes && null == SecondBytes)
        //    {
        //        return null;
        //    }

        //    if (null == FirstBytes)
        //    {
        //        return SecondBytes;
        //    }

        //    if (null == SecondBytes)
        //    {
        //        return FirstBytes;
        //    }

        //    byte[] byResult = new byte[FirstBytes.Length + SecondBytes.Length];
        //    Array.Copy(FirstBytes, byResult, FirstBytes.Length);
        //    Array.Copy(SecondBytes, 0, byResult, FirstBytes.Length, SecondBytes.Length);

        //    return byResult;
        //}

        ///// <summary>
        ///// 将2位16进制数字符串转换为10进制byte值，例：FF - 255
        ///// </summary>
        ///// <param name="TwoHexChars">2位16进制数字符串</param>
        ///// <returns></returns>
        //public static byte TwoHexCharsToByte(string TwoHexChars)
        //{
        //    int iResultValue = 0;

        //    try
        //    {
        //        if (string.IsNullOrEmpty(TwoHexChars) == true || TwoHexChars.Length != 2)
        //        {
        //            throw new Exception("执行转换的16进制字符串必须是2个字符");
        //        }

        //        #region "将2位16进制数转换为10进制整数值"

        //        TwoHexChars = TwoHexChars.ToUpper();

        //        for (int j = 0; j < TwoHexChars.Length; j++)
        //        {
        //            char cSingleValue = TwoHexChars[j];

        //            switch (cSingleValue)
        //            {
        //                case '0':
        //                    if (j == 0)
        //                    {
        //                        iResultValue += Convert.ToInt32(0) * 16;
        //                    }
        //                    else
        //                    {
        //                        iResultValue += Convert.ToInt32(0);
        //                    }

        //                    break;

        //                case '1':
        //                    if (j == 0)
        //                    {
        //                        iResultValue += Convert.ToInt32(1) * 16;
        //                    }
        //                    else
        //                    {
        //                        iResultValue += Convert.ToInt32(1);
        //                    }

        //                    break;

        //                case '2':
        //                    if (j == 0)
        //                    {
        //                        iResultValue += Convert.ToInt32(2) * 16;
        //                    }
        //                    else
        //                    {
        //                        iResultValue += Convert.ToInt32(2);
        //                    }

        //                    break;

        //                case '3':
        //                    if (j == 0)
        //                    {
        //                        iResultValue += Convert.ToInt32(3) * 16;
        //                    }
        //                    else
        //                    {
        //                        iResultValue += Convert.ToInt32(3);
        //                    }

        //                    break;

        //                case '4':
        //                    if (j == 0)
        //                    {
        //                        iResultValue += Convert.ToInt32(4) * 16;
        //                    }
        //                    else
        //                    {
        //                        iResultValue += Convert.ToInt32(4);
        //                    }

        //                    break;

        //                case '5':
        //                    if (j == 0)
        //                    {
        //                        iResultValue += Convert.ToInt32(5) * 16;
        //                    }
        //                    else
        //                    {
        //                        iResultValue += Convert.ToInt32(5);
        //                    }

        //                    break;

        //                case '6':
        //                    if (j == 0)
        //                    {
        //                        iResultValue += Convert.ToInt32(6) * 16;
        //                    }
        //                    else
        //                    {
        //                        iResultValue += Convert.ToInt32(6);
        //                    }

        //                    break;

        //                case '7':
        //                    if (j == 0)
        //                    {
        //                        iResultValue += Convert.ToInt32(7) * 16;
        //                    }
        //                    else
        //                    {
        //                        iResultValue += Convert.ToInt32(7);
        //                    }

        //                    break;

        //                case '8':
        //                    if (j == 0)
        //                    {
        //                        iResultValue += Convert.ToInt32(8) * 16;
        //                    }
        //                    else
        //                    {
        //                        iResultValue += Convert.ToInt32(8);
        //                    }

        //                    break;

        //                case '9':

        //                    if (j == 0)
        //                    {
        //                        iResultValue += Convert.ToInt32(9) * 16;
        //                    }
        //                    else
        //                    {
        //                        iResultValue += Convert.ToInt32(9);
        //                    }

        //                    break;

        //                case 'A':
        //                    if (j == 0)
        //                    {
        //                        iResultValue += Convert.ToInt32(10 * 16);
        //                        //iValue1 += Convert.ToByte(10 * 16);
        //                    }
        //                    else
        //                    {
        //                        iResultValue += Convert.ToInt32(10);
        //                        //iValue1 += Convert.ToByte(10);
        //                    }

        //                    break;

        //                case 'B':
        //                    if (j == 0)
        //                    {
        //                        iResultValue += Convert.ToInt32(11 * 16);
        //                        //iValue1 += Convert.ToByte(11 * 16);
        //                    }
        //                    else
        //                    {
        //                        iResultValue += Convert.ToInt32(11);
        //                        //iValue1 += Convert.ToByte(11);
        //                    }

        //                    break;

        //                case 'C':
        //                    if (j == 0)
        //                    {
        //                        iResultValue += Convert.ToInt32(12 * 16);
        //                        //iValue1 += Convert.ToByte(12 * 16);
        //                    }
        //                    else
        //                    {
        //                        iResultValue += Convert.ToInt32(12);
        //                        //iValue1 += Convert.ToByte(12);
        //                    }

        //                    break;

        //                case 'D':
        //                    if (j == 0)
        //                    {
        //                        iResultValue += Convert.ToInt32(13 * 16);
        //                        //iValue1 += Convert.ToByte(13 * 16);
        //                    }
        //                    else
        //                    {
        //                        iResultValue += Convert.ToInt32(13);
        //                        //iValue1 += Convert.ToByte(13);
        //                    }

        //                    break;

        //                case 'E':
        //                    if (j == 0)
        //                    {
        //                        iResultValue += Convert.ToInt32(14 * 16);
        //                        //iValue1 += Convert.ToByte(14 * 16);
        //                    }
        //                    else
        //                    {
        //                        iResultValue += Convert.ToInt32(14);
        //                        //iValue1 += Convert.ToByte(14);
        //                    }

        //                    break;

        //                case 'F':
        //                    if (j == 0)
        //                    {
        //                        iResultValue += Convert.ToInt32(15 * 16);
        //                        //iValue1 += Convert.ToByte(15 * 16);
        //                    }
        //                    else
        //                    {
        //                        iResultValue += Convert.ToInt32(15);
        //                        //iValue1 += Convert.ToByte(15);
        //                    }

        //                    break;

        //                default:
        //                    throw new Exception("执行转换的16进制字符为非法字符");
        //            }
        //        }

        //        #endregion
        //    }
        //    catch (Exception)// ex)
        //    {
        //        //Enqueue(ex.Message + "  " + ex.StackTrace);
        //    }

        //    return Convert.ToByte(iResultValue);
        //}

        ///// <summary>
        ///// 字节转换为2为16进制字符
        ///// </summary>
        ///// <param name="ByteData">字节</param>
        ///// <returns>是否成功执行</returns>
        //public static string ByteToTwoHexChars(byte ByteData)
        //{
        //    return ByteData.ToString("X2");;
        //}

        ///// <summary>
        ///// 字节值按位转换为布尔数组
        ///// </summary>
        ///// <param name="ByteValue">字节值</param>
        ///// <returns></returns>
        //public static bool[] CConverter.ByteToBitArray(byte ByteValue)
        //{
        //    try
        //    {
        //        bool[] bResult = new bool[8];
        //        byte byTemp = 0x0;

        //        for (int i = 0; i < bResult.Length; i++)
        //        {
        //            byTemp = Convert.ToByte(ByteValue >> i);
        //            if ((byTemp & 1) == 1)
        //            {
        //                bResult[i] = true;
        //            }
        //            else
        //            {
        //                bResult[i] = false;
        //            }
        //        }

        //        return bResult;
        //    }
        //    catch (Exception)// ex)
        //    {
        //        //Enqueue(ex.Message + ", " + ex.StackTrace);
        //        return null;
        //    }
        //}

        ///// <summary>
        ///// 布尔数组转换为字节数组
        ///// </summary>
        ///// <param name="BitValue">布尔数组</param>
        ///// <returns></returns>
        //public static byte[] BitArrayToByte(bool[] BitValue)
        //{
        //    try
        //    {
        //        if (BitValue.Length % 8 != 0)
        //        {
        //            throw new Exception("bool[]数组的长度不是8的整数，即不是以整字节的形式，请修改参数数组的长度");
        //        }

        //        byte[] byResult = new byte[BitValue.Length / 8];
        //        byte byTemp = 0x0;

        //        // 以8个BOOL数组长度为一个字节来进行计算，可以用或
        //        for (int j = 0; j < byResult.Length; j++)
        //        {
        //            byTemp = 0x0;
        //            int iBeginIndex = j * 8;
        //            for (int i = iBeginIndex; i < iBeginIndex + 8; i++)
        //            {
        //                if (BitValue[i] == true)
        //                {
        //                    byTemp |= Convert.ToByte(0x1 << (i - iBeginIndex));//
        //                }
        //                else
        //                {

        //                }
        //            }

        //            byResult[j] = byTemp;
        //        }

        //        #region "Old codes"

        //        //byte[] byResult = new byte[BitValue.Length / 8];
        //        //int iTemp = 0x0;

        //        //// 以8个BOOL数组长度为一个字节来进行计算，可以用或
        //        //for (int j = 0; j < byResult.Length; j++)
        //        //{
        //        //    iTemp = 0x0;
        //        //    int iBeginIndex = j * 8;
        //        //    for (int i = iBeginIndex; i < iBeginIndex + 8; i++)
        //        //    {
        //        //        if (BitValue[i] == true)
        //        //        {
        //        //            iTemp |= 0x1 << (i - iBeginIndex);//Convert.ToByte(0x1 << i)
        //        //        }
        //        //        else
        //        //        {

        //        //        }
        //        //    }

        //        //    byte[] byIntToBytes = BitConverter.GetBytes(iTemp);
        //        //    byResult[j] = byIntToBytes[0];
        //        //}

        //        #endregion

        //        return byResult;
        //    }
        //    catch (Exception)// ex)
        //    {
        //        //索引超出了数组界限。
        //        //值对于无符号的字节太大或太小。
        //        //Enqueue(ex.Message + ", " + ex.StackTrace);
        //        return null;
        //    }
        //}

        ///// <summary>
        ///// 将int值转换为字节数组
        ///// </summary>
        ///// <param name="Value">int值</param>
        ///// <returns></returns>
        //public static byte[] ToBytes(short Value)
        //{
        //    byte[] byResult = BitConverter.GetBytes(Value);
        //    return byResult;
        //}

        ///// <summary>
        ///// 将int值转换为字节数组
        ///// </summary>
        ///// <param name="Value">int值</param>
        ///// <returns></returns>
        //public static byte[] ToBytes(int Value)
        //{
        //    byte[] byResult = BitConverter.GetBytes(Value);
        //    return byResult;
        //}

        ///// <summary>
        ///// 将float值转换为字节数组
        ///// </summary>
        ///// <param name="Value">float值</param>
        ///// <returns></returns>
        //public static byte[] ToBytes(float Value)
        //{
        //    byte[] byResult = BitConverter.GetBytes(Value);
        //    return byResult;
        //}

        ///// <summary>
        ///// 将double值转换为字节数组
        ///// </summary>
        ///// <param name="Value">double值</param>
        ///// <returns></returns>
        //public static byte[] ToBytes(double Value)
        //{
        //    byte[] byResult = BitConverter.GetBytes(Value);
        //    return byResult;
        //}

        ///// <summary>
        ///// 将long值转换为字节数组
        ///// </summary>
        ///// <param name="Value">long值</param>
        ///// <returns></returns>
        //public static byte[] ToBytes(long Value)
        //{
        //    byte[] byResult = BitConverter.GetBytes(Value);
        //    return byResult;
        //}

        ///// <summary>
        ///// 将int整型值转换为4个字符的16进制字符串
        ///// </summary>
        ///// <param name="IntValue">int整型值</param>
        ///// <returns></returns>
        //public static string IntToFourHexString(int IntValue)
        //{
        //    return IntValue.ToString("X4");
        //}

        ///// <summary>
        ///// 将4个字符的16进制字符串转换为int整型值
        ///// </summary>
        ///// <param name="HexString">4个字符的16进制字符串</param>
        ///// <returns>int整型值</returns>
        //public static int FourHexStringToInt(string HexString)
        //{
        //    if (string.IsNullOrEmpty(HexString) == true)
        //    {
        //        return 0;
        //    }

        //    if (HexString.Length > 4)
        //    {
        //        return 0;
        //    }

        //    try
        //    {
        //        return Convert.ToInt32(HexStringToLong(HexString));
        //    }
        //    catch (Exception)
        //    {
        //        return 0;
        //    }
        //}

        ///// <summary>
        ///// 将16进制字符串转换为long整型值
        ///// </summary>
        ///// <param name="HexString">16进制字符串</param>
        ///// <returns></returns>
        //public static long HexStringToLong(string HexString)
        //{
        //    if (string.IsNullOrEmpty(HexString) == true)
        //    {
        //        return 0;
        //    }

        //    long lResultValue = 0;

        //    HexString = HexString.ToUpper();

        //    int iIndexOfPow = 0;

        //    try
        //    {
        //        for (int j = HexString.Length - 1; j >= 0; j--)
        //        {
        //            #region "将2位16进制数转换为10进制整数值"

        //            char cSingleValue = HexString[j];

        //            switch (cSingleValue)
        //            {
        //                case '0':
        //                    //lResultValue += 0;

        //                    break;

        //                case '1':
        //                    lResultValue += 1 * Convert.ToInt64(Math.Pow(16, iIndexOfPow));

        //                    break;

        //                case '2':
        //                    lResultValue += 2 * Convert.ToInt64(Math.Pow(16, iIndexOfPow));

        //                    break;

        //                case '3':
        //                    lResultValue += 3 * Convert.ToInt64(Math.Pow(16, iIndexOfPow));

        //                    break;

        //                case '4':
        //                    lResultValue += 4 * Convert.ToInt64(Math.Pow(16, iIndexOfPow));

        //                    break;

        //                case '5':
        //                    lResultValue += 5 * Convert.ToInt64(Math.Pow(16, iIndexOfPow));

        //                    break;

        //                case '6':
        //                    lResultValue += 6 * Convert.ToInt64(Math.Pow(16, iIndexOfPow));

        //                    break;

        //                case '7':
        //                    lResultValue += 7 * Convert.ToInt64(Math.Pow(16, iIndexOfPow));

        //                    break;

        //                case '8':
        //                    lResultValue += 8 * Convert.ToInt64(Math.Pow(16, iIndexOfPow));

        //                    break;

        //                case '9':

        //                    lResultValue += 9 * Convert.ToInt64(Math.Pow(16, iIndexOfPow));

        //                    break;

        //                case 'A':
        //                    lResultValue += 10 * Convert.ToInt64(Math.Pow(16, iIndexOfPow));

        //                    break;

        //                case 'B':
        //                    lResultValue += 11 * Convert.ToInt64(Math.Pow(16, iIndexOfPow));

        //                    break;

        //                case 'C':
        //                    lResultValue += 12 * Convert.ToInt64(Math.Pow(16, iIndexOfPow));

        //                    break;

        //                case 'D':
        //                    lResultValue += 13 * Convert.ToInt64(Math.Pow(16, iIndexOfPow));

        //                    break;

        //                case 'E':
        //                    lResultValue += 14 * Convert.ToInt64(Math.Pow(16, iIndexOfPow));

        //                    break;

        //                case 'F':
        //                    lResultValue += 15 * Convert.ToInt64(Math.Pow(16, iIndexOfPow));

        //                    break;

        //                default:
        //                    throw new Exception("执行转换的16进制字符为非法字符");
        //            }

        //            #endregion

        //            iIndexOfPow++;
        //        }
        //    }
        //    catch (Exception)// ex)
        //    {
        //        //Enqueue(ex.Message + ", " + ex.StackTrace);
        //    }

        //    return lResultValue;
        //}

        #endregion

        #endregion

        #endregion

    }//class

}//namespace