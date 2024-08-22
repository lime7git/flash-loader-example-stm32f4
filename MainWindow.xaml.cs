using System;
using System.IO.Ports;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace IMAGOPrinterProgrammerTool
{
    public partial class MainWindow : Window
    {
        private SerialPort serialPort;
        private StringBuilder receivedDataASCII = new StringBuilder();
        private StringBuilder receivedDataHex = new StringBuilder();

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
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = "Binary Files (*.bin)|*.bin|All Files (*.*)|*.*";
            bool? result = dlg.ShowDialog();

            if (result == true)
            {
                string filePath = dlg.FileName;
                DisplayFileAsHex(filePath);
            }
        }

        private void DisplayFileAsHex(string filePath)
        {
            byte[] fileBytes = File.ReadAllBytes(filePath);
            hexViewer.Document.Blocks.Clear();
            hexViewer.AppendText(BitConverter.ToString(fileBytes).Replace("-", " "));
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
