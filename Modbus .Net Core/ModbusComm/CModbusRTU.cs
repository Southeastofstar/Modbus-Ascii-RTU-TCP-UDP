using System;
using System.Collections.Generic;
using System.Text;
using System.IO.Ports;
//using System.Windows.Forms;
using System.Diagnostics;
using Microsoft.VisualBasic;

using ThreadLock;
using Converter;
using Converter.Modbus;

//OK-1, 
namespace ModbusComm
{
    //后续可以考虑在收到从站返回的信息后，处理数据前先添加CRC校验检查，以及处理返回结果的字节顺序：ABCD/BADC/CDAB/DCBA

    //MODBUS RTU 报文格式						
    //起始位		    设备地址	功能代码	数据	  CRC校验	结束符
    //T1-T2-T3-T4		8Bit	    8Bit	    n个8Bit	  16Bit	    T1-T2-T3-T4

    #region "MODBUS RTU 报文格式 -- 使用二进制码进行发送和接收"

    //读取
    //地址码         功能码    设置起始地址   读取数量      CRC
    //1字节          1字节     2字节          2字节         2字节

    //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
    //CRC校验计算结果： (2字节)

    //*****************************

    //写入
    //地址码    功能码    设置起始地址   设置值      CRC
    //1字节     1字节     2字节          2字节      2字节

    //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 数据(0~252字节)
    //CRC校验计算结果： (2字节)
    
    #endregion

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
                        //else
                        //{

                        //}
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

                    //case LockerType.CountdownEventLock:
                    //    SyncLocker = new CCountdownEventLock();

