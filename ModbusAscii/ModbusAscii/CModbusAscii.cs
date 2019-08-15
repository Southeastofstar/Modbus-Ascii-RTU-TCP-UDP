using System;
using System.Collections.Generic;
using System.Text;
using System.IO.Ports;
using System.Windows.Forms;
using System.Diagnostics;
using Microsoft.VisualBasic;

using ThreadLock;

namespace ModbusAscii
{
    //【特别声明：此代码为彭东南个人原创，如需要商用Modbus-Ascii/RTU/TCP/UDP(支持跨线程读写操作)，请联系作者打赏或者购买DLL或源代码】

    #region "MODBUS ASCII 报文格式 -- 使用ASCII码进行发送和接收(先转换为ASCII码，然后再转换为二进制码进行发送)"

    //起始位                        地址码    功能码    数据      CRC校验       结束符
    //3.5个字符或更多倍数的字符   

    //01 - 读取线圈状态
    //:         地址码         功能码    设置起始地址   读取数量    LRC     回车+换行符
    //1字符     2字符          2字符     4字符          4字符      2字符    2字符
    //1字节     1字节          1字节     2字节          2字节      1字节    2字节

    //02 - 读取输入状态
    //:         地址码         功能码    设置起始地址   读取数量    LRC     回车+换行符
    //1字符     2字符          2字符     4字符          4字符      2字符    2字符
    //1字节     1字节          1字节     2字节          2字节      1字节    2字节

    //03 - 读保持寄存器(short: -32,768 到 32,767)
    //:         地址码         功能码    设置起始地址   读取数量    LRC     回车+换行符
    //1字符     2字符          2字符     4字符          4字符      2字符    2字符
    //1字节     1字节          1字节     2字节          2字节      1字节    2字节

    //04 - 输入寄存器(short:  -32,768 到 32,767)
    //:         地址码         功能码    设置起始地址   读取数量    LRC     回车+换行符
    //1字符     2字符          2字符     4字符          4字符      2字符    2字符
    //1字节     1字节          1字节     2字节          2字节      1字节    2字节

    //05 - 设置单个继电器状态
    //:         地址码         功能码    设置起始地址   设置值      LRC     回车+换行符
    //1字符     2字符          2字符     4字符          4字符      2字符    2字符
    //1字节     1字节          1字节     2字节          2字节      1字节    2字节

    //06 - 设置单个保持寄存器(short:  -32,768 到 32,767)
    //:         地址码         功能码    设置起始地址   设置值      LRC     回车+换行符
    //1字符     2字符          2字符     4字符          4字符      2字符    2字符
    //1字节     1字节          1字节     2字节          2字节      1字节    2字节

    //0F(15) - 设置多个继电器状态
    //:         地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      LRC      回车+换行符
    //1字符     2字符          2字符     4字符          4字符      2字符      2*N字符      1字符    2字符
    //1字节     1字节          1字节     2字节          2字节      1字节      N字节        1字节    2字节

    //10(16) - 设置多个保持寄存器(short:  -32,768 到 32,767)
    //:         地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      LRC      回车+换行符
    //1字符     2字符          2字符     4字符          4字符      2字符      2*N字符      1字符    2字符
    //1字节     1字节          1字节     2字节          2字节      1字节      N字节        1字节    2字节

    #endregion

    /// <summary>
    /// Modbus Ascii通讯类，不支持跨线程读写操作；如需支持跨线程安全读写代码，请联系作者打赏或者购买
    /// </summary>
    public class CModbusAscii
    {
        /// <summary>
        /// Modbus Ascii通讯类的构造函数
        /// </summary>
        /// <param name="RS232CPortName">串口通讯的端口名称</param>
        /// <param name="baudrate">波段率</param>
        /// <param name="parity">奇偶校验</param>
        /// <param name="stopbits">停止位</param>
        /// <param name="writetimeout">写超时时间</param>
        /// <param name="readtimeout">读超时时间</param>
        public CModbusAscii(string RS232CPortName, eBaudRate baudrate = eBaudRate.Rate_19200,
            Parity parity = Parity.Even, StopBits stopbits = StopBits.One, int writetimeout = 1000,
            int readtimeout = 1000)
        {
            try
            {
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

                RS232CPort = new SerialPort();
                RS232CPort.PortName = RS232CPortName;

                BaudRateSetting(baudrate);

                RS232CPort.Encoding = Encoding.UTF8;
                RS232CPort.Parity = parity;
                RS232CPort.StopBits = stopbits;
                RS232CPort.WriteTimeout = writetimeout;
                RS232CPort.ReadTimeout = readtimeout;
                RS232CPort.NewLine = "\r\n";//Modbus Ascii 码通讯的结束符是：回车+换行
                
                RS232CPort.Open();
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                if (ShowMessageDialog == true)
                {
                    MessageBox.Show("Modbus Ascii 通讯类初始化时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        #region "变量定义"
        
        /// <summary>
        /// 软件作者：彭东南, southeastofstar@163.com
        /// </summary>
        public string Author
        {
            get { return "【软件作者：彭东南, southeastofstar@163.com】"; }
        }

        /// <summary>
        /// 处理从站返回的信息时使用处理字节的方式
        /// </summary>
        public bool ProcessFeedbackDataByBytes = true;

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
        public readonly byte[] SuffixBytes = new byte[] { 0x0D, 0x0A };

        /// <summary>
        /// 进行Modbus Ascii通讯的串口实例化对象
        /// </summary>
        SerialPort RS232CPort = null;

        /// <summary>
        /// 波特率设置
        /// </summary>
        eBaudRate eBaudRateSetting = eBaudRate.Rate_19200;

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

        #region "读 - ok"

        // 起始地址：0x0000~0xFFFF -- 0~65535
        // 读取数量：0x001~0x7D0 -- 1~2000

        #region "读单个/多个线圈 - ok"

        // ok
        /// <summary>
        /// 读取从站单个线圈状态(位 - bit)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="BitValue">返回线圈的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilBit(byte DeviceAddress, ushort BeginAddress, ref bool BitValue)//ushort ReadDataLength 读取数据长度,   , out string ReadBackData)
        {
            try
            {
                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                //起始位	    设备地址	功能代码	起始地址   	读取数据长度   LRC校验	   结束符
                //1个字符(:)	2个字符	    2个字符	    4个字符     4个字符	       2个字符	   2个字符(回车+换行)
                //1个字节       1个字节     1个字节     2个字节     2个字节        1个字节     2个字节

                byte[] byResult = MakeReadCmd(DeviceAddress, CModbusFunctionCode.ReadCoil, BeginAddress, 1);//ReadDataLength

                string sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)
                {
                    Enqueue( "发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();
                
                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"
                    
                    if (null == byReadData || byReadData.Length < 2)
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //ReadBackData = sFeedBackFromSlave;

                        //(COM1)发送字符串 - :010100000001FD
                        //(COM1)收到字符串 - :01010100FD

                        //(COM1)发送字符串 - :010100000001FD
                        //(COM1)收到字符串 - :01010101FC

                        //(COM1)发送字符串 - :010100000001FD
                        //(COM1)收到字符串 - :01010100FD   -- 读取成功

                        //(COM1)发送字符串 - :010100000001FD
                        //(COM1)收到字符串 - :0181017D   -- 读取错误
                        //if (byReadData[0] == PrefixByte && byReadData[1] == CModbusFunctionCode.WriteCoil)//
                        //{
                        //}

                        //(COM1)收到字符串 - :01010101FC

                        //**********************
                        //首字符
                        //[0] - :

                        //从站地址
                        //[1] - 0
                        //[2] - 1

                        //读取成功的功能码
                        //[3] - 0
                        //[4] - 1

                        //读取失败的错误码
                        //[3] - 0
                        //[4] - 8
                        //**********************

                        #endregion

                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 1
                        
                        string sCmd = CModbusFunctionCode.ReadCoil.ToString("X2");

                        //if (cByte3 == 0x0.ToString()[0] && cByte4 == CModbusFunctionCode.ReadCoil.ToString()[0])//:0101
                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])
                        {
                            //读取成功
                            if (Strings.Mid(sFeedBackFromSlave, 9, 1) == "0")
                            {
                                //读取的值是0
                                BitValue = false;
                            }
                            else
                            {
                                //读取的值是1
                                BitValue = true;
                            }

                            byReadData = null;

                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);

                            byReadData = null;

                            sFeedBackFromSlave = null;

                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //ReadBackData = sFeedBackFromSlave;

                        //(COM1)发送字符串 - :010100000001FD
                        //(COM1)收到字符串 - :01010100FD

                        //(COM1)发送字符串 - :010100000001FD
                        //(COM1)收到字符串 - :01010101FC

                        //(COM1)发送字符串 - :010100000001FD
                        //(COM1)收到字符串 - :01010100FD   -- 读取成功

                        //(COM1)发送字符串 - :010100000001FD
                        //(COM1)收到字符串 - :0181017D   -- 读取错误
                        
                        //(COM1)收到字符串 - :01010101FC

                        #endregion

                        //:01010101FC\r\n
                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.ReadCoil.ToString("X2"))//"01"
                        {
                            //读取成功
                            if (Strings.Mid(sFeedBackFromSlave, 9, 1) == "0")
                            {
                                //读取的值是0
                                BitValue = false;
                            }
                            else
                            {
                                //读取的值是1
                                BitValue = true;
                            }

                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            //读取错误
                            //throw new Exception("");

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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok
        /// <summary>
        /// 读取从站多个字节的线圈状态，函数是以字节为读取数据长度单位
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字节</param>
        /// <param name="BitValue">返回线圈的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilBit(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref bool[] BitValue)//  , out string ReadBackData)
        {
            try
            {
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

                //起始位	    设备地址	功能代码	起始地址   	读取数据长度   LRC校验	   结束符
                //1个字符(:)	2个字符	    2个字符	    4个字符     4个字符	       2个字符	   2个字符(回车+换行)
                //1个字节       1个字节     1个字节     2个字节     2个字节        1个字节     2个字节
                byte[] byResult = MakeReadCmd(DeviceAddress, CModbusFunctionCode.ReadCoil, BeginAddress, Convert.ToUInt16(ReadDataLength * 8));//

                string sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)
                {
                    Enqueue( "发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"
                    
                    if (null == byReadData || byReadData.Length < 2)
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101FFFE
                        
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :0101017F7E

                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :0101017786

                        //**********************
                        //首字符
                        //[0] - :

                        //从站地址
                        //[1] - 0
                        //[2] - 1

                        //读取成功的功能码
                        //[3] - 0
                        //[4] - 1

                        //读取失败的错误码
                        //[3] - 0
                        //[4] - 8
                        //**********************

                        //从站：10101010
                        //:   0  1  0  1  0  1  
                        //3A 30 31 30 31 30 31 41 41 35 33 0D 0A
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101AA53

                        //从站：10100010
                        //3A 30 31 30 31 30 31 41 32 35 42 0D 0A
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101A25B

                        //从站：11110010
                        //3A 30 31 30 31 30 31 46 32 30 42 0D 0A
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101F20B

                        //00000000
                        //成功执行读命令，结果值：False False False False False False False False 
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :01010100FD

                        //11010001
                        //成功执行读命令，结果值：True True False True False False False True 
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101D12C

                        //11011111
                        //成功执行读命令，结果值：True True False True True True True True 
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101DF1E

                        //2个字节的通信记录
                        //0000000011011111
                        //成功执行读命令，结果值：False False False False False False False False True True False True True True True True 
                        //(COM1)发送字符串 - :010100000010EE
                        //(COM1)收到字符串 - :010102DF001D

                        //1000010011011111
                        //成功执行读命令，结果值：True False False False False True False False True True False True True True True True 
                        //(COM1)发送字符串 - :010100000010EE
                        //(COM1)收到字符串 - :010102DF8499

                        //1111010011011111
                        //成功执行读命令，结果值：True True True True False True False False True True False True True True True True 
                        //(COM1)发送字符串 - :010100000010EE
                        //(COM1)收到字符串 - :010102DFF429

                        #endregion

                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 1

                        //string sTempResult = BytesToHexStringSplitByChar(byReadData);

                        string sCmd = CModbusFunctionCode.ReadCoil.ToString("X2");

                        //if (cByte3 == 0x0.ToString()[0] && cByte4 == CModbusFunctionCode.ReadCoil.ToString()[0])//:0101
                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])
                        {
                            #region "No use"

                            //bool[] bReadResultData = new bool[ReadDataLength * 8];

                            //for (int i = 0; i < ReadDataLength; i++)//函数是以字节为单位
                            //{
                            //    byte[] byTemp = new byte[4];
                            //    byTemp[0] = byReadData[i + 7];
                            //    byTemp[1] = byReadData[i + 7 + 1];

                            //    int iTemp = BitConverter.ToInt16(byTemp, 0);
                            //    string sTemp = iTemp.ToString("X4");

                            //    sTemp = Strings.Mid(sTemp, 3, 2);


                            //    bool[] bTemp = ByteToBitArray(byReadData[i + 7]);//返回数据的值是从第7个字节开始的，2个字节一组
                            //    for (int j = 0; j < bTemp.Length; j++)
                            //    {
                            //        bReadResultData[i * 8 + j] = bTemp[j];
                            //    }
                            //}

                            #endregion

                            byte[] byReadResultData = new byte[ReadDataLength];
                            //bool[] bReadResultData = new bool[ReadDataLength * 8];
                            BitValue = new bool[ReadDataLength * 8];

                            for (int i = 0; i < ReadDataLength; i++)
                            {
                                string sTemp = Strings.Mid(sFeedBackFromSlave, 8 + i * 2, 2);
                                byReadResultData[i] = TwoHexCharsToByte(sTemp);

                                bool[] bTemp = ByteToBitArray(byReadResultData[i]);
                                for (int j = 0; j < bTemp.Length; j++)
                                {
                                    BitValue[i * 8 + j] = bTemp[j];//bReadResultData
                                }
                            }

                            //BitValue = bReadResultData;

                            //bReadResultData = null;
                            byReadResultData = null;
                            
                            //BitValue = bReadResultData;

                            //bReadResultData = null;

                            byReadData = null;
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);

                            byReadData = null;
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)//null == byReadData || byReadData.Length < 2
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //**********
                        //10100000
                        //成功执行读命令，结果值：False False False False False True False True 
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101A05D

                        //***********
                        //11110000
                        //成功执行读命令，结果值：False False False False True True True True 
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101F00D

                        //***********
                        //10010011
                        //成功执行读命令，结果值：True True False False True False False True 
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101936A

                        //成功执行读命令，结果值：False True False True False False False True 
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :0101018A73

                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :01010159A4
                        //成功执行读命令，结果值：True False False True True False True False 

                        //成功执行读命令，结果值：True True False True False False True False False False False True False True False False 
                        //(COM1)发送字符串 - :010100000010EE
                        //(COM1)收到字符串 - :0101024B2889

                        //成功执行读命令，结果值：True True False True False False True False False False False True True True True True 
                        //(COM1)发送字符串 - :010100000010EE
                        //(COM1)收到字符串 - :0101024BF8B9

                        //:010102A44612\r\n

                        #endregion

                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.ReadCoil.ToString("X2"))//"01"
                        {
                            byte[] byReadResultData = new byte[ReadDataLength];
                            bool[] bReadResultData = new bool[ReadDataLength * 8];

                            for (int i = 0; i < ReadDataLength; i++)
                            {
                                string sTemp = Strings.Mid(sFeedBackFromSlave, 8 + i * 2, 2);
                                byReadResultData[i] = TwoHexCharsToByte(sTemp);

                                bool[] bTemp = ByteToBitArray(byReadResultData[i]);
                                for (int j = 0; j < bTemp.Length; j++)
                                {
                                    bReadResultData[i * 8 + j] = bTemp[j];
                                }
                            }

                            BitValue = bReadResultData;

                            bReadResultData = null;
                            byReadResultData = null;

                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);

                            sFeedBackFromSlave = null;

                            //throw new Exception("");
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok
        /// <summary>
        /// 读取从站1个字节的线圈状态(1个字节 = 8位 - bit)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ByteValue">返回线圈的1个字节当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilByte(byte DeviceAddress, ushort BeginAddress, ref byte ByteValue)//  , out string ReadBackData)
        {
            try
            {
                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 1 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //起始位	    设备地址	功能代码	起始地址   	读取数据长度   LRC校验	   结束符
                //1个字符(:)	2个字符	    2个字符	    4个字符     4个字符	       2个字符	   2个字符(回车+换行)
                //1个字节       1个字节     1个字节     2个字节     2个字节        1个字节     2个字节
                byte[] byResult = MakeReadCmd(DeviceAddress, CModbusFunctionCode.ReadCoil, BeginAddress, Convert.ToUInt16(1 * 8));//

                string sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)
                {
                    Enqueue( "发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"
                    
                    if (null == byReadData || byReadData.Length < 2)
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101FFFE
                        
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :0101017F7E

                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :0101017786

                        //**********************
                        //首字符
                        //[0] - :

                        //从站地址
                        //[1] - 0
                        //[2] - 1

                        //读取成功的功能码
                        //[3] - 0
                        //[4] - 1

                        //读取失败的错误码
                        //[3] - 0
                        //[4] - 8
                        //**********************

                        //从站：10101010
                        //:   0  1  0  1  0  1  
                        //3A 30 31 30 31 30 31 41 41 35 33 0D 0A
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101AA53

                        //从站：10100010
                        //3A 30 31 30 31 30 31 41 32 35 42 0D 0A
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101A25B

                        //从站：11110010
                        //3A 30 31 30 31 30 31 46 32 30 42 0D 0A
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101F20B

                        //00000000
                        //成功执行读命令，结果值：False False False False False False False False 
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :01010100FD

                        //11010001
                        //成功执行读命令，结果值：True True False True False False False True 
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101D12C

                        //11011111
                        //成功执行读命令，结果值：True True False True True True True True 
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101DF1E

                        //2个字节的通信记录
                        //0000000011011111
                        //成功执行读命令，结果值：False False False False False False False False True True False True True True True True 
                        //(COM1)发送字符串 - :010100000010EE
                        //(COM1)收到字符串 - :010102DF001D

                        //1000010011011111
                        //成功执行读命令，结果值：True False False False False True False False True True False True True True True True 
                        //(COM1)发送字符串 - :010100000010EE
                        //(COM1)收到字符串 - :010102DF8499

                        //1111010011011111
                        //成功执行读命令，结果值：True True True True False True False False True True False True True True True True 
                        //(COM1)发送字符串 - :010100000010EE
                        //(COM1)收到字符串 - :010102DFF429

                        #endregion

                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 1

                        //string sTempResult = BytesToHexStringSplitByChar(byReadData);

                        string sCmd = CModbusFunctionCode.ReadCoil.ToString("X2");

                        //if (cByte3 == 0x0.ToString()[0] && cByte4 == CModbusFunctionCode.ReadCoil.ToString()[0])//:0101
                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])
                        {
                            string sTemp = Strings.Mid(sFeedBackFromSlave, 8, 2);
                            ByteValue = TwoHexCharsToByte(sTemp);                            

                            byReadData = null;
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);

                            byReadData = null;
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)//null == byReadData || byReadData.Length < 2
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //**********
                        //10100000
                        //成功执行读命令，结果值：False False False False False True False True 
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101A05D

                        //***********
                        //11110000
                        //成功执行读命令，结果值：False False False False True True True True 
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101F00D

                        //***********
                        //10010011
                        //成功执行读命令，结果值：True True False False True False False True 
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101936A

                        //成功执行读命令，结果值：False True False True False False False True 
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :0101018A73

                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :01010159A4
                        //成功执行读命令，结果值：True False False True True False True False 

                        //成功执行读命令，结果值：True True False True False False True False False False False True False True False False 
                        //(COM1)发送字符串 - :010100000010EE
                        //(COM1)收到字符串 - :0101024B2889

                        //成功执行读命令，结果值：True True False True False False True False False False False True True True True True 
                        //(COM1)发送字符串 - :010100000010EE
                        //(COM1)收到字符串 - :0101024BF8B9

                        //:010102A44612\r\n

                        #endregion

                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.ReadCoil.ToString("X2"))//"01"
                        {
                            string sTemp = Strings.Mid(sFeedBackFromSlave, 8, 2);
                            ByteValue = TwoHexCharsToByte(sTemp);
                            
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);

                            sFeedBackFromSlave = null;
                            //throw new Exception("");
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok
        /// <summary>
        /// 读取从站多个字节的线圈状态(1个字节 = 位 - bit)，函数是以字节为读取数据长度单位
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字节</param>
        /// <param name="ByteValue">返回线圈的字节当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilByte(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref byte[] ByteValue)//  , out string ReadBackData)
        {
            try
            {
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

                //起始位	    设备地址	功能代码	起始地址   	读取数据长度   LRC校验	   结束符
                //1个字符(:)	2个字符	    2个字符	    4个字符     4个字符	       2个字符	   2个字符(回车+换行)
                //1个字节       1个字节     1个字节     2个字节     2个字节        1个字节     2个字节
                byte[] byResult = MakeReadCmd(DeviceAddress, CModbusFunctionCode.ReadCoil, BeginAddress, Convert.ToUInt16(ReadDataLength * 8));//

                string sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)
                {
                    Enqueue( "发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"
                    
                    if (null == byReadData || byReadData.Length < 2)
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101FFFE
                        
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :0101017F7E

                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :0101017786

                        //**********************
                        //首字符
                        //[0] - :

                        //从站地址
                        //[1] - 0
                        //[2] - 1

                        //读取成功的功能码
                        //[3] - 0
                        //[4] - 1

                        //读取失败的错误码
                        //[3] - 0
                        //[4] - 8
                        //**********************

                        //从站：10101010
                        //:   0  1  0  1  0  1  
                        //3A 30 31 30 31 30 31 41 41 35 33 0D 0A
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101AA53

                        //从站：10100010
                        //3A 30 31 30 31 30 31 41 32 35 42 0D 0A
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101A25B

                        //从站：11110010
                        //3A 30 31 30 31 30 31 46 32 30 42 0D 0A
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101F20B

                        //00000000
                        //成功执行读命令，结果值：False False False False False False False False 
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :01010100FD

                        //11010001
                        //成功执行读命令，结果值：True True False True False False False True 
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101D12C

                        //11011111
                        //成功执行读命令，结果值：True True False True True True True True 
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101DF1E

                        //2个字节的通信记录
                        //0000000011011111
                        //成功执行读命令，结果值：False False False False False False False False True True False True True True True True 
                        //(COM1)发送字符串 - :010100000010EE
                        //(COM1)收到字符串 - :010102DF001D

                        //1000010011011111
                        //成功执行读命令，结果值：True False False False False True False False True True False True True True True True 
                        //(COM1)发送字符串 - :010100000010EE
                        //(COM1)收到字符串 - :010102DF8499

                        //1111010011011111
                        //成功执行读命令，结果值：True True True True False True False False True True False True True True True True 
                        //(COM1)发送字符串 - :010100000010EE
                        //(COM1)收到字符串 - :010102DFF429

                        #endregion

                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 1

                        //string sTempResult = BytesToHexStringSplitByChar(byReadData);

                        string sCmd = CModbusFunctionCode.ReadCoil.ToString("X2");

                        //if (cByte3 == 0x0.ToString()[0] && cByte4 == CModbusFunctionCode.ReadCoil.ToString()[0])//:0101
                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])
                        {
                            ByteValue = new byte[ReadDataLength];

                            for (int i = 0; i < ReadDataLength; i++)
                            {
                                string sTemp = Strings.Mid(sFeedBackFromSlave, 8 + i * 2, 2);
                                ByteValue[i] = TwoHexCharsToByte(sTemp);
                            }

                            byReadData = null;
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);

                            byReadData = null;
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)//null == byReadData || byReadData.Length < 2
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //**********
                        //10100000
                        //成功执行读命令，结果值：False False False False False True False True 
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101A05D

                        //***********
                        //11110000
                        //成功执行读命令，结果值：False False False False True True True True 
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101F00D

                        //***********
                        //10010011
                        //成功执行读命令，结果值：True True False False True False False True 
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101936A

                        //成功执行读命令，结果值：False True False True False False False True 
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :0101018A73

                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :01010159A4
                        //成功执行读命令，结果值：True False False True True False True False 

                        //成功执行读命令，结果值：True True False True False False True False False False False True False True False False 
                        //(COM1)发送字符串 - :010100000010EE
                        //(COM1)收到字符串 - :0101024B2889

                        //成功执行读命令，结果值：True True False True False False True False False False False True True True True True 
                        //(COM1)发送字符串 - :010100000010EE
                        //(COM1)收到字符串 - :0101024BF8B9

                        //:010102A44612\r\n

                        #endregion

                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.ReadCoil.ToString("X2"))//"01"
                        {
                            ByteValue = new byte[ReadDataLength];

                            for (int i = 0; i < ReadDataLength; i++)
                            {
                                string sTemp = Strings.Mid(sFeedBackFromSlave, 8 + i * 2, 2);
                                ByteValue[i] = TwoHexCharsToByte(sTemp);
                            }
                            
                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);

                            sFeedBackFromSlave = null;

                            //throw new Exception("");
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok
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
                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //起始位	    设备地址	功能代码	起始地址   	读取数据长度   LRC校验	   结束符
                //1个字符(:)	2个字符	    2个字符	    4个字符     4个字符	       2个字符	   2个字符(回车+换行)
                //1个字节       1个字节     1个字节     2个字节     2个字节        1个字节     2个字节
                byte[] byResult = MakeReadCmd(DeviceAddress, CModbusFunctionCode.ReadCoil, BeginAddress, Convert.ToUInt16(2 * 8));//1个字 = 2 个字节 = 16位

                string sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)
                {
                    Enqueue( "发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"
                    
                    if (null == byReadData || byReadData.Length < 2)
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101FFFE
                        
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :0101017F7E

                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :0101017786

                        //**********************
                        //首字符
                        //[0] - :

                        //从站地址
                        //[1] - 0
                        //[2] - 1

                        //读取成功的功能码
                        //[3] - 0
                        //[4] - 1

                        //读取失败的错误码
                        //[3] - 0
                        //[4] - 8
                        //**********************

                        //从站：10101010
                        //:   0  1  0  1  0  1  
                        //3A 30 31 30 31 30 31 41 41 35 33 0D 0A
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101AA53

                        //从站：10100010
                        //3A 30 31 30 31 30 31 41 32 35 42 0D 0A
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101A25B

                        //从站：11110010
                        //3A 30 31 30 31 30 31 46 32 30 42 0D 0A
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101F20B

                        //00000000
                        //成功执行读命令，结果值：False False False False False False False False 
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :01010100FD

                        //11010001
                        //成功执行读命令，结果值：True True False True False False False True 
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101D12C

                        //11011111
                        //成功执行读命令，结果值：True True False True True True True True 
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101DF1E

                        //2个字节的通信记录
                        //0000000011011111
                        //成功执行读命令，结果值：False False False False False False False False True True False True True True True True 
                        //(COM1)发送字符串 - :010100000010EE
                        //(COM1)收到字符串 - :010102DF001D

                        //1000010011011111
                        //成功执行读命令，结果值：True False False False False True False False True True False True True True True True 
                        //(COM1)发送字符串 - :010100000010EE
                        //(COM1)收到字符串 - :010102DF8499

                        //1111010011011111
                        //成功执行读命令，结果值：True True True True False True False False True True False True True True True True 
                        //(COM1)发送字符串 - :010100000010EE
                        //(COM1)收到字符串 - :010102DFF429

                        #endregion

                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 1

                        //string sTempResult = BytesToHexStringSplitByChar(byReadData);

                        string sCmd = CModbusFunctionCode.ReadCoil.ToString("X2");

                        //if (cByte3 == 0x0.ToString()[0] && cByte4 == CModbusFunctionCode.ReadCoil.ToString()[0])//:0101
                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])
                        {
                            string sTemp = Strings.Mid(sFeedBackFromSlave, 8, 4);
                            sTemp = Strings.Mid(sTemp, 3, 2) + Strings.Mid(sTemp, 1, 2);//高低字节互换：D67C --> 7CD6

                            int iValue = HexStringToInt(sTemp);
                            byte[] bytesOfInt = BitConverter.GetBytes(iValue);
                            Value = BitConverter.ToInt16(bytesOfInt, 0);

                            bytesOfInt = null;

                            byReadData = null;
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);

                            byReadData = null;
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)//null == byReadData || byReadData.Length < 2
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //**********
                        //10100000
                        //成功执行读命令，结果值：False False False False False True False True 
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101A05D

                        //***********
                        //11110000
                        //成功执行读命令，结果值：False False False False True True True True 
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101F00D

                        //***********
                        //10010011
                        //成功执行读命令，结果值：True True False False True False False True 
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101936A

                        //成功执行读命令，结果值：False True False True False False False True 
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :0101018A73

                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :01010159A4
                        //成功执行读命令，结果值：True False False True True False True False 

                        //成功执行读命令，结果值：True True False True False False True False False False False True False True False False 
                        //(COM1)发送字符串 - :010100000010EE
                        //(COM1)收到字符串 - :0101024B2889

                        //成功执行读命令，结果值：True True False True False False True False False False False True True True True True 
                        //(COM1)发送字符串 - :010100000010EE
                        //(COM1)收到字符串 - :0101024BF8B9

                        //:010102A44612\r\n

                        #endregion

                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.ReadCoil.ToString("X2"))//"01"
                        {
                            string sTemp = Strings.Mid(sFeedBackFromSlave, 8, 4);
                            sTemp = Strings.Mid(sTemp, 3, 2) + Strings.Mid(sTemp, 1, 2);//高低字节互换：D67C --> 7CD6

                            int iValue = HexStringToInt(sTemp);
                            byte[] bytesOfInt = BitConverter.GetBytes(iValue);
                            Value = BitConverter.ToInt16(bytesOfInt, 0);

                            bytesOfInt = null;

                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);

                            sFeedBackFromSlave = null;
                            //throw new Exception("");
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok
        /// <summary>
        /// 读取从站多个字的线圈状态，函数是以字为读取数据长度单位
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

                //起始位	    设备地址	功能代码	起始地址   	读取数据长度   LRC校验	   结束符
                //1个字符(:)	2个字符	    2个字符	    4个字符     4个字符	       2个字符	   2个字符(回车+换行)
                //1个字节       1个字节     1个字节     2个字节     2个字节        1个字节     2个字节
                byte[] byResult = MakeReadCmd(DeviceAddress, CModbusFunctionCode.ReadCoil, BeginAddress, Convert.ToUInt16(ReadDataLength * 2 * 8));//1个字 = 2 个字节 = 16位

