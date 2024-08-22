using System;
using System.IO.Ports;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Microsoft.Win32;
using System.Collections.Generic;

namespace IMAGOPrinterProgrammerTool
{
    public partial class MainWindow : Window
    {
        private SerialPort serialPort;
        private StringBuilder receivedDataASCII = new StringBuilder();
        private StringBuilder receivedDataHex = new StringBuilder();

        public class HexViewerRow
        {
            public string Address { get; set; }
            public string Hex00 { get; set; }
            public string Hex01 { get; set; }
            public string Hex02 { get; set; }
            public string Hex03 { get; set; }
            public string Hex04 { get; set; }
            public string Hex05 { get; set; }
            public string Hex06 { get; set; }
            public string Hex07 { get; set; }
            public string Hex08 { get; set; }
            public string Hex09 { get; set; }
            public string Hex0A { get; set; }
            public string Hex0B { get; set; }
            public string Hex0C { get; set; }
            public string Hex0D { get; set; }
            public string Hex0E { get; set; }
            public string Hex0F { get; set; }
            public string AsciiDump { get; set; }
        }

        public MainWindow()
        {
            InitializeComponent();
            InitializeDefaults();
            buttonScan.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        }

        private void InitializeDefaults()
        {
            comboBoxBaudRate.SelectedIndex = 1;
            comboBoxParity.SelectedIndex = 1;
            comboBoxDataBits.SelectedIndex = 0;
        }

        private void ButtonScan_Click(object sender, RoutedEventArgs e)
        {
            comboBoxCOMPorts.Items.Clear();
            string[] ports = SerialPort.GetPortNames();
            foreach (var port in ports)
            {
                comboBoxCOMPorts.Items.Add(port);
            }

            if (comboBoxCOMPorts.Items.Count > 0)
                comboBoxCOMPorts.SelectedIndex = 0;
            else
                MessageBox.Show("No COM ports found!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        private void ButtonConnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (comboBoxCOMPorts.SelectedItem == null)
                {
                    MessageBox.Show("Please select a COM port", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                serialPort = new SerialPort(comboBoxCOMPorts.SelectedItem.ToString())
                {
                    BaudRate = int.Parse((comboBoxBaudRate.SelectedItem as ComboBoxItem).Content.ToString()),
                    Parity = (Parity)Enum.Parse(typeof(Parity), (comboBoxParity.SelectedItem as ComboBoxItem).Content.ToString()),
                    DataBits = int.Parse((comboBoxDataBits.SelectedItem as ComboBoxItem).Content.ToString()),
                    StopBits = StopBits.One,
                    Encoding = Encoding.ASCII
                };

                serialPort.DataReceived += SerialPort_DataReceived;
                serialPort.Open();

                MessageBox.Show("Connected to " + serialPort.PortName, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                buttonDisconnect.Visibility = Visibility.Visible;
                buttonConnect.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error connecting to COM port: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            string data = serialPort.ReadExisting();
            Dispatcher.Invoke(() =>
            {
                // Update ASCII view
                receivedDataASCII.Append(data);
                textBoxReceivedDataASCII.Text = receivedDataASCII.ToString();
                textBoxReceivedDataASCII.ScrollToEnd();

                // Update Hex view
                byte[] bytes = Encoding.ASCII.GetBytes(data);
                foreach (byte b in bytes)
                {
                    receivedDataHex.AppendFormat("{0:X2} ", b);
                }
                textBoxReceivedDataHex.Text = receivedDataHex.ToString();
                textBoxReceivedDataHex.ScrollToEnd();
            });
        }

        private void ButtonDisconnect_Click(object sender, RoutedEventArgs e)
        {
            if (serialPort != null && serialPort.IsOpen)
            {
                serialPort.Close();
                MessageBox.Show("Disconnected", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                buttonDisconnect.Visibility = Visibility.Collapsed;
                buttonConnect.Visibility = Visibility.Visible;
            }
        }

        private void ButtonLoadFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                byte[] fileBytes = File.ReadAllBytes(openFileDialog.FileName);
                DisplayHexData(fileBytes);
            }
        }

        private void DisplayHexData(byte[] data)
        {
            var hexViewerRows = new List<HexViewerRow>();

            for (int i = 0; i < data.Length; i += 16)
            {
                var row = new HexViewerRow();
                row.Address = i.ToString("X8");

                var hexValues = new StringBuilder();
                var asciiValues = new StringBuilder();

                for (int j = 0; j < 16; j++)
                {
                    if (i + j < data.Length)
                    {
                        byte b = data[i + j];
                        string hex = b.ToString("X2");
                        string ascii = b >= 32 && b <= 126 ? ((char)b).ToString() : ".";

                        hexValues.Append(hex + " ");
                        asciiValues.Append(ascii);

                        row.GetType().GetProperty($"Hex{j:X2}").SetValue(row, hex);
                    }
                    else
                    {
                        hexValues.Append("   "); // Placeholder for missing data in the last row
                    }
                }

                row.AsciiDump = asciiValues.ToString();
                hexViewerRows.Add(row);
            }

            hexViewerDataGrid.ItemsSource = hexViewerRows;
        }

        private void ButtonSendData_Click(object sender, RoutedEventArgs e)
        {
            if (serialPort != null && serialPort.IsOpen)
            {
                string dataToSend = textBoxSendData.Text;
                if (!string.IsNullOrEmpty(dataToSend))
                {
                    byte[] bytesToSend;
                    if (IsHex(dataToSend))
                    {
                        bytesToSend = StringToByteArray(dataToSend);
                    }
                    else
                    {
                        bytesToSend = Encoding.ASCII.GetBytes(dataToSend);
                    }

                    serialPort.Write(bytesToSend, 0, bytesToSend.Length);
                }
            }
            else
            {
                MessageBox.Show("Please connect to a COM port first.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ButtonClearData_Click(object sender, RoutedEventArgs e)
        {
            receivedDataASCII.Clear();
            receivedDataHex.Clear();
            textBoxReceivedDataASCII.Clear();
            textBoxReceivedDataHex.Clear();
        }

        private bool IsHex(string input)
        {
            foreach (char c in input)
            {
                if (!Uri.IsHexDigit(c) && !char.IsWhiteSpace(c))
                {
                    return false;
                }
            }
            return true;
        }

        private byte[] StringToByteArray(string hex)
        {
            hex = hex.Replace(" ", "");
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < hex.Length; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }
            return bytes;
        }
    }
}
