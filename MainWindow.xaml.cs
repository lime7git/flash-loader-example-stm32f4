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
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using stm32_custom_flas_loader;
using System.Threading.Tasks;
using System.Windows.Markup;
using System.Linq;

namespace IMAGOPrinterProgrammerTool
{
    public partial class MainWindow : Window
    {
        private SerialPort serialPort;
        private TcpClient tcpClient;
        private NetworkStream networkStream;
        private StringBuilder receivedDataASCII = new StringBuilder();
        private StringBuilder receivedDataHex = new StringBuilder();
        private byte[] fileBytes;
        private BackgroundWorker uploadWorker;
        private bool ackFlag = false;
        ManualResetEvent oSignalEvent;

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

            // Initialize BackgroundWorker for uploading
            uploadWorker = new BackgroundWorker
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };
            uploadWorker.DoWork += UploadWorker_DoWork;
            uploadWorker.ProgressChanged += UploadWorker_ProgressChanged;
            uploadWorker.RunWorkerCompleted += UploadWorker_RunWorkerCompleted;

            oSignalEvent = new ManualResetEvent(false);
        }

        private void ButtonUpload_Click(object sender, RoutedEventArgs e)
        {
            if (serialPort != null && serialPort.IsOpen && fileBytes != null)
            {
                progressBarUpload.Value = 0;
                textBlockStatus.Text = "Uploading...";
                uploadWorker.RunWorkerAsync();
            }
            else
            {
                MessageBox.Show("Please connect to a COM port and load a binary file first.");
            }
        }

        private void SendAsciiString(string command)
        {
            // Convert the ASCII string to a byte array
            byte[] commandBytes = System.Text.Encoding.ASCII.GetBytes(command);

            // Send the ASCII string to the COM port
            if (serialPort != null && serialPort.IsOpen)
            {
                serialPort.Write(commandBytes, 0, commandBytes.Length);
            }
            else if (tcpClient != null && tcpClient.Connected) // Send the ASCII string to the TCP server
            {
                if (networkStream != null && networkStream.CanWrite)
                {
                    networkStream.Write(commandBytes, 0, commandBytes.Length);
                }
            }
        }

        private void UploadWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            const byte COMMAND_EXTENDED_ERASE = 0x44;
            const byte COMMAND_GO = 0x21;
            const uint START_ADDRESS = 0x08000000; // flash starting addres stm32f4
            const int CHUNK_SIZE = 256; // max size for write command 


            // Send command to activate bootloader
            SendAsciiString("DO_PROGRAMMING");

            // Delay for configuration
            System.Threading.Thread.Sleep(200);

            // Send 0x7F as the next step in the protocol
            SendHex(new byte[] { 0x7F });

            // Wait for 0x79 ACK
            WaitForACK();

            //ERASE MEMORY
            SendCommand(COMMAND_EXTENDED_ERASE);

            // Step 2: Wait for ACK
            WaitForACK();

            // Step 3: Send the target address and its checksum
            SendHex(new byte[] { 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00 });

            // Step 4: Wait for ACK
            WaitForACK();


            // Send command to start upload
            // Assuming the protocol is handled in helper methods like SendCommand and ReceiveData
            for (int i = 0; i < fileBytes.Length; i += CHUNK_SIZE)
            {
                if (uploadWorker.CancellationPending)
                {
                    e.Cancel = true;
                    return;
                }

                int chunkLength = Math.Min(CHUNK_SIZE, fileBytes.Length - i);
                byte[] chunk = new byte[chunkLength];
                Array.Copy(fileBytes, i, chunk, 0, chunkLength);

                // Send the chunk to the target device
                bool success = UploadChunk(START_ADDRESS + (uint)i, chunk);
                if (!success)
                {
                    e.Result = false;
                    return;
                }

                // Report progress
                uploadWorker.ReportProgress((i + chunkLength) * 100 / fileBytes.Length);
            }

            // Verify upload
            bool verificationResult = VerifyUpload(fileBytes, START_ADDRESS);
            e.Result = verificationResult;

            SendCommand(COMMAND_GO);
            WaitForACK();

            //flash start address
            SendHex(new byte[] { 0x08, 0x00, 0x00, 0x00, 0x08 });

            // Step 4: Wait for ACK
            WaitForACK();
        }

        private void UploadWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (serialPort != null && serialPort.IsOpen)
            {
                progressBarUpload.Value = e.ProgressPercentage;
            }
            else if (tcpClient != null && tcpClient.Connected)
            {
                progressBarUploadEth.Value = e.ProgressPercentage;
            }
        }

        private void UploadWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled)
            {           
                if (serialPort != null && serialPort.IsOpen)
                {
                    textBlockStatus.Text = "Upload cancelled.";
                }
                else if (tcpClient != null && tcpClient.Connected)
                {
                    textBlockStatusEth.Text = "Upload cancelled.";
                }
            }
            else if (e.Error != null)
            {          
                if (serialPort != null && serialPort.IsOpen)
                {
                    textBlockStatus.Text = "Error during upload: " + e.Error.Message;
                }
                else if (tcpClient != null && tcpClient.Connected)
                {
                    textBlockStatusEth.Text = "Error during upload: " + e.Error.Message;
                }
            }
            else
            {
                bool success = (bool)e.Result;
               
                if (serialPort != null && serialPort.IsOpen)
                {
                    textBlockStatus.Text = success ? "Upload and verification successful!" : "Verification failed!";
                }
                else if (tcpClient != null && tcpClient.Connected)
                {
                    textBlockStatusEth.Text = success ? "Upload and verification successful!" : "Verification failed!";
                }
            }
        }

        private bool UploadChunk(uint address, byte[] chunk)
        {
            const byte COMMAND_WRITE_MEMORY = 0x31;

            try
            {
                // Step 1: Send the Write Memory command (0x31)
                SendCommand(COMMAND_WRITE_MEMORY);

                // Step 2: Wait for ACK
                WaitForACK();

                // Step 3: Send the target address and its checksum
                SendAddressWithChecksum(address);

                // Step 4: Wait for ACK
                WaitForACK();

                // Step 5: Send the length (N - 1) and the data, followed by the checksum
                SendDataWithChecksum(chunk);

                // Step 6: Wait for final ACK
                WaitForACK();
                return true;
            }
            catch (Exception ex)
            {
                // Handle any exceptions, such as communication errors
                MessageBox.Show($"Error during upload: {ex.Message}");
                return false;
            }
        }

        private void SendCommand(byte command)
        {
            byte[] commandBuffer = new byte[2];
            commandBuffer[0] = command;
            commandBuffer[1] = (byte)(command ^ 0xFF);  // XOR with 0xFF for checksum

            if (serialPort != null && serialPort.IsOpen)
            {
                serialPort.Write(commandBuffer, 0, commandBuffer.Length);
            }
            else if (tcpClient != null && tcpClient.Connected)
            {
                networkStream.Write(commandBuffer, 0, commandBuffer.Length);
            }
        }

        private void SendHex(byte[] hex)
        {
            if (serialPort != null && serialPort.IsOpen)
            {
                serialPort.Write(hex, 0, hex.Length);
            }
            else if (tcpClient != null && tcpClient.Connected)
            {
                networkStream.Write(hex, 0, hex.Length);
            }
        }

        private void SendAddressWithChecksum(uint address)
        {
            byte[] addressBuffer = new byte[5];
            addressBuffer[0] = (byte)((address >> 24) & 0xFF); // MSB
            addressBuffer[1] = (byte)((address >> 16) & 0xFF);
            addressBuffer[2] = (byte)((address >> 8) & 0xFF);
            addressBuffer[3] = (byte)(address & 0xFF);         // LSB

            // Checksum calculation: XOR all bytes of the address
            addressBuffer[4] = (byte)(addressBuffer[0] ^ addressBuffer[1] ^ addressBuffer[2] ^ addressBuffer[3]);

            if (serialPort != null && serialPort.IsOpen)
            {
                serialPort.Write(addressBuffer, 0, addressBuffer.Length);
            }
            else if (tcpClient != null && tcpClient.Connected)
            {
                networkStream.Write(addressBuffer, 0, addressBuffer.Length);
            }
        }

        private void SendDataWithChecksum(byte[] data)
        {
            int length = data.Length;
            byte[] dataBuffer = new byte[length + 2];

            // First byte is the length (N - 1)
            dataBuffer[0] = (byte)(length - 1);

            // Copy the data
            Array.Copy(data, 0, dataBuffer, 1, length);

            // Calculate checksum: XOR all bytes including length
            byte checksum = dataBuffer[0];
            for (int i = 1; i <= length; i++)
            {
                checksum ^= dataBuffer[i];
            }

            // Last byte is the checksum
            dataBuffer[length + 1] = checksum;

            // Send the data buffer
            if (serialPort != null && serialPort.IsOpen)
            {
                serialPort.Write(dataBuffer, 0, dataBuffer.Length);
            }
            else if (tcpClient != null && tcpClient.Connected)
            {
                networkStream.Write(dataBuffer, 0, dataBuffer.Length);
            }
        }

        private void WaitForACK(int timeout = 1000, int retryCount = 3)
        {
            oSignalEvent.WaitOne();
            oSignalEvent.Reset(); 
        }

        private bool GetAckFlag()
        {
            while (ackFlag != true) { };
            return ackFlag;
        }

        private bool VerifyUpload(byte[] originalData, uint startAddress)
        {
            // Placeholder: Implement the protocol to read back data from the device and verify.
            // Compare the received data with originalData.
            // Return true if the verification is successful, false otherwise.
            return true;
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
            if(tcpClient != null && tcpClient.Connected)
            {
                MessageBox.Show("Ethernet already connected!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                if (comboBoxCOMPorts.SelectedItem == null)
                {
                    MessageBox.Show("Please select a COM port", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                serialPort = new SerialPort(comboBoxCOMPorts.SelectedItem.ToString())
                {
                    BaudRate = 115200,
                    Parity = Parity.Even,
                    DataBits = 8,
                    StopBits = StopBits.One,
                   // Encoding = Encoding.ASCII
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

                if (data == "y")
                {
                    ackFlag = true;  // ACK received
                }
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
            else if(tcpClient != null && tcpClient.Connected)
            {

                if (networkStream != null)
                {
                    networkStream.Close();
                }

                if (tcpClient != null)
                {
                    tcpClient.Close();
                }

                MessageBox.Show("Disconnected", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                buttonDisconnect.Visibility = Visibility.Collapsed;
                buttonConnectEth.Visibility = Visibility.Visible;
            }
        }

        private void ButtonLoadFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                fileBytes = File.ReadAllBytes(openFileDialog.FileName);
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
            else if(tcpClient != null && tcpClient.Connected)
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

                    networkStream.Write(bytesToSend, 0, bytesToSend.Length);
                }
            }
            else
            {
                MessageBox.Show("Please connect to a COM port or TCP Client first.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
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
        private async Task ConnectToServer(string ipAddress, int port)
        {
            try
            {
                tcpClient = new TcpClient();
                await tcpClient.ConnectAsync(ipAddress, port);
                networkStream = tcpClient.GetStream();

               // MessageBox.Show("Connected to the server!");

                buttonDisconnect.Visibility = Visibility.Visible;
                buttonConnectEth.Visibility = Visibility.Collapsed;

                // Start listening for incoming data
                StartReceiving();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to connect: {ex.Message}");
            }
        }

        private async void StartReceiving()
        {
            byte[] buffer = new byte[1024];
            try
            {
                while (tcpClient != null && tcpClient.Connected)
                {
                    int bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        string receivedData = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                        // Update the UI with received data
                        Dispatcher.Invoke(() =>
                        {
                            textBoxReceivedDataASCII.AppendText(receivedData);
                            textBoxReceivedDataASCII.ScrollToEnd();

                            foreach (byte b in buffer.Take(bytesRead))
                            {
                                receivedDataHex.AppendFormat("{0:X2} ", b);
                            }

                            textBoxReceivedDataHex.Text = receivedDataHex.ToString();
                            textBoxReceivedDataHex.ScrollToEnd();                      
                        });

                        if (receivedData == "y")
                        {
                            ackFlag = true;  // ACK received
                            oSignalEvent.Set();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error while receiving data: {ex.Message}");
            }
        }

        private async void ButtonConnect_ClickEth(object sender, RoutedEventArgs e)
        {
            string ipAddr = textBoxIpAddr.Text;
            int tcpIpPort = int.Parse(textBoxTcpIpPort.Text);

            await ConnectToServer(ipAddr, tcpIpPort);
        }

        private void ButtonUpload_ClickEth(object sender, RoutedEventArgs e)
        {
            if (tcpClient != null && tcpClient.Connected && fileBytes != null)
            {
                progressBarUploadEth.Value = 0;
                textBlockStatusEth.Text = "Uploading...";
                uploadWorker.RunWorkerAsync();
            }
            else
            {
                MessageBox.Show("Please connect to a TCP Client and load a binary file first.");
            }
        }
    }
}