                string sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)
                {
                    Enqueue( "发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"
                    
                    if (null == byReadData || byReadData.Length < 2)
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101FFFE
                        
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :0101017F7E

                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :0101017786

                        //**********************
                        //首字符
                        //[0] - :

                        //从站地址
                        //[1] - 0
                        //[2] - 1

                        //读取成功的功能码
                        //[3] - 0
                        //[4] - 1

                        //读取失败的错误码
                        //[3] - 0
                        //[4] - 8
                        //**********************

                        //从站：10101010
                        //:   0  1  0  1  0  1  
                        //3A 30 31 30 31 30 31 41 41 35 33 0D 0A
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101AA53

                        //从站：10100010
                        //3A 30 31 30 31 30 31 41 32 35 42 0D 0A
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101A25B

                        //从站：11110010
                        //3A 30 31 30 31 30 31 46 32 30 42 0D 0A
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101F20B

                        //00000000
                        //成功执行读命令，结果值：False False False False False False False False 
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :01010100FD

                        //11010001
                        //成功执行读命令，结果值：True True False True False False False True 
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101D12C

                        //11011111
                        //成功执行读命令，结果值：True True False True True True True True 
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101DF1E

                        //2个字节的通信记录
                        //0000000011011111
                        //成功执行读命令，结果值：False False False False False False False False True True False True True True True True 
                        //(COM1)发送字符串 - :010100000010EE
                        //(COM1)收到字符串 - :010102DF001D

                        //1000010011011111
                        //成功执行读命令，结果值：True False False False False True False False True True False True True True True True 
                        //(COM1)发送字符串 - :010100000010EE
                        //(COM1)收到字符串 - :010102DF8499

                        //1111010011011111
                        //成功执行读命令，结果值：True True True True False True False False True True False True True True True True 
                        //(COM1)发送字符串 - :010100000010EE
                        //(COM1)收到字符串 - :010102DFF429

                        #endregion

                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 1

                        //string sTempResult = BytesToHexStringSplitByChar(byReadData);

                        string sCmd = CModbusFunctionCode.ReadCoil.ToString("X2");

                        //if (cByte3 == 0x0.ToString()[0] && cByte4 == CModbusFunctionCode.ReadCoil.ToString()[0])//:0101
                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])
                        {
                            short[] stResultValue = new short[ReadDataLength];

                            for (int i = 0; i < ReadDataLength; i++)
                            {
                                string sTemp = Strings.Mid(sFeedBackFromSlave, 8 + i * 4, 4);
                                sTemp = Strings.Mid(sTemp, 3, 2) + Strings.Mid(sTemp, 1, 2);//高低字节互换：D67C --> 7CD6

                                int iValue = HexStringToInt(sTemp);
                                byte[] bytesOfInt = BitConverter.GetBytes(iValue);
                                stResultValue[i] = BitConverter.ToInt16(bytesOfInt, 0);

                                bytesOfInt = null;
                            }

                            Value = stResultValue;
                            stResultValue = null;

                            byReadData = null;
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);

                            byReadData = null;
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)//null == byReadData || byReadData.Length < 2
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //**********
                        //10100000
                        //成功执行读命令，结果值：False False False False False True False True 
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101A05D

                        //***********
                        //11110000
                        //成功执行读命令，结果值：False False False False True True True True 
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101F00D

                        //***********
                        //10010011
                        //成功执行读命令，结果值：True True False False True False False True 
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :010101936A

                        //成功执行读命令，结果值：False True False True False False False True 
                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :0101018A73

                        //(COM1)发送字符串 - :010100000008F6
                        //(COM1)收到字符串 - :01010159A4
                        //成功执行读命令，结果值：True False False True True False True False 

                        //成功执行读命令，结果值：True True False True False False True False False False False True False True False False 
                        //(COM1)发送字符串 - :010100000010EE
                        //(COM1)收到字符串 - :0101024B2889

                        //成功执行读命令，结果值：True True False True False False True False False False False True True True True True 
                        //(COM1)发送字符串 - :010100000010EE
                        //(COM1)收到字符串 - :0101024BF8B9

                        //:010102A44612\r\n

                        #endregion

                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.ReadCoil.ToString("X2"))//"01"
                        {
                            short[] stResultValue = new short[ReadDataLength];

                            for (int i = 0; i < ReadDataLength; i++)
                            {
                                string sTemp = Strings.Mid(sFeedBackFromSlave, 8 + i * 4, 4);
                                sTemp = Strings.Mid(sTemp, 3, 2) + Strings.Mid(sTemp, 1, 2);//高低字节互换：D67C --> 7CD6

                                int iValue = HexStringToInt(sTemp);
                                byte[] bytesOfInt = BitConverter.GetBytes(iValue);
                                stResultValue[i] = BitConverter.ToInt16(bytesOfInt, 0);

                                bytesOfInt = null;
                            }

                            Value = stResultValue;
                            stResultValue = null;
                            
                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);

                            sFeedBackFromSlave = null;

                            //throw new Exception("");
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok
        /// <summary>
        /// 读取从站1个双字的线圈状态(位 - bit)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回线圈的1个双字当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilWord(byte DeviceAddress, ushort BeginAddress, ref int Value)//  , out string ReadBackData)
        {
            try
            {
                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //起始位	    设备地址	功能代码	起始地址   	读取数据长度   LRC校验	   结束符
                //1个字符(:)	2个字符	    2个字符	    4个字符     4个字符	       2个字符	   2个字符(回车+换行)
                //1个字节       1个字节     1个字节     2个字节     2个字节        1个字节     2个字节
                byte[] byResult = MakeReadCmd(DeviceAddress, CModbusFunctionCode.ReadCoil, BeginAddress, Convert.ToUInt16(4 * 8));//1个字 = 2 个字节 = 16位

                string sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)
                {
                    Enqueue( "发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"
                    
                    if (null == byReadData || byReadData.Length < 2)
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"
                        
                        //**********************
                        //首字符
                        //[0] - :

                        //从站地址
                        //[1] - 0
                        //[2] - 1

                        //读取成功的功能码
                        //[3] - 0
                        //[4] - 1

                        //读取失败的错误码
                        //[3] - 0
                        //[4] - 8
                        //**********************



                        #endregion

                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 1

                        //string sTempResult = BytesToHexStringSplitByChar(byReadData);

                        string sCmd = CModbusFunctionCode.ReadCoil.ToString("X2");

                        //if (cByte3 == 0x0.ToString()[0] && cByte4 == CModbusFunctionCode.ReadCoil.ToString()[0])//:0101
                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])
                        {
                            //这里是读取的字节结构：
                            //:010108 FFE36EC4 51F6540B 3C\r\n

                            //这里是在计算器里面转换的HEX字节结构：
                            //                 0B54F651
                            string sTemp = Strings.Mid(sFeedBackFromSlave, 8, 8);//‭10BC1B88‬  //‭‬

                            //读取的字节结构：2078B4EF
                            //转换的HEX字节结构：EFB47820

                            //读取的字节结构：FFE36EC4
                            //转换的HEX字节结构：C46EE3FF
                            string sResult = "";
                            for (int j = sTemp.Length / 2; j > 0; j--)
                            {
                                sResult += Strings.Mid(sTemp, j * 2 - 1, 2);
                            }

                            int iValue = HexStringToInt(sResult);

                            Value = iValue;

                            byReadData = null;
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);

                            byReadData = null;
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)//null == byReadData || byReadData.Length < 2
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //成功执行写命令1214390746
                        //写操作记录：(COM1)发送字符串 - :010F0000002004DA21624827
                        //写操作记录：(COM1)收到字符串 - :010F00000020D0

                        //成功执行读命令，结果值：1214390746
                        //(COM1)发送字符串 - :010100000020DE
                        //(COM1)收到字符串 - :010104DA21624855

                        //成功执行写命令1175543634
                        //写操作记录：(COM1)发送字符串 - :010F0000002004525F1146C4
                        //写操作记录：(COM1)收到字符串 - :010F00000020D0

                        //成功执行读命令，结果值：1175543634
                        //(COM1)发送字符串 - :010100000020DE
                        //(COM1)收到字符串 - :010104525F1146F2

                        //成功执行写命令1930396067
                        //写操作记录：(COM1)发送字符串 - :010F0000002004A3810F7326
                        //写操作记录：(COM1)收到字符串 - :010F00000020D0

                        //成功执行读命令，结果值：1930396067
                        //(COM1)发送字符串 - :010100000020DE
                        //(COM1)收到字符串 - :010104A3810F7354

                        #endregion

                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.ReadCoil.ToString("X2"))//"01"
                        {
                            //这里是读取的字节结构：
                            //:010108 FFE36EC4 51F6540B 3C\r\n

                            //这里是在计算器里面转换的HEX字节结构：
                            //                 0B54F651
                            string sTemp = Strings.Mid(sFeedBackFromSlave, 8, 8);//‭10BC1B88‬  //‭‬

                            //读取的字节结构：2078B4EF
                            //转换的HEX字节结构：EFB47820

                            //读取的字节结构：FFE36EC4
                            //转换的HEX字节结构：C46EE3FF
                            string sResult = "";
                            for (int j = sTemp.Length / 2; j > 0; j--)
                            {
                                sResult += Strings.Mid(sTemp, j * 2 - 1, 2);
                            }

                            int iValue = HexStringToInt(sResult);

                            Value = iValue;

                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);

                            sFeedBackFromSlave = null;
                            //throw new Exception("");
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok
        /// <summary>
        /// 读取从站多个双字的线圈状态，函数是以字为读取数据长度单位
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字</param>
        /// <param name="Value">返回线圈多个双字的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilWord(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref int[] Value)//  , out string ReadBackData)
        {
            try
            {
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

                //起始位	    设备地址	功能代码	起始地址   	读取数据长度   LRC校验	   结束符
                //1个字符(:)	2个字符	    2个字符	    4个字符     4个字符	       2个字符	   2个字符(回车+换行)
                //1个字节       1个字节     1个字节     2个字节     2个字节        1个字节     2个字节
                byte[] byResult = MakeReadCmd(DeviceAddress, CModbusFunctionCode.ReadCoil, BeginAddress, Convert.ToUInt16(ReadDataLength * 4 * 8));//1个字 = 2 个字节 = 16位

                string sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)
                {
                    Enqueue( "发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"

                //发送值[0~1]：1099526371 1920016278
                //写操作记录：(COM1)发送字符串 - :010F0000004008E3708941961F7172F3
                //写操作记录：(COM1)收到字符串 - :010F00000040B0

                //成功执行读命令，结果值：1099526371 1920016278 
                //(COM1)发送字符串 - :010100000040BE
                //(COM1)收到字符串 - :010108E3708941961F717241

                //发送值[0~1]：1254912609 98040679
                //写操作记录：(COM1)发送字符串 - :010F00000040086172CC4A67FBD70581
                //写操作记录：(COM1)收到字符串 - :010F00000040B0

                //成功执行读命令，结果值：1254912609 98040679 
                //(COM1)发送字符串 - :010100000040BE
                //(COM1)收到字符串 - :0101086172CC4A67FBD705CF

                //成功执行读命令，结果值：1254912609 98040679 
                //(COM1)发送字符串 - :010100000040BE
                //(COM1)收到字符串 - :0101086172CC4A67FBD705CF

                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"
                    
                    if (null == byReadData || byReadData.Length < 2)
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"
                        
                        //**********************
                        //首字符
                        //[0] - :

                        //从站地址
                        //[1] - 0
                        //[2] - 1

                        //读取成功的功能码
                        //[3] - 0
                        //[4] - 1

                        //读取失败的错误码
                        //[3] - 0
                        //[4] - 8
                        //**********************
                        
                        #endregion

                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 1

                        //string sTempResult = BytesToHexStringSplitByChar(byReadData);

                        string sCmd = CModbusFunctionCode.ReadCoil.ToString("X2");

                        //if (cByte3 == 0x0.ToString()[0] && cByte4 == CModbusFunctionCode.ReadCoil.ToString()[0])//:0101
                         if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])
                        {
                            int[] iResult = new int[ReadDataLength];

                            for (int i = 0; i < ReadDataLength; i++)
                            {
                                //这里是读取的字节结构：
                                //:010108 FFE36EC4 51F6540B 3C\r\n

                                //这里是在计算器里面转换的HEX字节结构：
                                //                 0B54F651
                                string sTemp = Strings.Mid(sFeedBackFromSlave, 8 + i * 8, 8);//‭10BC1B88‬  //‭‬

                                //读取的字节结构：2078B4EF
                                //转换的HEX字节结构：EFB47820

                                //读取的字节结构：FFE36EC4
                                //转换的HEX字节结构：C46EE3FF
                                string sResult = "";
                                for (int j = sTemp.Length / 2; j > 0 ; j--)
                                {
                                    sResult += Strings.Mid(sTemp,  j * 2 - 1, 2);
                                }

                                iResult[i] = HexStringToInt(sResult);
                            }

                            Value = iResult;
                            iResult = null;

                            byReadData = null;
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);

                            byReadData = null;
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)//null == byReadData || byReadData.Length < 2
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.ReadCoil.ToString("X2"))//"01"
                        {
                            int[] iResult = new int[ReadDataLength];

                            for (int i = 0; i < ReadDataLength; i++)
                            {
                                //这里是读取的字节结构：
                                //:010108 FFE36EC4 51F6540B 3C\r\n

                                //这里是在计算器里面转换的HEX字节结构：
                                //                 0B54F651
                                string sTemp = Strings.Mid(sFeedBackFromSlave, 8 + i * 8, 8);//‭10BC1B88‬  //‭‬

                                //读取的字节结构：2078B4EF
                                //转换的HEX字节结构：EFB47820

                                //读取的字节结构：FFE36EC4
                                //转换的HEX字节结构：C46EE3FF
                                string sResult = "";
                                for (int j = sTemp.Length / 2; j > 0; j--)
                                {
                                    sResult += Strings.Mid(sTemp, j * 2 - 1, 2);
                                }

                                iResult[i] = HexStringToInt(sResult);
                            }

                            Value = iResult;
                            iResult = null;
                            
                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);

                            sFeedBackFromSlave = null;

                            //throw new Exception("");
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok
        /// <summary>
        /// 读取从站2个双字的线圈状态(位 - bit)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回线圈的2个双字当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilWord(byte DeviceAddress, ushort BeginAddress, ref long Value)//  , out string ReadBackData)
        {
            try
            {
                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //起始位	    设备地址	功能代码	起始地址   	读取数据长度   LRC校验	   结束符
                //1个字符(:)	2个字符	    2个字符	    4个字符     4个字符	       2个字符	   2个字符(回车+换行)
                //1个字节       1个字节     1个字节     2个字节     2个字节        1个字节     2个字节
                byte[] byResult = MakeReadCmd(DeviceAddress, CModbusFunctionCode.ReadCoil, BeginAddress, Convert.ToUInt16(8 * 8));//1个字 = 2 个字节 = 16位

                string sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)
                {
                    Enqueue( "发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"

                //**********************
                //首字符
                //[0] - :

                //从站地址
                //[1] - 0
                //[2] - 1

                //读取成功的功能码
                //[3] - 0
                //[4] - 1

                //读取失败的错误码
                //[3] - 0
                //[4] - 8
                //**********************

                //成功执行写命令-790511038
                //写操作记录：(COM1)发送字符串 - :010F000000400842C2E1D0FFFFFFFFF7
                //写操作记录：(COM1)收到字符串 - :010F00000040B0

                //成功执行读命令，结果值：-790511038
                //(COM1)发送字符串 - :010100000040BE
                //(COM1)收到字符串 - :01010842C2E1D0FFFFFFFF45

                //成功执行读命令，结果值：-790511038
                //(COM1)发送字符串 - :010100000040BE
                //(COM1)收到字符串 - :01010842C2E1D0FFFFFFFF45

                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"
                    
                    if (null == byReadData || byReadData.Length < 2)
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 1

                        //string sTempResult = BytesToHexStringSplitByChar(byReadData);

                        string sCmd = CModbusFunctionCode.ReadCoil.ToString("X2");

                        //if (cByte3 == 0x0.ToString()[0] && cByte4 == CModbusFunctionCode.ReadCoil.ToString()[0])//:0101
                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])
                        {
                            //这里是读取的字节结构：
                            //:0101087BCF1680FFFFFFFF1A\r\n
                            string sTemp = Strings.Mid(sFeedBackFromSlave, 8, 16);

                            //读取的字节结构：7BCF1680FFFFFFFF
                            //转换的HEX字节结构：FFFFFFFF8016CF7B
                            string sResult = "";
                            for (int j = sTemp.Length / 2; j > 0; j--)
                            {
                                sResult += Strings.Mid(sTemp, j * 2 - 1, 2);
                            }

                            long lValue = HexStringToLong(sResult);

                            Value = lValue;
                            
                            byReadData = null;
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);

                            byReadData = null;
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)//null == byReadData || byReadData.Length < 2
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"



                        #endregion

                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.ReadCoil.ToString("X2"))//"01"
                        {
                            //这里是读取的字节结构：
                            //:0101087BCF1680FFFFFFFF1A\r\n
                            string sTemp = Strings.Mid(sFeedBackFromSlave, 8, 16);

                            //读取的字节结构：7BCF1680FFFFFFFF
                            //转换的HEX字节结构：FFFFFFFF8016CF7B
                            string sResult = "";
                            for (int j = sTemp.Length / 2; j > 0; j--)
                            {
                                sResult += Strings.Mid(sTemp, j * 2 - 1, 2);
                            }

                            long lValue = HexStringToLong(sResult);

                            Value = lValue;

                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);

                            sFeedBackFromSlave = null;
                            //throw new Exception("");
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok
        /// <summary>
        /// 读取从站N个2个双字的线圈状态，函数是以字为读取数据长度单位
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字</param>
        /// <param name="Value">返回线圈N个2个双字的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadCoilWord(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref long[] Value)//  , out string ReadBackData)
        {
            try
            {
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

                //起始位	    设备地址	功能代码	起始地址   	读取数据长度   LRC校验	   结束符
                //1个字符(:)	2个字符	    2个字符	    4个字符     4个字符	       2个字符	   2个字符(回车+换行)
                //1个字节       1个字节     1个字节     2个字节     2个字节        1个字节     2个字节
                byte[] byResult = MakeReadCmd(DeviceAddress, CModbusFunctionCode.ReadCoil, BeginAddress, Convert.ToUInt16(ReadDataLength * 8 * 8));//1个字 = 2 个字节 = 16位

                string sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)
                {
                    Enqueue( "发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"

                //**********************
                //首字符
                //[0] - :

                //从站地址
                //[1] - 0
                //[2] - 1

                //读取成功的功能码
                //[3] - 0
                //[4] - 1

                //读取失败的错误码
                //[3] - 0
                //[4] - 8
                //**********************

                //发送值[0~1]：1325802384 1140179960 
                //成功执行写命令
                //写操作记录：(COM1)发送字符串 - :010F00000080109023064F00000000F8C3F5430000000065
                //写操作记录：(COM1)收到字符串 - :010F0000008070

                //成功执行读命令，结果值：1325802384 1140179960 
                //(COM1)发送字符串 - :0101000000807E
                //(COM1)收到字符串 - :0101109023064F00000000F8C3F54300000000F3

                //发送值[0~1]：-1278254254 1513776700
                //写操作记录：(COM1)发送字符串 - :010F00000080105263CFB3FFFFFFFF3C663A5A00000000F7
                //写操作记录：(COM1)收到字符串 - :010F0000008070

                //成功执行读命令，结果值：-1278254254 1513776700 
                //(COM1)发送字符串 - :0101000000807E
                //(COM1)收到字符串 - :0101105263CFB3FFFFFFFF3C663A5A0000000085

                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"
                    
                    if (null == byReadData || byReadData.Length < 2)
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 1

                        //string sTempResult = BytesToHexStringSplitByChar(byReadData);

                        string sCmd = CModbusFunctionCode.ReadCoil.ToString("X2");

                        //if (cByte3 == 0x0.ToString()[0] && cByte4 == CModbusFunctionCode.ReadCoil.ToString()[0])//:0101
                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])
                        {
                            long[] lResultData = new long[ReadDataLength];

                            for (int i = 0; i < ReadDataLength; i++)
                            {
                                //这里是读取的字节结构：
                                //:0101087BCF1680FFFFFFFF1A\r\n
                                string sTemp = Strings.Mid(sFeedBackFromSlave, 8 + i * 16, 16);

                                //读取的字节结构：7BCF1680FFFFFFFF
                                //转换的HEX字节结构：FFFFFFFF8016CF7B
                                string sResult = "";
                                for (int j = sTemp.Length / 2; j > 0; j--)
                                {
                                    sResult += Strings.Mid(sTemp, j * 2 - 1, 2);
                                }

                                long lValue = HexStringToLong(sResult);
                                lResultData[i] = lValue;
                            }

                            Value = lResultData;
                            lResultData = null;

                            byReadData = null;
                            sFeedBackFromSlave = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);

                            byReadData = null;
                            sFeedBackFromSlave = null;
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)//null == byReadData || byReadData.Length < 2
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"



                        #endregion

                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.ReadCoil.ToString("X2"))//"01"
                        {
                            long[] lResultData = new long[ReadDataLength];

                            for (int i = 0; i < ReadDataLength; i++)
                            {
                                //这里是读取的字节结构：
                                //:0101087BCF1680FFFFFFFF1A\r\n
                                string sTemp = Strings.Mid(sFeedBackFromSlave, 8 + i * 16, 16);

                                //读取的字节结构：7BCF1680FFFFFFFF
                                //转换的HEX字节结构：FFFFFFFF8016CF7B
                                string sResult = "";
                                for (int j = sTemp.Length / 2; j > 0; j--)
                                {
                                    sResult += Strings.Mid(sTemp, j * 2 - 1, 2);
                                }

                                long lValue = HexStringToLong(sResult);
                                lResultData[i] = lValue;
                            }

                            Value = lResultData;
                            lResultData = null;
                            
                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);

                            sFeedBackFromSlave = null;

                            //throw new Exception("");
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        #endregion

        #region "读单个/多个输入位的状态 - ok"

        // ok
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
                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                //起始位	    设备地址	功能代码	起始地址   	读取数据长度   LRC校验	   结束符
                //1个字符(:)	2个字符	    2个字符	    4个字符     4个字符	       2个字符	   2个字符(回车+换行)
                //1个字节       1个字节     1个字节     2个字节     2个字节        1个字节     2个字节

                byte[] byResult = MakeReadCmd(DeviceAddress, CModbusFunctionCode.ReadInputSignal, BeginAddress, 1);//ReadDataLength

                string sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)
                {
                    Enqueue( "发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();
                
                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"

                //成功执行读命令，结果值：True
                //(COM1)发送字符串 - :010200000001FC
                //(COM1)收到字符串 - :01020101FB

                //成功执行读命令，结果值：False
                //(COM1)发送字符串 - :010200000001FC
                //(COM1)收到字符串 - :01020100FC

                //成功执行读命令，结果值：False
                //(COM1)发送字符串 - :010200000001FC
                //(COM1)收到字符串 - :01020100FC

                //成功执行读命令，结果值：True
                //(COM1)发送字符串 - :010200000001FC
                //(COM1)收到字符串 - :01020101FB

                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"
                    
                    if (null == byReadData || byReadData.Length < 2)
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //**********************
                        //首字符
                        //[0] - :

                        //从站地址
                        //[1] - 0
                        //[2] - 1

                        //读取成功的功能码
                        //[3] - 0
                        //[4] - 2

                        //读取失败的错误码
                        //[3] - 0
                        //[4] - 8
                        //**********************

                        //***********Inputsignal
                        //:01020101FB\r\n
                        //成功执行读命令，结果值：True
                        //(COM1)发送字符串 - :010200000001FC
                        //(COM1)收到字符串 - :01020101FB

                        //:01020100FC\r\n
                        //成功执行读命令，结果值：False
                        //(COM1)发送字符串 - :010200000001FC
                        //(COM1)收到字符串 - :01020100FC

                        #endregion

                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 2

                        string sCmd = CModbusFunctionCode.ReadInputSignal.ToString("X2");

                        //if (cByte3 == 0x0.ToString()[0] && cByte4 == CModbusFunctionCode.ReadInputSignal.ToString()[0])//:0102
                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])
                        {
                            //读取成功
                            if (Strings.Mid(sFeedBackFromSlave, 9, 1) == "0")
                            {
                                //读取的值是0
                                Value = false;
                            }
                            else
                            {
                                //读取的值是1
                                Value = true;
                            }

                            sFeedBackFromSlave = null;
                            byReadData = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);

                            sFeedBackFromSlave = null;
                            byReadData = null;
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //**********************
                        //首字符
                        //[0] - :

                        //从站地址
                        //[1] - 0
                        //[2] - 1

                        //读取成功的功能码
                        //[3] - 0
                        //[4] - 2

                        //读取失败的错误码
                        //[3] - 0
                        //[4] - 8
                        //**********************

                        //:01020100FC\r\n
                        //成功执行读命令，结果值：False
                        //(COM1)发送字符串 - :010200000001FC
                        //(COM1)收到字符串 - :01020100FC

                        //:01020101FB\r\n
                        //成功执行读命令，结果值：True
                        //(COM1)发送字符串 - :010200000001FC
                        //(COM1)收到字符串 - :01020101FB

                        #endregion

                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.ReadInputSignal.ToString("X2"))//"02"
                        {
                            //读取成功
                            if (Strings.Mid(sFeedBackFromSlave, 9, 1) == "0")
                            {
                                //读取的值是0
                                Value = false;
                            }
                            else
                            {
                                //读取的值是1
                                Value = true;
                            }

                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            //读取错误
                            //throw new Exception("");
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok
        /// <summary>
        /// 读取从站多个字节的输入状态(1个字节 = 8位 - bit)，函数是以字节为读取数据长度单位
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

                //起始位	    设备地址	功能代码	起始地址   	读取数据长度   LRC校验	   结束符
                //1个字符(:)	2个字符	    2个字符	    4个字符     4个字符	       2个字符	   2个字符(回车+换行)
                //1个字节       1个字节     1个字节     2个字节     2个字节        1个字节     2个字节
                byte[] byResult = MakeReadCmd(DeviceAddress, CModbusFunctionCode.ReadInputSignal, BeginAddress, Convert.ToUInt16(ReadDataLength * 8));//

                string sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)
                {
                    Enqueue( "发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"

                //成功执行读命令，结果值：True False False False False False False False False False False False False False False False 
                //(COM1)发送字符串 - :010200000010ED
                //(COM1)收到字符串 - :0102020100FA

                //成功执行读命令，结果值：True True True True False False False False False False False False False False False False 
                //(COM1)发送字符串 - :010200000010ED
                //(COM1)收到字符串 - :0102020F00EC

                //成功执行读命令，结果值：True True True True False False True True False False True True False False True True 
                //(COM1)发送字符串 - :010200000010ED
                //(COM1)收到字符串 - :010202CFCC60

                //成功执行读命令，结果值：True True True True False False True True False False True True False False True True 
                //(COM1)发送字符串 - :010200000010ED
                //(COM1)收到字符串 - :010202CFCC60

                //成功执行读命令，结果值：True True True True False False True True False False True True False False True True 
                //(COM1)发送字符串 - :010200000010ED
                //(COM1)收到字符串 - :010202CFCC60
                
                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    //sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //**********************
                        //首字符
                        //[0] - :

                        //从站地址
                        //[1] - 0
                        //[2] - 1

                        //读取成功的功能码
                        //[3] - 0
                        //[4] - 1

                        //读取失败的错误码
                        //[3] - 0
                        //[4] - 8
                        //**********************

                        //:0184017A\r\n

                        #endregion

                        //string sTempResult = BytesToHexStringSplitByChar(byReadData);

                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 2

                        string sCmd = CModbusFunctionCode.ReadInputSignal.ToString("X2");

                        //if (cByte3 == 0x0.ToString()[0] && cByte4 == CModbusFunctionCode.ReadInputRegister.ToString()[0])//:0102
                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])
                        {
                            byte[] byReadResultData = new byte[ReadDataLength];
                            //bool[] bReadResultData = new bool[ReadDataLength * 8];
                            Value = new bool[ReadDataLength * 8];

                            for (int i = 0; i < ReadDataLength; i++)
                            {
                                string sTemp = Strings.Mid(sFeedBackFromSlave, 8 + i * 2, 2);
                                byReadResultData[i] = TwoHexCharsToByte(sTemp);

                                bool[] bTemp = ByteToBitArray(byReadResultData[i]);
                                for (int j = 0; j < bTemp.Length; j++)
                                {
                                    Value[i * 8 + j] = bTemp[j];//bReadResultData
                                }
                            }

                            //BitValue = bReadResultData;

                            //bReadResultData = null;
                            byReadResultData = null;
                            
                            //BitValue = bReadResultData;

                            //bReadResultData = null;

                            byReadData = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);
                            byReadData = null;
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)//null == byReadData || byReadData.Length < 2
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.ReadInputSignal.ToString("X2"))//"02"
                        {
                            byte[] byReadResultData = new byte[ReadDataLength];
                            bool[] bReadResultData = new bool[ReadDataLength * 8];

                            for (int i = 0; i < ReadDataLength; i++)
                            {
                                string sTemp = Strings.Mid(sFeedBackFromSlave, 8 + i * 2, 2);
                                byReadResultData[i] = TwoHexCharsToByte(sTemp);

                                bool[] bTemp = ByteToBitArray(byReadResultData[i]);
                                for (int j = 0; j < bTemp.Length; j++)
                                {
                                    bReadResultData[i * 8 + j] = bTemp[j];
                                }
                            }

                            Value = bReadResultData;

                            bReadResultData = null;
                            byReadResultData = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);

                            //throw new Exception("");
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok
        /// <summary>
        /// 读取从站1个字节的输入状态(1个字节 = 8位 - bit)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回输入状态1个字节的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputByte(byte DeviceAddress, ushort BeginAddress, ref byte Value)//  , out string ReadBackData)
        {
            try
            {
                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 1 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //起始位	    设备地址	功能代码	起始地址   	读取数据长度   LRC校验	   结束符
                //1个字符(:)	2个字符	    2个字符	    4个字符     4个字符	       2个字符	   2个字符(回车+换行)
                //1个字节       1个字节     1个字节     2个字节     2个字节        1个字节     2个字节
                byte[] byResult = MakeReadCmd(DeviceAddress, CModbusFunctionCode.ReadInputSignal, BeginAddress, Convert.ToUInt16(1 * 8));//

                string sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)
                {
                    Enqueue( "发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"

                //成功执行读命令，结果值：True False False False False False False False False False False False False False False False 
                //(COM1)发送字符串 - :010200000010ED
                //(COM1)收到字符串 - :0102020100FA

                //成功执行读命令，结果值：True True True True False False False False False False False False False False False False 
                //(COM1)发送字符串 - :010200000010ED
                //(COM1)收到字符串 - :0102020F00EC

                //成功执行读命令，结果值：True True True True False False True True False False True True False False True True 
                //(COM1)发送字符串 - :010200000010ED
                //(COM1)收到字符串 - :010202CFCC60

                //成功执行读命令，结果值：True True True True False False True True False False True True False False True True 
                //(COM1)发送字符串 - :010200000010ED
                //(COM1)收到字符串 - :010202CFCC60

                //成功执行读命令，结果值：True True True True False False True True False False True True False False True True 
                //(COM1)发送字符串 - :010200000010ED
                //(COM1)收到字符串 - :010202CFCC60
                
                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    //sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //**********************
                        //首字符
                        //[0] - :

                        //从站地址
                        //[1] - 0
                        //[2] - 1

                        //读取成功的功能码
                        //[3] - 0
                        //[4] - 1

                        //读取失败的错误码
                        //[3] - 0
                        //[4] - 8
                        //**********************

                        //:0184017A\r\n

                        #endregion

                        //string sTempResult = BytesToHexStringSplitByChar(byReadData);

                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 2

                        string sCmd = CModbusFunctionCode.ReadInputSignal.ToString("X2");

                        //if (cByte3 == 0x0.ToString()[0] && cByte4 == CModbusFunctionCode.ReadInputRegister.ToString()[0])//:0102
                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])
                        {
                            string sTemp = Strings.Mid(sFeedBackFromSlave, 8, 2);
                            Value = TwoHexCharsToByte(sTemp);

                            byReadData = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);
                            byReadData = null;
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)//null == byReadData || byReadData.Length < 2
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.ReadInputSignal.ToString("X2"))//"02"
                        {
                            string sTemp = Strings.Mid(sFeedBackFromSlave, 8, 2);
                            Value = TwoHexCharsToByte(sTemp);

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);

                            //throw new Exception("");
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok
        /// <summary>
        /// 读取从站多个字节的输入状态(1个字节 = 8位 - bit)
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

                //起始位	    设备地址	功能代码	起始地址   	读取数据长度   LRC校验	   结束符
                //1个字符(:)	2个字符	    2个字符	    4个字符     4个字符	       2个字符	   2个字符(回车+换行)
                //1个字节       1个字节     1个字节     2个字节     2个字节        1个字节     2个字节
                byte[] byResult = MakeReadCmd(DeviceAddress, CModbusFunctionCode.ReadInputSignal, BeginAddress, Convert.ToUInt16(ReadDataLength * 8));//

                string sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)
                {
                    Enqueue( "发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"

                //成功执行读命令，结果值：True False False False False False False False False False False False False False False False 
                //(COM1)发送字符串 - :010200000010ED
                //(COM1)收到字符串 - :0102020100FA

                //成功执行读命令，结果值：True True True True False False False False False False False False False False False False 
                //(COM1)发送字符串 - :010200000010ED
                //(COM1)收到字符串 - :0102020F00EC

                //成功执行读命令，结果值：True True True True False False True True False False True True False False True True 
                //(COM1)发送字符串 - :010200000010ED
                //(COM1)收到字符串 - :010202CFCC60

                //成功执行读命令，结果值：True True True True False False True True False False True True False False True True 
                //(COM1)发送字符串 - :010200000010ED
                //(COM1)收到字符串 - :010202CFCC60

                //成功执行读命令，结果值：True True True True False False True True False False True True False False True True 
                //(COM1)发送字符串 - :010200000010ED
                //(COM1)收到字符串 - :010202CFCC60
                
                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    //sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //**********************
                        //首字符
                        //[0] - :

                        //从站地址
                        //[1] - 0
                        //[2] - 1

                        //读取成功的功能码
                        //[3] - 0
                        //[4] - 1

                        //读取失败的错误码
                        //[3] - 0
                        //[4] - 8
                        //**********************

                        //:0184017A\r\n

                        #endregion

                        //string sTempResult = BytesToHexStringSplitByChar(byReadData);

                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 2

                        string sCmd = CModbusFunctionCode.ReadInputSignal.ToString("X2");

                        //if (cByte3 == 0x0.ToString()[0] && cByte4 == CModbusFunctionCode.ReadInputRegister.ToString()[0])//:0102
                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])
                        {
                            Value = new byte[ReadDataLength];

                            for (int i = 0; i < ReadDataLength; i++)
                            {
                                string sTemp = Strings.Mid(sFeedBackFromSlave, 8 + i * 2, 2);
                                Value[i] = TwoHexCharsToByte(sTemp);
                            }
                            
                            byReadData = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);
                            byReadData = null;
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)//null == byReadData || byReadData.Length < 2
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.ReadInputSignal.ToString("X2"))//"02"
                        {
                            Value = new byte[ReadDataLength];

                            for (int i = 0; i < ReadDataLength; i++)
                            {
                                string sTemp = Strings.Mid(sFeedBackFromSlave, 8 + i * 2, 2);
                                Value[i] = TwoHexCharsToByte(sTemp);
                            }
                            
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);

                            //throw new Exception("");
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok
        /// <summary>
        /// 读取从站1个字的输入状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回输入状态1个字的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputWord(byte DeviceAddress, ushort BeginAddress, ref short Value)//  , out string ReadBackData)
        {
            try
            {
                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //起始位	    设备地址	功能代码	起始地址   	读取数据长度   LRC校验	   结束符
                //1个字符(:)	2个字符	    2个字符	    4个字符     4个字符	       2个字符	   2个字符(回车+换行)
                //1个字节       1个字节     1个字节     2个字节     2个字节        1个字节     2个字节
                byte[] byResult = MakeReadCmd(DeviceAddress, CModbusFunctionCode.ReadInputSignal, BeginAddress, Convert.ToUInt16(2 * 8));//

                string sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)
                {
                    Enqueue( "发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"

                //在写操作里面

                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    //sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //**********************
                        //首字符
                        //[0] - :

                        //从站地址
                        //[1] - 0
                        //[2] - 1

                        //读取成功的功能码
                        //[3] - 0
                        //[4] - 1

                        //读取失败的错误码
                        //[3] - 0
                        //[4] - 8
                        //**********************

                        //:0184017A\r\n

                        #endregion

                        //string sTempResult = BytesToHexStringSplitByChar(byReadData);

                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 2

                        string sCmd = CModbusFunctionCode.ReadInputSignal.ToString("X2");

                        //if (cByte3 == 0x0.ToString()[0] && cByte4 == CModbusFunctionCode.ReadInputRegister.ToString()[0])//:0102
                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])
                        {
                            string sTemp = Strings.Mid(sFeedBackFromSlave, 8, 4);

                            sTemp = Strings.Mid(sTemp, 3, 2) + Strings.Mid(sTemp, 1, 2);//高低字节互换：D67C --> 7CD6

                            int iValue = HexStringToInt(sTemp);
                            byte[] bytesOfInt = BitConverter.GetBytes(iValue);
                            Value = BitConverter.ToInt16(bytesOfInt, 0);

                            bytesOfInt = null;

                            byReadData = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);
                            byReadData = null;
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)//null == byReadData || byReadData.Length < 2
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.ReadInputSignal.ToString("X2"))//"02"
                        {
                            string sTemp = Strings.Mid(sFeedBackFromSlave, 8, 4);

                            sTemp = Strings.Mid(sTemp, 3, 2) + Strings.Mid(sTemp, 1, 2);//高低字节互换：D67C --> 7CD6

                            int iValue = HexStringToInt(sTemp);
                            byte[] bytesOfInt = BitConverter.GetBytes(iValue);
                            Value = BitConverter.ToInt16(bytesOfInt, 0);

                            bytesOfInt = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);

                            //throw new Exception("");
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok
        /// <summary>
        /// 读取从站多个字的输入状态
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

                //起始位	    设备地址	功能代码	起始地址   	读取数据长度   LRC校验	   结束符
                //1个字符(:)	2个字符	    2个字符	    4个字符     4个字符	       2个字符	   2个字符(回车+换行)
                //1个字节       1个字节     1个字节     2个字节     2个字节        1个字节     2个字节
                byte[] byResult = MakeReadCmd(DeviceAddress, CModbusFunctionCode.ReadInputSignal, BeginAddress, Convert.ToUInt16(ReadDataLength * 2 * 8));//

                string sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)
                {
                    Enqueue( "发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"

                //在写操作里面
                
                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    //sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //**********************
                        //首字符
                        //[0] - :

                        //从站地址
                        //[1] - 0
                        //[2] - 1

                        //读取成功的功能码
                        //[3] - 0
                        //[4] - 1

                        //读取失败的错误码
                        //[3] - 0
                        //[4] - 8
                        //**********************

                        //:0184017A\r\n

                        #endregion

                        //string sTempResult = BytesToHexStringSplitByChar(byReadData);

                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 2

                        string sCmd = CModbusFunctionCode.ReadInputSignal.ToString("X2");

                        //if (cByte3 == 0x0.ToString()[0] && cByte4 == CModbusFunctionCode.ReadInputRegister.ToString()[0])//:0102
                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])
                        {
                            short[] stResultValue = new short[ReadDataLength];

                            for (int i = 0; i < ReadDataLength; i++)
                            {
                                string sTemp = Strings.Mid(sFeedBackFromSlave, 8 + i * 4, 4);

                                sTemp = Strings.Mid(sTemp, 3, 2) + Strings.Mid(sTemp, 1, 2);//高低字节互换：D67C --> 7CD6

                                int iValue = HexStringToInt(sTemp);
                                byte[] bytesOfInt = BitConverter.GetBytes(iValue);
                                stResultValue[i] = BitConverter.ToInt16(bytesOfInt, 0);

                                bytesOfInt = null;

                                bytesOfInt = null;
                            }

                            Value = stResultValue;
                            stResultValue = null;
                            
                            byReadData = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);
                            byReadData = null;
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)//null == byReadData || byReadData.Length < 2
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.ReadInputSignal.ToString("X2"))//"02"
                        {
                            short[] stResultValue = new short[ReadDataLength];

                            for (int i = 0; i < ReadDataLength; i++)
                            {
                                string sTemp = Strings.Mid(sFeedBackFromSlave, 8 + i * 4, 4);

                                sTemp = Strings.Mid(sTemp, 3, 2) + Strings.Mid(sTemp, 1, 2);//高低字节互换：D67C --> 7CD6

                                int iValue = HexStringToInt(sTemp);
                                byte[] bytesOfInt = BitConverter.GetBytes(iValue);
                                stResultValue[i] = BitConverter.ToInt16(bytesOfInt, 0);

                                bytesOfInt = null;
                            }

                            Value = stResultValue;
                            stResultValue = null;
                            
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);

                            //throw new Exception("");
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok
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
                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //起始位	    设备地址	功能代码	起始地址   	读取数据长度   LRC校验	   结束符
                //1个字符(:)	2个字符	    2个字符	    4个字符     4个字符	       2个字符	   2个字符(回车+换行)
                //1个字节       1个字节     1个字节     2个字节     2个字节        1个字节     2个字节
                byte[] byResult = MakeReadCmd(DeviceAddress, CModbusFunctionCode.ReadInputSignal, BeginAddress, Convert.ToUInt16(4 * 8));//

                string sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)
                {
                    Enqueue( "发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"

                //在写操作里面

                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    //sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //**********************
                        //首字符
                        //[0] - :

                        //从站地址
                        //[1] - 0
                        //[2] - 1

                        //读取成功的功能码
                        //[3] - 0
                        //[4] - 1

                        //读取失败的错误码
                        //[3] - 0
                        //[4] - 8
                        //**********************

                        //:0184017A\r\n

                        #endregion

                        //string sTempResult = BytesToHexStringSplitByChar(byReadData);

                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 2

                        string sCmd = CModbusFunctionCode.ReadInputSignal.ToString("X2");

                        //if (cByte3 == 0x0.ToString()[0] && cByte4 == CModbusFunctionCode.ReadInputRegister.ToString()[0])//:0102
                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])
                        {
                            //这里是读取的字节结构：
                            //:010108 FFE36EC4 51F6540B 3C\r\n

                            //这里是在计算器里面转换的HEX字节结构：
                            //                 0B54F651
                            string sTemp = Strings.Mid(sFeedBackFromSlave, 8, 8);//‭10BC1B88‬  //‭‬

                            //读取的字节结构：2078B4EF
                            //转换的HEX字节结构：EFB47820

                            //读取的字节结构：FFE36EC4
                            //转换的HEX字节结构：C46EE3FF
                            string sResult = "";
                            for (int j = sTemp.Length / 2; j > 0; j--)
                            {
                                sResult += Strings.Mid(sTemp, j * 2 - 1, 2);
                            }

                            int iValue = HexStringToInt(sResult);

                            Value = iValue;
                            
                            byReadData = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);
                            byReadData = null;
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)//null == byReadData || byReadData.Length < 2
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.ReadInputSignal.ToString("X2"))//"02"
                        {
                            //这里是读取的字节结构：
                            //:010108 FFE36EC4 51F6540B 3C\r\n

                            //这里是在计算器里面转换的HEX字节结构：
                            //                 0B54F651
                            string sTemp = Strings.Mid(sFeedBackFromSlave, 8, 8);//‭10BC1B88‬  //‭‬

                            //读取的字节结构：2078B4EF
                            //转换的HEX字节结构：EFB47820

                            //读取的字节结构：FFE36EC4
                            //转换的HEX字节结构：C46EE3FF
                            string sResult = "";
                            for (int j = sTemp.Length / 2; j > 0; j--)
                            {
                                sResult += Strings.Mid(sTemp, j * 2 - 1, 2);
                            }

                            int iValue = HexStringToInt(sResult);

                            Value = iValue;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);

                            //throw new Exception("");
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok
        /// <summary>
        /// 读取从站多个双字的输入状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字</param>
        /// <param name="Value">返回输入状态多个双字的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputWord(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref int[] Value)//  , out string ReadBackData)
        {
            try
            {
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

                //起始位	    设备地址	功能代码	起始地址   	读取数据长度   LRC校验	   结束符
                //1个字符(:)	2个字符	    2个字符	    4个字符     4个字符	       2个字符	   2个字符(回车+换行)
                //1个字节       1个字节     1个字节     2个字节     2个字节        1个字节     2个字节
                byte[] byResult = MakeReadCmd(DeviceAddress, CModbusFunctionCode.ReadInputSignal, BeginAddress, Convert.ToUInt16(ReadDataLength * 4 * 8));//

                string sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)
                {
                    Enqueue( "发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"

                //在写操作里面
                
                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    //sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //**********************
                        //首字符
                        //[0] - :

                        //从站地址
                        //[1] - 0
                        //[2] - 1

                        //读取成功的功能码
                        //[3] - 0
                        //[4] - 1

                        //读取失败的错误码
                        //[3] - 0
                        //[4] - 8
                        //**********************

                        //:0184017A\r\n

                        #endregion

                        //string sTempResult = BytesToHexStringSplitByChar(byReadData);

                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 2

                        string sCmd = CModbusFunctionCode.ReadInputSignal.ToString("X2");

                        //if (cByte3 == 0x0.ToString()[0] && cByte4 == CModbusFunctionCode.ReadInputRegister.ToString()[0])//:0102
                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])
                        {
                            int[] iResultValue = new int[ReadDataLength];

                            for (int i = 0; i < ReadDataLength; i++)
                            {
                                //这里是读取的字节结构：
                                //:010108 FFE36EC4 51F6540B 3C\r\n

                                //这里是在计算器里面转换的HEX字节结构：
                                //                 0B54F651
                                string sTemp = Strings.Mid(sFeedBackFromSlave, 8 + i * 8, 8);//‭10BC1B88‬  //‭‬

                                //读取的字节结构：2078B4EF
                                //转换的HEX字节结构：EFB47820

                                //读取的字节结构：FFE36EC4
                                //转换的HEX字节结构：C46EE3FF
                                string sResult = "";
                                for (int j = sTemp.Length / 2; j > 0; j--)
                                {
                                    sResult += Strings.Mid(sTemp, j * 2 - 1, 2);
                                }

                                iResultValue[i] = (int)HexStringToLong(sResult);
                            }

                            Value = iResultValue;
                            iResultValue = null;
                            
                            byReadData = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);
                            byReadData = null;
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)//null == byReadData || byReadData.Length < 2
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.ReadInputSignal.ToString("X2"))//"02"
                        {
                            int[] iResultValue = new int[ReadDataLength];

                            for (int i = 0; i < ReadDataLength; i++)
                            {
                                string sTemp = Strings.Mid(sFeedBackFromSlave, 8 + i * 8, 8);

                                //这里是读取的字节结构：
                                //:010108 FFE36EC4 51F6540B 3C\r\n

                                //这里是在计算器里面转换的HEX字节结构：
                                //                 0B54F651

                                //读取的字节结构：2078B4EF
                                //转换的HEX字节结构：EFB47820

                                //读取的字节结构：FFE36EC4
                                //转换的HEX字节结构：C46EE3FF
                                string sResult = "";
                                for (int j = sTemp.Length / 2; j > 0; j--)
                                {
                                    sResult += Strings.Mid(sTemp, j * 2 - 1, 2);
                                }

                                iResultValue[i] = (int)HexStringToLong(sResult);
                            }

                            Value = iResultValue;
                            iResultValue = null;
                            
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);

                            //throw new Exception("");
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok
        /// <summary>
        /// 读取从站2个双字的输入状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回输入状态2个双字的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputWord(byte DeviceAddress, ushort BeginAddress, ref long Value)//  , out string ReadBackData)
        {
            try
            {
                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //起始位	    设备地址	功能代码	起始地址   	读取数据长度   LRC校验	   结束符
                //1个字符(:)	2个字符	    2个字符	    4个字符     4个字符	       2个字符	   2个字符(回车+换行)
                //1个字节       1个字节     1个字节     2个字节     2个字节        1个字节     2个字节
                byte[] byResult = MakeReadCmd(DeviceAddress, CModbusFunctionCode.ReadInputSignal, BeginAddress, Convert.ToUInt16(8 * 8));//

                string sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)
                {
                    Enqueue( "发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"

                //在写操作里面

                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    //sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //**********************
                        //首字符
                        //[0] - :

                        //从站地址
                        //[1] - 0
                        //[2] - 1

                        //读取成功的功能码
                        //[3] - 0
                        //[4] - 1

                        //读取失败的错误码
                        //[3] - 0
                        //[4] - 8
                        //**********************

                        //:0184017A\r\n

                        #endregion

                        //string sTempResult = BytesToHexStringSplitByChar(byReadData);

                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 2

                        string sCmd = CModbusFunctionCode.ReadInputSignal.ToString("X2");

                        //if (cByte3 == 0x0.ToString()[0] && cByte4 == CModbusFunctionCode.ReadInputRegister.ToString()[0])//:0102
                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])
                        {
                            //这里是读取的字节结构：
                            //:0101087BCF1680FFFFFFFF1A\r\n
                            string sTemp = Strings.Mid(sFeedBackFromSlave, 8, 16);

                            //读取的字节结构：7BCF1680FFFFFFFF
                            //转换的HEX字节结构：FFFFFFFF8016CF7B
                            string sResult = "";
                            for (int j = sTemp.Length / 2; j > 0; j--)
                            {
                                sResult += Strings.Mid(sTemp, j * 2 - 1, 2);
                            }

                            long lValue = HexStringToLong(sResult);

                            Value = lValue;
                            
                            byReadData = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);
                            byReadData = null;
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)//null == byReadData || byReadData.Length < 2
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.ReadInputSignal.ToString("X2"))//"02"
                        {
                            //这里是读取的字节结构：
                            //:0101087BCF1680FFFFFFFF1A\r\n
                            string sTemp = Strings.Mid(sFeedBackFromSlave, 8, 16);

                            //读取的字节结构：7BCF1680FFFFFFFF
                            //转换的HEX字节结构：FFFFFFFF8016CF7B
                            string sResult = "";
                            for (int j = sTemp.Length / 2; j > 0; j--)
                            {
                                sResult += Strings.Mid(sTemp, j * 2 - 1, 2);
                            }

                            long lValue = HexStringToLong(sResult);

                            Value = lValue;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);

                            //throw new Exception("");
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok
        /// <summary>
        /// 读取从站多个2双字的输入状态
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度</param>
        /// <param name="Value">返回输入状态多个2双字的当前值</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputWord(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref long[] Value)//  , out string ReadBackData)
        {
            try
            {
                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                //if (ReadDataLength < 1 || ReadDataLength > 125)
                //{
                //    throw new Exception("读取数据长度的值不正确，有效值范围：1~125(字)");
                //}

                if (BeginAddress + ReadDataLength * 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //起始位	    设备地址	功能代码	起始地址   	读取数据长度   LRC校验	   结束符
                //1个字符(:)	2个字符	    2个字符	    4个字符     4个字符	       2个字符	   2个字符(回车+换行)
                //1个字节       1个字节     1个字节     2个字节     2个字节        1个字节     2个字节
                byte[] byResult = MakeReadCmd(DeviceAddress, CModbusFunctionCode.ReadInputSignal, BeginAddress, Convert.ToUInt16(ReadDataLength * 8 * 8));//

                string sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)
                {
                    Enqueue( "发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"

                //在写操作里面
                
                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    //sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //**********************
                        //首字符
                        //[0] - :

                        //从站地址
                        //[1] - 0
                        //[2] - 1

                        //读取成功的功能码
                        //[3] - 0
                        //[4] - 1

                        //读取失败的错误码
                        //[3] - 0
                        //[4] - 8
                        //**********************

                        //:0184017A\r\n

                        #endregion

                        //string sTempResult = BytesToHexStringSplitByChar(byReadData);

                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 2

                        string sCmd = CModbusFunctionCode.ReadInputSignal.ToString("X2");

                        //if (cByte3 == 0x0.ToString()[0] && cByte4 == CModbusFunctionCode.ReadInputRegister.ToString()[0])//:0102
                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])
                        {
                            long[] lResultValue = new long[ReadDataLength];

                            for (int i = 0; i < ReadDataLength; i++)
                            {
                                //这里是读取的字节结构：
                                //:0101087BCF1680FFFFFFFF1A\r\n
                                string sTemp = Strings.Mid(sFeedBackFromSlave, 8 + i * 16, 16);

                                //读取的字节结构：7BCF1680FFFFFFFF
                                //转换的HEX字节结构：FFFFFFFF8016CF7B
                                string sResult = "";
                                for (int j = sTemp.Length / 2; j > 0; j--)
                                {
                                    sResult += Strings.Mid(sTemp, j * 2 - 1, 2);
                                }

                                lResultValue[i] = HexStringToLong(sResult);
                            }

                            Value = lResultValue;
                            lResultValue = null;
                            
                            byReadData = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);
                            byReadData = null;
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)//null == byReadData || byReadData.Length < 2
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.ReadInputSignal.ToString("X2"))//"02"
                        {
                            long[] lResultValue = new long[ReadDataLength];

                            for (int i = 0; i < ReadDataLength; i++)
                            {
                                //这里是读取的字节结构：
                                //:0101087BCF1680FFFFFFFF1A\r\n
                                string sTemp = Strings.Mid(sFeedBackFromSlave, 8 + i * 16, 16);

                                //读取的字节结构：7BCF1680FFFFFFFF
                                //转换的HEX字节结构：FFFFFFFF8016CF7B
                                string sResult = "";
                                for (int j = sTemp.Length / 2; j > 0; j--)
                                {
                                    sResult += Strings.Mid(sTemp, j * 2 - 1, 2);
                                }

                                lResultValue[i] = HexStringToLong(sResult);
                            }

                            Value = lResultValue;
                            lResultValue = null;
                            
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);

                            //throw new Exception("");
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        #endregion

        #region "读单个/多个输入寄存器的状态 - ok"

        // ok
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
                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                //起始位	    设备地址	功能代码	起始地址   	读取数据长度   LRC校验	   结束符
                //1个字符(:)	2个字符	    2个字符	    4个字符     4个字符	       2个字符	   2个字符(回车+换行)
                //1个字节       1个字节     1个字节     2个字节     2个字节        1个字节     2个字节
                byte[] byResult = MakeReadCmd(DeviceAddress, CModbusFunctionCode.ReadInputRegister, BeginAddress, Convert.ToUInt16(1));

                string sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)
                {
                    Enqueue( "发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"



                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    //sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //**********************
                        //首字符
                        //[0] - :

                        //从站地址
                        //[1] - 0
                        //[2] - 1

                        //读取成功的功能码
                        //[3] - 0
                        //[4] - 1

                        //读取失败的错误码
                        //[3] - 0
                        //[4] - 8
                        //**********************



                        #endregion
                        
                        //string sTempResult = BytesToHexStringSplitByChar(byReadData);

                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 4

                        string sCmd = CModbusFunctionCode.ReadInputRegister.ToString("X2");

                        //if (cByte3 == 0x0.ToString()[0] && cByte4 == CModbusFunctionCode.ReadInputRegister.ToString()[0])//:0104
                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])
                        {
                            //每次截取4个16进制字符，转换为int型数据
                            string sTemp = Strings.Mid(sFeedBackFromSlave, 8, 4);

                            Value = (short)HexStringToInt(sTemp);
                            
                            byReadData = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);
                            byReadData = null;
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)//null == byReadData || byReadData.Length < 2
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.ReadInputRegister.ToString("X2"))//"04"
                        {
                            //每次截取4个16进制字符，转换为int型数据
                            string sTemp = Strings.Mid(sFeedBackFromSlave, 8, 4);

                            Value = (short)HexStringToInt(sTemp);

                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);

                            sFeedBackFromSlave = null;
                            //throw new Exception("");
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok
        /// <summary>
        /// 读取从站输入寄存器的当前值(short: -32,768 到 32,767)，函数是以字节为读取数据长度单位
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：字</param>
        /// <param name="Value">返回输入寄存器的当前值(short: -32,768 到 32,767)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputRegister(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref short[] Value)//  , out string ReadBackData)
        {
            try
            {
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

                //起始位	    设备地址	功能代码	起始地址   	读取数据长度   LRC校验	   结束符
                //1个字符(:)	2个字符	    2个字符	    4个字符     4个字符	       2个字符	   2个字符(回车+换行)
                //1个字节       1个字节     1个字节     2个字节     2个字节        1个字节     2个字节
                byte[] byResult = MakeReadCmd(DeviceAddress, CModbusFunctionCode.ReadInputRegister, BeginAddress, Convert.ToUInt16(ReadDataLength));

                string sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)
                {
                    Enqueue( "发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"



                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    //sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //**********************
                        //首字符
                        //[0] - :

                        //从站地址
                        //[1] - 0
                        //[2] - 1

                        //读取成功的功能码
                        //[3] - 0
                        //[4] - 1

                        //读取失败的错误码
                        //[3] - 0
                        //[4] - 8
                        //**********************
                        


                        #endregion

                        //string sTempResult = BytesToHexStringSplitByChar(byReadData);

                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 4

                        string sCmd = CModbusFunctionCode.ReadInputRegister.ToString("X2");

                        //if (cByte3 == 0x0.ToString()[0] && cByte4 == CModbusFunctionCode.ReadInputRegister.ToString()[0])//:0104
                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])
                        {
                            short[] stResultValue = new short[ReadDataLength];

                            for (int i = 0; i < ReadDataLength; i++)
                            {
                                //每次截取4个16进制字符，转换为int型数据
                                string sTemp = Strings.Mid(sFeedBackFromSlave, 8 + i * 4, 4);

                                stResultValue[i] = (short)HexStringToInt(sTemp);
                            }

                            Value = stResultValue;
                            stResultValue = null;
                            
                            sFeedBackFromSlave = null;
                            byReadData = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);

                            sFeedBackFromSlave = null;
                            byReadData = null;
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)//null == byReadData || byReadData.Length < 2
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.ReadInputRegister.ToString("X2"))//"04"
                        {
                            short[] iResultValue = new short[ReadDataLength];

                            for (int i = 0; i < ReadDataLength; i++)
                            {
                                //每次截取4个16进制字符，转换为int型数据
                                string sTemp = Strings.Mid(sFeedBackFromSlave, 8 + i * 4, 4);

                                iResultValue[i] = (short)HexStringToInt(sTemp);
                            }

                            Value = iResultValue;
                            iResultValue = null;

                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);

                            sFeedBackFromSlave = null;
                            //throw new Exception("");
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok
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
                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 2 * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //起始位	    设备地址	功能代码	起始地址   	读取数据长度   LRC校验	   结束符
                //1个字符(:)	2个字符	    2个字符	    4个字符     4个字符	       2个字符	   2个字符(回车+换行)
                //1个字节       1个字节     1个字节     2个字节     2个字节        1个字节     2个字节
                byte[] byResult = MakeReadCmd(DeviceAddress, CModbusFunctionCode.ReadInputRegister, BeginAddress, Convert.ToUInt16(2));

                string sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)
                {
                    Enqueue( "发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"

                //成功执行读命令，结果值：-32548
                //(COM1)发送字符串 - :010400000001FA
                //(COM1)收到字符串 - :01040280DC9D

                #endregion
                
                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    //sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //**********************
                        //首字符
                        //[0] - :

                        //从站地址
                        //[1] - 0
                        //[2] - 1

                        //读取成功的功能码
                        //[3] - 0
                        //[4] - 1

                        //读取失败的错误码
                        //[3] - 0
                        //[4] - 8
                        //**********************

                        //:0184017A\r\n



                        #endregion

                        //:010402009861\r\n

                        //string sTempResult = BytesToHexStringSplitByChar(byReadData);

                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 4

                        string sCmd = CModbusFunctionCode.ReadInputRegister.ToString("X2");

                        //if (cByte3 == 0x0.ToString()[0] && cByte4 == CModbusFunctionCode.ReadInputRegister.ToString()[0])//:0104
                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])
                        {
                            //:010404075AFFFF98\r\n
                            //每次截取8个16进制字符，转换为int型数据
                            string sTemp = Strings.Mid(sFeedBackFromSlave, 8, 8);
                            
                            //读取的字节结构：075AFFFF
                            //转换的HEX字节结构：‭FFFF075A‬

                            string sResult = Strings.Mid(sTemp, 5, 4) + Strings.Mid(sTemp, 1, 4);

                            Value = HexStringToInt(sResult);
                            
                            byReadData = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);
                            byReadData = null;
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)//null == byReadData || byReadData.Length < 2
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.ReadInputRegister.ToString("X2"))//"04"
                        {
                            //:010404075AFFFF98\r\n
                            //每次截取8个16进制字符，转换为int型数据
                            string sTemp = Strings.Mid(sFeedBackFromSlave, 8, 8);

                            //读取的字节结构：075AFFFF
                            //转换的HEX字节结构：‭FFFF075A‬

                            string sResult = Strings.Mid(sTemp, 5, 4) + Strings.Mid(sTemp, 1, 4);

                            Value = HexStringToInt(sResult);

                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);

                            sFeedBackFromSlave = null;
                            //throw new Exception("");
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok
        /// <summary>
        /// 读取从站输入寄存器的当前值(int)，函数是以字节为读取数据长度单位
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：双字</param>
        /// <param name="Value">返回输入寄存器的当前值(int)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputRegister(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref int[] Value)//  , out string ReadBackData)
        {
            try
            {
                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                //if (ReadDataLength < 1 || ReadDataLength > 125)//读取数据长度，有效值范围：1~250(字节)
                //{
                //    throw new Exception("读取数据长度的值不正确，有效值范围：1~250(字节)");
                //}

                if (BeginAddress + ReadDataLength * 2 * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //起始位	    设备地址	功能代码	起始地址   	读取数据长度   LRC校验	   结束符
                //1个字符(:)	2个字符	    2个字符	    4个字符     4个字符	       2个字符	   2个字符(回车+换行)
                //1个字节       1个字节     1个字节     2个字节     2个字节        1个字节     2个字节
                byte[] byResult = MakeReadCmd(DeviceAddress, CModbusFunctionCode.ReadInputRegister, BeginAddress, Convert.ToUInt16(ReadDataLength * 2));

                string sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)
                {
                    Enqueue( "发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"



                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    //sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //**********************
                        //首字符
                        //[0] - :

                        //从站地址
                        //[1] - 0
                        //[2] - 1

                        //读取成功的功能码
                        //[3] - 0
                        //[4] - 1

                        //读取失败的错误码
                        //[3] - 0
                        //[4] - 8
                        //**********************



                        #endregion

                        //string sTempResult = BytesToHexStringSplitByChar(byReadData);

                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 4

                        string sCmd = CModbusFunctionCode.ReadInputRegister.ToString("X2");

                        //if (cByte3 == 0x0.ToString()[0] && cByte4 == CModbusFunctionCode.ReadInputRegister.ToString()[0])//:0104
                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])
                        {
                            int[] iResultValue = new int[ReadDataLength];

                            for (int i = 0; i < ReadDataLength; i++)
                            {
                                //:010404075AFFFF98\r\n
                                //每次截取8个16进制字符，转换为int型数据
                                string sTemp = Strings.Mid(sFeedBackFromSlave, 8 + i * 8, 8);

                                //读取的字节结构：075AFFFF
                                //转换的HEX字节结构：‭FFFF075A‬

                                string sResult = Strings.Mid(sTemp, 5, 4) + Strings.Mid(sTemp, 1, 4);

                                iResultValue[i] = HexStringToInt(sResult);
                            }

                            Value = iResultValue;
                            iResultValue = null;
                            
                            sFeedBackFromSlave = null;
                            byReadData = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);

                            sFeedBackFromSlave = null;
                            byReadData = null;
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)//null == byReadData || byReadData.Length < 2
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.ReadInputRegister.ToString("X2"))//"04"
                        {
                            int[] iResultValue = new int[ReadDataLength];

                            for (int i = 0; i < ReadDataLength; i++)
                            {
                                //:010404075AFFFF98\r\n
                                //每次截取8个16进制字符，转换为int型数据
                                string sTemp = Strings.Mid(sFeedBackFromSlave, 8 + i * 8, 8);

                                //读取的字节结构：075AFFFF
                                //转换的HEX字节结构：‭FFFF075A‬

                                string sResult = Strings.Mid(sTemp, 5, 4) + Strings.Mid(sTemp, 1, 4);

                                iResultValue[i] = HexStringToInt(sResult);
                            }

                            Value = iResultValue;
                            iResultValue = null;

                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);

                            sFeedBackFromSlave = null;
                            //throw new Exception("");
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok
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
                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 2 * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //起始位	    设备地址	功能代码	起始地址   	读取数据长度   LRC校验	   结束符
                //1个字符(:)	2个字符	    2个字符	    4个字符     4个字符	       2个字符	   2个字符(回车+换行)
                //1个字节       1个字节     1个字节     2个字节     2个字节        1个字节     2个字节
                byte[] byResult = MakeReadCmd(DeviceAddress, CModbusFunctionCode.ReadInputRegister, BeginAddress, Convert.ToUInt16(2));

                string sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)
                {
                    Enqueue( "发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"



                #endregion
                
                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    //sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //**********************
                        //首字符
                        //[0] - :

                        //从站地址
                        //[1] - 0
                        //[2] - 1

                        //读取成功的功能码
                        //[3] - 0
                        //[4] - 1

                        //读取失败的错误码
                        //[3] - 0
                        //[4] - 8
                        //**********************
                        


                        #endregion

                        //string sTempResult = BytesToHexStringSplitByChar(byReadData);

                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 4

                        string sCmd = CModbusFunctionCode.ReadInputRegister.ToString("X2");

                        //if (cByte3 == 0x0.ToString()[0] && cByte4 == CModbusFunctionCode.ReadInputRegister.ToString()[0])//:0104
                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])
                        {
                            //每次截取8个16进制字符，转换为float型数据
                            string sTemp = Strings.Mid(sFeedBackFromSlave, 8, 8);
                            
                            byte[] byDataBytes = new byte[4];

                            //每次截取8个16进制字符，转换为float型数据
                            byDataBytes[0] = TwoHexCharsToByte(Strings.Mid(sTemp, 3, 2));
                            byDataBytes[1] = TwoHexCharsToByte(Strings.Mid(sTemp, 1, 2));
                            byDataBytes[2] = TwoHexCharsToByte(Strings.Mid(sTemp, 7, 2));
                            byDataBytes[3] = TwoHexCharsToByte(Strings.Mid(sTemp, 5, 2));

                            Value = BitConverter.ToSingle(byDataBytes, 0);
                            
                            byReadData = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);
                            byReadData = null;
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)//null == byReadData || byReadData.Length < 2
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.ReadInputRegister.ToString("X2"))//"04"
                        {
                            //每次截取8个16进制字符，转换为float型数据
                            string sTemp = Strings.Mid(sFeedBackFromSlave, 8, 8);
                            
                            byte[] byDataBytes = new byte[4];

                            //每次截取8个16进制字符，转换为float型数据
                            byDataBytes[0] = TwoHexCharsToByte(Strings.Mid(sTemp, 3, 2));
                            byDataBytes[1] = TwoHexCharsToByte(Strings.Mid(sTemp, 1, 2));
                            byDataBytes[2] = TwoHexCharsToByte(Strings.Mid(sTemp, 7, 2));
                            byDataBytes[3] = TwoHexCharsToByte(Strings.Mid(sTemp, 5, 2));

                            Value = BitConverter.ToSingle(byDataBytes, 0);

                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);

                            sFeedBackFromSlave = null;
                            //throw new Exception("");
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok
        /// <summary>
        /// 读取从站输入寄存器的当前值(float)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：双字</param>
        /// <param name="Value">返回输入寄存器的当前值(float)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadInputRegister(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref float[] Value)//  , out string ReadBackData)
        {
            try
            {
                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 2 * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //起始位	    设备地址	功能代码	起始地址   	读取数据长度   LRC校验	   结束符
                //1个字符(:)	2个字符	    2个字符	    4个字符     4个字符	       2个字符	   2个字符(回车+换行)
                //1个字节       1个字节     1个字节     2个字节     2个字节        1个字节     2个字节
                byte[] byResult = MakeReadCmd(DeviceAddress, CModbusFunctionCode.ReadInputRegister, BeginAddress, Convert.ToUInt16(ReadDataLength * 2));

                string sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)
                {
                    Enqueue( "发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"



                #endregion
                
                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    //sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //**********************
                        //首字符
                        //[0] - :

                        //从站地址
                        //[1] - 0
                        //[2] - 1

                        //读取成功的功能码
                        //[3] - 0
                        //[4] - 1

                        //读取失败的错误码
                        //[3] - 0
                        //[4] - 8
                        //**********************
                        


                        #endregion

                        //string sTempResult = BytesToHexStringSplitByChar(byReadData);

                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 4

                        string sCmd = CModbusFunctionCode.ReadInputRegister.ToString("X2");

                        //if (cByte3 == 0x0.ToString()[0] && cByte4 == CModbusFunctionCode.ReadInputRegister.ToString()[0])//:0104
                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])
                        {
                            float[] fResultData = new float[ReadDataLength];

                            for (int i = 0; i < ReadDataLength; i++)
                            {
                                //每次截取8个16进制字符，转换为float型数据
                                string sTemp = Strings.Mid(sFeedBackFromSlave, 8 + i * 8, 8);
                                
                                byte[] byDataBytes = new byte[4];

                                //每次截取8个16进制字符，转换为float型数据
                                byDataBytes[0] = TwoHexCharsToByte(Strings.Mid(sTemp, 3, 2));
                                byDataBytes[1] = TwoHexCharsToByte(Strings.Mid(sTemp, 1, 2));
                                byDataBytes[2] = TwoHexCharsToByte(Strings.Mid(sTemp, 7, 2));
                                byDataBytes[3] = TwoHexCharsToByte(Strings.Mid(sTemp, 5, 2));

                                fResultData[i] = BitConverter.ToSingle(byDataBytes, 0);
                            }

                            Value = fResultData;
                            
                            byReadData = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);
                            byReadData = null;
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)//null == byReadData || byReadData.Length < 2
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.ReadInputRegister.ToString("X2"))//"04"
                        {
                            float[] fResultData = new float[ReadDataLength];

                            for (int i = 0; i < ReadDataLength; i++)
                            {
                                //每次截取8个16进制字符，转换为float型数据
                                string sTemp = Strings.Mid(sFeedBackFromSlave, 8 + i * 8, 8);
                                
                                byte[] byDataBytes = new byte[4];

                                //每次截取8个16进制字符，转换为float型数据
                                byDataBytes[0] = TwoHexCharsToByte(Strings.Mid(sTemp, 3, 2));
                                byDataBytes[1] = TwoHexCharsToByte(Strings.Mid(sTemp, 1, 2));
                                byDataBytes[2] = TwoHexCharsToByte(Strings.Mid(sTemp, 7, 2));
                                byDataBytes[3] = TwoHexCharsToByte(Strings.Mid(sTemp, 5, 2));

                                fResultData[i] = BitConverter.ToSingle(byDataBytes, 0);
                            }

                            Value = fResultData;

                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);

                            sFeedBackFromSlave = null;
                            //throw new Exception("");
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ?? 测试读取数据时不能像其它功能码一样正确读取double值，在float范围内可以正确读取，其它会对应不上，待更多测试
        /// <summary>
        /// ?? 测试读取数据时不能像其它功能码一样正确读取double值，在float范围内可以正确读取，其它会对应不上，待更多测试
        /// 读取从站输入寄存器的当前值(double)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回输入寄存器的当前值(double)</param>
        /// <returns>是否成功执行命令</returns>
        private bool ReadInputRegister(byte DeviceAddress, ushort BeginAddress, ref double Value)//  , out string ReadBackData)
        {
            try
            {
                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //起始位	    设备地址	功能代码	起始地址   	读取数据长度   LRC校验	   结束符
                //1个字符(:)	2个字符	    2个字符	    4个字符     4个字符	       2个字符	   2个字符(回车+换行)
                //1个字节       1个字节     1个字节     2个字节     2个字节        1个字节     2个字节
                byte[] byResult = MakeReadCmd(DeviceAddress, CModbusFunctionCode.ReadInputRegister, BeginAddress, Convert.ToUInt16(4));

                string sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)
                {
                    Enqueue( "发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"



                #endregion
                
                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    //sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //**********************
                        //首字符
                        //[0] - :

                        //从站地址
                        //[1] - 0
                        //[2] - 1

                        //读取成功的功能码
                        //[3] - 0
                        //[4] - 1

                        //读取失败的错误码
                        //[3] - 0
                        //[4] - 8
                        //**********************
                        


                        #endregion

                        //string sTempResult = BytesToHexStringSplitByChar(byReadData);

                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 4

                        string sCmd = CModbusFunctionCode.ReadInputRegister.ToString("X2");

                        //if (cByte3 == 0x0.ToString()[0] && cByte4 == CModbusFunctionCode.ReadInputRegister.ToString()[0])//:0104
                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])
                        {
                            //每次截取16个16进制字符，转换为double型数据
                            string sTemp = Strings.Mid(sFeedBackFromSlave, 8, 16);

                            byte[] byDataBytes = new byte[8];
                            byDataBytes[0] = TwoHexCharsToByte(Strings.Mid(sTemp, 3, 2));
                            byDataBytes[1] = TwoHexCharsToByte(Strings.Mid(sTemp, 1, 2));
                            byDataBytes[2] = TwoHexCharsToByte(Strings.Mid(sTemp, 7, 2));
                            byDataBytes[3] = TwoHexCharsToByte(Strings.Mid(sTemp, 5, 2));

                            byDataBytes[4] = TwoHexCharsToByte(Strings.Mid(sTemp, 11, 2));
                            byDataBytes[5] = TwoHexCharsToByte(Strings.Mid(sTemp, 9, 2));
                            byDataBytes[6] = TwoHexCharsToByte(Strings.Mid(sTemp, 15, 2));
                            byDataBytes[7] = TwoHexCharsToByte(Strings.Mid(sTemp, 13, 2));
                            
                            Value = BitConverter.ToDouble(byDataBytes, 0);
                            byDataBytes = null;
                                                        
                            byReadData = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);
                            byReadData = null;
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)//null == byReadData || byReadData.Length < 2
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.ReadInputRegister.ToString("X2"))//"04"
                        {
                            //每次截取16个16进制字符，转换为double型数据
                            string sTemp = Strings.Mid(sFeedBackFromSlave, 8, 16);

                            byte[] byDataBytes = new byte[8];
                            byDataBytes[0] = TwoHexCharsToByte(Strings.Mid(sTemp, 3, 2));
                            byDataBytes[1] = TwoHexCharsToByte(Strings.Mid(sTemp, 1, 2));
                            byDataBytes[2] = TwoHexCharsToByte(Strings.Mid(sTemp, 7, 2));
                            byDataBytes[3] = TwoHexCharsToByte(Strings.Mid(sTemp, 5, 2));

                            byDataBytes[4] = TwoHexCharsToByte(Strings.Mid(sTemp, 11, 2));
                            byDataBytes[5] = TwoHexCharsToByte(Strings.Mid(sTemp, 9, 2));
                            byDataBytes[6] = TwoHexCharsToByte(Strings.Mid(sTemp, 15, 2));
                            byDataBytes[7] = TwoHexCharsToByte(Strings.Mid(sTemp, 13, 2));

                            Value = BitConverter.ToDouble(byDataBytes, 0);
                            byDataBytes = null;

                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);

                            sFeedBackFromSlave = null;
                            //throw new Exception("");
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        //  ?? 测试读取数据时不能像其它功能码一样正确读取long值，在int范围内可以正确读取，其它会对应不上，待更多测试
        /// <summary>
        /// 读取从站输入寄存器的当前值(long)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="Value">返回输入寄存器的当前值(long)</param>
        /// <returns>是否成功执行命令</returns>
        private bool ReadInputRegister(byte DeviceAddress, ushort BeginAddress, ref long Value)//  , out string ReadBackData)
        {
            try
            {
                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //起始位	    设备地址	功能代码	起始地址   	读取数据长度   LRC校验	   结束符
                //1个字符(:)	2个字符	    2个字符	    4个字符     4个字符	       2个字符	   2个字符(回车+换行)
                //1个字节       1个字节     1个字节     2个字节     2个字节        1个字节     2个字节
                byte[] byResult = MakeReadCmd(DeviceAddress, CModbusFunctionCode.ReadInputRegister, BeginAddress, Convert.ToUInt16(4));

                string sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)
                {
                    Enqueue( "发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"



                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    //sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //**********************
                        //首字符
                        //[0] - :

                        //从站地址
                        //[1] - 0
                        //[2] - 1

                        //读取成功的功能码
                        //[3] - 0
                        //[4] - 1

                        //读取失败的错误码
                        //[3] - 0
                        //[4] - 8
                        //**********************
                        


                        #endregion

                        //string sTempResult = BytesToHexStringSplitByChar(byReadData);

                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 4

                        string sCmd = CModbusFunctionCode.ReadInputRegister.ToString("X2");

                        //if (cByte3 == 0x0.ToString()[0] && cByte4 == CModbusFunctionCode.ReadInputRegister.ToString()[0])//:0104
                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])
                        {
                            //每次截取16个16进制字符，转换为long型数据
                            string sTemp = Strings.Mid(sFeedBackFromSlave, 8, 16);

                            //这里是读取的字节结构：
                            //:010408000280000000000071\r\n
                            //读取的字节结构：   0002800000000000
                            //转换的HEX字节结构：‭‬0000000080000002

                            string sResult = "";
                            for (int j = sTemp.Length / 4; j > 0; j--)
                            {
                                sResult += Strings.Mid(sTemp, (j - 1) * 4 + 1, 4);
                            }

                            Value = HexStringToLong(sResult);

                            ////每次截取16个16进制字符，转换为double型数据
                            //string sTemp = Strings.Mid(sFeedBackFromSlave, 8, 16);//

                            //byte[] byDataBytes = new byte[8];
                            //byDataBytes[0] = TwoHexCharsToByte(Strings.Mid(sTemp, 3, 2));
                            //byDataBytes[1] = TwoHexCharsToByte(Strings.Mid(sTemp, 1, 2));
                            //byDataBytes[2] = TwoHexCharsToByte(Strings.Mid(sTemp, 7, 2));
                            //byDataBytes[3] = TwoHexCharsToByte(Strings.Mid(sTemp, 5, 2));

                            //byDataBytes[4] = TwoHexCharsToByte(Strings.Mid(sTemp, 11, 2));
                            //byDataBytes[5] = TwoHexCharsToByte(Strings.Mid(sTemp, 9, 2));
                            //byDataBytes[6] = TwoHexCharsToByte(Strings.Mid(sTemp, 15, 2));
                            //byDataBytes[7] = TwoHexCharsToByte(Strings.Mid(sTemp, 13, 2));

                            //Value = BitConverter.ToInt64(byDataBytes, 0);
                            
                            byReadData = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);
                            byReadData = null;
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)//null == byReadData || byReadData.Length < 2
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.ReadInputRegister.ToString("X2"))//"04"
                        {
                            //每次截取16个16进制字符，转换为long型数据
                            string sTemp = Strings.Mid(sFeedBackFromSlave, 8, 16);

                            string sResult = "";
                            //:01041000028000FFFE7FFF0000000000000000EE\r\n
                            //读取的字节结构：   00028000FFFE7FFF
                            //转换的HEX字节结构：‭‬7FFFFFFE80000002

                            for (int j = sTemp.Length / 4; j > 0; j--)
                            {
                                sResult += Strings.Mid(sTemp, (j - 1) * 4 + 1, 4);
                            }

                            Value = HexStringToLong(sResult);

                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);

                            sFeedBackFromSlave = null;
                            //throw new Exception("");
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        //  ?? 测试读取数据时不能像其它功能码一样正确读取long值，在int范围内可以正确读取，其它会对应不上，待更多测试
        /// <summary>
        /// 读取从站输入寄存器的当前值(int)，函数是以字节为读取数据长度单位
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：双字</param>
        /// <param name="Value">返回输入寄存器的当前值(int)</param>
        /// <returns>是否成功执行命令</returns>
        private bool ReadInputRegister(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref long[] Value)//  , out string ReadBackData)
        {
            try
            {
                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                //if (ReadDataLength < 1 || ReadDataLength > 125)//读取数据长度，有效值范围：1~250(字节)
                //{
                //    throw new Exception("读取数据长度的值不正确，有效值范围：1~250(字节)");
                //}

                if (BeginAddress + ReadDataLength * 4 * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //起始位	    设备地址	功能代码	起始地址   	读取数据长度   LRC校验	   结束符
                //1个字符(:)	2个字符	    2个字符	    4个字符     4个字符	       2个字符	   2个字符(回车+换行)
                //1个字节       1个字节     1个字节     2个字节     2个字节        1个字节     2个字节
                byte[] byResult = MakeReadCmd(DeviceAddress, CModbusFunctionCode.ReadInputRegister, BeginAddress, Convert.ToUInt16(ReadDataLength * 4));

                string sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)
                {
                    Enqueue( "发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"



                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    //sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //**********************
                        //首字符
                        //[0] - :

                        //从站地址
                        //[1] - 0
                        //[2] - 1

                        //读取成功的功能码
                        //[3] - 0
                        //[4] - 1

                        //读取失败的错误码
                        //[3] - 0
                        //[4] - 8
                        //**********************



                        #endregion

                        //string sTempResult = BytesToHexStringSplitByChar(byReadData);

                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 4

                        string sCmd = CModbusFunctionCode.ReadInputRegister.ToString("X2");

                        //if (cByte3 == 0x0.ToString()[0] && cByte4 == CModbusFunctionCode.ReadInputRegister.ToString()[0])//:0104
                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])
                        {
                            long[] lResultValue = new long[ReadDataLength];

                            for (int i = 0; i < ReadDataLength; i++)
                            {
                                //每次截取16个16进制字符，转换为long型数据
                                string sTemp = Strings.Mid(sFeedBackFromSlave, 8 + i * 16, 16);

                                //这里是读取的字节结构：
                                //:01041000028000FFFE7FFF0000000000000000EE\r\n
                                //读取的字节结构：   00028000FFFE7FFF
                                //转换的HEX字节结构：‭‬7FFFFFFE80000002

                                string sResult = "";
                                for (int j = sTemp.Length / 4; j > 0; j--)
                                {
                                    sResult += Strings.Mid(sTemp, (j - 1) * 4 + 1, 4);
                                }

                                lResultValue[i] = HexStringToLong(sResult);
                            }

                            Value = lResultValue;
                            lResultValue = null;
                            
                            sFeedBackFromSlave = null;
                            byReadData = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);

                            sFeedBackFromSlave = null;
                            byReadData = null;
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)//null == byReadData || byReadData.Length < 2
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.ReadInputRegister.ToString("X2"))//"04"
                        {
                            long[] lResultValue = new long[ReadDataLength];

                            for (int i = 0; i < ReadDataLength; i++)
                            {
                                //每次截取16个16进制字符，转换为long型数据
                                string sTemp = Strings.Mid(sFeedBackFromSlave, 8 + i * 16, 16);

                                //这里是读取的字节结构：
                                //:01041000028000FFFE7FFF0000000000000000EE\r\n
                                //读取的字节结构：   00028000FFFE7FFF
                                //转换的HEX字节结构：‭‬7FFFFFFE80000002

                                string sResult = "";
                                for (int j = sTemp.Length / 4; j > 0; j--)
                                {
                                    sResult += Strings.Mid(sTemp, (j - 1) * 4 + 1, 4);
                                }

                                lResultValue[i] = HexStringToLong(sResult);
                            }

                            Value = lResultValue;
                            lResultValue = null;

                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);

                            sFeedBackFromSlave = null;
                            //throw new Exception("");
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        #endregion

        #region "读单个/多个保持寄存器的状态 - ok"

        // ok
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
                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //起始位	    设备地址	功能代码	起始地址   	读取数据字长度   LRC校验	   结束符
                //1个字符(:)	2个字符	    2个字符	    4个字符     4个字符	         2个字符	   2个字符(回车+换行)
                //1个字节       1个字节     1个字节     2个字节     2个字节          1个字节       2个字节
                byte[] byResult = MakeReadCmd(DeviceAddress, CModbusFunctionCode.ReadRegister, BeginAddress, Convert.ToUInt16(1));

                string sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)
                {
                    Enqueue( "发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"



                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    //sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //**********************
                        //首字符
                        //[0] - :

                        //从站地址
                        //[1] - 0
                        //[2] - 1

                        //读取成功的功能码
                        //[3] - 0
                        //[4] - 3

                        //读取失败的错误码
                        //[3] - 0
                        //[4] - 8
                        //**********************
                        
                        #endregion
                        
                        //string sTempResult = BytesToHexStringSplitByChar(byReadData);

                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 3

                        string sCmd = CModbusFunctionCode.ReadRegister.ToString("X2");

                        //if (cByte3 == 0x0.ToString()[0] && cByte4 == CModbusFunctionCode.ReadRegister.ToString()[0])//:0103
                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])
                        {
                            //每次截取4个16进制字符，转换为int型数据，再转换为short型数据
                            string sTemp = Strings.Mid(sFeedBackFromSlave, 8, 4);//short	-32,768 到 32,767

                            Value = (short)HexStringToInt(sTemp);

                            //int iValue = (short)HexStringToInt(sTemp);
                            ////转换为short型数据
                            //byte[] byCalcIntBytes = BitConverter.GetBytes(iValue);
                            //Value = BitConverter.ToInt16(byCalcIntBytes, 0);
                            //byCalcIntBytes = null;

                            byReadData = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);
                            byReadData = null;
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)//null == byReadData || byReadData.Length < 2
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.ReadRegister.ToString("X2"))//"03"
                        {
                            //每次截取4个16进制字符，转换为int型数据，再转换为short型数据
                            string sTemp = Strings.Mid(sFeedBackFromSlave, 8, 4);//short	-32,768 到 32,767

                            Value = (short)HexStringToInt(sTemp);

                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);

                            sFeedBackFromSlave = null;
                            //throw new Exception("");
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok
        /// <summary>
        /// 读取从站保持寄存器的当前值(short: -32,768 到 32,767)，函数是以字节为读取数据长度单位
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：short</param>
        /// <param name="Value">返回保持寄存器的当前值(short: -32,768 到 32,767)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadKeepRegister(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref short[] Value)//  , out string ReadBackData)
        {
            try
            {
                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                //if (ReadDataLength < 1 || ReadDataLength > 125)//读取数据字长度
                //{
                //    throw new Exception("读取数据字长度的值不正确，有效值范围：1~125(字)");
                //}

                if (BeginAddress + ReadDataLength * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //起始位	    设备地址	功能代码	起始地址   	读取数据长度   LRC校验	   结束符
                //1个字符(:)	2个字符	    2个字符	    4个字符     4个字符	       2个字符	   2个字符(回车+换行)
                //1个字节       1个字节     1个字节     2个字节     2个字节        1个字节     2个字节
                byte[] byResult = MakeReadCmd(DeviceAddress, CModbusFunctionCode.ReadRegister, BeginAddress, Convert.ToUInt16(ReadDataLength));

                string sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)
                {
                    Enqueue( "发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"



                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    //sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //**********************
                        //首字符
                        //[0] - :

                        //从站地址
                        //[1] - 0
                        //[2] - 1

                        //读取成功的功能码
                        //[3] - 0
                        //[4] - 1

                        //读取失败的错误码
                        //[3] - 0
                        //[4] - 8
                        //**********************

                        #endregion

                        //string sTempResult = BytesToHexStringSplitByChar(byReadData);

                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 3

                        string sCmd = CModbusFunctionCode.ReadRegister.ToString("X2");

                        //if (cByte3 == 0x0.ToString()[0] && cByte4 == CModbusFunctionCode.ReadRegister.ToString()[0])//:0103
                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])
                        {
                            short[] stResultValue = new short[ReadDataLength];

                            for (int i = 0; i < ReadDataLength; i++)
                            {
                                //每次截取4个16进制字符，转换为int型数据，再转换为short型数据
                                string sTemp = Strings.Mid(sFeedBackFromSlave, 8 + i * 4, 4);//short	-32,768 到 32,767

                                stResultValue[i] = (short)HexStringToInt(sTemp);
                            }

                            Value = stResultValue;
                            
                            sFeedBackFromSlave = null;
                            byReadData = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);

                            sFeedBackFromSlave = null;
                            byReadData = null;
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)//null == byReadData || byReadData.Length < 2
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.ReadRegister.ToString("X2"))//"03"
                        {
                            short[] stResultValue = new short[ReadDataLength];

                            for (int i = 0; i < ReadDataLength; i++)
                            {
                                //每次截取4个16进制字符，转换为int型数据，再转换为short型数据
                                string sTemp = Strings.Mid(sFeedBackFromSlave, 8 + i * 4, 4);//short	-32,768 到 32,767

                                stResultValue[i] = (short)HexStringToInt(sTemp);
                            }

                            Value = stResultValue;

                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);

                            sFeedBackFromSlave = null;
                            //throw new Exception("");
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok
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
                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 2 * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //起始位	    设备地址	功能代码	起始地址   	读取数据字长度   LRC校验	   结束符
                //1个字符(:)	2个字符	    2个字符	    4个字符     4个字符	         2个字符	   2个字符(回车+换行)
                //1个字节       1个字节     1个字节     2个字节     2个字节          1个字节       2个字节
                byte[] byResult = MakeReadCmd(DeviceAddress, CModbusFunctionCode.ReadRegister, BeginAddress, Convert.ToUInt16(2));

                string sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)
                {
                    Enqueue( "发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"



                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    //sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //**********************
                        //首字符
                        //[0] - :

                        //从站地址
                        //[1] - 0
                        //[2] - 1

                        //读取成功的功能码
                        //[3] - 0
                        //[4] - 3

                        //读取失败的错误码
                        //[3] - 0
                        //[4] - 8
                        //**********************
                        
                        #endregion
                        
                        //string sTempResult = BytesToHexStringSplitByChar(byReadData);

                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 3

                        string sCmd = CModbusFunctionCode.ReadRegister.ToString("X2");

                        //if (cByte3 == 0x0.ToString()[0] && cByte4 == CModbusFunctionCode.ReadRegister.ToString()[0])//:0103
                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])
                        {
                            string sTemp = Strings.Mid(sFeedBackFromSlave, 8, 8);

                            //读取的字节结构：075AFFFF
                            //转换的HEX字节结构：‭FFFF075A‬
                            string sResult = Strings.Mid(sTemp, 5, 4) + Strings.Mid(sTemp, 1, 4);

                            Value = HexStringToInt(sResult);

                            byReadData = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);
                            byReadData = null;
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)//null == byReadData || byReadData.Length < 2
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.ReadRegister.ToString("X2"))//"03"
                        {
                            string sTemp = Strings.Mid(sFeedBackFromSlave, 8, 8);

                            //读取的字节结构：075AFFFF
                            //转换的HEX字节结构：‭FFFF075A‬
                            string sResult = Strings.Mid(sTemp, 5, 4) + Strings.Mid(sTemp, 1, 4);

                            Value = HexStringToInt(sResult);

                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);

                            sFeedBackFromSlave = null;
                            //throw new Exception("");
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok
        /// <summary>
        /// 读取从站保持寄存器的当前值(int)
        /// </summary>
        /// <param name="DeviceAddress">从站设备地址</param>
        /// <param name="BeginAddress">读取起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，单位：short</param>
        /// <param name="Value">返回保持寄存器的当前值(int)</param>
        /// <returns>是否成功执行命令</returns>
        public bool ReadKeepRegister(byte DeviceAddress, ushort BeginAddress, ushort ReadDataLength, ref int[] Value)//  , out string ReadBackData)
        {
            try
            {
                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                //if (ReadDataLength < 1 || ReadDataLength > 125)//读取数据字长度
                //{
                //    throw new Exception("读取数据字长度的值不正确，有效值范围：1~125(字)");
                //}

                if (BeginAddress + ReadDataLength * 2 * 2 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //起始位	    设备地址	功能代码	起始地址   	读取数据长度   LRC校验	   结束符
                //1个字符(:)	2个字符	    2个字符	    4个字符     4个字符	       2个字符	   2个字符(回车+换行)
                //1个字节       1个字节     1个字节     2个字节     2个字节        1个字节     2个字节
                byte[] byResult = MakeReadCmd(DeviceAddress, CModbusFunctionCode.ReadRegister, BeginAddress, Convert.ToUInt16(ReadDataLength * 2));

                string sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)
                {
                    Enqueue( "发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"



                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    //sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //**********************
                        //首字符
                        //[0] - :

                        //从站地址
                        //[1] - 0
                        //[2] - 1

                        //读取成功的功能码
                        //[3] - 0
                        //[4] - 1

                        //读取失败的错误码
                        //[3] - 0
                        //[4] - 8
                        //**********************

                        #endregion

                        //string sTempResult = BytesToHexStringSplitByChar(byReadData);

                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 3

                        string sCmd = CModbusFunctionCode.ReadRegister.ToString("X2");

                        //if (cByte3 == 0x0.ToString()[0] && cByte4 == CModbusFunctionCode.ReadRegister.ToString()[0])//:0103
                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])
                        {
                            int[] iResultValue = new int[ReadDataLength];
                            
                            for (int i = 0; i < ReadDataLength; i++)
                            {
                                //:010308222B4C6C492A3945FE\r\n
                                string sTemp = Strings.Mid(sFeedBackFromSlave, 8 + i * 8, 8);

                                //读取的字节结构：075AFFFF
                                //转换的HEX字节结构：‭FFFF075A‬
                                string sResult = Strings.Mid(sTemp, 5, 4) + Strings.Mid(sTemp, 1, 4);

                                iResultValue[i] = HexStringToInt(sResult);
                            }

                            Value = iResultValue;
                            iResultValue = null;
                            
                            sFeedBackFromSlave = null;
                            byReadData = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);

                            sFeedBackFromSlave = null;
                            byReadData = null;
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)//null == byReadData || byReadData.Length < 2
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.ReadRegister.ToString("X2"))//"03"
                        {
                            int[] iResultValue = new int[ReadDataLength];

                            for (int i = 0; i < ReadDataLength; i++)
                            {
                                string sTemp = Strings.Mid(sFeedBackFromSlave, 8 + i * 8, 8);

                                //读取的字节结构：075AFFFF
                                //转换的HEX字节结构：‭FFFF075A‬
                                string sResult = Strings.Mid(sTemp, 5, 4) + Strings.Mid(sTemp, 1, 4);

                                iResultValue[i] = HexStringToInt(sResult);
                            }

                            Value = iResultValue;
                            iResultValue = null;

                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);

                            sFeedBackFromSlave = null;
                            //throw new Exception("");
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok
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
                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //起始位	    设备地址	功能代码	起始地址   	读取数据字长度   LRC校验	   结束符
                //1个字符(:)	2个字符	    2个字符	    4个字符     4个字符	         2个字符	   2个字符(回车+换行)
                //1个字节       1个字节     1个字节     2个字节     2个字节          1个字节     2个字节
                byte[] byResult = MakeReadCmd(DeviceAddress, CModbusFunctionCode.ReadRegister, BeginAddress, Convert.ToUInt16(2));

                string sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)
                {
                    Enqueue( "发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"

                //**********************
                //首字符
                //[0] - :

                //从站地址
                //[1] - 0
                //[2] - 1

                //读取成功的功能码
                //[3] - 0
                //[4] - F

                //读取失败的错误码
                //[3] - 0
                //[4] - 8
                //**********************



                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    //sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        //string sTempResult = BytesToHexStringSplitByChar(byReadData);

                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 3

                        string sCmd = CModbusFunctionCode.ReadRegister.ToString("X2");

                        //if (cByte3 == 0x0.ToString()[0] && cByte4 == CModbusFunctionCode.ReadRegister.ToString()[0])//:0103
                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])
                        {
                            string sTemp = Strings.Mid(sFeedBackFromSlave, 8, 8);

                            //读取的字节结构：075AFFFF
                            //转换的HEX字节结构：‭FFFF075A‬
                            string sResult = Strings.Mid(sTemp, 5, 4) + Strings.Mid(sTemp, 1, 4);

                            byte[] byDataBytes = new byte[4];

                            //每次截取8个16进制字符，转换为float型数据
                            byDataBytes[0] = TwoHexCharsToByte(Strings.Mid(sTemp, 3, 2));
                            byDataBytes[1] = TwoHexCharsToByte(Strings.Mid(sTemp, 1, 2));
                            byDataBytes[2] = TwoHexCharsToByte(Strings.Mid(sTemp, 7, 2));
                            byDataBytes[3] = TwoHexCharsToByte(Strings.Mid(sTemp, 5, 2));
                            
                            Value = BitConverter.ToSingle(byDataBytes, 0);

                            byDataBytes = null;
                            sFeedBackFromSlave = null;
                            byReadData = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);

                            sFeedBackFromSlave = null;
                            byReadData = null;
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)//null == byReadData || byReadData.Length < 2
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.ReadRegister.ToString("X2"))//"03"
                        {
                            byte[] byDataBytes = new byte[4];

                            //每次截取8个16进制字符，转换为float型数据
                            string sTemp = Strings.Mid(sFeedBackFromSlave, 8, 8);//
                            byDataBytes[0] = TwoHexCharsToByte(Strings.Mid(sTemp, 3, 2));
                            byDataBytes[1] = TwoHexCharsToByte(Strings.Mid(sTemp, 1, 2));
                            byDataBytes[2] = TwoHexCharsToByte(Strings.Mid(sTemp, 7, 2));
                            byDataBytes[3] = TwoHexCharsToByte(Strings.Mid(sTemp, 5, 2));

                            Value = BitConverter.ToSingle(byDataBytes, 0);

                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);

                            sFeedBackFromSlave = null;
                            //throw new Exception("");
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok
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
                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 4 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //起始位	    设备地址	功能代码	起始地址   	读取数据字长度   LRC校验	   结束符
                //1个字符(:)	2个字符	    2个字符	    4个字符     4个字符	         2个字符	   2个字符(回车+换行)
                //1个字节       1个字节     1个字节     2个字节     2个字节          1个字节     2个字节
                byte[] byResult = MakeReadCmd(DeviceAddress, CModbusFunctionCode.ReadRegister, BeginAddress, Convert.ToUInt16(ReadDataLength * 2));

                string sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)
                {
                    Enqueue( "发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"

                //**********************
                //首字符
                //[0] - :

                //从站地址
                //[1] - 0
                //[2] - 1

                //读取成功的功能码
                //[3] - 0
                //[4] - F

                //读取失败的错误码
                //[3] - 0
                //[4] - 8
                //**********************

                //发送值[0~3]：-1096 4743.429 5829.714 -2837.143 
                //成功执行写命令
                //写操作记录：(COM1)发送字符串 - :011000000008100000C4893B6E45942DB745B65249C53198
                //写操作记录：(COM1)收到字符串 - :011000000008E7
                
                //成功执行读命令，结果值：-1096 4743.429 
                //(COM1)发送字符串 - :010300000004F8
                //(COM1)收到字符串 - :0103080000C4893B6E459425
                
                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    //sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        //string sTempResult = BytesToHexStringSplitByChar(byReadData);

                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 3

                        string sCmd = CModbusFunctionCode.ReadRegister.ToString("X2");

                        //if (cByte3 == 0x0.ToString()[0] && cByte4 == CModbusFunctionCode.ReadRegister.ToString()[0])//:0103
                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])
                        {
                            //-36.265480
                            //:0103040FDAC2113C\r\n

                            //0.0
                            //:01030400000000F8\r\n
                            float[] fResultData = new float[ReadDataLength];

                            for (int i = 0; i < ReadDataLength; i++)
                            {
                                byte[] byDataBytes = new byte[4];

                                //每次截取8个16进制字符，转换为float型数据
                                string sTemp = Strings.Mid(sFeedBackFromSlave, 8 + i * 8, 8);
                                byDataBytes[0] = TwoHexCharsToByte(Strings.Mid(sTemp, 3, 2));
                                byDataBytes[1] = TwoHexCharsToByte(Strings.Mid(sTemp, 1, 2));
                                byDataBytes[2] = TwoHexCharsToByte(Strings.Mid(sTemp, 7, 2));
                                byDataBytes[3] = TwoHexCharsToByte(Strings.Mid(sTemp, 5, 2));

                                fResultData[i] = BitConverter.ToSingle(byDataBytes, 0);

                                byDataBytes = null;
                            }

                            Value = fResultData;
                            fResultData = null;
                                                        
                            sFeedBackFromSlave = null;
                            byReadData = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);

                            sFeedBackFromSlave = null;
                            byReadData = null;
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)//null == byReadData || byReadData.Length < 2
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.ReadRegister.ToString("X2"))//"03"
                        {
                            float[] fResultData = new float[ReadDataLength];

                            for (int i = 0; i < ReadDataLength; i++)
                            {
                                byte[] byDataBytes = new byte[4];

                                //每次截取8个16进制字符，转换为float型数据
                                string sTemp = Strings.Mid(sFeedBackFromSlave, 8 + i * 8, 8);
                                byDataBytes[0] = TwoHexCharsToByte(Strings.Mid(sTemp, 3, 2));
                                byDataBytes[1] = TwoHexCharsToByte(Strings.Mid(sTemp, 1, 2));
                                byDataBytes[2] = TwoHexCharsToByte(Strings.Mid(sTemp, 7, 2));
                                byDataBytes[3] = TwoHexCharsToByte(Strings.Mid(sTemp, 5, 2));

                                fResultData[i] = BitConverter.ToSingle(byDataBytes, 0);

                                byDataBytes = null;
                            }

                            Value = fResultData;
                            fResultData = null;

                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);

                            sFeedBackFromSlave = null;
                            //throw new Exception("");
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok
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
                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //起始位	    设备地址	功能代码	起始地址   	读取数据字长度   LRC校验	   结束符
                //1个字符(:)	2个字符	    2个字符	    4个字符     4个字符	         2个字符	   2个字符(回车+换行)
                //1个字节       1个字节     1个字节     2个字节     2个字节          1个字节     2个字节
                byte[] byResult = MakeReadCmd(DeviceAddress, CModbusFunctionCode.ReadRegister, BeginAddress, Convert.ToUInt16(4));

                string sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)
                {
                    Enqueue( "发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"

                //**********************
                //首字符
                //[0] - :

                //从站地址
                //[1] - 0
                //[2] - 1

                //读取成功的功能码
                //[3] - 0
                //[4] - F

                //读取失败的错误码
                //[3] - 0
                //[4] - 8
                //**********************

                //成功执行写命令-29.1428571428571
                //写操作记录：(COM1)发送字符串 - :01100000000408924949242492C03DE8
                //写操作记录：(COM1)收到字符串 - :011000000004EB

                //成功执行读命令，结果值：-29.1428571428571
                //(COM1)发送字符串 - :010300000004F8
                //(COM1)收到字符串 - :010308924949242492C03DF9

                //成功执行写命令5415.71428571429
                //写操作记录：(COM1)发送字符串 - :01100000000408B6DBDB6D27B640B538
                //写操作记录：(COM1)收到字符串 - :011000000004EB

                //成功执行读命令，结果值：5415.71428571429
                //(COM1)发送字符串 - :010300000004F8
                //(COM1)收到字符串 - :010308B6DBDB6D27B640B549

                //成功执行写命令-3825.71428571429
                //写操作记录：(COM1)发送字符串 - :011000000004086DB7B6DBE36DC0AD71
                //写操作记录：(COM1)收到字符串 - :011000000004EB

                //成功执行读命令，结果值：-3825.71428571429
                //(COM1)发送字符串 - :010300000004F8
                //(COM1)收到字符串 - :0103086DB7B6DBE36DC0AD82

                //成功执行读命令，结果值：-3825.71428571429
                //(COM1)发送字符串 - :010300000004F8
                //(COM1)收到字符串 - :0103086DB7B6DBE36DC0AD82

                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    //sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        //string sTempResult = BytesToHexStringSplitByChar(byReadData);

                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 3

                        string sCmd = CModbusFunctionCode.ReadRegister.ToString("X2");

                        //if (cByte3 == 0x0.ToString()[0] && cByte4 == CModbusFunctionCode.ReadRegister.ToString()[0])//:0103
                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])
                        {

                            byte[] byDataBytes = new byte[8];

                            //每次截取16个16进制字符，转换为double型数据
                            string sTemp = Strings.Mid(sFeedBackFromSlave, 8, 16);//
                            byDataBytes[0] = TwoHexCharsToByte(Strings.Mid(sTemp, 3, 2));
                            byDataBytes[1] = TwoHexCharsToByte(Strings.Mid(sTemp, 1, 2));
                            byDataBytes[2] = TwoHexCharsToByte(Strings.Mid(sTemp, 7, 2));
                            byDataBytes[3] = TwoHexCharsToByte(Strings.Mid(sTemp, 5, 2));

                            byDataBytes[4] = TwoHexCharsToByte(Strings.Mid(sTemp, 11, 2));
                            byDataBytes[5] = TwoHexCharsToByte(Strings.Mid(sTemp, 9, 2));
                            byDataBytes[6] = TwoHexCharsToByte(Strings.Mid(sTemp, 15, 2));
                            byDataBytes[7] = TwoHexCharsToByte(Strings.Mid(sTemp, 13, 2));

                            Value = BitConverter.ToDouble(byDataBytes, 0);

                            byDataBytes = null;
                            sFeedBackFromSlave = null;
                            byReadData = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);

                            sFeedBackFromSlave = null;
                            byReadData = null;
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)//null == byReadData || byReadData.Length < 2
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.ReadRegister.ToString("X2"))//"03"
                        {
                            byte[] byDataBytes = new byte[8];

                            //每次截取16个16进制字符，转换为double型数据
                            string sTemp = Strings.Mid(sFeedBackFromSlave, 8, 16);//
                            byDataBytes[0] = TwoHexCharsToByte(Strings.Mid(sTemp, 3, 2));
                            byDataBytes[1] = TwoHexCharsToByte(Strings.Mid(sTemp, 1, 2));
                            byDataBytes[2] = TwoHexCharsToByte(Strings.Mid(sTemp, 7, 2));
                            byDataBytes[3] = TwoHexCharsToByte(Strings.Mid(sTemp, 5, 2));

                            byDataBytes[4] = TwoHexCharsToByte(Strings.Mid(sTemp, 11, 2));
                            byDataBytes[5] = TwoHexCharsToByte(Strings.Mid(sTemp, 9, 2));
                            byDataBytes[6] = TwoHexCharsToByte(Strings.Mid(sTemp, 15, 2));
                            byDataBytes[7] = TwoHexCharsToByte(Strings.Mid(sTemp, 13, 2));

                            Value = BitConverter.ToDouble(byDataBytes, 0);

                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);

                            sFeedBackFromSlave = null;
                            //throw new Exception("");
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok
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
                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //起始位	    设备地址	功能代码	起始地址   	读取数据字长度   LRC校验	   结束符
                //1个字符(:)	2个字符	    2个字符	    4个字符     4个字符	         2个字符	   2个字符(回车+换行)
                //1个字节       1个字节     1个字节     2个字节     2个字节          1个字节     2个字节
                byte[] byResult = MakeReadCmd(DeviceAddress, CModbusFunctionCode.ReadRegister, BeginAddress, Convert.ToUInt16(ReadDataLength * 4));

                string sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)
                {
                    Enqueue( "发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"

                //**********************
                //首字符
                //[0] - :

                //从站地址
                //[1] - 0
                //[2] - 1

                //读取成功的功能码
                //[3] - 0
                //[4] - F

                //读取失败的错误码
                //[3] - 0
                //[4] - 8
                //**********************

                //发送值[0~3]：-6502 -5821.14306640625 539.714294433594 -6940.5712890625 
                //成功执行写命令
                //写操作记录：(COM1)发送字符串 - :01100000001020000000006600C0B90000A000BD24C0B60000E000DDB64080000040001C92C0BB4D
                //写操作记录：(COM1)收到字符串 - :011000000010DF

                //成功执行读命令，结果值：-6502 -5821.14306640625 
                //(COM1)发送字符串 - :010300000008F4
                //(COM1)收到字符串 - :010310000000006600C0B90000A000BD24C0B616

                //发送值[0~3]：7027.4287109375 5529.4287109375 -7479.71435546875 -5750.5712890625 
                //成功执行写命令
                //写操作记录：(COM1)发送字符串 - :011000000010200000C000736D40BB0000C000996D40B50000E00037B6C0BD000040007692C0B661
                //写操作记录：(COM1)收到字符串 - :011000000010DF

                //成功执行读命令，结果值：7027.4287109375 5529.4287109375 
                //(COM1)发送字符串 - :010300000008F4
                //(COM1)收到字符串 - :0103100000C000736D40BB0000C000996D40B596

                //发送值[0~3]：6341.14306640625 -2607.14282226563 1535.14282226563 -3786
                //写操作记录：(COM1)发送字符串 - :011000000010200000A000C52440B8000020005E49C0A400004000FC924097000000009400C0AD6D
                //写操作记录：(COM1)收到字符串 - :011000000010DF

                //成功执行读命令，结果值：6341.14306640625 -2607.14282226563 
                //(COM1)发送字符串 - :010300000008F4
                //(COM1)收到字符串 - :0103100000A000C52440B8000020005E49C0A440

                //发送值[0~3]：9072.5712890625 6789.4287109375 -6218.85693359375 3906
                //写操作记录：(COM1)发送字符串 - :0110000000102000002000B84940C10000C000856D40BA000060004ADBC0B800000000840040AE82
                //写操作记录：(COM1)收到字符串 - :011000000010DF

                //成功执行读命令，结果值：9072.5712890625 6789.4287109375 
                //(COM1)发送字符串 - :010300000008F4
                //(COM1)收到字符串 - :01031000002000B84940C10000C000856D40BA1E
                
                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    //sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        //string sTempResult = BytesToHexStringSplitByChar(byReadData);

                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 3

                        string sCmd = CModbusFunctionCode.ReadRegister.ToString("X2");

                        //if (cByte3 == 0x0.ToString()[0] && cByte4 == CModbusFunctionCode.ReadRegister.ToString()[0])//:0103
                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])
                        {
                            double[] dResultValue = new double[ReadDataLength];

                            for (int i = 0; i < ReadDataLength; i++)
                            {
                                byte[] byDataBytes = new byte[8];

                                //每次截取16个16进制字符，转换为double型数据
                                string sTemp = Strings.Mid(sFeedBackFromSlave, 8 + i * 16, 16);//
                                byDataBytes[0] = TwoHexCharsToByte(Strings.Mid(sTemp, 3, 2));
                                byDataBytes[1] = TwoHexCharsToByte(Strings.Mid(sTemp, 1, 2));
                                byDataBytes[2] = TwoHexCharsToByte(Strings.Mid(sTemp, 7, 2));
                                byDataBytes[3] = TwoHexCharsToByte(Strings.Mid(sTemp, 5, 2));

                                byDataBytes[4] = TwoHexCharsToByte(Strings.Mid(sTemp, 11, 2));
                                byDataBytes[5] = TwoHexCharsToByte(Strings.Mid(sTemp, 9, 2));
                                byDataBytes[6] = TwoHexCharsToByte(Strings.Mid(sTemp, 15, 2));
                                byDataBytes[7] = TwoHexCharsToByte(Strings.Mid(sTemp, 13, 2));

                                dResultValue[i] = BitConverter.ToDouble(byDataBytes, 0);

                                byDataBytes = null;
                            }

                            Value = dResultValue;
                            dResultValue = null;

                            sFeedBackFromSlave = null;
                            byReadData = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);

                            sFeedBackFromSlave = null;
                            byReadData = null;
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)//null == byReadData || byReadData.Length < 2
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.ReadRegister.ToString("X2"))//"03"
                        {
                            double[] dResultValue = new double[ReadDataLength];

                            for (int i = 0; i < ReadDataLength; i++)
                            {
                                byte[] byDataBytes = new byte[8];

                                //每次截取16个16进制字符，转换为double型数据
                                string sTemp = Strings.Mid(sFeedBackFromSlave, 8 + i * 16, 16);//
                                byDataBytes[0] = TwoHexCharsToByte(Strings.Mid(sTemp, 3, 2));
                                byDataBytes[1] = TwoHexCharsToByte(Strings.Mid(sTemp, 1, 2));
                                byDataBytes[2] = TwoHexCharsToByte(Strings.Mid(sTemp, 7, 2));
                                byDataBytes[3] = TwoHexCharsToByte(Strings.Mid(sTemp, 5, 2));

                                byDataBytes[4] = TwoHexCharsToByte(Strings.Mid(sTemp, 11, 2));
                                byDataBytes[5] = TwoHexCharsToByte(Strings.Mid(sTemp, 9, 2));
                                byDataBytes[6] = TwoHexCharsToByte(Strings.Mid(sTemp, 15, 2));
                                byDataBytes[7] = TwoHexCharsToByte(Strings.Mid(sTemp, 13, 2));

                                dResultValue[i] = BitConverter.ToDouble(byDataBytes, 0);

                                byDataBytes = null;
                            }

                            Value = dResultValue;
                            dResultValue = null;

                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);

                            sFeedBackFromSlave = null;
                            //throw new Exception("");
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok
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
                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //起始位	    设备地址	功能代码	起始地址   	读取数据字长度   LRC校验	   结束符
                //1个字符(:)	2个字符	    2个字符	    4个字符     4个字符	         2个字符	   2个字符(回车+换行)
                //1个字节       1个字节     1个字节     2个字节     2个字节          1个字节     2个字节
                byte[] byResult = MakeReadCmd(DeviceAddress, CModbusFunctionCode.ReadRegister, BeginAddress, Convert.ToUInt16(4));

                string sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)
                {
                    Enqueue( "发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"

                //**********************
                //首字符
                //[0] - :

                //从站地址
                //[1] - 0
                //[2] - 1

                //读取成功的功能码
                //[3] - 0
                //[4] - F

                //读取失败的错误码
                //[3] - 0
                //[4] - 8
                //**********************

                //成功执行写命令-105431462
                //写操作记录：(COM1)发送字符串 - :011000000004083E5AF9B7FFFFFFFF9F
                //写操作记录：(COM1)收到字符串 - :011000000004EB

                //成功执行读命令，结果值：-105431462
                //(COM1)发送字符串 - :010300000004F8
                //(COM1)收到字符串 - :0103083E5AF9B7FFFFFFFFB0

                //成功执行写命令1325929153
                //写操作记录：(COM1)发送字符串 - :0110000000040812C14F0800000000B9
                //写操作记录：(COM1)收到字符串 - :011000000004EB

                //成功执行读命令，结果值：1325929153
                //(COM1)发送字符串 - :010300000004F8
                //(COM1)收到字符串 - :01030812C14F0800000000CA

                //成功执行写命令218182470
                //写操作记录：(COM1)发送字符串 - :0110000000040833460D01000000005C
                //写操作记录：(COM1)收到字符串 - :011000000004EB

                //成功执行读命令，结果值：218182470
                //(COM1)发送字符串 - :010300000004F8
                //(COM1)收到字符串 - :01030833460D01000000006D

                //成功执行写命令-1344473489
                //写操作记录：(COM1)发送字符串 - :01100000000408F66FAFDCFFFFFFFFF7
                //写操作记录：(COM1)收到字符串 - :011000000004EB

                //成功执行读命令，结果值：-1344473489
                //(COM1)发送字符串 - :010300000004F8
                //(COM1)收到字符串 - :010308F66FAFDCFFFFFFFF08

                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    //sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        //string sTempResult = BytesToHexStringSplitByChar(byReadData);

                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 3

                        string sCmd = CModbusFunctionCode.ReadRegister.ToString("X2");

                        //if (cByte3 == 0x0.ToString()[0] && cByte4 == CModbusFunctionCode.ReadRegister.ToString()[0])//:0103
                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])
                        {

                            byte[] byDataBytes = new byte[8];

                            //每次截取16个16进制字符，转换为double型数据
                            string sTemp = Strings.Mid(sFeedBackFromSlave, 8, 16);//
                            byDataBytes[0] = TwoHexCharsToByte(Strings.Mid(sTemp, 3, 2));
                            byDataBytes[1] = TwoHexCharsToByte(Strings.Mid(sTemp, 1, 2));
                            byDataBytes[2] = TwoHexCharsToByte(Strings.Mid(sTemp, 7, 2));
                            byDataBytes[3] = TwoHexCharsToByte(Strings.Mid(sTemp, 5, 2));

                            byDataBytes[4] = TwoHexCharsToByte(Strings.Mid(sTemp, 11, 2));
                            byDataBytes[5] = TwoHexCharsToByte(Strings.Mid(sTemp, 9, 2));
                            byDataBytes[6] = TwoHexCharsToByte(Strings.Mid(sTemp, 15, 2));
                            byDataBytes[7] = TwoHexCharsToByte(Strings.Mid(sTemp, 13, 2));

                            Value = BitConverter.ToInt64(byDataBytes, 0);

                            byDataBytes = null;
                            sFeedBackFromSlave = null;
                            byReadData = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);

                            sFeedBackFromSlave = null;
                            byReadData = null;
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)//null == byReadData || byReadData.Length < 2
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.ReadRegister.ToString("X2"))//"03"
                        {
                            byte[] byDataBytes = new byte[8];

                            //每次截取16个16进制字符，转换为double型数据
                            string sTemp = Strings.Mid(sFeedBackFromSlave, 8, 16);//
                            byDataBytes[0] = TwoHexCharsToByte(Strings.Mid(sTemp, 3, 2));
                            byDataBytes[1] = TwoHexCharsToByte(Strings.Mid(sTemp, 1, 2));
                            byDataBytes[2] = TwoHexCharsToByte(Strings.Mid(sTemp, 7, 2));
                            byDataBytes[3] = TwoHexCharsToByte(Strings.Mid(sTemp, 5, 2));

                            byDataBytes[4] = TwoHexCharsToByte(Strings.Mid(sTemp, 11, 2));
                            byDataBytes[5] = TwoHexCharsToByte(Strings.Mid(sTemp, 9, 2));
                            byDataBytes[6] = TwoHexCharsToByte(Strings.Mid(sTemp, 15, 2));
                            byDataBytes[7] = TwoHexCharsToByte(Strings.Mid(sTemp, 13, 2));

                            Value = BitConverter.ToInt64(byDataBytes, 0);

                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);

                            sFeedBackFromSlave = null;
                            //throw new Exception("");
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        // ok
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
                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + ReadDataLength * 8 * 8 > 65535)
                {
                    throw new Exception("读取地址超出范围，正确寻址范围：0~65535(位)");
                }

                //起始位	    设备地址	功能代码	起始地址   	读取数据字长度   LRC校验	   结束符
                //1个字符(:)	2个字符	    2个字符	    4个字符     4个字符	         2个字符	   2个字符(回车+换行)
                //1个字节       1个字节     1个字节     2个字节     2个字节          1个字节     2个字节
                byte[] byResult = MakeReadCmd(DeviceAddress, CModbusFunctionCode.ReadRegister, BeginAddress, Convert.ToUInt16(ReadDataLength * 4));

                string sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)
                {
                    Enqueue( "发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"

                //**********************
                //首字符
                //[0] - :

                //从站地址
                //[1] - 0
                //[2] - 1

                //读取成功的功能码
                //[3] - 0
                //[4] - F

                //读取失败的错误码
                //[3] - 0
                //[4] - 8
                //**********************

                //发送值[0~3]：1560360770 -330733133 -559386401 -358732414 
                //成功执行写命令
                //写操作记录：(COM1)发送字符串 - :0110000000102037425D010000000069B3EC49FFFFFFFF70DFDEA8FFFFFFFF2D82EA9EFFFFFFFF97
                //写操作记录：(COM1)收到字符串 - :011000000010DF

                //成功执行读命令，结果值：1560360770 -330733133 
                //(COM1)发送字符串 - :010300000008F4
                //(COM1)收到字符串 - :01031037425D010000000069B3EC49FFFFFFFFC8

                //发送值[0~3]：-1745165922 1859478028 -204545940 -347596378
                //写操作记录：(COM1)发送字符串 - :01100000001020E19E97FAFFFFFFFF620C6ED500000000E06CF3CEFFFFFFFF19A6EB48FFFFFFFF0B
                //写操作记录：(COM1)收到字符串 - :011000000010DF

                //成功执行读命令，结果值：-1745165922 1859478028 
                //(COM1)发送字符串 - :010300000008F4
                //(COM1)收到字符串 - :010310E19E97FAFFFFFFFF620C6ED5000000002F

                //发送值[0~3]：-1099268831 567276160 1472460616 -1051978640
                //写操作记录：(COM1)发送字符串 - :011000000010207D21BE7AFFFFFFFFF28021CF00000000F74857C3000000001470C14CFFFFFFFFA5
                //写操作记录：(COM1)收到字符串 - :011000000010DF

                //成功执行读命令，结果值：-1099268831 567276160 
                //(COM1)发送字符串 - :010300000008F4
                //(COM1)收到字符串 - :0103107D21BE7AFFFFFFFFF28021CF00000000B8

                //发送值[0~3]：1123495095 1423358440 -2142490831 -1573317120
                //写操作记录：(COM1)发送字符串 - :011000000010202CB742F700000000B9E854D6000000002F31804CFFFFFFFF1600A239FFFFFFFFC3
                //写操作记录：(COM1)收到字符串 - :011000000010DF

                //成功执行读命令，结果值：1123495095 1423358440 
                //(COM1)发送字符串 - :010300000008F4
                //(COM1)收到字符串 - :0103102CB742F700000000B9E854D60000000005
                
                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    //sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        //string sTempResult = BytesToHexStringSplitByChar(byReadData);

                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 3

                        string sCmd = CModbusFunctionCode.ReadRegister.ToString("X2");

                        //if (cByte3 == 0x0.ToString()[0] && cByte4 == CModbusFunctionCode.ReadRegister.ToString()[0])//:0103
                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])
                        {
                            long[] lResultValue = new long[ReadDataLength];

                            for (int i = 0; i < ReadDataLength; i++)
                            {
                                byte[] byDataBytes = new byte[8];

                                //每次截取16个16进制字符，转换为double型数据
                                string sTemp = Strings.Mid(sFeedBackFromSlave, 8 + i * 16, 16);//
                                byDataBytes[0] = TwoHexCharsToByte(Strings.Mid(sTemp, 3, 2));
                                byDataBytes[1] = TwoHexCharsToByte(Strings.Mid(sTemp, 1, 2));
                                byDataBytes[2] = TwoHexCharsToByte(Strings.Mid(sTemp, 7, 2));
                                byDataBytes[3] = TwoHexCharsToByte(Strings.Mid(sTemp, 5, 2));

                                byDataBytes[4] = TwoHexCharsToByte(Strings.Mid(sTemp, 11, 2));
                                byDataBytes[5] = TwoHexCharsToByte(Strings.Mid(sTemp, 9, 2));
                                byDataBytes[6] = TwoHexCharsToByte(Strings.Mid(sTemp, 15, 2));
                                byDataBytes[7] = TwoHexCharsToByte(Strings.Mid(sTemp, 13, 2));

                                lResultValue[i] = BitConverter.ToInt64(byDataBytes, 0);

                                byDataBytes = null;
                            }

                            Value = lResultValue;
                            lResultValue = null;

                            sFeedBackFromSlave = null;
                            byReadData = null;
                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);

                            sFeedBackFromSlave = null;
                            byReadData = null;
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)//null == byReadData || byReadData.Length < 2
                    {
                        //ReadBackData = "";
                        return false;
                    }
                    else
                    {
                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.ReadRegister.ToString("X2"))//"03"
                        {
                            long[] lResultValue = new long[ReadDataLength];

                            for (int i = 0; i < ReadDataLength; i++)
                            {
                                byte[] byDataBytes = new byte[8];

                                //每次截取16个16进制字符，转换为double型数据
                                string sTemp = Strings.Mid(sFeedBackFromSlave, 8 + i * 16, 16);//
                                byDataBytes[0] = TwoHexCharsToByte(Strings.Mid(sTemp, 3, 2));
                                byDataBytes[1] = TwoHexCharsToByte(Strings.Mid(sTemp, 1, 2));
                                byDataBytes[2] = TwoHexCharsToByte(Strings.Mid(sTemp, 7, 2));
                                byDataBytes[3] = TwoHexCharsToByte(Strings.Mid(sTemp, 5, 2));

                                byDataBytes[4] = TwoHexCharsToByte(Strings.Mid(sTemp, 11, 2));
                                byDataBytes[5] = TwoHexCharsToByte(Strings.Mid(sTemp, 9, 2));
                                byDataBytes[6] = TwoHexCharsToByte(Strings.Mid(sTemp, 15, 2));
                                byDataBytes[7] = TwoHexCharsToByte(Strings.Mid(sTemp, 13, 2));

                                lResultValue[i] = BitConverter.ToInt64(byDataBytes, 0);

                                byDataBytes = null;
                            }

                            Value = lResultValue;
                            lResultValue = null;

                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);

                            sFeedBackFromSlave = null;
                            //throw new Exception("");
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                //ReadBackData = "";
                return false;
            }
        }

        #endregion

        #endregion

        #region "写 - ok"

        #region "写单个/多个线圈 - ok"

        // ok
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
                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                //:         地址码         功能码    设置起始地址   设置值      LRC     回车+换行符
                //1字符     2字符          2字符     4字符          4字符      2字符    2字符
                //1字节     1字节          1字节     2字节          2字节      1字节    2字节

                // 求 LRC -- 设备地址(2个字符) + 功能码(2个字符) + 起始地址(4个字符) + 数据值(4个字符)
                string sCmdToCalcLRC = "";
                sCmdToCalcLRC += DeviceAddress.ToString("X2");//设备地址(2个字符)
                sCmdToCalcLRC += CModbusFunctionCode.WriteCoil.ToString("X2");//功能码(2个字符)

                byte[] byBeginAddress = IntToBytes(BeginAddress);//起始地址(4个字符)
                sCmdToCalcLRC += byBeginAddress[1].ToString("X2");// 起始地址 - 高字节
                sCmdToCalcLRC += byBeginAddress[0].ToString("X2");// 起始地址 - 低字节

                // 单个线圈值的设置
                //0x0000	释放继电器线圈
                //0xFF00	吸合继电器线圈
                if (IsOn == true)
                {
                    sCmdToCalcLRC += "FF00";
                }
                else
                {
                    sCmdToCalcLRC += "0000";
                }
                //byte[] byDataLength = IntToHex(1);//数据长度(4个字符)
                //sCmdToCalcLRC += byDataLength[0].ToString("X2");
                //sCmdToCalcLRC += byDataLength[1].ToString("X2");

                byte[] byResult = HexCmdConvertToBytesIncludeLRC(sCmdToCalcLRC);

                sCmdToCalcLRC = null;

                string sCmdDataToBeSent = "";// Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                {
                    sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);
                    Enqueue("发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                byResult = null;

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //(COM1)发送字符串 - :01050000FF00FB
                        //(COM1)收到字符串 - :01850179  -- 写从站线圈失败后，

                        //(COM1)发送字符串 - :01050000FF00FB
                        //(COM1)收到字符串 - :01050000FF00FB  -- 成功写从站后，从站返回的字符串内容与发送的字符串内容相同
                        //if (byReadData[0] == PrefixByte && byReadData[1] == CModbusFunctionCode.WriteCoil)//
                        //{
                        //}

                        //3A 30 31 30 35 30 30 30 30 46 46 30 30 46 42 0D 0A -- 写线圈OK
                        //3A 30 31 38 35 30 36 37 34 0D 0A -- 错误
                        //string sTemp = BytesToHexStringSplitByChar(byReadData);

                        //**********************
                        //首字符
                        //[0] - :

                        //从站地址
                        //[1] - 0
                        //[2] - 1

                        //读取成功的功能码
                        //[3] - 0
                        //[4] - 5

                        //读取失败的错误码
                        //[3] - 0
                        //[4] - 8
                        //**********************

                        #endregion

                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 5

                        //string sByte3 = byReadData[3].ToString("X2");//功能码的第一个字节 - 0
                        //string sByte4 = byReadData[4].ToString("X2");//功能码的第一个字节 - 5

                        string sCmd = CModbusFunctionCode.WriteCoil.ToString("X2");
                        
                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])//:0105
                        //if (sByte3 == 0x0.ToString("X2") && sByte4 == sCmd)//:0105
                        {
                            byReadData = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //(COM1)发送字符串 - :01050000FF00FB
                        //(COM1)收到字符串 - :01850179  -- 写从站线圈失败后，

                        //(COM1)发送字符串 - :01050000FF00FB
                        //(COM1)收到字符串 - :01050000FF00FB  -- 成功写从站后，从站返回的字符串内容与发送的字符串内容相同

                        #endregion

                        //成功写从站后，从站返回的字符串内容与发送的字符串内容相同
                        string sFeedBack = Strings.Mid(sFeedBackFromSlave, 4, 2);
                        string sCmd = CModbusFunctionCode.WriteCoil.ToString("X2");
                        if (sFeedBack == sCmd)//sCmdDataToBeSent == sFeedBackFromSlave)
                        {
                            sCmdDataToBeSent = null;
                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sCmdDataToBeSent = null;
                            sFeedBackFromSlave = null;

                            return false;
                        }
                    }

                    #endregion
                }

                #region "Old codes"

                //byte[] byResult = null;

                //byte[] byDataToBeSent = new byte[6];//[8]

                //byDataToBeSent[0] = DeviceAddress;	//设备地址

                //byDataToBeSent[1] = CModbusFunctionCode.WriteCoil;	//功能号码

                //// 起始地址
                //byte[] byStartAddress = BitConverter.GetBytes(BeginAddress);
                //byDataToBeSent[2] = byStartAddress[1];  // 起始地址 - 高字节
                //byDataToBeSent[3] = byStartAddress[0];  // 起始地址 - 低字节

                //byResult = JoinTwoByteArrays(new byte[] { DeviceAddress }, byStartAddress);

                //byte[] byWriteValue = new byte[2];
                //byWriteValue[0] = Convert.ToByte((IsOn == true) ? 0xFF : 0x00);
                //byWriteValue[1] = 0x00; //低字节
                
                //// 单个线圈值的设置
                ////0x0000	释放继电器线圈
                ////0xFF00	吸合继电器线圈
                //if (IsOn == true)
                //{
                //    byDataToBeSent[4] = 0xFF; //高字节 - 吸合继电器线圈
                //}
                //else
                //{
                //    byDataToBeSent[4] = 0x00; //高字节 - 释放继电器线圈
                //}

                //byDataToBeSent[5] = 0x00; //低字节

                ////LRC校验结果值
                //byte[] byCRCResults = CLRC.LRC(byDataToBeSent);
                //Array.Resize<byte>(ref byDataToBeSent, 8);
                //byDataToBeSent[6] = byCRCResults[1];
                //byDataToBeSent[7] = byCRCResults[0];

                //// 通过串口发送指令数据给客户端
                //RS232CPort.Write(byDataToBeSent, 0, byDataToBeSent.Length);

                //// 读取客户端的返回信息
                //Stopwatch swUsedTime = new Stopwatch();
                //swUsedTime.Restart();

                //while (RS232CPort.BytesToRead < 2)
                //{
                //    if (swUsedTime.ElapsedMilliseconds >= SleepTime)
                //    {
                //        break;
                //    }

                //    Application.DoEvents();
                //    System.Threading.Thread.Sleep(iSleepTime);
                //}

                //byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                //if (null == byReadData || byReadData.Length < 2)
                //{
                //    return false;
                //}
                //else
                //{
                //    if (byReadData[0] == CModbusFunctionCode.WriteCoil)
                //    {

                //    }
                //}

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

            //return true;
        }

        // ok
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

                //if (SetValue.Length > 1968)
                //{
                //    throw new Exception("设置值bool[]数组的长度超出范围(1~1968)，请修改参数数组的长度");
                //}

                if (BeginAddress + SetValue.Length > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //if (BeginAddress + SetValue.Length > 65535)
                //{
                //    throw new Exception("起始地址加bool[]数组的长度超出寻址范围，请修改起始地址或参数数组的长度");
                //}

                //:         地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      LRC      回车+换行符
                //1字符     2字符          2字符     4字符          4字符      2字符      2*N字符      1字符    2字符
                //1字节     1字节          1字节     2字节          2字节      1字节      N字节        1字节    2字节
                
                // 求 LRC -- 设备地址(1字节)   功能码(1字节)    设置起始地址(2字节)   数据位长度(2字节)   字节计数(1字节)   设置内容(N字节)
                string sCmdToCalcLRC = "";
                sCmdToCalcLRC += DeviceAddress.ToString("X2");//设备地址(2个字符)
                sCmdToCalcLRC += CModbusFunctionCode.WriteMultiCoil.ToString("X2");//功能码(2个字符)

                byte[] byBeginAddress = IntToBytes(BeginAddress);//起始地址(4个字符)
                sCmdToCalcLRC += byBeginAddress[1].ToString("X2");// 起始地址 - 高字节
                sCmdToCalcLRC += byBeginAddress[0].ToString("X2");// 起始地址 - 低字节
                
                //数据位长度 - 位的数据长度
                int iDataLength = SetValue.Length;
                byte[] byDataLength = IntToBytes(iDataLength);
                sCmdToCalcLRC += byDataLength[1].ToString("X2");// 数据长度 - 高字节
                sCmdToCalcLRC += byDataLength[0].ToString("X2");// 数据长度 - 低字节

                // 字节计数
                byte byByteCount = Convert.ToByte(iDataLength / 8);//bool[]数组长度 / 8 = 字节数
                sCmdToCalcLRC += byByteCount.ToString("X2");

                // 线圈值的设置 - 将布尔数组转换为字节数组
                byte[] byBitArrayToBytes = BitArrayToByte(SetValue);
                for (int i = 0; i < byBitArrayToBytes.Length; i++)
                {
                    sCmdToCalcLRC += byBitArrayToBytes[i].ToString("X2");
                }

                //byte[] byDataLength = IntToHex(1);//数据长度(4个字符)
                //sCmdToCalcLRC += byDataLength[0].ToString("X2");
                //sCmdToCalcLRC += byDataLength[1].ToString("X2");

                byte[] byResult = HexCmdConvertToBytesIncludeLRC(sCmdToCalcLRC);

                sCmdToCalcLRC = null;

                string sCmdDataToBeSent = "";// Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                {
                    sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);
                    Enqueue("发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                byResult = null;

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //(COM1)发送字符串 - :01050000FF00FB
                        //(COM1)收到字符串 - :01850179  -- 写从站线圈失败后，

                        //(COM1)发送字符串 - :01050000FF00FB
                        //(COM1)收到字符串 - :01050000FF00FB  -- 成功写从站后，从站返回的字符串内容与发送的字符串内容相同
                        //if (byReadData[0] == PrefixByte && byReadData[1] == CModbusFunctionCode.WriteCoil)//
                        //{
                        //}

                        //3A 30 31 30 35 30 30 30 30 46 46 30 30 46 42 0D 0A -- 写线圈OK
                        //3A 30 31 38 35 30 36 37 34 0D 0A -- 错误
                        //string sTemp = BytesToHexStringSplitByChar(byReadData);

                        //发送值[0~7]：True False True False True False False False 
                        //写操作记录：(COM1)发送字符串 - :010F000000080815CB
                        //写操作记录：(COM1)收到字符串 - :018F036D

                        //发送值[0~7]：False True True True False False True False
                        //写操作记录：(COM1)发送字符串 - :010F00000008014E99
                        //写操作记录：(COM1)收到字符串 - :010F00000008E8

                        //发送值[0~7]：True False False True False True False False
                        //写操作记录：(COM1)发送字符串 - :010F000000080129BE
                        //写操作记录：(COM1)收到字符串 - :010F00000008E8

                        //发送值[0~7]：True True True False True False True False
                        //写操作记录：(COM1)发送字符串 - :010F00000008015790
                        //写操作记录：(COM1)收到字符串 - :010F00000008E8

                        //发送值[0~7]：False False True False False True True False
                        //写操作记录：(COM1)发送字符串 - :010F00000008016483
                        //写操作记录：(COM1)收到字符串 - :010F00000008E8
                        
                        //3A 30 31 30 46 30 30 30 30 30 30 30 31 45 46 0D 0A
                        // :018F036D\r\n

                        //**********************
                        //首字符
                        //[0] - :

                        //从站地址
                        //[1] - 0
                        //[2] - 1

                        //读取成功的功能码
                        //[3] - 0
                        //[4] - 5

                        //读取失败的错误码
                        //[3] - 0
                        //[4] - 8
                        //**********************

                        #endregion

                        //string sTemp = BytesToHexStringSplitByChar(byReadData);
                        
                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 0F

                        string sCmd = CModbusFunctionCode.WriteMultiCoil.ToString("X2");

                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])//CModbusFunctionCode.WriteMultiCoil.ToString()[0]       :010F
                        {
                            byReadData = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        // *** 写从站多个线圈失败后，

                        //发送值[0~7]：True False True False True False False False
                        //写操作记录：(COM1)发送字符串 - :010F000000080815CB
                        //写操作记录：(COM1)收到字符串 - :018F036D

                        //***************************
                        // *** 成功写从站多个线圈后，从站返回的字符串内容与发送的字符串内容，前面 13 个字符相同

                        //发送值[0~7]：False False True False False True True False
                        //写操作记录：(COM1)发送字符串 - :010F00000008016483
                        //写操作记录：(COM1)收到字符串 - :010F00000008E8

                        //发送值[0~15]：False True True True False False False False True True False True True False False True
                        //写操作记录：(COM1)发送字符串 - :010F00000010020E9B35
                        //写操作记录：(COM1)收到字符串 - :010F00000010E0

                        //发送值[0~15]：False True True False True True True True False False False False True True True False
                        //写操作记录：(COM1)发送字符串 - :010F0000001002F67078
                        //写操作记录：(COM1)收到字符串 - :010F00000010E0

                        //发送值[0~15]：True True False True False False True False False False False True False True False False
                        //写操作记录：(COM1)发送字符串 - :010F00000010024B286B
                        //写操作记录：(COM1)收到字符串 - :010F00000010E0

                        #endregion

                        //string sFront13CharsOfSentCmd = Strings.Mid(sCmdDataToBeSent, 1, 13);

                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.WriteMultiCoil.ToString("X2"))//sFeedBackFromSlave[3] == '0' && sFeedBackFromSlave[4] == 'F'
                        {
                            sCmdDataToBeSent = null;
                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sCmdDataToBeSent = null;
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        // ok 
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
                
                //:         地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      LRC      回车+换行符
                //1字符     2字符          2字符     4字符          4字符      2字符      2*N字符      1字符    2字符
                //1字节     1字节          1字节     2字节          2字节      1字节      N字节        1字节    2字节
                
                // 求 LRC -- 设备地址(1字节)   功能码(1字节)    设置起始地址(2字节)   数据位长度(2字节)   字节计数(1字节)   设置内容(N字节)
                string sCmdToCalcLRC = "";
                sCmdToCalcLRC += DeviceAddress.ToString("X2");//设备地址(2个字符)
                sCmdToCalcLRC += CModbusFunctionCode.WriteMultiCoil.ToString("X2");//功能码(2个字符)

                byte[] byBeginAddress = IntToBytes(BeginAddress);//起始地址(4个字符)
                sCmdToCalcLRC += byBeginAddress[1].ToString("X2");// 起始地址 - 高字节
                sCmdToCalcLRC += byBeginAddress[0].ToString("X2");// 起始地址 - 低字节

                //数据位长度 - 位的数据长度
                int iDataLength = 1 * 8;
                byte[] byDataLength = IntToBytes(iDataLength);
                sCmdToCalcLRC += byDataLength[1].ToString("X2");// 数据长度 - 高字节
                sCmdToCalcLRC += byDataLength[0].ToString("X2");// 数据长度 - 低字节

                // 字节计数
                byte byByteCount = Convert.ToByte(iDataLength / 8);//1 - 字节数
                sCmdToCalcLRC += byByteCount.ToString("X2");

                // 线圈值的设置
                sCmdToCalcLRC += SetValue.ToString("X2");

                //byte[] byDataLength = IntToHex(1);//数据长度(4个字符)
                //sCmdToCalcLRC += byDataLength[0].ToString("X2");
                //sCmdToCalcLRC += byDataLength[1].ToString("X2");

                byte[] byResult = HexCmdConvertToBytesIncludeLRC(sCmdToCalcLRC);

                sCmdToCalcLRC = null;

                string sCmdDataToBeSent = "";// Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                {
                    sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);
                    Enqueue("发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                byResult = null;

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"

                //成功执行写命令192
                //写操作记录：(COM1)发送字符串 - :010F0000000801C027
                //写操作记录：(COM1)收到字符串 - :010F00000008E8

                //成功执行读命令，结果值：192
                //(COM1)发送字符串 - :010100000008F6
                //(COM1)收到字符串 - :010101C03D
                
                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //(COM1)发送字符串 - :01050000FF00FB
                        //(COM1)收到字符串 - :01850179  -- 写从站线圈失败后，

                        //(COM1)发送字符串 - :01050000FF00FB
                        //(COM1)收到字符串 - :01050000FF00FB  -- 成功写从站后，从站返回的字符串内容与发送的字符串内容相同
                        //if (byReadData[0] == PrefixByte && byReadData[1] == CModbusFunctionCode.WriteCoil)//
                        //{
                        //}

                        //3A 30 31 30 35 30 30 30 30 46 46 30 30 46 42 0D 0A -- 写线圈OK
                        //3A 30 31 38 35 30 36 37 34 0D 0A -- 错误
                        //string sTemp = BytesToHexStringSplitByChar(byReadData);

                        //发送值[0~7]：True False True False True False False False 
                        //写操作记录：(COM1)发送字符串 - :010F000000080815CB
                        //写操作记录：(COM1)收到字符串 - :018F036D

                        //发送值[0~7]：False True True True False False True False
                        //写操作记录：(COM1)发送字符串 - :010F00000008014E99
                        //写操作记录：(COM1)收到字符串 - :010F00000008E8

                        //发送值[0~7]：True False False True False True False False
                        //写操作记录：(COM1)发送字符串 - :010F000000080129BE
                        //写操作记录：(COM1)收到字符串 - :010F00000008E8

                        //发送值[0~7]：True True True False True False True False
                        //写操作记录：(COM1)发送字符串 - :010F00000008015790
                        //写操作记录：(COM1)收到字符串 - :010F00000008E8

                        //发送值[0~7]：False False True False False True True False
                        //写操作记录：(COM1)发送字符串 - :010F00000008016483
                        //写操作记录：(COM1)收到字符串 - :010F00000008E8
                        
                        //3A 30 31 30 46 30 30 30 30 30 30 30 31 45 46 0D 0A
                        // :018F036D\r\n

                        //**********************
                        //首字符
                        //[0] - :

                        //从站地址
                        //[1] - 0
                        //[2] - 1

                        //读取成功的功能码
                        //[3] - 0
                        //[4] - 5

                        //读取失败的错误码
                        //[3] - 0
                        //[4] - 8
                        //**********************

                        #endregion

                        //string sTemp = BytesToHexStringSplitByChar(byReadData);
                        
                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 0F

                        string sCmd = CModbusFunctionCode.WriteMultiCoil.ToString("X2");

                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])//CModbusFunctionCode.WriteMultiCoil.ToString()[0]       :010F
                        {
                            byReadData = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        // *** 写从站多个线圈失败后
                        //写操作记录：(COM1)收到字符串 - :018F036D

                        // *** 成功写从站多个线圈后，从站返回的字符串内容与发送的字符串内容，前面 13 个字符相同

                        //string sFront13CharsOfSentCmd = Strings.Mid(sCmdDataToBeSent, 1, 13);

                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.WriteMultiCoil.ToString("X2"))//sFeedBackFromSlave[3] == '0' && sFeedBackFromSlave[4] == 'F'
                        {
                            sCmdDataToBeSent = null;
                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sCmdDataToBeSent = null;
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        // ok 
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

                //if (SetValue.Length * 8 > 1968)
                //{
                //    throw new Exception("设置值byte[]数组的长度超出范围(1~246)，请修改参数数组的长度");
                //}

                if (BeginAddress + SetValue.Length * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //:         地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      LRC      回车+换行符
                //1字符     2字符          2字符     4字符          4字符      2字符      2*N字符      1字符    2字符
                //1字节     1字节          1字节     2字节          2字节      1字节      N字节        1字节    2字节
                
                // 求 LRC -- 设备地址(1字节)   功能码(1字节)    设置起始地址(2字节)   数据位长度(2字节)   字节计数(1字节)   设置内容(N字节)
                string sCmdToCalcLRC = "";
                sCmdToCalcLRC += DeviceAddress.ToString("X2");//设备地址(2个字符)
                sCmdToCalcLRC += CModbusFunctionCode.WriteMultiCoil.ToString("X2");//功能码(2个字符)

                byte[] byBeginAddress = IntToBytes(BeginAddress);//起始地址(4个字符)
                sCmdToCalcLRC += byBeginAddress[1].ToString("X2");// 起始地址 - 高字节
                sCmdToCalcLRC += byBeginAddress[0].ToString("X2");// 起始地址 - 低字节

                //数据位长度 - 位的数据长度
                int iDataLength = SetValue.Length * 8;
                byte[] byDataLength = IntToBytes(iDataLength);
                sCmdToCalcLRC += byDataLength[1].ToString("X2");// 数据长度 - 高字节
                sCmdToCalcLRC += byDataLength[0].ToString("X2");// 数据长度 - 低字节

                // 字节计数
                byte byByteCount = Convert.ToByte(iDataLength / 8);//N - 字节数
                sCmdToCalcLRC += byByteCount.ToString("X2");

                // 线圈值的设置
                for (int i = 0; i < SetValue.Length; i++)
                {
                    sCmdToCalcLRC += SetValue[i].ToString("X2");
                }
                
                byte[] byResult = HexCmdConvertToBytesIncludeLRC(sCmdToCalcLRC);

                sCmdToCalcLRC = null;

                string sCmdDataToBeSent = "";// Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                {
                    sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);
                    Enqueue("发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                byResult = null;

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"

                //发送值[0~1]：215 33 
                //成功执行写命令
                //写操作记录：(COM1)发送字符串 - :010F0000001002D721E6
                //写操作记录：(COM1)收到字符串 - :010F00000010E0

                //成功执行读命令，结果值：215 33 
                //(COM1)发送字符串 - :010100000010EE
                //(COM1)收到字符串 - :010102D72104

                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //(COM1)发送字符串 - :01050000FF00FB
                        //(COM1)收到字符串 - :01850179  -- 写从站线圈失败后，

                        //(COM1)发送字符串 - :01050000FF00FB
                        //(COM1)收到字符串 - :01050000FF00FB  -- 成功写从站后，从站返回的字符串内容与发送的字符串内容相同
                        //if (byReadData[0] == PrefixByte && byReadData[1] == CModbusFunctionCode.WriteCoil)//
                        //{
                        //}

                        //3A 30 31 30 35 30 30 30 30 46 46 30 30 46 42 0D 0A -- 写线圈OK
                        //3A 30 31 38 35 30 36 37 34 0D 0A -- 错误
                        //string sTemp = BytesToHexStringSplitByChar(byReadData);

                        //发送值[0~7]：True False True False True False False False 
                        //写操作记录：(COM1)发送字符串 - :010F000000080815CB
                        //写操作记录：(COM1)收到字符串 - :018F036D

                        //发送值[0~7]：False True True True False False True False
                        //写操作记录：(COM1)发送字符串 - :010F00000008014E99
                        //写操作记录：(COM1)收到字符串 - :010F00000008E8

                        //发送值[0~7]：True False False True False True False False
                        //写操作记录：(COM1)发送字符串 - :010F000000080129BE
                        //写操作记录：(COM1)收到字符串 - :010F00000008E8

                        //发送值[0~7]：True True True False True False True False
                        //写操作记录：(COM1)发送字符串 - :010F00000008015790
                        //写操作记录：(COM1)收到字符串 - :010F00000008E8

                        //发送值[0~7]：False False True False False True True False
                        //写操作记录：(COM1)发送字符串 - :010F00000008016483
                        //写操作记录：(COM1)收到字符串 - :010F00000008E8
                        
                        //3A 30 31 30 46 30 30 30 30 30 30 30 31 45 46 0D 0A
                        // :018F036D\r\n

                        //**********************
                        //首字符
                        //[0] - :

                        //从站地址
                        //[1] - 0
                        //[2] - 1

                        //读取成功的功能码
                        //[3] - 0
                        //[4] - 5

                        //读取失败的错误码
                        //[3] - 0
                        //[4] - 8
                        //**********************

                        #endregion

                        //string sTemp = BytesToHexStringSplitByChar(byReadData);
                        
                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 0F

                        string sCmd = CModbusFunctionCode.WriteMultiCoil.ToString("X2");

                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])//CModbusFunctionCode.WriteMultiCoil.ToString()[0]       :010F
                        {
                            byReadData = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        // *** 写从站多个线圈失败后
                        //写操作记录：(COM1)收到字符串 - :018F036D

                        // *** 成功写从站多个线圈后，从站返回的字符串内容与发送的字符串内容，前面 13 个字符相同

                        //string sFront13CharsOfSentCmd = Strings.Mid(sCmdDataToBeSent, 1, 13);

                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.WriteMultiCoil.ToString("X2"))//sFeedBackFromSlave[3] == '0' && sFeedBackFromSlave[4] == 'F'
                        {
                            sCmdDataToBeSent = null;
                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sCmdDataToBeSent = null;
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        // ok 
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

                //:         地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      LRC      回车+换行符
                //1字符     2字符          2字符     4字符          4字符      2字符      2*N字符      1字符    2字符
                //1字节     1字节          1字节     2字节          2字节      1字节      N字节        1字节    2字节
                
                // 求 LRC -- 设备地址(1字节)   功能码(1字节)    设置起始地址(2字节)   数据位长度(2字节)   字节计数(1字节)   设置内容(N字节)
                string sCmdToCalcLRC = "";
                sCmdToCalcLRC += DeviceAddress.ToString("X2");//设备地址(2个字符)
                sCmdToCalcLRC += CModbusFunctionCode.WriteMultiCoil.ToString("X2");//功能码(2个字符)

                byte[] byBeginAddress = IntToBytes(BeginAddress);//起始地址(4个字符)
                sCmdToCalcLRC += byBeginAddress[1].ToString("X2");// 起始地址 - 高字节
                sCmdToCalcLRC += byBeginAddress[0].ToString("X2");// 起始地址 - 低字节

                //数据位长度 - 位的数据长度
                int iDataLength = 2 * 8;
                byte[] byDataLength = IntToBytes(iDataLength);
                sCmdToCalcLRC += byDataLength[1].ToString("X2");// 数据长度 - 高字节
                sCmdToCalcLRC += byDataLength[0].ToString("X2");// 数据长度 - 低字节

                // 字节计数
                byte byByteCount = 2;// Convert.ToByte(iDataLength / 8);//2 - 字节数
                sCmdToCalcLRC += byByteCount.ToString("X2");

                // 线圈值的设置
                byte[] byTemp = BitConverter.GetBytes(SetValue);
                sCmdToCalcLRC += byTemp[0].ToString("X2");// 数据设置值 - 高字节
                sCmdToCalcLRC += byTemp[1].ToString("X2");// 数据设置值 - 低字节

                //byte[] byDataLength = IntToHex(1);//数据长度(4个字符)
                //sCmdToCalcLRC += byDataLength[0].ToString("X2");
                //sCmdToCalcLRC += byDataLength[1].ToString("X2");

                byte[] byResult = HexCmdConvertToBytesIncludeLRC(sCmdToCalcLRC);

                sCmdToCalcLRC = null;

                string sCmdDataToBeSent = "";// Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                {
                    sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);
                    Enqueue("发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                byResult = null;

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"

                //成功执行写命令12320
                //写操作记录：(COM1)发送字符串 - :010F000000100220308E
                //写操作记录：(COM1)收到字符串 - :010F00000010E0

                //成功执行读命令，结果值：12320
                //(COM1)发送字符串 - :010100000010EE
                //(COM1)收到字符串 - :0101022030AC

                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //(COM1)发送字符串 - :01050000FF00FB
                        //(COM1)收到字符串 - :01850179  -- 写从站线圈失败后，

                        //(COM1)收到字符串 - :01050000FF00FB  -- 成功写从站后，从站返回的字符串内容与发送的字符串内容相同
                        
                        //3A 30 31 30 46 30 30 30 30 30 30 30 31 45 46 0D 0A
                        // :018F036D\r\n

                        //**********************
                        //首字符
                        //[0] - :

                        //从站地址
                        //[1] - 0
                        //[2] - 1

                        //读取成功的功能码
                        //[3] - 0
                        //[4] - 5

                        //读取失败的错误码
                        //[3] - 0
                        //[4] - 8
                        //**********************

                        #endregion

                        //string sTemp = BytesToHexStringSplitByChar(byReadData);
                        
                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 0F

                        string sCmd = CModbusFunctionCode.WriteMultiCoil.ToString("X2");

                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])//CModbusFunctionCode.WriteMultiCoil.ToString()[0]       :010F
                        {
                            byReadData = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        // *** 写从站多个线圈失败后
                        //写操作记录：(COM1)收到字符串 - :018F036D

                        // *** 成功写从站多个线圈后，从站返回的字符串内容与发送的字符串内容，前面 13 个字符相同

                        //string sFront13CharsOfSentCmd = Strings.Mid(sCmdDataToBeSent, 1, 13);

                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.WriteMultiCoil.ToString("X2"))
                            //sFeedBackFromSlave[3] == '0' && sFeedBackFromSlave[4] == 'F'
                        {
                            sCmdDataToBeSent = null;
                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sCmdDataToBeSent = null;
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        // ok 
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

                //if (SetValue.Length * 2 * 8 > 1968)
                //{
                //    throw new Exception("设置值byte[]数组的长度超出范围(1~123)，请修改参数数组的长度");
                //}

                if (BeginAddress + SetValue.Length * 2 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //:         地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      LRC      回车+换行符
                //1字符     2字符          2字符     4字符          4字符      2字符      2*N字符      1字符    2字符
                //1字节     1字节          1字节     2字节          2字节      1字节      N字节        1字节    2字节
                
                // 求 LRC -- 设备地址(1字节)   功能码(1字节)    设置起始地址(2字节)   数据位长度(2字节)   字节计数(1字节)   设置内容(N字节)
                string sCmdToCalcLRC = "";
                sCmdToCalcLRC += DeviceAddress.ToString("X2");//设备地址(2个字符)
                sCmdToCalcLRC += CModbusFunctionCode.WriteMultiCoil.ToString("X2");//功能码(2个字符)

                byte[] byBeginAddress = IntToBytes(BeginAddress);//起始地址(4个字符)
                sCmdToCalcLRC += byBeginAddress[1].ToString("X2");// 起始地址 - 高字节
                sCmdToCalcLRC += byBeginAddress[0].ToString("X2");// 起始地址 - 低字节

                //数据位长度 - 位的数据长度
                int iDataLength = SetValue.Length * 2 * 8;
                byte[] byDataLength = IntToBytes(iDataLength);
                sCmdToCalcLRC += byDataLength[1].ToString("X2");// 数据长度 - 高字节
                sCmdToCalcLRC += byDataLength[0].ToString("X2");// 数据长度 - 低字节

                // 字节计数
                byte byByteCount = Convert.ToByte(SetValue.Length * 2);//N - 字节数
                sCmdToCalcLRC += byByteCount.ToString("X2");

                // 线圈值的设置
                for (int i = 0; i < SetValue.Length; i++)
                {
                    //sCmdToCalcLRC += SetValue[i].ToString("X4");

                    byte[] byTemp = BitConverter.GetBytes(SetValue[i]);
                    sCmdToCalcLRC += byTemp[0].ToString("X2");// 数据设置值 - 高字节
                    sCmdToCalcLRC += byTemp[1].ToString("X2");// 数据设置值 - 低字节
                }
                
                byte[] byResult = HexCmdConvertToBytesIncludeLRC(sCmdToCalcLRC);

                sCmdToCalcLRC = null;

                string sCmdDataToBeSent = "";// Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                {
                    sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);
                    Enqueue("发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                byResult = null;

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"
                
                //发送值[0~1]：-455 -32647 
                //成功执行写命令
                //写操作记录：(COM1)发送字符串 - :010F000000200439FE79809C
                //写操作记录：(COM1)收到字符串 - :010F00000020D0

                //成功执行读命令，结果值：-455 -32647 
                //(COM1)发送字符串 - :010100000020DE
                //(COM1)收到字符串 - :01010439FE7980CA

                //发送值[0~1]：-4316 24771
                //写操作记录：(COM1)发送字符串 - :010F000000200424EFC36096
                //写操作记录：(COM1)收到字符串 - :010F00000020D0

                //成功执行读命令，结果值：-4316 24771 
                //(COM1)发送字符串 - :010100000020DE
                //(COM1)收到字符串 - :01010424EFC360C4

                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //(COM1)发送字符串 - :01050000FF00FB
                        //(COM1)收到字符串 - :01850179  -- 写从站线圈失败后，

                        //(COM1)发送字符串 - :01050000FF00FB
                        //(COM1)收到字符串 - :01050000FF00FB  -- 成功写从站后，从站返回的字符串内容与发送的字符串内容相同
                        
                        //3A 30 31 30 46 30 30 30 30 30 30 30 31 45 46 0D 0A
                        // :018F036D\r\n

                        //**********************
                        //首字符
                        //[0] - :

                        //从站地址
                        //[1] - 0
                        //[2] - 1

                        //读取成功的功能码
                        //[3] - 0
                        //[4] - 5

                        //读取失败的错误码
                        //[3] - 0
                        //[4] - 8
                        //**********************

                        #endregion

                        //string sTemp = BytesToHexStringSplitByChar(byReadData);
                        
                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 0F

                        string sCmd = CModbusFunctionCode.WriteMultiCoil.ToString("X2");

                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])//CModbusFunctionCode.WriteMultiCoil.ToString()[0]       :010F
                        {
                            byReadData = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        // *** 写从站多个线圈失败后
                        //写操作记录：(COM1)收到字符串 - :018F036D

                        // *** 成功写从站多个线圈后，从站返回的字符串内容与发送的字符串内容，前面 13 个字符相同

                        //string sFront13CharsOfSentCmd = Strings.Mid(sCmdDataToBeSent, 1, 13);

                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.WriteMultiCoil.ToString("X2"))//sFeedBackFromSlave[3] == '0' && sFeedBackFromSlave[4] == 'F'
                        {
                            sCmdDataToBeSent = null;
                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sCmdDataToBeSent = null;
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        // ok
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

                //:         地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      LRC      回车+换行符
                //1字符     2字符          2字符     4字符          4字符      2字符      2*N字符      1字符    2字符
                //1字节     1字节          1字节     2字节          2字节      1字节      N字节        1字节    2字节
                
                // 求 LRC -- 设备地址(1字节)   功能码(1字节)    设置起始地址(2字节)   数据位长度(2字节)   字节计数(1字节)   设置内容(N字节)
                string sCmdToCalcLRC = "";
                sCmdToCalcLRC += DeviceAddress.ToString("X2");//设备地址(2个字符)
                sCmdToCalcLRC += CModbusFunctionCode.WriteMultiCoil.ToString("X2");//功能码(2个字符)

                byte[] byBeginAddress = IntToBytes(BeginAddress);//起始地址(4个字符)
                sCmdToCalcLRC += byBeginAddress[1].ToString("X2");// 起始地址 - 高字节
                sCmdToCalcLRC += byBeginAddress[0].ToString("X2");// 起始地址 - 低字节

                //数据位长度 - 位的数据长度
                int iDataLength = 4 * 8;
                byte[] byDataLength = IntToBytes(iDataLength);
                sCmdToCalcLRC += byDataLength[1].ToString("X2");// 数据长度 - 高字节
                sCmdToCalcLRC += byDataLength[0].ToString("X2");// 数据长度 - 低字节

                // 字节计数
                byte byByteCount = 4;// Convert.ToByte(iDataLength / 8);//4 - 字节数
                sCmdToCalcLRC += byByteCount.ToString("X2");

                // 线圈值的设置
                byte[] byTemp = BitConverter.GetBytes(SetValue);
                sCmdToCalcLRC += byTemp[0].ToString("X2");// 数据设置值 - 高字节
                sCmdToCalcLRC += byTemp[1].ToString("X2");// 数据设置值 - 低字节
                sCmdToCalcLRC += byTemp[2].ToString("X2");// 数据设置值 - 高字节
                sCmdToCalcLRC += byTemp[3].ToString("X2");// 数据设置值 - 低字节

                //byte[] byDataLength = IntToHex(1);//数据长度(4个字符)
                //sCmdToCalcLRC += byDataLength[0].ToString("X2");
                //sCmdToCalcLRC += byDataLength[1].ToString("X2");

                byte[] byResult = HexCmdConvertToBytesIncludeLRC(sCmdToCalcLRC);

                sCmdToCalcLRC = null;

                string sCmdDataToBeSent = "";// Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                {
                    sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);
                    Enqueue("发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                byResult = null;

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"



                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"
                        
                        //**********************
                        //首字符
                        //[0] - :

                        //从站地址
                        //[1] - 0
                        //[2] - 1

                        //读取成功的功能码
                        //[3] - 0
                        //[4] - 5

                        //读取失败的错误码
                        //[3] - 0
                        //[4] - 8
                        //**********************

                        #endregion

                        //string sTemp = BytesToHexStringSplitByChar(byReadData);
                        
                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 0F

                        string sCmd = CModbusFunctionCode.WriteMultiCoil.ToString("X2");

                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])//CModbusFunctionCode.WriteMultiCoil.ToString()[0]       :010F
                        {
                            byReadData = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        // *** 写从站多个线圈失败后
                        //写操作记录：(COM1)收到字符串 - :018F036D

                        // *** 成功写从站多个线圈后，从站返回的字符串内容与发送的字符串内容，前面 13 个字符相同

                        //string sFront13CharsOfSentCmd = Strings.Mid(sCmdDataToBeSent, 1, 13);

                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.WriteMultiCoil.ToString("X2"))
                            //sFeedBackFromSlave[3] == '0' && sFeedBackFromSlave[4] == 'F'
                        {
                            sCmdDataToBeSent = null;
                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sCmdDataToBeSent = null;
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        // ok
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

                //if (SetValue.Length * 4 * 8 > 1968)
                //{
                //    throw new Exception("设置值byte[]数组的长度超出范围(1~123)，请修改参数数组的长度");
                //}

                if (BeginAddress + SetValue.Length * 4 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //:         地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      LRC      回车+换行符
                //1字符     2字符          2字符     4字符          4字符      2字符      2*N字符      1字符    2字符
                //1字节     1字节          1字节     2字节          2字节      1字节      N字节        1字节    2字节
                
                // 求 LRC -- 设备地址(1字节)   功能码(1字节)    设置起始地址(2字节)   数据位长度(2字节)   字节计数(1字节)   设置内容(N字节)
                string sCmdToCalcLRC = "";
                sCmdToCalcLRC += DeviceAddress.ToString("X2");//设备地址(2个字符)
                sCmdToCalcLRC += CModbusFunctionCode.WriteMultiCoil.ToString("X2");//功能码(2个字符)

                byte[] byBeginAddress = IntToBytes(BeginAddress);//起始地址(4个字符)
                sCmdToCalcLRC += byBeginAddress[1].ToString("X2");// 起始地址 - 高字节
                sCmdToCalcLRC += byBeginAddress[0].ToString("X2");// 起始地址 - 低字节

                //数据位长度 - 位的数据长度
                int iDataLength = SetValue.Length * 4 * 8;
                byte[] byDataLength = IntToBytes(iDataLength);
                sCmdToCalcLRC += byDataLength[1].ToString("X2");// 数据长度 - 高字节
                sCmdToCalcLRC += byDataLength[0].ToString("X2");// 数据长度 - 低字节

                // 字节计数
                byte byByteCount = Convert.ToByte(SetValue.Length * 4);//N - 字节数
                sCmdToCalcLRC += byByteCount.ToString("X2");

                // 线圈值的设置
                for (int i = 0; i < SetValue.Length; i++)
                {
                    //sCmdToCalcLRC += SetValue[i].ToString("X4");

                    byte[] byTemp = BitConverter.GetBytes(SetValue[i]);
                    sCmdToCalcLRC += byTemp[0].ToString("X2");// 数据设置值 - 高字节
                    sCmdToCalcLRC += byTemp[1].ToString("X2");// 数据设置值 - 低字节
                    sCmdToCalcLRC += byTemp[2].ToString("X2");// 数据设置值 - 高字节
                    sCmdToCalcLRC += byTemp[3].ToString("X2");// 数据设置值 - 低字节
                }
                
                byte[] byResult = HexCmdConvertToBytesIncludeLRC(sCmdToCalcLRC);

                sCmdToCalcLRC = null;

                string sCmdDataToBeSent = "";// Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                {
                    sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);
                    Enqueue("发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                byResult = null;

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"
                
                

                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        

                        //**********************
                        //首字符
                        //[0] - :

                        //从站地址
                        //[1] - 0
                        //[2] - 1

                        //读取成功的功能码
                        //[3] - 0
                        //[4] - 5

                        //读取失败的错误码
                        //[3] - 0
                        //[4] - 8
                        //**********************

                        #endregion

                        //string sTemp = BytesToHexStringSplitByChar(byReadData);
                        
                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 0F

                        string sCmd = CModbusFunctionCode.WriteMultiCoil.ToString("X2");

                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])//CModbusFunctionCode.WriteMultiCoil.ToString()[0]       :010F
                        {
                            byReadData = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        // *** 写从站多个线圈失败后
                        //写操作记录：(COM1)收到字符串 - :018F036D

                        // *** 成功写从站多个线圈后，从站返回的字符串内容与发送的字符串内容，前面 13 个字符相同

                        //string sFront13CharsOfSentCmd = Strings.Mid(sCmdDataToBeSent, 1, 13);

                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.WriteMultiCoil.ToString("X2"))//sFeedBackFromSlave[3] == '0' && sFeedBackFromSlave[4] == 'F'
                        {
                            sCmdDataToBeSent = null;
                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sCmdDataToBeSent = null;
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        // ok
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

                //:         地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      LRC      回车+换行符
                //1字符     2字符          2字符     4字符          4字符      2字符      2*N字符      1字符    2字符
                //1字节     1字节          1字节     2字节          2字节      1字节      N字节        1字节    2字节
                
                // 求 LRC -- 设备地址(1字节)   功能码(1字节)    设置起始地址(2字节)   数据位长度(2字节)   字节计数(1字节)   设置内容(N字节)
                string sCmdToCalcLRC = "";
                sCmdToCalcLRC += DeviceAddress.ToString("X2");//设备地址(2个字符)
                sCmdToCalcLRC += CModbusFunctionCode.WriteMultiCoil.ToString("X2");//功能码(2个字符)

                byte[] byBeginAddress = IntToBytes(BeginAddress);//起始地址(4个字符)
                sCmdToCalcLRC += byBeginAddress[1].ToString("X2");// 起始地址 - 高字节
                sCmdToCalcLRC += byBeginAddress[0].ToString("X2");// 起始地址 - 低字节

                //数据位长度 - 位的数据长度
                int iDataLength = 8 * 8;
                byte[] byDataLength = IntToBytes(iDataLength);
                sCmdToCalcLRC += byDataLength[1].ToString("X2");// 数据长度 - 高字节
                sCmdToCalcLRC += byDataLength[0].ToString("X2");// 数据长度 - 低字节

                // 字节计数
                byte byByteCount = 8;//8 - 字节数
                sCmdToCalcLRC += byByteCount.ToString("X2");

                // 线圈值的设置
                byte[] byTemp = BitConverter.GetBytes(SetValue);
                sCmdToCalcLRC += byTemp[0].ToString("X2");// 数据设置值 - 高字节
                sCmdToCalcLRC += byTemp[1].ToString("X2");// 数据设置值 - 低字节
                sCmdToCalcLRC += byTemp[2].ToString("X2");// 数据设置值 - 高字节
                sCmdToCalcLRC += byTemp[3].ToString("X2");// 数据设置值 - 低字节
                sCmdToCalcLRC += byTemp[4].ToString("X2");// 数据设置值 - 高字节
                sCmdToCalcLRC += byTemp[5].ToString("X2");// 数据设置值 - 低字节
                sCmdToCalcLRC += byTemp[6].ToString("X2");// 数据设置值 - 高字节
                sCmdToCalcLRC += byTemp[7].ToString("X2");// 数据设置值 - 低字节

                byte[] byResult = HexCmdConvertToBytesIncludeLRC(sCmdToCalcLRC);

                sCmdToCalcLRC = null;

                string sCmdDataToBeSent = "";// Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                {
                    sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);
                    Enqueue("发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                byResult = null;

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"



                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"
                        
                        //**********************
                        //首字符
                        //[0] - :

                        //从站地址
                        //[1] - 0
                        //[2] - 1

                        //读取成功的功能码
                        //[3] - 0
                        //[4] - 5

                        //读取失败的错误码
                        //[3] - 0
                        //[4] - 8
                        //**********************

                        #endregion

                        //string sTemp = BytesToHexStringSplitByChar(byReadData);
                        
                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 0F

                        string sCmd = CModbusFunctionCode.WriteMultiCoil.ToString("X2");

                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])//CModbusFunctionCode.WriteMultiCoil.ToString()[0]       :010F
                        {
                            byReadData = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        // *** 写从站多个线圈失败后
                        //写操作记录：(COM1)收到字符串 - :018F036D

                        // *** 成功写从站多个线圈后，从站返回的字符串内容与发送的字符串内容，前面 13 个字符相同

                        //string sFront13CharsOfSentCmd = Strings.Mid(sCmdDataToBeSent, 1, 13);

                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.WriteMultiCoil.ToString("X2"))
                            //sFeedBackFromSlave[3] == '0' && sFeedBackFromSlave[4] == 'F'
                        {
                            sCmdDataToBeSent = null;
                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sCmdDataToBeSent = null;
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        // ok
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

                //if (SetValue.Length * 8 * 8 > 1968)
                //{
                //    throw new Exception("设置值byte[]数组的长度超出范围(1~123)，请修改参数数组的长度");
                //}

                if (BeginAddress + SetValue.Length * 8 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //:         地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      LRC      回车+换行符
                //1字符     2字符          2字符     4字符          4字符      2字符      2*N字符      1字符    2字符
                //1字节     1字节          1字节     2字节          2字节      1字节      N字节        1字节    2字节
                
                // 求 LRC -- 设备地址(1字节)   功能码(1字节)    设置起始地址(2字节)   数据位长度(2字节)   字节计数(1字节)   设置内容(N字节)
                string sCmdToCalcLRC = "";
                sCmdToCalcLRC += DeviceAddress.ToString("X2");//设备地址(2个字符)
                sCmdToCalcLRC += CModbusFunctionCode.WriteMultiCoil.ToString("X2");//功能码(2个字符)

                byte[] byBeginAddress = IntToBytes(BeginAddress);//起始地址(4个字符)
                sCmdToCalcLRC += byBeginAddress[1].ToString("X2");// 起始地址 - 高字节
                sCmdToCalcLRC += byBeginAddress[0].ToString("X2");// 起始地址 - 低字节

                //数据位长度 - 位的数据长度
                int iDataLength = SetValue.Length * 8 * 8;
                byte[] byDataLength = IntToBytes(iDataLength);
                sCmdToCalcLRC += byDataLength[1].ToString("X2");// 数据长度 - 高字节
                sCmdToCalcLRC += byDataLength[0].ToString("X2");// 数据长度 - 低字节

                // 字节计数
                byte byByteCount = Convert.ToByte(SetValue.Length * 8);//N - 字节数
                sCmdToCalcLRC += byByteCount.ToString("X2");

                // 线圈值的设置
                for (int i = 0; i < SetValue.Length; i++)
                {
                    //sCmdToCalcLRC += SetValue[i].ToString("X4");

                    byte[] byTemp = BitConverter.GetBytes(SetValue[i]);
                    sCmdToCalcLRC += byTemp[0].ToString("X2");// 数据设置值 - 高字节
                    sCmdToCalcLRC += byTemp[1].ToString("X2");// 数据设置值 - 低字节
                    sCmdToCalcLRC += byTemp[2].ToString("X2");// 数据设置值 - 高字节
                    sCmdToCalcLRC += byTemp[3].ToString("X2");// 数据设置值 - 低字节
                    sCmdToCalcLRC += byTemp[4].ToString("X2");// 数据设置值 - 高字节
                    sCmdToCalcLRC += byTemp[5].ToString("X2");// 数据设置值 - 低字节
                    sCmdToCalcLRC += byTemp[6].ToString("X2");// 数据设置值 - 高字节
                    sCmdToCalcLRC += byTemp[7].ToString("X2");// 数据设置值 - 低字节
                }
                
                byte[] byResult = HexCmdConvertToBytesIncludeLRC(sCmdToCalcLRC);

                sCmdToCalcLRC = null;

                string sCmdDataToBeSent = "";// Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                {
                    sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);
                    Enqueue("发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                byResult = null;

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"
                
                

                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        

                        //**********************
                        //首字符
                        //[0] - :

                        //从站地址
                        //[1] - 0
                        //[2] - 1

                        //读取成功的功能码
                        //[3] - 0
                        //[4] - 5

                        //读取失败的错误码
                        //[3] - 0
                        //[4] - 8
                        //**********************

                        #endregion

                        //string sTemp = BytesToHexStringSplitByChar(byReadData);
                        
                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 0F

                        string sCmd = CModbusFunctionCode.WriteMultiCoil.ToString("X2");

                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])//CModbusFunctionCode.WriteMultiCoil.ToString()[0]       :010F
                        {
                            byReadData = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        // *** 写从站多个线圈失败后
                        //写操作记录：(COM1)收到字符串 - :018F036D

                        // *** 成功写从站多个线圈后，从站返回的字符串内容与发送的字符串内容，前面 13 个字符相同

                        //string sFront13CharsOfSentCmd = Strings.Mid(sCmdDataToBeSent, 1, 13);

                        if (Strings.Mid(sFeedBackFromSlave, 4, 2) == CModbusFunctionCode.WriteMultiCoil.ToString("X2"))//sFeedBackFromSlave[3] == '0' && sFeedBackFromSlave[4] == 'F'
                        {
                            sCmdDataToBeSent = null;
                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sCmdDataToBeSent = null;
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        #endregion

        #region "写单个/多个保持寄存器 - ok"

        // ok
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
                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 2 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //06 - 设置单个保持寄存器(short:  -32,768 到 32,767)
                //:         地址码         功能码    设置起始地址   设置值      LRC     回车+换行符
                //1字符     2字符          2字符     4字符          4字符      2字符    2字符

                // 求 LRC -- 设备地址(2个字符) + 功能码(2个字符) + 起始地址(4个字符) + 数据值(4个字符)
                string sCmdToCalcLRC = "";
                sCmdToCalcLRC += DeviceAddress.ToString("X2");//设备地址(2个字符)
                sCmdToCalcLRC += CModbusFunctionCode.WriteRegister.ToString("X2");//功能码(2个字符)

                byte[] byBeginAddress = IntToBytes(BeginAddress);//起始地址(4个字符)
                sCmdToCalcLRC += byBeginAddress[1].ToString("X2");// 起始地址 - 高字节
                sCmdToCalcLRC += byBeginAddress[0].ToString("X2");// 起始地址 - 低字节

                //设置值
                sCmdToCalcLRC += SetValue.ToString("X4");
                
                byte[] byResult = HexCmdConvertToBytesIncludeLRC(sCmdToCalcLRC);

                sCmdToCalcLRC = null;

                string sCmdDataToBeSent = "";// Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                {
                    sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);
                    Enqueue("发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                byResult = null;

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"

                //写从站线圈失败后，

                //成功写从站后，从站返回的字符串内容与发送的字符串内容相同

                //成功执行写命令-4609
                //写操作记录：(COM1)发送字符串 - :01060000EDFF0D
                //写操作记录：(COM1)收到字符串 - :01060000EDFF0D

                //成功执行写命令-5159
                //写操作记录：(COM1)发送字符串 - :01060000EBD935
                //写操作记录：(COM1)收到字符串 - :01060000EBD935

                //成功执行写命令20116
                //写操作记录：(COM1)发送字符串 - :010600004E9417
                //写操作记录：(COM1)收到字符串 - :010600004E9417

                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //string sTemp = BytesToHexStringSplitByChar(byReadData);

                        //**********************
                        //首字符
                        //[0] - :

                        //从站地址
                        //[1] - 0
                        //[2] - 1

                        //读取成功的功能码
                        //[3] - 0
                        //[4] - 6

                        //读取失败的错误码
                        //[3] - 0
                        //[4] - 8
                        //**********************

                        #endregion

                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 0
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 6

                        string sCmd = CModbusFunctionCode.WriteRegister.ToString("X2");
                        
                        if (cByte3 == 0x0.ToString()[0] && cByte4 == sCmd[1])//:0106
                        //if (sByte3 == 0x0.ToString("X2") && sByte4 == sCmd)//:0106
                        {
                            byReadData = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        //成功写从站后，从站返回的字符串内容与发送的字符串内容相同
                        string sFeedBack = Strings.Mid(sFeedBackFromSlave, 4, 2);
                        string sCmd = CModbusFunctionCode.WriteRegister.ToString("X2");
                        if (sFeedBack == sCmd)//sCmdDataToBeSent == sFeedBackFromSlave)
                        {
                            sCmdDataToBeSent = null;
                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sCmdDataToBeSent = null;
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        // ok
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

                //10(16) - 设置多个保持寄存器
                //:         地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      LRC      回车+换行符
                //1字符     2字符          2字符     4字符          4字符      2字符      2*N字符      1字符    2字符
                //1字节     1字节          1字节     2字节          2字节      1字节      N字节        1字节    2字节
                //short:  -32,768 到 32,767

                string sCmdToCalcLRC = "";
                sCmdToCalcLRC += DeviceAddress.ToString("X2");//设备地址(2个字符)
                sCmdToCalcLRC += CModbusFunctionCode.WriteMultiRegister.ToString("X2");//功能码(2个字符)

                byte[] byBeginAddress = IntToBytes(BeginAddress);//起始地址(4个字符)
                sCmdToCalcLRC += byBeginAddress[1].ToString("X2");// 起始地址 - 高字节
                sCmdToCalcLRC += byBeginAddress[0].ToString("X2");// 起始地址 - 低字节

                //数据字长度 - 字数据的长度
                int iDataLength = SetValue.Length;
                byte[] byDataLength = IntToBytes(iDataLength);
                sCmdToCalcLRC += byDataLength[1].ToString("X2");// 数据长度 - 高字节
                sCmdToCalcLRC += byDataLength[0].ToString("X2");// 数据长度 - 低字节

                // 字节计数
                byte byByteCount = Convert.ToByte(iDataLength * 2);//数组长度 * 2 = 字节数
                sCmdToCalcLRC += byByteCount.ToString("X2");

                //设置值
                for (int i = 0; i < SetValue.Length; i++)
                {
                    sCmdToCalcLRC += SetValue[i].ToString("X4");
                }

                byte[] byResult = HexCmdConvertToBytesIncludeLRC(sCmdToCalcLRC);

                sCmdToCalcLRC = null;

                string sCmdDataToBeSent = "";// Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                {
                    sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);
                    Enqueue("发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                byResult = null;

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"

                //写从站线圈失败后，

                //成功写从站后，从站返回的字符串内容与发送的字符串内容相同

                //发送值[0~9]：1761 -14407 5777 5774 16502 17463 1001 -19511 27348 -21393
                //写操作记录：(COM1)发送字符串 - :01100000000A1406E1C7B91691168E4076443703E9B3C96AD4AC6F2D
                //写操作记录：(COM1)收到字符串 - :01100000000AE5

                //发送值[0~9]：26335 23829 -26550 24597 3427 29150 31561 3617 -19622 10699
                //写操作记录：(COM1)发送字符串 - :01100000000A1466DF5D15984A60150D6371DE7B490E21B35A29CB10
                //写操作记录：(COM1)收到字符串 - :01100000000AE5

                //发送值[0~9]：-17156 6028 12968 -24273 -21261 25743 -18234 -5990 5811 -10245
                //写操作记录：(COM1)发送字符串 - :01100000000A14BCFC178C32A8A12FACF3648FB8C6E89A16B3D7FB9F
                //写操作记录：(COM1)收到字符串 - :01100000000AE5

                //发送值[0~9]：-17884 -22043 -19558 6775 1968 -28626 10138 -25864 9493 29086
                //写操作记录：(COM1)发送字符串 - :01100000000A14BA24A9E5B39A1A7707B0902E279A9AF82515719E76
                //写操作记录：(COM1)收到字符串 - :01100000000AE5

                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        return false;
                    }
                    else
                    {
                        #region "通讯记录"

                        //string sTemp = BytesToHexStringSplitByChar(byReadData);

                        //**********************
                        //首字符
                        //[0] - :

                        //从站地址
                        //[1] - 0
                        //[2] - 1

                        //读取成功的功能码
                        //[3] - 0
                        //[4] - 6

                        //读取失败的错误码
                        //[3] - 0
                        //[4] - 8
                        //**********************

                        #endregion

                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 1
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 0

                        string sResultCmd = cByte3.ToString() + cByte4.ToString();
                        string sCmd = CModbusFunctionCode.WriteMultiRegister.ToString("X2");
                        
                        if (sResultCmd == sCmd)//:0106
                        //if (cByte3 == 0x0.ToString() && cByte4 == sCmd[1])//:0110
                        //if (sByte3 == 0x0.ToString("X2") && sByte4 == sCmd)//:0110
                        {
                            byReadData = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {


                        //成功写从站后，从站返回的字符串内容与发送的字符串内容相同
                        string sFeedBack = Strings.Mid(sFeedBackFromSlave, 4, 2);
                        string sCmd = CModbusFunctionCode.WriteMultiRegister.ToString("X2");
                        if (sFeedBack == sCmd)//sCmdDataToBeSent == sFeedBackFromSlave)
                        {
                            sCmdDataToBeSent = null;
                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sCmdDataToBeSent = null;
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
                    MessageBox.Show("Modbus Ascii 通讯时发生错误：\r\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return false;
            }

            //return true;
        }

        // ok
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
                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //10(16) - 设置多个保持寄存器
                //:         地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      LRC      回车+换行符
                //1字符     2字符          2字符     4字符          4字符      2字符      2*N字符      1字符    2字符
                //1字节     1字节          1字节     2字节          2字节      1字节      N字节        1字节    2字节

                string sCmdToCalcLRC = "";
                sCmdToCalcLRC += DeviceAddress.ToString("X2");//设备地址(2个字符)
                sCmdToCalcLRC += CModbusFunctionCode.WriteMultiRegister.ToString("X2");//功能码(2个字符)

                byte[] byBeginAddress = IntToBytes(BeginAddress);//起始地址(4个字符)
                sCmdToCalcLRC += byBeginAddress[1].ToString("X2");// 起始地址 - 高字节
                sCmdToCalcLRC += byBeginAddress[0].ToString("X2");// 起始地址 - 低字节

                //数据字长度 - 字数据的长度
                int iDataLength = 2;
                byte[] byDataLength = IntToBytes(iDataLength);
                sCmdToCalcLRC += byDataLength[1].ToString("X2");// 数据长度 - 高字节
                sCmdToCalcLRC += byDataLength[0].ToString("X2");// 数据长度 - 低字节

                // 字节计数
                byte byByteCount = Convert.ToByte(iDataLength * 2);//字长度 * 2 = 字节数
                sCmdToCalcLRC += byByteCount.ToString("X2");

                //设置值
                byte[] byFloatToBytes = BitConverter.GetBytes(SetValue);
                for (int i = 0; i < byFloatToBytes.Length / 2; i++)
                {
                    sCmdToCalcLRC += byFloatToBytes[i * 2 + 1].ToString("X2");// 数据 - 高字节
                    sCmdToCalcLRC += byFloatToBytes[i * 2].ToString("X2");// 数据 - 低字节                    
                }

                byte[] byResult = HexCmdConvertToBytesIncludeLRC(sCmdToCalcLRC);

                sCmdToCalcLRC = null;

                string sCmdDataToBeSent = "";// Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                {
                    sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);
                    Enqueue("发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                byResult = null;

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"

                //**********************
                //首字符
                //[0] - :

                //从站地址
                //[1] - 0
                //[2] - 1

                //读取成功的功能码
                //[3] - 0
                //[4] - 6

                //读取失败的错误码
                //[3] - 0
                //[4] - 8
                //**********************

                //成功执行写命令8184.571
                //写操作记录：(COM1)发送字符串 - :01100000000204C49245FF4F

                //写操作记录：(COM1)收到字符串 - :011000000002ED

                //成功执行写命令2654
                //写操作记录：(COM1)发送字符串 - :01100000000204E00045259F

                //写操作记录：(COM1)收到字符串 - :011000000002ED

                //成功执行写命令-3837.714
                //写操作记录：(COM1)发送字符串 - :01100000000204DB6EC56F6C

                //写操作记录：(COM1)收到字符串 - :011000000002ED

                //成功执行写命令2093.428
                //写操作记录：(COM1)发送字符串 - :01100000000204D6DB4502F1

                //写操作记录：(COM1)收到字符串 - :011000000002ED

                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        return false;
                    }
                    else
                    {
                        //string sTemp = BytesToHexStringSplitByChar(byReadData);
                        
                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 1
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 0

                        string sResultCmd = cByte3.ToString() + cByte4.ToString();
                        string sCmd = CModbusFunctionCode.WriteMultiRegister.ToString("X2");

                        if (sResultCmd == sCmd)//:0106
                        //if (cByte3 == 0x0.ToString() && cByte4 == sCmd[1])//:0110
                        //if (sByte3 == 0x0.ToString("X2") && sByte4 == sCmd)//:0110
                        {
                            byReadData = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        //成功写从站后，从站返回的字符串内容与发送的字符串内容相同
                        string sFeedBack = Strings.Mid(sFeedBackFromSlave, 4, 2);
                        string sCmd = CModbusFunctionCode.WriteMultiRegister.ToString("X2");
                        if (sFeedBack == sCmd)//sCmdDataToBeSent == sFeedBackFromSlave)
                        {
                            sCmdDataToBeSent = null;
                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sCmdDataToBeSent = null;
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

            //return true;
        }

        // ok
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

                //10(16) - 设置多个保持寄存器
                //:         地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      LRC      回车+换行符
                //1字符     2字符          2字符     4字符          4字符      2字符      2*N字符      1字符    2字符
                //1字节     1字节          1字节     2字节          2字节      1字节      N字节        1字节    2字节

                string sCmdToCalcLRC = "";
                sCmdToCalcLRC += DeviceAddress.ToString("X2");//设备地址(2个字符)
                sCmdToCalcLRC += CModbusFunctionCode.WriteMultiRegister.ToString("X2");//功能码(2个字符)

                byte[] byBeginAddress = IntToBytes(BeginAddress);//起始地址(4个字符)
                sCmdToCalcLRC += byBeginAddress[1].ToString("X2");// 起始地址 - 高字节
                sCmdToCalcLRC += byBeginAddress[0].ToString("X2");// 起始地址 - 低字节

                //数据字长度 - 字数据的长度
                int iDataLength = SetValue.Length * 2;
                byte[] byDataLength = IntToBytes(iDataLength);
                sCmdToCalcLRC += byDataLength[1].ToString("X2");// 数据长度 - 高字节
                sCmdToCalcLRC += byDataLength[0].ToString("X2");// 数据长度 - 低字节

                // 字节计数
                byte byByteCount = Convert.ToByte(iDataLength * 2);//字长度 * 2 = 字节数
                sCmdToCalcLRC += byByteCount.ToString("X2");

                //设置值
                for (int j = 0; j < SetValue.Length; j++)
                {
                    byte[] byFloatToBytes = BitConverter.GetBytes(SetValue[j]);
                    for (int i = 0; i < byFloatToBytes.Length / 2; i++)
                    {
                        sCmdToCalcLRC += byFloatToBytes[i * 2 + 1].ToString("X2");// 数据 - 高字节
                        sCmdToCalcLRC += byFloatToBytes[i * 2].ToString("X2");// 数据 - 低字节                    
                    }
                }
                
                byte[] byResult = HexCmdConvertToBytesIncludeLRC(sCmdToCalcLRC);

                sCmdToCalcLRC = null;

                string sCmdDataToBeSent = "";// Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                {
                    sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);
                    Enqueue("发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                byResult = null;

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"

                //**********************
                //首字符
                //[0] - :

                //从站地址
                //[1] - 0
                //[2] - 1

                //读取成功的功能码
                //[3] - 0
                //[4] - 6

                //读取失败的错误码
                //[3] - 0
                //[4] - 8
                //**********************



                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        return false;
                    }
                    else
                    {
                        //string sTemp = BytesToHexStringSplitByChar(byReadData);
                        
                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 1
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 0

                        string sResultCmd = cByte3.ToString() + cByte4.ToString();
                        string sCmd = CModbusFunctionCode.WriteMultiRegister.ToString("X2");

                        if (sResultCmd == sCmd)//:0106
                        //if (cByte3 == 0x0.ToString() && cByte4 == sCmd[1])//:0110
                        //if (sByte3 == 0x0.ToString("X2") && sByte4 == sCmd)//:0110
                        {
                            byReadData = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        //成功写从站后，从站返回的字符串内容与发送的字符串内容相同
                        string sFeedBack = Strings.Mid(sFeedBackFromSlave, 4, 2);
                        string sCmd = CModbusFunctionCode.WriteMultiRegister.ToString("X2");
                        if (sFeedBack == sCmd)//sCmdDataToBeSent == sFeedBackFromSlave)
                        {
                            sCmdDataToBeSent = null;
                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sCmdDataToBeSent = null;
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

            //return true;
        }

        // ok
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
                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 4 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //10(16) - 设置多个保持寄存器
                //:         地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      LRC      回车+换行符
                //1字符     2字符          2字符     4字符          4字符      2字符      2*N字符      1字符    2字符
                //1字节     1字节          1字节     2字节          2字节      1字节      N字节        1字节    2字节
                //short:  -32,768 到 32,767

                string sCmdToCalcLRC = "";
                sCmdToCalcLRC += DeviceAddress.ToString("X2");//设备地址(2个字符)
                sCmdToCalcLRC += CModbusFunctionCode.WriteMultiRegister.ToString("X2");//功能码(2个字符)

                byte[] byBeginAddress = IntToBytes(BeginAddress);//起始地址(4个字符)
                sCmdToCalcLRC += byBeginAddress[1].ToString("X2");// 起始地址 - 高字节
                sCmdToCalcLRC += byBeginAddress[0].ToString("X2");// 起始地址 - 低字节

                //float = 2个short，所以这里转换为字节数量：2 * 2
                //数据字长度 - 字数据的长度
                int iDataLength = 2;
                byte[] byDataLength = IntToBytes(iDataLength);
                sCmdToCalcLRC += byDataLength[1].ToString("X2");// 数据长度 - 高字节
                sCmdToCalcLRC += byDataLength[0].ToString("X2");// 数据长度 - 低字节

                // 字节计数
                byte byByteCount = Convert.ToByte(iDataLength * 2);//字长度 * 2 = 字节数
                sCmdToCalcLRC += byByteCount.ToString("X2");

                //设置值
                byte[] byFloatToBytes = BitConverter.GetBytes(SetValue);
                for (int i = 0; i < byFloatToBytes.Length / 2; i++)
                {
                    sCmdToCalcLRC += byFloatToBytes[i * 2 + 1].ToString("X2");// 数据 - 高字节
                    sCmdToCalcLRC += byFloatToBytes[i * 2].ToString("X2");// 数据 - 低字节                    
                }

                byte[] byResult = HexCmdConvertToBytesIncludeLRC(sCmdToCalcLRC);

                sCmdToCalcLRC = null;

                string sCmdDataToBeSent = "";// Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                {
                    sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);
                    Enqueue("发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                byResult = null;

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"

                //**********************
                //首字符
                //[0] - :

                //从站地址
                //[1] - 0
                //[2] - 1

                //读取成功的功能码
                //[3] - 0
                //[4] - 6

                //读取失败的错误码
                //[3] - 0
                //[4] - 8
                //**********************

                //成功执行写命令8184.571
                //写操作记录：(COM1)发送字符串 - :01100000000204C49245FF4F

                //写操作记录：(COM1)收到字符串 - :011000000002ED

                //成功执行写命令2654
                //写操作记录：(COM1)发送字符串 - :01100000000204E00045259F

                //写操作记录：(COM1)收到字符串 - :011000000002ED

                //成功执行写命令-3837.714
                //写操作记录：(COM1)发送字符串 - :01100000000204DB6EC56F6C

                //写操作记录：(COM1)收到字符串 - :011000000002ED

                //成功执行写命令2093.428
                //写操作记录：(COM1)发送字符串 - :01100000000204D6DB4502F1

                //写操作记录：(COM1)收到字符串 - :011000000002ED

                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        return false;
                    }
                    else
                    {
                        //string sTemp = BytesToHexStringSplitByChar(byReadData);
                        
                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 1
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 0

                        string sResultCmd = cByte3.ToString() + cByte4.ToString();
                        string sCmd = CModbusFunctionCode.WriteMultiRegister.ToString("X2");

                        if (sResultCmd == sCmd)//:0106
                        //if (cByte3 == 0x0.ToString() && cByte4 == sCmd[1])//:0110
                        //if (sByte3 == 0x0.ToString("X2") && sByte4 == sCmd)//:0110
                        {
                            byReadData = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        //成功写从站后，从站返回的字符串内容与发送的字符串内容相同
                        string sFeedBack = Strings.Mid(sFeedBackFromSlave, 4, 2);
                        string sCmd = CModbusFunctionCode.WriteMultiRegister.ToString("X2");
                        if (sFeedBack == sCmd)//sCmdDataToBeSent == sFeedBackFromSlave)
                        {
                            sCmdDataToBeSent = null;
                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sCmdDataToBeSent = null;
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

            //return true;
        }

        // ok
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

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + SetValue.Length * 4 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //10(16) - 设置多个保持寄存器
                //:         地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      LRC      回车+换行符
                //1字符     2字符          2字符     4字符          4字符      2字符      2*N字符      1字符    2字符
                //1字节     1字节          1字节     2字节          2字节      1字节      N字节        1字节    2字节
                //short:  -32,768 到 32,767

                string sCmdToCalcLRC = "";
                sCmdToCalcLRC += DeviceAddress.ToString("X2");//设备地址(2个字符)
                sCmdToCalcLRC += CModbusFunctionCode.WriteMultiRegister.ToString("X2");//功能码(2个字符)

                byte[] byBeginAddress = IntToBytes(BeginAddress);//起始地址(4个字符)
                sCmdToCalcLRC += byBeginAddress[1].ToString("X2");// 起始地址 - 高字节
                sCmdToCalcLRC += byBeginAddress[0].ToString("X2");// 起始地址 - 低字节

                //float = 2个short，所以这里转换为字节数量：2 * 2
                //数据字长度 - 字数据的长度
                int iDataLength = SetValue.Length * 2;
                byte[] byDataLength = IntToBytes(iDataLength);
                sCmdToCalcLRC += byDataLength[1].ToString("X2");// 数据长度 - 高字节
                sCmdToCalcLRC += byDataLength[0].ToString("X2");// 数据长度 - 低字节

                // 字节计数
                byte byByteCount = Convert.ToByte(iDataLength * 2);//字长度 * 2 = 字节数
                sCmdToCalcLRC += byByteCount.ToString("X2");

                //设置值
                for (int j = 0; j < SetValue.Length; j++)
                {
                    byte[] byFloatToBytes = BitConverter.GetBytes(SetValue[j]);
                    for (int i = 0; i < byFloatToBytes.Length / 2; i++)
                    {
                        sCmdToCalcLRC += byFloatToBytes[i * 2 + 1].ToString("X2");// 数据 - 高字节
                        sCmdToCalcLRC += byFloatToBytes[i * 2].ToString("X2");// 数据 - 低字节                    
                    }
                }
                
                byte[] byResult = HexCmdConvertToBytesIncludeLRC(sCmdToCalcLRC);

                sCmdToCalcLRC = null;

                string sCmdDataToBeSent = "";// Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                {
                    sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);
                    Enqueue("发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                byResult = null;

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"

                //**********************
                //首字符
                //[0] - :

                //从站地址
                //[1] - 0
                //[2] - 1

                //读取成功的功能码
                //[3] - 0
                //[4] - 6

                //读取失败的错误码
                //[3] - 0
                //[4] - 8
                //**********************

                //发送值[0~3]：6611.714 -8570.571 4602.571 -5777.714 
                //成功执行写命令
                //写操作记录：(COM1)发送字符串 - :011000000008109DB745CEEA49C605D492458F8DB7C5B47B

                //写操作记录：(COM1)收到字符串 - :011000000008E7

                //发送值[0~3]：-9180 -1506.571 2436 -6548 
                //成功执行写命令
                //写操作记录：(COM1)发送字符串 - :011000000008107000C60F5249C4BC40004518A000C5CCA9

                //写操作记录：(COM1)收到字符串 - :011000000008E7

                //发送值[0~3]：-234.5714 -2424 8976.571 2919.714 
                //成功执行写命令
                //写操作记录：(COM1)发送字符串 - :011000000008109249C36A8000C5174249460C7B6E453632

                //写操作记录：(COM1)收到字符串 - :011000000008E7

                //发送值[0~3]：-1406.857 3498.857 -8491.429 -1091.143 
                //成功执行写命令
                //写操作记录：(COM1)发送字符串 - :01100000000810DB6EC4AFADB7455AADB7C6046492C488A8

                //写操作记录：(COM1)收到字符串 - :011000000008E7

                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        return false;
                    }
                    else
                    {
                        //string sTemp = BytesToHexStringSplitByChar(byReadData);
                        
                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 1
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 0

                        string sResultCmd = cByte3.ToString() + cByte4.ToString();
                        string sCmd = CModbusFunctionCode.WriteMultiRegister.ToString("X2");

                        if (sResultCmd == sCmd)//:0106
                        //if (cByte3 == 0x0.ToString() && cByte4 == sCmd[1])//:0110
                        //if (sByte3 == 0x0.ToString("X2") && sByte4 == sCmd)//:0110
                        {
                            byReadData = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        //成功写从站后，从站返回的字符串内容与发送的字符串内容相同
                        string sFeedBack = Strings.Mid(sFeedBackFromSlave, 4, 2);
                        string sCmd = CModbusFunctionCode.WriteMultiRegister.ToString("X2");
                        if (sFeedBack == sCmd)//sCmdDataToBeSent == sFeedBackFromSlave)
                        {
                            sCmdDataToBeSent = null;
                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sCmdDataToBeSent = null;
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

            //return true;
        }

        // ok
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
                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //10(16) - 设置多个保持寄存器
                //:         地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      LRC      回车+换行符
                //1字符     2字符          2字符     4字符          4字符      2字符      2*N字符      1字符    2字符
                //1字节     1字节          1字节     2字节          2字节      1字节      N字节        1字节    2字节
                //short:  -32,768 到 32,767

                string sCmdToCalcLRC = "";
                sCmdToCalcLRC += DeviceAddress.ToString("X2");//设备地址(2个字符)
                sCmdToCalcLRC += CModbusFunctionCode.WriteMultiRegister.ToString("X2");//功能码(2个字符)

                byte[] byBeginAddress = IntToBytes(BeginAddress);//起始地址(4个字符)
                sCmdToCalcLRC += byBeginAddress[1].ToString("X2");// 起始地址 - 高字节
                sCmdToCalcLRC += byBeginAddress[0].ToString("X2");// 起始地址 - 低字节

                //double = 4个short，所以这里转换为字节数量：4 * 2
                //数据字长度 - 字数据的长度
                int iDataLength = 4;
                byte[] byDataLength = IntToBytes(iDataLength);
                sCmdToCalcLRC += byDataLength[1].ToString("X2");// 数据长度 - 高字节
                sCmdToCalcLRC += byDataLength[0].ToString("X2");// 数据长度 - 低字节

                // 字节计数
                byte byByteCount = Convert.ToByte(iDataLength * 2);//字长度 * 2 = 字节数
                sCmdToCalcLRC += byByteCount.ToString("X2");

                //设置值
                byte[] byFloatToBytes = BitConverter.GetBytes(SetValue);
                for (int i = 0; i < byFloatToBytes.Length / 2; i++)
                {
                    sCmdToCalcLRC += byFloatToBytes[i * 2 + 1].ToString("X2");// 数据 - 高字节
                    sCmdToCalcLRC += byFloatToBytes[i * 2].ToString("X2");// 数据 - 低字节                    
                }

                byte[] byResult = HexCmdConvertToBytesIncludeLRC(sCmdToCalcLRC);

                sCmdToCalcLRC = null;

                string sCmdDataToBeSent = "";// Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                {
                    sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);
                    Enqueue("发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                byResult = null;

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"

                //写从站线圈失败后，

                //成功写从站后，从站返回的字符串内容与发送的字符串内容相同

                //**********************
                //首字符
                //[0] - :

                //从站地址
                //[1] - 0
                //[2] - 1

                //读取成功的功能码
                //[3] - 0
                //[4] - 6

                //读取失败的错误码
                //[3] - 0
                //[4] - 8
                //**********************

                //成功执行写命令-603.142857142857
                //写操作记录：(COM1)发送字符串 - :0110000000040824929249D924C08213
                //写操作记录：(COM1)收到字符串 - :011000000004EB

                //成功执行写命令-5564.28571428571
                //写操作记录：(COM1)发送字符串 - :0110000000040849252492BC49C0B545
                //写操作记录：(COM1)收到字符串 - :011000000004EB

                //成功执行写命令7798.57142857143
                //写操作记录：(COM1)发送字符串 - :0110000000040892494924769240BE95
                //写操作记录：(COM1)收到字符串 - :011000000004EB

                //成功执行写命令-7428.28571428571
                //写操作记录：(COM1)发送字符串 - :01100000000408492524920449C0BDF5
                //写操作记录：(COM1)收到字符串 - :011000000004EB

                //成功执行写命令-7319.42857142857
                //写操作记录：(COM1)发送字符串 - :011000000004086DB7B6DB976DC0BCAE
                //写操作记录：(COM1)收到字符串 - :011000000004EB

                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        return false;
                    }
                    else
                    {
                        //string sTemp = BytesToHexStringSplitByChar(byReadData);

                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 1
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 0

                        string sResultCmd = cByte3.ToString() + cByte4.ToString();
                        string sCmd = CModbusFunctionCode.WriteMultiRegister.ToString("X2");

                        if (sResultCmd == sCmd)//:0106
                        //if (cByte3 == 0x0.ToString() && cByte4 == sCmd[1])//:0110
                        //if (sByte3 == 0x0.ToString("X2") && sByte4 == sCmd)//:0110
                        {
                            byReadData = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        //成功写从站后，从站返回的字符串内容与发送的字符串内容相同
                        string sFeedBack = Strings.Mid(sFeedBackFromSlave, 4, 2);
                        string sCmd = CModbusFunctionCode.WriteMultiRegister.ToString("X2");
                        if (sFeedBack == sCmd)//sCmdDataToBeSent == sFeedBackFromSlave)
                        {
                            sCmdDataToBeSent = null;
                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sCmdDataToBeSent = null;
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

            //return true;
        }

        // ok
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

                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + SetValue.Length * 8 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //10(16) - 设置多个保持寄存器
                //:         地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      LRC      回车+换行符
                //1字符     2字符          2字符     4字符          4字符      2字符      2*N字符      1字符    2字符
                //1字节     1字节          1字节     2字节          2字节      1字节      N字节        1字节    2字节
                //short:  -32,768 到 32,767

                string sCmdToCalcLRC = "";
                sCmdToCalcLRC += DeviceAddress.ToString("X2");//设备地址(2个字符)
                sCmdToCalcLRC += CModbusFunctionCode.WriteMultiRegister.ToString("X2");//功能码(2个字符)

                byte[] byBeginAddress = IntToBytes(BeginAddress);//起始地址(4个字符)
                sCmdToCalcLRC += byBeginAddress[1].ToString("X2");// 起始地址 - 高字节
                sCmdToCalcLRC += byBeginAddress[0].ToString("X2");// 起始地址 - 低字节

                //double = 4个short，所以这里转换为字节数量：4 * 2
                //数据字长度 - 字数据的长度
                int iDataLength = SetValue.Length * 4;
                byte[] byDataLength = IntToBytes(iDataLength);
                sCmdToCalcLRC += byDataLength[1].ToString("X2");// 数据长度 - 高字节
                sCmdToCalcLRC += byDataLength[0].ToString("X2");// 数据长度 - 低字节

                // 字节计数
                byte byByteCount = Convert.ToByte(iDataLength * 2);//字长度 * 2 = 字节数
                sCmdToCalcLRC += byByteCount.ToString("X2");

                //设置值
                for (int j = 0; j < SetValue.Length; j++)
                {
                    byte[] byFloatToBytes = BitConverter.GetBytes(SetValue[j]);
                    for (int i = 0; i < byFloatToBytes.Length / 2; i++)
                    {
                        sCmdToCalcLRC += byFloatToBytes[i * 2 + 1].ToString("X2");// 数据 - 高字节
                        sCmdToCalcLRC += byFloatToBytes[i * 2].ToString("X2");// 数据 - 低字节                    
                    }
                }
                
                byte[] byResult = HexCmdConvertToBytesIncludeLRC(sCmdToCalcLRC);

                sCmdToCalcLRC = null;

                string sCmdDataToBeSent = "";// Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                {
                    sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);
                    Enqueue("发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                byResult = null;

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"

                //写从站线圈失败后，

                //成功写从站后，从站返回的字符串内容与发送的字符串内容相同

                //**********************
                //首字符
                //[0] - :

                //从站地址
                //[1] - 0
                //[2] - 1

                //读取成功的功能码
                //[3] - 0
                //[4] - 6

                //读取失败的错误码
                //[3] - 0
                //[4] - 8
                //**********************

                //发送值[0~3]：-6803.14306640625 -4078.57153320313 -5290.85693359375 -3673.42846679688
                //写操作记录：(COM1)发送字符串 - :011000000010200000A0009324C0BA0000A000DD24C0AF00006000AADBC0B400006000B2DBC0AC2C
                //写操作记录：(COM1)收到字符串 - :011000000010DF

                //发送值[0~3]：8971.7138671875 -6616.85693359375 8470.5712890625 -766.285705566406
                //写操作记录：(COM1)发送字符串 - :011000000010200000600085DB40C100006000D8DBC0B9000020008B4940C000002000F249C087DC
                //写操作记录：(COM1)收到字符串 - :011000000010DF

                //发送值[0~3]：-2275.14282226563 7505.71435546875 -6846.28564453125 1870.57141113281
                //写操作记录：(COM1)发送字符串 - :0110000000102000002000C649C0A10000E00051B640BD00002000BE49C0BA000020003A49409D2A
                //写操作记录：(COM1)收到字符串 - :011000000010DF

                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        return false;
                    }
                    else
                    {
                        //string sTemp = BytesToHexStringSplitByChar(byReadData);

                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 1
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 0

                        string sResultCmd = cByte3.ToString() + cByte4.ToString();
                        string sCmd = CModbusFunctionCode.WriteMultiRegister.ToString("X2");

                        if (sResultCmd == sCmd)//:0106
                        //if (cByte3 == 0x0.ToString() && cByte4 == sCmd[1])//:0110
                        //if (sByte3 == 0x0.ToString("X2") && sByte4 == sCmd)//:0110
                        {
                            byReadData = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        //成功写从站后，从站返回的字符串内容与发送的字符串内容相同
                        string sFeedBack = Strings.Mid(sFeedBackFromSlave, 4, 2);
                        string sCmd = CModbusFunctionCode.WriteMultiRegister.ToString("X2");
                        if (sFeedBack == sCmd)//sCmdDataToBeSent == sFeedBackFromSlave)
                        {
                            sCmdDataToBeSent = null;
                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sCmdDataToBeSent = null;
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

            //return true;
        }

        // ok
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
                if (BeginAddress < 0 || BeginAddress > 65535)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("起始地址的值不正确，有效值范围：0~65535(位)");
                }

                if (BeginAddress + 8 * 8 > 65535)
                {
                    throw new Exception("起始地址加设置值的长度超出寻址范围，请修改起始地址或参数数组的长度");
                }

                //10(16) - 设置多个保持寄存器
                //:         地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      LRC      回车+换行符
                //1字符     2字符          2字符     4字符          4字符      2字符      2*N字符      1字符    2字符
                //1字节     1字节          1字节     2字节          2字节      1字节      N字节        1字节    2字节
                //short:  -32,768 到 32,767

                string sCmdToCalcLRC = "";
                sCmdToCalcLRC += DeviceAddress.ToString("X2");//设备地址(2个字符)
                sCmdToCalcLRC += CModbusFunctionCode.WriteMultiRegister.ToString("X2");//功能码(2个字符)

                byte[] byBeginAddress = IntToBytes(BeginAddress);//起始地址(4个字符)
                sCmdToCalcLRC += byBeginAddress[1].ToString("X2");// 起始地址 - 高字节
                sCmdToCalcLRC += byBeginAddress[0].ToString("X2");// 起始地址 - 低字节

                //double = 4个short，所以这里转换为字节数量：4 * 2
                //数据字长度 - 字数据的长度
                int iDataLength = 4;
                byte[] byDataLength = IntToBytes(iDataLength);
                sCmdToCalcLRC += byDataLength[1].ToString("X2");// 数据长度 - 高字节
                sCmdToCalcLRC += byDataLength[0].ToString("X2");// 数据长度 - 低字节

                // 字节计数
                byte byByteCount = Convert.ToByte(iDataLength * 2);//字长度 * 2 = 字节数
                sCmdToCalcLRC += byByteCount.ToString("X2");

                //设置值
                byte[] byFloatToBytes = BitConverter.GetBytes(SetValue);
                for (int i = 0; i < byFloatToBytes.Length / 2; i++)
                {
                    sCmdToCalcLRC += byFloatToBytes[i * 2 + 1].ToString("X2");// 数据 - 高字节
                    sCmdToCalcLRC += byFloatToBytes[i * 2].ToString("X2");// 数据 - 低字节                    
                }

                byte[] byResult = HexCmdConvertToBytesIncludeLRC(sCmdToCalcLRC);

                sCmdToCalcLRC = null;

                string sCmdDataToBeSent = "";// Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                {
                    sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);
                    Enqueue("发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                byResult = null;

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"

                //写从站线圈失败后，

                //成功写从站后，从站返回的字符串内容与发送的字符串内容相同

                //**********************
                //首字符
                //[0] - :

                //从站地址
                //[1] - 0
                //[2] - 1

                //读取成功的功能码
                //[3] - 0
                //[4] - 6

                //读取失败的错误码
                //[3] - 0
                //[4] - 8
                //**********************

                //成功执行写命令-1679369208
                //写操作记录：(COM1)发送字符串 - :01100000000408DC089BE6FFFFFFFF82
                //写操作记录：(COM1)收到字符串 - :011000000004EB

                //成功执行写命令-232922630
                //写操作记录：(COM1)发送字符串 - :01100000000408E1FAF21DFFFFFFFFFD
                //写操作记录：(COM1)收到字符串 - :011000000004EB

                //成功执行写命令1321247723
                //写操作记录：(COM1)发送字符串 - :01100000000408A3EB4EC00000000047
                //写操作记录：(COM1)收到字符串 - :011000000004EB

                //成功执行写命令-1253068578
                //写操作记录：(COM1)发送字符串 - :01100000000408B0DEB54FFFFFFFFF55
                //写操作记录：(COM1)收到字符串 - :011000000004EB

                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        return false;
                    }
                    else
                    {
                        //string sTemp = BytesToHexStringSplitByChar(byReadData);

                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 1
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 0

                        string sResultCmd = cByte3.ToString() + cByte4.ToString();
                        string sCmd = CModbusFunctionCode.WriteMultiRegister.ToString("X2");

                        if (sResultCmd == sCmd)//:0106
                        //if (cByte3 == 0x0.ToString() && cByte4 == sCmd[1])//:0110
                        //if (sByte3 == 0x0.ToString("X2") && sByte4 == sCmd)//:0110
                        {
                            byReadData = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        //成功写从站后，从站返回的字符串内容与发送的字符串内容相同
                        string sFeedBack = Strings.Mid(sFeedBackFromSlave, 4, 2);
                        string sCmd = CModbusFunctionCode.WriteMultiRegister.ToString("X2");
                        if (sFeedBack == sCmd)//sCmdDataToBeSent == sFeedBackFromSlave)
                        {
                            sCmdDataToBeSent = null;
                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sCmdDataToBeSent = null;
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

            //return true;
        }

        // ok
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

                //10(16) - 设置多个保持寄存器
                //:         地址码         功能码    设置起始地址   设置长度   字节计数   设置内容      LRC      回车+换行符
                //1字符     2字符          2字符     4字符          4字符      2字符      2*N字符      1字符    2字符
                //1字节     1字节          1字节     2字节          2字节      1字节      N字节        1字节    2字节
                //short:  -32,768 到 32,767

                string sCmdToCalcLRC = "";
                sCmdToCalcLRC += DeviceAddress.ToString("X2");//设备地址(2个字符)
                sCmdToCalcLRC += CModbusFunctionCode.WriteMultiRegister.ToString("X2");//功能码(2个字符)

                byte[] byBeginAddress = IntToBytes(BeginAddress);//起始地址(4个字符)
                sCmdToCalcLRC += byBeginAddress[1].ToString("X2");// 起始地址 - 高字节
                sCmdToCalcLRC += byBeginAddress[0].ToString("X2");// 起始地址 - 低字节

                //double = 4个short，所以这里转换为字节数量：4 * 2
                //数据字长度 - 字数据的长度
                int iDataLength = SetValue.Length * 4;
                byte[] byDataLength = IntToBytes(iDataLength);
                sCmdToCalcLRC += byDataLength[1].ToString("X2");// 数据长度 - 高字节
                sCmdToCalcLRC += byDataLength[0].ToString("X2");// 数据长度 - 低字节

                // 字节计数
                byte byByteCount = Convert.ToByte(iDataLength * 2);//字长度 * 2 = 字节数
                sCmdToCalcLRC += byByteCount.ToString("X2");

                //设置值
                for (int j = 0; j < SetValue.Length; j++)
                {
                    byte[] byFloatToBytes = BitConverter.GetBytes(SetValue[j]);
                    for (int i = 0; i < byFloatToBytes.Length / 2; i++)
                    {
                        sCmdToCalcLRC += byFloatToBytes[i * 2 + 1].ToString("X2");// 数据 - 高字节
                        sCmdToCalcLRC += byFloatToBytes[i * 2].ToString("X2");// 数据 - 低字节                    
                    }
                }
                
                byte[] byResult = HexCmdConvertToBytesIncludeLRC(sCmdToCalcLRC);

                sCmdToCalcLRC = null;

                string sCmdDataToBeSent = "";// Encoding.ASCII.GetString(byResult);

                if (bSaveSendStringToLog == true)//&& ProcessFeedbackDataByBytes == false
                {
                    sCmdDataToBeSent = Encoding.ASCII.GetString(byResult);
                    Enqueue("发送字符串 - " + sCmdDataToBeSent);
                }

                

                //清除发送前的接收缓冲区
                ClearReceiveBuffer();

                // 通过串口发送指令数据给客户端
                RS232CPort.Write(byResult, 0, byResult.Length);

                byResult = null;

                // 读取客户端的返回信息
                Stopwatch swUsedTime = new Stopwatch();
                swUsedTime.Restart();

                while (RS232CPort.BytesToRead < 2)
                {
                    if (swUsedTime.ElapsedMilliseconds >= iWaitFeedbackTime)
                    {
                        
                        throw new Exception("超时未收到从站 [" + DeviceAddress.ToString("") + "] 的反馈信息");
                        //break;
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(iSleepTime);
                }

                byte[] byReadData = Encoding.UTF8.GetBytes(RS232CPort.ReadExisting());

                

                string sFeedBackFromSlave = Encoding.ASCII.GetString(byReadData);

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

                #region "通讯记录"

                //写从站线圈失败后，

                //成功写从站后，从站返回的字符串内容与发送的字符串内容相同

                //**********************
                //首字符
                //[0] - :

                //从站地址
                //[1] - 0
                //[2] - 1

                //读取成功的功能码
                //[3] - 0
                //[4] - 6

                //读取失败的错误码
                //[3] - 0
                //[4] - 8
                //**********************

                //发送值[0~3]：-1463375088 879789444 1723001098 -963809514
                //写操作记录：(COM1)发送字符串 - :01100000001020AB10A8C6FFFFFFFF8584347000000000E90A66B2000000006F16C68DFFFFFFFF0E
                //写操作记录：(COM1)收到字符串 - :011000000010DF

                //发送值[0~3]：1021952288 -579563780 -1379586892 18051368
                //写操作记录：(COM1)发送字符串 - :01100000001020C1203CE9000000008EFCDD74FFFFFFFF2CB4ADC5FFFFFFFF7128011300000000E7
                //写操作记录：(COM1)收到字符串 - :011000000010DF

                //发送值[0~3]：1874326609 1742260251 -629622734 -337371585
                //写操作记录：(COM1)发送字符串 - :01100000001020F4516FB700000000C81B67D800000000B832DA78FFFFFFFF1E3FEBE4FFFFFFFFD2
                //写操作记录：(COM1)收到字符串 - :011000000010DF

                #endregion

                if (ProcessFeedbackDataByBytes == true)
                {
                    #region "以字节方式处理从站的返回结果"

                    sFeedBackFromSlave = null;

                    if (null == byReadData || byReadData.Length < 2)
                    {
                        return false;
                    }
                    else
                    {
                        //string sTemp = BytesToHexStringSplitByChar(byReadData);

                        char cByte3 = Convert.ToChar(byReadData[3]);//功能码的第一个字节 - 1
                        char cByte4 = Convert.ToChar(byReadData[4]);//功能码的第一个字节 - 0

                        string sResultCmd = cByte3.ToString() + cByte4.ToString();
                        string sCmd = CModbusFunctionCode.WriteMultiRegister.ToString("X2");

                        if (sResultCmd == sCmd)//:0106
                        //if (cByte3 == 0x0.ToString() && cByte4 == sCmd[1])//:0110
                        //if (sByte3 == 0x0.ToString("X2") && sByte4 == sCmd)//:0110
                        {
                            byReadData = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(byReadData);
                            return false;
                        }
                    }

                    #endregion
                }
                else
                {
                    #region "以字符串方式处理从站的返回结果"

                    byReadData = null;

                    if (string.IsNullOrEmpty(sFeedBackFromSlave) == true)
                    {
                        return false;
                    }
                    else
                    {
                        //成功写从站后，从站返回的字符串内容与发送的字符串内容相同
                        string sFeedBack = Strings.Mid(sFeedBackFromSlave, 4, 2);
                        string sCmd = CModbusFunctionCode.WriteMultiRegister.ToString("X2");
                        if (sFeedBack == sCmd)//sCmdDataToBeSent == sFeedBackFromSlave)
                        {
                            sCmdDataToBeSent = null;
                            sFeedBackFromSlave = null;

                            return true;
                        }
                        else
                        {
                            AnalysisErrorCode(sFeedBackFromSlave);
                            sCmdDataToBeSent = null;
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
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
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
                    qErrorMsg.Enqueue("(" + RS232CPort.PortName + ")" + Msg);//发生错误
                }
                else
                {
                    qErrorMsg.Dequeue();
                    qErrorMsg.Enqueue("(" + RS232CPort.PortName + ")" + Msg);//发生错误
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

        /// <summary>
        /// TBD -- 解析返回信息的错误代码
        /// </summary>
        /// <param name="MsgWithErrorCode">从站返回的完整字符串(含错误信息)</param>
        /// <returns></returns>
        public string AnalysisErrorCode(string MsgWithErrorCode)
        {
            return "";
        }

        /// <summary>
        /// TBD -- 解析返回信息的错误代码
        /// </summary>
        /// <param name="MsgWithErrorCode">从站返回的完整字节数组(含错误信息)</param>
        /// <returns></returns>
        public string AnalysisErrorCode(byte[] MsgWithErrorCode)
        {
            return "";
        }

        /// <summary>
        /// 创建读取命令的字节数组，可以直接发送这个字节数组到串口端口
        /// </summary>
        /// <param name="DeviceAddress">从站地址</param>
        /// <param name="ReadFunctionCode">读取功能码</param>
        /// <param name="BeginReadAddress">读取的起始地址</param>
        /// <param name="ReadDataLength">读取数据长度，有效值范围：1~2000(位)</param>
        /// <returns></returns>
        private byte[] MakeReadCmd(byte DeviceAddress, byte ReadFunctionCode, ushort BeginReadAddress, ushort ReadDataLength)
        {
            //byte[] byResultData = null;// new byte[15];

            try
            {
                if (ReadDataLength < 1 || ReadDataLength > 2000)//读取数据长度，有效值范围：1~2000(位)
                {
                    throw new Exception("读取数据长度的值不正确，有效值范围：1~2000(位)，换算为1~250个字节");
                }

                string sReadCmd = DeviceAddress.ToString("X2");//设备地址码，1个字节，2个字符
                sReadCmd += ReadFunctionCode.ToString("X2");//功能码，1个字节，2个字符
                sReadCmd += BeginReadAddress.ToString("X4");//起始地址，2个字节，4个字符
                sReadCmd += ReadDataLength.ToString("X4");//读取数据长度，2个字节，4个字符

                return HexCmdConvertToBytesIncludeLRC(sReadCmd);

                #region "Old codes"

                //byte[] byCmdDataUsedToCalcLRC = new byte[12];

                //byCmdDataUsedToCalcLRC[0] = DeviceAddress;       // 从站地址

                //byCmdDataUsedToCalcLRC[1] = ReadFunctionCode;    // 读取功能码

                //byte[] byBeginAddress = BitConverter.GetBytes(BeginReadAddress);//以字节数组的形式返回指定的 16 位无符号整数值
                //byCmdDataUsedToCalcLRC[2] = byBeginAddress[1];				// 起始地址的高八位
                //byCmdDataUsedToCalcLRC[3] = byBeginAddress[0];				// 起始地址的低八位

                //byte[] byReadDataLength = BitConverter.GetBytes(ReadDataLength);
                //byCmdDataUsedToCalcLRC[4] = byReadDataLength[1];			// 读取数据长度的高八位
                //byCmdDataUsedToCalcLRC[5] = byReadDataLength[0];			// 读取数据长度的低八位

                ////计算LRC
                //byte[] byLRCResult = CalcLRCBytes(byCmdDataUsedToCalcLRC);
                //if (null == byLRCResult || byLRCResult.Length != 2)
                //{
                //    return null;
                //}

                ////汇总指令字节数组
                //byResultData[0] = PrefixByte;//:

                ////6个字节的命令(1~6)：从站地址  读取功能码  起始地址  
                //for (int i = 0; i < byCmdDataUsedToCalcLRC.Length; i++)
                //{
                //    byResultData[i + 1] = byCmdDataUsedToCalcLRC[i];
                //}

                //byResultData[7] = byLRCResult[0];
                //byResultData[8] = byLRCResult[1];

                #endregion
            }
            catch (Exception ex)
            {
                Enqueue(ex.Message + "  " + ex.StackTrace);
                return null;
            }

            //return byResultData;
        }

        /// <summary>
        /// 【此字节数组可直接通过串口发送】
        /// 将 16 进制 Modbus Ascii 命令字符串转换为字节数组，命令字符串不要包含 ':' 和 '回车+换行'，程序会自动添加；
        /// 格式：地址(2个字符) + 功能码(2个字符) + 起始地址(4个字符) + 数据长度(4个字符)
        /// </summary>
        /// <param name="ModbusAsciiHexCommand">16进制字符串</param>
        /// <returns></returns>
        public static byte[] HexCmdConvertToBytesIncludeLRC(string ModbusAsciiHexCommand)
        {
            byte[] byResults = null;

            try
            {
                if (string.IsNullOrEmpty(ModbusAsciiHexCommand) == true)
                {
                    throw new Exception("Modbus Ascii 命令字符串不能为空");
                }

                if (ModbusAsciiHexCommand[0] == ':')
                {
                    ModbusAsciiHexCommand.Remove(0, 1);
                }

                if (Strings.Left(ModbusAsciiHexCommand, 2) == "\r\n")
                {
                    ModbusAsciiHexCommand.Remove(ModbusAsciiHexCommand.Length - 2, 2);
                }

                if (ModbusAsciiHexCommand.Length % 2 != 0)
                {
                    throw new Exception("Modbus Ascii 命令字符串长度不正确，应该是2的整数倍");
                }

                // 执行写的时候，命令字符串的长度不是固定的
                //if (ModbusAsciiCommand.Length != 10)
                //{
                //    throw new Exception("Modbus Ascii 命令字符串长度不正确，应该是10个字符");
                //}

                byte[] byCommandStringToBytes = Encoding.ASCII.GetBytes(ModbusAsciiHexCommand);
                

                byte[] byLRCResult = CalcLRCBytes(ModbusAsciiHexCommand);
                if (null == byLRCResult || byLRCResult.Length != 2)
                {
                    throw new Exception("LRC计算错误");
                }

                byResults = new byte[byCommandStringToBytes.Length + 5];

                byResults[0] = 0x3A;

                //ASCII字符转换为字节，共 6 个字节
                for (int i = 0; i < byCommandStringToBytes.Length; i++)
                {
                    byResults[i + 1] = byCommandStringToBytes[i];
                }

                //LRC
                byResults[byCommandStringToBytes.Length + 1 + 0] = byLRCResult[0];
                byResults[byCommandStringToBytes.Length + 1 + 1] = byLRCResult[1];

                //CRLF
                byResults[byCommandStringToBytes.Length + 1 + 2] = 0x0D;
                byResults[byCommandStringToBytes.Length + 1 + 3] = 0x0A;
            }
            catch (Exception)// ex)
            {
                //Enqueue(ex.Message + "  " + ex.StackTrace);
            }

            return byResults;
        }

        /// <summary>
        /// 将字节数组转换为16进制字符串，且用字符进行分隔
        /// </summary>
        /// <param name="ByteData">字节数组</param>
        /// <param name="SplitChar">分割字符</param>
        /// <returns></returns>
        public static string BytesToHexStringSplitByChar(byte[] ByteData, char SplitChar = ' ')
        {
            string sResult = "";

            try
            {
                if (null != ByteData && ByteData.Length > 0)
                {
                    for (int i = 0; i < ByteData.Length; i++)
                    {
                        sResult += ByteData[i].ToString("X2") + SplitChar;
                    }
                }
            }
            catch (Exception)// ex)
            {
                //Enqueue(ex.Message + "  " + ex.StackTrace);
            }

            return sResult;
        }

        /// <summary>
        /// 将16进制字符串(用字符进行分隔)转换为ASCII码字符串
        /// </summary>
        /// <param name="HexStringSplitByChar">16进制字符串(用字符进行分隔)</param>
        /// <param name="SplitChar">分割字符</param>
        /// <returns></returns>
        public static string HexStringSplitByCharToASCIIString(string HexStringSplitByChar, char SplitChar = ' ')
        {
            string sResult = "";

            try
            {
                if (string.IsNullOrEmpty(HexStringSplitByChar) == true)
                {
                    return "";
                }

                string[] sHexString = HexStringSplitByChar.Split(SplitChar);
                byte[] byConvertData = new byte[sHexString.Length];
                if (null == sHexString && sHexString.Length < 1)
                {
                    return "";   
                }
                else
                {
                    for (int i = 0; i < sHexString.Length; i++)
                    {
                        byConvertData[i] = TwoHexCharsToByte(sHexString[i]);
                    }

                    sResult = Encoding.ASCII.GetString(byConvertData);
                }
            }
            catch (Exception)// ex)
            {
                //Enqueue(ex.Message + "  " + ex.StackTrace);
            }

            return sResult;
        }

        /// <summary>
        /// 拼接两个字节数组，将第二个字节数组拼接在第一个字节数组后面
        /// </summary>
        /// <param name="FirstBytes"></param>
        /// <param name="SecondBytes"></param>
        /// <returns></returns>
        public static byte[] JoinTwoByteArrays(byte[] FirstBytes, byte[] SecondBytes)
        {
            if (null == FirstBytes && null == SecondBytes)
            {
                return null;
            }

            if (null == FirstBytes)
            {
                return SecondBytes;
            }

            if (null == SecondBytes)
            {
                return FirstBytes;
            }

            byte[] byResult = new byte[FirstBytes.Length + SecondBytes.Length];
            Array.Copy(FirstBytes, byResult, FirstBytes.Length);
            Array.Copy(SecondBytes, 0, byResult, FirstBytes.Length, SecondBytes.Length);

            return byResult;
        }

        /// <summary>
        /// 计算LRC值，返回长度为2的字节数组 -- 格式：地址(2个字符) + 功能码(2个字符) + 起始地址(4个字符) + 数据长度(4个字符)
        /// </summary>
        /// <param name="ModbusAsciiCommand">16进制字符串</param>
        /// <returns></returns>
        public static byte[] CalcLRCBytes(string ModbusAsciiCommand)
        {
            byte[] byLRCResult = null;

            try
            {
                if (string.IsNullOrEmpty(ModbusAsciiCommand) == true)
                {
                    throw new Exception("Modbus Ascii 命令字符串不能为空");
                }

                if (ModbusAsciiCommand[0] == ':')
                {
                    ModbusAsciiCommand = ModbusAsciiCommand.Remove(0, 1);
                }

                if (Strings.Left(ModbusAsciiCommand, 2) == "\r\n")
                {
                    ModbusAsciiCommand = ModbusAsciiCommand.Remove(ModbusAsciiCommand.Length - 2, 2);
                }

                if (Strings.Left(ModbusAsciiCommand, 1) == "\r" || Strings.Left(ModbusAsciiCommand, 1) == "\n")
                {
                    ModbusAsciiCommand = ModbusAsciiCommand.Remove(ModbusAsciiCommand.Length - 1, 1);

                    //
                    if (Strings.Left(ModbusAsciiCommand, 1) == "\r" || Strings.Left(ModbusAsciiCommand, 1) == "\n")
                    {
                        ModbusAsciiCommand = ModbusAsciiCommand.Remove(ModbusAsciiCommand.Length - 1, 1);
                    }
                }
                else
                {
                    //如果倒数第二个字符是回车或换行符号就将其删除，并将最后一个字符前移一个位置
                    if (Strings.Mid(ModbusAsciiCommand, ModbusAsciiCommand.Length - 2, 1) == "\r" || Strings.Mid(ModbusAsciiCommand, ModbusAsciiCommand.Length - 2, 1) == "\n")
                    {
                        string sTemp = Strings.Mid(ModbusAsciiCommand, 1, ModbusAsciiCommand.Length - 2);
                        sTemp += ModbusAsciiCommand[ModbusAsciiCommand.Length];
                        ModbusAsciiCommand = sTemp;
                    }
                }

                if (ModbusAsciiCommand.Length % 2 != 0)
                {
                    throw new Exception("Modbus Ascii 命令字符串长度不正确，应该是2的整数倍");
                }

                // 执行写的时候，命令字符串的长度不是固定的
                //if (ModbusAsciiCommand.Length != 10)
                //{
                //    throw new Exception("Modbus Ascii 命令字符串长度不正确，应该是10个字符");
                //}
                
                byte[] byBytesToCalcLRC = new byte[ModbusAsciiCommand.Length / 2];

                for (int i = 0; i < ModbusAsciiCommand.Length / 2; i++)
                {
                    string sTemp = Strings.Mid(ModbusAsciiCommand, i * 2 + 1, 2);
                    byBytesToCalcLRC[i] = TwoHexCharsToByte(sTemp);
                }
                
                // 计算字节求和值
                int iAdditionValue = 0;
                byte bySumValue = 0;
                for (int i = 0; i < byBytesToCalcLRC.Length; i++)//
                {
                    iAdditionValue += byBytesToCalcLRC[i];//
                }

                // 取字节累加值的第1个字节， int 包含 4 个字节
                byte[] byLatestTemp = BitConverter.GetBytes(iAdditionValue);
                bySumValue = byLatestTemp[0];
                
                // 取反
                bySumValue = Convert.ToByte(255 ^ bySumValue);
                //bySumValue = (byte)(255 - bySumValue);

                // +1
                bySumValue += 1;

                byLRCResult = new byte[2];
                string sLRCResult = bySumValue.ToString("X2");
                for (int i = 0; i < byLRCResult.Length; i++)
                {
                    byLRCResult[i] = Convert.ToByte(sLRCResult[i]);
                }
            }
            catch (Exception)// ex)
            {
                //Enqueue(ex.Message + "  " + ex.StackTrace);
            }

            return byLRCResult;
        }

        /// <summary>
        /// 计算LRC值，返回长度为2的字节数组 -- 格式：地址(2个字符) + 功能码(2个字符) + 起始地址(4个字符) + 数据长度(4个字符)
        /// </summary>
        /// <param name="DataUsedToCalcLRC">用于计算LRC的字节数组</param>
        /// <returns></returns>
        public static byte[] CalcLRCBytes(byte[] DataUsedToCalcLRC)
        {
            byte[] byLRCResult = null;

            try
            {
                if (null == DataUsedToCalcLRC)
                {
                    throw new Exception("Modbus Ascii 命令字节数组不能为空");
                }

                // 检查第一个字节是否为 ':'
                if (DataUsedToCalcLRC[0] == PrefixByte)
                {
                    byte[] byTemp = DataUsedToCalcLRC;
                    Array.Copy(DataUsedToCalcLRC, DataUsedToCalcLRC.GetLowerBound(0) + 1, byTemp, 0, DataUsedToCalcLRC.Length - 1); //跳过第一个字节 ':'
                    DataUsedToCalcLRC = byTemp;
                }

                // 检查最后2个字节是否为回车或换行符号
                if (DataUsedToCalcLRC[DataUsedToCalcLRC.Length -1] == 0x0D || DataUsedToCalcLRC[DataUsedToCalcLRC.Length -1] == 0x0A)
                {
                    // 第一次执行
                    byte[] byTemp = DataUsedToCalcLRC;
                    Array.Copy(DataUsedToCalcLRC, DataUsedToCalcLRC.GetLowerBound(0), byTemp, 0, DataUsedToCalcLRC.Length - 1);
                    DataUsedToCalcLRC = byTemp;

                    // 第二次执行
                    if (DataUsedToCalcLRC[DataUsedToCalcLRC.Length - 1] == 0x0D || DataUsedToCalcLRC[DataUsedToCalcLRC.Length - 1] == 0x0A)
                    {
                        byTemp = DataUsedToCalcLRC;
                        Array.Copy(DataUsedToCalcLRC, DataUsedToCalcLRC.GetLowerBound(0), byTemp, 0, DataUsedToCalcLRC.Length - 1);
                        DataUsedToCalcLRC = byTemp;
                    }
                }
                else
                {
                    // 如果最后一个字节不是回车或换行符号，但是倒数第二个字节是回车或换行符号，就将最后一个字节的值赋给倒数第二个字节，然后将整个数组的长度-1
                    if (DataUsedToCalcLRC[DataUsedToCalcLRC.Length - 2] == 0x0D || DataUsedToCalcLRC[DataUsedToCalcLRC.Length - 2] == 0x0A)
                    {
                        DataUsedToCalcLRC[DataUsedToCalcLRC.Length - 2] = DataUsedToCalcLRC[DataUsedToCalcLRC.Length - 1];
                        Array.Resize<byte>(ref DataUsedToCalcLRC, DataUsedToCalcLRC.Length - 1);
                    }
                }

                if (DataUsedToCalcLRC.Length % 2 != 0)
                {
                    throw new Exception("Modbus Ascii 命令字节数组长度不正确，应该是2的整数倍");
                }
                
                // 计算字节求和值
                int iAdditionValue = 0;
                byte bySumValue = 0;
                for (int i = 0; i < DataUsedToCalcLRC.Length; i++)//
                {
                    iAdditionValue += DataUsedToCalcLRC[i];//
                }

                // 取字节累加值的第1个字节， int 包含 4 个字节
                byte[] byLatestTemp = BitConverter.GetBytes(iAdditionValue);
                bySumValue = byLatestTemp[0];
                
                // 取反
                bySumValue = Convert.ToByte(255 ^ bySumValue);
                //bySumValue = (byte)(255 - bySumValue);

                // +1
                bySumValue += 1;

                byLRCResult = new byte[2];
                string sLRCResult = bySumValue.ToString("X2");
                for (int i = 0; i < byLRCResult.Length; i++)
                {
                    byLRCResult[i] = Convert.ToByte(sLRCResult[i]);
                }
            }
            catch (Exception)// ex)
            {
                //Enqueue(ex.Message + "  " + ex.StackTrace);
            }

            return byLRCResult;
        }

        /// <summary>
        /// 计算LRC值，返回值的16进制字符串 -- 格式：地址(2个字符) + 功能码(2个字符) + 起始地址(4个字符) + 数据长度(4个字符)
        /// </summary>
        /// <param name="ModbusAsciiCommand">16进制字符串</param>
        /// <returns></returns>
        public static string CalcLRCString(string ModbusAsciiCommand)
        {
            try
            {
                if (string.IsNullOrEmpty(ModbusAsciiCommand) == true)
                {
                    throw new Exception("Modbus Ascii 命令字符串不能为空");
                }

                if (ModbusAsciiCommand[0] == ':')
                {
                    ModbusAsciiCommand.Remove(0, 1);
                }

                if (Strings.Left(ModbusAsciiCommand, 2) == "\r\n")
                {
                    ModbusAsciiCommand.Remove(ModbusAsciiCommand.Length - 2, 2);
                }

                if (ModbusAsciiCommand.Length % 2 != 0)
                {
                    throw new Exception("Modbus Ascii 命令字符串长度不正确，应该是2的整数倍");
                }

                // 执行写的时候，命令字符串的长度不是固定的
                //if (ModbusAsciiCommand.Length != 10)
                //{
                //    
                //}

                //byte[] byCommandStringToBytes = Encoding.ASCII.GetBytes(ModbusAsciiCommand);

                byte[] byBytesToCalcLRC = new byte[ModbusAsciiCommand.Length / 2];

                for (int i = 0; i < ModbusAsciiCommand.Length / 2; i++)
                {
                    string sTemp = Strings.Mid(ModbusAsciiCommand, i * 2 + 1, 2);

                    byBytesToCalcLRC[i] = TwoHexCharsToByte(sTemp);
                }
                
                // 计算字节求和值
                int iAdditionValue = 0;
                byte bySumValue = 0;
                for (int i = 0; i < byBytesToCalcLRC.Length; i++)//  byCommandStringToBytes
                {
                    iAdditionValue += byBytesToCalcLRC[i];//  byCommandStringToBytes
                }

                // 取字节累加值的第1个字节， int 包含 4 个字节
                byte[] byLatestTemp = BitConverter.GetBytes(iAdditionValue);
                bySumValue = byLatestTemp[0];
                
                // 取反
                bySumValue = Convert.ToByte(255 ^ bySumValue);
                //bySumValue = (byte)(255 - bySumValue);

                // +1
                bySumValue += 1;

                return bySumValue.ToString("X2");
            }
            catch (Exception)// ex)
            {
                //Enqueue(ex.Message + "  " + ex.StackTrace);
                return "";
            }
        }
        
        /// <summary>
        /// 将客户端返回的字节信息进行计算LRC，确认匹配OK就返回true，否则返回 false
        /// </summary>
        /// <param name="ReceivedBytesFromSlave">客户端返回的字节信息</param>
        /// <returns>匹配OK就返回true，否则返回 false</returns>
        public static bool CalcLRCForReceivedBytesFromSlave(byte[] ReceivedBytesFromSlave)
        {
            try
            {
                if (null == ReceivedBytesFromSlave)
                {
                    return false;
                }

                if (ReceivedBytesFromSlave.Length <= 4)
                {
                    throw new Exception("客户端返回的字符串信息长度不足以进行计算LRC");
                }
                
                //:         地址码         功能码    设置起始地址   设置值      LRC     回车+换行符
                //1字符     2字符          2字符     4字符          4字符      2字符    2字符
                //1字节     1字节          1字节     2字节          2字节      1字节    2字节

                // 求 LRC -- 设备地址(2个字符) + 功能码(2个字符) + 起始地址(4个字符) + 数据值(4个字符)

                byte[] byUsedToCalcLRC = new byte[ReceivedBytesFromSlave.Length - 3];
                Array.Copy(ReceivedBytesFromSlave, 1, byUsedToCalcLRC, 0, byUsedToCalcLRC.Length);

                byte[] byLRCResult = CalcLRCBytes(byUsedToCalcLRC);

                if (null == byLRCResult || byLRCResult.Length != 2)
                {
                    return false;
                }

                if (ReceivedBytesFromSlave[ReceivedBytesFromSlave.Length - 2] == byLRCResult[0])
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 将客户端返回的字符串信息进行计算LRC，确认匹配OK就返回true，否则返回 false
        /// </summary>
        /// <param name="ReceivedStringFromSlave">客户端返回的字符串信息</param>
        /// <returns>匹配OK就返回true，否则返回 false</returns>
        public static bool CalcLRCForReceivedStringFromSlave(string ReceivedStringFromSlave)
        {
            try
            {
                if (string.IsNullOrEmpty(ReceivedStringFromSlave) == true)
                {
                    return false;
                }

                if (ReceivedStringFromSlave.Length <=4)
                {
                    throw new Exception("客户端返回的字符串信息长度不足以进行计算LRC");
                }

                //:         地址码         功能码    设置起始地址   设置值      LRC     回车+换行符
                //1字符     2字符          2字符     4字符          4字符      2字符    2字符
                //1字节     1字节          1字节     2字节          2字节      1字节    2字节

                //求 LRC -- 设备地址(2个字符) + 功能码(2个字符) + 起始地址(4个字符) + 数据值(4个字符)

                //*****************************************
                //(COM1)发送字符串 - :01050000FF00FB
                //(COM1)收到字符串 - :01850179  -- 写从站线圈失败后，

                //(COM1)发送字符串 - :01050000FF00FB
                //(COM1)收到字符串 - :01050000FF00FB  -- 成功写从站后，从站返回的字符串内容与发送的字符串内容相同

                //:01050000FF00FB\r\n
                //FB
                string sReceivedLRC = Strings.Mid(ReceivedStringFromSlave, ReceivedStringFromSlave.Length - 3, 2);
                string sUsedToCalcLRC = Strings.Mid(ReceivedStringFromSlave, 2, ReceivedStringFromSlave.Length - 5);
                string sLRCCalcResult = CalcLRCString(sUsedToCalcLRC);

                if (string.IsNullOrEmpty(sLRCCalcResult) == true)
                {
                    return false;
                }

                if (sReceivedLRC == sLRCCalcResult)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 将2位16进制数字符串转换为10进制byte值，例：FF - 255
        /// </summary>
        /// <param name="TwoHexChars">2位16进制数字符串</param>
        /// <returns></returns>
        public static byte TwoHexCharsToByte(string TwoHexChars)
        {
            int iResultValue = 0;

            try
            {
                if (string.IsNullOrEmpty(TwoHexChars) == true || TwoHexChars.Length > 2)
                {
                    throw new Exception("执行转换的16进制字符串必须是不大于2个字符");
                }

                #region "将2位16进制数转换为10进制整数值"

                TwoHexChars = TwoHexChars.ToUpper();

                int iPowIndex = 0;
                int iLengthOfHexString = TwoHexChars.Length;

                for (int j = iLengthOfHexString - 1; j >= 0; j--)//for (int j = 0; j < TwoHexChars.Length; j++)
                {
                    char cSingleValue = TwoHexChars[j];//TwoHexChars[j];

                    iPowIndex = iLengthOfHexString - j - 1;

                    switch (cSingleValue)
                    {
                        case '0':
                            

                            break;

                        case '1':
                            iResultValue += 1 * Convert.ToInt32(Math.Pow(16, iPowIndex));
                            //if (j == 0)
                            //{
                            //    iResultValue += Convert.ToInt32(1) * 16;
                            //}
                            //else
                            //{
                            //    iResultValue += Convert.ToInt32(1);
                            //}

                            break;

                        case '2':
                            iResultValue += 2 * Convert.ToInt32(Math.Pow(16, iPowIndex));
                            //if (j == 0)
                            //{
                            //    iResultValue += Convert.ToInt32(2) * 16;
                            //}
                            //else
                            //{
                            //    iResultValue += Convert.ToInt32(2);
                            //}

                            break;

                        case '3':
                            iResultValue += 3 * Convert.ToInt32(Math.Pow(16, iPowIndex));
                            //if (j == 0)
                            //{
                            //    iResultValue += Convert.ToInt32(3) * 16;
                            //}
                            //else
                            //{
                            //    iResultValue += Convert.ToInt32(3);
                            //}

                            break;

                        case '4':
                            iResultValue += 4 * Convert.ToInt32(Math.Pow(16, iPowIndex));
                            //if (j == 0)
                            //{
                            //    iResultValue += Convert.ToInt32(4) * 16;
                            //}
                            //else
                            //{
                            //    iResultValue += Convert.ToInt32(4);
                            //}

                            break;

                        case '5':
                            iResultValue += 5 * Convert.ToInt32(Math.Pow(16, iPowIndex));
                            //if (j == 0)
                            //{
                            //    iResultValue += Convert.ToInt32(5) * 16;
                            //}
                            //else
                            //{
                            //    iResultValue += Convert.ToInt32(5);
                            //}

                            break;

                        case '6':
                            iResultValue += 6 * Convert.ToInt32(Math.Pow(16, iPowIndex));
                            //if (j == 0)
                            //{
                            //    iResultValue += Convert.ToInt32(6) * 16;
                            //}
                            //else
                            //{
                            //    iResultValue += Convert.ToInt32(6);
                            //}

                            break;

                        case '7':
                            iResultValue += 7 * Convert.ToInt32(Math.Pow(16, iPowIndex));
                            //if (j == 0)
                            //{
                            //    iResultValue += Convert.ToInt32(7) * 16;
                            //}
                            //else
                            //{
                            //    iResultValue += Convert.ToInt32(7);
                            //}

                            break;

                        case '8':
                            iResultValue += 8 * Convert.ToInt32(Math.Pow(16, iPowIndex));
                            //if (j == 0)
                            //{
                            //    iResultValue += Convert.ToInt32(8) * 16;
                            //}
                            //else
                            //{
                            //    iResultValue += Convert.ToInt32(8);
                            //}

                            break;

                        case '9':
                            iResultValue += 9 * Convert.ToInt32(Math.Pow(16, iPowIndex));
                            //if (j == 0)
                            //{
                            //    iResultValue += Convert.ToInt32(9) * 16;
                            //}
                            //else
                            //{
                            //    iResultValue += Convert.ToInt32(9);
                            //}

                            break;

                        case 'A':
                            iResultValue += 10 * Convert.ToInt32(Math.Pow(16, iPowIndex));
                            //if (j == 0)
                            //{
                            //    iResultValue += Convert.ToInt32(10 * 16);
                            //    //iValue1 += Convert.ToByte(10 * 16);
                            //}
                            //else
                            //{
                            //    iResultValue += Convert.ToInt32(10);
                            //    //iValue1 += Convert.ToByte(10);
                            //}

                            break;

                        case 'B':
                            iResultValue += 11 * Convert.ToInt32(Math.Pow(16, iPowIndex));
                            //if (j == 0)
                            //{
                            //    iResultValue += Convert.ToInt32(11 * 16);
                            //    //iValue1 += Convert.ToByte(11 * 16);
                            //}
                            //else
                            //{
                            //    iResultValue += Convert.ToInt32(11);
                            //    //iValue1 += Convert.ToByte(11);
                            //}

                            break;

                        case 'C':
                            iResultValue += 12 * Convert.ToInt32(Math.Pow(16, iPowIndex));
                            //if (j == 0)
                            //{
                            //    iResultValue += Convert.ToInt32(12 * 16);
                            //    //iValue1 += Convert.ToByte(12 * 16);
                            //}
                            //else
                            //{
                            //    iResultValue += Convert.ToInt32(12);
                            //    //iValue1 += Convert.ToByte(12);
                            //}

                            break;

                        case 'D':
                            iResultValue += 13 * Convert.ToInt32(Math.Pow(16, iPowIndex));
                            //if (j == 0)
                            //{
                            //    iResultValue += Convert.ToInt32(13 * 16);
                            //    //iValue1 += Convert.ToByte(13 * 16);
                            //}
                            //else
                            //{
                            //    iResultValue += Convert.ToInt32(13);
                            //    //iValue1 += Convert.ToByte(13);
                            //}

                            break;

                        case 'E':
                            iResultValue += 14 * Convert.ToInt32(Math.Pow(16, iPowIndex));
                            //if (j == 0)
                            //{
                            //    iResultValue += Convert.ToInt32(14 * 16);
                            //    //iValue1 += Convert.ToByte(14 * 16);
                            //}
                            //else
                            //{
                            //    iResultValue += Convert.ToInt32(14);
                            //    //iValue1 += Convert.ToByte(14);
                            //}

                            break;

                        case 'F':
                            iResultValue += 15 * Convert.ToInt32(Math.Pow(16, iPowIndex));
                            //if (j == 0)
                            //{
                            //    iResultValue += Convert.ToInt32(15 * 16);
                            //    //iValue1 += Convert.ToByte(15 * 16);
                            //}
                            //else
                            //{
                            //    iResultValue += Convert.ToInt32(15);
                            //    //iValue1 += Convert.ToByte(15);
                            //}

                            break;

                        default:
                            throw new Exception("执行转换的16进制字符为非法字符");
                    }
                }

                #endregion
            }
            catch (Exception)// ex)
            {
                //Enqueue(ex.Message + "  " + ex.StackTrace);
            }

            return (byte)iResultValue;
        }

        /// <summary>
        /// 字节转换为2为16进制字符
        /// </summary>
        /// <param name="ByteData">字节</param>
        /// <returns>是否成功执行</returns>
        public static string ByteToTwoHexChars(byte ByteData)
        {
            return ByteData.ToString("X2");;
        }

        /// <summary>
        /// 字节值按位转换为布尔数组
        /// </summary>
        /// <param name="ByteValue">字节值</param>
        /// <returns></returns>
        public static bool[] ByteToBitArray(byte ByteValue)
        {
            try
            {
                bool[] bResult = new bool[8];
                byte byTemp = 0x0;

                for (int i = 0; i < bResult.Length; i++)
                {
                    byTemp = Convert.ToByte(ByteValue >> i);
                    if ((byTemp & 1) == 1)
                    {
                        bResult[i] = true;
                    }
                    else
                    {
                        bResult[i] = false;
                    }
                }

                return bResult;
            }
            catch (Exception)// ex)
            {
                //Enqueue(ex.Message + ", " + ex.StackTrace);
                return null;
            }
        }

        /// <summary>
        /// 布尔数组转换为字节数组
        /// </summary>
        /// <param name="BitValue">布尔数组</param>
        /// <returns></returns>
        public static byte[] BitArrayToByte(bool[] BitValue)
        {
            try
            {
                if (BitValue.Length % 8 != 0)
                {
                    throw new Exception("bool[]数组的长度不是8的整数，即不是以整字节的形式，请修改参数数组的长度");
                }

                byte[] byResult = new byte[BitValue.Length / 8];
                byte byTemp = 0x0;

                // 以8个BOOL数组长度为一个字节来进行计算，可以用或
                for (int j = 0; j < byResult.Length; j++)
                {
                    byTemp = 0x0;
                    int iBeginIndex = j * 8;
                    for (int i = iBeginIndex; i < iBeginIndex + 8; i++)
                    {
                        if (BitValue[i] == true)
                        {
                            byTemp |= Convert.ToByte(0x1 << (i - iBeginIndex));//
                        }
                        else
                        {

                        }
                    }

                    byResult[j] = byTemp;
                }

                #region "Old codes"

                //byte[] byResult = new byte[BitValue.Length / 8];
                //int iTemp = 0x0;

                //// 以8个BOOL数组长度为一个字节来进行计算，可以用或
                //for (int j = 0; j < byResult.Length; j++)
                //{
                //    iTemp = 0x0;
                //    int iBeginIndex = j * 8;
                //    for (int i = iBeginIndex; i < iBeginIndex + 8; i++)
                //    {
                //        if (BitValue[i] == true)
                //        {
                //            iTemp |= 0x1 << (i - iBeginIndex);//Convert.ToByte(0x1 << i)
                //        }
                //        else
                //        {

                //        }
                //    }

                //    byte[] byIntToBytes = BitConverter.GetBytes(iTemp);
                //    byResult[j] = byIntToBytes[0];
                //}

                #endregion

                return byResult;
            }
            catch (Exception)// ex)
            {
                //索引超出了数组界限。
                //值对于无符号的字节太大或太小。
                //Enqueue(ex.Message + ", " + ex.StackTrace);
                return null;
            }
        }

        /// <summary>
        /// 将int整型值转换为字节数组
        /// </summary>
        /// <param name="IntValue">int整型值</param>
        /// <returns></returns>
        public static byte[] IntToBytes(int IntValue)
        {
            byte[] byResult = BitConverter.GetBytes(IntValue);
            return byResult;
        }

        /// <summary>
        /// 将int整型值转换为4个字符的16进制字符串
        /// </summary>
        /// <param name="IntValue">int整型值</param>
        /// <returns></returns>
        public static string IntToFourHexString(int IntValue)
        {
            return IntValue.ToString("X4");
        }

        /// <summary>
        /// 将4个字符的16进制字符串转换为int整型值
        /// </summary>
        /// <param name="HexString">4个字符的16进制字符串</param>
        /// <returns>int整型值</returns>
        public static int HexStringToInt(string HexString)
        {
            if (string.IsNullOrEmpty(HexString) == true)
            {
                return 0;
            }

            if (HexString.Length > 8)
            {
                return 0;
            }

            try
            {
                return (int)HexStringToLong(HexString);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>
        /// 将16进制字符串转换为long整型值
        /// </summary>
        /// <param name="HexString">16进制字符串</param>
        /// <returns></returns>
        public static long HexStringToLong(string HexString)
        {
            if (string.IsNullOrEmpty(HexString) == true)
            {
                return 0;
            }

            long lResultValue = 0;
            int iLengthOfHexString = HexString.Length;

            HexString = HexString.ToUpper();

            int iIndexOfPow = 0;

            try
            {
                //EFB47820 : 从最后一个字符开始计算16进制对应的10进制数值
                for (int j = iLengthOfHexString - 1; j >= 0; j--)
                {
                    #region "将2位16进制数转换为10进制整数值"

                    if (j == 1)
                    {
                        
                    }

                    char cSingleValue = HexString[j];

                    iIndexOfPow = iLengthOfHexString - j - 1;

                    switch (cSingleValue)
                    {
                        case '0':
                            //lResultValue += 0;

                            break;

                        case '1':
                            lResultValue += 1 * Convert.ToInt64(Math.Pow(16, iIndexOfPow));

                            break;

                        case '2':
                            lResultValue += 2 * Convert.ToInt64(Math.Pow(16, iIndexOfPow));

                            break;

                        case '3':
                            lResultValue += 3 * Convert.ToInt64(Math.Pow(16, iIndexOfPow));

                            break;

                        case '4':
                            lResultValue += 4 * Convert.ToInt64(Math.Pow(16, iIndexOfPow));

                            break;

                        case '5':
                            lResultValue += 5 * Convert.ToInt64(Math.Pow(16, iIndexOfPow));

                            break;

                        case '6':
                            lResultValue += 6 * Convert.ToInt64(Math.Pow(16, iIndexOfPow));

                            break;

                        case '7':
                            lResultValue += 7 * Convert.ToInt64(Math.Pow(16, iIndexOfPow));

                            break;

                        case '8':
                            lResultValue += 8 * Convert.ToInt64(Math.Pow(16, iIndexOfPow));

                            break;

                        case '9':

                            lResultValue += 9 * Convert.ToInt64(Math.Pow(16, iIndexOfPow));

                            break;

                        case 'A':
                            lResultValue += 10 * Convert.ToInt64(Math.Pow(16, iIndexOfPow));

                            break;

                        case 'B':
                            lResultValue += 11 * Convert.ToInt64(Math.Pow(16, iIndexOfPow));

                            break;

                        case 'C':
                            lResultValue += 12 * Convert.ToInt64(Math.Pow(16, iIndexOfPow));

                            break;

                        case 'D':
                            lResultValue += 13 * Convert.ToInt64(Math.Pow(16, iIndexOfPow));

                            break;

                        case 'E':
                            lResultValue += 14 * Convert.ToInt64(Math.Pow(16, iIndexOfPow));

                            break;

                        case 'F':
                            lResultValue += 15 * Convert.ToInt64(Math.Pow(16, iIndexOfPow));

                            break;

                        default:
                            throw new Exception("执行转换的16进制字符为非法字符");
                    }

                    #endregion

                    iIndexOfPow++;
                }
            }
            catch (Exception)// ex)
            {
                //Enqueue(ex.Message + ", " + ex.StackTrace);
            }

            return lResultValue;
        }

        #endregion

        #endregion

    }//class

    /// <summary>
    /// 串口通讯波特率：bps
    /// </summary>
    public enum eBaudRate
    {
        /// <summary>
        /// 波特率(bps)：用户自定义
        /// </summary>
        Rate_UserDefine,

        /// <summary>
        /// 波特率(bps)：75
        /// </summary>
        Rate_75,

        /// <summary>
        /// 波特率(bps)：110
        /// </summary>
        Rate_110,

        /// <summary>
        /// 波特率(bps)：134
        /// </summary>
        Rate_134,

        /// <summary>
        /// 波特率(bps)：150
        /// </summary>
        Rate_150,

        /// <summary>
        /// 波特率(bps)：300
        /// </summary>
        Rate_300,

        /// <summary>
        /// 波特率(bps)：600
        /// </summary>
        Rate_600,

        /// <summary>
        /// 波特率(bps)：1200
        /// </summary>
        Rate_1200,

        /// <summary>
        /// 波特率(bps)：1800
        /// </summary>
        Rate_1800,

        /// <summary>
        /// 波特率(bps)：2400
        /// </summary>
        Rate_2400,

        /// <summary>
        /// 波特率(bps)：4800
        /// </summary>
        Rate_4800,

        /// <summary>
        /// 波特率(bps)：7200
        /// </summary>
        Rate_7200,

        /// <summary>
        /// 波特率(bps)：9600
        /// </summary>
        Rate_9600,

        /// <summary>
        /// 波特率(bps)：14400
        /// </summary>
        Rate_14400,

        /// <summary>
        /// 波特率(bps)：19200
        /// </summary>
        Rate_19200,

        /// <summary>
        /// 波特率(bps)：38400
        /// </summary>
        Rate_38400,

        /// <summary>
        /// 波特率(bps)：57600
        /// </summary>
        Rate_57600,

        /// <summary>
        /// 波特率(bps)：115200
        /// </summary>
        Rate_115200,

        /// <summary>
        /// ：128000
        /// </summary>
        Rate_128000
    }

}//namespace