                    //    break;
                }

                RS232CPort = new SerialPort();
                RS232CPort.PortName = RS232CPortName;

                BaudRateSetting(baudrate);

                RS232CPort.Encoding = Encoding.UTF8;
                RS232CPort.Parity = parity;
                RS232CPort.StopBits = stopbits;
                RS232CPort.WriteTimeout = writetimeout;
                RS232CPort.ReadTimeout = readtimeout;
                //RS232CPort.NewLine = "\r\n";//Modbus RTU码通讯的结束符是：回车+换行，此处是RTU通讯，故屏蔽
                
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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ")  通讯类初始化时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }
            }
        }

        #region "RTU-通讯方式下返回字符串中信息对应位置的定义"

        //未成功执行写命令
        //写操作记录：Modbus RTU(COM1)发送字节转换为16进制 - 01050000FF000000E4D3
        //写操作记录：Modbus RTU(COM1)收到字节转换为16进制 - 0185018350

        //未成功执行读命令
        //Modbus RTU(COM1)发送字节转换为16进制 - 010100000001FDCA
        //Modbus RTU(COM1)收到字节转换为16进制 - 0181018190

        //成功执行写命令-26506
        //写操作记录：Modbus RTU(COM1)发送字节转换为16进制 - 0106000098760000297D
        //写操作记录：Modbus RTU(COM1)收到字节转换为16进制 - 01060000987663EC

        //成功执行读命令，结果值：-26506
        //Modbus RTU(COM1)发送字节转换为16进制 - 010300000001840A
        //Modbus RTU(COM1)收到字节转换为16进制 - 01030298765262

        // 0   - 从站地址
        // 1   - 功能码
        // 2   - 错误码(命令执行失败)；数据字节长度(命令执行成功)
        // 3~(N-2)   - 有效数据开始的索引号
        // 最后2个字节为CRC校验码值

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
        public BytesFormat ReadInputRegisterBytesFormat { get; set; }//internal

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

        //ParaBytesFormat

        //WriteBytesFormat

        //ReadBytesFormat

        #region "读 - ok-ok"

        // 起始地址：0x0000~0xFFFF -- 0~65535
        // 读取数量：0x001~0x7D0 -- 1~2000

        #region "读单个/多个线圈 - ok-ok"

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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, 1);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadCoil;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(1);// 读取数量 -- 数据位的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";//  CConverter.BytesToHexStringSplitByChar(byDataToBeSent);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();
                
                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

                //Unlock();

                //if (bSaveReceivedStringToLog == true)
                //{
                //    string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);
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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    //成功执行写命令:True
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 05 00 00 FF 00 8C 3A 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 05 00 00 FF 00 8C 3A 

                    //成功执行读命令，结果值：True
                    //(COM1)发送字节转换为16进制 - 01 01 00 00 00 01 FD CA 
                    //(COM1)收到字节转换为16进制 - 01 01 01 01 90 48 

                    //成功执行写命令:False
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 05 00 00 00 00 CD CA 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 05 00 00 00 00 CD CA 

                    //成功执行读命令，结果值：False
                    //(COM1)发送字节转换为16进制 - 01 01 00 00 00 01 FD CA 
                    //(COM1)收到字节转换为16进制 - 01 01 01 00 51 88 

                    //成功执行写命令:True
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 05 00 00 FF 00 8C 3A 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 05 00 00 FF 00 8C 3A 

                    //成功执行读命令，结果值：True
                    //(COM1)发送字节转换为16进制 - 01 01 00 00 00 01 FD CA 
                    //(COM1)收到字节转换为16进制 - 01 01 01 01 90 48 

                    //成功执行写命令:False
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 05 00 00 00 00 CD CA 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 05 00 00 00 00 CD CA 

                    //成功执行读命令，结果值：False
                    //(COM1)发送字节转换为16进制 - 01 01 00 00 00 01 FD CA 
                    //(COM1)收到字节转换为16进制 - 01 01 01 00 51 88

                    #endregion

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
                        // 单个线圈的值
                        //0x0000	释放继电器线圈
                        //0xFF00	吸合继电器线圈

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

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

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, ReadDataLength * 8);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadCoil;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(ReadDataLength * 8);// 读取数量 -- 数据位的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

                //Unlock();

                //if (bSaveReceivedStringToLog == true)
                //{
                //    string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);
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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    //发送值[0~15]：True True True False True True True False True False False False True False True True
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 10 02 77 D1 05 8C 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 10 54 07 

                    //成功执行读命令，结果值：True True True False True True True False True False False False True False True True 
                    //(COM1)发送字节转换为16进制 - 01 01 00 00 00 10 3D C6 
                    //(COM1)收到字节转换为16进制 - 01 01 02 77 D1 5E 50 

                    //发送值[0~15]：True False True True False True False True True False False False True True True False
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 10 02 AD 71 5E 94 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 10 54 07 

                    //成功执行读命令，结果值：True False True True False True False True True False False False True True True False 
                    //(COM1)发送字节转换为16进制 - 01 01 00 00 00 10 3D C6 
                    //(COM1)收到字节转换为16进制 - 01 01 02 AD 71 05 48 

                    //发送值[0~15]：False False True True False False False False True False True True True False True False
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 10 02 0C 5D 26 D9 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 10 54 07 

                    //成功执行读命令，结果值：False False True True False False False False True False True True True False True False 
                    //(COM1)发送字节转换为16进制 - 01 01 00 00 00 10 3D C6 
                    //(COM1)收到字节转换为16进制 - 01 01 02 0C 5D 7D 05 

                    #endregion

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
                        //bool[] bResultData = new bool[ReadDataLength * 8];

                        //for (int i = 0; i < ReadDataLength; i++)
                        //{
                        //    //??
                        //    bool[] bTempData = CConverter.ByteToBitArray(byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i]);
                        //    for (int j = 0; j < bTempData.Length; j++)
                        //    {
                        //        bResultData[i * 8 + j] = bTempData[j];
                        //    }

                        //    bTempData = null;
                        //}

                        //Value = bResultData;
                        //bResultData = null;

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 1 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, 1 * 8);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadCoil;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(1 * 8);// 读取数量 -- 数据位的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}
                
                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

                //Unlock();

                //if (bSaveReceivedStringToLog == true)
                //{
                //    string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);
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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    //成功执行写命令182
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 08 01 B6 7F 23 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 08 54 0D 

                    //成功执行读命令，结果值：182
                    //(COM1)发送字节转换为16进制 - 01 01 00 00 00 08 3D CC 
                    //(COM1)收到字节转换为16进制 - 01 01 01 B6 D0 3E 

                    //成功执行写命令32
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 08 01 20 FF 4D 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 08 54 0D 

                    //成功执行读命令，结果值：32
                    //(COM1)发送字节转换为16进制 - 01 01 00 00 00 08 3D CC 
                    //(COM1)收到字节转换为16进制 - 01 01 01 20 50 50 

                    //成功执行写命令26
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 08 01 1A 7F 5E 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 08 54 0D 

                    //成功执行读命令，结果值：26
                    //(COM1)发送字节转换为16进制 - 01 01 00 00 00 08 3D CC 
                    //(COM1)收到字节转换为16进制 - 01 01 01 1A D0 43 

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // okokok
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

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

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, ReadDataLength * 8);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadCoil;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(ReadDataLength * 8);// 读取数量 -- 数据位的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

                //Unlock();

                //if (bSaveReceivedStringToLog == true)
                //{
                //    string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);
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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    //发送值[0~1]：199 219
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 10 02 C7 DB F0 4B 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 10 54 07 

                    //成功执行读命令，结果值：199 219 
                    //(COM1)发送字节转换为16进制 - 01 01 00 00 00 10 3D C6 
                    //(COM1)收到字节转换为16进制 - 01 01 02 C7 DB AB 97 

                    //发送值[0~1]：130 157
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 10 02 82 9D 43 29 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 10 54 07 

                    //成功执行读命令，结果值：130 157 
                    //(COM1)发送字节转换为16进制 - 01 01 00 00 00 10 3D C6 
                    //(COM1)收到字节转换为16进制 - 01 01 02 82 9D 18 F5 

                    #endregion

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
                        //byte[] byResultData = new byte[ReadDataLength];
                        //for (int i = 0; i < ReadDataLength; i++)
                        //{
                        //    byResultData[i] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i];
                        //}

                        //Value = byResultData;
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 1 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, 1 * 8);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadCoil;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(1 * 8);// 读取数量 -- 数据位的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}
                
                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

                //Unlock();

                //if (bSaveReceivedStringToLog == true)
                //{
                //    string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);
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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

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

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, ReadDataLength * 8);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadCoil;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(ReadDataLength * 8);// 读取数量 -- 数据位的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

                //Unlock();

                //if (bSaveReceivedStringToLog == true)
                //{
                //    string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);
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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, 2 * 8);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadCoil;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(2 * 8);// 读取数量 -- 数据位的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

                //Unlock();

                //if (bSaveReceivedStringToLog == true)
                //{
                //    string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);
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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    //成功执行写命令15280
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 10 02 B0 3B D6 33 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 10 54 07 

                    //成功执行读命令，结果值：15280
                    //(COM1)发送字节转换为16进制 - 01 01 00 00 00 10 3D C6 
                    //(COM1)收到字节转换为16进制 - 01 01 02 B0 3B 8D EF 

                    //成功执行写命令7671
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 10 02 F7 1D 64 19 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 10 54 07 

                    //成功执行读命令，结果值：7671
                    //(COM1)发送字节转换为16进制 - 01 01 00 00 00 10 3D C6 
                    //(COM1)收到字节转换为16进制 - 01 01 02 F7 1D 3F C5 

                    //成功执行写命令11424
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 10 02 A0 2C 9B FD 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 10 54 07

                    //成功执行读命令，结果值：11424
                    //(COM1)发送字节转换为16进制 - 01 01 00 00 00 10 3D C6 
                    //(COM1)收到字节转换为16进制 - 01 01 02 A0 2C C0 21 

                    #endregion

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
                        //byte[] byResultData = new byte[2];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes];
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];

                        ////Value = CConverter.ToInt16(byResultData, ReadCoilAndInputBytesFormat, 0);
                        //Value = BitConverter.ToInt16(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                //if (ReadDataLength < 1 || ReadDataLength > 125)//读取数据长度，有效值范围：1~125(字)
                //{
                //    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                //}

                if (BeginAddress + ReadDataLength * 2 * 8 > 65535)//1个字 = 2 个字节 = 16位
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, ReadDataLength * 2 * 8);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadCoil;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(ReadDataLength * 2 * 8);// 读取数量 -- 数据位的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

                //Unlock();

                //if (bSaveReceivedStringToLog == true)
                //{
                //    string sFeedBackFromSlave = CConverter.BytesToHexStringSplitByChar(byReadData);
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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    //发送值[0~1]：-29305 26441
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 20 04 87 8D 49 67 0A 6D 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 20 54 13 

                    //成功执行读命令，结果值：-29305 26441 
                    //(COM1)发送字节转换为16进制 - 01 01 00 00 00 20 3D D2 
                    //(COM1)收到字节转换为16进制 - 01 01 04 87 8D 49 67 35 34 

                    //发送值[0~1]：22093 -17798
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 20 04 4D 56 7A BA 91 27 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 20 54 13 

                    //成功执行读命令，结果值：22093 -17798 
                    //(COM1)发送字节转换为16进制 - 01 01 00 00 00 20 3D D2 
                    //(COM1)收到字节转换为16进制 - 01 01 04 4D 56 7A BA AE 7E 

                    #endregion

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
                        //Value = new short[ReadDataLength];

                        //for (int i = 0; i < ReadDataLength; i++)
                        //{
                        //    byte[] byResultData = new byte[2];
                        //    byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 2 + 0];
                        //    byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 2 + 1];

                        //    Value[i] = BitConverter.ToInt16(byResultData, 0);
                        //    byResultData = null;
                        //}

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, 2 * 8);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadCoil;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(2 * 8);// 读取数量 -- 数据位的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    #endregion

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
                        //byte[] byResultData = new byte[2];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes];
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];

                        //Value = BitConverter.ToUInt16(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                //if (ReadDataLength < 1 || ReadDataLength > 125)//读取数据长度，有效值范围：1~125(字)
                //{
                //    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                //}

                if (BeginAddress + ReadDataLength * 2 * 8 > 65535)//1个字 = 2 个字节 = 16位
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, ReadDataLength * 2 * 8);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadCoil;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(ReadDataLength * 2 * 8);// 读取数量 -- 数据位的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    #endregion

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
                        //Value = new ushort[ReadDataLength];

                        //for (int i = 0; i < ReadDataLength; i++)
                        //{
                        //    byte[] byResultData = new byte[2];
                        //    byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 2 + 0];
                        //    byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 2 + 1];

                        //    Value[i] = BitConverter.ToUInt16(byResultData, 0);
                        //    byResultData = null;
                        //}

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, 4 * 8);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadCoil;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(4 * 8);// 读取数量 -- 数据位的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    //成功执行写命令847345521
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 20 04 71 77 81 32 8E 2B 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 20 54 13 

                    //成功执行读命令，结果值：847345521
                    //(COM1)发送字节转换为16进制 - 01 01 00 00 00 20 3D D2 
                    //(COM1)收到字节转换为16进制 - 01 01 04 71 77 81 32 B1 72 

                    //成功执行写命令-159861249
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 20 04 FF B5 78 F6 47 3C 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 20 54 13 

                    //成功执行读命令，结果值：-159861249
                    //(COM1)发送字节转换为16进制 - 01 01 00 00 00 20 3D D2 
                    //(COM1)收到字节转换为16进制 - 01 01 04 FF B5 78 F6 78 65 

                    //成功执行写命令-707998984
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 20 04 F8 CA CC D5 41 49 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 20 54 13 

                    //成功执行读命令，结果值：-707998984
                    //(COM1)发送字节转换为16进制 - 01 01 00 00 00 20 3D D2 
                    //(COM1)收到字节转换为16进制 - 01 01 04 F8 CA CC D5 7E 10 

                    #endregion

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
                        //byte[] byResultData = new byte[4];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];
                        //byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 2];
                        //byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 3];

                        //Value = BitConverter.ToInt32(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                //if (ReadDataLength < 1 || ReadDataLength > 125)//读取数据长度，有效值范围：1~125(字)
                //{
                //    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                //}

                if (BeginAddress + ReadDataLength * 4 * 8 > 65535)//1个字 = 2 个字节 = 16位
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, ReadDataLength * 4 * 8);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadCoil;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(ReadDataLength * 4 * 8);// 读取数量 -- 数据位的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    //发送值[0~3]：-1943959430 6529772 -355526495 -696478494 
                    //未成功执行写命令
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 80 10 7A 88 21 8C EC A2 63 00 A1 18 CF EA E2 94 7C D6 1D B1 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 8F 02 C5 F1 

                    //发送值[0~3]：-373718665 1807829400 -1763294981 -1221045901
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 80 10 77 81 B9 E9 98 49 C1 6B FB 40 E6 96 73 51 38 B7 7E 6E 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 80 54 6B 

                    //成功执行读命令，结果值：-373718665 1807829400 
                    //(COM1)发送字节转换为16进制 - 01 01 00 00 00 40 3D FA 
                    //(COM1)收到字节转换为16进制 - 01 01 08 77 81 B9 E9 98 49 C1 6B 4A 60 

                    //发送值[0~3]：-1557844102 -1695419704 -1711527296 -584049534
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 80 10 7A 2F 25 A3 C8 F2 F1 9A 80 2A FC 99 82 1C 30 DD AB 32 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 80 54 6B 

                    //成功执行读命令，结果值：-1557844102 -1695419704 
                    //(COM1)发送字节转换为16进制 - 01 01 00 00 00 40 3D FA 
                    //(COM1)收到字节转换为16进制 - 01 01 08 7A 2F 25 A3 C8 F2 F1 9A F4 C0 

                    //发送值[0~3]：451203787 -980301591 -1595631220 624134427
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 80 10 CB D2 E4 1A E9 C8 91 C5 8C 99 E4 A0 1B 89 33 25 7F C6 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 80 54 6B 

                    //成功执行读命令，结果值：451203787 -980301591 
                    //(COM1)发送字节转换为16进制 - 01 01 00 00 00 40 3D FA 
                    //(COM1)收到字节转换为16进制 - 01 01 08 CB D2 E4 1A E9 C8 91 C5 DD B4 

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, 4 * 8);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadCoil;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(4 * 8);// 读取数量 -- 数据位的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    #endregion

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
                        //byte[] byResultData = new byte[4];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];
                        //byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 2];
                        //byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 3];

                        //Value = BitConverter.ToUInt32(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                //if (ReadDataLength < 1 || ReadDataLength > 125)//读取数据长度，有效值范围：1~125(字)
                //{
                //    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                //}

                if (BeginAddress + ReadDataLength * 4 * 8 > 65535)//1个字 = 2 个字节 = 16位
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, ReadDataLength * 4 * 8);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadCoil;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(ReadDataLength * 4 * 8);// 读取数量 -- 数据位的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        #region "Hide"

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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, 4 * 8);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadCoil;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(4 * 8);// 读取数量 -- 数据位的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************



                    #endregion

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
                        //byte[] byResultData = new byte[4];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];
                        //byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 2];
                        //byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 3];

                        //Value = BitConverter.ToSingle(byResultData, 0);
                        //byResultData = null;

                        Value = CConverter.ToFloat(CConverter.CopyBytes(byReadData, (uint)(1 * (Byte)ByteCount.Float), (uint)PosIndexOfDataInRTUReceivedBytes), ReadCoilBytesFormat, 0);

                        byDataToBeSent = null;
                        byReadData = null;

                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                //if (ReadDataLength < 1 || ReadDataLength > 125)//读取数据长度，有效值范围：1~125(字)
                //{
                //    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                //}

                if (BeginAddress + ReadDataLength * 4 * 8 > 65535)//1个字 = 2 个字节 = 16位
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, ReadDataLength * 4 * 8);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadCoil;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(ReadDataLength * 4 * 8);// 读取数量 -- 数据位的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************



                    #endregion

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

                        Value = CConverter.ToFloatArray(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (Byte)ByteCount.Float), (uint)PosIndexOfDataInRTUReceivedBytes), ReadCoilBytesFormat, 0);
                        
                        byDataToBeSent = null;
                        byReadData = null;

                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, 8 * 8);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadCoil;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(8 * 8);// 读取数量 -- 数据位的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************



                    #endregion

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

                        Value = CConverter.ToDouble(CConverter.CopyBytes(byReadData, (uint)(1 * (Byte)ByteCount.Double), (uint)PosIndexOfDataInRTUReceivedBytes), ReadCoilBytesFormat, 0);

                        byDataToBeSent = null;
                        byReadData = null;

                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

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

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, ReadDataLength * 8 * 8);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadCoil;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(ReadDataLength * 8 * 8);// 读取数量 -- 数据位的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************



                    #endregion

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

                        Value = CConverter.ToDoubleArray(CConverter.CopyBytes(byReadData, (uint)(ReadDataLength * (Byte)ByteCount.Double), (uint)PosIndexOfDataInRTUReceivedBytes), ReadCoilBytesFormat, 0);
                        
                        byDataToBeSent = null;
                        byReadData = null;

                        return true;
                    }
                    else
                    {
                        AnalysisErrorCode(byReadData);

                        byDataToBeSent = null;
                        byReadData = null;
                        return false;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, 8 * 8);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadCoil;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(8 * 8);// 读取数量 -- 数据位的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************



                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

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

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, ReadDataLength * 8 * 8);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadCoil;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(ReadDataLength * 8 * 8);// 读取数量 -- 数据位的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************



                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, 8 * 8);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadCoil;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(8 * 8);// 读取数量 -- 数据位的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************



                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

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

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadCoil, BeginAddress, ReadDataLength * 8 * 8);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadCoil;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(ReadDataLength * 8 * 8);// 读取数量 -- 数据位的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************



                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        #endregion

        #region "读单个/多个输入位的状态 - ok-ok"

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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节        2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)

                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, 1);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadInputSignal;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(1);// 读取数量 -- 数据位的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    //成功执行读命令，结果值：True
                    //(COM1)发送字节转换为16进制 - 01 02 00 00 00 01 B9 CA 
                    //(COM1)收到字节转换为16进制 - 01 02 01 01 60 48 

                    //成功执行读命令，结果值：False
                    //(COM1)发送字节转换为16进制 - 01 02 00 00 00 01 B9 CA 
                    //(COM1)收到字节转换为16进制 - 01 02 01 00 A1 88 

                    //成功执行读命令，结果值：True
                    //(COM1)发送字节转换为16进制 - 01 02 00 00 00 01 B9 CA 
                    //(COM1)收到字节转换为16进制 - 01 02 01 01 60 48 

                    //成功执行读命令，结果值：False
                    //(COM1)发送字节转换为16进制 - 01 02 00 00 00 01 B9 CA 
                    //(COM1)收到字节转换为16进制 - 01 02 01 00 A1 88 

                    #endregion

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
                        // 单个位的值
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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                //if (ReadDataLength < 1 || ReadDataLength > 125)//读取数据长度，有效值范围：1~250(字节)
                //{
                //    throw new Exception("读取数据长度的值不正确，有效值范围：1~250(字节)");
                //}

                if (BeginAddress + ReadDataLength * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, ReadDataLength * 8);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadInputSignal;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(ReadDataLength * 8);// 读取数量 -- 数据位的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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
                    //ReadBackData = "";
                    return false;
                }
                else
                {
                    #region "通讯记录"

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    //成功执行读命令，结果值：False True True True False False True True False False False False False False False False 
                    //(COM1)发送字节转换为16进制 - 01 02 00 00 00 10 79 C6 
                    //(COM1)收到字节转换为16进制 - 01 02 02 CE 00 ED D8 

                    //成功执行读命令，结果值：False True True True False False True True False False True True True True True False 
                    //(COM1)发送字节转换为16进制 - 01 02 00 00 00 10 79 C6 
                    //(COM1)收到字节转换为16进制 - 01 02 02 CE 7C EC 39 

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 1 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, 1 * 8);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadInputSignal;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(1 * 8);// 读取数量 -- 数据位的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    //成功执行读命令，结果值：206
                    //(COM1)发送字节转换为16进制 - 01 02 00 00 00 08 79 CC 
                    //(COM1)收到字节转换为16进制 - 01 02 01 CE 20 1C 

                    //成功执行读命令，结果值：206
                    //(COM1)发送字节转换为16进制 - 01 02 00 00 00 08 79 CC 
                    //(COM1)收到字节转换为16进制 - 01 02 01 CE 20 1C 

                    //成功执行读命令，结果值：164
                    //(COM1)发送字节转换为16进制 - 01 02 00 00 00 08 79 CC 
                    //(COM1)收到字节转换为16进制 - 01 02 01 A4 A0 33 

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                //if (ReadDataLength < 1 || ReadDataLength > 125)//读取数据长度，有效值范围：1~250(字节)
                //{
                //    throw new Exception("读取数据长度的值不正确，有效值范围：1~250(字节)");
                //}

                if (BeginAddress + ReadDataLength * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, ReadDataLength * 8);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadInputSignal;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(ReadDataLength * 8);// 读取数量 -- 数据位的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    //成功执行读命令，结果值：206 124 
                    //(COM1)发送字节转换为16进制 - 01 02 00 00 00 10 79 C6 
                    //(COM1)收到字节转换为16进制 - 01 02 02 CE 7C EC 39 

                    //成功执行读命令，结果值：164 156 
                    //(COM1)发送字节转换为16进制 - 01 02 00 00 00 10 79 C6 
                    //(COM1)收到字节转换为16进制 - 01 02 02 A4 9C C3 11

                    #endregion

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
                        //byte[] byResultData = new byte[ReadDataLength];
                        //for (int i = 0; i < ReadDataLength; i++)
                        //{
                        //    byResultData[i] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i];
                        //}

                        //Value = byResultData;
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 1 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, 1 * 8);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadInputSignal;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(1 * 8);// 读取数量 -- 数据位的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                //if (ReadDataLength < 1 || ReadDataLength > 125)//读取数据长度，有效值范围：1~250(字节)
                //{
                //    throw new Exception("读取数据长度的值不正确，有效值范围：1~250(字节)");
                //}

                if (BeginAddress + ReadDataLength * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, ReadDataLength * 8);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadInputSignal;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(ReadDataLength * 8);// 读取数量 -- 数据位的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 1 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, 2 * 8);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadInputSignal;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(2 * 8);// 读取数量 -- 数据位的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    //成功执行读命令，结果值：-25436
                    //(COM1)发送字节转换为16进制 - 01 02 00 00 00 10 79 C6 
                    //(COM1)收到字节转换为16进制 - 01 02 02 A4 9C C3 11 

                    //成功执行读命令，结果值：-1
                    //(COM1)发送字节转换为16进制 - 01 02 00 00 00 10 79 C6 
                    //(COM1)收到字节转换为16进制 - 01 02 02 FF FF B8 08 

                    //成功执行读命令，结果值：4095
                    //(COM1)发送字节转换为16进制 - 01 02 00 00 00 10 79 C6 
                    //(COM1)收到字节转换为16进制 - 01 02 02 FF 0F B8 4C 

                    //成功执行读命令，结果值：3903
                    //(COM1)发送字节转换为16进制 - 01 02 00 00 00 10 79 C6 
                    //(COM1)收到字节转换为16进制 - 01 02 02 3F 0F E8 4C 

                    #endregion

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
                        //byte[] byResultData = new byte[2];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];

                        //Value = BitConverter.ToInt16(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                //if (ReadDataLength < 1 || ReadDataLength > 125)
                //{
                //    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                //}

                if (BeginAddress + ReadDataLength * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, ReadDataLength * 2 * 8);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadInputSignal;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(ReadDataLength * 2 * 8);// 读取数量 -- 数据位的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    //成功执行读命令，结果值：3903 3413 
                    //(COM1)发送字节转换为16进制 - 01 02 00 00 00 20 79 D2 
                    //(COM1)收到字节转换为16进制 - 01 02 04 3F 0F 55 0D 39 60 

                    //成功执行读命令，结果值：3903 3413 
                    //(COM1)发送字节转换为16进制 - 01 02 00 00 00 20 79 D2 
                    //(COM1)收到字节转换为16进制 - 01 02 04 3F 0F 55 0D 39 60 

                    //成功执行读命令，结果值：3307 3301 
                    //(COM1)发送字节转换为16进制 - 01 02 00 00 00 20 79 D2 
                    //(COM1)收到字节转换为16进制 - 01 02 04 EB 0C E5 0C 44 90 

                    #endregion

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
                        //Value = new short[ReadDataLength];

                        //for (int i = 0; i < ReadDataLength; i++)
                        //{
                        //    byte[] byResultData = new byte[2];
                        //    byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 2 + 0];
                        //    byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 2 + 1];

                        //    Value[i] = BitConverter.ToInt16(byResultData, 0);
                        //    byResultData = null;
                        //}

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 1 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, 2 * 8);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadInputSignal;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(2 * 8);// 读取数量 -- 数据位的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    #endregion

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
                        //byte[] byResultData = new byte[2];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];

                        //Value = BitConverter.ToUInt16(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                //if (ReadDataLength < 1 || ReadDataLength > 125)
                //{
                //    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                //}

                if (BeginAddress + ReadDataLength * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, ReadDataLength * 2 * 8);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadInputSignal;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(ReadDataLength * 2 * 8);// 读取数量 -- 数据位的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    #endregion

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
                        //Value = new ushort[ReadDataLength];

                        //for (int i = 0; i < ReadDataLength; i++)
                        //{
                        //    byte[] byResultData = new byte[2];
                        //    byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 2 + 0];
                        //    byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + i * 2 + 1];

                        //    Value[i] = BitConverter.ToUInt16(byResultData, 0);
                        //    byResultData = null;
                        //}

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, 4 * 8);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadInputSignal;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(4 * 8);// 读取数量 -- 数据位的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    //成功执行读命令，结果值：-1073692648
                    //(COM1)发送字节转换为16进制 - 01 02 00 00 00 20 79 D2 
                    //(COM1)收到字节转换为16进制 - 01 02 04 18 C0 00 C0 FD 2E 

                    //成功执行读命令，结果值：25216536
                    //(COM1)发送字节转换为16进制 - 01 02 00 00 00 20 79 D2 
                    //(COM1)收到字节转换为16进制 - 01 02 04 18 C6 80 01 BD 7F 

                    //未成功执行读命令
                    //(COM1)发送字节转换为16进制 - 01 02 00 00 00 40 79 FA 
                    //(COM1)收到字节转换为16进制 - 01 82 02 C1 61 

                    #endregion

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
                        //byte[] byResultData = new byte[4];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];
                        //byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 2];
                        //byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 3];

                        //Value = BitConverter.ToInt32(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                //if (ReadDataLength < 1 || ReadDataLength > 125)//读取数据长度，有效值范围：1~125(字)
                //{
                //    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                //}

                if (BeginAddress + ReadDataLength * 4 * 8 > 65535)//1个字 = 2 个字节 = 16位
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, ReadDataLength * 4 * 8);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadInputSignal;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(ReadDataLength * 4 * 8);// 读取数量 -- 数据位的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    //成功执行读命令，结果值：1610612739 50331648 
                    //(COM1)发送字节转换为16进制 - 01 02 00 00 00 40 79 FA 
                    //(COM1)收到字节转换为16进制 - 01 02 08 03 00 00 60 00 00 00 03 44 0E 

                    //成功执行读命令，结果值：1611333635 125829120 
                    //(COM1)发送字节转换为16进制 - 01 02 00 00 00 40 79 FA 
                    //(COM1)收到字节转换为16进制 - 01 02 08 03 00 0B 60 00 00 80 07 25 76 

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, 4 * 8);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadInputSignal;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(4 * 8);// 读取数量 -- 数据位的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    #endregion

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
                        //byte[] byResultData = new byte[4];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];
                        //byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 2];
                        //byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 3];

                        //Value = BitConverter.ToUInt32(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                //if (ReadDataLength < 1 || ReadDataLength > 125)//读取数据长度，有效值范围：1~125(字)
                //{
                //    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                //}

                if (BeginAddress + ReadDataLength * 4 * 8 > 65535)//1个字 = 2 个字节 = 16位
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, ReadDataLength * 4 * 8);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadInputSignal;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(ReadDataLength * 4 * 8);// 读取数量 -- 数据位的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, 8 * 8);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadInputSignal;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(8 * 8);// 读取数量 -- 数据位的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    //成功执行读命令，结果值：540431956895793155
                    //(COM1)发送字节转换为16进制 - 01 02 00 00 00 40 79 FA 
                    //(COM1)收到字节转换为16进制 - 01 02 08 03 00 0B 60 00 00 80 07 25 76 

                    //成功执行读命令，结果值：-1152921504606846976
                    //(COM1)发送字节转换为16进制 - 01 02 00 00 00 40 79 FA 
                    //(COM1)收到字节转换为16进制 - 01 02 08 00 00 00 00 00 00 00 F0 C4 56 

                    //成功执行读命令，结果值：1055531162664960
                    //(COM1)发送字节转换为16进制 - 01 02 00 00 00 40 79 FA 
                    //(COM1)收到字节转换为16进制 - 01 02 08 00 00 00 00 00 C0 03 00 C4 DE 

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

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

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, ReadDataLength * 8 * 8);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadInputSignal;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(ReadDataLength * 8 * 8);// 读取数量 -- 数据位的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    //成功执行读命令，结果值：277025521664 -2305843009211432960 
                    //(COM1)发送字节转换为16进制 - 01 02 00 00 00 80 79 AA 
                    //(COM1)收到字节转换为16进制 - 01 02 10 00 00 02 80 40 00 00 00 00 80 22 00 00 00 00 E0 CE 5B 

                    //成功执行读命令，结果值：277598044160 -1709116058584839616 
                    //(COM1)发送字节转换为16进制 - 01 02 00 00 00 80 79 AA 
                    //(COM1)收到字节转换为16进制 - 01 02 10 00 00 22 A2 40 00 00 00 40 8A 22 00 00 00 48 E8 0E 6F 

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, 8 * 8);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadInputSignal;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(8 * 8);// 读取数量 -- 数据位的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

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

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputSignal, BeginAddress, ReadDataLength * 8 * 8);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadInputSignal;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(ReadDataLength * 8 * 8);// 读取数量 -- 数据位的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        #endregion

        #region "读单个/多个输入寄存器的状态 - ok-ok"

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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, 1);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadInputRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(1);// 读取数量 -- 输入寄存器数据(short)的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    //成功执行读命令，结果值：0
                    //(COM1)发送字节转换为16进制 - 01 04 00 00 00 01 31 CA 
                    //(COM1)收到字节转换为16进制 - 01 04 02 00 00 B9 30 

                    //成功执行读命令，结果值：-32254
                    //(COM1)发送字节转换为16进制 - 01 04 00 00 00 01 31 CA 
                    //(COM1)收到字节转换为16进制 - 01 04 02 82 02 58 51 

                    //成功执行读命令，结果值：3244
                    //(COM1)发送字节转换为16进制 - 01 04 00 00 00 01 31 CA 
                    //(COM1)收到字节转换为16进制 - 01 04 02 0C AC BC 4D 

                    #endregion

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
                        //byte[] byResultData = new byte[2];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];//接收数据 - 高字节
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];//接收数据 - 低字节

                        //Value = BitConverter.ToInt16(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                //if (ReadDataLength < 1 || ReadDataLength > 125)//读取数据长度，有效值范围：1~250(字节)
                //{
                //    throw new Exception("读取数据长度的值不正确，有效值范围：1~250(字节)");
                //}

                if (BeginAddress + ReadDataLength * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, ReadDataLength);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadInputRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(ReadDataLength);// 读取数量 -- 输入寄存器数据(short)的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    //成功执行读命令，结果值：3244 -32546 
                    //(COM1)发送字节转换为16进制 - 01 04 00 00 00 02 71 CB 
                    //(COM1)收到字节转换为16进制 - 01 04 04 0C AC 80 DE D9 6D 

                    //成功执行读命令，结果值：-2543 32454 
                    //(COM1)发送字节转换为16进制 - 01 04 00 00 00 02 71 CB 
                    //(COM1)收到字节转换为16进制 - 01 04 04 F6 11 7E C6 39 FB 

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, 1);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadInputRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(1);// 读取数量 -- 输入寄存器数据(short)的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    #endregion

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
                        //byte[] byResultData = new byte[2];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];//接收数据 - 高字节
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];//接收数据 - 低字节

                        //Value = BitConverter.ToUInt16(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                //if (ReadDataLength < 1 || ReadDataLength > 125)//读取数据长度，有效值范围：1~250(字节)
                //{
                //    throw new Exception("读取数据长度的值不正确，有效值范围：1~250(字节)");
                //}

                if (BeginAddress + ReadDataLength * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, ReadDataLength);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadInputRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(ReadDataLength);// 读取数量 -- 输入寄存器数据(short)的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, 2);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadInputRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(2);// 读取数量 -- 输入寄存器数据(short)的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    //成功执行读命令，结果值：-654453
                    //(COM1)发送字节转换为16进制 - 01 04 00 00 00 02 71 CB 
                    //(COM1)收到字节转换为16进制 - 01 04 04 03 8B FF F6 4A 5C 

                    //成功执行读命令，结果值：654453
                    //(COM1)发送字节转换为16进制 - 01 04 00 00 00 02 71 CB 
                    //(COM1)收到字节转换为16进制 - 01 04 04 FC 75 00 09 1A 08 

                    //成功执行读命令，结果值：-7678645
                    //(COM1)发送字节转换为16进制 - 01 04 00 00 00 02 71 CB 
                    //(COM1)收到字节转换为16进制 - 01 04 04 D5 4B FF 8A 73 C9 

                    #endregion

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
                        //byte[] byResultData = new byte[4];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];
                        //byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 3];
                        //byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 2];

                        //Value = BitConverter.ToInt32(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                //if (ReadDataLength < 1 || ReadDataLength > 125)
                //{
                //    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                //}

                if (BeginAddress + ReadDataLength * 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, ReadDataLength * 2);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadInputRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(ReadDataLength * 2);// 读取数量 -- 输入寄存器数据(short)的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    //成功执行读命令，结果值：-7678645 -5453453 
                    //(COM1)发送字节转换为16进制 - 01 04 00 00 00 04 F1 C9 
                    //(COM1)收到字节转换为16进制 - 01 04 08 D5 4B FF 8A C9 73 FF AC 10 70 

                    //成功执行读命令，结果值：7678645 5453453 
                    //(COM1)发送字节转换为16进制 - 01 04 00 00 00 04 F1 C9 
                    //(COM1)收到字节转换为16进制 - 01 04 08 2A B5 00 75 36 8D 00 53 10 F4 

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, 2);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadInputRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(2);// 读取数量 -- 输入寄存器数据(short)的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    #endregion

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
                        //byte[] byResultData = new byte[4];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];
                        //byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 3];
                        //byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 2];

                        //Value = BitConverter.ToUInt32(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                //if (ReadDataLength < 1 || ReadDataLength > 125)
                //{
                //    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                //}

                if (BeginAddress + ReadDataLength * 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, ReadDataLength * 2);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadInputRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(ReadDataLength * 2);// 读取数量 -- 输入寄存器数据(short)的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, 2);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadInputRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(2);// 读取数量 -- 输入寄存器数据(short)的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************



                    #endregion

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
                        //byte[] byResultData = new byte[4];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];
                        //byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 3];
                        //byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 2];

                        //Value = BitConverter.ToSingle(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, ReadDataLength * 2);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadInputRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(ReadDataLength * 2);// 读取数量 -- 输入寄存器数据(short)的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************



                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, 4);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadInputRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(4);// 读取数量 -- 输入寄存器数据(short)的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************



                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok-ok
        /// <summary>
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 4 * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, ReadDataLength * 4);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadInputRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(ReadDataLength * 4);// 读取数量 -- 输入寄存器数据(short)的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************



                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, 4);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadInputRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(4);// 读取数量 -- 输入寄存器数据(short)的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************



                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

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

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, ReadDataLength * 4);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadInputRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(ReadDataLength * 4);// 读取数量 -- 输入寄存器数据(short)的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************



                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
        public bool ReadInputRegister(byte DeviceAddress, ushort BeginAddress, ref ulong Value)//  , out string ReadBackData)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, 4);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadInputRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(4);// 读取数量 -- 输入寄存器数据(short)的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************



                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
        public bool ReadInputRegister(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref ulong[] Value)//  , out string ReadBackData)
        {
            try
            {
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

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

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadInputRegister, BeginAddress, ReadDataLength * 4);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadInputRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(ReadDataLength * 4);// 读取数量 -- 输入寄存器数据(short)的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************



                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, 1);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(1);// 读取数量 -- 输入寄存器数据(short)的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    //成功执行写命令-29728
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 06 00 00 8B E0 EE B2 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 06 00 00 8B E0 EE B2 

                    //成功执行读命令，结果值：-29728
                    //(COM1)发送字节转换为16进制 - 01 03 00 00 00 01 84 0A 
                    //(COM1)收到字节转换为16进制 - 01 03 02 8B E0 DF 3C 

                    //成功执行写命令27583
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 06 00 00 6B BF E7 4A 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 06 00 00 6B BF E7 4A 

                    //成功执行读命令，结果值：27583
                    //(COM1)发送字节转换为16进制 - 01 03 00 00 00 01 84 0A 
                    //(COM1)收到字节转换为16进制 - 01 03 02 6B BF D6 C4 

                    #endregion

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
                        //byte[] byResultData = new byte[2];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];//接收数据 - 高字节
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];//接收数据 - 低字节

                        //Value = BitConverter.ToInt16(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                //if (ReadDataLength < 1 || ReadDataLength > 125)//读取数据长度，有效值范围：1~250(字节)
                //{
                //    throw new Exception("读取数据长度的值不正确，有效值范围：1~250(字节)");
                //}

                if (BeginAddress + ReadDataLength * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, ReadDataLength);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(ReadDataLength);// 读取数量 -- 输入寄存器数据(short)的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    //发送值[0~9]：21104 20967 32602 3967 -11415 3012 -20926 -6503 -1811 3131
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 0A 14 52 70 51 E7 7F 5A 0F 7F D3 69 0B C4 AE 42 E6 99 F8 ED 0C 3B 39 D1 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 0A 40 0E 

                    //成功执行读命令，结果值：21104 20967 
                    //(COM1)发送字节转换为16进制 - 01 03 00 00 00 02 C4 0B 
                    //(COM1)收到字节转换为16进制 - 01 03 04 52 70 51 E7 96 8A 

                    //发送值[0~9]：11645 19849 18088 6684 -3700 8227 -11502 14386 2582 6354
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 0A 14 2D 7D 4D 89 46 A8 1A 1C F1 8C 20 23 D3 12 38 32 0A 16 18 D2 3E F6 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 0A 40 0E 

                    //成功执行读命令，结果值：11645 19849 
                    //(COM1)发送字节转换为16进制 - 01 03 00 00 00 02 C4 0B 
                    //(COM1)收到字节转换为16进制 - 01 03 04 2D 7D 4D 89 97 B1 

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, 1);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(1);// 读取数量 -- 输入寄存器数据(short)的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    #endregion

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
                        //byte[] byResultData = new byte[2];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];//接收数据 - 高字节
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];//接收数据 - 低字节

                        //Value = BitConverter.ToUInt16(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                //if (ReadDataLength < 1 || ReadDataLength > 125)//读取数据长度，有效值范围：1~250(字节)
                //{
                //    throw new Exception("读取数据长度的值不正确，有效值范围：1~250(字节)");
                //}

                if (BeginAddress + ReadDataLength * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, ReadDataLength);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(ReadDataLength);// 读取数量 -- 输入寄存器数据(short)的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, 2);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(2);// 读取数量 -- 输入寄存器数据(short)的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    //成功执行写命令2135038961
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 02 04 1B F1 7F 42 04 B9 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 02 41 C8 

                    //成功执行读命令，结果值：2135038961
                    //(COM1)发送字节转换为16进制 - 01 03 00 00 00 02 C4 0B 
                    //(COM1)收到字节转换为16进制 - 01 03 04 1B F1 7F 42 0D 25 

                    //成功执行写命令-990422292
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 02 04 5A EC C4 F7 33 C4 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 02 41 C8 

                    //成功执行读命令，结果值：-990422292
                    //(COM1)发送字节转换为16进制 - 01 03 00 00 00 02 C4 0B 
                    //(COM1)收到字节转换为16进制 - 01 03 04 5A EC C4 F7 3A 58 

                    #endregion

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
                        //byte[] byResultData = new byte[4];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];
                        //byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 3];
                        //byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 2];

                        //Value = BitConverter.ToInt32(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                //if (ReadDataLength < 1 || ReadDataLength > 125)
                //{
                //    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                //}

                if (BeginAddress + ReadDataLength * 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, ReadDataLength * 2);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(ReadDataLength * 2);// 读取数量 -- 输入寄存器数据(short)的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    //发送值[0~3]：-1241294755 -1138041830 -565152383 678706776
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 08 10 58 5D B6 03 DC 1A BC 2A 75 81 DE 50 3E 58 28 74 9C F9 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 08 C1 CF 

                    //成功执行读命令，结果值：-1241294755 -1138041830 
                    //(COM1)发送字节转换为16进制 - 01 03 00 00 00 04 44 09 
                    //(COM1)收到字节转换为16进制 - 01 03 08 58 5D B6 03 DC 1A BC 2A A8 96 

                    //发送值[0~3]：1427072877 79318523 -242942681 1406057195
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 08 10 67 6D 55 0F 4D FB 04 BA FD 27 F1 84 BA EB 53 CE CB 71 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 08 C1 CF 

                    //成功执行读命令，结果值：1427072877 79318523 
                    //(COM1)发送字节转换为16进制 - 01 03 00 00 00 04 44 09 
                    //(COM1)收到字节转换为16进制 - 01 03 08 67 6D 55 0F 4D FB 04 BA D3 35 

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, 2);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(2);// 读取数量 -- 输入寄存器数据(short)的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    #endregion

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
                        //byte[] byResultData = new byte[4];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];
                        //byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 3];
                        //byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 2];

                        //Value = BitConverter.ToUInt32(byResultData, 0);
                        //byResultData = null;

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                //if (ReadDataLength < 1 || ReadDataLength > 125)
                //{
                //    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                //}

                if (BeginAddress + ReadDataLength * 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, ReadDataLength * 2);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(ReadDataLength * 2);// 读取数量 -- 输入寄存器数据(short)的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //********************** 

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, 2);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(2);// 读取数量 -- 输入寄存器数据(short)的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    //成功执行写命令8099.143
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 02 04 19 25 45 FD 16 29 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 02 41 C8 

                    //成功执行读命令，结果值：8099.143
                    //(COM1)发送字节转换为16进制 - 01 03 00 00 00 02 C4 0B 
                    //(COM1)收到字节转换为16进制 - 01 03 04 19 25 45 FD 1F B5 

                    //成功执行写命令553.4286
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 02 04 5B 6E 44 0A 32 51 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 02 41 C8 

                    //成功执行读命令，结果值：553.4286
                    //(COM1)发送字节转换为16进制 - 01 03 00 00 00 02 C4 0B 
                    //(COM1)收到字节转换为16进制 - 01 03 04 5B 6E 44 0A 3B CD 

                    #endregion

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
                        //byte[] byResultData = new byte[4];
                        //byResultData[0] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 1];
                        //byResultData[1] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 0];
                        //byResultData[2] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 3];
                        //byResultData[3] = byReadData[iPositionIndexOfReceivedDataInReceivedBytes + 2];

                        //Value = BitConverter.ToSingle(byResultData, 0);
                        //byResultData = null;

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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }
                
                if (BeginAddress + ReadDataLength * 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, ReadDataLength * 2);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(ReadDataLength * 2);// 读取数量 -- 输入寄存器数据(short)的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    //发送值[0~3]：6522 -3920.286 5098 5304.286
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 08 10 D0 00 45 CB 04 92 C5 75 50 00 45 9F C2 49 45 A5 D2 A5 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 08 C1 CF 

                    //成功执行读命令，结果值：6522 -3920.286 
                    //(COM1)发送字节转换为16进制 - 01 03 00 00 00 04 44 09 
                    //(COM1)收到字节转换为16进制 - 01 03 08 D0 00 45 CB 04 92 C5 75 01 A4 

                    //发送值[0~3]：8199.143 8353.714 -5680.857 -7395.714
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 08 10 1C 92 46 00 86 DB 46 02 86 DB C5 B1 1D B7 C5 E7 13 8D 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 08 C1 CF 

                    //成功执行读命令，结果值：8199.143 8353.714 
                    //(COM1)发送字节转换为16进制 - 01 03 00 00 00 04 44 09 
                    //(COM1)收到字节转换为16进制 - 01 03 08 1C 92 46 00 86 DB 46 02 C3 33 

                    #endregion

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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, 4);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(4);// 读取数量 -- 输入寄存器数据(short)的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    //成功执行写命令-1184.28571428571
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 04 08 24 92 92 49 81 24 C0 92 7E BE 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 04 C1 CA 

                    //成功执行读命令，结果值：-1184.28571428571
                    //(COM1)发送字节转换为16进制 - 01 03 00 00 00 04 44 09 
                    //(COM1)收到字节转换为16进制 - 01 03 08 24 92 92 49 81 24 C0 92 5D 13 

                    //成功执行写命令-6963.14285714286
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 04 08 24 92 92 49 33 24 C0 BB 98 18 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 04 C1 CA 

                    //成功执行读命令，结果值：-6963.14285714286
                    //(COM1)发送字节转换为16进制 - 01 03 00 00 00 04 44 09 
                    //(COM1)收到字节转换为16进制 - 01 03 08 24 92 92 49 33 24 C0 BB BB B5 

                    #endregion

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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

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

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, ReadDataLength * 4);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(ReadDataLength * 4);// 读取数量 -- 输入寄存器数据(short)的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    //发送值[0~3]：-3356.28564453125 7258.5712890625 1713.42858886719 -1917.71423339844
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 10 20 00 00 40 00 38 92 C0 AA 00 00 40 00 5A 92 40 BC 00 00 E0 00 C5 B6 40 9A 00 00 60 00 F6 DB C0 9D 1E 2A 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 10 C1 C5 

                    //成功执行读命令，结果值：-3356.28564453125 7258.5712890625 
                    //(COM1)发送字节转换为16进制 - 01 03 00 00 00 08 44 0C 
                    //(COM1)收到字节转换为16进制 - 01 03 10 00 00 40 00 38 92 C0 AA 00 00 40 00 5A 92 40 BC FC E3 

                    //发送值[0~3]：3147.14282226563 -5814.85693359375 -6477.4287109375 8238.5712890625
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 10 20 00 00 20 00 96 49 40 A8 00 00 60 00 B6 DB C0 B6 00 00 C0 00 4D 6D C0 B9 00 00 20 00 17 49 40 C0 F0 F4 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 10 C1 C5 

                    //成功执行读命令，结果值：3147.14282226563 -5814.85693359375 
                    //(COM1)发送字节转换为16进制 - 01 03 00 00 00 08 44 0C 
                    //(COM1)收到字节转换为16进制 - 01 03 10 00 00 20 00 96 49 40 A8 00 00 60 00 B6 DB C0 B6 66 1B 

                    #endregion

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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, 4);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(4);// 读取数量 -- 输入寄存器数据(short)的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    //成功执行写命令1001163748
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 04 08 8B E4 3B AC 00 00 00 00 CF 34 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 04 C1 CA 

                    //成功执行读命令，结果值：1001163748
                    //(COM1)发送字节转换为16进制 - 01 03 00 00 00 04 44 09 
                    //(COM1)收到字节转换为16进制 - 01 03 08 8B E4 3B AC 00 00 00 00 EC 99 

                    //成功执行写命令1847745561
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 04 08 5C 19 6E 22 00 00 00 00 CB 5B 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 04 C1 CA 

                    //成功执行读命令，结果值：1847745561
                    //(COM1)发送字节转换为16进制 - 01 03 00 00 00 04 44 09 
                    //(COM1)收到字节转换为16进制 - 01 03 08 5C 19 6E 22 00 00 00 00 E8 F6 

                    #endregion

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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

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

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, ReadDataLength * 4);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(ReadDataLength * 4);// 读取数量 -- 输入寄存器数据(short)的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    //发送值[0~3]：-772938552 -1550517744 1747907255 1107178448
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 10 20 E4 C8 D1 ED FF FF FF FF FA 10 A3 94 FF FF FF FF F2 B7 68 2E 00 00 00 00 33 D0 41 FE 00 00 00 00 A8 0A 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 10 C1 C5 

                    //成功执行读命令，结果值：-772938552 -1550517744 
                    //(COM1)发送字节转换为16进制 - 01 03 00 00 00 08 44 0C 
                    //(COM1)收到字节转换为16进制 - 01 03 10 E4 C8 D1 ED FF FF FF FF FA 10 A3 94 FF FF FF FF 79 9C 

                    //发送值[0~3]：-163752708 1973914548 -1665239660 -885676542
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 10 20 54 FC F6 3D FF FF FF FF 8B B4 75 A7 00 00 00 00 75 94 9C BE FF FF FF FF A6 02 CB 35 FF FF FF FF F4 80 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 10 C1 C5 

                    //成功执行读命令，结果值：-163752708 1973914548 
                    //(COM1)发送字节转换为16进制 - 01 03 00 00 00 08 44 0C 
                    //(COM1)收到字节转换为16进制 - 01 03 10 54 FC F6 3D FF FF FF FF 8B B4 75 A7 00 00 00 00 15 CD 

                    #endregion

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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, 4);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(4);// 读取数量 -- 输入寄存器数据(short)的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    #endregion

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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

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

                //地址码         功能码    设置起始地址   读取数量      CRC
                //1字节          1字节     2字节          2字节         2字节

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
                //CRC校验计算结果： (2字节)
                
                byte[] byDataToBeSent = PackReadCmd(DeviceAddress, ModbusFuncCode.ReadRegister, BeginAddress, ReadDataLength * 4);

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.ReadRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);// 起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //byte[] byReadDataLength = CConverter.ToBytes(ReadDataLength * 4);// 读取数量 -- 输入寄存器数据(short)的数量
                //byDataToBeSent[4] = byReadDataLength[1];// 读取数量 -- 高字节
                //byDataToBeSent[5] = byReadDataLength[0];// 读取数量 -- 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()
                    //**********************

                    #endregion

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
                    //MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        #endregion

        #endregion

        #region "写 - ok-ok"

        #region "写单个/多个线圈 - ok-ok"

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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                // 单个线圈值的设置
                //0x0000	释放继电器线圈
                //0xFF00	吸合继电器线圈
                byte[] byWriteData = new byte[2];
                if (IsOn == true)
                {
                    byWriteData[0] = 0xFF;//
                    byWriteData[1] = 0X00;//
                }
                else
                {
                    byWriteData[0] = 0x00;//
                    byWriteData[1] = 0X00;//
                }

                byWriteData = CConverter.Reorder2BytesData(byWriteData, WriteCoilBytesFormat, 0);

                byte[] byDataToBeSent = PackSingleWriteCmd(DeviceAddress, ModbusFuncCode.WriteCoil, BeginAddress, byWriteData);

                if (null == byDataToBeSent)
                {
                    return false;
                }

                #region "Old codes -- No use"

                ////CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 数据(0~252字节)
                ////CRC校验计算结果： (2字节)		

                ////Tx:01 0F 00 00 00 10 02 00 00 E2 20
                ////Rx:01 0F 00 00 00 10 54 07

                ////地址码    功能码    设置起始地址   设置值      CRC
                ////1字节     1字节     2字节          2字节      2字节
                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 设置起始地址(2字节) +  设置值(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.WriteCoil;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);//起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                //// 单个线圈值的设置
                ////0x0000	释放继电器线圈
                ////0xFF00	吸合继电器线圈
                //if (IsOn == true)
                //{
                //    byDataToBeSent[4] = 0xFF;//
                //    byDataToBeSent[5] = 0X00;//
                //}
                //else
                //{
                //    byDataToBeSent[4] = 0x00;//
                //    byDataToBeSent[5] = 0X00;//
                //}

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6] = byCRCResult[0];//
                //byDataToBeSent[7] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";// CConverter.BytesToHexStringSplitByChar(byData);

                //if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                ////*********************************
                ////读取的字符串转化为字节数组后CRC计算错误
                //// RS232CPort.Encoding.GetBytes(RS232CPort.ReadExisting());// Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());
                ////*********************************

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //成功：
                    //功能码     设置地址    设置内容
                    //1字节      2字节       2字节
                    //
                    //Tx:01 05 00 00 00 00 CD CA
                    //Rx:01 05 00 00 00 00 CD CA
                    //Tx:01 05 00 00 FF 00 8C 3A
                    //Rx:01 05 00 00 FF 00 8C 3A

                    //********************** 
                    //失败：
                    //功能码(错误码 + 功能码 -- 85)     错误代码
                    //1字节(2个16进制字符)              1字节(2个16进制字符)
                    //
                    //Tx:01 05 00 00 00 00 CD CA
                    //Rx:01 85 01 83 50

                    //**********************                    
                    //[0] - 从站地址
                    //[1] - 功能码(成功)
                    //[1] - 失败的错误码 + 功能码()

                    //**********************
                    //成功执行写命令:True
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 05 00 00 FF 00 8C 3A 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 05 00 00 EF BF BD 00 EF BF BD 3A 

                    //成功执行写命令:False
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 05 00 00 00 00 CD CA 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 05 00 00 00 00 EF BF BD EF BF BD 

                    //成功执行写命令:True
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 05 00 00 FF 00 8C 3A 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 05 00 00 EF BF BD 00 EF BF BD 3A 

                    //成功执行写命令:False
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 05 00 00 00 00 CD CA 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 05 00 00 00 00 EF BF BD EF BF BD 

                    //监控从站实际发送的字节是：
                    //Rx:01 05 00 00 00 00 CD CA
                    //Tx:01 05 00 00 00 00 CD CA

                    //从站收到指令后发送的字节和主站实际上收到的字节内容对应不上，CRC校验通不过，暂时不知道 “EF BF BD”是从哪里来的！
                    //【关键问题】
                    // -- 用 RS232CPort.Encoding.GetBytes(RS232CPort.ReadExisting()) 读取的字符串转化为字节数组后CRC计算错误
                    // -- 用 byte[] byReadData = new byte[RS232CPort.BytesToRead]; RS232CPort.Read(byReadData, 0, byReadData.Length); 这些字节计算 CRC 正确

                    //成功执行写命令:True
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 05 00 00 FF 00 8C 3A 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 05 00 00 FF 00 8C 3A 
                    //成功执行写命令:False
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 05 00 00 00 00 CD CA 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 05 00 00 00 00 CD CA 

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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

                //if (SetValue.Length > 1968)
                //{
                //    throw new Exception("设置值bool[]数组的长度超出范围(1~1968)，请修改参数数组的长度");
                //}

                if (BeginAddress + SetValue.Length > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置内容(N字节)
                //CRC校验计算结果： (2字节)		
                
                //地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      CRC
                //1字节          1字节     2字节          2字节      1字节      N字节         2字节

                // 线圈值的设置 - 将布尔数组转换为字节数组
                byte[] byWriteData = CConverter.BitArrayToByte(SetValue);
                byWriteData = CConverter.ReorderBytesData(byWriteData, WriteCoilBytesFormat, 0);

                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length, byWriteData);

                if (null == byDataToBeSent)
                {
                    return false;
                }

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[7 + SetValue.Length / 8];//地址码(1字节) + 功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置值(N字节) = (7+N)字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.WriteMultiCoil;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);//起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                ////数据位长度 - 位的数据长度
                //int iDataLength = SetValue.Length;
                //byte[] byDataLength = CConverter.ToBytes(iDataLength);
                //byDataToBeSent[4] = byDataLength[1];// 数据长度 - 高字节
                //byDataToBeSent[5] = byDataLength[0];// 数据长度 - 低字节

                //// 字节计数
                //byte byByteCount = Convert.ToByte(iDataLength / 8);//bool[]数组长度 / 8 = 字节数
                //byDataToBeSent[6] = byByteCount;

                //// 线圈值的设置 - 将布尔数组转换为字节数组
                //byte[] byBitArrayToBytes = CConverter.BitArrayToByte(SetValue);
                //for (int i = 0; i < byBitArrayToBytes.Length; i++)
                //{
                //    byDataToBeSent[7 + i] = byBitArrayToBytes[i];
                //}

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[7 + byBitArrayToBytes.Length + 0] = byCRCResult[0];//
                //byDataToBeSent[7 + byBitArrayToBytes.Length + 1] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //byCRCResult = null;

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //发送值[0~15]：False True True False True False True True True False False False False True True False
                    //未成功执行写命令
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 10 02 D6 61 7D A8 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 8F 02 C5 F1 

                    //发送值[0~15]：False False False True True True True False False False False True True False True False
                    //成功执行写命令
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 10 02 78 58 C1 DA 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 10 54 07

                    //发送值[0~15]：True False False False True True False True False False True False False False False False
                    //成功执行写命令
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 10 02 B1 04 97 B3 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 10 54 07 

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ")  通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress + 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置内容(N字节)
                //CRC校验计算结果： (2字节)		

                //地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      CRC
                //1字节          1字节     2字节          2字节      1字节      N字节         2字节

                byte[] byWriteData = new byte[] { SetValue };

                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, 8, byWriteData);

                if (null == byDataToBeSent)
                {
                    return false;
                }

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[7 + 1];//地址码(1字节) + 功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置值(N字节) = (7+N)字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.WriteMultiCoil;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);//起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                ////数据位长度 - 位的数据长度
                //int iDataLength = 8;
                //byte[] byDataLength = CConverter.ToBytes(iDataLength);
                //byDataToBeSent[4] = byDataLength[1];// 数据长度 - 高字节
                //byDataToBeSent[5] = byDataLength[0];// 数据长度 - 低字节

                //// 字节计数
                //byte byByteCount = 0x1;//字节数 = 1
                //byDataToBeSent[6] = byByteCount;

                //// 线圈值的设置 - 字节值
                //byDataToBeSent[7] = SetValue;

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[7 + 1 + 0] = byCRCResult[0];//
                //byDataToBeSent[7 + 1 + 1] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //成功执行写命令131
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 08 01 83 BF 34 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 08 54 0D 

                    //成功执行写命令195
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 08 01 C3 BE C4 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 08 54 0D 

                    //成功执行写命令153
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 08 01 99 3E FF 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 08 54 0D 

                    //成功执行写命令31
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 08 01 1F BF 5D 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 08 54 0D 

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

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

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置内容(N字节)
                //CRC校验计算结果： (2字节)		

                //地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      CRC
                //1字节          1字节     2字节          2字节      1字节      N字节         2字节
                
                byte[] byWriteData = SetValue;
                byWriteData = CConverter.ReorderBytesData(byWriteData, WriteCoilBytesFormat, 0);

                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length * 8, byWriteData);

                if (null == byDataToBeSent)
                {
                    return false;
                }

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[7 + SetValue.Length];//地址码(1字节) + 功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置值(N字节) = (7+N)字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.WriteMultiCoil;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);//起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                ////数据位长度 - 位的数据长度
                //int iDataLength = SetValue.Length * 8;
                //byte[] byDataLength = CConverter.ToBytes(iDataLength);
                //byDataToBeSent[4] = byDataLength[1];// 数据长度 - 高字节
                //byDataToBeSent[5] = byDataLength[0];// 数据长度 - 低字节

                //// 字节计数
                //byte byByteCount = Convert.ToByte(SetValue.Length);//字节数 = N
                //byDataToBeSent[6] = byByteCount;

                //// 线圈值的设置 - 字节值
                //for (int i = 0; i < SetValue.Length; i++)
                //{
                //    byDataToBeSent[7 + i] = SetValue[i];
                //}                

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[7 + SetValue.Length + 0] = byCRCResult[0];//
                //byDataToBeSent[7 + SetValue.Length + 1] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //发送值[0~1]：13 155
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 10 02 0D 9B A7 1B 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 10 54 07 

                    //发送值[0~1]：99 236
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 10 02 63 EC CB 5D 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 10 54 07 

                    //发送值[0~1]：251 86
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 10 02 FB 56 21 2E 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 10 54 07 

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress + 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置内容(N字节)
                //CRC校验计算结果： (2字节)		

                //地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      CRC
                //1字节          1字节     2字节          2字节      1字节      N字节         2字节

                byte[] byWriteData = new byte[] { (byte)SetValue };

                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, 8, byWriteData);

                if (null == byDataToBeSent)
                {
                    return false;
                }

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[7 + 1];//地址码(1字节) + 功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置值(N字节) = (7+N)字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.WriteMultiCoil;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);//起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                ////数据位长度 - 位的数据长度
                //int iDataLength = 8;
                //byte[] byDataLength = CConverter.ToBytes(iDataLength);
                //byDataToBeSent[4] = byDataLength[1];// 数据长度 - 高字节
                //byDataToBeSent[5] = byDataLength[0];// 数据长度 - 低字节

                //// 字节计数
                //byte byByteCount = 0x1;//字节数 = 1
                //byDataToBeSent[6] = byByteCount;

                //// 线圈值的设置 - 字节值
                //byDataToBeSent[7] = (byte)SetValue;

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[7 + 1 + 0] = byCRCResult[0];//
                //byDataToBeSent[7 + 1 + 1] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //成功执行写命令131
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 08 01 83 BF 34 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 08 54 0D 

                    //成功执行写命令195
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 08 01 C3 BE C4 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 08 54 0D 

                    //成功执行写命令153
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 08 01 99 3E FF 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 08 54 0D 

                    //成功执行写命令31
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 08 01 1F BF 5D 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 08 54 0D 

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

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

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置内容(N字节)
                //CRC校验计算结果： (2字节)		

                //地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      CRC
                //1字节          1字节     2字节          2字节      1字节      N字节         2字节
                
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

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[7 + SetValue.Length];//地址码(1字节) + 功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置值(N字节) = (7+N)字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.WriteMultiCoil;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);//起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                ////数据位长度 - 位的数据长度
                //int iDataLength = SetValue.Length * 8;
                //byte[] byDataLength = CConverter.ToBytes(iDataLength);
                //byDataToBeSent[4] = byDataLength[1];// 数据长度 - 高字节
                //byDataToBeSent[5] = byDataLength[0];// 数据长度 - 低字节

                //// 字节计数
                //byte byByteCount = Convert.ToByte(SetValue.Length);//字节数 = N
                //byDataToBeSent[6] = byByteCount;

                //// 线圈值的设置 - 字节值
                //for (int i = 0; i < SetValue.Length; i++)
                //{
                //    byDataToBeSent[7 + i] = (byte)SetValue[i];
                //}

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[7 + SetValue.Length + 0] = byCRCResult[0];//
                //byDataToBeSent[7 + SetValue.Length + 1] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //发送值[0~1]：13 155
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 10 02 0D 9B A7 1B 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 10 54 07 

                    //发送值[0~1]：99 236
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 10 02 63 EC CB 5D 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 10 54 07 

                    //发送值[0~1]：251 86
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 10 02 FB 56 21 2E 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 10 54 07 

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置内容(N字节)
                //CRC校验计算结果： (2字节)		

                //地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      CRC
                //1字节          1字节     2字节          2字节      1字节      N字节         2字节
                
                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);

                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, 2 * 8, byWriteData);

                if (null == byDataToBeSent)
                {
                    return false;
                }

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[7 + 2];//地址码(1字节) + 功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置值(N字节) = (7+N)字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.WriteMultiCoil;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);//起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                ////数据位长度 - 位的数据长度
                //int iDataLength = 8 * 2;
                //byte[] byDataLength = CConverter.ToBytes(iDataLength);
                //byDataToBeSent[4] = byDataLength[1];// 数据长度 - 高字节
                //byDataToBeSent[5] = byDataLength[0];// 数据长度 - 低字节

                //// 字节计数
                //byte byByteCount = 0x2;//字节数 = 2
                //byDataToBeSent[6] = byByteCount;

                //// 线圈值的设置 - 字节值
                //byte[] byShortValueToBytes = CConverter.ToBytes(SetValue);
                //byDataToBeSent[7] = byShortValueToBytes[0];
                //byDataToBeSent[8] = byShortValueToBytes[1];

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[7 + 2 + 0] = byCRCResult[0];//
                //byDataToBeSent[7 + 2 + 1] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //成功执行写命令-5350
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 10 02 1A EB A9 0F 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 10 54 07 

                    //成功执行写命令27375
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 10 02 EF 6A 2E 3F 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 10 54 07 

                    //成功执行写命令-3532
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 10 02 34 F2 75 65 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 10 54 07

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

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

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置内容(N字节)
                //CRC校验计算结果： (2字节)		

                //地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      CRC
                //1字节          1字节     2字节          2字节      1字节      N字节         2字节

                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);

                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length * 2 * 8, byWriteData);

                if (null == byDataToBeSent)
                {
                    return false;
                }

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[7 + SetValue.Length * 2];//地址码(1字节) + 功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置值(N字节) = (7+N)字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.WriteMultiCoil;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);//起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                ////数据位长度 - 位的数据长度
                //int iDataLength = SetValue.Length * 2 * 8;
                //byte[] byDataLength = CConverter.ToBytes(iDataLength);
                //byDataToBeSent[4] = byDataLength[1];// 数据长度 - 高字节
                //byDataToBeSent[5] = byDataLength[0];// 数据长度 - 低字节

                //// 字节计数
                //byte byByteCount = Convert.ToByte(SetValue.Length * 2);//字节数 = N
                //byDataToBeSent[6] = byByteCount;

                //// 线圈值的设置 - 字节值
                //for (int i = 0; i < SetValue.Length; i++)
                //{
                //    byte[] byShortToBytes = CConverter.ToBytes(SetValue[i]);
                //    byDataToBeSent[7 + i * 2 + 0] = byShortToBytes[0];
                //    byDataToBeSent[7 + i * 2 + 1] = byShortToBytes[1];
                //}

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[7 + SetValue.Length * 2 + 0] = byCRCResult[0];// 数据设置值 - 高字节
                //byDataToBeSent[7 + SetValue.Length * 2 + 1] = byCRCResult[1];// 数据设置值 - 低字节

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //发送值[0~1]：13803 19450
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 20 04 EB 35 FA 4B E3 F5 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 20 54 13 

                    //发送值[0~1]：17932 7888
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 20 04 0C 46 D0 1E FB C5 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 20 54 13 

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置内容(N字节)
                //CRC校验计算结果： (2字节)		

                //地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      CRC
                //1字节          1字节     2字节          2字节      1字节      N字节         2字节
                
                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);

                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, 2 * 8, byWriteData);

                if (null == byDataToBeSent)
                {
                    return false;
                }

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[7 + 2];//地址码(1字节) + 功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置值(N字节) = (7+N)字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.WriteMultiCoil;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);//起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                ////数据位长度 - 位的数据长度
                //int iDataLength = 8 * 2;
                //byte[] byDataLength = CConverter.ToBytes(iDataLength);
                //byDataToBeSent[4] = byDataLength[1];// 数据长度 - 高字节
                //byDataToBeSent[5] = byDataLength[0];// 数据长度 - 低字节

                //// 字节计数
                //byte byByteCount = 0x2;//字节数 = 2
                //byDataToBeSent[6] = byByteCount;

                //// 线圈值的设置 - 字节值
                //byte[] byShortValueToBytes = CConverter.ToBytes(SetValue);
                //byDataToBeSent[7] = byShortValueToBytes[0];
                //byDataToBeSent[8] = byShortValueToBytes[1];

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[7 + 2 + 0] = byCRCResult[0];//
                //byDataToBeSent[7 + 2 + 1] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //成功执行写命令-5350
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 10 02 1A EB A9 0F 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 10 54 07 

                    //成功执行写命令27375
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 10 02 EF 6A 2E 3F 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 10 54 07 

                    //成功执行写命令-3532
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 10 02 34 F2 75 65 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 10 54 07

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

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

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置内容(N字节)
                //CRC校验计算结果： (2字节)		

                //地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      CRC
                //1字节          1字节     2字节          2字节      1字节      N字节         2字节
                
                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);

                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length * 2 * 8, byWriteData);

                if (null == byDataToBeSent)
                {
                    return false;
                }

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[7 + SetValue.Length * 2];//地址码(1字节) + 功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置值(N字节) = (7+N)字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.WriteMultiCoil;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);//起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                ////数据位长度 - 位的数据长度
                //int iDataLength = SetValue.Length * 2 * 8;
                //byte[] byDataLength = CConverter.ToBytes(iDataLength);
                //byDataToBeSent[4] = byDataLength[1];// 数据长度 - 高字节
                //byDataToBeSent[5] = byDataLength[0];// 数据长度 - 低字节

                //// 字节计数
                //byte byByteCount = Convert.ToByte(SetValue.Length * 2);//字节数 = N
                //byDataToBeSent[6] = byByteCount;

                //// 线圈值的设置 - 字节值
                //for (int i = 0; i < SetValue.Length; i++)
                //{
                //    byte[] byShortToBytes = CConverter.ToBytes(SetValue[i]);
                //    byDataToBeSent[7 + i * 2 + 0] = byShortToBytes[0];
                //    byDataToBeSent[7 + i * 2 + 1] = byShortToBytes[1];
                //}

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[7 + SetValue.Length * 2 + 0] = byCRCResult[0];// 数据设置值 - 高字节
                //byDataToBeSent[7 + SetValue.Length * 2 + 1] = byCRCResult[1];// 数据设置值 - 低字节

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //发送值[0~1]：13803 19450
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 20 04 EB 35 FA 4B E3 F5 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 20 54 13 

                    //发送值[0~1]：17932 7888
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 20 04 0C 46 D0 1E FB C5 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 20 54 13 

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置内容(N字节)
                //CRC校验计算结果： (2字节)		

                //地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      CRC
                //1字节          1字节     2字节          2字节      1字节      N字节         2字节

                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);

                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, 8 * 4, byWriteData);

                if (null == byDataToBeSent)
                {
                    return false;
                }

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[7 + 4];//地址码(1字节) + 功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置值(N字节) = (7+N)字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.WriteMultiCoil;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);//起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                ////数据位长度 - 位的数据长度
                //int iDataLength = 8 * 4;
                //byte[] byDataLength = CConverter.ToBytes(iDataLength);
                //byDataToBeSent[4] = byDataLength[1];// 数据长度 - 高字节
                //byDataToBeSent[5] = byDataLength[0];// 数据长度 - 低字节

                //// 字节计数
                //byte byByteCount = 0x4;//字节数 = 4
                //byDataToBeSent[6] = byByteCount;

                //// 线圈值的设置 - 字节值
                //byte[] byShortValueToBytes = BitConverter.GetBytes(SetValue);
                //byDataToBeSent[7 + 0] = byShortValueToBytes[0];
                //byDataToBeSent[7 + 1] = byShortValueToBytes[1];
                //byDataToBeSent[7 + 2] = byShortValueToBytes[2];
                //byDataToBeSent[7 + 3] = byShortValueToBytes[3];

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[7 + 4 + 0] = byCRCResult[0];//
                //byDataToBeSent[7 + 4 + 1] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //成功执行写命令330858185
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 20 04 C9 7E B8 13 A8 C1 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 20 54 13 

                    //成功执行读命令，结果值：330858185
                    //(COM1)发送字节转换为16进制 - 01 01 00 00 00 20 3D D2 
                    //(COM1)收到字节转换为16进制 - 01 01 04 C9 7E B8 13 97 98 

                    //成功执行写命令1836795602
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 20 04 D2 46 7B 6D FF 38 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 20 54 13 

                    //成功执行读命令，结果值：1836795602
                    //(COM1)发送字节转换为16进制 - 01 01 00 00 00 20 3D D2 
                    //(COM1)收到字节转换为16进制 - 01 01 04 D2 46 7B 6D C0 61 

                    //成功执行写命令333429455
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 20 04 CF BA DF 13 C3 84 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 20 54 13 

                    //成功执行读命令，结果值：333429455
                    //(COM1)发送字节转换为16进制 - 01 01 00 00 00 20 3D D2 
                    //(COM1)收到字节转换为16进制 - 01 01 04 CF BA DF 13 FC DD 

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

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

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置内容(N字节)
                //CRC校验计算结果： (2字节)		

                //地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      CRC
                //1字节          1字节     2字节          2字节      1字节      N字节         2字节

                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);

                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length * 4 * 8, byWriteData);

                if (null == byDataToBeSent)
                {
                    return false;
                }

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[7 + SetValue.Length * 4];//地址码(1字节) + 功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置值(N字节) = (7+N)字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.WriteMultiCoil;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);//起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                ////数据位长度 - 位的数据长度
                //int iDataLength = SetValue.Length * 4 * 8;
                //byte[] byDataLength = CConverter.ToBytes(iDataLength);
                //byDataToBeSent[4] = byDataLength[1];// 数据长度 - 高字节
                //byDataToBeSent[5] = byDataLength[0];// 数据长度 - 低字节

                //// 字节计数
                //byte byByteCount = Convert.ToByte(SetValue.Length * 4);//字节数 = N
                //byDataToBeSent[6] = byByteCount;

                //// 线圈值的设置 - 字节值
                //for (int i = 0; i < SetValue.Length; i++)
                //{
                //    byte[] byShortToBytes = BitConverter.GetBytes(SetValue[i]);
                //    byDataToBeSent[7 + i * 4 + 0] = byShortToBytes[0];
                //    byDataToBeSent[7 + i * 4 + 1] = byShortToBytes[1];
                //    byDataToBeSent[7 + i * 4 + 2] = byShortToBytes[2];
                //    byDataToBeSent[7 + i * 4 + 3] = byShortToBytes[3];
                //}

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[7 + SetValue.Length * 4 + 0] = byCRCResult[0];// 数据设置值 - 高字节
                //byDataToBeSent[7 + SetValue.Length * 4 + 1] = byCRCResult[1];// 数据设置值 - 低字节

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //发送值[0~3]：-349846368 -1552854674 -1846009266 -547873524
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 80 10 A0 C4 25 EB 6E 51 71 A3 4E 22 F8 91 0C 1D 58 DF B5 48 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 80 54 6B 

                    //成功执行读命令，结果值：-349846368 -1552854674 
                    //(COM1)发送字节转换为16进制 - 01 01 00 00 00 40 3D FA 
                    //(COM1)收到字节转换为16进制 - 01 01 08 A0 C4 25 EB 6E 51 71 A3 B1 9B 

                    //发送值[0~3]：-584133808 1798923771 1162866534 -859036975
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 80 10 50 D3 2E DD FB 65 39 6B 66 EF 4F 45 D1 22 CC CC 64 33 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 80 54 6B 

                    //成功执行读命令，结果值：-584133808 1798923771 
                    //(COM1)发送字节转换为16进制 - 01 01 00 00 00 40 3D FA 
                    //(COM1)收到字节转换为16进制 - 01 01 08 50 D3 2E DD FB 65 39 6B 4B 35 

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置内容(N字节)
                //CRC校验计算结果： (2字节)		

                //地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      CRC
                //1字节          1字节     2字节          2字节      1字节      N字节         2字节

                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);

                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, 8 * 4, byWriteData);

                if (null == byDataToBeSent)
                {
                    return false;
                }

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[7 + 4];//地址码(1字节) + 功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置值(N字节) = (7+N)字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.WriteMultiCoil;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);//起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                ////数据位长度 - 位的数据长度
                //int iDataLength = 8 * 4;
                //byte[] byDataLength = CConverter.ToBytes(iDataLength);
                //byDataToBeSent[4] = byDataLength[1];// 数据长度 - 高字节
                //byDataToBeSent[5] = byDataLength[0];// 数据长度 - 低字节

                //// 字节计数
                //byte byByteCount = 0x4;//字节数 = 4
                //byDataToBeSent[6] = byByteCount;

                //// 线圈值的设置 - 字节值
                //byte[] byShortValueToBytes = BitConverter.GetBytes(SetValue);
                //byDataToBeSent[7 + 0] = byShortValueToBytes[0];
                //byDataToBeSent[7 + 1] = byShortValueToBytes[1];
                //byDataToBeSent[7 + 2] = byShortValueToBytes[2];
                //byDataToBeSent[7 + 3] = byShortValueToBytes[3];

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[7 + 4 + 0] = byCRCResult[0];//
                //byDataToBeSent[7 + 4 + 1] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

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

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置内容(N字节)
                //CRC校验计算结果： (2字节)		

                //地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      CRC
                //1字节          1字节     2字节          2字节      1字节      N字节         2字节

                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);

                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length * 4 * 8, byWriteData);

                if (null == byDataToBeSent)
                {
                    return false;
                }

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[7 + SetValue.Length * 4];//地址码(1字节) + 功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置值(N字节) = (7+N)字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.WriteMultiCoil;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);//起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                ////数据位长度 - 位的数据长度
                //int iDataLength = SetValue.Length * 4 * 8;
                //byte[] byDataLength = CConverter.ToBytes(iDataLength);
                //byDataToBeSent[4] = byDataLength[1];// 数据长度 - 高字节
                //byDataToBeSent[5] = byDataLength[0];// 数据长度 - 低字节

                //// 字节计数
                //byte byByteCount = Convert.ToByte(SetValue.Length * 4);//字节数 = N
                //byDataToBeSent[6] = byByteCount;

                //// 线圈值的设置 - 字节值
                //for (int i = 0; i < SetValue.Length; i++)
                //{
                //    byte[] byShortToBytes = BitConverter.GetBytes(SetValue[i]);
                //    byDataToBeSent[7 + i * 4 + 0] = byShortToBytes[0];
                //    byDataToBeSent[7 + i * 4 + 1] = byShortToBytes[1];
                //    byDataToBeSent[7 + i * 4 + 2] = byShortToBytes[2];
                //    byDataToBeSent[7 + i * 4 + 3] = byShortToBytes[3];
                //}

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[7 + SetValue.Length * 4 + 0] = byCRCResult[0];// 数据设置值 - 高字节
                //byDataToBeSent[7 + SetValue.Length * 4 + 1] = byCRCResult[1];// 数据设置值 - 低字节

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        #region "Hide"

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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置内容(N字节)
                //CRC校验计算结果： (2字节)		

                //地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      CRC
                //1字节          1字节     2字节          2字节      1字节      N字节         2字节

                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);

                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, 8 * 4, byWriteData);

                if (null == byDataToBeSent)
                {
                    return false;
                }

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[7 + 4];//地址码(1字节) + 功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置值(N字节) = (7+N)字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.WriteMultiCoil;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);//起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                ////数据位长度 - 位的数据长度
                //int iDataLength = 8 * 4;
                //byte[] byDataLength = CConverter.ToBytes(iDataLength);
                //byDataToBeSent[4] = byDataLength[1];// 数据长度 - 高字节
                //byDataToBeSent[5] = byDataLength[0];// 数据长度 - 低字节

                //// 字节计数
                //byte byByteCount = 0x4;//字节数 = 4
                //byDataToBeSent[6] = byByteCount;

                //// 线圈值的设置 - 字节值
                //byte[] byShortValueToBytes = BitConverter.GetBytes(SetValue);
                //byDataToBeSent[7] = byShortValueToBytes[0];
                //byDataToBeSent[8] = byShortValueToBytes[1];
                //byDataToBeSent[9] = byShortValueToBytes[2];
                //byDataToBeSent[10] = byShortValueToBytes[3];

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[7 + 4 + 0] = byCRCResult[0];//
                //byDataToBeSent[7 + 4 + 1] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

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

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置内容(N字节)
                //CRC校验计算结果： (2字节)		

                //地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      CRC
                //1字节          1字节     2字节          2字节      1字节      N字节         2字节

                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);

                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length * 4 * 8, byWriteData);

                if (null == byDataToBeSent)
                {
                    return false;
                }

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[7 + SetValue.Length * 4];//地址码(1字节) + 功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置值(N字节) = (7+N)字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.WriteMultiCoil;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);//起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                ////数据位长度 - 位的数据长度
                //int iDataLength = SetValue.Length * 4 * 8;
                //byte[] byDataLength = CConverter.ToBytes(iDataLength);
                //byDataToBeSent[4] = byDataLength[1];// 数据长度 - 高字节
                //byDataToBeSent[5] = byDataLength[0];// 数据长度 - 低字节

                //// 字节计数
                //byte byByteCount = Convert.ToByte(SetValue.Length * 4);//字节数 = N
                //byDataToBeSent[6] = byByteCount;

                //// 线圈值的设置 - 字节值
                //for (int i = 0; i < SetValue.Length; i++)
                //{
                //    byte[] byShortToBytes = BitConverter.GetBytes(SetValue[i]);
                //    byDataToBeSent[7 + i * 4 + 0] = byShortToBytes[0];
                //    byDataToBeSent[7 + i * 4 + 1] = byShortToBytes[1];
                //    byDataToBeSent[7 + i * 4 + 2] = byShortToBytes[2];
                //    byDataToBeSent[7 + i * 4 + 3] = byShortToBytes[3];
                //}

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[7 + SetValue.Length * 4 + 0] = byCRCResult[0];// 数据设置值 - 高字节
                //byDataToBeSent[7 + SetValue.Length * 4 + 1] = byCRCResult[1];// 数据设置值 - 低字节

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置内容(N字节)
                //CRC校验计算结果： (2字节)		

                //地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      CRC
                //1字节          1字节     2字节          2字节      1字节      N字节         2字节

                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);

                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, 8 * 8, byWriteData);

                if (null == byDataToBeSent)
                {
                    return false;
                }

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[7 + 8];//地址码(1字节) + 功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置值(N字节) = (7+N)字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.WriteMultiCoil;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);//起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                ////数据位长度 - 位的数据长度
                //int iDataLength = 8 * 8;
                //byte[] byDataLength = CConverter.ToBytes(iDataLength);
                //byDataToBeSent[4] = byDataLength[1];// 数据长度 - 高字节
                //byDataToBeSent[5] = byDataLength[0];// 数据长度 - 低字节

                //// 字节计数
                //byte byByteCount = 0x8;//字节数 = 8
                //byDataToBeSent[6] = byByteCount;

                //// 线圈值的设置 - 字节值
                //byte[] byShortValueToBytes = BitConverter.GetBytes(SetValue);
                //byDataToBeSent[7 + 0] = byShortValueToBytes[0];
                //byDataToBeSent[7 + 1] = byShortValueToBytes[1];
                //byDataToBeSent[7 + 2] = byShortValueToBytes[2];
                //byDataToBeSent[7 + 3] = byShortValueToBytes[3];
                //byDataToBeSent[7 + 4] = byShortValueToBytes[4];
                //byDataToBeSent[7 + 5] = byShortValueToBytes[5];
                //byDataToBeSent[7 + 6] = byShortValueToBytes[6];
                //byDataToBeSent[7 + 7] = byShortValueToBytes[7];

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[7 + 8 + 0] = byCRCResult[0];//
                //byDataToBeSent[7 + 8 + 1] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        // -
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

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

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置内容(N字节)
                //CRC校验计算结果： (2字节)		

                //地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      CRC
                //1字节          1字节     2字节          2字节      1字节      N字节         2字节

                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);

                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length * 8 * 8, byWriteData);

                if (null == byDataToBeSent)
                {
                    return false;
                }

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[7 + SetValue.Length * 8];//地址码(1字节) + 功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置值(N字节) = (7+N)字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.WriteMultiCoil;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);//起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                ////数据位长度 - 位的数据长度
                //int iDataLength = SetValue.Length * 8 * 8;
                //byte[] byDataLength = CConverter.ToBytes(iDataLength);
                //byDataToBeSent[4] = byDataLength[1];// 数据长度 - 高字节
                //byDataToBeSent[5] = byDataLength[0];// 数据长度 - 低字节

                //// 字节计数
                //byte byByteCount = Convert.ToByte(SetValue.Length * 8);//字节数 = N
                //byDataToBeSent[6] = byByteCount;

                //// 线圈值的设置 - 字节值
                //for (int i = 0; i < SetValue.Length; i++)
                //{
                //    byte[] byShortToBytes = BitConverter.GetBytes(SetValue[i]);
                //    byDataToBeSent[7 + i * 8 + 0] = byShortToBytes[0];
                //    byDataToBeSent[7 + i * 8 + 1] = byShortToBytes[1];
                //    byDataToBeSent[7 + i * 8 + 2] = byShortToBytes[2];
                //    byDataToBeSent[7 + i * 8 + 3] = byShortToBytes[3];
                //    byDataToBeSent[7 + i * 8 + 4] = byShortToBytes[4];
                //    byDataToBeSent[7 + i * 8 + 5] = byShortToBytes[5];
                //    byDataToBeSent[7 + i * 8 + 6] = byShortToBytes[6];
                //    byDataToBeSent[7 + i * 8 + 7] = byShortToBytes[7];
                //}

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[7 + SetValue.Length * 4 + 0] = byCRCResult[0];// 数据设置值 - 高字节
                //byDataToBeSent[7 + SetValue.Length * 4 + 1] = byCRCResult[1];// 数据设置值 - 低字节

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置内容(N字节)
                //CRC校验计算结果： (2字节)		

                //地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      CRC
                //1字节          1字节     2字节          2字节      1字节      N字节         2字节

                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);

                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, 8 * 8, byWriteData);

                if (null == byDataToBeSent)
                {
                    return false;
                }

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[7 + 8];//地址码(1字节) + 功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置值(N字节) = (7+N)字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.WriteMultiCoil;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);//起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                ////数据位长度 - 位的数据长度
                //int iDataLength = 8 * 8;
                //byte[] byDataLength = CConverter.ToBytes(iDataLength);
                //byDataToBeSent[4] = byDataLength[1];// 数据长度 - 高字节
                //byDataToBeSent[5] = byDataLength[0];// 数据长度 - 低字节

                //// 字节计数
                //byte byByteCount = 0x8;//字节数 = 8
                //byDataToBeSent[6] = byByteCount;

                //// 线圈值的设置 - 字节值
                //byte[] byShortValueToBytes = BitConverter.GetBytes(SetValue);
                //byDataToBeSent[7 + 0] = byShortValueToBytes[0];
                //byDataToBeSent[7 + 1] = byShortValueToBytes[1];
                //byDataToBeSent[7 + 2] = byShortValueToBytes[2];
                //byDataToBeSent[7 + 3] = byShortValueToBytes[3];
                //byDataToBeSent[7 + 4] = byShortValueToBytes[4];
                //byDataToBeSent[7 + 5] = byShortValueToBytes[5];
                //byDataToBeSent[7 + 6] = byShortValueToBytes[6];
                //byDataToBeSent[7 + 7] = byShortValueToBytes[7];

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[7 + 8 + 0] = byCRCResult[0];//
                //byDataToBeSent[7 + 8 + 1] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //成功执行写命令-254255828
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 40 08 2C 5D D8 F0 FF FF FF FF 36 75 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 40 54 3B
 
                    //成功执行读命令，结果值：-254255828
                    //(COM1)发送字节转换为16进制 - 01 01 00 00 00 40 3D FA 
                    //(COM1)收到字节转换为16进制 - 01 01 08 2C 5D D8 F0 FF FF FF FF E9 CC 

                    //成功执行写命令322514101
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 40 08 B5 2C 39 13 00 00 00 00 4D 87 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 40 54 3B 

                    //成功执行读命令，结果值：322514101
                    //(COM1)发送字节转换为16进制 - 01 01 00 00 00 40 3D FA 
                    //(COM1)收到字节转换为16进制 - 01 01 08 B5 2C 39 13 00 00 00 00 92 3E 

                    //成功执行写命令-2104965112
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 40 08 08 C8 88 82 FF FF FF FF C4 CC 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 40 54 3B 

                    //成功执行读命令，结果值：-2104965112
                    //(COM1)发送字节转换为16进制 - 01 01 00 00 00 40 3D FA 
                    //(COM1)收到字节转换为16进制 - 01 01 08 08 C8 88 82 FF FF FF FF 1B 75 

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

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

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置内容(N字节)
                //CRC校验计算结果： (2字节)		

                //地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      CRC
                //1字节          1字节     2字节          2字节      1字节      N字节         2字节

                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);

                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length * 8 * 8, byWriteData);

                if (null == byDataToBeSent)
                {
                    return false;
                }

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[7 + SetValue.Length * 8];//地址码(1字节) + 功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置值(N字节) = (7+N)字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.WriteMultiCoil;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);//起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                ////数据位长度 - 位的数据长度
                //int iDataLength = SetValue.Length * 8 * 8;
                //byte[] byDataLength = CConverter.ToBytes(iDataLength);
                //byDataToBeSent[4] = byDataLength[1];// 数据长度 - 高字节
                //byDataToBeSent[5] = byDataLength[0];// 数据长度 - 低字节

                //// 字节计数
                //byte byByteCount = Convert.ToByte(SetValue.Length * 8);//字节数 = N
                //byDataToBeSent[6] = byByteCount;

                //// 线圈值的设置 - 字节值
                //for (int i = 0; i < SetValue.Length; i++)
                //{
                //    byte[] byShortToBytes = BitConverter.GetBytes(SetValue[i]);
                //    byDataToBeSent[7 + i * 8 + 0] = byShortToBytes[0];
                //    byDataToBeSent[7 + i * 8 + 1] = byShortToBytes[1];
                //    byDataToBeSent[7 + i * 8 + 2] = byShortToBytes[2];
                //    byDataToBeSent[7 + i * 8 + 3] = byShortToBytes[3];
                //    byDataToBeSent[7 + i * 8 + 4] = byShortToBytes[4];
                //    byDataToBeSent[7 + i * 8 + 5] = byShortToBytes[5];
                //    byDataToBeSent[7 + i * 8 + 6] = byShortToBytes[6];
                //    byDataToBeSent[7 + i * 8 + 7] = byShortToBytes[7];
                //}

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[7 + SetValue.Length * 8 + 0] = byCRCResult[0];// 数据设置值 - 高字节
                //byDataToBeSent[7 + SetValue.Length * 8 + 1] = byCRCResult[1];// 数据设置值 - 低字节

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //发送值[0~1]：1744723392 -1266176702
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 80 10 C0 5D FE 67 00 00 00 00 42 AD 87 B4 FF FF FF FF F2 60 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 80 54 6B 

                    //成功执行读命令，结果值：1744723392 -1266176702 
                    //(COM1)发送字节转换为16进制 - 01 01 00 00 00 80 3D AA 
                    //(COM1)收到字节转换为16进制 - 01 01 10 C0 5D FE 67 00 00 00 00 42 AD 87 B4 FF FF FF FF AB 8B 

                    //发送值[0~1]：1808329480 -1474867894
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 80 10 08 EB C8 6B 00 00 00 00 4A 4D 17 A8 FF FF FF FF 98 80 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 80 54 6B 

                    //成功执行读命令，结果值：1808329480 -1474867894 
                    //(COM1)发送字节转换为16进制 - 01 01 00 00 00 80 3D AA 
                    //(COM1)收到字节转换为16进制 - 01 01 10 08 EB C8 6B 00 00 00 00 4A 4D 17 A8 FF FF FF FF C1 6B 

                    //发送值[0~1]：179433055 1612635156
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 80 10 5F EE B1 0A 00 00 00 00 14 DC 1E 60 00 00 00 00 3C B1 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 80 54 6B 

                    //成功执行读命令，结果值：179433055 1612635156 
                    //(COM1)发送字节转换为16进制 - 01 01 00 00 00 80 3D AA 
                    //(COM1)收到字节转换为16进制 - 01 01 10 5F EE B1 0A 00 00 00 00 14 DC 1E 60 00 00 00 00 65 5A 

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置内容(N字节)
                //CRC校验计算结果： (2字节)		

                //地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      CRC
                //1字节          1字节     2字节          2字节      1字节      N字节         2字节

                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);

                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, 8 * 8, byWriteData);

                if (null == byDataToBeSent)
                {
                    return false;
                }

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[7 + 8];//地址码(1字节) + 功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置值(N字节) = (7+N)字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.WriteMultiCoil;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);//起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                ////数据位长度 - 位的数据长度
                //int iDataLength = 8 * 8;
                //byte[] byDataLength = CConverter.ToBytes(iDataLength);
                //byDataToBeSent[4] = byDataLength[1];// 数据长度 - 高字节
                //byDataToBeSent[5] = byDataLength[0];// 数据长度 - 低字节

                //// 字节计数
                //byte byByteCount = 0x8;//字节数 = 8
                //byDataToBeSent[6] = byByteCount;

                //// 线圈值的设置 - 字节值
                //byte[] byShortValueToBytes = BitConverter.GetBytes(SetValue);
                //byDataToBeSent[7 + 0] = byShortValueToBytes[0];
                //byDataToBeSent[7 + 1] = byShortValueToBytes[1];
                //byDataToBeSent[7 + 2] = byShortValueToBytes[2];
                //byDataToBeSent[7 + 3] = byShortValueToBytes[3];
                //byDataToBeSent[7 + 4] = byShortValueToBytes[4];
                //byDataToBeSent[7 + 5] = byShortValueToBytes[5];
                //byDataToBeSent[7 + 6] = byShortValueToBytes[6];
                //byDataToBeSent[7 + 7] = byShortValueToBytes[7];

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[7 + 8 + 0] = byCRCResult[0];//
                //byDataToBeSent[7 + 8 + 1] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //成功执行写命令-254255828
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 40 08 2C 5D D8 F0 FF FF FF FF 36 75 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 40 54 3B
 
                    //成功执行读命令，结果值：-254255828
                    //(COM1)发送字节转换为16进制 - 01 01 00 00 00 40 3D FA 
                    //(COM1)收到字节转换为16进制 - 01 01 08 2C 5D D8 F0 FF FF FF FF E9 CC 

                    //成功执行写命令322514101
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 40 08 B5 2C 39 13 00 00 00 00 4D 87 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 40 54 3B 

                    //成功执行读命令，结果值：322514101
                    //(COM1)发送字节转换为16进制 - 01 01 00 00 00 40 3D FA 
                    //(COM1)收到字节转换为16进制 - 01 01 08 B5 2C 39 13 00 00 00 00 92 3E 

                    //成功执行写命令-2104965112
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 40 08 08 C8 88 82 FF FF FF FF C4 CC 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 40 54 3B 

                    //成功执行读命令，结果值：-2104965112
                    //(COM1)发送字节转换为16进制 - 01 01 00 00 00 40 3D FA 
                    //(COM1)收到字节转换为16进制 - 01 01 08 08 C8 88 82 FF FF FF FF 1B 75 

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

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

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置内容(N字节)
                //CRC校验计算结果： (2字节)		

                //地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      CRC
                //1字节          1字节     2字节          2字节      1字节      N字节         2字节

                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteCoilBytesFormat);

                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiCoil, BeginAddress, SetValue.Length * 8 * 8, byWriteData);

                if (null == byDataToBeSent)
                {
                    return false;
                }

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[7 + SetValue.Length * 8];//地址码(1字节) + 功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置值(N字节) = (7+N)字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.WriteMultiCoil;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);//起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                ////数据位长度 - 位的数据长度
                //int iDataLength = SetValue.Length * 8 * 8;
                //byte[] byDataLength = CConverter.ToBytes(iDataLength);
                //byDataToBeSent[4] = byDataLength[1];// 数据长度 - 高字节
                //byDataToBeSent[5] = byDataLength[0];// 数据长度 - 低字节

                //// 字节计数
                //byte byByteCount = Convert.ToByte(SetValue.Length * 8);//字节数 = N
                //byDataToBeSent[6] = byByteCount;

                //// 线圈值的设置 - 字节值
                //for (int i = 0; i < SetValue.Length; i++)
                //{
                //    byte[] byShortToBytes = BitConverter.GetBytes(SetValue[i]);
                //    byDataToBeSent[7 + i * 8 + 0] = byShortToBytes[0];
                //    byDataToBeSent[7 + i * 8 + 1] = byShortToBytes[1];
                //    byDataToBeSent[7 + i * 8 + 2] = byShortToBytes[2];
                //    byDataToBeSent[7 + i * 8 + 3] = byShortToBytes[3];
                //    byDataToBeSent[7 + i * 8 + 4] = byShortToBytes[4];
                //    byDataToBeSent[7 + i * 8 + 5] = byShortToBytes[5];
                //    byDataToBeSent[7 + i * 8 + 6] = byShortToBytes[6];
                //    byDataToBeSent[7 + i * 8 + 7] = byShortToBytes[7];
                //}

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[7 + SetValue.Length * 8 + 0] = byCRCResult[0];// 数据设置值 - 高字节
                //byDataToBeSent[7 + SetValue.Length * 8 + 1] = byCRCResult[1];// 数据设置值 - 低字节

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //发送值[0~1]：1744723392 -1266176702
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 80 10 C0 5D FE 67 00 00 00 00 42 AD 87 B4 FF FF FF FF F2 60 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 80 54 6B 

                    //成功执行读命令，结果值：1744723392 -1266176702 
                    //(COM1)发送字节转换为16进制 - 01 01 00 00 00 80 3D AA 
                    //(COM1)收到字节转换为16进制 - 01 01 10 C0 5D FE 67 00 00 00 00 42 AD 87 B4 FF FF FF FF AB 8B 

                    //发送值[0~1]：1808329480 -1474867894
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 80 10 08 EB C8 6B 00 00 00 00 4A 4D 17 A8 FF FF FF FF 98 80 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 80 54 6B 

                    //成功执行读命令，结果值：1808329480 -1474867894 
                    //(COM1)发送字节转换为16进制 - 01 01 00 00 00 80 3D AA 
                    //(COM1)收到字节转换为16进制 - 01 01 10 08 EB C8 6B 00 00 00 00 4A 4D 17 A8 FF FF FF FF C1 6B 

                    //发送值[0~1]：179433055 1612635156
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 0F 00 00 00 80 10 5F EE B1 0A 00 00 00 00 14 DC 1E 60 00 00 00 00 3C B1 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 0F 00 00 00 80 54 6B 

                    //成功执行读命令，结果值：179433055 1612635156 
                    //(COM1)发送字节转换为16进制 - 01 01 00 00 00 80 3D AA 
                    //(COM1)收到字节转换为16进制 - 01 01 10 5F EE B1 0A 00 00 00 00 14 DC 1E 60 00 00 00 00 65 5A 

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        #endregion

        #region "写单个/多个保持寄存器 - ok-ok"

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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //地址码     功能码    设置起始地址   设置值      CRC
                //1字节      1字节     2字节          2字节      2字节
                
                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);

                byte[] byDataToBeSent = PackSingleWriteCmd(DeviceAddress, ModbusFuncCode.WriteRegister, BeginAddress, byWriteData);

                if (null == byDataToBeSent)
                {
                    return false;
                }

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 设置起始地址(2字节) + 设置值(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.WriteRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);//起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                ////值 - 字节值
                //byte[] byShortValueToBytes = CConverter.ToBytes(SetValue);
                //byDataToBeSent[4] = byShortValueToBytes[1];// 数据长度 - 高字节
                //byDataToBeSent[5] = byShortValueToBytes[0];// 数据长度 - 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6 + 0] = byCRCResult[0];//
                //byDataToBeSent[6 + 1] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //成功执行写命令28688
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 06 00 00 70 10 AD C6 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 06 00 00 70 10 AD C6 

                    //成功执行写命令32339
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 06 00 00 7E 53 E8 57 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 06 00 00 7E 53 E8 57 

                    //成功执行写命令-20030
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 06 00 00 B1 C2 7C 0B 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 06 00 00 B1 C2 7C 0B 

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

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

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置内容(N字节)
                //CRC校验计算结果： (2字节)		

                //地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      CRC
                //1字节          1字节     2字节          2字节      1字节      N字节         2字节

                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);

                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, SetValue.Length, byWriteData);

                if (null == byDataToBeSent)
                {
                    return false;
                }

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[7 + SetValue.Length * 2];//地址码(1字节) + 功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置值(N字节) = (7+N)字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.WriteMultiRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);//起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                ////数据长度 - 字的数据长度
                //int iDataLength = SetValue.Length;
                //byte[] byDataLength = CConverter.ToBytes(iDataLength);
                //byDataToBeSent[4] = byDataLength[1];// 数据长度 - 高字节
                //byDataToBeSent[5] = byDataLength[0];// 数据长度 - 低字节

                //// 字节计数
                //byte byByteCount = Convert.ToByte(SetValue.Length * 2);//字节数 = N
                //byDataToBeSent[6] = byByteCount;

                //// 线圈值的设置 - 字节值
                //for (int i = 0; i < SetValue.Length; i++)
                //{
                //    byte[] byShortToBytes = CConverter.ToBytes(SetValue[i]);
                //    byDataToBeSent[7 + i * 2] = byShortToBytes[1];
                //    byDataToBeSent[7 + i * 2 + 1] = byShortToBytes[0];
                //}

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[7 + SetValue.Length * 2 + 0] = byCRCResult[0];// 数据设置值 - 高字节
                //byDataToBeSent[7 + SetValue.Length * 2 + 1] = byCRCResult[1];// 数据设置值 - 低字节

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //Error:
                    //发送值[0~9]：11049 6692 1464 -15418 -12909 -25756 11528 -13835 13744 -23536
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 A0 14 29 2B 24 1A B8 05 C6 C3 93 CD 64 9B 08 2D F5 C9 B0 35 10 A4 01 D4 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 90 03 0C 01 

                    //Error:
                    //发送值[0~9]：41 -5813 -14181 -28527 -10040 -3822 31816 27954 -21839 16148
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 A0 14 29 00 4B E9 9B C8 91 90 C8 D8 12 F1 48 7C 32 6D B1 AA 14 3F 71 27 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 90 03 0C 01 

                    //Error:
                    //发送值[0~9]：-29364 13964 -10164 -30340 -12277 17692 23901 -26660 -19025 23144 
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 A0 14 8D 4C 36 8C D8 4C 89 7C D0 0B 45 1C 5D 5D 97 DC B5 AF 5A 68 7A B1 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 90 03 0C 01 

                    //OK
                    //发送值[0~9]：-8789 -30641 -12786 -26750 13275 25578 26799 13574 13915 -13584 
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 0A 14 DD AB 88 4F CE 0E 97 82 33 DB 63 EA 68 AF 35 06 36 5B CA F0 61 FD 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 0A 40 0E 

                    //OK
                    //发送值[0~9]：26594 22339 14263 19038 -2434 18338 -495 5597 834 20849 
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 0A 14 67 E2 57 43 37 B7 4A 5E F6 7E 47 A2 FE 11 15 DD 03 42 51 71 64 79 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 0A 40 0E 

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //地址码     功能码    设置起始地址   设置值      CRC
                //1字节      1字节     2字节          2字节      2字节
                
                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);

                byte[] byDataToBeSent = PackSingleWriteCmd(DeviceAddress, ModbusFuncCode.WriteRegister, BeginAddress, byWriteData);

                if (null == byDataToBeSent)
                {
                    return false;
                }

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 设置起始地址(2字节) + 设置值(2字节) = 6字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.WriteRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress, BytesFormat.BADC);//起始地址(2字节)
                ////byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                ////byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                ////值 - 字节值
                //byte[] byShortValueToBytes = CConverter.ToBytes(SetValue, BytesFormat.BADC);
                ////byDataToBeSent[4] = byShortValueToBytes[1];// 数据长度 - 高字节
                ////byDataToBeSent[5] = byShortValueToBytes[0];// 数据长度 - 低字节

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[6 + 0] = byCRCResult[0];//
                //byDataToBeSent[6 + 1] = byCRCResult[1];//

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //成功执行写命令28688
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 06 00 00 70 10 AD C6 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 06 00 00 70 10 AD C6 

                    //成功执行写命令32339
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 06 00 00 7E 53 E8 57 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 06 00 00 7E 53 E8 57 

                    //成功执行写命令-20030
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 06 00 00 B1 C2 7C 0B 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 06 00 00 B1 C2 7C 0B 

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

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

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置内容(N字节)
                //CRC校验计算结果： (2字节)		

                //地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      CRC
                //1字节          1字节     2字节          2字节      1字节      N字节         2字节
                
                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);

                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, SetValue.Length, byWriteData);

                if (null == byDataToBeSent)
                {
                    return false;
                }

                #region "Old codes -- No use"

                //byte[] byDataToBeSent = new byte[7 + SetValue.Length * 2];//地址码(1字节) + 功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置值(N字节) = (7+N)字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.WriteMultiRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);//起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                ////数据长度 - 字的数据长度
                //int iDataLength = SetValue.Length;
                //byte[] byDataLength = CConverter.ToBytes(iDataLength);
                //byDataToBeSent[4] = byDataLength[1];// 数据长度 - 高字节
                //byDataToBeSent[5] = byDataLength[0];// 数据长度 - 低字节

                //// 字节计数
                //byte byByteCount = Convert.ToByte(SetValue.Length * 2);//字节数 = N
                //byDataToBeSent[6] = byByteCount;

                //// 线圈值的设置 - 字节值
                //for (int i = 0; i < SetValue.Length; i++)
                //{
                //    byte[] byShortToBytes = CConverter.ToBytes(SetValue[i]);
                //    byDataToBeSent[7 + i * 2] = byShortToBytes[1];
                //    byDataToBeSent[7 + i * 2 + 1] = byShortToBytes[0];
                //}

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[7 + SetValue.Length * 2 + 0] = byCRCResult[0];// 数据设置值 - 高字节
                //byDataToBeSent[7 + SetValue.Length * 2 + 1] = byCRCResult[1];// 数据设置值 - 低字节

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //Error:
                    //发送值[0~9]：11049 6692 1464 -15418 -12909 -25756 11528 -13835 13744 -23536
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 A0 14 29 2B 24 1A B8 05 C6 C3 93 CD 64 9B 08 2D F5 C9 B0 35 10 A4 01 D4 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 90 03 0C 01 

                    //Error:
                    //发送值[0~9]：41 -5813 -14181 -28527 -10040 -3822 31816 27954 -21839 16148
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 A0 14 29 00 4B E9 9B C8 91 90 C8 D8 12 F1 48 7C 32 6D B1 AA 14 3F 71 27 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 90 03 0C 01 

                    //Error:
                    //发送值[0~9]：-29364 13964 -10164 -30340 -12277 17692 23901 -26660 -19025 23144 
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 A0 14 8D 4C 36 8C D8 4C 89 7C D0 0B 45 1C 5D 5D 97 DC B5 AF 5A 68 7A B1 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 90 03 0C 01 

                    //OK
                    //发送值[0~9]：-8789 -30641 -12786 -26750 13275 25578 26799 13574 13915 -13584 
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 0A 14 DD AB 88 4F CE 0E 97 82 33 DB 63 EA 68 AF 35 06 36 5B CA F0 61 FD 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 0A 40 0E 

                    //OK
                    //发送值[0~9]：26594 22339 14263 19038 -2434 18338 -495 5597 834 20849 
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 0A 14 67 E2 57 43 37 B7 4A 5E F6 7E 47 A2 FE 11 15 DD 03 42 51 71 64 79 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 0A 40 0E 

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置内容(N字节)
                //CRC校验计算结果： (2字节)		

                //地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      CRC
                //1字节          1字节     2字节          2字节      1字节      N字节         2字节
                
                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);

                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, 2, byWriteData);

                if (null == byDataToBeSent)
                {
                    return false;
                }

                #region "Old codes -- No use"

                ////float = 2个short，所以这里转换为字节数量：2 * 2
                //byte[] byDataToBeSent = new byte[7 + 2 * 2];//地址码(1字节) + 功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置值(N字节) = (7+N)字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.WriteMultiRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);//起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                ////数据长度 - 字的数据长度
                //int iDataLength = 2;
                //byte[] byDataLength = CConverter.ToBytes(iDataLength);
                //byDataToBeSent[4] = byDataLength[1];// 数据长度 - 高字节
                //byDataToBeSent[5] = byDataLength[0];// 数据长度 - 低字节

                //// 字节计数
                //byte byByteCount = Convert.ToByte(2 * 2);//字节数 = float = 2个short，所以这里转换为字节数量：2 * 2
                //byDataToBeSent[6] = byByteCount;

                //// 线圈值的设置 - 字节值
                //byte[] byDataBytes = BitConverter.GetBytes(SetValue);
                
                //for (int i = 0; i < byDataBytes.Length / 2; i++)
                //{
                //    byDataToBeSent[7 + i * 2 + 0] = byDataBytes[i * 2 + 1];// 数据 - 高字节
                //    byDataToBeSent[7 + i * 2 + 1] = byDataBytes[i * 2];// 数据 - 低字节
                //}

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[7 + 2 * 2 + 0] = byCRCResult[0];// 数据设置值 - 高字节
                //byDataToBeSent[7 + 2 * 2 + 1] = byCRCResult[1];// 数据设置值 - 低字节

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //成功执行写命令339504327
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 02 04 6C C7 14 3C 50 13 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 02 41 C8 

                    //成功执行写命令448913744
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 02 04 E1 50 1A C1 0F 72 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 02 41 C8 
                    //成功执行写命令-1582710813
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 02 04 BF E3 A1 A9 9E 63 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 02 41 C8 

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

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

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置内容(N字节)
                //CRC校验计算结果： (2字节)		

                //地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      CRC
                //1字节          1字节     2字节          2字节      1字节      N字节         2字节
                
                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);

                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, SetValue.Length * 2, byWriteData);

                if (null == byDataToBeSent)
                {
                    return false;
                }

                #region "Old codes -- No use"

                ////float = 2个short，所以这里转换为字节数量：2 * 2
                //byte[] byDataToBeSent = new byte[7 + SetValue.Length * 2 * 2];//地址码(1字节) + 功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置值(N字节) = (7+N)字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.WriteMultiRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);//起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                ////数据长度 - 字的数据长度
                //int iDataLength = SetValue.Length * 2;
                //byte[] byDataLength = CConverter.ToBytes(iDataLength);
                //byDataToBeSent[4] = byDataLength[1];// 数据长度 - 高字节
                //byDataToBeSent[5] = byDataLength[0];// 数据长度 - 低字节

                //// 字节计数
                //byte byByteCount = Convert.ToByte(SetValue.Length * 2 * 2);//字节数 = float = 2个short，所以这里转换为字节数量：2 * 2
                //byDataToBeSent[6] = byByteCount;

                //// 线圈值的设置 - 字节值
                //for (int j = 0; j < SetValue.Length; j++)
                //{
                //    byte[] byDataBytes = BitConverter.GetBytes(SetValue[j]);

                //    for (int i = 0; i < byDataBytes.Length / 2; i++)
                //    {
                //        byDataToBeSent[7 + j * 4 + i * 2 + 0] = byDataBytes[i * 2 + 1];
                //        byDataToBeSent[7 + j * 4 + i * 2 + 1] = byDataBytes[i * 2];
                //    }                    
                //}

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[7 + SetValue.Length * 2 * 2 + 0] = byCRCResult[0];// 数据设置值 - 高字节
                //byDataToBeSent[7 + SetValue.Length * 2 * 2 + 1] = byCRCResult[1];// 数据设置值 - 低字节

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //发送值[0~3]：476525262 1022917331 -908850870 -1270293692
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 08 10 32 CE 1C 67 7A D3 3C F8 09 4A C9 D4 DB 44 B4 48 0F D1 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 08 C1 CF 

                    //发送值[0~3]：-4340250 -591892235 622426862 -380301904
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 08 10 C5 E6 FF BD 70 F5 DC B8 7A EE 25 19 0D B0 E9 55 64 60 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 08 C1 CF 

                    //发送值[0~3]：1793983947 1938555105 -606453368 -2037564530
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 08 10 05 CB 6A EE 00 E1 73 8C 41 88 DB DA 3B 8E 86 8D 8F 57 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 08 C1 CF 

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置内容(N字节)
                //CRC校验计算结果： (2字节)		

                //地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      CRC
                //1字节          1字节     2字节          2字节      1字节      N字节         2字节
                
                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);

                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, 2, byWriteData);

                if (null == byDataToBeSent)
                {
                    return false;
                }

                #region "Old codes -- No use"

                ////float = 2个short，所以这里转换为字节数量：2 * 2
                //byte[] byDataToBeSent = new byte[7 + 2 * 2];//地址码(1字节) + 功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置值(N字节) = (7+N)字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.WriteMultiRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);//起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                ////数据长度 - 字的数据长度
                //int iDataLength = 2;
                //byte[] byDataLength = CConverter.ToBytes(iDataLength);
                //byDataToBeSent[4] = byDataLength[1];// 数据长度 - 高字节
                //byDataToBeSent[5] = byDataLength[0];// 数据长度 - 低字节

                //// 字节计数
                //byte byByteCount = Convert.ToByte(2 * 2);//字节数 = float = 2个short，所以这里转换为字节数量：2 * 2
                //byDataToBeSent[6] = byByteCount;

                //// 线圈值的设置 - 字节值
                //byte[] byDataBytes = BitConverter.GetBytes(SetValue);
                
                //for (int i = 0; i < byDataBytes.Length / 2; i++)
                //{
                //    byDataToBeSent[7 + i * 2 + 0] = byDataBytes[i * 2 + 1];// 数据 - 高字节
                //    byDataToBeSent[7 + i * 2 + 1] = byDataBytes[i * 2];// 数据 - 低字节
                //}

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[7 + 2 * 2 + 0] = byCRCResult[0];// 数据设置值 - 高字节
                //byDataToBeSent[7 + 2 * 2 + 1] = byCRCResult[1];// 数据设置值 - 低字节

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //成功执行写命令339504327
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 02 04 6C C7 14 3C 50 13 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 02 41 C8 

                    //成功执行写命令448913744
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 02 04 E1 50 1A C1 0F 72 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 02 41 C8 
                    //成功执行写命令-1582710813
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 02 04 BF E3 A1 A9 9E 63 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 02 41 C8 

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

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

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置内容(N字节)
                //CRC校验计算结果： (2字节)		

                //地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      CRC
                //1字节          1字节     2字节          2字节      1字节      N字节         2字节
                
                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);

                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, SetValue.Length * 2, byWriteData);

                if (null == byDataToBeSent)
                {
                    return false;
                }

                #region "Old codes -- No use"

                ////float = 2个short，所以这里转换为字节数量：2 * 2
                //byte[] byDataToBeSent = new byte[7 + SetValue.Length * 2 * 2];//地址码(1字节) + 功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置值(N字节) = (7+N)字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.WriteMultiRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);//起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                ////数据长度 - 字的数据长度
                //int iDataLength = SetValue.Length * 2;
                //byte[] byDataLength = CConverter.ToBytes(iDataLength);
                //byDataToBeSent[4] = byDataLength[1];// 数据长度 - 高字节
                //byDataToBeSent[5] = byDataLength[0];// 数据长度 - 低字节

                //// 字节计数
                //byte byByteCount = Convert.ToByte(SetValue.Length * 2 * 2);//字节数 = float = 2个short，所以这里转换为字节数量：2 * 2
                //byDataToBeSent[6] = byByteCount;

                //// 线圈值的设置 - 字节值
                //for (int j = 0; j < SetValue.Length; j++)
                //{
                //    byte[] byDataBytes = BitConverter.GetBytes(SetValue[j]);

                //    for (int i = 0; i < byDataBytes.Length / 2; i++)
                //    {
                //        byDataToBeSent[7 + j * 4 + i * 2 + 0] = byDataBytes[i * 2 + 1];
                //        byDataToBeSent[7 + j * 4 + i * 2 + 1] = byDataBytes[i * 2];
                //    }                    
                //}

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[7 + SetValue.Length * 2 * 2 + 0] = byCRCResult[0];// 数据设置值 - 高字节
                //byDataToBeSent[7 + SetValue.Length * 2 * 2 + 1] = byCRCResult[1];// 数据设置值 - 低字节

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置内容(N字节)
                //CRC校验计算结果： (2字节)		

                //地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      CRC
                //1字节          1字节     2字节          2字节      1字节      N字节         2字节
                
                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);

                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, 2, byWriteData);

                if (null == byDataToBeSent)
                {
                    return false;
                }

                #region "Old codes -- No use"

                ////float = 2个short，所以这里转换为字节数量：2 * 2
                //byte[] byDataToBeSent = new byte[7 + 2 * 2];//地址码(1字节) + 功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置值(N字节) = (7+N)字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.WriteMultiRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);//起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                ////数据长度 - 字的数据长度
                //int iDataLength = 2;
                //byte[] byDataLength = CConverter.ToBytes(iDataLength);
                //byDataToBeSent[4] = byDataLength[1];// 数据长度 - 高字节
                //byDataToBeSent[5] = byDataLength[0];// 数据长度 - 低字节

                //// 字节计数
                //byte byByteCount = Convert.ToByte(2 * 2);//字节数 = float = 2个short，所以这里转换为字节数量：2 * 2
                //byDataToBeSent[6] = byByteCount;

                //// 线圈值的设置 - 字节值
                //byte[] byDataBytes = BitConverter.GetBytes(SetValue);
                
                //for (int i = 0; i < byDataBytes.Length / 2; i++)
                //{
                //    byDataToBeSent[7 + i * 2] = byDataBytes[i * 2 + 1];// 数据 - 高字节
                //    byDataToBeSent[7 + i * 2 + 1] = byDataBytes[i * 2];// 数据 - 低字节
                //}

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[7 + 2 * 2 + 0] = byCRCResult[0];// 数据设置值 - 高字节
                //byDataToBeSent[7 + 2 * 2 + 1] = byCRCResult[1];// 数据设置值 - 低字节

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //成功执行写命令6524.286
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 02 04 E2 49 45 CB 67 06 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 02 41 C8 

                    //成功执行写命令-1313.714
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 02 04 36 DB C4 A4 DF 67 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 02 41 C8 

                    //成功执行写命令898
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 02 04 80 00 44 60 E9 47 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 02 41 C8 

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

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

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置内容(N字节)
                //CRC校验计算结果： (2字节)		

                //地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      CRC
                //1字节          1字节     2字节          2字节      1字节      N字节         2字节
                
                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);

                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress,SetValue.Length * 2 , byWriteData);

                if (null == byDataToBeSent)
                {
                    return false;
                }

                #region "Old codes -- No use"

                ////float = 2个short，所以这里转换为字节数量：2 * 2
                //byte[] byDataToBeSent = new byte[7 + SetValue.Length * 2 * 2];//地址码(1字节) + 功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置值(N字节) = (7+N)字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.WriteMultiRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);//起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                ////数据长度 - 字的数据长度
                //int iDataLength = SetValue.Length * 2;
                //byte[] byDataLength = CConverter.ToBytes(iDataLength);
                //byDataToBeSent[4] = byDataLength[1];// 数据长度 - 高字节
                //byDataToBeSent[5] = byDataLength[0];// 数据长度 - 低字节

                //// 字节计数
                //byte byByteCount = Convert.ToByte(SetValue.Length * 2 * 2);//字节数 = float = 2个short，所以这里转换为字节数量：2 * 2
                //byDataToBeSent[6] = byByteCount;

                //// 线圈值的设置 - 字节值
                //for (int j = 0; j < SetValue.Length; j++)
                //{
                //    byte[] byDataBytes = BitConverter.GetBytes(SetValue[j]);

                //    for (int i = 0; i < byDataBytes.Length / 2; i++)
                //    {
                //        byDataToBeSent[7 + j * 4 + i * 2] = byDataBytes[i * 2 + 1];
                //        byDataToBeSent[7 + j * 4 + i * 2 + 1] = byDataBytes[i * 2];
                //    }                    
                //}

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[7 + SetValue.Length * 2 * 2 + 0] = byCRCResult[0];// 数据设置值 - 高字节
                //byDataToBeSent[7 + SetValue.Length * 2 * 2 + 1] = byCRCResult[1];// 数据设置值 - 低字节

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //发送值[0~3]：-1738.571 9298.857 -6286.857 -1143.143
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 08 10 52 49 C4 D9 4B 6E 46 11 76 DB C5 C4 E4 92 C4 8E 8F 52 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 08 C1 CF 

                    //发送值[0~3]：-630 -8281.714 6392 9085.143
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 08 10 80 00 C4 1D 66 DB C6 01 C0 00 45 C7 F4 92 46 0D AA 04 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 08 C1 CF 

                    //发送值[0~3]：-9237.143 -7459.714 4608.857 3643.143
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 08 10 54 92 C6 10 1D B7 C5 E9 06 DB 45 90 B2 49 45 63 BA 93 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 08 C1 CF 

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置内容(N字节)
                //CRC校验计算结果： (2字节)		

                //地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      CRC
                //1字节          1字节     2字节          2字节      1字节      N字节         2字节
                
                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);

                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, 4, byWriteData);

                if (null == byDataToBeSent)
                {
                    return false;
                }

                #region "Old codes -- No use"

                ////double = 4个short，所以这里转换为字节数量：4 * 2
                //byte[] byDataToBeSent = new byte[7 + 4 * 2];//地址码(1字节) + 功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置值(N字节) = (7+N)字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.WriteMultiRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);//起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                ////数据长度 - 字的数据长度
                //int iDataLength = 4;
                //byte[] byDataLength = CConverter.ToBytes(iDataLength);
                //byDataToBeSent[4] = byDataLength[1];// 数据长度 - 高字节
                //byDataToBeSent[5] = byDataLength[0];// 数据长度 - 低字节

                //// 字节计数
                //byte byByteCount = Convert.ToByte(4 * 2);//字节数 = double = 4个short，所以这里转换为字节数量：4 * 2
                //byDataToBeSent[6] = byByteCount;

                //// 线圈值的设置 - 字节值
                //byte[] byDoubleToBytes = BitConverter.GetBytes(SetValue);
                
                //for (int i = 0; i < byDoubleToBytes.Length / 2; i++)
                //{
                //    byDataToBeSent[7 + i * 2] = byDoubleToBytes[i * 2 + 1];// 数据 - 高字节
                //    byDataToBeSent[7 + i * 2 + 1] = byDoubleToBytes[i * 2];// 数据 - 低字节
                //}

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[7 + 4 * 2 + 0] = byCRCResult[0];// 数据设置值 - 高字节
                //byDataToBeSent[7 + 4 * 2 + 1] = byCRCResult[1];// 数据设置值 - 低字节

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //成功执行写命令-4438.57142857143
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 04 08 92 49 49 24 56 92 C0 B1 B8 44 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 04 C1 CA 

                    //成功执行写命令2656.85714285714
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 04 08 B6 DB DB 6D C1 B6 40 A4 05 6A 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 04 C1 CA 

                    //成功执行写命令-45.7142857142857
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 04 08 6D B7 B6 DB DB 6D C0 46 B2 4E 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 04 C1 CA 

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

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

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置内容(N字节)
                //CRC校验计算结果： (2字节)		

                //地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      CRC
                //1字节          1字节     2字节          2字节      1字节      N字节         2字节
                
                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);

                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, SetValue.Length * 4, byWriteData);

                if (null == byDataToBeSent)
                {
                    return false;
                }

                #region "Old codes -- No use"

                ////double = 4个short，所以这里转换为字节数量：SetValue.Length * 4 * 2
                //byte[] byDataToBeSent = new byte[7 + SetValue.Length * 4 * 2];//地址码(1字节) + 功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置值(N字节) = (7+N)字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.WriteMultiRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);//起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                ////数据长度 - 字的数据长度
                //int iDataLength = SetValue.Length * 4;
                //byte[] byDataLength = CConverter.ToBytes(iDataLength);
                //byDataToBeSent[4] = byDataLength[1];// 数据长度 - 高字节
                //byDataToBeSent[5] = byDataLength[0];// 数据长度 - 低字节

                //// 字节计数
                //byte byByteCount = Convert.ToByte(SetValue.Length * 4 * 2);//字节数 = double = 4个short，所以这里转换为字节数量：4 * 2
                //byDataToBeSent[6] = byByteCount;

                //// 线圈值的设置 - 字节值
                //for (int j = 0; j < SetValue.Length; j++)
                //{
                //    byte[] byDoubleToBytes = BitConverter.GetBytes(SetValue[j]);

                //    for (int i = 0; i < byDoubleToBytes.Length / 2; i++)
                //    {
                //        byDataToBeSent[7 + j * 8 + i * 2] = byDoubleToBytes[i * 2 + 1];// 数据 - 高字节
                //        byDataToBeSent[7 + j * 8 + i * 2 + 1] = byDoubleToBytes[i * 2];// 数据 - 低字节
                //    }
                //}

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[7 + SetValue.Length * 4 * 2 + 0] = byCRCResult[0];// 数据设置值 - 高字节
                //byDataToBeSent[7 + SetValue.Length * 4 * 2 + 1] = byCRCResult[1];// 数据设置值 - 低字节

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //发送值[0~3]：-246 1514.57141113281 -2021.71423339844 7905.4287109375
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 10 20 00 00 00 00 C0 00 C0 6E 00 00 20 00 AA 49 40 97 00 00 60 00 96 DB C0 9F 00 00 C0 00 E1 6D 40 BE FE 5A 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 10 C1 C5 

                    //发送值[0~3]：428.285705566406 -1867.42858886719 1131.42858886719 -7593.4287109375
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 10 20 00 00 40 00 C4 92 40 7A 00 00 E0 00 2D B6 C0 9D 00 00 E0 00 AD B6 40 91 00 00 C0 00 A9 6D C0 BD 03 63 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 10 C1 C5 

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置内容(N字节)
                //CRC校验计算结果： (2字节)		

                //地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      CRC
                //1字节          1字节     2字节          2字节      1字节      N字节         2字节
                
                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);

                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, 4, byWriteData);

                if (null == byDataToBeSent)
                {
                    return false;
                }

                #region "Old codes -- No use"

                ////double = 4个short，所以这里转换为字节数量：4 * 2
                //byte[] byDataToBeSent = new byte[7 + 4 * 2];//地址码(1字节) + 功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置值(N字节) = (7+N)字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.WriteMultiRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);//起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                ////数据长度 - 字的数据长度
                //int iDataLength = 4;
                //byte[] byDataLength = CConverter.ToBytes(iDataLength);
                //byDataToBeSent[4] = byDataLength[1];// 数据长度 - 高字节
                //byDataToBeSent[5] = byDataLength[0];// 数据长度 - 低字节

                //// 字节计数
                //byte byByteCount = Convert.ToByte(4 * 2);//字节数 = double = 4个short，所以这里转换为字节数量：4 * 2
                //byDataToBeSent[6] = byByteCount;

                //// 线圈值的设置 - 字节值
                //byte[] byDoubleToBytes = BitConverter.GetBytes(SetValue);
                
                //for (int i = 0; i < byDoubleToBytes.Length / 2; i++)
                //{
                //    byDataToBeSent[7 + i * 2] = byDoubleToBytes[i * 2 + 1];// 数据 - 高字节
                //    byDataToBeSent[7 + i * 2 + 1] = byDoubleToBytes[i * 2];// 数据 - 低字节
                //}

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[7 + 4 * 2 + 0] = byCRCResult[0];// 数据设置值 - 高字节
                //byDataToBeSent[7 + 4 * 2 + 1] = byCRCResult[1];// 数据设置值 - 低字节

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //成功执行写命令-912079796
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 04 08 C4 4C C9 A2 FF FF FF FF DF C9 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 04 C1 CA 
                    //成功执行写命令1827285256
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 04 08 29 08 6C EA 00 00 00 00 ED 73 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 04 C1 CA 
                    //成功执行写命令1157902144
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 04 08 2F 40 45 04 00 00 00 00 4A B3 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 04 C1 CA 
                    //成功执行写命令-1903822418
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 04 08 F9 AE 8E 85 FF FF FF FF E4 3A 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 04 C1 CA 

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

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

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置内容(N字节)
                //CRC校验计算结果： (2字节)		

                //地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      CRC
                //1字节          1字节     2字节          2字节      1字节      N字节         2字节
                
                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);

                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, SetValue.Length * 4, byWriteData);

                if (null == byDataToBeSent)
                {
                    return false;
                }

                #region "Old codes -- No use"

                ////double = 4个short，所以这里转换为字节数量：SetValue.Length * 4 * 2
                //byte[] byDataToBeSent = new byte[7 + SetValue.Length * 4 * 2];//地址码(1字节) + 功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置值(N字节) = (7+N)字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.WriteMultiRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);//起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                ////数据长度 - 字的数据长度
                //int iDataLength = SetValue.Length * 4;
                //byte[] byDataLength = CConverter.ToBytes(iDataLength);
                //byDataToBeSent[4] = byDataLength[1];// 数据长度 - 高字节
                //byDataToBeSent[5] = byDataLength[0];// 数据长度 - 低字节

                //// 字节计数
                //byte byByteCount = Convert.ToByte(SetValue.Length * 4 * 2);//字节数 = double = 4个short，所以这里转换为字节数量：4 * 2
                //byDataToBeSent[6] = byByteCount;

                //// 线圈值的设置 - 字节值
                //for (int j = 0; j < SetValue.Length; j++)
                //{
                //    byte[] byDoubleToBytes = BitConverter.GetBytes(SetValue[j]);

                //    for (int i = 0; i < byDoubleToBytes.Length / 2; i++)
                //    {
                //        byDataToBeSent[7 + j * 8 + i * 2] = byDoubleToBytes[i * 2 + 1];// 数据 - 高字节
                //        byDataToBeSent[7 + j * 8 + i * 2 + 1] = byDoubleToBytes[i * 2];// 数据 - 低字节
                //    }
                //}

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[7 + SetValue.Length * 4 * 2 + 0] = byCRCResult[0];// 数据设置值 - 高字节
                //byDataToBeSent[7 + SetValue.Length * 4 * 2 + 1] = byCRCResult[1];// 数据设置值 - 低字节

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //发送值[0~3]：-610285535 -99357941 -1567199904 -80377002
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 10 20 C8 21 DB 9F FF FF FF FF EB 0B FA 13 FF FF FF FF 6D 60 A2 96 FF FF FF FF 8B 56 FB 35 FF FF FF FF 39 00 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 10 C1 C5 

                    //发送值[0~3]：1048392884 1868250620 925460064 2099401003
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 10 20 34 B4 3E 7D 00 00 00 00 3D FC 6F 5B 00 00 00 00 66 60 37 29 00 00 00 00 51 2B 7D 22 00 00 00 00 FA 04 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 10 C1 C5 

                    //发送值[0~3]：822885324 2120724152 1254641722 -1092318208
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 10 20 3B CC 31 0C 00 00 00 00 AE B8 7E 67 00 00 00 00 50 3A 4A C8 00 00 00 00 8C 00 BE E4 FF FF FF FF 45 A5 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 10 C1 C5 

                    //发送值[0~3]：690449040 1333132832 1223748428 -149121200
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 10 20 6A 90 29 27 00 00 00 00 FE 20 4F 75 00 00 00 00 EB 4C 48 F0 00 00 00 00 97 50 F7 1C FF FF FF FF 32 D8 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 10 C1 C5 

                    //发送值[0~3]：464933644 404543674 437956160 1804413751
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 10 20 53 0C 1B B6 00 00 00 00 D8 BA 18 1C 00 00 00 00 AE 40 1A 1A 00 00 00 00 2B 37 6B 8D 00 00 00 00 A7 E2 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 10 C1 C5 

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置内容(N字节)
                //CRC校验计算结果： (2字节)		

                //地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      CRC
                //1字节          1字节     2字节          2字节      1字节      N字节         2字节
                
                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);

                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, 4, byWriteData);

                if (null == byDataToBeSent)
                {
                    return false;
                }

                #region "Old codes -- No use"

                ////double = 4个short，所以这里转换为字节数量：4 * 2
                //byte[] byDataToBeSent = new byte[7 + 4 * 2];//地址码(1字节) + 功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置值(N字节) = (7+N)字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.WriteMultiRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);//起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                ////数据长度 - 字的数据长度
                //int iDataLength = 4;
                //byte[] byDataLength = CConverter.ToBytes(iDataLength);
                //byDataToBeSent[4] = byDataLength[1];// 数据长度 - 高字节
                //byDataToBeSent[5] = byDataLength[0];// 数据长度 - 低字节

                //// 字节计数
                //byte byByteCount = Convert.ToByte(4 * 2);//字节数 = double = 4个short，所以这里转换为字节数量：4 * 2
                //byDataToBeSent[6] = byByteCount;

                //// 线圈值的设置 - 字节值
                //byte[] byDoubleToBytes = BitConverter.GetBytes(SetValue);
                
                //for (int i = 0; i < byDoubleToBytes.Length / 2; i++)
                //{
                //    byDataToBeSent[7 + i * 2] = byDoubleToBytes[i * 2 + 1];// 数据 - 高字节
                //    byDataToBeSent[7 + i * 2 + 1] = byDoubleToBytes[i * 2];// 数据 - 低字节
                //}

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[7 + 4 * 2 + 0] = byCRCResult[0];// 数据设置值 - 高字节
                //byDataToBeSent[7 + 4 * 2 + 1] = byCRCResult[1];// 数据设置值 - 低字节

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //成功执行写命令-912079796
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 04 08 C4 4C C9 A2 FF FF FF FF DF C9 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 04 C1 CA 
                    //成功执行写命令1827285256
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 04 08 29 08 6C EA 00 00 00 00 ED 73 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 04 C1 CA 
                    //成功执行写命令1157902144
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 04 08 2F 40 45 04 00 00 00 00 4A B3 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 04 C1 CA 
                    //成功执行写命令-1903822418
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 04 08 F9 AE 8E 85 FF FF FF FF E4 3A 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 04 C1 CA 

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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
                if (null == RS232CPort || RS232CPort.IsOpen == false)
                {
                    return false;
                }

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

                //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置内容(N字节)
                //CRC校验计算结果： (2字节)		

                //地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      CRC
                //1字节          1字节     2字节          2字节      1字节      N字节         2字节
                
                byte[] byWriteData = CConverter.ToBytes(SetValue, WriteKeepRegisterBytesFormat);

                byte[] byDataToBeSent = PackMultiWriteCmd(DeviceAddress, ModbusFuncCode.WriteMultiRegister, BeginAddress, SetValue.Length * 4, byWriteData);

                if (null == byDataToBeSent)
                {
                    return false;
                }

                #region "Old codes -- No use"

                ////double = 4个short，所以这里转换为字节数量：SetValue.Length * 4 * 2
                //byte[] byDataToBeSent = new byte[7 + SetValue.Length * 4 * 2];//地址码(1字节) + 功能码(1字节) + 设置起始地址(2字节) + 设置长度(2字节) + 字节计数(1字节) + 设置值(N字节) = (7+N)字节

                //byDataToBeSent[0] = DeviceAddress;//地址码
                //byDataToBeSent[1] = CModbusFunctionCode.WriteMultiRegister;//功能码

                //byte[] byBeginAddress = CConverter.ToBytes(BeginAddress);//起始地址(2字节)
                //byDataToBeSent[2] = byBeginAddress[1];// 起始地址 - 高字节
                //byDataToBeSent[3] = byBeginAddress[0];// 起始地址 - 低字节

                ////数据长度 - 字的数据长度
                //int iDataLength = SetValue.Length * 4;
                //byte[] byDataLength = CConverter.ToBytes(iDataLength);
                //byDataToBeSent[4] = byDataLength[1];// 数据长度 - 高字节
                //byDataToBeSent[5] = byDataLength[0];// 数据长度 - 低字节

                //// 字节计数
                //byte byByteCount = Convert.ToByte(SetValue.Length * 4 * 2);//字节数 = double = 4个short，所以这里转换为字节数量：4 * 2
                //byDataToBeSent[6] = byByteCount;

                //// 线圈值的设置 - 字节值
                //for (int j = 0; j < SetValue.Length; j++)
                //{
                //    byte[] byDoubleToBytes = BitConverter.GetBytes(SetValue[j]);

                //    for (int i = 0; i < byDoubleToBytes.Length / 2; i++)
                //    {
                //        byDataToBeSent[7 + j * 8 + i * 2] = byDoubleToBytes[i * 2 + 1];// 数据 - 高字节
                //        byDataToBeSent[7 + j * 8 + i * 2 + 1] = byDoubleToBytes[i * 2];// 数据 - 低字节
                //    }
                //}

                //byte[] byCRCResult = CalcCRC(byDataToBeSent);

                //Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
                //byDataToBeSent[7 + SetValue.Length * 4 * 2 + 0] = byCRCResult[0];// 数据设置值 - 高字节
                //byDataToBeSent[7 + SetValue.Length * 4 * 2 + 1] = byCRCResult[1];// 数据设置值 - 低字节

                //byCRCResult = null;

                #endregion

                byte[] byReadData = null;

                byReadData = WriteAndReadBytes(byDataToBeSent, DeviceAddress);

                //if (null == byReadData)
                //{
                //    return false;
                //}

                #region "Old codes - No use"

                //string sCmdDataToBeSent = "";

                //if (bSaveSendStringToLog == true)
                //{
                //    sCmdDataToBeSent = CConverter.BytesToHexStringSplitByChar(byDataToBeSent);
                //    Enqueue("发送字节转换为16进制 - " + sCmdDataToBeSent);
                //}

                //Lock();

                ////清除发送前的接收缓冲区
                //ClearReceiveBuffer();

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);
                
                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
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

                //byte[] byReadData = new byte[RS232CPort.BytesToRead];
                //RS232CPort.Read(byReadData, 0, byReadData.Length);

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

                    //发送值[0~3]：-610285535 -99357941 -1567199904 -80377002
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 10 20 C8 21 DB 9F FF FF FF FF EB 0B FA 13 FF FF FF FF 6D 60 A2 96 FF FF FF FF 8B 56 FB 35 FF FF FF FF 39 00 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 10 C1 C5 

                    //发送值[0~3]：1048392884 1868250620 925460064 2099401003
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 10 20 34 B4 3E 7D 00 00 00 00 3D FC 6F 5B 00 00 00 00 66 60 37 29 00 00 00 00 51 2B 7D 22 00 00 00 00 FA 04 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 10 C1 C5 

                    //发送值[0~3]：822885324 2120724152 1254641722 -1092318208
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 10 20 3B CC 31 0C 00 00 00 00 AE B8 7E 67 00 00 00 00 50 3A 4A C8 00 00 00 00 8C 00 BE E4 FF FF FF FF 45 A5 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 10 C1 C5 

                    //发送值[0~3]：690449040 1333132832 1223748428 -149121200
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 10 20 6A 90 29 27 00 00 00 00 FE 20 4F 75 00 00 00 00 EB 4C 48 F0 00 00 00 00 97 50 F7 1C FF FF FF FF 32 D8 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 10 C1 C5 

                    //发送值[0~3]：464933644 404543674 437956160 1804413751
                    //写操作记录：(COM1)发送字节转换为16进制 - 01 10 00 00 00 10 20 53 0C 1B B6 00 00 00 00 D8 BA 18 1C 00 00 00 00 AE 40 1A 1A 00 00 00 00 2B 37 6B 8D 00 00 00 00 A7 E2 
                    //写操作记录：(COM1)收到字节转换为16进制 - 01 10 00 00 00 10 C1 C5 

                    #endregion

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
                    //MessageBox.Show("Modbus RTU(" + RS232CPort.PortName + ") 通讯时发生错误：\r\n" + ex.Message, "提示", //MessageBoxButtons.OK, //MessageBoxIcon.Information);
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

            //throw new NotImplementedException();
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
                    qErrorMsg.Enqueue("Modbus RTU(" + RS232CPort.PortName + ")" + Msg);//发生错误
                }
                else
                {
                    qErrorMsg.Dequeue();
                    qErrorMsg.Enqueue("Modbus RTU(" + RS232CPort.PortName + ")" + Msg);//发生错误
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
            if (bReadSlaveDataOnly == false ||  bIsDisposing == true)
            {
                return;
            }

            try
            {
                //接收所有字节
                byte[] byReadData = new byte[RS232CPort.BytesToRead];
                RS232CPort.Read(byReadData, 0, byReadData.Length);

                qReceivedDataQueue.Enqueue(UnpackReceivedRTUMsg(byReadData));
            }
            catch (Exception ex)
            {
                Enqueue("接收数据发生错误：" + ex.Message + "; " + ex.StackTrace);
            }

            #region "Old codes - No use"

            ////原始字符串、原始字节数组、从站地址、功能码、数据字符串/字节数组、日期和时间、是否CRC校验OK、是否LRC校验OK

            //CReadbackData gotData = new CReadbackData();
            //gotData.ReceivedDateTime = DateTime.Now;//UtcNow  日期和时间

            //try
            //{
            //    //接收所有字节
            //    byte[] byReadData = new byte[RS232CPort.BytesToRead];
            //    RS232CPort.Read(byReadData, 0, byReadData.Length);

            //    gotData.ReceivedBytes = byReadData;//原始字节数组

            //    //将字节数组转化为16进制字符串
            //    string sFeedBackFromSlave = CConverter.Bytes1To2HexStr(byReadData);
            //    gotData.ReceivedString = sFeedBackFromSlave;//原始字符串

            //    if (bSaveReceivedStringToLog == true)
            //    {
            //        if (string.IsNullOrEmpty(sFeedBackFromSlave) == false)
            //        {
            //            Enqueue("收到字节转换为16进制 - " + sFeedBackFromSlave);
            //        }
            //    }

            //    //计算CRC  是否CRC校验OK
            //    if (CheckCRCOfReceivedData(byReadData) == false)
            //    {
            //        gotData.IsCRCOK = false;
            //        if (bSaveReceivedStringToLog == true)
            //        {
            //            Enqueue("错误 - 收到字节进行CRC校验时，收到的CRC码和计算的CRC码不匹配");
            //        }
            //    }
            //    else
            //    {
            //        gotData.IsCRCOK = true;
            //    }

            //    gotData.SlaveAddress = byReadData[iPositionIndexOfSlaveAddressInReceivedBytes];//从站地址

            //    gotData.FuncCode = byReadData[iPositionIndexOfFunctionCodeInReceivedBytes];//功能码

            //    //数据字符串/字节数组
            //    int iLength = byReadData.Length;
            //    int iCopyDataLength = iLength - iPositionIndexOfReceivedDataInReceivedBytes - 2;
            //    gotData.DataBytes = new byte[iCopyDataLength];
            //    Array.Copy(byReadData, iPositionIndexOfReceivedDataInReceivedBytes, gotData.DataBytes, 0, iCopyDataLength);
            //}
            //catch (Exception ex)
            //{
            //    gotData.ErrorMsg = ex.Message + "; " + ex.StackTrace;
            //}

            //qReceivedDataQueue.Enqueue(gotData);

            #endregion
        }

        /// <summary>
        /// 将Modbus-RTU接收到的字节信息进行解析
        /// </summary>
        /// <param name="ReceivedMsg">接收到的字节信息</param>
        /// <returns></returns>
        private CReadbackData UnpackReceivedRTUMsg(byte[] ReceivedMsg)
        {
            //原始字符串、原始字节数组、从站地址、功能码、数据字符串/字节数组、日期和时间、是否CRC校验OK、是否LRC校验OK

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

                //计算CRC  是否CRC校验OK
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

                gotData.SlaveAddress = ReceivedMsg[PosIndexOfSlaveAddressInRTUReceivedBytes];//从站地址

                gotData.FuncCode = ReceivedMsg[PosIndexOfFuncCodeInRTUReceivedBytes];//功能码
                gotData.FuncDescription = CModbusFuncCode.FuncInfo((ModbusFuncCode)gotData.FuncCode);//功能码的描述信息

                gotData.ErrorCode = ReceivedMsg[PosIndexOfErrorCodeInRTUReceivedBytes];//错误码
                gotData.ErrorMsg = AnalysisErrorCode(ReceivedMsg);//错误码的描述信息

                //数据字符串/字节数组
                int iLength = ReceivedMsg.Length;
                int iCopyDataLength = iLength - PosIndexOfDataInRTUReceivedBytes - 2;//CRC占2个字节
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
            //, string sCmdDataToBeSent   <param name="sCmdDataToBeSent">要发送的数据字符串，用于保存至日志</param>
            byte[] byReadData = null;

            try
            {
                Lock();

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                bIsConnected = true;

                if (bSaveSendStringToLog == true)
                {
                    string sTemp = CConverter.Bytes1To2HexStr(byResult);
                    Enqueue("发送字节转换为16进制 - " + sTemp);
                }

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < iMinLengthOfResponse)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        Unlock();
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    //Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                //byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());
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

            #region "Old codes"

            //if (FuncCode != ModbusFuncCode.ReadCoil && FuncCode != ModbusFuncCode.ReadInputRegister
            //    && FuncCode != ModbusFuncCode.ReadInputSignal && FuncCode != ModbusFuncCode.ReadRegister)
            //{
            //    return null;
            //}

            //if (ReadDataLength < 1)
            //{
            //    ReadDataLength = 1;
            //}

            ////地址码         功能码    设置起始地址   读取数量      CRC
            ////1字节          1字节     2字节          2字节         2字节

            ////CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
            ////CRC校验计算结果： (2字节)

            //byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

            //try
            //{
            //    byDataToBeSent[0] = DeviceAddress;//地址码
            //    byDataToBeSent[1] = (byte)FuncCode;//功能码

            //    byte[] byBeginAddress = CConverter.ToBytes((short)BeginAddress, ParaBytesFormat);// 起始地址(2字节)
            //    byDataToBeSent[2] = byBeginAddress[0];// 起始地址 - 高字节  [1]
            //    byDataToBeSent[3] = byBeginAddress[1];// 起始地址 - 低字节  [0]

            //    byte[] byReadDataLength = CConverter.ToBytes((short)ReadDataLength, ParaBytesFormat);// 读取数量 -- 数据位的数量
            //    byDataToBeSent[4] = byReadDataLength[0];// 读取数量 -- 高字节  [1]
            //    byDataToBeSent[5] = byReadDataLength[1];// 读取数量 -- 低字节  [0]

            //    byte[] byCRCResult = CalcCRC(byDataToBeSent);
            //    byCRCResult = CConverter.Reorder2BytesData(byCRCResult, WriteCoilBytesFormat, 0);

            //    Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
            //    byDataToBeSent[6] = byCRCResult[0];//
            //    byDataToBeSent[7] = byCRCResult[1];//
            //}
            //catch (Exception ex)
            //{
            //    Enqueue("封装读取命令发生错误:" + ex.Message + "; " + ex.StackTrace);
            //}

            //return byDataToBeSent;

            #endregion

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

            #region "Old codes"

            //if (null == Data || Data.Length < 1)
            //{
            //    return null;
            //}

            //if (FuncCode != ModbusFuncCode.WriteCoil && FuncCode != ModbusFuncCode.WriteRegister)
            //{
            //    return null;
            //}

            ////地址码    功能码    设置起始地址   设置值      CRC
            ////1字节     1字节     2字节          2字节      2字节
            //byte[] byDataToBeSent = new byte[6 + Data.Length];//地址码(1字节) + 功能码(1字节) + 设置起始地址(2字节) +  设置值(2字节) = 6字节

            //try
            //{
            //    byDataToBeSent[0] = DeviceAddress;//地址码
            //    byDataToBeSent[1] = (byte)FuncCode;//功能码

            //    byte[] byBeginAddress = CConverter.ToBytes((short)BeginAddress, ParaBytesFormat);//起始地址(2字节)
            //    byDataToBeSent[2] = byBeginAddress[0];// 起始地址 - 高字节  [1]
            //    byDataToBeSent[3] = byBeginAddress[1];// 起始地址 - 低字节  [0]

            //    for (int i = 0; i < Data.Length; i++)
            //    {
            //        byDataToBeSent[4 + i] = Data[i];
            //    }

            //    byte[] byCRCResult = CalcCRC(byDataToBeSent);
            //    byCRCResult = CConverter.Reorder2BytesData(byCRCResult, WriteCoilBytesFormat, 0);

            //    int iDataLengthIncludeCRCData = byDataToBeSent.Length + 2;//数组长度 + 2个字节的CRC

            //    Array.Resize<byte>(ref byDataToBeSent, iDataLengthIncludeCRCData);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
            //    byDataToBeSent[iDataLengthIncludeCRCData - 2] = byCRCResult[0];//
            //    byDataToBeSent[iDataLengthIncludeCRCData - 1] = byCRCResult[1];//

            //    byCRCResult = null;
            //}
            //catch (Exception ex)
            //{
            //    Enqueue("封装Single写命令发生错误:" + ex.Message + "; " + ex.StackTrace);
            //}

            //return byDataToBeSent;

            #endregion
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

            #region "Old codes"

            //if (null == Data || Data.Length < 1)
            //{
            //    return null;
            //}

            //if (FuncCode != ModbusFuncCode.WriteMultiCoil && FuncCode != ModbusFuncCode.WriteMultiRegister)
            //{
            //    return null;
            //}

            //int iDataByteLength = Data.Length;

            ////地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      CRC
            ////1字节          1字节     2字节          2字节      1字节      N字节         2字节
            //byte[] byDataToBeSent = new byte[7 + iDataByteLength];

            //try
            //{
            //    byDataToBeSent[0] = DeviceAddress;//地址码
            //    byDataToBeSent[1] = (byte)FuncCode;//功能码

            //    byte[] byBeginAddress = CConverter.ToBytes((short)BeginAddress, ParaBytesFormat);//起始地址(2字节)
            //    byDataToBeSent[2] = byBeginAddress[0];// 起始地址 - 高字节  [1]
            //    byDataToBeSent[3] = byBeginAddress[1];// 起始地址 - 低字节  [0]

            //    //数据位长度 - 位的数据长度
            //    byte[] byDataLength = CConverter.ToBytes((short)DataLength, ParaBytesFormat);
            //    byDataToBeSent[4] = byDataLength[0];// 数据长度 - 高字节  [1]
            //    byDataToBeSent[5] = byDataLength[1];// 数据长度 - 低字节  [0]

            //    // 字节计数
            //    byte byByteCount = Convert.ToByte(iDataByteLength);//bool[]数组长度 / 8 = 字节数
            //    byDataToBeSent[6] = byByteCount;

            //    for (int i = 0; i < iDataByteLength; i++)
            //    {
            //        byDataToBeSent[7 + i] = Data[i];
            //    }

            //    byte[] byCRCResult = CalcCRC(byDataToBeSent);
            //    byCRCResult = CConverter.Reorder2BytesData(byCRCResult, WriteCoilBytesFormat, 0);

            //    int iDataLengthIncludeCRCData = byDataToBeSent.Length + 2;//数组长度 + 2个字节的CRC

            //    Array.Resize<byte>(ref byDataToBeSent, iDataLengthIncludeCRCData);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
            //    byDataToBeSent[iDataLengthIncludeCRCData - 2] = byCRCResult[0];//
            //    byDataToBeSent[iDataLengthIncludeCRCData - 1] = byCRCResult[1];//

            //    byCRCResult = null;
            //}
            //catch (Exception ex)
            //{
            //    Enqueue("封装Multi写命令发生错误:" + ex.Message + "; " + ex.StackTrace);
            //}

            //return byDataToBeSent;

            #endregion
        }

        #region "New codes"

        ///// <summary>
        ///// 封装读取命令，返回处理好的字节数组，然后可以直接将字节数组发送到串口
        ///// </summary>
        ///// <param name="DeviceAddress">从站地址</param>
        ///// <param name="FuncCode">功能码</param>
        ///// <param name="BeginAddress">起始地址</param>
        ///// <param name="ReadDataLength">读取数据长度</param>
        ///// <returns></returns>
        //public static byte[] PackReadCmd(byte DeviceAddress, ModbusFuncCode FuncCode, ushort BeginAddress, int ReadDataLength, BytesFormat ParaBytesFormat, BytesFormat WriteCoilBytesFormat)
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

        //    //地址码         功能码    设置起始地址   读取数量      CRC
        //    //1字节          1字节     2字节          2字节         2字节

        //    //CRC校验计算目标：	地址码(1字节) +  功能码(1字节) + 起始地址(2字节) + 读取数据位的数量(2字节)
        //    //CRC校验计算结果： (2字节)

        //    byte[] byDataToBeSent = new byte[6];//地址码(1字节) + 功能码(1字节) + 起始地址(2字节) +  读取数据位的数量(2字节) = 6字节

        //    try
        //    {
        //        byDataToBeSent[0] = DeviceAddress;//地址码
        //        byDataToBeSent[1] = (byte)FuncCode;//功能码

        //        byte[] byBeginAddress = CConverter.ToBytes((short)BeginAddress, ParaBytesFormat);// 起始地址(2字节)
        //        byDataToBeSent[2] = byBeginAddress[0];// 起始地址 - 高字节  [1]
        //        byDataToBeSent[3] = byBeginAddress[1];// 起始地址 - 低字节  [0]

        //        byte[] byReadDataLength = CConverter.ToBytes((short)ReadDataLength, ParaBytesFormat);// 读取数量 -- 数据位的数量
        //        byDataToBeSent[4] = byReadDataLength[0];// 读取数量 -- 高字节  [1]
        //        byDataToBeSent[5] = byReadDataLength[1];// 读取数量 -- 低字节  [0]

        //        byte[] byCRCResult = CalcCRC(byDataToBeSent);
        //        byCRCResult = CConverter.Reorder2BytesData(byCRCResult, WriteCoilBytesFormat, 0);

        //        Array.Resize<byte>(ref byDataToBeSent, byDataToBeSent.Length + 2);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
        //        byDataToBeSent[6] = byCRCResult[0];//
        //        byDataToBeSent[7] = byCRCResult[1];//
        //    }
        //    catch (Exception ex)
        //    {
        //        //Enqueue("封装读取命令发生错误:" + ex.Message + "; " + ex.StackTrace);
        //    }

        //    return byDataToBeSent;
        //}

        ///// <summary>
        ///// 封装写命令，返回处理好的字节数组
        ///// </summary>
        ///// <param name="DeviceAddress">从站地址</param>
        ///// <param name="FuncCode">功能码</param>
        ///// <param name="BeginAddress">起始地址</param>
        ///// <param name="Data">要发送的数据的字节数组</param>
        ///// <returns></returns>
        //public static byte[] PackSingleWriteCmd(byte DeviceAddress, ModbusFuncCode FuncCode, ushort BeginAddress, byte[] Data, BytesFormat ParaBytesFormat, BytesFormat WriteCoilBytesFormat)
        //{
        //    if (null == Data || Data.Length < 1)
        //    {
        //        return null;
        //    }

        //    if (FuncCode != ModbusFuncCode.WriteCoil && FuncCode != ModbusFuncCode.WriteRegister)
        //    {
        //        return null;
        //    }

        //    //地址码    功能码    设置起始地址   设置值      CRC
        //    //1字节     1字节     2字节          2字节      2字节
        //    byte[] byDataToBeSent = new byte[6 + Data.Length];//地址码(1字节) + 功能码(1字节) + 设置起始地址(2字节) +  设置值(2字节) = 6字节

        //    try
        //    {
        //        byDataToBeSent[0] = DeviceAddress;//地址码
        //        byDataToBeSent[1] = (byte)FuncCode;//功能码

        //        byte[] byBeginAddress = CConverter.ToBytes((short)BeginAddress, ParaBytesFormat);//起始地址(2字节)
        //        byDataToBeSent[2] = byBeginAddress[0];// 起始地址 - 高字节  [1]
        //        byDataToBeSent[3] = byBeginAddress[1];// 起始地址 - 低字节  [0]

        //        for (int i = 0; i < Data.Length; i++)
        //        {
        //            byDataToBeSent[4 + i] = Data[i];
        //        }

        //        byte[] byCRCResult = CalcCRC(byDataToBeSent);
        //        byCRCResult = CConverter.Reorder2BytesData(byCRCResult, WriteCoilBytesFormat, 0);

        //        int iDataLengthIncludeCRCData = byDataToBeSent.Length + 2;//数组长度 + 2个字节的CRC

        //        Array.Resize<byte>(ref byDataToBeSent, iDataLengthIncludeCRCData);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
        //        byDataToBeSent[iDataLengthIncludeCRCData - 2] = byCRCResult[0];//
        //        byDataToBeSent[iDataLengthIncludeCRCData - 1] = byCRCResult[1];//

        //        byCRCResult = null;
        //    }
        //    catch (Exception)// ex)
        //    {
        //        //Enqueue("封装Single写命令发生错误:" + ex.Message + "; " + ex.StackTrace);
        //    }

        //    return byDataToBeSent;
        //}

        ///// <summary>
        ///// 封装写命令，返回处理好的字节数组
        ///// </summary>
        ///// <param name="DeviceAddress">从站地址</param>
        ///// <param name="FuncCode">功能码</param>
        ///// <param name="BeginAddress">起始地址</param>
        ///// <param name="DataLength">要发送的数据的数量：线圈 -- 位(bool)；寄存器 -- 字(short)</param>
        ///// <param name="Data">要发送的数据的字节数组</param>
        ///// <returns></returns>
        //public static byte[] PackMultiWriteCmd(byte DeviceAddress, ModbusFuncCode FuncCode, ushort BeginAddress, int DataLength, byte[] Data, BytesFormat ParaBytesFormat, BytesFormat WriteCoilBytesFormat)
        //{
        //    if (null == Data || Data.Length < 1)
        //    {
        //        return null;
        //    }

        //    if (FuncCode != ModbusFuncCode.WriteMultiCoil && FuncCode != ModbusFuncCode.WriteMultiRegister)
        //    {
        //        return null;
        //    }

        //    int iDataByteLength = Data.Length;

        //    //地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      CRC
        //    //1字节          1字节     2字节          2字节      1字节      N字节         2字节
        //    byte[] byDataToBeSent = new byte[7 + iDataByteLength];

        //    try
        //    {
        //        byDataToBeSent[0] = DeviceAddress;//地址码
        //        byDataToBeSent[1] = (byte)FuncCode;//功能码

        //        byte[] byBeginAddress = CConverter.ToBytes((short)BeginAddress, ParaBytesFormat);//起始地址(2字节)
        //        byDataToBeSent[2] = byBeginAddress[0];// 起始地址 - 高字节  [1]
        //        byDataToBeSent[3] = byBeginAddress[1];// 起始地址 - 低字节  [0]

        //        //数据位长度 - 位的数据长度
        //        byte[] byDataLength = CConverter.ToBytes((short)DataLength, ParaBytesFormat);
        //        byDataToBeSent[4] = byDataLength[0];// 数据长度 - 高字节  [1]
        //        byDataToBeSent[5] = byDataLength[1];// 数据长度 - 低字节  [0]

        //        // 字节计数
        //        byte byByteCount = Convert.ToByte(iDataByteLength);//bool[]数组长度 / 8 = 字节数
        //        byDataToBeSent[6] = byByteCount;

        //        for (int i = 0; i < iDataByteLength; i++)
        //        {
        //            byDataToBeSent[7 + i] = Data[i];
        //        }

        //        byte[] byCRCResult = CalcCRC(byDataToBeSent);
        //        byCRCResult = CConverter.Reorder2BytesData(byCRCResult, WriteCoilBytesFormat, 0);

        //        int iDataLengthIncludeCRCData = byDataToBeSent.Length + 2;//数组长度 + 2个字节的CRC

        //        Array.Resize<byte>(ref byDataToBeSent, iDataLengthIncludeCRCData);//将原有数组长度 + 2，然后将CRC校验值添加到数组的最后2个字节
        //        byDataToBeSent[iDataLengthIncludeCRCData - 2] = byCRCResult[0];//
        //        byDataToBeSent[iDataLengthIncludeCRCData - 1] = byCRCResult[1];//

        //        byCRCResult = null;
        //    }
        //    catch (Exception)// ex)
        //    {
        //        //Enqueue("封装Multi写命令发生错误:" + ex.Message + "; " + ex.StackTrace);
        //    }

        //    return byDataToBeSent;
        //}

        #endregion

        ///// <summary>
        ///// TBD -- 解析返回信息的错误代码
        ///// </summary>
        ///// <param name="MsgWithErrorCode">从站返回的完整字符串(含错误信息)</param>
        ///// <returns></returns>
        //string AnalysisErrorCode(string MsgWithErrorCode)
        //{
        //    return "";
        //}

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
                //string sTemp = "从站[" + Msg.SlaveAddress.ToString() + "], 功能码[" + Msg.FuncCode.ToString() + "](" + Msg.FuncDescription + "), 错误码[" + Msg.ErrorCode.ToString() + "], 错误信息[" + Msg.ErrorMsg + "]";

                Enqueue(Msg.ErrorMsg);

                return Msg.ErrorMsg;
            }
        }

        ///// <summary>
        ///// 创建读取命令的字节数组，可以直接发送这个字节数组到串口端口
        ///// </summary>
        ///// <param name="DeviceAddress">从站地址</param>
        ///// <param name="ReadFunctionCode">读取功能码</param>
        ///// <param name="BeginReadAddress">读取的起始地址</param>
        ///// <param name="ReadDataLength">读取数据长度，有效值范围：1~2000(位)</param>
        ///// <returns></returns>
        //private byte[] MakeReadCmd(byte DeviceAddress, byte ReadFunctionCode, ushort BeginReadAddress, ushort ReadDataLength)
        //{
        //    //byte[] byResultData = null;// new byte[15];
        //    //return byResultData;
        //    return null;
        //}

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

            //if (BytesData == null) return null;

            //if (SetCalcLengthOfBytes == 0 || SetCalcLengthOfBytes > BytesData.Length)
            //{
            //    SetCalcLengthOfBytes = BytesData.Length;
            //}

            //ushort usCrcResultValue = ushort.MaxValue;
            
            //try
            //{
            //    for (int i = 0; i < SetCalcLengthOfBytes; i++)
            //    {
            //        //值对于无符号的字节太大或太小。
            //        byte byIndexInCrcTable = (byte)(usCrcResultValue ^ BytesData[i]);//XOR
            //        usCrcResultValue >>= 8;//右移8位
            //        usCrcResultValue ^= CRCMatchTable[byIndexInCrcTable];//与CRC表对应位置进行XOR
            //    }
            //}
            //catch (Exception)
            //{
            //    return null;
            //}
            
            //return BitConverter.GetBytes(usCrcResultValue);
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

            //计算CRC结果与收到的CRC值匹配就判断接收到的数据是OK，否则是数据有错误
            if (byCRCResult[0] == ReceivedRTUFrame[iLength - 2]//计算结果CRC[0] 与 收到的CRC[0]进行比较
                && byCRCResult[1] == ReceivedRTUFrame[iLength - 1])//计算结果CRC[1] 与 收到的CRC[1]进行比较
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        #region "下面代码移到 Converter 项目下面的 Converter 类"

        ///// <summary>
        ///// CRC校验码匹配表：32*8 = 256
        ///// </summary>
        //private static readonly ushort[] CRCMatchTable =
        //{
        //    #region "CRC校验码匹配表：32*8 = 256"

        //    0X0000, 0XC0C1, 0XC181, 0X0140, 0XC301, 0X03C0, 0X0280, 0XC241,
        //    0XC601, 0X06C0, 0X0780, 0XC741, 0X0500, 0XC5C1, 0XC481, 0X0440,
        //    0XCC01, 0X0CC0, 0X0D80, 0XCD41, 0X0F00, 0XCFC1, 0XCE81, 0X0E40,
        //    0X0A00, 0XCAC1, 0XCB81, 0X0B40, 0XC901, 0X09C0, 0X0880, 0XC841,
        //    0XD801, 0X18C0, 0X1980, 0XD941, 0X1B00, 0XDBC1, 0XDA81, 0X1A40,
        //    0X1E00, 0XDEC1, 0XDF81, 0X1F40, 0XDD01, 0X1DC0, 0X1C80, 0XDC41,
        //    0X1400, 0XD4C1, 0XD581, 0X1540, 0XD701, 0X17C0, 0X1680, 0XD641,
        //    0XD201, 0X12C0, 0X1380, 0XD341, 0X1100, 0XD1C1, 0XD081, 0X1040,
        //    0XF001, 0X30C0, 0X3180, 0XF141, 0X3300, 0XF3C1, 0XF281, 0X3240,
        //    0X3600, 0XF6C1, 0XF781, 0X3740, 0XF501, 0X35C0, 0X3480, 0XF441,
        //    0X3C00, 0XFCC1, 0XFD81, 0X3D40, 0XFF01, 0X3FC0, 0X3E80, 0XFE41,
        //    0XFA01, 0X3AC0, 0X3B80, 0XFB41, 0X3900, 0XF9C1, 0XF881, 0X3840,
        //    0X2800, 0XE8C1, 0XE981, 0X2940, 0XEB01, 0X2BC0, 0X2A80, 0XEA41,
        //    0XEE01, 0X2EC0, 0X2F80, 0XEF41, 0X2D00, 0XEDC1, 0XEC81, 0X2C40,
        //    0XE401, 0X24C0, 0X2580, 0XE541, 0X2700, 0XE7C1, 0XE681, 0X2640,
        //    0X2200, 0XE2C1, 0XE381, 0X2340, 0XE101, 0X21C0, 0X2080, 0XE041,
        //    0XA001, 0X60C0, 0X6180, 0XA141, 0X6300, 0XA3C1, 0XA281, 0X6240,
        //    0X6600, 0XA6C1, 0XA781, 0X6740, 0XA501, 0X65C0, 0X6480, 0XA441,
        //    0X6C00, 0XACC1, 0XAD81, 0X6D40, 0XAF01, 0X6FC0, 0X6E80, 0XAE41,
        //    0XAA01, 0X6AC0, 0X6B80, 0XAB41, 0X6900, 0XA9C1, 0XA881, 0X6840,
        //    0X7800, 0XB8C1, 0XB981, 0X7940, 0XBB01, 0X7BC0, 0X7A80, 0XBA41,
        //    0XBE01, 0X7EC0, 0X7F80, 0XBF41, 0X7D00, 0XBDC1, 0XBC81, 0X7C40,
        //    0XB401, 0X74C0, 0X7580, 0XB541, 0X7700, 0XB7C1, 0XB681, 0X7640,
        //    0X7200, 0XB2C1, 0XB381, 0X7340, 0XB101, 0X71C0, 0X7080, 0XB041,
        //    0X5000, 0X90C1, 0X9181, 0X5140, 0X9301, 0X53C0, 0X5280, 0X9241,
        //    0X9601, 0X56C0, 0X5780, 0X9741, 0X5500, 0X95C1, 0X9481, 0X5440,
        //    0X9C01, 0X5CC0, 0X5D80, 0X9D41, 0X5F00, 0X9FC1, 0X9E81, 0X5E40,
        //    0X5A00, 0X9AC1, 0X9B81, 0X5B40, 0X9901, 0X59C0, 0X5880, 0X9841,
        //    0X8801, 0X48C0, 0X4980, 0X8941, 0X4B00, 0X8BC1, 0X8A81, 0X4A40,
        //    0X4E00, 0X8EC1, 0X8F81, 0X4F40, 0X8D01, 0X4DC0, 0X4C80, 0X8C41,
        //    0X4400, 0X84C1, 0X8581, 0X4540, 0X8701, 0X47C0, 0X4680, 0X8641,
        //    0X8201, 0X42C0, 0X4380, 0X8341, 0X4100, 0X81C1, 0X8081, 0X4040
            
        //    #endregion
        //};

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
        ///// 将16进制字符串(用字符进行分隔)转换为RTU码字符串
        ///// </summary>
        ///// <param name="HexStringSplitByChar">16进制字符串(用字符进行分隔)</param>
        ///// <param name="SplitChar">分割字符</param>
        ///// <returns></returns>
        //public static string HexStringSplitByCharToRTUString(string HexStringSplitByChar, char SplitChar = ' ')
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
        //public static bool[] ByteToBitArray(byte ByteValue)
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