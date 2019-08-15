using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ModbusAscii
{
    /// <summary>
    /// Modbus错误代码
    /// </summary>
    public sealed class CModbusErrorCode
    {
        #region "Modbus错误代码"

        /// <summary>
        /// 非法功能码 - 0x01：对于服务器或从站来说，询问中接收到的功能码是不可允许的操作。这也许是因为功能码仅仅适用于新设备而在被选单元中是不可实现的。同时，还指出服务器或从站在错误状态中处理这种请求，例如：因为它是未配置的，并且要求返回寄存器值。
        /// </summary>
        public const byte IllegalFunction = 0x01;

        /// <summary>
        /// 非法数据地址 - 0x02：对应服务器或从站来说，询问中接收到的数据地址是不可允许的地址。特别是，参考号和传输长度的组合是无效的。对于带有100个寄存器的控制器来说，带有偏移量 96 和长度 4 的请求会成功，带有偏移量 96 和长度 5 的请求将产生异常码 02.
        /// </summary>
        public const byte AddressOverrange = 0x02;

        /// <summary>
        /// 读取长度超过最大值，非法数据值 - 0x03：对于服务器或从站来说，询问中包括的值是不可允许的值。这个值指示了组合请求剩余结构中的故障，例如：隐含长度是不正确的。并不意味着，因为 MODBUS 协议不知道任何特殊寄存器的任何特殊值的重要意义，寄存器中被提交存储的数据项有一个应用程序期望之外的值。
        /// </summary>
        public const byte ReadLengthOverrange = 0x03;

        /// <summary>
        /// 读写异常，从站设备故障 - 0x04：当服务器或从站正在设法执行请求的操作时，产生不可重新获得的差错。
        /// </summary>
        public const byte ReadWriteException = 0x04;

        /// <summary>
        /// 确认 - 0x05：与编程命令一起使用。服务器或从站已经接受请求，并且正在处理这个请求，但是需要长的持续时间进行这些操作。返回这个响应防止在客户机或主站中发生超时错误。客户机或主站可以继续发送轮询程序完成报文来确定是否完成处理。
        /// </summary>
        public const byte Acknowledgement = 0x05;

        /// <summary>
        /// 从属设备忙 - 0x06：与编程命令一起使用。服务器或从站正在处理长持续时间的程序命令。当服务器或从站空闲时，用户或主机应该稍后重新传输报文
        /// </summary>
        public const byte SlaveIsBusy = 0x06;

        /// <summary>
        /// 存储奇偶性差错 - 0x08：与功能码 20 和 21 以及参考类型 6 一起使用，指示扩展文件区不能通过一致性校验。服务器或从站设法读取文件记录，但是在存储器红发现一个奇偶校验错误。客户机或者主站可以重新发送请求，但可以在服务器或从站设备上要求服务。
        /// </summary>
        public const byte StoraryParityError = 0x08;

        /// <summary>
        /// 不可用网关路径 - 0x0A：与网关一起使用，指示网关不能为处理请求分配输入端口至输出端口的内部通信路径。通常意味着网关是错误配置的或过载的。
        /// </summary>
        public const byte NotAvailableGatewayPath = 0x0A;

        /// <summary>
        /// 网关目标设备响应失败 - 0x0B：与网关一起使用，指示没有从目标设备中获得响应。通常意味着设备未在网络中。
        /// </summary>
        public const byte NotResponseFromGatewayStation = 0x0B;

        #endregion

    }//class

}//namespace