using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualBasic;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

using ModbusComm;
using ThreadLock;
using Converter;
using Converter.Modbus;

namespace ModbusCommTest
{
    public partial class Form1 : Form
    {


        bool bUseSocketToTest = true;

        CModbusSocket ModbusSocket = null;

        CModbusUDP ModbusUDP = null;

        CModbusTCP ModbusTCP = null;
        

        /// <summary>
        /// COM1
        /// </summary>
        CModbusRTU ModbusRTU = new CModbusRTU("PDN", "COM1");

        /// <summary>
        /// COM3
        /// </summary>
        CModbusAscii ModbusAscii = new CModbusAscii("PDN", "COM3");//

        public Form1()
        {
            InitializeComponent();
        }

        private void btnClosePort_Click(object sender, EventArgs e)
        {
            if (null != ModbusAscii)
            {
                ModbusAscii.Close();
            }

            if (null != ModbusRTU)
            {
                ModbusRTU.Close();
            }
        }

        private void btnOpenPort_Click(object sender, EventArgs e)
        {
            

            if (null != ModbusAscii)
            {
                ModbusAscii.Open();
            }

            if (null != ModbusRTU)
            {
                ModbusRTU.Open();
            }
        }

        private void btnCalcLRC_Click(object sender, EventArgs e)
        {
            //:01030000000AF2

            try
            {
                string sLRCResult = CModbusAscii.CalcLRCString(txtStringUsedToCalcLRC.Text);
                rtbLog.AppendText("LRC 结果字符串：" + sLRCResult + "\r\n");

                byte[] byLRCResult = CModbusAscii.CalcLRCBytes(txtStringUsedToCalcLRC.Text);
                sLRCResult = "";
                for (int i = 0; i < byLRCResult.Length; i++)
                {
                    sLRCResult += byLRCResult[i].ToString("X2");
                }
                rtbLog.AppendText("LRC 结果字节数组转字符串：" + sLRCResult + "\r\n");


                #region "Old codes"

                //string sNewCmd = "010300040002";
                //byte[] byNewResults = Encoding.ASCII.GetBytes(sNewCmd);
                //string sHexString = "";
                //for (int i = 0; i < byNewResults.Length; i++)
                //{
                //    sHexString += byNewResults[i].ToString("X2");
                //}


                ////txtLRCResult.Text = LRC(sHexString);
                //txtLRCResult.Text = LRC("31303030315E315E31303030325E315E31303030375E39395E31303032325E36353631335E");//303130333030303430303032

                //**************************************

                #region "OK - 1"

                ////020100000008  F5
                ////010300040002  F6
                //string sNewCmd = "010300040002";   
                //string sTempUse = "";

                //byte[] byLatestMethod = new byte[sNewCmd.Length / 2];

                //int x = 0;

                //for (int i = 0; i < sNewCmd.Length / 2; i++)
                //{
                //    byLatestMethod[x] = Convert.ToByte(Strings.Mid(sNewCmd, i * 2 + 1, 2));
                //    x++;
                //}

                ////byte[] byDataCompare = CLRC.LRC(byLatestMethod);
                ////for (int i = 0; i < byDataCompare.Length; i++)
                ////{
                ////    sTempUse += byDataCompare[i].ToString("X2");
                ////}
                ////rtbLog.AppendText(sNewCmd + " 计算LRC后的值转换为16进制：" + sTempUse + "\r\n");
                
                //int iAdditionValue=0;
                //byte bySumValue = 0;
                //for (int i = 0; i < byLatestMethod.Length; i++)
                //{
                //    iAdditionValue += byLatestMethod[i];
                //}

                //sTempUse = sNewCmd + " 计算字节求和值：" + iAdditionValue.ToString() + " 转换为16进制：" + iAdditionValue.ToString("X2") + "\r\n";
                //rtbLog.AppendText(sTempUse);
                //byte[] byLatestTemp = BitConverter.GetBytes(iAdditionValue);
                //bySumValue = byLatestTemp[0];

                //txtLRCResult.Text = bySumValue.ToString("X2");
                //rtbLog.AppendText(sNewCmd + " 计算字节求和值：" + bySumValue.ToString() + " 转换为16进制：" + bySumValue.ToString("X2") + "\r\n");

                //bySumValue = Convert.ToByte(255 ^ bySumValue);

                ////bySumValue = (byte)(255 - bySumValue);
                //rtbLog.AppendText("取反后值为：" + bySumValue.ToString() + " 转换为16进制：" + bySumValue.ToString("X2") + "\r\n");

                //bySumValue += 1;
                //rtbLog.AppendText("+1后值为：" + bySumValue.ToString() + " 转换为16进制：" + bySumValue.ToString("X2") + "\r\n");

                //byte[] byNewResults = Encoding.ASCII.GetBytes(sNewCmd);

                ////// add LRC check
                //byte modbus_lrc = 0;// CLRC.LRCByte(byNewResults);

                //byNewResults = CLRC.LRC(byNewResults);


                //// 020100000008
                ////1 - Addtion Result: 0B
                ////2 - Bit reversal 1:F4
                ////3- +1: F5


                //int sum = 0;

                //bool bUseAddMethod = true;

                //if (bUseAddMethod == false)
                //{
                //    for (int i = 0; i < byNewResults.Length; i++)
                //    {
                //        sum ^= byNewResults[i];
                //    }
                //}
                //else
                //{
                //    for (int i = 0; i < byNewResults.Length; i++)
                //    {
                //        sum += byNewResults[i];
                //    }

                //    string sTemp = sum.ToString("X2");

                //    if (false)
                //    {
                //        sum = 255 - sum;
                //        sum += 1;
                //    }
                //    else
                //    {
                //        //sum = sum % 256;
                //        //sum = 256 - sum;

                //        //***********************
                //        sum = 255 - sum;
                //        sum = sum + 1;
                //    }
                //}


                //byte[] LRC = new byte[] { (byte)sum };


                ////**************************************
                //string sLrcResultString = LRC[0].ToString("X2");
                //byte[] byLRCResultBytes = new byte[2];
                ////for (int i = 0; i < byLRCResultBytes.Length; i++)
                ////{
                ////    byLRCResultBytes[i] = Convert.ToByte(sLrcResultString[i]);
                ////}
                ////byLRCResultBytes[0] = Convert.ToByte(sLrcResultString[0]);
                ////byLRCResultBytes[1] = Convert.ToByte(sLrcResultString[1]);

                //byLRCResultBytes = Encoding.ASCII.GetBytes(sLrcResultString);

                //// Translate to ascii information
                //byte[] byFianlBytes = null;

                //byFianlBytes = CConversion.SpliceTwoByteArray(CConversion.SpliceTwoByteArray(CConversion.SpliceTwoByteArray(new byte[] { 0x3A }, byNewResults), byLRCResultBytes), new byte[] { 0x0D, 0x0A });
                ////byFianlBytes = CConversion.SpliceTwoByteArray(CConversion.SpliceTwoByteArray(new byte[] { 0x3A }, byNewResults), new byte[] { 0x0D, 0x0A });

                //string sTempHex = "";
                //for (int i = 0; i < byFianlBytes.Length; i++)
                //{
                //    sTempHex += byFianlBytes[i].ToString("X2") + " ";
                //}
                //txtLRCResult.Text = sTempHex;
                
                #endregion

                //****************************
                //string sData = "01050000FF00";//"3A 30 31 30 35 30 30 30 30 46 46 30 30 46 42 0D 0A"
                //string[] sSplitData = sData.Split(' ');
                //byte[] byResult = new byte[sSplitData.Length];
                //char[] cResult = new char[sSplitData.Length];
                //string sResult = "";



                //for (int i = 0; i < sSplitData.Length; i++)
                //{
                //    sResult += sSplitData[i];

                //    int iValue =  Convert.ToInt32(sSplitData[i]);
                //    ////byResult[i] = Convert.ToByte(sSplitData[i]);
                //    //cResult[i] = Convert.ToChar(iValue);
                //    ////sResult += Convert.ToString(byResult[i]);
                //}



                //txtLRCResult.Text = sResult;

                //byte byTest = 124;

                //string sTest = byTest.ToString("X2");

                //byte[] byTestResult = CConversion.StringToBytes(sTest);

                //string sFinalResult = "";

                //for (int i = 0; i < byTestResult.Length; i++)
                //{
                //    sFinalResult += byTestResult[i].ToString("X2");
                //}



                //byte[] byData = Encoding.UTF8.GetBytes("010300040002");  //:010300040002        CConversion.StringToBytes(":01030000000A");// 

                //byte bySingleByte = CLRC.LRCByte(byData);


                //byData = CLRC.LRC(byData);

                //if (CLRC.CheckLRC(byData) == true)
                //{

                //}
                //else
                //{

                //}

                //txtLRCResult.Text = bySingleByte.ToString("X2");

                //string sNewCmd = "010300040002";//:
                //byte[] byNewResults = new byte[sNewCmd.Length * 2];
                //for (int i = 0; i < sNewCmd.Length; i++)
                //{
                //    byte byTemp = Convert.ToByte(sNewCmd[i]);
                //    string sTemp = byTemp.ToString("X2");
                //    byNewResults[i * 2] = Convert.ToByte(sTemp[0]);
                //    byNewResults[i * 2+1] = Convert.ToByte(sTemp[1]);
                //}


                //byte byNewSingleByte = CLRC.LRCByte(byNewResults);

                //txtLRCResult.Text = byNewSingleByte.ToString("X2");

                //byte[] byFianlBytes = Encoding.UTF8.GetBytes(byNewSingleByte.ToString("X2"));

                //string sFinalLRCResult = "";
                //for (int i = 0; i < byFianlBytes.Length; i++)
                //{
                //    sFinalLRCResult += byFianlBytes[i].ToString("X2");
                //}


                //// add LRC check
                //byte[] modbus_lrc = CLRC.LRC(Encoding.UTF8.GetBytes(sNewCmd));

                //// Translate to ascii information
                //byte[] modbus_ascii = CConversion.BytesToAsciiBytes(modbus_lrc);

                //// add head and end informarion
                //byFianlBytes = CConversion.SpliceTwoByteArray(CConversion.SpliceTwoByteArray(new byte[] { 0x3A }, modbus_ascii), new byte[] { 0x0D, 0x0A });
                //byFianlBytes = CConversion.SpliceTwoByteArray(CConversion.SpliceTwoByteArray(new byte[] { 0x3A }, modbus_lrc), new byte[] { 0x0D, 0x0A });

                //string sTempHex = "";
                //for (int i = 0; i < byFianlBytes.Length; i++)
                //{
                //    sTempHex += byFianlBytes[i].ToString("X2") + " ";
                //}
                //txtLRCResult.Text = sTempHex;
                //ModbusAsc.WriteBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, true);

                #endregion
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        bool bWriteBitValue = false;

        private void btn16HexStringWithSpaceConvertToAscii_Click(object sender, EventArgs e)
        {
            txtAsciiResultOfHexStringWithSpaceSplit.Text = CConverter.HexStringSplitByCharToASCIIString(txtHexStringWithSpaceSplit.Text);
        }

        bool bReadWriteArray = false;

        private void chkReadWriteArray_CheckedChanged(object sender, EventArgs e)
        {
            bReadWriteArray = chkReadWriteArray.Checked;
        }

        #region "ModbusAscii - ok"

        #region "Coil - ok"

        // ok
        private void btnAsciiWriteCoil_Click(object sender, EventArgs e)
        {
            try
            {
                //bool bWriteSingleCoil = false;

                if (bReadWriteArray == false)//bWriteSingleCoil
                {
                    //byte[] byCmd = ModbusAscii.HexCmdConvertToBytesIncludeLRC(txtStringUsedToCalcLRC.Text);

                    //txtLRCResult.Text = ModbusAscii.BytesToHexStringSplitByChar(byCmd);
                    bWriteBitValue = !bWriteBitValue;

                    if (ModbusAscii.WriteCoilBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, bWriteBitValue) == true)//1111      
                    {
                        rtbLog.AppendText(string.Format("成功执行写命令:{0}\r\n", bWriteBitValue));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行写命令\r\n");
                    }
                }
                else
                {
                    bool[] bDataToBeSent = new bool[16];

                    Random rndData = new Random();

                    string sSentBools = "";

                    for (int i = 0; i < bDataToBeSent.Length; i++)
                    {
                        if (rndData.Next(1, 16) % 2 == 0)
                        {
                            bDataToBeSent[i] = true;
                        }
                        else
                        {
                            bDataToBeSent[i] = false;
                        }

                        sSentBools += bDataToBeSent[i].ToString() + " ";
                    }

                    rtbLog.AppendText("发送值[0~" + (bDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                    if (ModbusAscii.WriteCoilBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, bDataToBeSent) == true)//1111      
                    {
                        rtbLog.AppendText("成功执行写命令\r\n");
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行写命令\r\n");
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusAscii.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnAsciiReadCoil_Click(object sender, EventArgs e)
        {
            try
            {
                //string sReadResult = "";

                //:010100000001FD
                //从站返回字符串：:0181017D - 非法命令

                //从站返回字符串：:01010100FD - 从站返回值OK

                //ASCII码 - :010100000001FD
                //发送的字节 - 3A 30 31 30 31 30 30 30 30 30 30 30 31 46 44 0D 0A
                //接收的字节 - 3A 30 31 30 31 30 31 30 30 46 44 0D 0A

                //000011-Tx:3A 30 31 30 31 30 30 30 30 30 30 30 31 46 44 0D 0A
                //000012-Rx:3A 30 31 30 31 30 31 30 30 46 44 0D 0A
                //000013-Tx:3A 30 31 30 31 30 30 30 30 30 30 30 31 46 44 0D 0A
                //000014-Rx:3A 30 31 30 31 30 31 30 31 46 43 0D 0A

                //bool bReadSingleBit = false;

                if (bReadWriteArray == false)//bReadSingleBit
                {
                    bool bReadBitValue = false;
                    if (ModbusAscii.ReadCoilBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref bReadBitValue))//   (ModbusAscii.ReadBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 1, out sReadResult))
                    {
                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }
                else
                {
                    bool[] bReadBitsValue = null;
                    if (ModbusAscii.ReadCoilBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref bReadBitsValue))//   (ModbusAscii.ReadBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 1, out sReadResult))  
                    {
                        //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                        string sBitsResults = "";
                        for (int i = 0; i < bReadBitsValue.Length; i++)
                        //for (int i = bReadBitsValue.Length - 1; i >= 0; i--)
                        {
                            sBitsResults += bReadBitsValue[i].ToString() + " ";
                        }

                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }

                
                //rtbLog.AppendText("从站返回字符串：" + sReadResult + "\r\n");
                //rtbLog.AppendText("从站返回字符串转换为字节：" + ModbusAscii.BytesToHexStringSplitByChar(Encoding.ASCII.GetBytes(sReadResult)) + "\r\n");

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusAscii.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
                
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        
        // ok
        private void btnAsciiReadCoilByte_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        byte byReadValue = 0;
                        if (ModbusAscii.ReadCoilByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref byReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", byReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        sbyte byReadValue = 0;
                        if (ModbusAscii.ReadCoilByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref byReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", byReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        byte[] byReadValue = null;
                        if (ModbusAscii.ReadCoilByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref byReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < byReadValue.Length; i++)
                            {
                                sBitsResults += byReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        sbyte[] byReadValue = null;
                        if (ModbusAscii.ReadCoilByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref byReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < byReadValue.Length; i++)
                            {
                                sBitsResults += byReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusAscii.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnAsciiReadCoilWord_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort stReadValue = 0;
                        if (ModbusAscii.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref stReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", stReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        short stReadValue = 0;
                        if (ModbusAscii.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref stReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", stReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }

                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort[] stReadValue = null;
                        if (ModbusAscii.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref stReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < stReadValue.Length; i++)
                            {
                                sBitsResults += stReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        short[] stReadValue = null;
                        if (ModbusAscii.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref stReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < stReadValue.Length; i++)
                            {
                                sBitsResults += stReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusAscii.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnAsciiWriteCoilByte_Click(object sender, EventArgs e)
        {
            try
            {
                Random rndData = new Random();

                if (bReadWriteArray == false)
                {
                    //int iTemp = rndData.Next(0, 255);
                    int iTemp = bReadUnsignedData ? rndData.Next(byte.MinValue, byte.MaxValue) : rndData.Next(sbyte.MinValue, sbyte.MaxValue);

                    byte[] byTemp = BitConverter.GetBytes(iTemp);

                    if (bReadUnsignedData == true)
                    {
                        byte byWriteValue = byTemp[0];

                        if (ModbusAscii.WriteCoilByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, byWriteValue) == true)
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", byWriteValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        sbyte byWriteValue = (sbyte)byTemp[0];

                        if (ModbusAscii.WriteCoilByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, byWriteValue) == true)
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", byWriteValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        byte[] byDataToBeSent = new byte[2];

                        string sSentBools = "";

                        for (int i = 0; i < byDataToBeSent.Length; i++)
                        {
                            int iTemp = rndData.Next(byte.MinValue, byte.MaxValue); //rndData.Next(0, 255);
                            byte[] byTemp = BitConverter.GetBytes(iTemp);
                            byte byWriteValue = byTemp[0];
                            byDataToBeSent[i] = byWriteValue;

                            sSentBools += byDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (byDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusAscii.WriteCoilByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, byDataToBeSent) == true)//1111      
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        sbyte[] byDataToBeSent = new sbyte[2];

                        string sSentBools = "";

                        for (int i = 0; i < byDataToBeSent.Length; i++)
                        {
                            int iTemp = rndData.Next(sbyte.MinValue, sbyte.MaxValue);//rndData.Next(0, 255);
                            byte[] byTemp = BitConverter.GetBytes(iTemp);
                            byte byWriteValue = byTemp[0];
                            byDataToBeSent[i] = (sbyte)byWriteValue;

                            sSentBools += byDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (byDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusAscii.WriteCoilByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, byDataToBeSent) == true)//1111      
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusAscii.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnAsciiWriteCoilWord_Click(object sender, EventArgs e)
        {
            try
            {
                Random rndData = new Random();

                if (bReadWriteArray == false)
                {
                    //int iTemp = bReadUnsignedData ? rndData.Next(-32768, 32767) : rndData.Next(-32768, 32767);
                    int iTemp = bReadUnsignedData ? rndData.Next(ushort.MinValue, ushort.MaxValue) : rndData.Next(short.MinValue, short.MaxValue);

                    byte[] byTemp = BitConverter.GetBytes(iTemp);

                    if (bReadUnsignedData == true)
                    {
                        ushort stWriteValue = BitConverter.ToUInt16(byTemp, 0);

                        if (ModbusAscii.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, stWriteValue) == true)
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", stWriteValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        short stWriteValue = BitConverter.ToInt16(byTemp, 0);

                        if (ModbusAscii.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, stWriteValue) == true)
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", stWriteValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort[] stDataToBeSent = new ushort[2];

                        string sSentBools = "";

                        for (int i = 0; i < stDataToBeSent.Length; i++)
                        {
                            int iTemp = rndData.Next(ushort.MinValue, ushort.MaxValue);//rndData.Next(-32768, 32767);
                            byte[] byTemp = BitConverter.GetBytes(iTemp);
                            stDataToBeSent[i] = BitConverter.ToUInt16(byTemp, 0);

                            sSentBools += stDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (stDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusAscii.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, stDataToBeSent) == true)
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        short[] stDataToBeSent = new short[2];

                        string sSentBools = "";

                        for (int i = 0; i < stDataToBeSent.Length; i++)
                        {
                            int iTemp = rndData.Next(short.MinValue, short.MaxValue);//rndData.Next(-32768, 32767);
                            byte[] byTemp = BitConverter.GetBytes(iTemp);
                            stDataToBeSent[i] = BitConverter.ToInt16(byTemp, 0);

                            sSentBools += stDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (stDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusAscii.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, stDataToBeSent) == true)
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusAscii.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnAsciiReadCoilInt_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        uint iReadValue = 0;
                        if (ModbusAscii.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref iReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", iReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        int iReadValue = 0;
                        if (ModbusAscii.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref iReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", iReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        uint[] iReadValue = null;
                        if (ModbusAscii.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref iReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < iReadValue.Length; i++)
                            {
                                sBitsResults += iReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        int[] iReadValue = null;
                        if (ModbusAscii.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref iReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < iReadValue.Length; i++)
                            {
                                sBitsResults += iReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusAscii.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnAsciiReadCoilLong_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong lReadValue = 0;
                        if (ModbusAscii.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref lReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", lReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        long lReadValue = 0;
                        if (ModbusAscii.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref lReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", lReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong[] lReadValue = null;
                        if (ModbusAscii.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref lReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < lReadValue.Length; i++)
                            {
                                sBitsResults += lReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        long[] lReadValue = null;
                        if (ModbusAscii.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref lReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < lReadValue.Length; i++)
                            {
                                sBitsResults += lReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusAscii.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnAsciiWriteCoilInt_Click(object sender, EventArgs e)
        {
            try
            {
                Random rndData = new Random();

                if (bReadWriteArray == false)
                {
                    //int iTemp = bReadUnsignedData ? rndData.Next(ushort.MinValue, ushort.MaxValue) : rndData.Next(short.MinValue, short.MaxValue);

                    if (bReadUnsignedData == true)
                    {
                        uint iTemp = (uint)rndData.Next(int.MinValue, int.MaxValue);
                        if (ModbusAscii.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, iTemp) == true)
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", iTemp.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        int iTemp = rndData.Next(int.MinValue, int.MaxValue);
                        if (ModbusAscii.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, iTemp) == true)
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", iTemp.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        uint[] iDataToBeSent = new uint[2];

                        string sSentBools = "";

                        for (int i = 0; i < iDataToBeSent.Length; i++)
                        {
                            iDataToBeSent[i] = (uint)rndData.Next(int.MinValue, int.MaxValue);
                            sSentBools += iDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (iDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusAscii.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, iDataToBeSent) == true)
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        int[] iDataToBeSent = new int[2];

                        string sSentBools = "";

                        for (int i = 0; i < iDataToBeSent.Length; i++)
                        {
                            iDataToBeSent[i] = rndData.Next(int.MinValue, int.MaxValue);
                            sSentBools += iDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (iDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusAscii.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, iDataToBeSent) == true)
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusAscii.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnAsciiWriteCoilLong_Click(object sender, EventArgs e)
        {
            try
            {
                Random rndData = new Random();

                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong lTemp = (ulong)(rndData.Next(int.MinValue, int.MaxValue) * rndData.Next(int.MinValue, int.MaxValue));
                        if (ModbusAscii.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, lTemp) == true)
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", lTemp.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        long lTemp = rndData.Next(int.MinValue, int.MaxValue) * rndData.Next(int.MinValue, int.MaxValue);
                        if (ModbusAscii.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, lTemp) == true)
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", lTemp.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong[] lDataToBeSent = new ulong[2];

                        string sSentBools = "";

                        for (int i = 0; i < lDataToBeSent.Length; i++)
                        {
                            lDataToBeSent[i] = (ulong)(rndData.Next(int.MinValue, int.MaxValue) * rndData.Next(int.MinValue, int.MaxValue));
                            sSentBools += lDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (lDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusAscii.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, lDataToBeSent) == true)
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        long[] lDataToBeSent = new long[2];

                        string sSentBools = "";

                        for (int i = 0; i < lDataToBeSent.Length; i++)
                        {
                            lDataToBeSent[i] = rndData.Next(int.MinValue, int.MaxValue) * rndData.Next(int.MinValue, int.MaxValue);
                            sSentBools += lDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (lDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusAscii.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, lDataToBeSent) == true)
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusAscii.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        #endregion

        #region "Input - ok"

        // ok
        private void btnAsciiReadInputBit_Click(object sender, EventArgs e)
        {
            try
            {
                //bool bReadSingleBit = false;

                if (bReadWriteArray == false)//bReadSingleBit
                {
                    bool bReadBitValue = false;
                    if (ModbusAscii.ReadInputBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref bReadBitValue))//   (ModbusAscii.ReadBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 1, out sReadResult))
                    {
                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }
                else
                {
                    bool[] bReadBitsValue = null;
                    if (ModbusAscii.ReadInputBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref bReadBitsValue))//   (ModbusAscii.ReadBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 1, out sReadResult))  
                    {
                        //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                        string sBitsResults = "";
                        for (int i = 0; i < bReadBitsValue.Length; i++)
                        //for (int i = bReadBitsValue.Length - 1; i >= 0; i--)
                        {
                            sBitsResults += bReadBitsValue[i].ToString() + " ";
                        }

                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }

                
                //rtbLog.AppendText("从站返回字符串：" + sReadResult + "\r\n");
                //rtbLog.AppendText("从站返回字符串转换为字节：" + ModbusAscii.BytesToHexStringSplitByChar(Encoding.ASCII.GetBytes(sReadResult)) + "\r\n");

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusAscii.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
                
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnAsciiReadInputByte_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        byte byReadValue = 0;
                        if (ModbusAscii.ReadInputByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref byReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", byReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        sbyte byReadValue = 0;
                        if (ModbusAscii.ReadInputByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref byReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", byReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        byte[] byReadValue = null;
                        if (ModbusAscii.ReadInputByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref byReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < byReadValue.Length; i++)
                            {
                                sBitsResults += byReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        sbyte[] byReadValue = null;
                        if (ModbusAscii.ReadInputByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref byReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < byReadValue.Length; i++)
                            {
                                sBitsResults += byReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusAscii.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnAsciiReadInputShort_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort stReadValue = 0;
                        if (ModbusAscii.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref stReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", stReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        short stReadValue = 0;
                        if (ModbusAscii.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref stReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", stReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort[] stReadValue = null;
                        if (ModbusAscii.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref stReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < stReadValue.Length; i++)
                            {
                                sBitsResults += stReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        short[] stReadValue = null;
                        if (ModbusAscii.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref stReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < stReadValue.Length; i++)
                            {
                                sBitsResults += stReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusAscii.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnAsciiReadInputInt_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        uint iReadValue = 0;
                        if (ModbusAscii.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref iReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", iReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        int iReadValue = 0;
                        if (ModbusAscii.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref iReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", iReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        uint[] iReadValue = null;
                        if (ModbusAscii.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref iReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < iReadValue.Length; i++)
                            {
                                sBitsResults += iReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        int[] iReadValue = null;
                        if (ModbusAscii.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref iReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < iReadValue.Length; i++)
                            {
                                sBitsResults += iReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusAscii.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnAsciiReadInputLong_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong lReadValue = 0;
                        if (ModbusAscii.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref lReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", lReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        long lReadValue = 0;
                        if (ModbusAscii.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref lReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", lReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong[] lReadValue = null;
                        if (ModbusAscii.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref lReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < lReadValue.Length; i++)
                            {
                                sBitsResults += lReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        long[] lReadValue = null;
                        if (ModbusAscii.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref lReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < lReadValue.Length; i++)
                            {
                                sBitsResults += lReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusAscii.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnAsciiReadInputRegister_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort stValue = 0;
                        if (ModbusAscii.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref stValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", stValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        short stValue = 0;
                        if (ModbusAscii.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref stValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", stValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort[] stValue = null;
                        if (ModbusAscii.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref stValue))//   (ModbusAscii.ReadBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 1, out sReadResult))  
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < stValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += stValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        short[] stValue = null;
                        if (ModbusAscii.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref stValue))//   (ModbusAscii.ReadBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 1, out sReadResult))  
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < stValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += stValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }


                //rtbLog.AppendText("从站返回字符串：" + sReadResult + "\r\n");
                //rtbLog.AppendText("从站返回字符串转换为字节：" + ModbusAscii.BytesToHexStringSplitByChar(Encoding.ASCII.GetBytes(sReadResult)) + "\r\n");

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusAscii.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        
        // ok
        private void btnAsciiReadInputRegisterInt_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        uint iValue = 0;
                        if (ModbusAscii.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref iValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", iValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        int iValue = 0;
                        if (ModbusAscii.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref iValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", iValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        uint[] iValue = null;
                        if (ModbusAscii.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref iValue))//   (ModbusAscii.ReadBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 1, out sReadResult))  
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < iValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += iValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        int[] iValue = null;
                        if (ModbusAscii.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref iValue))//   (ModbusAscii.ReadBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 1, out sReadResult))  
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < iValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += iValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }


                //rtbLog.AppendText("从站返回字符串：" + sReadResult + "\r\n");
                //rtbLog.AppendText("从站返回字符串转换为字节：" + ModbusAscii.BytesToHexStringSplitByChar(Encoding.ASCII.GetBytes(sReadResult)) + "\r\n");

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusAscii.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // 待进一步测试
        private void btnAsciiReadInputRegisterLong_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong lValue = 0;
                        if (ModbusAscii.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref lValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", lValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        long lValue = 0;
                        if (ModbusAscii.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref lValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", lValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong[] lValue = null;
                        if (ModbusAscii.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref lValue))//   (ModbusAscii.ReadBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 1, out sReadResult))  
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < lValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += lValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        long[] lValue = null;
                        if (ModbusAscii.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref lValue))//   (ModbusAscii.ReadBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 1, out sReadResult))  
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < lValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += lValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }


                //rtbLog.AppendText("从站返回字符串：" + sReadResult + "\r\n");
                //rtbLog.AppendText("从站返回字符串转换为字节：" + ModbusAscii.BytesToHexStringSplitByChar(Encoding.ASCII.GetBytes(sReadResult)) + "\r\n");

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusAscii.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnAsciiReadInputRegisterFloat_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    float fValue = 0;
                    if (ModbusAscii.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref fValue))
                    {
                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", fValue.ToString()));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }
                else
                {
                    float[] fValue = null;
                    if (ModbusAscii.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref fValue))//   (ModbusAscii.ReadBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 1, out sReadResult))  
                    {
                        //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                        string sBitsResults = "";
                        for (int i = 0; i < fValue.Length; i++)
                        //for (int i = iValue.Length - 1; i >= 0; i--)
                        {
                            sBitsResults += fValue[i].ToString() + " ";
                        }

                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }


                //rtbLog.AppendText("从站返回字符串：" + sReadResult + "\r\n");
                //rtbLog.AppendText("从站返回字符串转换为字节：" + ModbusAscii.BytesToHexStringSplitByChar(Encoding.ASCII.GetBytes(sReadResult)) + "\r\n");

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusAscii.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // TBD
        private void btnAsciiReadInputRegisterDouble_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    double dValue = 0;
                    if (ModbusAscii.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref dValue))
                    {
                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", dValue.ToString()));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }
                else
                {
                    double[] dValue = null;
                    if (ModbusAscii.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref dValue))//   (ModbusAscii.ReadBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 1, out sReadResult))  
                    {
                        //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                        string sBitsResults = "";
                        for (int i = 0; i < dValue.Length; i++)
                        //for (int i = iValue.Length - 1; i >= 0; i--)
                        {
                            sBitsResults += dValue[i].ToString() + " ";
                        }

                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }


                //rtbLog.AppendText("从站返回字符串：" + sReadResult + "\r\n");
                //rtbLog.AppendText("从站返回字符串转换为字节：" + ModbusAscii.BytesToHexStringSplitByChar(Encoding.ASCII.GetBytes(sReadResult)) + "\r\n");

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusAscii.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        #endregion

        #region "KeepRegister - ok"

        // ok
        private void btnAsciiReadShortKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort stValue = 0;
                        if (ModbusAscii.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref stValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", stValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        short stValue = 0;
                        if (ModbusAscii.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref stValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", stValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort[] stValue = null;
                        if (ModbusAscii.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref stValue))//   (ModbusAscii.ReadBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 1, out sReadResult))  
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < stValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += stValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        short[] stValue = null;
                        if (ModbusAscii.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref stValue))//   (ModbusAscii.ReadBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 1, out sReadResult))  
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < stValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += stValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                //rtbLog.AppendText("从站返回字符串：" + sReadResult + "\r\n");
                //rtbLog.AppendText("从站返回字符串转换为字节：" + ModbusAscii.BytesToHexStringSplitByChar(Encoding.ASCII.GetBytes(sReadResult)) + "\r\n");

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusAscii.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnAsciiWriteShortKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                Random rndShortData = new Random();

                if (bReadWriteArray == false)//bWriteSingleCoil
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort stValue = Convert.ToUInt16(rndShortData.Next(ushort.MinValue, ushort.MaxValue));//-32678, 32676));

                        if (ModbusAscii.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, stValue) == true)//1111      
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", stValue));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        short stValue = Convert.ToInt16(rndShortData.Next(short.MinValue, short.MaxValue));//-32678, 32676));

                        if (ModbusAscii.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, stValue) == true)//1111      
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", stValue));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort[] stDataToBeSent = new ushort[4];

                        string sSentBools = "";

                        for (int i = 0; i < stDataToBeSent.Length; i++)
                        {

                            stDataToBeSent[i] = Convert.ToUInt16(rndShortData.Next(ushort.MinValue, ushort.MaxValue));

                            sSentBools += stDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (stDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusAscii.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, stDataToBeSent) == true)//1111      
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        short[] stDataToBeSent = new short[4];

                        string sSentBools = "";

                        for (int i = 0; i < stDataToBeSent.Length; i++)
                        {

                            stDataToBeSent[i] = Convert.ToInt16(rndShortData.Next(short.MinValue, short.MaxValue));

                            sSentBools += stDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (stDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusAscii.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, stDataToBeSent) == true)//1111      
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusAscii.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnAsciiReadIntKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        uint iValue = 0;
                        if (ModbusAscii.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref iValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", iValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        int iValue = 0;
                        if (ModbusAscii.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref iValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", iValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }

                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        uint[] iValue = null;
                        if (ModbusAscii.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref iValue))//   (ModbusAscii.ReadBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 1, out sReadResult))  
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < iValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += iValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        int[] iValue = null;
                        if (ModbusAscii.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref iValue))//   (ModbusAscii.ReadBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 1, out sReadResult))  
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < iValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += iValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }

                }


                //rtbLog.AppendText("从站返回字符串：" + sReadResult + "\r\n");
                //rtbLog.AppendText("从站返回字符串转换为字节：" + ModbusAscii.BytesToHexStringSplitByChar(Encoding.ASCII.GetBytes(sReadResult)) + "\r\n");

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusAscii.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnAsciiWriteIntKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                Random rndShortData = new Random();

                if (bReadWriteArray == false)//bWriteSingleCoil
                {
                    if (bReadUnsignedData == true)
                    {
                        uint iValue = (uint)rndShortData.Next(int.MinValue, int.MaxValue);

                        if (ModbusAscii.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, iValue) == true)//1111      
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", iValue));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        int iValue = rndShortData.Next(int.MinValue, int.MaxValue);

                        if (ModbusAscii.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, iValue) == true)//1111      
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", iValue));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        uint[] iDataToBeSent = new uint[4];

                        string sSentBools = "";

                        for (int i = 0; i < iDataToBeSent.Length; i++)
                        {
                            iDataToBeSent[i] = (uint)rndShortData.Next(int.MinValue, int.MaxValue);

                            sSentBools += iDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (iDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusAscii.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, iDataToBeSent) == true)//1111      
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        int[] iDataToBeSent = new int[4];

                        string sSentBools = "";

                        for (int i = 0; i < iDataToBeSent.Length; i++)
                        {
                            iDataToBeSent[i] = rndShortData.Next(int.MinValue, int.MaxValue);

                            sSentBools += iDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (iDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusAscii.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, iDataToBeSent) == true)//1111      
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusAscii.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnAsciiWriteFloatKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                Random rndShortData = new Random();

                if (bReadWriteArray == false)//bWriteSingleCoil
                {
                    float fValue = Convert.ToSingle(Convert.ToInt16(rndShortData.Next(-32678, 32676)) / 3.5);

                    if (ModbusAscii.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, fValue) == true)//1111      
                    {
                        rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", fValue.ToString()));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行写命令\r\n");
                    }
                }
                else
                {
                    float[] fDataToBeSent = new float[4];

                    string sSentBools = "";

                    for (int i = 0; i < fDataToBeSent.Length; i++)
                    {

                        fDataToBeSent[i] = Convert.ToSingle(Convert.ToInt16(rndShortData.Next(-32678, 32676)) / 3.5);

                        sSentBools += fDataToBeSent[i].ToString() + " ";
                    }

                    rtbLog.AppendText("发送值[0~" + (fDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                    if (ModbusAscii.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, fDataToBeSent) == true)//1111      
                    {
                        rtbLog.AppendText("成功执行写命令\r\n");
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行写命令\r\n");
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusAscii.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnAsciiWriteDoubleKeepRegister_Click(object sender, EventArgs e)
        {
            //ModbusAscii
            try
            {
                Random rndShortData = new Random();

                if (bReadWriteArray == false)//bWriteSingleCoil
                {
                    double dValue = Convert.ToDouble(Convert.ToInt16(rndShortData.Next(-32678, 32676)) / 3.5);

                    if (ModbusAscii.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, dValue) == true)//1111      
                    {
                        rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", dValue.ToString()));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行写命令\r\n");
                    }
                }
                else
                {
                    double[] dDataToBeSent = new double[4];

                    string sSentBools = "";

                    for (int i = 0; i < dDataToBeSent.Length; i++)
                    {

                        dDataToBeSent[i] = Convert.ToSingle(Convert.ToInt16(rndShortData.Next(-32678, 32676)) / 3.5);

                        sSentBools += dDataToBeSent[i].ToString() + " ";
                    }

                    rtbLog.AppendText("发送值[0~" + (dDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                    if (ModbusAscii.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, dDataToBeSent) == true)//1111      
                    {
                        rtbLog.AppendText("成功执行写命令\r\n");
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行写命令\r\n");
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusAscii.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnAsciiWriteLongKeepRegister_Click(object sender, EventArgs e)
        {
            //ModbusAscii
            try
            {
                Random rndShortData = new Random();

                if (bReadWriteArray == false)//bWriteSingleCoil
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong dValue = (ulong)(rndShortData.Next(int.MinValue, int.MaxValue) * rndShortData.Next(int.MinValue, int.MaxValue));

                        if (ModbusAscii.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, dValue) == true)//1111      
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", dValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        long dValue = rndShortData.Next(int.MinValue, int.MaxValue) * rndShortData.Next(int.MinValue, int.MaxValue);

                        if (ModbusAscii.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, dValue) == true)//1111      
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", dValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong[] dDataToBeSent = new ulong[4];

                        string sSentBools = "";

                        for (int i = 0; i < dDataToBeSent.Length; i++)
                        {

                            dDataToBeSent[i] = (ulong)(rndShortData.Next(int.MinValue, int.MaxValue) * rndShortData.Next(int.MinValue, int.MaxValue));

                            sSentBools += dDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (dDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusAscii.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, dDataToBeSent) == true)//1111      
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        long[] dDataToBeSent = new long[4];

                        string sSentBools = "";

                        for (int i = 0; i < dDataToBeSent.Length; i++)
                        {

                            dDataToBeSent[i] = rndShortData.Next(int.MinValue, int.MaxValue) * rndShortData.Next(int.MinValue, int.MaxValue);

                            sSentBools += dDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (dDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusAscii.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, dDataToBeSent) == true)//1111      
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusAscii.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnAsciiReadFloatKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    float fValue = 0;
                    if (ModbusAscii.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref fValue))
                    {
                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", fValue.ToString()));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }
                else
                {
                    float[] fValue = null;
                    if (ModbusAscii.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref fValue))//   (ModbusAscii.ReadBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 1, out sReadResult))  
                    {
                        //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                        string sBitsResults = "";
                        for (int i = 0; i < fValue.Length; i++)
                        //for (int i = iValue.Length - 1; i >= 0; i--)
                        {
                            sBitsResults += fValue[i].ToString() + " ";
                        }

                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }


                //rtbLog.AppendText("从站返回字符串：" + sReadResult + "\r\n");
                //rtbLog.AppendText("从站返回字符串转换为字节：" + ModbusAscii.BytesToHexStringSplitByChar(Encoding.ASCII.GetBytes(sReadResult)) + "\r\n");

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusAscii.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnAsciiReadDoubleKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    double dValue = 0;
                    if (ModbusAscii.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref dValue))
                    {
                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", dValue.ToString()));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }
                else
                {
                    double[] dValue = null;
                    if (ModbusAscii.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref dValue))//   (ModbusAscii.ReadBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 1, out sReadResult))  
                    {
                        //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                        string sBitsResults = "";
                        for (int i = 0; i < dValue.Length; i++)
                        //for (int i = iValue.Length - 1; i >= 0; i--)
                        {
                            sBitsResults += dValue[i].ToString() + " ";
                        }

                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }


                //rtbLog.AppendText("从站返回字符串：" + sReadResult + "\r\n");
                //rtbLog.AppendText("从站返回字符串转换为字节：" + ModbusAscii.BytesToHexStringSplitByChar(Encoding.ASCII.GetBytes(sReadResult)) + "\r\n");

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusAscii.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnAsciiReadLongKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong lValue = 0;
                        if (ModbusAscii.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref lValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", lValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        long lValue = 0;
                        if (ModbusAscii.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref lValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", lValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong[] lValue = null;
                        if (ModbusAscii.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref lValue))//   (ModbusAscii.ReadBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 1, out sReadResult))  
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < lValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += lValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        long[] lValue = null;
                        if (ModbusAscii.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref lValue))//   (ModbusAscii.ReadBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 1, out sReadResult))  
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < lValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += lValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                //rtbLog.AppendText("从站返回字符串：" + sReadResult + "\r\n");
                //rtbLog.AppendText("从站返回字符串转换为字节：" + ModbusAscii.BytesToHexStringSplitByChar(Encoding.ASCII.GetBytes(sReadResult)) + "\r\n");

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusAscii.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        #endregion

        #endregion

        #region "ModbusRTU - ok"

        private void btnCalcCRC_Click(object sender, EventArgs e)
        {
            try
            {
                byte[] byData = Encoding.UTF8.GetBytes(CConverter.HexStringSplitByCharToString(txtUsedToCalcCRC.Text)); //Encoding.UTF8.GetBytes(txtUsedToCalcCRC.Text);
                //byte[] byData = Encoding.UTF8.GetBytes(ModbusRTU.HexStringSplitByCharToRTUString(txtUsedToCalcCRC.Text)); //Encoding.UTF8.GetBytes(txtUsedToCalcCRC.Text);

                byte[] byCRCResult = CModbusRTU.CalcCRC(byData);

                byData = CConverter.JoinTwoByteArrays(byData, byCRCResult);

                txtCalcCRCResults.Text = CConverter.Bytes1To2HexStr(byData);// Encoding.UTF8.GetString(byData);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        #region "Coil - ok"

        // ok
        private void btnRTUReadCoilBit_Click(object sender, EventArgs e)
        {
            try
            {
                //string sReadResult = "";
                
                if (bReadWriteArray == false)//bReadSingleBit
                {
                    bool bReadBitValue = false;
                    if (ModbusRTU.ReadCoilBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref bReadBitValue))//   (ModbusRTU.ReadBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 1, out sReadResult))
                    {
                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }
                else
                {
                    bool[] bReadBitsValue = null;
                    if (ModbusRTU.ReadCoilBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref bReadBitsValue))//   (ModbusRTU.ReadBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 1, out sReadResult))  
                    {
                        //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                        string sBitsResults = "";
                        for (int i = 0; i < bReadBitsValue.Length; i++)
                        //for (int i = bReadBitsValue.Length - 1; i >= 0; i--)
                        {
                            sBitsResults += bReadBitsValue[i].ToString() + " ";
                        }

                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }


                //rtbLog.AppendText("从站返回字符串：" + sReadResult + "\r\n");
                //rtbLog.AppendText("从站返回字符串转换为字节：" + ModbusRTU.BytesToHexStringSplitByChar(Encoding.ASCII.GetBytes(sReadResult)) + "\r\n");

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusRTU.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnRTUWriteCoilBit_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    bWriteBitValue = !bWriteBitValue;

                    if (ModbusRTU.WriteCoilBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, bWriteBitValue) == true)//1111      
                    {
                        rtbLog.AppendText(string.Format("成功执行写命令:{0}\r\n", bWriteBitValue));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行写命令\r\n");
                    }
                }
                else
                {
                    bool[] bDataToBeSent = new bool[16];

                    Random rndData = new Random();

                    string sSentBools = "";

                    for (int i = 0; i < bDataToBeSent.Length; i++)
                    {
                        if (rndData.Next(1, 16) % 2 == 0)
                        {
                            bDataToBeSent[i] = true;
                        }
                        else
                        {
                            bDataToBeSent[i] = false;
                        }

                        sSentBools += bDataToBeSent[i].ToString() + " ";
                    }

                    rtbLog.AppendText("发送值[0~" + (bDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                    if (ModbusRTU.WriteCoilBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, bDataToBeSent) == true)//1111      
                    {
                        rtbLog.AppendText("成功执行写命令\r\n");
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行写命令\r\n");
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusRTU.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnRTUReadCoilByte_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        byte byReadValue = 0;
                        if (ModbusRTU.ReadCoilByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref byReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", byReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        sbyte byReadValue = 0;
                        if (ModbusRTU.ReadCoilByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref byReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", byReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        byte[] byReadValue = null;
                        if (ModbusRTU.ReadCoilByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref byReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < byReadValue.Length; i++)
                            {
                                sBitsResults += byReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        sbyte[] byReadValue = null;
                        if (ModbusRTU.ReadCoilByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref byReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < byReadValue.Length; i++)
                            {
                                sBitsResults += byReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusRTU.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnRTUReadCoilWord_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort stReadValue = 0;
                        if (ModbusRTU.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref stReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", stReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        short stReadValue = 0;
                        if (ModbusRTU.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref stReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", stReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort[] stReadValue = null;
                        if (ModbusRTU.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref stReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < stReadValue.Length; i++)
                            {
                                sBitsResults += stReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        short[] stReadValue = null;
                        if (ModbusRTU.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref stReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < stReadValue.Length; i++)
                            {
                                sBitsResults += stReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusRTU.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnRTUWriteCoilByte_Click(object sender, EventArgs e)
        {
            try
            {
                Random rndData = new Random();

                if (bReadWriteArray == false)
                {
                    int iTemp = bReadUnsignedData ? rndData.Next(byte.MinValue, byte.MaxValue) : rndData.Next(sbyte.MinValue, sbyte.MaxValue);
                    byte[] byTemp = BitConverter.GetBytes(iTemp);

                    if (bReadUnsignedData == true)
                    {
                        byte byWriteValue = byTemp[0];

                        if (ModbusRTU.WriteCoilByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, byWriteValue) == true)
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", byWriteValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        sbyte byWriteValue = (sbyte)byTemp[0];

                        if (ModbusRTU.WriteCoilByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, byWriteValue) == true)
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", byWriteValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        byte[] byDataToBeSent = new byte[2];

                        string sSentBools = "";

                        for (int i = 0; i < byDataToBeSent.Length; i++)
                        {
                            int iTemp = rndData.Next(byte.MinValue, byte.MaxValue);
                            byte[] byTemp = BitConverter.GetBytes(iTemp);
                            byte byWriteValue = byTemp[0];
                            byDataToBeSent[i] = byWriteValue;

                            sSentBools += byDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (byDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusRTU.WriteCoilByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, byDataToBeSent) == true)//1111      
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        sbyte[] byDataToBeSent = new sbyte[2];

                        string sSentBools = "";

                        for (int i = 0; i < byDataToBeSent.Length; i++)
                        {
                            int iTemp = rndData.Next(sbyte.MinValue, sbyte.MaxValue);
                            byte[] byTemp = BitConverter.GetBytes(iTemp);
                            byte byWriteValue = byTemp[0];
                            byDataToBeSent[i] = (sbyte)byWriteValue;

                            sSentBools += byDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (byDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusRTU.WriteCoilByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, byDataToBeSent) == true)//1111      
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusRTU.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnRTUWriteCoilWord_Click(object sender, EventArgs e)
        {
            try
            {
                Random rndData = new Random();

                if (bReadWriteArray == false)
                {
                    int iTemp = bReadUnsignedData ? rndData.Next(UInt16.MinValue, UInt16.MaxValue) : rndData.Next(Int16.MinValue, Int16.MaxValue);
                    byte[] byTemp = BitConverter.GetBytes(iTemp);

                    if (bReadUnsignedData == true)
                    {
                        ushort stWriteValue = BitConverter.ToUInt16(byTemp, 0);

                        if (ModbusRTU.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, stWriteValue) == true)
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", stWriteValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        short stWriteValue = BitConverter.ToInt16(byTemp, 0);

                        if (ModbusRTU.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, stWriteValue) == true)
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", stWriteValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort[] stDataToBeSent = new ushort[2];

                        string sSentBools = "";

                        for (int i = 0; i < stDataToBeSent.Length; i++)
                        {
                            int iTemp = rndData.Next(UInt16.MinValue, UInt16.MaxValue);//rndData.Next(-32768, 32767);
                            byte[] byTemp = BitConverter.GetBytes(iTemp);
                            stDataToBeSent[i] = BitConverter.ToUInt16(byTemp, 0);

                            sSentBools += stDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (stDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusRTU.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, stDataToBeSent) == true)
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        short[] stDataToBeSent = new short[2];

                        string sSentBools = "";

                        for (int i = 0; i < stDataToBeSent.Length; i++)
                        {
                            int iTemp = rndData.Next(Int16.MinValue, Int16.MaxValue);//rndData.Next(-32768, 32767);
                            byte[] byTemp = BitConverter.GetBytes(iTemp);
                            stDataToBeSent[i] = BitConverter.ToInt16(byTemp, 0);

                            sSentBools += stDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (stDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusRTU.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, stDataToBeSent) == true)
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusRTU.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnRTUWriteCoilFloat_Click(object sender, EventArgs e)
        {
            try
            {
                Random rndShortData = new Random();

                if (bReadWriteArray == false)//bWriteSingleCoil
                {
                    if (bReadUnsignedData == true)
                    {
                        uint iValue = (uint)rndShortData.Next(int.MinValue, int.MaxValue);

                        if (ModbusRTU.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, iValue) == true)//1111      
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", iValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        int iValue = rndShortData.Next(int.MinValue, int.MaxValue);

                        if (ModbusRTU.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, iValue) == true)//1111      
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", iValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        uint[] iDataToBeSent = new uint[4];

                        string sSentBools = "";

                        for (int i = 0; i < iDataToBeSent.Length; i++)
                        {
                            iDataToBeSent[i] = (uint)rndShortData.Next(int.MinValue, int.MaxValue);

                            sSentBools += iDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (iDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusRTU.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, iDataToBeSent) == true)//1111      
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        int[] iDataToBeSent = new int[4];

                        string sSentBools = "";

                        for (int i = 0; i < iDataToBeSent.Length; i++)
                        {
                            iDataToBeSent[i] = rndShortData.Next(int.MinValue, int.MaxValue);

                            sSentBools += iDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (iDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusRTU.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, iDataToBeSent) == true)//1111      
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusRTU.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        //ok
        private void btnRTUWriteCoilLong_Click(object sender, EventArgs e)
        {
            try
            {
                Random rndShortData = new Random();

                if (bReadWriteArray == false)//bWriteSingleCoil
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong lValue = (ulong)(rndShortData.Next(int.MinValue, int.MaxValue) * rndShortData.Next(int.MinValue, int.MaxValue));

                        if (ModbusRTU.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, lValue) == true)//1111      
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", lValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        long lValue = rndShortData.Next(int.MinValue, int.MaxValue) * rndShortData.Next(int.MinValue, int.MaxValue);

                        if (ModbusRTU.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, lValue) == true)//1111      
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", lValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong[] lDataToBeSent = new ulong[2];

                        string sSentBools = "";

                        for (int i = 0; i < lDataToBeSent.Length; i++)
                        {

                            lDataToBeSent[i] = (ulong)(rndShortData.Next(int.MinValue, int.MaxValue) * rndShortData.Next(int.MinValue, int.MaxValue));

                            sSentBools += lDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (lDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusRTU.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, lDataToBeSent) == true)//1111      
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        long[] lDataToBeSent = new long[2];

                        string sSentBools = "";

                        for (int i = 0; i < lDataToBeSent.Length; i++)
                        {

                            lDataToBeSent[i] = rndShortData.Next(int.MinValue, int.MaxValue) * rndShortData.Next(int.MinValue, int.MaxValue);

                            sSentBools += lDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (lDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusRTU.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, lDataToBeSent) == true)//1111      
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusRTU.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        //ok
        private void btnRTUReadCoilInt_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        uint iValue = 0;
                        if (ModbusRTU.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref iValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", iValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        int iValue = 0;
                        if (ModbusRTU.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref iValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", iValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        uint[] iValue = null;
                        if (ModbusRTU.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref iValue))
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < iValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += iValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        int[] iValue = null;
                        if (ModbusRTU.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref iValue))
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < iValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += iValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusRTU.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        //ok
        private void btnRTUReadCoilLong_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong lValue = 0;
                        if (ModbusRTU.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref lValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", lValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        long lValue = 0;
                        if (ModbusRTU.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref lValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", lValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong[] lValue = null;
                        if (ModbusRTU.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref lValue))
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < lValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += lValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        long[] lValue = null;
                        if (ModbusRTU.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref lValue))
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < lValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += lValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusRTU.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        #endregion

        #region "Input - ok"

        // ok
        private void btnRTUReadInputBit_Click(object sender, EventArgs e)
        {
            try
            {
                //bool bReadSingleBit = false;

                if (bReadWriteArray == false)//bReadSingleBit
                {
                    bool bReadBitValue = false;
                    if (ModbusRTU.ReadInputBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref bReadBitValue))//   (ModbusRTU.ReadBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 1, out sReadResult))
                    {
                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }
                else
                {
                    bool[] bReadBitsValue = null;
                    if (ModbusRTU.ReadInputBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref bReadBitsValue))//   (ModbusRTU.ReadBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 1, out sReadResult))  
                    {
                        //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                        string sBitsResults = "";
                        for (int i = 0; i < bReadBitsValue.Length; i++)
                        //for (int i = bReadBitsValue.Length - 1; i >= 0; i--)
                        {
                            sBitsResults += bReadBitsValue[i].ToString() + " ";
                        }

                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }


                //rtbLog.AppendText("从站返回字符串：" + sReadResult + "\r\n");
                //rtbLog.AppendText("从站返回字符串转换为字节：" + ModbusRTU.BytesToHexStringSplitByChar(Encoding.ASCII.GetBytes(sReadResult)) + "\r\n");

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusRTU.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnRTUReadInputByte_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        byte byReadValue = 0;
                        if (ModbusRTU.ReadInputByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref byReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", byReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        sbyte byReadValue = 0;
                        if (ModbusRTU.ReadInputByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref byReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", byReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        byte[] byReadValue = null;
                        if (ModbusRTU.ReadInputByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref byReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < byReadValue.Length; i++)
                            {
                                sBitsResults += byReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        sbyte[] byReadValue = null;
                        if (ModbusRTU.ReadInputByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref byReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < byReadValue.Length; i++)
                            {
                                sBitsResults += byReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusRTU.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnRTUReadInputWord_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort stReadValue = 0;
                        if (ModbusRTU.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref stReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", stReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        short stReadValue = 0;
                        if (ModbusRTU.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref stReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", stReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort[] stReadValue = null;
                        if (ModbusRTU.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref stReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < stReadValue.Length; i++)
                            {
                                sBitsResults += stReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        short[] stReadValue = null;
                        if (ModbusRTU.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref stReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < stReadValue.Length; i++)
                            {
                                sBitsResults += stReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusRTU.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnRTUReadInputInt_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        uint iReadValue = 0;
                        if (ModbusRTU.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref iReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", iReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        int iReadValue = 0;
                        if (ModbusRTU.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref iReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", iReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        uint[] iReadValue = null;
                        if (ModbusRTU.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref iReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < iReadValue.Length; i++)
                            {
                                sBitsResults += iReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        int[] iReadValue = null;
                        if (ModbusRTU.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref iReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < iReadValue.Length; i++)
                            {
                                sBitsResults += iReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusRTU.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnRTUReadInputLong_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong lReadValue = 0;
                        if (ModbusRTU.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref lReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", lReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        long lReadValue = 0;
                        if (ModbusRTU.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref lReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", lReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong[] lReadValue = null;
                        if (ModbusRTU.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref lReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < lReadValue.Length; i++)
                            {
                                sBitsResults += lReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        long[] lReadValue = null;
                        if (ModbusRTU.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref lReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < lReadValue.Length; i++)
                            {
                                sBitsResults += lReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusRTU.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnRTUReadInputRegisterShort_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort stValue = 0;
                        if (ModbusRTU.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref stValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", stValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        short stValue = 0;
                        if (ModbusRTU.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref stValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", stValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort[] stValue = null;
                        if (ModbusRTU.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref stValue))//   (ModbusRTU.ReadBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 1, out sReadResult))  
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < stValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += stValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        short[] stValue = null;
                        if (ModbusRTU.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref stValue))//   (ModbusRTU.ReadBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 1, out sReadResult))  
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < stValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += stValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }


                //rtbLog.AppendText("从站返回字符串：" + sReadResult + "\r\n");
                //rtbLog.AppendText("从站返回字符串转换为字节：" + ModbusRTU.BytesToHexStringSplitByChar(Encoding.ASCII.GetBytes(sReadResult)) + "\r\n");

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusRTU.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnRTUReadInputRegisterInt_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        uint iValue = 0;
                        if (ModbusRTU.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref iValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", iValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        int iValue = 0;
                        if (ModbusRTU.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref iValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", iValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        uint[] iValue = null;
                        if (ModbusRTU.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref iValue))//   (ModbusRTU.ReadBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 1, out sReadResult))  
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < iValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += iValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        int[] iValue = null;
                        if (ModbusRTU.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref iValue))//   (ModbusRTU.ReadBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 1, out sReadResult))  
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < iValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += iValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }


                //rtbLog.AppendText("从站返回字符串：" + sReadResult + "\r\n");
                //rtbLog.AppendText("从站返回字符串转换为字节：" + ModbusRTU.BytesToHexStringSplitByChar(Encoding.ASCII.GetBytes(sReadResult)) + "\r\n");

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusRTU.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // 待进一步测试
        private void btnRTUReadInputRegisterLong_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong lValue = 0;
                        if (ModbusRTU.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref lValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", lValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        long lValue = 0;
                        if (ModbusRTU.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref lValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", lValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong[] lValue = null;
                        if (ModbusRTU.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref lValue))
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < lValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += lValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        long[] lValue = null;
                        if (ModbusRTU.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref lValue))
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < lValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += lValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusRTU.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnRTUReadInputRegisterFloat_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    float fValue = 0;
                    if (ModbusRTU.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref fValue))
                    {
                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", fValue.ToString()));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }
                else
                {
                    float[] fValue = null;
                    if (ModbusRTU.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref fValue))//   (ModbusRTU.ReadBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 1, out sReadResult))  
                    {
                        //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                        string sBitsResults = "";
                        for (int i = 0; i < fValue.Length; i++)
                        //for (int i = iValue.Length - 1; i >= 0; i--)
                        {
                            sBitsResults += fValue[i].ToString() + " ";
                        }

                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }


                //rtbLog.AppendText("从站返回字符串：" + sReadResult + "\r\n");
                //rtbLog.AppendText("从站返回字符串转换为字节：" + ModbusRTU.BytesToHexStringSplitByChar(Encoding.ASCII.GetBytes(sReadResult)) + "\r\n");

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusRTU.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnRTUReadInputRegisterDouble_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    double dValue = 0;
                    if (ModbusRTU.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref dValue))
                    {
                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", dValue.ToString()));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }
                else
                {
                    double[] dValue = null;
                    if (ModbusRTU.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref dValue))//   (ModbusRTU.ReadBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 1, out sReadResult))  
                    {
                        //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                        string sBitsResults = "";
                        for (int i = 0; i < dValue.Length; i++)
                        //for (int i = iValue.Length - 1; i >= 0; i--)
                        {
                            sBitsResults += dValue[i].ToString() + " ";
                        }

                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }


                //rtbLog.AppendText("从站返回字符串：" + sReadResult + "\r\n");
                //rtbLog.AppendText("从站返回字符串转换为字节：" + ModbusRTU.BytesToHexStringSplitByChar(Encoding.ASCII.GetBytes(sReadResult)) + "\r\n");

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusRTU.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        #endregion

        #region "KeepRegister - ok"

        // ok
        private void btnRTUReadKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort stValue = 0;
                        if (ModbusRTU.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref stValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", stValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        short stValue = 0;
                        if (ModbusRTU.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref stValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", stValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort[] stValue = null;
                        if (ModbusRTU.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref stValue))//   (ModbusRTU.ReadBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 1, out sReadResult))  
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < stValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += stValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        short[] stValue = null;
                        if (ModbusRTU.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref stValue))//   (ModbusRTU.ReadBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 1, out sReadResult))  
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < stValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += stValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                //rtbLog.AppendText("从站返回字符串：" + sReadResult + "\r\n");
                //rtbLog.AppendText("从站返回字符串转换为字节：" + ModbusRTU.BytesToHexStringSplitByChar(Encoding.ASCII.GetBytes(sReadResult)) + "\r\n");

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusRTU.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnRTUWriteSingleKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                Random rndShortData = new Random();

                if (bReadWriteArray == false)//bWriteSingleCoil
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort stValue = Convert.ToUInt16(rndShortData.Next(ushort.MinValue, ushort.MaxValue));

                        if (ModbusRTU.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, stValue) == true)//1111      
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", stValue));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        short stValue = Convert.ToInt16(rndShortData.Next(short.MinValue, short.MaxValue));

                        if (ModbusRTU.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, stValue) == true)//1111      
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", stValue));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort[] stDataToBeSent = new ushort[10];

                        string sSentBools = "";

                        for (int i = 0; i < stDataToBeSent.Length; i++)
                        {
                            stDataToBeSent[i] = Convert.ToUInt16(rndShortData.Next(ushort.MinValue, ushort.MaxValue));

                            sSentBools += stDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (stDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusRTU.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, stDataToBeSent) == true)//1111      
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        short[] stDataToBeSent = new short[10];

                        string sSentBools = "";

                        for (int i = 0; i < stDataToBeSent.Length; i++)
                        {
                            stDataToBeSent[i] = Convert.ToInt16(rndShortData.Next(short.MinValue, short.MaxValue));

                            sSentBools += stDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (stDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusRTU.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, stDataToBeSent) == true)//1111      
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusRTU.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnRTUWriteFloatKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                Random rndShortData = new Random();

                if (bReadWriteArray == false)//bWriteSingleCoil
                {
                    float fValue = Convert.ToSingle( Convert.ToInt16(rndShortData.Next(-32678, 32676)) / 3.5);

                    if (ModbusRTU.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, fValue) == true)//1111      
                    {
                        rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", fValue.ToString()));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行写命令\r\n");
                    }
                }
                else
                {
                    float[] fDataToBeSent = new float[4];

                    string sSentBools = "";

                    for (int i = 0; i < fDataToBeSent.Length; i++)
                    {

                        fDataToBeSent[i] = Convert.ToSingle(Convert.ToInt16(rndShortData.Next(-32678, 32676)) / 3.5);

                        sSentBools += fDataToBeSent[i].ToString() + " ";
                    }

                    rtbLog.AppendText("发送值[0~" + (fDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                    if (ModbusRTU.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, fDataToBeSent) == true)//1111      
                    {
                        rtbLog.AppendText("成功执行写命令\r\n");
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行写命令\r\n");
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusRTU.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnRTUWriteDoubleKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                Random rndShortData = new Random();

                if (bReadWriteArray == false)//bWriteSingleCoil
                {
                    double dValue = Convert.ToDouble(Convert.ToInt16(rndShortData.Next(-32678, 32676)) / 3.5);

                    if (ModbusRTU.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, dValue) == true)//1111      
                    {
                        rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", dValue.ToString()));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行写命令\r\n");
                    }
                }
                else
                {
                    double[] dDataToBeSent = new double[4];

                    string sSentBools = "";

                    for (int i = 0; i < dDataToBeSent.Length; i++)
                    {

                        dDataToBeSent[i] = Convert.ToSingle(Convert.ToInt16(rndShortData.Next(-32678, 32676)) / 3.5);

                        sSentBools += dDataToBeSent[i].ToString() + " ";
                    }

                    rtbLog.AppendText("发送值[0~" + (dDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                    if (ModbusRTU.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, dDataToBeSent) == true)//1111      
                    {
                        rtbLog.AppendText("成功执行写命令\r\n");
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行写命令\r\n");
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusRTU.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnRTUWriteLongKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                Random rndShortData = new Random();

                if (bReadWriteArray == false)//bWriteSingleCoil
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong dValue = (ulong)(rndShortData.Next(int.MinValue, int.MaxValue) * rndShortData.Next(int.MinValue, int.MaxValue));

                        if (ModbusRTU.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, dValue) == true)//1111      
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", dValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        long dValue = rndShortData.Next(int.MinValue, int.MaxValue) * rndShortData.Next(int.MinValue, int.MaxValue);

                        if (ModbusRTU.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, dValue) == true)//1111      
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", dValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong[] dDataToBeSent = new ulong[4];

                        string sSentBools = "";

                        for (int i = 0; i < dDataToBeSent.Length; i++)
                        {

                            dDataToBeSent[i] = (ulong)(rndShortData.Next(int.MinValue, int.MaxValue) * rndShortData.Next(int.MinValue, int.MaxValue));

                            sSentBools += dDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (dDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusRTU.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, dDataToBeSent) == true)//1111      
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        long[] dDataToBeSent = new long[4];

                        string sSentBools = "";

                        for (int i = 0; i < dDataToBeSent.Length; i++)
                        {

                            dDataToBeSent[i] = rndShortData.Next(int.MinValue, int.MaxValue) * rndShortData.Next(int.MinValue, int.MaxValue);

                            sSentBools += dDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (dDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusRTU.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, dDataToBeSent) == true)//1111      
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusRTU.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnRTUReadFloatKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    float fValue = 0;
                    if (ModbusRTU.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref fValue))
                    {
                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", fValue.ToString()));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }
                else
                {
                    float[] fValue = null;
                    if (ModbusRTU.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref fValue))//   (ModbusRTU.ReadBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 1, out sReadResult))  
                    {
                        //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                        string sBitsResults = "";
                        for (int i = 0; i < fValue.Length; i++)
                        //for (int i = iValue.Length - 1; i >= 0; i--)
                        {
                            sBitsResults += fValue[i].ToString() + " ";
                        }

                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }


                //rtbLog.AppendText("从站返回字符串：" + sReadResult + "\r\n");
                //rtbLog.AppendText("从站返回字符串转换为字节：" + ModbusRTU.BytesToHexStringSplitByChar(Encoding.ASCII.GetBytes(sReadResult)) + "\r\n");

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusRTU.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnRTUReadDoubleKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    double dValue = 0;
                    if (ModbusRTU.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref dValue))
                    {
                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", dValue.ToString()));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }
                else
                {
                    double[] dValue = null;
                    if (ModbusRTU.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref dValue))//   (ModbusRTU.ReadBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 1, out sReadResult))  
                    {
                        //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                        string sBitsResults = "";
                        for (int i = 0; i < dValue.Length; i++)
                        //for (int i = iValue.Length - 1; i >= 0; i--)
                        {
                            sBitsResults += dValue[i].ToString() + " ";
                        }

                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }


                //rtbLog.AppendText("从站返回字符串：" + sReadResult + "\r\n");
                //rtbLog.AppendText("从站返回字符串转换为字节：" + ModbusRTU.BytesToHexStringSplitByChar(Encoding.ASCII.GetBytes(sReadResult)) + "\r\n");

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusRTU.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnRTUReadLongKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong lValue = 0;
                        if (ModbusRTU.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref lValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", lValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        long lValue = 0;
                        if (ModbusRTU.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref lValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", lValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong[] lValue = null;
                        if (ModbusRTU.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref lValue))//   (ModbusRTU.ReadBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 1, out sReadResult))  
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < lValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += lValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        long[] lValue = null;
                        if (ModbusRTU.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref lValue))//   (ModbusRTU.ReadBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 1, out sReadResult))  
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < lValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += lValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }


                //rtbLog.AppendText("从站返回字符串：" + sReadResult + "\r\n");
                //rtbLog.AppendText("从站返回字符串转换为字节：" + ModbusRTU.BytesToHexStringSplitByChar(Encoding.ASCII.GetBytes(sReadResult)) + "\r\n");

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusRTU.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnRTUWriteIntKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                Random rndShortData = new Random();

                if (bReadWriteArray == false)//bWriteSingleCoil
                {
                    if (bReadUnsignedData == true)
                    {
                        uint iValue = (uint)(rndShortData.Next(int.MinValue, int.MaxValue));

                        if (ModbusRTU.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, iValue) == true)//1111      
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", iValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        int iValue = rndShortData.Next(int.MinValue, int.MaxValue);

                        if (ModbusRTU.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, iValue) == true)//1111      
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", iValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        uint[] iDataToBeSent = new uint[4];

                        string sSentBools = "";

                        for (int i = 0; i < iDataToBeSent.Length; i++)
                        {
                            iDataToBeSent[i] = (uint)(rndShortData.Next(int.MinValue, int.MaxValue));

                            sSentBools += iDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (iDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusRTU.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, iDataToBeSent) == true)//1111      
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        int[] iDataToBeSent = new int[4];

                        string sSentBools = "";

                        for (int i = 0; i < iDataToBeSent.Length; i++)
                        {
                            iDataToBeSent[i] = rndShortData.Next(int.MinValue, int.MaxValue);

                            sSentBools += iDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (iDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusRTU.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, iDataToBeSent) == true)//1111      
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusRTU.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnRTUReadIntKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        uint iValue = 0;
                        if (ModbusRTU.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref iValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", iValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        int iValue = 0;
                        if (ModbusRTU.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref iValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", iValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        uint[] iValue = null;
                        if (ModbusRTU.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref iValue))//   (ModbusRTU.ReadBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 1, out sReadResult))  
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < iValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += iValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        int[] iValue = null;
                        if (ModbusRTU.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref iValue))//   (ModbusRTU.ReadBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 1, out sReadResult))  
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < iValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += iValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }


                //rtbLog.AppendText("从站返回字符串：" + sReadResult + "\r\n");
                //rtbLog.AppendText("从站返回字符串转换为字节：" + ModbusRTU.BytesToHexStringSplitByChar(Encoding.ASCII.GetBytes(sReadResult)) + "\r\n");

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusRTU.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        #endregion

        #endregion

        #region "ModbusTCP - ok"

        #region "Coil - ok"

        // ok
        private void btnTCPReadCoilBit_Click(object sender, EventArgs e)
        {
            try
            {
                //string sReadResult = "";

                if (bReadWriteArray == false)//bReadSingleBit
                {
                    bool bReadBitValue = false;
                    if (ModbusTCP.ReadCoilBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref bReadBitValue))
                    {
                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }
                else
                {
                    bool[] bReadBitsValue = null;
                    if (ModbusTCP.ReadCoilBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref bReadBitsValue))
                    {
                        //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                        string sBitsResults = "";
                        for (int i = 0; i < bReadBitsValue.Length; i++)
                        //for (int i = bReadBitsValue.Length - 1; i >= 0; i--)
                        {
                            sBitsResults += bReadBitsValue[i].ToString() + " ";
                        }

                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }
                
                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusTCP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnTCPReadCoilByte_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        byte byReadValue = 0;
                        if (ModbusTCP.ReadCoilByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref byReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", byReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        sbyte byReadValue = 0;
                        if (ModbusTCP.ReadCoilByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref byReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", byReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        byte[] byReadValue = null;
                        if (ModbusTCP.ReadCoilByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref byReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < byReadValue.Length; i++)
                            {
                                sBitsResults += byReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        sbyte[] byReadValue = null;
                        if (ModbusTCP.ReadCoilByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref byReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < byReadValue.Length; i++)
                            {
                                sBitsResults += byReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusTCP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // 
        private void btnTCPReadCoilWord_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort stReadValue = 0;
                        if (ModbusTCP.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref stReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", stReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        short stReadValue = 0;
                        if (ModbusTCP.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref stReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", stReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort[] stReadValue = null;
                        if (ModbusTCP.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref stReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < stReadValue.Length; i++)
                            {
                                sBitsResults += stReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        short[] stReadValue = null;
                        if (ModbusTCP.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref stReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < stReadValue.Length; i++)
                            {
                                sBitsResults += stReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusTCP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // 
        private void btnTCPReadCoilInt_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        uint iReadValue = 0;
                        if (ModbusTCP.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref iReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", iReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        int iReadValue = 0;
                        if (ModbusTCP.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref iReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", iReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        uint[] iReadValue = null;
                        if (ModbusTCP.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref iReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < iReadValue.Length; i++)
                            {
                                sBitsResults += iReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        int[] iReadValue = null;
                        if (ModbusTCP.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref iReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < iReadValue.Length; i++)
                            {
                                sBitsResults += iReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusTCP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // 
        private void btnTCPReadCoilLong_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong lReadValue = 0;
                        if (ModbusTCP.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref lReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", lReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        long lReadValue = 0;
                        if (ModbusTCP.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref lReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", lReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong[] lReadValue = null;
                        if (ModbusTCP.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref lReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < lReadValue.Length; i++)
                            {
                                sBitsResults += lReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        long[] lReadValue = null;
                        if (ModbusTCP.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref lReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < lReadValue.Length; i++)
                            {
                                sBitsResults += lReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusTCP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // 
        private void btnTCPWriteCoilBit_Click(object sender, EventArgs e)
        {
            try
            {
                //bool bWriteSingleCoil = false;

                if (bReadWriteArray == false)//bWriteSingleCoil
                {
                    bWriteBitValue = !bWriteBitValue;

                    if (ModbusTCP.WriteCoilBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, bWriteBitValue) == true)//1111      
                    {
                        rtbLog.AppendText(string.Format("成功执行写命令:{0}\r\n", bWriteBitValue));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行写命令\r\n");
                    }
                }
                else
                {
                    bool[] bDataToBeSent = new bool[16];

                    Random rndData = new Random();

                    string sSentBools = "";

                    for (int i = 0; i < bDataToBeSent.Length; i++)
                    {
                        if (rndData.Next(1, 16) % 2 == 0)
                        {
                            bDataToBeSent[i] = true;
                        }
                        else
                        {
                            bDataToBeSent[i] = false;
                        }

                        sSentBools += bDataToBeSent[i].ToString() + " ";
                    }

                    rtbLog.AppendText("发送值[0~" + (bDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                    if (ModbusTCP.WriteCoilBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, bDataToBeSent) == true)//1111      
                    {
                        rtbLog.AppendText("成功执行写命令\r\n");
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行写命令\r\n");
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusTCP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // 
        private void btnTCPWriteCoilByte_Click(object sender, EventArgs e)
        {
            try
            {
                Random rndData = new Random();

                if (bReadWriteArray == false)
                {
                    int iTemp = bReadUnsignedData ? rndData.Next(byte.MinValue, byte.MaxValue) : rndData.Next(sbyte.MinValue, sbyte.MaxValue);//rndData.Next(0, 255);
                    byte[] byTemp = BitConverter.GetBytes(iTemp);

                    if (bReadUnsignedData == true)
                    {
                        byte byWriteValue = byTemp[0];

                        if (ModbusTCP.WriteCoilByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, byWriteValue) == true)
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", byWriteValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        sbyte byWriteValue = (sbyte)byTemp[0];

                        if (ModbusTCP.WriteCoilByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, byWriteValue) == true)
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", byWriteValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        byte[] byDataToBeSent = new byte[2];

                        string sSentBools = "";

                        for (int i = 0; i < byDataToBeSent.Length; i++)
                        {
                            int iTemp = rndData.Next(byte.MinValue, byte.MaxValue);//rndData.Next(0, 255);
                            byte[] byTemp = BitConverter.GetBytes(iTemp);
                            byte byWriteValue = byTemp[0];
                            byDataToBeSent[i] = byWriteValue;

                            sSentBools += byDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (byDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusTCP.WriteCoilByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, byDataToBeSent) == true)//1111      
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        sbyte[] byDataToBeSent = new sbyte[2];

                        string sSentBools = "";

                        for (int i = 0; i < byDataToBeSent.Length; i++)
                        {
                            int iTemp = rndData.Next(sbyte.MinValue, sbyte.MaxValue);
                            byte[] byTemp = BitConverter.GetBytes(iTemp);
                            byte byWriteValue = byTemp[0];
                            byDataToBeSent[i] = (sbyte)byWriteValue;

                            sSentBools += byDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (byDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusTCP.WriteCoilByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, byDataToBeSent) == true)//1111      
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusTCP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // 
        private void btnTCPWriteCoilWord_Click(object sender, EventArgs e)
        {
            try
            {
                Random rndData = new Random();

                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        int iTemp = rndData.Next(ushort.MinValue, ushort.MaxValue);//rndData.Next(-32768, 32767);
                        byte[] byTemp = BitConverter.GetBytes(iTemp);
                        ushort stWriteValue = BitConverter.ToUInt16(byTemp, 0);

                        if (ModbusTCP.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, stWriteValue) == true)
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", stWriteValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        int iTemp = rndData.Next(short.MinValue, short.MaxValue);//rndData.Next(-32768, 32767);
                        byte[] byTemp = BitConverter.GetBytes(iTemp);
                        short stWriteValue = BitConverter.ToInt16(byTemp, 0);

                        if (ModbusTCP.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, stWriteValue) == true)
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", stWriteValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort[] stDataToBeSent = new ushort[2];

                        string sSentBools = "";

                        for (int i = 0; i < stDataToBeSent.Length; i++)
                        {
                            int iTemp = rndData.Next(ushort.MinValue, ushort.MaxValue);
                            byte[] byTemp = BitConverter.GetBytes(iTemp);
                            stDataToBeSent[i] = BitConverter.ToUInt16(byTemp, 0);

                            sSentBools += stDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (stDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusTCP.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, stDataToBeSent) == true)
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        short[] stDataToBeSent = new short[2];

                        string sSentBools = "";

                        for (int i = 0; i < stDataToBeSent.Length; i++)
                        {
                            int iTemp = rndData.Next(short.MinValue, short.MaxValue);
                            byte[] byTemp = BitConverter.GetBytes(iTemp);
                            stDataToBeSent[i] = BitConverter.ToInt16(byTemp, 0);

                            sSentBools += stDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (stDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusTCP.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, stDataToBeSent) == true)
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusTCP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // 
        private void btnTCPWriteCoilInt_Click(object sender, EventArgs e)
        {
            try
            {
                Random rndData = new Random();

                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        uint iTemp = (uint)rndData.Next(int.MinValue, int.MaxValue);
                        if (ModbusTCP.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, iTemp) == true)
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", iTemp.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        int iTemp = rndData.Next(int.MinValue, int.MaxValue);
                        if (ModbusTCP.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, iTemp) == true)
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", iTemp.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        uint[] iDataToBeSent = new uint[2];

                        string sSentBools = "";

                        for (int i = 0; i < iDataToBeSent.Length; i++)
                        {
                            iDataToBeSent[i] = (uint)rndData.Next(int.MinValue, int.MaxValue);
                            sSentBools += iDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (iDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusTCP.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, iDataToBeSent) == true)
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        int[] iDataToBeSent = new int[2];

                        string sSentBools = "";

                        for (int i = 0; i < iDataToBeSent.Length; i++)
                        {
                            iDataToBeSent[i] = rndData.Next(int.MinValue, int.MaxValue);
                            sSentBools += iDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (iDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusTCP.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, iDataToBeSent) == true)
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusTCP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // 
        private void btnTCPWriteCoilLong_Click(object sender, EventArgs e)
        {
            try
            {
                Random rndData = new Random();

                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong lTemp = (ulong)(rndData.Next(int.MinValue, int.MaxValue) * rndData.Next(int.MinValue, int.MaxValue));
                        if (ModbusTCP.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, lTemp) == true)
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", lTemp.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        long lTemp = rndData.Next(int.MinValue, int.MaxValue) * rndData.Next(int.MinValue, int.MaxValue);
                        if (ModbusTCP.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, lTemp) == true)
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", lTemp.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong[] lDataToBeSent = new ulong[2];

                        string sSentBools = "";

                        for (int i = 0; i < lDataToBeSent.Length; i++)
                        {
                            lDataToBeSent[i] = (ulong)(rndData.Next(int.MinValue, int.MaxValue) * rndData.Next(int.MinValue, int.MaxValue));
                            sSentBools += lDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (lDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusTCP.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, lDataToBeSent) == true)
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        long[] lDataToBeSent = new long[2];

                        string sSentBools = "";

                        for (int i = 0; i < lDataToBeSent.Length; i++)
                        {
                            lDataToBeSent[i] = rndData.Next(int.MinValue, int.MaxValue) * rndData.Next(int.MinValue, int.MaxValue);
                            sSentBools += lDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (lDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusTCP.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, lDataToBeSent) == true)
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusTCP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        #endregion

        #region "ReadInputRegister"

        // 
        private void btnTCPReadInputRegisterShort_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort stValue = 0;
                        if (ModbusTCP.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref stValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", stValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        short stValue = 0;
                        if (ModbusTCP.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref stValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", stValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort[] stValue = null;
                        if (ModbusTCP.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref stValue))
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < stValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += stValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        short[] stValue = null;
                        if (ModbusTCP.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref stValue))
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < stValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += stValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                
                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusTCP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // 
        private void btnTCPReadInputRegisterInt_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        uint iValue = 0;
                        if (ModbusTCP.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref iValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", iValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        int iValue = 0;
                        if (ModbusTCP.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref iValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", iValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        uint[] iValue = null;
                        if (ModbusTCP.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref iValue))
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < iValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += iValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        int[] iValue = null;
                        if (ModbusTCP.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref iValue))
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < iValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += iValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                
                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusTCP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // 待进一步测试
        private void btnTCPReadInputRegisterLong_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong lValue = 0;
                        if (ModbusTCP.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref lValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", lValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        long lValue = 0;
                        if (ModbusTCP.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref lValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", lValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong[] lValue = null;
                        if (ModbusTCP.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref lValue))
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < lValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += lValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        long[] lValue = null;
                        if (ModbusTCP.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref lValue))
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < lValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += lValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                
                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusTCP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // 
        private void btnTCPReadInputRegisterFloat_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    float fValue = 0;
                    if (ModbusTCP.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref fValue))
                    {
                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", fValue.ToString()));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }
                else
                {
                    float[] fValue = null;
                    if (ModbusTCP.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref fValue))
                    {
                        //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                        string sBitsResults = "";
                        for (int i = 0; i < fValue.Length; i++)
                        //for (int i = iValue.Length - 1; i >= 0; i--)
                        {
                            sBitsResults += fValue[i].ToString() + " ";
                        }

                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }
                
                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusTCP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // TBD
        private void btnTCPReadInputRegisterDouble_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    double dValue = 0;
                    if (ModbusTCP.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref dValue))
                    {
                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", dValue.ToString()));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }
                else
                {
                    double[] dValue = null;
                    if (ModbusTCP.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref dValue))
                    {
                        //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                        string sBitsResults = "";
                        for (int i = 0; i < dValue.Length; i++)
                        //for (int i = iValue.Length - 1; i >= 0; i--)
                        {
                            sBitsResults += dValue[i].ToString() + " ";
                        }

                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }
                
                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusTCP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        #endregion

        #region "ReadInput"

        // 
        private void btnTCPReadInputBit_Click(object sender, EventArgs e)
        {
            try
            {
                //bool bReadSingleBit = false;

                if (bReadWriteArray == false)//bReadSingleBit
                {
                    bool bReadBitValue = false;
                    if (ModbusTCP.ReadInputBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref bReadBitValue))
                    {
                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }
                else
                {
                    bool[] bReadBitsValue = null;
                    if (ModbusTCP.ReadInputBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref bReadBitsValue))
                    {
                        //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                        string sBitsResults = "";
                        for (int i = 0; i < bReadBitsValue.Length; i++)
                        //for (int i = bReadBitsValue.Length - 1; i >= 0; i--)
                        {
                            sBitsResults += bReadBitsValue[i].ToString() + " ";
                        }

                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusTCP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // 
        private void btnTCPReadInputByte_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        byte byReadValue = 0;
                        if (ModbusTCP.ReadInputByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref byReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", byReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        sbyte byReadValue = 0;
                        if (ModbusTCP.ReadInputByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref byReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", byReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        byte[] byReadValue = null;
                        if (ModbusTCP.ReadInputByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref byReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < byReadValue.Length; i++)
                            {
                                sBitsResults += byReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        sbyte[] byReadValue = null;
                        if (ModbusTCP.ReadInputByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref byReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < byReadValue.Length; i++)
                            {
                                sBitsResults += byReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusTCP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // 
        private void btnTCPReadInputShort_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort stReadValue = 0;
                        if (ModbusTCP.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref stReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", stReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        short stReadValue = 0;
                        if (ModbusTCP.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref stReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", stReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort[] stReadValue = null;
                        if (ModbusTCP.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref stReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < stReadValue.Length; i++)
                            {
                                sBitsResults += stReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        short[] stReadValue = null;
                        if (ModbusTCP.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref stReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < stReadValue.Length; i++)
                            {
                                sBitsResults += stReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusTCP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // 
        private void btnTCPReadInputInt_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        uint iReadValue = 0;
                        if (ModbusTCP.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref iReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", iReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        int iReadValue = 0;
                        if (ModbusTCP.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref iReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", iReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        uint[] iReadValue = null;
                        if (ModbusTCP.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref iReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < iReadValue.Length; i++)
                            {
                                sBitsResults += iReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        int[] iReadValue = null;
                        if (ModbusTCP.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref iReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < iReadValue.Length; i++)
                            {
                                sBitsResults += iReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusTCP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // 
        private void btnTCPReadInputLong_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong lReadValue = 0;
                        if (ModbusTCP.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref lReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", lReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        long lReadValue = 0;
                        if (ModbusTCP.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref lReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", lReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }

                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong[] lReadValue = null;
                        if (ModbusTCP.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref lReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < lReadValue.Length; i++)
                            {
                                sBitsResults += lReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        long[] lReadValue = null;
                        if (ModbusTCP.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref lReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < lReadValue.Length; i++)
                            {
                                sBitsResults += lReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusTCP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        #endregion

        #region "KeepRegister"

        // 
        private void btnTCPReadShortKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort stValue = 0;
                        if (ModbusTCP.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref stValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", stValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        short stValue = 0;
                        if (ModbusTCP.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref stValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", stValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort[] stValue = null;
                        if (ModbusTCP.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 4, ref stValue))
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < stValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += stValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        short[] stValue = null;
                        if (ModbusTCP.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 4, ref stValue))
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < stValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += stValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusTCP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // 
        private void btnTCPReadIntKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        uint iValue = 0;
                        if (ModbusTCP.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref iValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", iValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        int iValue = 0;
                        if (ModbusTCP.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref iValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", iValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        uint[] iValue = null;
                        if (ModbusTCP.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref iValue))
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < iValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += iValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        int[] iValue = null;
                        if (ModbusTCP.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref iValue))
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < iValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += iValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusTCP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // 
        private void btnTCPReadFloatKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    float fValue = 0;
                    if (ModbusTCP.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref fValue))
                    {
                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", fValue.ToString()));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }
                else
                {
                    float[] fValue = null;
                    if (ModbusTCP.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref fValue))
                    {
                        //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                        string sBitsResults = "";
                        for (int i = 0; i < fValue.Length; i++)
                        //for (int i = iValue.Length - 1; i >= 0; i--)
                        {
                            sBitsResults += fValue[i].ToString() + " ";
                        }

                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusTCP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // 
        private void btnTCPReadDoubleKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    double dValue = 0;
                    if (ModbusTCP.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref dValue))
                    {
                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", dValue.ToString()));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }
                else
                {
                    double[] dValue = null;
                    if (ModbusTCP.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref dValue))
                    {
                        //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                        string sBitsResults = "";
                        for (int i = 0; i < dValue.Length; i++)
                        //for (int i = iValue.Length - 1; i >= 0; i--)
                        {
                            sBitsResults += dValue[i].ToString() + " ";
                        }

                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusTCP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // 
        private void btnTCPReadLongKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong lValue = 0;
                        if (ModbusTCP.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref lValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", lValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        long lValue = 0;
                        if (ModbusTCP.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref lValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", lValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong[] lValue = null;
                        if (ModbusTCP.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref lValue))
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < lValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += lValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        long[] lValue = null;
                        if (ModbusTCP.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref lValue))
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < lValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += lValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusTCP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // 
        private void btnTCPWriteShortKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                Random rndShortData = new Random();

                if (bReadWriteArray == false)//bWriteSingleCoil
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort stValue = Convert.ToUInt16(rndShortData.Next(ushort.MinValue, ushort.MaxValue));//-32678, 32676));

                        if (ModbusTCP.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, stValue) == true)//1111      
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", stValue));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        short stValue = Convert.ToInt16(rndShortData.Next(short.MinValue, short.MaxValue));//-32678, 32676));

                        if (ModbusTCP.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, stValue) == true)//1111      
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", stValue));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort[] stDataToBeSent = new ushort[5];

                        string sSentBools = "";

                        for (int i = 0; i < stDataToBeSent.Length; i++)
                        {

                            stDataToBeSent[i] = Convert.ToUInt16(rndShortData.Next(ushort.MinValue, ushort.MaxValue));

                            sSentBools += stDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (stDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusTCP.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, stDataToBeSent) == true)//1111      
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        short[] stDataToBeSent = new short[5];

                        string sSentBools = "";

                        for (int i = 0; i < stDataToBeSent.Length; i++)
                        {

                            stDataToBeSent[i] = Convert.ToInt16(rndShortData.Next(short.MinValue, short.MaxValue));

                            sSentBools += stDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (stDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusTCP.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, stDataToBeSent) == true)//1111      
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusTCP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // 
        private void btnTCPWriteIntKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                Random rndShortData = new Random();

                if (bReadWriteArray == false)//bWriteSingleCoil
                {
                    if (bReadUnsignedData == true)
                    {
                        uint iValue = (uint)rndShortData.Next(int.MinValue, int.MaxValue);

                        if (ModbusTCP.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, iValue) == true)//1111      
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", iValue));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        int iValue = rndShortData.Next(int.MinValue, int.MaxValue);

                        if (ModbusTCP.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, iValue) == true)//1111      
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", iValue));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        uint[] iDataToBeSent = new uint[4];

                        string sSentBools = "";

                        for (int i = 0; i < iDataToBeSent.Length; i++)
                        {

                            iDataToBeSent[i] = (uint)rndShortData.Next(int.MinValue, int.MaxValue);

                            sSentBools += iDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (iDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusTCP.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, iDataToBeSent) == true)//1111      
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        int[] iDataToBeSent = new int[4];

                        string sSentBools = "";

                        for (int i = 0; i < iDataToBeSent.Length; i++)
                        {

                            iDataToBeSent[i] = rndShortData.Next(int.MinValue, int.MaxValue);

                            sSentBools += iDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (iDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusTCP.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, iDataToBeSent) == true)//1111      
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusTCP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // 
        private void btnTCPWriteFloatKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                Random rndShortData = new Random();

                if (bReadWriteArray == false)//bWriteSingleCoil
                {
                    float fValue = Convert.ToSingle(Convert.ToInt16(rndShortData.Next(-32678, 32676)) / 3.5);

                    if (ModbusTCP.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, fValue) == true)//1111      
                    {
                        rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", fValue.ToString()));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行写命令\r\n");
                    }
                }
                else
                {
                    float[] fDataToBeSent = new float[4];

                    string sSentBools = "";

                    for (int i = 0; i < fDataToBeSent.Length; i++)
                    {

                        fDataToBeSent[i] = Convert.ToSingle(Convert.ToInt16(rndShortData.Next(-32678, 32676)) / 3.5);

                        sSentBools += fDataToBeSent[i].ToString() + " ";
                    }

                    rtbLog.AppendText("发送值[0~" + (fDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                    if (ModbusTCP.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, fDataToBeSent) == true)//1111      
                    {
                        rtbLog.AppendText("成功执行写命令\r\n");
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行写命令\r\n");
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusTCP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // 
        private void btnTCPWriteDoubleKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                Random rndShortData = new Random();

                if (bReadWriteArray == false)//bWriteSingleCoil
                {
                    double dValue = Convert.ToDouble(Convert.ToInt16(rndShortData.Next(-32678, 32676)) / 3.5);

                    if (ModbusTCP.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, dValue) == true)//1111      
                    {
                        rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", dValue.ToString()));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行写命令\r\n");
                    }
                }
                else
                {
                    double[] dDataToBeSent = new double[4];

                    string sSentBools = "";

                    for (int i = 0; i < dDataToBeSent.Length; i++)
                    {

                        dDataToBeSent[i] = Convert.ToSingle(Convert.ToInt16(rndShortData.Next(-32678, 32676)) / 3.5);

                        sSentBools += dDataToBeSent[i].ToString() + " ";
                    }

                    rtbLog.AppendText("发送值[0~" + (dDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                    if (ModbusTCP.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, dDataToBeSent) == true)//1111      
                    {
                        rtbLog.AppendText("成功执行写命令\r\n");
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行写命令\r\n");
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusTCP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // 
        private void btnTCPWriteLongKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                Random rndShortData = new Random();

                if (bReadWriteArray == false)//bWriteSingleCoil
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong dValue = (ulong)(rndShortData.Next(int.MinValue, int.MaxValue) * rndShortData.Next(int.MinValue, int.MaxValue));

                        if (ModbusTCP.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, dValue) == true)//1111      
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", dValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        long dValue = rndShortData.Next(int.MinValue, int.MaxValue) * rndShortData.Next(int.MinValue, int.MaxValue);

                        if (ModbusTCP.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, dValue) == true)//1111      
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", dValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong[] dDataToBeSent = new ulong[4];

                        string sSentBools = "";

                        for (int i = 0; i < dDataToBeSent.Length; i++)
                        {

                            dDataToBeSent[i] = (ulong)(rndShortData.Next(int.MinValue, int.MaxValue) * rndShortData.Next(int.MinValue, int.MaxValue));

                            sSentBools += dDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (dDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusTCP.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, dDataToBeSent) == true)//1111      
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        long[] dDataToBeSent = new long[4];

                        string sSentBools = "";

                        for (int i = 0; i < dDataToBeSent.Length; i++)
                        {

                            dDataToBeSent[i] = rndShortData.Next(int.MinValue, int.MaxValue) * rndShortData.Next(int.MinValue, int.MaxValue);

                            sSentBools += dDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (dDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusTCP.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, dDataToBeSent) == true)//1111      
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusTCP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        
        #endregion

        #endregion

        #region "ModbusUDP - ok"

        #region "Coil"

        // ok
        private void btnUDPReadCoilBit_Click(object sender, EventArgs e)
        {
            try
            {
                //string sReadResult = "";

                if (bReadWriteArray == false)//bReadSingleBit
                {
                    bool bReadBitValue = false;
                    if (ModbusUDP.ReadCoilBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref bReadBitValue))
                    {
                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }
                else
                {
                    bool[] bReadBitsValue = null;
                    if (ModbusUDP.ReadCoilBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref bReadBitsValue))
                    {
                        //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                        string sBitsResults = "";
                        for (int i = 0; i < bReadBitsValue.Length; i++)
                        //for (int i = bReadBitsValue.Length - 1; i >= 0; i--)
                        {
                            sBitsResults += bReadBitsValue[i].ToString() + " ";
                        }

                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusUDP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        //
        private void btnUDPReadCoilByte_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        byte byReadValue = 0;
                        if (ModbusUDP.ReadCoilByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref byReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", byReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        sbyte byReadValue = 0;
                        if (ModbusUDP.ReadCoilByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref byReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", byReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        byte[] byReadValue = null;
                        if (ModbusUDP.ReadCoilByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref byReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < byReadValue.Length; i++)
                            {
                                sBitsResults += byReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        sbyte[] byReadValue = null;
                        if (ModbusUDP.ReadCoilByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref byReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < byReadValue.Length; i++)
                            {
                                sBitsResults += byReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusUDP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        //
        private void btnUDPReadCoilWord_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort stReadValue = 0;
                        if (ModbusUDP.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref stReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", stReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        short stReadValue = 0;
                        if (ModbusUDP.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref stReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", stReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort[] stReadValue = null;
                        if (ModbusUDP.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref stReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < stReadValue.Length; i++)
                            {
                                sBitsResults += stReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        short[] stReadValue = null;
                        if (ModbusUDP.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref stReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < stReadValue.Length; i++)
                            {
                                sBitsResults += stReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusUDP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        //
        private void btnUDPReadCoilInt_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        uint iReadValue = 0;
                        if (ModbusUDP.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref iReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", iReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        int iReadValue = 0;
                        if (ModbusUDP.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref iReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", iReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        uint[] iReadValue = null;
                        if (ModbusUDP.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref iReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < iReadValue.Length; i++)
                            {
                                sBitsResults += iReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        int[] iReadValue = null;
                        if (ModbusUDP.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref iReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < iReadValue.Length; i++)
                            {
                                sBitsResults += iReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusUDP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        //
        private void btnUDPReadCoilLong_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong lReadValue = 0;
                        if (ModbusUDP.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref lReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", lReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        long lReadValue = 0;
                        if (ModbusUDP.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref lReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", lReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong[] lReadValue = null;
                        if (ModbusUDP.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref lReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < lReadValue.Length; i++)
                            {
                                sBitsResults += lReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        long[] lReadValue = null;
                        if (ModbusUDP.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref lReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < lReadValue.Length; i++)
                            {
                                sBitsResults += lReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusUDP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ok
        private void btnUDPWriteCoilBit_Click(object sender, EventArgs e)
        {
            try
            {
                //bool bWriteSingleCoil = false;

                if (bReadWriteArray == false)//bWriteSingleCoil
                {
                    bWriteBitValue = !bWriteBitValue;

                    if (ModbusUDP.WriteCoilBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, bWriteBitValue) == true)//1111      
                    {
                        rtbLog.AppendText(string.Format("成功执行写命令:{0}\r\n", bWriteBitValue));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行写命令\r\n");
                    }
                }
                else
                {
                    bool[] bDataToBeSent = new bool[16];

                    Random rndData = new Random();

                    string sSentBools = "";

                    for (int i = 0; i < bDataToBeSent.Length; i++)
                    {
                        if (rndData.Next(1, 16) % 2 == 0)
                        {
                            bDataToBeSent[i] = true;
                        }
                        else
                        {
                            bDataToBeSent[i] = false;
                        }

                        sSentBools += bDataToBeSent[i].ToString() + " ";
                    }

                    rtbLog.AppendText("发送值[0~" + (bDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                    if (ModbusUDP.WriteCoilBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, bDataToBeSent) == true)//1111      
                    {
                        rtbLog.AppendText("成功执行写命令\r\n");
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行写命令\r\n");
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusUDP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        //
        private void btnUDPWriteCoilByte_Click(object sender, EventArgs e)
        {
            try
            {
                Random rndData = new Random();

                if (bReadWriteArray == false)
                {
                    int iTemp = rndData.Next(0, 255);
                    byte[] byTemp = BitConverter.GetBytes(iTemp);

                    if (bReadUnsignedData == true)
                    {
                        byte byWriteValue = byTemp[0];

                        if (ModbusUDP.WriteCoilByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, byWriteValue) == true)
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", byWriteValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        sbyte byWriteValue = (sbyte)byTemp[0];

                        if (ModbusUDP.WriteCoilByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, byWriteValue) == true)
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", byWriteValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        byte[] byDataToBeSent = new byte[2];

                        string sSentBools = "";

                        for (int i = 0; i < byDataToBeSent.Length; i++)
                        {
                            int iTemp = rndData.Next(byte.MinValue, byte.MaxValue);
                            byte[] byTemp = BitConverter.GetBytes(iTemp);
                            byte byWriteValue = byTemp[0];
                            byDataToBeSent[i] = byWriteValue;

                            sSentBools += byDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (byDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusUDP.WriteCoilByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, byDataToBeSent) == true)//1111      
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        sbyte[] byDataToBeSent = new sbyte[2];

                        string sSentBools = "";

                        for (int i = 0; i < byDataToBeSent.Length; i++)
                        {
                            int iTemp = rndData.Next(sbyte.MinValue, sbyte.MaxValue);
                            byte[] byTemp = BitConverter.GetBytes(iTemp);
                            byte byWriteValue = byTemp[0];
                            byDataToBeSent[i] = (sbyte)byWriteValue;

                            sSentBools += byDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (byDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusUDP.WriteCoilByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, byDataToBeSent) == true)//1111      
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusUDP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        //
        private void btnUDPWriteCoilWord_Click(object sender, EventArgs e)
        {
            try
            {
                Random rndData = new Random();

                if (bReadWriteArray == false)
                {
                    int iTemp = bReadUnsignedData ? rndData.Next(ushort.MinValue, ushort.MaxValue) : rndData.Next(short.MinValue, short.MaxValue);// rndData.Next(-32768, 32767);
                    byte[] byTemp = BitConverter.GetBytes(iTemp);

                    if (bReadUnsignedData == true)
                    {
                        ushort stWriteValue = BitConverter.ToUInt16(byTemp, 0);

                        if (ModbusUDP.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, stWriteValue) == true)
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", stWriteValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        short stWriteValue = BitConverter.ToInt16(byTemp, 0);

                        if (ModbusUDP.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, stWriteValue) == true)
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", stWriteValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort[] stDataToBeSent = new ushort[2];

                        string sSentBools = "";

                        for (int i = 0; i < stDataToBeSent.Length; i++)
                        {
                            int iTemp = rndData.Next(ushort.MinValue, ushort.MaxValue);//rndData.Next(-32768, 32767);
                            byte[] byTemp = BitConverter.GetBytes(iTemp);
                            stDataToBeSent[i] = BitConverter.ToUInt16(byTemp, 0);

                            sSentBools += stDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (stDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusUDP.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, stDataToBeSent) == true)
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        short[] stDataToBeSent = new short[2];

                        string sSentBools = "";

                        for (int i = 0; i < stDataToBeSent.Length; i++)
                        {
                            int iTemp = rndData.Next(short.MinValue, short.MaxValue);//rndData.Next(-32768, 32767);
                            byte[] byTemp = BitConverter.GetBytes(iTemp);
                            stDataToBeSent[i] = BitConverter.ToInt16(byTemp, 0);

                            sSentBools += stDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (stDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusUDP.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, stDataToBeSent) == true)
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusUDP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        //
        private void btnUDPWriteCoilInt_Click(object sender, EventArgs e)
        {
            try
            {
                Random rndData = new Random();

                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        uint iTemp = (uint)rndData.Next(int.MinValue, int.MaxValue);
                        if (ModbusUDP.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, iTemp) == true)
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", iTemp.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        int iTemp = rndData.Next(int.MinValue, int.MaxValue);
                        if (ModbusUDP.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, iTemp) == true)
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", iTemp.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        uint[] iDataToBeSent = new uint[2];

                        string sSentBools = "";

                        for (int i = 0; i < iDataToBeSent.Length; i++)
                        {
                            iDataToBeSent[i] = (uint)rndData.Next(int.MinValue, int.MaxValue);
                            sSentBools += iDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (iDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusUDP.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, iDataToBeSent) == true)
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        int[] iDataToBeSent = new int[2];

                        string sSentBools = "";

                        for (int i = 0; i < iDataToBeSent.Length; i++)
                        {
                            iDataToBeSent[i] = rndData.Next(int.MinValue, int.MaxValue);
                            sSentBools += iDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (iDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusUDP.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, iDataToBeSent) == true)
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusUDP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        //
        private void btnUDPWriteCoilLong_Click(object sender, EventArgs e)
        {
            try
            {
                Random rndData = new Random();

                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong lTemp = (ulong)(rndData.Next(int.MinValue, int.MaxValue) * rndData.Next(int.MinValue, int.MaxValue));
                        if (ModbusUDP.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, lTemp) == true)
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", lTemp.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        long lTemp = rndData.Next(int.MinValue, int.MaxValue) * rndData.Next(int.MinValue, int.MaxValue);
                        if (ModbusUDP.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, lTemp) == true)
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", lTemp.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong[] lDataToBeSent = new ulong[2];

                        string sSentBools = "";

                        for (int i = 0; i < lDataToBeSent.Length; i++)
                        {
                            lDataToBeSent[i] = (ulong)(rndData.Next(int.MinValue, int.MaxValue) * rndData.Next(int.MinValue, int.MaxValue));
                            sSentBools += lDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (lDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusUDP.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, lDataToBeSent) == true)
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        long[] lDataToBeSent = new long[2];

                        string sSentBools = "";

                        for (int i = 0; i < lDataToBeSent.Length; i++)
                        {
                            lDataToBeSent[i] = rndData.Next(int.MinValue, int.MaxValue) * rndData.Next(int.MinValue, int.MaxValue);
                            sSentBools += lDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (lDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusUDP.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, lDataToBeSent) == true)
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusUDP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        #endregion

        #region "InputRegister"

        //
        private void btnUDPReadInputRegisterShort_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort stValue = 0;
                        if (ModbusUDP.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref stValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", stValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        short stValue = 0;
                        if (ModbusUDP.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref stValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", stValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort[] stValue = null;
                        if (ModbusUDP.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref stValue))
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < stValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += stValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        short[] stValue = null;
                        if (ModbusUDP.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref stValue))
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < stValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += stValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusUDP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        //
        private void btnUDPReadInputRegisterInt_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        uint iValue = 0;
                        if (ModbusUDP.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref iValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", iValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        int iValue = 0;
                        if (ModbusUDP.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref iValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", iValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        uint[] iValue = null;
                        if (ModbusUDP.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref iValue))
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < iValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += iValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        int[] iValue = null;
                        if (ModbusUDP.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref iValue))
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < iValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += iValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusUDP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        //
        private void btnUDPReadInputRegisterFloat_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    float fValue = 0;
                    if (ModbusUDP.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref fValue))
                    {
                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", fValue.ToString()));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }
                else
                {
                    float[] fValue = null;
                    if (ModbusUDP.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref fValue))
                    {
                        //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                        string sBitsResults = "";
                        for (int i = 0; i < fValue.Length; i++)
                        //for (int i = iValue.Length - 1; i >= 0; i--)
                        {
                            sBitsResults += fValue[i].ToString() + " ";
                        }

                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusUDP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // NG
        private void button45_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong lValue = 0;
                        if (ModbusUDP.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref lValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", lValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        long lValue = 0;
                        if (ModbusUDP.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref lValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", lValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong[] lValue = null;
                        if (ModbusUDP.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref lValue))
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < lValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += lValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        long[] lValue = null;
                        if (ModbusUDP.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref lValue))
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < lValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += lValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusUDP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // NG
        private void button48_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    double dValue = 0;
                    if (ModbusUDP.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref dValue))
                    {
                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", dValue.ToString()));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }
                else
                {
                    double[] dValue = null;
                    if (ModbusUDP.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref dValue))
                    {
                        //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                        string sBitsResults = "";
                        for (int i = 0; i < dValue.Length; i++)
                        //for (int i = iValue.Length - 1; i >= 0; i--)
                        {
                            sBitsResults += dValue[i].ToString() + " ";
                        }

                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusUDP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        #endregion

        #region "InputBit"

        //
        private void btnUDPReadInputBit_Click(object sender, EventArgs e)
        {
            try
            {
                //bool bReadSingleBit = false;

                if (bReadWriteArray == false)//bReadSingleBit
                {
                    bool bReadBitValue = false;
                    if (ModbusUDP.ReadInputBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref bReadBitValue))
                    {
                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }
                else
                {
                    bool[] bReadBitsValue = null;
                    if (ModbusUDP.ReadInputBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref bReadBitsValue))
                    {
                        //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                        string sBitsResults = "";
                        for (int i = 0; i < bReadBitsValue.Length; i++)
                        //for (int i = bReadBitsValue.Length - 1; i >= 0; i--)
                        {
                            sBitsResults += bReadBitsValue[i].ToString() + " ";
                        }

                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusUDP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        //
        private void btnUDPReadInputByte_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        byte byReadValue = 0;
                        if (ModbusUDP.ReadInputByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref byReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", byReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        sbyte byReadValue = 0;
                        if (ModbusUDP.ReadInputByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref byReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", byReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        byte[] byReadValue = null;
                        if (ModbusUDP.ReadInputByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref byReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < byReadValue.Length; i++)
                            {
                                sBitsResults += byReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        sbyte[] byReadValue = null;
                        if (ModbusUDP.ReadInputByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref byReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < byReadValue.Length; i++)
                            {
                                sBitsResults += byReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusUDP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        //
        private void btnUDPReadInputShort_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort stReadValue = 0;
                        if (ModbusUDP.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref stReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", stReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        short stReadValue = 0;
                        if (ModbusUDP.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref stReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", stReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort[] stReadValue = null;
                        if (ModbusUDP.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref stReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < stReadValue.Length; i++)
                            {
                                sBitsResults += stReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        short[] stReadValue = null;
                        if (ModbusUDP.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref stReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < stReadValue.Length; i++)
                            {
                                sBitsResults += stReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusUDP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        //
        private void btnUDPReadInputInt_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        uint iReadValue = 0;
                        if (ModbusUDP.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref iReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", iReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        int iReadValue = 0;
                        if (ModbusUDP.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref iReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", iReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        uint[] iReadValue = null;
                        if (ModbusUDP.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref iReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < iReadValue.Length; i++)
                            {
                                sBitsResults += iReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        int[] iReadValue = null;
                        if (ModbusUDP.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref iReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < iReadValue.Length; i++)
                            {
                                sBitsResults += iReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusUDP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        //
        private void btnUDPReadInputLong_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong lReadValue = 0;
                        if (ModbusUDP.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref lReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", lReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        long lReadValue = 0;
                        if (ModbusUDP.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref lReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", lReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong[] lReadValue = null;
                        if (ModbusUDP.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref lReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < lReadValue.Length; i++)
                            {
                                sBitsResults += lReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        long[] lReadValue = null;
                        if (ModbusUDP.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref lReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < lReadValue.Length; i++)
                            {
                                sBitsResults += lReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusUDP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        #endregion

        #region "KeepRegister"

        //
        private void btnUDPReadShortKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort stValue = 0;
                        if (ModbusUDP.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref stValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", stValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        short stValue = 0;
                        if (ModbusUDP.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref stValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", stValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort[] stValue = null;
                        if (ModbusUDP.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 4, ref stValue))
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < stValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += stValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        short[] stValue = null;
                        if (ModbusUDP.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 4, ref stValue))
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < stValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += stValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusUDP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        //
        private void btnUDPReadIntKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        uint iValue = 0;
                        if (ModbusUDP.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref iValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", iValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        int iValue = 0;
                        if (ModbusUDP.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref iValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", iValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        uint[] iValue = null;
                        if (ModbusUDP.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref iValue))
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < iValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += iValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        int[] iValue = null;
                        if (ModbusUDP.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref iValue))
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < iValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += iValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusUDP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        //
        private void btnUDPReadFloatKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    float fValue = 0;
                    if (ModbusUDP.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref fValue))
                    {
                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", fValue.ToString()));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }
                else
                {
                    float[] fValue = null;
                    if (ModbusUDP.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref fValue))
                    {
                        //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                        string sBitsResults = "";
                        for (int i = 0; i < fValue.Length; i++)
                        //for (int i = iValue.Length - 1; i >= 0; i--)
                        {
                            sBitsResults += fValue[i].ToString() + " ";
                        }

                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusUDP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        //
        private void btnUDPReadDoubleKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    double dValue = 0;
                    if (ModbusUDP.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref dValue))
                    {
                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", dValue.ToString()));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }
                else
                {
                    double[] dValue = null;
                    if (ModbusUDP.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref dValue))
                    {
                        //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                        string sBitsResults = "";
                        for (int i = 0; i < dValue.Length; i++)
                        //for (int i = iValue.Length - 1; i >= 0; i--)
                        {
                            sBitsResults += dValue[i].ToString() + " ";
                        }

                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusUDP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        //
        private void btnUDPReadLongKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong lValue = 0;
                        if (ModbusUDP.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref lValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", lValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        long lValue = 0;
                        if (ModbusUDP.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref lValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", lValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong[] lValue = null;
                        if (ModbusUDP.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref lValue))
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < lValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += lValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        long[] lValue = null;
                        if (ModbusUDP.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref lValue))
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < lValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += lValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusUDP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        //
        private void btnUDPWriteShortKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                Random rndShortData = new Random();

                if (bReadWriteArray == false)//bWriteSingleCoil
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort stValue = Convert.ToUInt16(rndShortData.Next(ushort.MinValue, ushort.MaxValue));//-32678, 32676));

                        if (ModbusUDP.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, stValue) == true)//1111      
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", stValue));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        short stValue = Convert.ToInt16(rndShortData.Next(short.MinValue, short.MaxValue));//-32678, 32676));

                        if (ModbusUDP.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, stValue) == true)//1111      
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", stValue));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort[] stDataToBeSent = new ushort[5];

                        string sSentBools = "";

                        for (int i = 0; i < stDataToBeSent.Length; i++)
                        {

                            stDataToBeSent[i] = Convert.ToUInt16(rndShortData.Next(ushort.MinValue, ushort.MaxValue));

                            sSentBools += stDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (stDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusUDP.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, stDataToBeSent) == true)//1111      
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        short[] stDataToBeSent = new short[5];

                        string sSentBools = "";

                        for (int i = 0; i < stDataToBeSent.Length; i++)
                        {

                            stDataToBeSent[i] = Convert.ToInt16(rndShortData.Next(short.MinValue, short.MaxValue));

                            sSentBools += stDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (stDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusUDP.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, stDataToBeSent) == true)//1111      
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusUDP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        //
        private void btnUDPWriteIntKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                Random rndShortData = new Random();

                if (bReadWriteArray == false)//bWriteSingleCoil
                {
                    if (bReadUnsignedData == true)
                    {
                        uint iValue = (uint)rndShortData.Next(int.MinValue, int.MaxValue);

                        if (ModbusUDP.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, iValue) == true)//1111      
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", iValue));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        int iValue = rndShortData.Next(int.MinValue, int.MaxValue);

                        if (ModbusUDP.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, iValue) == true)//1111      
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", iValue));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        uint[] iDataToBeSent = new uint[4];

                        string sSentBools = "";

                        for (int i = 0; i < iDataToBeSent.Length; i++)
                        {

                            iDataToBeSent[i] = (uint)rndShortData.Next(int.MinValue, int.MaxValue);

                            sSentBools += iDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (iDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusUDP.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, iDataToBeSent) == true)//1111      
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        int[] iDataToBeSent = new int[4];

                        string sSentBools = "";

                        for (int i = 0; i < iDataToBeSent.Length; i++)
                        {

                            iDataToBeSent[i] = rndShortData.Next(int.MinValue, int.MaxValue);

                            sSentBools += iDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (iDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusUDP.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, iDataToBeSent) == true)//1111      
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusUDP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        //
        private void btnUDPWriteFloatKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                Random rndShortData = new Random();

                if (bReadWriteArray == false)//bWriteSingleCoil
                {
                    float fValue = Convert.ToSingle(Convert.ToInt16(rndShortData.Next(-32678, 32676)) / 3.5);

                    if (ModbusUDP.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, fValue) == true)//1111      
                    {
                        rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", fValue.ToString()));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行写命令\r\n");
                    }
                }
                else
                {
                    float[] fDataToBeSent = new float[4];

                    string sSentBools = "";

                    for (int i = 0; i < fDataToBeSent.Length; i++)
                    {

                        fDataToBeSent[i] = Convert.ToSingle(Convert.ToInt16(rndShortData.Next(-32678, 32676)) / 3.5);

                        sSentBools += fDataToBeSent[i].ToString() + " ";
                    }

                    rtbLog.AppendText("发送值[0~" + (fDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                    if (ModbusUDP.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, fDataToBeSent) == true)//1111      
                    {
                        rtbLog.AppendText("成功执行写命令\r\n");
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行写命令\r\n");
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusUDP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        //
        private void btnUDPWriteDoubleKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                Random rndShortData = new Random();

                if (bReadWriteArray == false)//bWriteSingleCoil
                {
                    double dValue = Convert.ToDouble(Convert.ToInt16(rndShortData.Next(-32678, 32676)) / 3.5);

                    if (ModbusUDP.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, dValue) == true)//1111      
                    {
                        rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", dValue.ToString()));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行写命令\r\n");
                    }
                }
                else
                {
                    double[] dDataToBeSent = new double[4];

                    string sSentBools = "";

                    for (int i = 0; i < dDataToBeSent.Length; i++)
                    {

                        dDataToBeSent[i] = Convert.ToSingle(Convert.ToInt16(rndShortData.Next(-32678, 32676)) / 3.5);

                        sSentBools += dDataToBeSent[i].ToString() + " ";
                    }

                    rtbLog.AppendText("发送值[0~" + (dDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                    if (ModbusUDP.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, dDataToBeSent) == true)//1111      
                    {
                        rtbLog.AppendText("成功执行写命令\r\n");
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行写命令\r\n");
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusUDP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        //
        private void btnUDPWriteLongKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                Random rndShortData = new Random();

                if (bReadWriteArray == false)//bWriteSingleCoil
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong dValue = (ulong)(rndShortData.Next(int.MinValue, int.MaxValue) * rndShortData.Next(int.MinValue, int.MaxValue));

                        if (ModbusUDP.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, dValue) == true)//1111      
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", dValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        long dValue = rndShortData.Next(int.MinValue, int.MaxValue) * rndShortData.Next(int.MinValue, int.MaxValue);

                        if (ModbusUDP.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, dValue) == true)//1111      
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", dValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong[] dDataToBeSent = new ulong[4];

                        string sSentBools = "";

                        for (int i = 0; i < dDataToBeSent.Length; i++)
                        {

                            dDataToBeSent[i] = (ulong)(rndShortData.Next(int.MinValue, int.MaxValue) * rndShortData.Next(int.MinValue, int.MaxValue));

                            sSentBools += dDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (dDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusUDP.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, dDataToBeSent) == true)//1111      
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        long[] dDataToBeSent = new long[4];

                        string sSentBools = "";

                        for (int i = 0; i < dDataToBeSent.Length; i++)
                        {

                            dDataToBeSent[i] = rndShortData.Next(int.MinValue, int.MaxValue) * rndShortData.Next(int.MinValue, int.MaxValue);

                            sSentBools += dDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (dDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusUDP.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, dDataToBeSent) == true)//1111      
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusUDP.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        #endregion

        #endregion

        private void btnConnectTCPSlave_Click(object sender, EventArgs e)
        {
            try
            {
                if (rdbTCP.Checked == false && rdbUDP.Checked == false)
                {
                    rdbTCP.Checked = true;
                }

                if (bUseSocketToTest == true)
                {
                    #region "用SOCKET进行测试"

                    if (null != ModbusSocket)
                    {
                        ModbusSocket.Dispose();
                        ModbusSocket = null;
                    }

                    ModbusSocket = new CModbusSocket("PDN", txtServerIPAddress.Text, (ushort)nudServerPort.Value, rdbTCP.Checked ? SocketDefinition.TCP : SocketDefinition.UDP);

                    #endregion
                }
                else
                {
                    #region "分TCP/UDP进行测试"

                    if (rdbTCP.Checked == false && rdbUDP.Checked == true)
                    {
                        if (null != ModbusUDP)
                        {
                            ModbusUDP.Dispose();
                            ModbusUDP = null;
                        }

                        ModbusUDP = new CModbusUDP("PDN", txtServerIPAddress.Text, (ushort)nudServerPort.Value, 1000, 1000);//, LockerType.ExchangeLock);//AutoResetEventLock  ExchangeLock  CountdownEventLock 

                        if (chkTestThreadLockForReadWrite.Checked == true)
                        {
                            if (null == NewReadTask)
                            {
                                stUsedTimeForRead.Restart();

                                NewReadTask = new Task(() =>
                                {
                                    #region "读线程"

                                    while (true)
                                    {
                                        try
                                        {
                                            if (bUDPIsConnectedBackForRead != ModbusUDP.IsConnected)
                                            {
                                                bUDPIsConnectedBackForRead = ModbusUDP.IsConnected;

                                                if (bUDPIsConnectedBackForRead == false)
                                                {
                                                    stUsedTimeForRead.Stop();
                                                }
                                                else
                                                {
                                                    stUsedTimeForRead.Start();
                                                }
                                            }

                                            if (bTestThreadLock == false)
                                            {
                                                continue;
                                            }

                                            //Thread.Sleep(2);

                                            bool bResult = false;
                                            if (ModbusUDP.ReadCoilBit(1, 0, ref bResult) == true)
                                            {
                                                if (true)//bReadResultBackup != bResult)
                                                {
                                                    bReadResultBackup = bResult;
                                                    this.Invoke(new Action(() =>
                                                    {
                                                        this.rtbLog.AppendText("读取变量:" + bResult.ToString() + "\r\n");
                                                    }));
                                                }
                                            }
                                            else
                                            {
                                                this.Invoke(new Action(() =>
                                                {
                                                    this.rtbLog.AppendText("读取变量失败" + "\r\n");

                                                    while (true)
                                                    {
                                                        lock (this)
                                                        {
                                                            string sTempErrMsg = ModbusUDP.GetInfo();
                                                            if (string.IsNullOrEmpty(sTempErrMsg) == true)
                                                            {
                                                                break;
                                                            }
                                                            else
                                                            {
                                                                rtbErrLog.AppendText("读变量失败:" + sTempErrMsg + "\r\n");
                                                            }
                                                        }
                                                    }
                                                }));
                                            }
                                        }
                                        catch (Exception)
                                        {
                                        }
                                    }

                                    #endregion
                                });

                                NewReadTask.Start();
                            }

                            if (null == NewWriteTask)
                            {
                                stUsedTimeForWrite.Restart();

                                NewWriteTask = new Task(() =>
                                {
                                    #region "写线程"

                                    while (true)
                                    {
                                        try
                                        {
                                            if (bUDPIsConnectedBackForWrite != ModbusUDP.IsConnected)
                                            {
                                                bUDPIsConnectedBackForWrite = ModbusUDP.IsConnected;

                                                if (bUDPIsConnectedBackForWrite == false)
                                                {
                                                    stUsedTimeForRead.Stop();
                                                }
                                                else
                                                {
                                                    stUsedTimeForRead.Start();
                                                }
                                            }

                                            if (bTestThreadLock == false)
                                            {
                                                continue;
                                            }

                                            //Thread.Sleep(2);

                                            bDataToBeSent = !bDataToBeSent;
                                            if (ModbusUDP.WriteCoilBit(1, 0, bDataToBeSent) == true)
                                            {
                                                this.Invoke(new Action(() =>
                                                {
                                                    this.rtbLog.AppendText("写变量:" + bDataToBeSent.ToString() + "\r\n");
                                                }));
                                            }
                                            else
                                            {
                                                this.Invoke(new Action(() =>
                                                {
                                                    this.rtbLog.AppendText("写变量失败" + "\r\n");

                                                    while (true)
                                                    {
                                                        lock (this)
                                                        {
                                                            string sTempErrMsg = ModbusUDP.GetInfo();
                                                            if (string.IsNullOrEmpty(sTempErrMsg) == true)
                                                            {
                                                                break;
                                                            }
                                                            else
                                                            {
                                                                rtbErrLog.AppendText("写变量失败:" + sTempErrMsg + "\r\n");
                                                            }
                                                        }
                                                    }
                                                }));
                                            }
                                        }
                                        catch (Exception)
                                        {
                                        }

                                    }

                                    #endregion
                                });

                                NewWriteTask.Start();
                            }
                        }
                    }

                    if (rdbTCP.Checked == true && rdbUDP.Checked == false)
                    {
                        if (null != ModbusTCP)
                        {
                            ModbusTCP.Dispose();
                            ModbusTCP = null;
                        }

                        ModbusTCP = new CModbusTCP("PDN", txtServerIPAddress.Text, (ushort)nudServerPort.Value);
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        bool bTCPIsConnected = false;

        bool bUDPIsConnected = false;

        bool bSocketIsConnected = false;

        private void timer1_Tick(object sender, EventArgs e)
        {
            try
            {
                lblUsedTimeForWrite.Text = Convert.ToDouble(stUsedTimeForWrite.ElapsedMilliseconds).ToString();
                lblUsedTimeForRead.Text = Convert.ToDouble(stUsedTimeForRead.ElapsedMilliseconds).ToString();

                if (bUseSocketToTest == true)
                {
                    #region "Socket"

                    if (null != ModbusSocket)
                    {
                        bSocketIsConnected = ModbusSocket.IsConnected;

                        if (bSocketIsConnected == true)
                        {
                            lblReceiveID.Text = ModbusSocket.MsgIDForReading.ToString();
                            lblSendID.Text = ModbusSocket.MsgIDForWriting.ToString();
                        }
                    }
                    else
                    {
                        bSocketIsConnected = false;
                    }

                    #endregion
                }
                else
                {
                    #region "TCP/UDP"

                    if (null != ModbusTCP)
                    {
                        bTCPIsConnected = ModbusTCP.IsConnected;

                        //if (ModbusTCP.IsConnected == true)
                        //{
                        //    btnShowTCPConnectStatus.BackColor = Color.Green;
                        //}
                        //else
                        //{
                        //    btnShowTCPConnectStatus.BackColor = Color.Red;
                        //}

                        if (bTCPIsConnected == true && bUDPIsConnected == false)
                        {
                            lblReceiveID.Text = ModbusTCP.MsgIDForReading.ToString();
                            lblSendID.Text = ModbusTCP.MsgIDForWriting.ToString();
                        }
                    }
                    else
                    {
                        bTCPIsConnected = false;
                        //btnShowTCPConnectStatus.BackColor = Color.Red;
                    }

                    if (null != ModbusUDP)
                    {
                        bUDPIsConnected = ModbusUDP.IsConnected;

                        if (bTCPIsConnected == false && bUDPIsConnected == true)
                        {
                            lblReceiveID.Text = ModbusUDP.MsgIDForReading.ToString();
                            lblSendID.Text = ModbusUDP.MsgIDForWriting.ToString();
                        }
                    }
                    else
                    {
                        bUDPIsConnected = false;
                    }

                    #endregion
                }

                if (bTCPIsConnected == true || bUDPIsConnected == true || bSocketIsConnected == true)
                {
                    btnShowTCPConnectStatus.BackColor = Color.Green;
                }
                else
                {
                    btnShowTCPConnectStatus.BackColor = Color.Red;
                }

            }
            catch (Exception)
            {
            }
        }

        bool bUDPIsConnectedBackForRead = false;

        bool bUDPIsConnectedBackForWrite = false;

        bool bReadResultBackup = false;

        bool bDataToBeSent = false;

        Stopwatch stUsedTimeForRead = new Stopwatch();

        Stopwatch stUsedTimeForWrite = new Stopwatch();

        Task NewReadTask = null;

        Task NewWriteTask = null;

        bool bTestThreadLock = true;

        private void btnShowTCPConnectStatus_Click(object sender, EventArgs e)
        {
            bTestThreadLock = !bTestThreadLock;
            if (bTestThreadLock == true)
            {
                stUsedTimeForRead.Restart();
                stUsedTimeForWrite.Restart();
            }
            else
            {
                stUsedTimeForRead.Stop();
                stUsedTimeForWrite.Stop();
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                if (null != NewReadTask)
                {
                    NewReadTask.Dispose();//只有在任务处于完成状态(RanToCompletion、Faulted 或 Canceled)时才能释放它。
                    NewReadTask.Wait(100, new CancellationToken(true));//.Dispose();
                }

                if (null != NewWriteTask)
                {
                    NewWriteTask.Dispose();//只有在任务处于完成状态(RanToCompletion、Faulted 或 Canceled)时才能释放它。
                }
            }
            catch (Exception)
            {
            }
        }

        Task NewTaskThead = null;

        int iCountForNewTaskThead = 0;

        private void btnNewTaskThead_Click(object sender, EventArgs e)
        {
            btnNewTaskThead.Enabled = false;

            try
            {
                //iCountForNewTaskThead = 0;
                if (null == NewTaskThead)
                {

                    #region "Task.Factory.StartNew(() =>"

                    Task TempTask = Task.Factory.StartNew(() =>
                    {
                        this.Invoke(new Action(() =>
                        {
                            iCountForNewTaskThead++;
                            lblCountForNewTask.Text = iCountForNewTaskThead.ToString();

                            //Application.DoEvents();
                            //Thread.Sleep(2000);
                        }));
                    });

                    TempTask.Wait(100);
                    //TempTask.Wait();//如果不添加等待时间，就会卡住在这里，线程里面也没有执行更新，但是在控制台里面是OK的

                    if (TempTask.IsCompleted == true)
                    {
                        TempTask.Dispose();
                        TempTask = null;
                    }

                    #endregion

                    #region "new Task(() =>"

                    NewTaskThead = new Task(() =>
                    {
                        this.Invoke(new Action(() =>
                        {
                            iCountForNewTaskThead++;
                            lblCountForNewTask.Text = iCountForNewTaskThead.ToString();

                            Application.DoEvents();
                            //Thread.Sleep(2000);
                        }));
                    });

                    //使用 Task.Factory.StartNew 方式时报错：不能对已开始的任务调用 Start。
                    NewTaskThead.Start();//实例化方式为 new Task(() => 时使用

                    NewTaskThead.Wait();

                    //while (NewTaskThead.IsCompleted == false)
                    //{
                    //    Application.DoEvents();
                    //}

                    NewTaskThead.Dispose();
                    NewTaskThead = null;

                    #endregion
                }

                //if (NewTaskThead.IsCompleted == true)
                //{
                //    NewTaskThead.Start();
                //}

                //如果在实例化Task对象时先执行一次Start()，后面再次判断IsCompleted==true时就会报错：不能对已完成的任务调用 Start。
            }
            catch (Exception ex)
            {
                //不能对已完成的任务调用 Start。
                MessageBox.Show(ex.Message);
            }

            btnNewTaskThead.Enabled = true;
        }

        /// <summary>
        /// 读取无符号数据标志
        /// </summary>
        bool bReadUnsignedData = false;

        private void chkReadUnsignedData_CheckedChanged(object sender, EventArgs e)
        {
            bReadUnsignedData = chkReadUnsignedData.Checked;
        }

        #region "Modbus - Socket"

        #region "Coil"

        private void btnSocketReadCoilBit_Click(object sender, EventArgs e)
        {
            try
            {
                //string sReadResult = "";

                if (bReadWriteArray == false)//bReadSingleBit
                {
                    bool bReadBitValue = false;
                    if (ModbusSocket.ReadCoilBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref bReadBitValue))
                    {
                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }
                else
                {
                    bool[] bReadBitsValue = null;
                    if (ModbusSocket.ReadCoilBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref bReadBitsValue))
                    {
                        //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                        string sBitsResults = "";
                        for (int i = 0; i < bReadBitsValue.Length; i++)
                        //for (int i = bReadBitsValue.Length - 1; i >= 0; i--)
                        {
                            sBitsResults += bReadBitsValue[i].ToString() + " ";
                        }

                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusSocket.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btnSocketReadCoilByte_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        byte byReadValue = 0;
                        if (ModbusSocket.ReadCoilByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref byReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", byReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        sbyte byReadValue = 0;
                        if (ModbusSocket.ReadCoilByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref byReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", byReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        byte[] byReadValue = null;
                        if (ModbusSocket.ReadCoilByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref byReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < byReadValue.Length; i++)
                            {
                                sBitsResults += byReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        sbyte[] byReadValue = null;
                        if (ModbusSocket.ReadCoilByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref byReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < byReadValue.Length; i++)
                            {
                                sBitsResults += byReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusSocket.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btnSocketReadCoilWord_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort stReadValue = 0;
                        if (ModbusSocket.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref stReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", stReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        short stReadValue = 0;
                        if (ModbusSocket.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref stReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", stReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort[] stReadValue = null;
                        if (ModbusSocket.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref stReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < stReadValue.Length; i++)
                            {
                                sBitsResults += stReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        short[] stReadValue = null;
                        if (ModbusSocket.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref stReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < stReadValue.Length; i++)
                            {
                                sBitsResults += stReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusSocket.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btnSocketReadCoilInt_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        uint iReadValue = 0;
                        if (ModbusSocket.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref iReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", iReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        int iReadValue = 0;
                        if (ModbusSocket.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref iReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", iReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        uint[] iReadValue = null;
                        if (ModbusSocket.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref iReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < iReadValue.Length; i++)
                            {
                                sBitsResults += iReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        int[] iReadValue = null;
                        if (ModbusSocket.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref iReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < iReadValue.Length; i++)
                            {
                                sBitsResults += iReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusSocket.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btnSocketReadCoilLong_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong lReadValue = 0;
                        if (ModbusSocket.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref lReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", lReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        long lReadValue = 0;
                        if (ModbusSocket.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref lReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", lReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong[] lReadValue = null;
                        if (ModbusSocket.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref lReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < lReadValue.Length; i++)
                            {
                                sBitsResults += lReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        long[] lReadValue = null;
                        if (ModbusSocket.ReadCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref lReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < lReadValue.Length; i++)
                            {
                                sBitsResults += lReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusSocket.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btnSocketWriteCoilBit_Click(object sender, EventArgs e)
        {
            try
            {
                //bool bWriteSingleCoil = false;

                if (bReadWriteArray == false)//bWriteSingleCoil
                {
                    bWriteBitValue = !bWriteBitValue;

                    if (ModbusSocket.WriteCoilBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, bWriteBitValue) == true)//1111      
                    {
                        rtbLog.AppendText(string.Format("成功执行写命令:{0}\r\n", bWriteBitValue));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行写命令\r\n");
                    }
                }
                else
                {
                    bool[] bDataToBeSent = new bool[16];

                    Random rndData = new Random();

                    string sSentBools = "";

                    for (int i = 0; i < bDataToBeSent.Length; i++)
                    {
                        if (rndData.Next(1, 16) % 2 == 0)
                        {
                            bDataToBeSent[i] = true;
                        }
                        else
                        {
                            bDataToBeSent[i] = false;
                        }

                        sSentBools += bDataToBeSent[i].ToString() + " ";
                    }

                    rtbLog.AppendText("发送值[0~" + (bDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                    if (ModbusSocket.WriteCoilBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, bDataToBeSent) == true)//1111      
                    {
                        rtbLog.AppendText("成功执行写命令\r\n");
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行写命令\r\n");
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusSocket.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btnSocketWriteCoilByte_Click(object sender, EventArgs e)
        {
            try
            {
                Random rndData = new Random();

                if (bReadWriteArray == false)
                {
                    int iTemp = bReadUnsignedData ? rndData.Next(byte.MinValue, byte.MaxValue) : rndData.Next(sbyte.MinValue, sbyte.MaxValue);//rndData.Next(0, 255);
                    byte[] byTemp = BitConverter.GetBytes(iTemp);

                    if (bReadUnsignedData == true)
                    {
                        byte byWriteValue = byTemp[0];

                        if (ModbusSocket.WriteCoilByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, byWriteValue) == true)
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", byWriteValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        sbyte byWriteValue = (sbyte)byTemp[0];

                        if (ModbusSocket.WriteCoilByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, byWriteValue) == true)
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", byWriteValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        byte[] byDataToBeSent = new byte[2];

                        string sSentBools = "";

                        for (int i = 0; i < byDataToBeSent.Length; i++)
                        {
                            int iTemp = rndData.Next(byte.MinValue, byte.MaxValue);//rndData.Next(0, 255);
                            byte[] byTemp = BitConverter.GetBytes(iTemp);
                            byte byWriteValue = byTemp[0];
                            byDataToBeSent[i] = byWriteValue;

                            sSentBools += byDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (byDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusSocket.WriteCoilByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, byDataToBeSent) == true)//1111      
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        sbyte[] byDataToBeSent = new sbyte[2];

                        string sSentBools = "";

                        for (int i = 0; i < byDataToBeSent.Length; i++)
                        {
                            int iTemp = rndData.Next(sbyte.MinValue, sbyte.MaxValue);
                            byte[] byTemp = BitConverter.GetBytes(iTemp);
                            byte byWriteValue = byTemp[0];
                            byDataToBeSent[i] = (sbyte)byWriteValue;

                            sSentBools += byDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (byDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusSocket.WriteCoilByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, byDataToBeSent) == true)//1111      
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusSocket.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btnSocketWriteCoilWord_Click(object sender, EventArgs e)
        {
            try
            {
                Random rndData = new Random();

                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        int iTemp = rndData.Next(ushort.MinValue, ushort.MaxValue);//rndData.Next(-32768, 32767);
                        byte[] byTemp = BitConverter.GetBytes(iTemp);
                        ushort stWriteValue = BitConverter.ToUInt16(byTemp, 0);

                        if (ModbusSocket.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, stWriteValue) == true)
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", stWriteValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        int iTemp = rndData.Next(short.MinValue, short.MaxValue);//rndData.Next(-32768, 32767);
                        byte[] byTemp = BitConverter.GetBytes(iTemp);
                        short stWriteValue = BitConverter.ToInt16(byTemp, 0);

                        if (ModbusSocket.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, stWriteValue) == true)
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", stWriteValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort[] stDataToBeSent = new ushort[2];

                        string sSentBools = "";

                        for (int i = 0; i < stDataToBeSent.Length; i++)
                        {
                            int iTemp = rndData.Next(ushort.MinValue, ushort.MaxValue);
                            byte[] byTemp = BitConverter.GetBytes(iTemp);
                            stDataToBeSent[i] = BitConverter.ToUInt16(byTemp, 0);

                            sSentBools += stDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (stDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusSocket.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, stDataToBeSent) == true)
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        short[] stDataToBeSent = new short[2];

                        string sSentBools = "";

                        for (int i = 0; i < stDataToBeSent.Length; i++)
                        {
                            int iTemp = rndData.Next(short.MinValue, short.MaxValue);
                            byte[] byTemp = BitConverter.GetBytes(iTemp);
                            stDataToBeSent[i] = BitConverter.ToInt16(byTemp, 0);

                            sSentBools += stDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (stDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusSocket.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, stDataToBeSent) == true)
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusSocket.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btnSocketWriteCoilInt_Click(object sender, EventArgs e)
        {
            try
            {
                Random rndData = new Random();

                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        uint iTemp = (uint)rndData.Next(int.MinValue, int.MaxValue);
                        if (ModbusSocket.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, iTemp) == true)
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", iTemp.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        int iTemp = rndData.Next(int.MinValue, int.MaxValue);
                        if (ModbusSocket.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, iTemp) == true)
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", iTemp.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        uint[] iDataToBeSent = new uint[2];

                        string sSentBools = "";

                        for (int i = 0; i < iDataToBeSent.Length; i++)
                        {
                            iDataToBeSent[i] = (uint)rndData.Next(int.MinValue, int.MaxValue);
                            sSentBools += iDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (iDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusSocket.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, iDataToBeSent) == true)
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        int[] iDataToBeSent = new int[2];

                        string sSentBools = "";

                        for (int i = 0; i < iDataToBeSent.Length; i++)
                        {
                            iDataToBeSent[i] = rndData.Next(int.MinValue, int.MaxValue);
                            sSentBools += iDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (iDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusSocket.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, iDataToBeSent) == true)
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusSocket.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btnSocketWriteCoilLong_Click(object sender, EventArgs e)
        {
            try
            {
                Random rndData = new Random();

                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong lTemp = (ulong)(rndData.Next(int.MinValue, int.MaxValue) * rndData.Next(int.MinValue, int.MaxValue));
                        if (ModbusSocket.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, lTemp) == true)
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", lTemp.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        long lTemp = rndData.Next(int.MinValue, int.MaxValue) * rndData.Next(int.MinValue, int.MaxValue);
                        if (ModbusSocket.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, lTemp) == true)
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", lTemp.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong[] lDataToBeSent = new ulong[2];

                        string sSentBools = "";

                        for (int i = 0; i < lDataToBeSent.Length; i++)
                        {
                            lDataToBeSent[i] = (ulong)(rndData.Next(int.MinValue, int.MaxValue) * rndData.Next(int.MinValue, int.MaxValue));
                            sSentBools += lDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (lDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusSocket.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, lDataToBeSent) == true)
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        long[] lDataToBeSent = new long[2];

                        string sSentBools = "";

                        for (int i = 0; i < lDataToBeSent.Length; i++)
                        {
                            lDataToBeSent[i] = rndData.Next(int.MinValue, int.MaxValue) * rndData.Next(int.MinValue, int.MaxValue);
                            sSentBools += lDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (lDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusSocket.WriteCoilWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, lDataToBeSent) == true)
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusSocket.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        #endregion

        #region "ReadInputRegister"

        private void btnSocketReadInputRegisterShort_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort stValue = 0;
                        if (ModbusSocket.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref stValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", stValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        short stValue = 0;
                        if (ModbusSocket.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref stValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", stValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort[] stValue = null;
                        if (ModbusSocket.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref stValue))
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < stValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += stValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        short[] stValue = null;
                        if (ModbusSocket.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref stValue))
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < stValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += stValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusSocket.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btnSocketReadInputRegisterInt_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        uint iValue = 0;
                        if (ModbusSocket.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref iValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", iValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        int iValue = 0;
                        if (ModbusSocket.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref iValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", iValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        uint[] iValue = null;
                        if (ModbusSocket.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref iValue))
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < iValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += iValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        int[] iValue = null;
                        if (ModbusSocket.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref iValue))
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < iValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += iValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusSocket.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btnSocketReadInputRegisterLong_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong lValue = 0;
                        if (ModbusSocket.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref lValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", lValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        long lValue = 0;
                        if (ModbusSocket.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref lValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", lValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong[] lValue = null;
                        if (ModbusSocket.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref lValue))
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < lValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += lValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        long[] lValue = null;
                        if (ModbusSocket.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref lValue))
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < lValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += lValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusSocket.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btnSocketReadInputRegisterFloat_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    float fValue = 0;
                    if (ModbusSocket.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref fValue))
                    {
                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", fValue.ToString()));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }
                else
                {
                    float[] fValue = null;
                    if (ModbusSocket.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref fValue))
                    {
                        //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                        string sBitsResults = "";
                        for (int i = 0; i < fValue.Length; i++)
                        //for (int i = iValue.Length - 1; i >= 0; i--)
                        {
                            sBitsResults += fValue[i].ToString() + " ";
                        }

                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusSocket.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btnSocketReadInputRegisterDouble_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    double dValue = 0;
                    if (ModbusSocket.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref dValue))
                    {
                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", dValue.ToString()));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }
                else
                {
                    double[] dValue = null;
                    if (ModbusSocket.ReadInputRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref dValue))
                    {
                        //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                        string sBitsResults = "";
                        for (int i = 0; i < dValue.Length; i++)
                        //for (int i = iValue.Length - 1; i >= 0; i--)
                        {
                            sBitsResults += dValue[i].ToString() + " ";
                        }

                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusSocket.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        #endregion

        #region "ReadInputBit"

        private void btnSocketReadInputBit_Click(object sender, EventArgs e)
        {
            try
            {
                //bool bReadSingleBit = false;

                if (bReadWriteArray == false)//bReadSingleBit
                {
                    bool bReadBitValue = false;
                    if (ModbusSocket.ReadInputBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref bReadBitValue))
                    {
                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }
                else
                {
                    bool[] bReadBitsValue = null;
                    if (ModbusSocket.ReadInputBit((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref bReadBitsValue))
                    {
                        //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                        string sBitsResults = "";
                        for (int i = 0; i < bReadBitsValue.Length; i++)
                        //for (int i = bReadBitsValue.Length - 1; i >= 0; i--)
                        {
                            sBitsResults += bReadBitsValue[i].ToString() + " ";
                        }

                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusSocket.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btnSocketReadInputByte_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        byte byReadValue = 0;
                        if (ModbusSocket.ReadInputByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref byReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", byReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        sbyte byReadValue = 0;
                        if (ModbusSocket.ReadInputByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref byReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", byReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        byte[] byReadValue = null;
                        if (ModbusSocket.ReadInputByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref byReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < byReadValue.Length; i++)
                            {
                                sBitsResults += byReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        sbyte[] byReadValue = null;
                        if (ModbusSocket.ReadInputByte((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref byReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < byReadValue.Length; i++)
                            {
                                sBitsResults += byReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusSocket.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btnSocketReadInputShort_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort stReadValue = 0;
                        if (ModbusSocket.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref stReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", stReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        short stReadValue = 0;
                        if (ModbusSocket.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref stReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", stReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort[] stReadValue = null;
                        if (ModbusSocket.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref stReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < stReadValue.Length; i++)
                            {
                                sBitsResults += stReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        short[] stReadValue = null;
                        if (ModbusSocket.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref stReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < stReadValue.Length; i++)
                            {
                                sBitsResults += stReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusSocket.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btnSocketReadInputInt_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        uint iReadValue = 0;
                        if (ModbusSocket.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref iReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", iReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        int iReadValue = 0;
                        if (ModbusSocket.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref iReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", iReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        uint[] iReadValue = null;
                        if (ModbusSocket.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref iReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < iReadValue.Length; i++)
                            {
                                sBitsResults += iReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        int[] iReadValue = null;
                        if (ModbusSocket.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref iReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < iReadValue.Length; i++)
                            {
                                sBitsResults += iReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusSocket.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btnSocketReadInputLong_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong lReadValue = 0;
                        if (ModbusSocket.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref lReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", lReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        long lReadValue = 0;
                        if (ModbusSocket.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref lReadValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", lReadValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }

                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong[] lReadValue = null;
                        if (ModbusSocket.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref lReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < lReadValue.Length; i++)
                            {
                                sBitsResults += lReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        long[] lReadValue = null;
                        if (ModbusSocket.ReadInputWord((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref lReadValue))
                        {
                            string sBitsResults = "";
                            for (int i = 0; i < lReadValue.Length; i++)
                            {
                                sBitsResults += lReadValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusSocket.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        #endregion

        #region "KeepRegister"

        private void btnSocketReadShortKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort stValue = 0;
                        if (ModbusSocket.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref stValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", stValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        short stValue = 0;
                        if (ModbusSocket.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref stValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", stValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort[] stValue = null;
                        if (ModbusSocket.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 4, ref stValue))
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < stValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += stValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        short[] stValue = null;
                        if (ModbusSocket.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 4, ref stValue))
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < stValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += stValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusSocket.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btnSocketReadIntKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        uint iValue = 0;
                        if (ModbusSocket.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref iValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", iValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        int iValue = 0;
                        if (ModbusSocket.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref iValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", iValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        uint[] iValue = null;
                        if (ModbusSocket.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref iValue))
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < iValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += iValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        int[] iValue = null;
                        if (ModbusSocket.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref iValue))
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < iValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += iValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusSocket.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btnSocketReadFloatKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    float fValue = 0;
                    if (ModbusSocket.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref fValue))
                    {
                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", fValue.ToString()));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }
                else
                {
                    float[] fValue = null;
                    if (ModbusSocket.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref fValue))
                    {
                        //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                        string sBitsResults = "";
                        for (int i = 0; i < fValue.Length; i++)
                        //for (int i = iValue.Length - 1; i >= 0; i--)
                        {
                            sBitsResults += fValue[i].ToString() + " ";
                        }

                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusSocket.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btnSocketReadDoubleKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    double dValue = 0;
                    if (ModbusSocket.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref dValue))
                    {
                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", dValue.ToString()));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }
                else
                {
                    double[] dValue = null;
                    if (ModbusSocket.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref dValue))
                    {
                        //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                        string sBitsResults = "";
                        for (int i = 0; i < dValue.Length; i++)
                        //for (int i = iValue.Length - 1; i >= 0; i--)
                        {
                            sBitsResults += dValue[i].ToString() + " ";
                        }

                        rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行读命令\r\n");
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusSocket.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btnSocketReadLongKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                if (bReadWriteArray == false)
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong lValue = 0;
                        if (ModbusSocket.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref lValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", lValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        long lValue = 0;
                        if (ModbusSocket.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, ref lValue))
                        {
                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", lValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong[] lValue = null;
                        if (ModbusSocket.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref lValue))
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < lValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += lValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                    else
                    {
                        long[] lValue = null;
                        if (ModbusSocket.ReadKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, 2, ref lValue))
                        {
                            //rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", bReadBitValue.ToString()));
                            string sBitsResults = "";
                            for (int i = 0; i < lValue.Length; i++)
                            //for (int i = iValue.Length - 1; i >= 0; i--)
                            {
                                sBitsResults += lValue[i].ToString() + " ";
                            }

                            rtbLog.AppendText(string.Format("成功执行读命令，结果值：{0}\r\n", sBitsResults));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行读命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusSocket.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText(sTempMsg + "\r\n");//"读取操作记录：" + 
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btnSocketWriteShortKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                Random rndShortData = new Random();

                if (bReadWriteArray == false)//bWriteSingleCoil
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort stValue = Convert.ToUInt16(rndShortData.Next(ushort.MinValue, ushort.MaxValue));//-32678, 32676));

                        if (ModbusSocket.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, stValue) == true)//1111      
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", stValue));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        short stValue = Convert.ToInt16(rndShortData.Next(short.MinValue, short.MaxValue));//-32678, 32676));

                        if (ModbusSocket.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, stValue) == true)//1111      
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", stValue));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ushort[] stDataToBeSent = new ushort[5];

                        string sSentBools = "";

                        for (int i = 0; i < stDataToBeSent.Length; i++)
                        {

                            stDataToBeSent[i] = Convert.ToUInt16(rndShortData.Next(ushort.MinValue, ushort.MaxValue));

                            sSentBools += stDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (stDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusSocket.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, stDataToBeSent) == true)//1111      
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        short[] stDataToBeSent = new short[5];

                        string sSentBools = "";

                        for (int i = 0; i < stDataToBeSent.Length; i++)
                        {

                            stDataToBeSent[i] = Convert.ToInt16(rndShortData.Next(short.MinValue, short.MaxValue));

                            sSentBools += stDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (stDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusSocket.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, stDataToBeSent) == true)//1111      
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusSocket.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btnSocketWriteIntKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                Random rndShortData = new Random();

                if (bReadWriteArray == false)//bWriteSingleCoil
                {
                    if (bReadUnsignedData == true)
                    {
                        uint iValue = (uint)rndShortData.Next(int.MinValue, int.MaxValue);

                        if (ModbusSocket.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, iValue) == true)//1111      
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", iValue));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        int iValue = rndShortData.Next(int.MinValue, int.MaxValue);

                        if (ModbusSocket.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, iValue) == true)//1111      
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", iValue));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        uint[] iDataToBeSent = new uint[4];

                        string sSentBools = "";

                        for (int i = 0; i < iDataToBeSent.Length; i++)
                        {

                            iDataToBeSent[i] = (uint)rndShortData.Next(int.MinValue, int.MaxValue);

                            sSentBools += iDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (iDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusSocket.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, iDataToBeSent) == true)//1111      
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        int[] iDataToBeSent = new int[4];

                        string sSentBools = "";

                        for (int i = 0; i < iDataToBeSent.Length; i++)
                        {

                            iDataToBeSent[i] = rndShortData.Next(int.MinValue, int.MaxValue);

                            sSentBools += iDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (iDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusSocket.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, iDataToBeSent) == true)//1111      
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusSocket.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btnSocketWriteFloatKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                Random rndShortData = new Random();

                if (bReadWriteArray == false)//bWriteSingleCoil
                {
                    float fValue = Convert.ToSingle(Convert.ToInt16(rndShortData.Next(-32678, 32676)) / 3.5);

                    if (ModbusSocket.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, fValue) == true)//1111      
                    {
                        rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", fValue.ToString()));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行写命令\r\n");
                    }
                }
                else
                {
                    float[] fDataToBeSent = new float[4];

                    string sSentBools = "";

                    for (int i = 0; i < fDataToBeSent.Length; i++)
                    {

                        fDataToBeSent[i] = Convert.ToSingle(Convert.ToInt16(rndShortData.Next(-32678, 32676)) / 3.5);

                        sSentBools += fDataToBeSent[i].ToString() + " ";
                    }

                    rtbLog.AppendText("发送值[0~" + (fDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                    if (ModbusSocket.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, fDataToBeSent) == true)//1111      
                    {
                        rtbLog.AppendText("成功执行写命令\r\n");
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行写命令\r\n");
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusSocket.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btnSocketWriteDoubleKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                Random rndShortData = new Random();

                if (bReadWriteArray == false)//bWriteSingleCoil
                {
                    double dValue = Convert.ToDouble(Convert.ToInt16(rndShortData.Next(-32678, 32676)) / 3.5);

                    if (ModbusSocket.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, dValue) == true)//1111      
                    {
                        rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", dValue.ToString()));
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行写命令\r\n");
                    }
                }
                else
                {
                    double[] dDataToBeSent = new double[4];

                    string sSentBools = "";

                    for (int i = 0; i < dDataToBeSent.Length; i++)
                    {

                        dDataToBeSent[i] = Convert.ToSingle(Convert.ToInt16(rndShortData.Next(-32678, 32676)) / 3.5);

                        sSentBools += dDataToBeSent[i].ToString() + " ";
                    }

                    rtbLog.AppendText("发送值[0~" + (dDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                    if (ModbusSocket.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, dDataToBeSent) == true)//1111      
                    {
                        rtbLog.AppendText("成功执行写命令\r\n");
                    }
                    else
                    {
                        rtbLog.AppendText("未成功执行写命令\r\n");
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusSocket.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btnSocketWriteLongKeepRegister_Click(object sender, EventArgs e)
        {
            try
            {
                Random rndShortData = new Random();

                if (bReadWriteArray == false)//bWriteSingleCoil
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong dValue = (ulong)(rndShortData.Next(int.MinValue, int.MaxValue) * rndShortData.Next(int.MinValue, int.MaxValue));

                        if (ModbusSocket.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, dValue) == true)//1111      
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", dValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        long dValue = rndShortData.Next(int.MinValue, int.MaxValue) * rndShortData.Next(int.MinValue, int.MaxValue);

                        if (ModbusSocket.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, dValue) == true)//1111      
                        {
                            rtbLog.AppendText(string.Format("成功执行写命令{0}\r\n", dValue.ToString()));
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }
                else
                {
                    if (bReadUnsignedData == true)
                    {
                        ulong[] dDataToBeSent = new ulong[4];

                        string sSentBools = "";

                        for (int i = 0; i < dDataToBeSent.Length; i++)
                        {

                            dDataToBeSent[i] = (ulong)(rndShortData.Next(int.MinValue, int.MaxValue) * rndShortData.Next(int.MinValue, int.MaxValue));

                            sSentBools += dDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (dDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusSocket.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, dDataToBeSent) == true)//1111      
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                    else
                    {
                        long[] dDataToBeSent = new long[4];

                        string sSentBools = "";

                        for (int i = 0; i < dDataToBeSent.Length; i++)
                        {

                            dDataToBeSent[i] = rndShortData.Next(int.MinValue, int.MaxValue) * rndShortData.Next(int.MinValue, int.MaxValue);

                            sSentBools += dDataToBeSent[i].ToString() + " ";
                        }

                        rtbLog.AppendText("发送值[0~" + (dDataToBeSent.Length - 1).ToString() + "]：" + sSentBools + "\r\n");

                        if (ModbusSocket.WriteKeepRegister((byte)nudSlaveAddress.Value, (ushort)nudBeginAddress.Value, dDataToBeSent) == true)//1111      
                        {
                            rtbLog.AppendText("成功执行写命令\r\n");
                        }
                        else
                        {
                            rtbLog.AppendText("未成功执行写命令\r\n");
                        }
                    }
                }

                string sTempMsg = "";
                do
                {
                    sTempMsg = ModbusSocket.GetInfo();
                    if (string.IsNullOrEmpty(sTempMsg) == false)
                    {
                        rtbLog.AppendText("写操作记录：" + sTempMsg + "\r\n");
                    }
                } while (string.IsNullOrEmpty(sTempMsg) == false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        #endregion

        #endregion

        private void chkTestSocket_CheckedChanged(object sender, EventArgs e)
        {
            bUseSocketToTest = chkTestSocket.Checked;
        }

        bool bUseInterfaceToChangeLanguage = true;

        private void btnChinese_Click(object sender, EventArgs e)
        {
            if (bUseInterfaceToChangeLanguage == true)
            {
                CModbusErrorCode.info = new Chinese();
                CModbusFuncCode.info = new Chinese();
            }
            else
            {
                CModbusErrorCode.Chinese();
                CModbusFuncCode.Chinese();
            }
        }

        private void btnEnglish_Click(object sender, EventArgs e)
        {
            if (bUseInterfaceToChangeLanguage == true)
            {
                CModbusErrorCode.info = new English();
                CModbusFuncCode.info = new English();
            }
            else
            {
                CModbusErrorCode.English();
                CModbusFuncCode.English();
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

    }//class

}//namespace