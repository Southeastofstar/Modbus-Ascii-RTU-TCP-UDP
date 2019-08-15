using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ModbusAscii
{
    /// <summary>
    /// Modbus通讯功能码
    /// </summary>
    public sealed class CModbusFunctionCode
    {
        #region "Modbus通讯功能码"

        #region "读"

        /// <summary>
        /// 【读】读取线圈 - 0x01
        /// </summary>
        public const byte ReadCoil = 0x01;

        /// <summary>
        /// 【读】读取离散量(获取一组开关输入的当前状态) - 0x02
        /// </summary>
        public const byte ReadInputSignal = 0x02;

        /// <summary>
        /// 【读】读取一个或多个寄存器，取得当前的二进制值 - 0x03
        /// </summary>
        public const byte ReadRegister = 0x03;

        /// <summary>
        /// 【读】读取一个或多个输入寄存器，取得当前的二进制值 - 0x04
        /// </summary>
        public const byte ReadInputRegister = 0x04;

        /// <summary>
        /// 【读】读取异常状态，取得 8 个内部线圈的通断状态，这 8 个线圈的地址由控制器决定 - 0x07
        /// </summary>
        public const byte ReadErrorStatus = 0x07;

        /// <summary>
        /// 【读】回送诊断校验：把诊断校验报文送从机，以对通信处理进行评鉴 - 0x08
        /// </summary>
        public const byte SendBackCheckToSlave = 0x08;

        /// <summary>
        /// 【读】报告从机标识：可使主机判断编址从机的类型及该从机运行指示灯的状态 - 0x11
        /// </summary>
        public const byte MasterAskSlaveToReportSlaveID = 0x11;

        #endregion

        #region "写"

        /// <summary>
        /// 【写】写单个线圈，强置一个逻辑线圈的通断状态 - 0x05
        /// </summary>
        public const byte WriteCoil = 0x05;
        
        /// <summary>
        /// 【写】写单个寄存器，把具体二进制值写入一个保存寄存器 - 0x06
        /// </summary>
        public const byte WriteRegister = 0x06;

        /// <summary>
        /// 【写】写多个线圈：强置一串连续逻辑线圈的通断 - 0x0F
        /// </summary>
        public const byte WriteMultiCoil = 0x0F;

        /// <summary>
        /// 【写】写多个寄存器：把具体的二进制值装入一串连续的保持寄存器 - 0x10
        /// </summary>
        public const byte WriteMultiRegister = 0x10;

        /// <summary>
        /// 【写】重置通信链路：发生非可修改错误后，使从机复位于已知状态，可重置顺序字节 - 0x13
        /// </summary>
        public const byte ResetCommLinkRoute = 0x13;
        
        #endregion

        #endregion

    }//class

}//namespace