// ====================================================================================================================
// 
// Application: WIN 10 IOT Communication part port serie
//
// Auteur:      J. J. Moreno
// Date:        Mars 2018
//
// Test pour connexion par ligne série RS232.
// Bouton 'Connecter'       ouverture du port sélectionné
// Bouton 'Déconnecter':    fermeture du port en cours d'utilisation
// Bouton 'Envoi':          transmet les caractères de la TextBox d'émission, ajoute CR/LF à la fin de l'émission
// Slider :                 transmet la valeur du slider dès que celui-ci est relâché
// Bouton 'Sauver Chaîne Test':    enregistre la chaîne de la Textbox d'émission comme variable de test pour envois multiples
// Bouton 'Envoi Test':     transmet la chaine de test enregistrée
//
// La gestion du port série est issue de l'exemple SerialSample de https://github.com/ms-iot/samples
// avec la licence suivante:
//
//Copyright(c) Microsoft Open Technologies, Inc.All rights reserved.
//The MIT License(MIT)
//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:
 
//The above copyright notice and this permission notice shall be included in
//all copies or substantial portions of the Software.
 
//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
//THE SOFTWARE.
// ====================================================================================================================

using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace AppSerialCOM
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>

    public sealed partial class MainPage : Page
    {
        // déclaration pour le port COM
        private SerialDevice serialPort = null;
        DataWriter dataWriteObject = null;
        DataReader dataReaderObject = null;

        private ObservableCollection<DeviceInformation> listOfDevices;
        private CancellationTokenSource ReadCancellationTokenSource;
        private string testText = "";


        public MainPage()
        {   // Initialisations
            this.InitializeComponent();
            Bt_ConnectDevice.IsEnabled = false;
            Bt_SendText.IsEnabled = false;
            Bt_SendTestText.IsEnabled = false;
            TxBl_Header.Text = "Séléction port de communication";
            listOfDevices = new ObservableCollection<DeviceInformation>();
            ListAvailablePorts();
        }

        /// <summary>
        /// ListAvailablePorts
        /// - Use SerialDevice.GetDeviceSelector to enumerate all serial devices
        /// - Attaches the DeviceInformation to the ListBox source so that DeviceIds are displayed
        /// </summary>
        // Enumeration de la liste des ports COM disponibles
        private async void ListAvailablePorts()
        {
            try
            {
                string aqs = SerialDevice.GetDeviceSelector();
                var dis = await DeviceInformation.FindAllAsync(aqs);

                TxBl_status.Text = "Choisir un port et connecter";

                for (int i = 0; i < dis.Count; i++)
                {
                    listOfDevices.Add(dis[i]);
                }

                DeviceListSource.Source = listOfDevices;
                Bt_ConnectDevice.IsEnabled = true;
                ConnectDevices.SelectedIndex = -1;
            }
            catch (Exception ex)
            {
                TxBl_status.Text = ex.Message;
            }
        }

        /// <summary>
        /// - Create a DataReader object
        /// - Create an async task to read from the SerialDevice InputStream
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Listen()
        {
            try
            {
                if (serialPort != null)
                {
                    dataReaderObject = new DataReader(serialPort.InputStream);

                    // keep reading the serial input
                    while (true)
                    {
                        await ReadAsync(ReadCancellationTokenSource.Token);
                    }
                }
            }
            catch (TaskCanceledException tce)
            {
                TxBl_status.Text = "Reading task was cancelled, closing device and cleaning up";
                CloseDevice();
            }
            catch (Exception ex)
            {
                TxBl_status.Text = ex.Message;
            }
            finally
            {
                // Cleanup once complete
                if (dataReaderObject != null)
                {
                    dataReaderObject.DetachStream();
                    dataReaderObject = null;
                }
            }
        }

        /// <summary>
        /// ReadAsync: Task that waits on data and reads asynchronously from the serial device InputStream
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task ReadAsync(CancellationToken cancellationToken)
        {
            Task<UInt32> loadAsyncTask;

            uint ReadBufferLength = 1024;

            // If task cancellation was requested, comply
            cancellationToken.ThrowIfCancellationRequested();

            // Set InputStreamOptions to complete the asynchronous read operation when one or more bytes is available
            dataReaderObject.InputStreamOptions = InputStreamOptions.Partial;

            using (var childCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                // Create a task object to wait for data on the serialPort.InputStream
                loadAsyncTask = dataReaderObject.LoadAsync(ReadBufferLength).AsTask(childCancellationTokenSource.Token);

                // Launch the task and wait
                UInt32 bytesRead = await loadAsyncTask;
                if (bytesRead > 0)
                {
                    TxBx_RxText.Text += dataReaderObject.ReadString(bytesRead);
                    TxBl_status.Text = bytesRead.ToString()+" bytes reçus!";
                }
            }
        }

        /// <summary>
        /// CancelReadTask:
        /// - Uses the ReadCancellationTokenSource to cancel read operations
        /// </summary>
        private void CancelReadTask()
        {
            if (ReadCancellationTokenSource != null)
            {
                if (!ReadCancellationTokenSource.IsCancellationRequested)
                {
                    ReadCancellationTokenSource.Cancel();
                }
            }
        }

        // OpenWriteStream: creation de l'objet dataWriteObjet du port serie
        private void OpenWriteStream()
        {
            try
            {
                if (serialPort != null)
                {
                    // Create the DataWriter object and attach to OutputStream
                    dataWriteObject = new DataWriter(serialPort.OutputStream);
                }
                else
                {
                    TxBl_status.Text = "Choisir un port et clicker 'Connecter'";
                }
            }
            catch (Exception ex)
            {
                TxBl_status.Text = "sendTextButton_Click: " + ex.Message;
            }
        }

        // CloseWriteStream: disposer de l'objet dataWriteObjet
        private void CloseWriteStream()
        {
            if (dataWriteObject != null)
            {
                dataWriteObject.DetachStream();
                dataWriteObject = null;
            }

        }
        /// <summary>
        /// WriteAsync: Task that asynchronously writes data from the input text box 'sendText' to the OutputStream 
        /// </summary>
        /// <returns></returns>
        private async Task WriteAsync(string lineToSend)
        {
            Task<UInt32> storeAsyncTask;

            if (lineToSend.Length != 0)
            {
                // Load the text from the sendText input text box to the dataWriter object
                dataWriteObject.WriteString(lineToSend);

                // Launch an async task to complete the write operation
                storeAsyncTask = dataWriteObject.StoreAsync().AsTask();

                UInt32 bytesWritten = await storeAsyncTask;
                if (bytesWritten > 0)
                {
                    TxBl_status.Text = TxBx_TxText.Text.Length.ToString() + " caractères envoyés avec succès!";
                }
            }
            else
            {
                TxBl_status.Text = "Tapez le text à envoyer et clickez sur 'Envoi'";
            }
        }

        /// <summary>
        /// CloseDevice:
        /// - Disposes SerialDevice object
        /// - Clears the enumerated device Id list
        /// </summary>
        private void CloseDevice()
        {
            if (serialPort != null)
            {
                serialPort.Dispose();
            }
            serialPort = null;

            Bt_ConnectDevice.IsEnabled = true;
            Bt_SendText.IsEnabled = false;
            TxBx_RxText.Text = "";
            listOfDevices.Clear();
        }


        /// <summary>
        /// comPortInput_Click: Action to take when 'Connect' button is clicked
        /// - Get the selected device index and use Id to create the SerialDevice object
        /// - Configure default settings for the serial port
        /// - Create the ReadCancellationTokenSource token
        /// - Start listening on the serial port input
        /// </summary>
        private async void Bt_ConnectDevice_Click(object sender, RoutedEventArgs e)
        {
            var selection = ConnectDevices.SelectedItems;

            if (selection.Count <= 0)
            {
                TxBl_status.Text = "Choisir un port et clicker 'Connecter'";
                return;
            }

            DeviceInformation entry = (DeviceInformation)selection[0];

            try
            {
                serialPort = await SerialDevice.FromIdAsync(entry.Id);

                if (serialPort == null) return;

                // Disable the 'Connect' button 
                Bt_ConnectDevice.IsEnabled = false;

                // Configure serial settings
                serialPort.WriteTimeout = TimeSpan.FromMilliseconds(1000);
                serialPort.ReadTimeout = TimeSpan.FromMilliseconds(1000);
                serialPort.BaudRate = 9600;
                serialPort.Parity = SerialParity.None;
                serialPort.StopBits = SerialStopBitCount.One;
                serialPort.DataBits = 8;
                serialPort.Handshake = SerialHandshake.None;

                // Display configured settings
                TxBl_status.Text = "Serial port configuré";
                TxBl_Header.Text = entry.Name + " (" + serialPort.PortName + ") : ";
                TxBl_Header.Text += serialPort.BaudRate + " Bauds - ";
                TxBl_Header.Text += serialPort.DataBits + " bits - Parité=" + serialPort.Parity.ToString() + " - ";
                TxBl_Header.Text += serialPort.StopBits + " stop bit(s)";

                // Set the RcvdText field to invoke the TextChanged callback
                // The callback launches an async Read task to wait for data
                TxBx_RxText.Text = "Attente de données...";

                // Create cancellation token object to close I/O operations when closing the device
                ReadCancellationTokenSource = new CancellationTokenSource();

                //Enable 'WRITE' buttons to allow sending data
                Bt_SendText.IsEnabled = true;
                Bt_SendTestText.IsEnabled = true;

                OpenWriteStream();
                Listen();
            }
            catch (Exception ex)
            {
                TxBl_status.Text = ex.Message;
                Bt_ConnectDevice.IsEnabled = true;
                Bt_SendText.IsEnabled = false;
            }

        }

        /// <summary>
        /// sendTextButton_Click: Action to take when 'WRITE' button is clicked
        /// - Create a DataWriter object with the OutputStream of the SerialDevice
        /// - Create an async task that performs the write operation
        /// </summary>
        private async void Bt_SendText_Click(object sender, RoutedEventArgs e)
        {
            string txt = TxBx_TxText.Text;
            try
            {
                //Launch the WriteAsync task to perform the write
                await WriteAsync(txt + Environment.NewLine);
                TxBl_status.Text = "Envoyé: " + txt;
                TxBx_TxText.Text = "";
            }
            catch (Exception ex)
            {
                TxBl_status.Text = "sendTextButton_Click: " + ex.Message;
            }
        }

        /// <summary>
        /// closeDevice_Click: Action to take when 'Disconnect and Refresh List' is clicked on
        /// - Cancel all read operations
        /// - Close and dispose the SerialDevice object
        /// - Enumerate connected devices
        /// </summary>
        private void Bt_DisconnectDevice_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TxBl_status.Text = "";
                CloseWriteStream();
                CancelReadTask();
                CloseDevice();
                ListAvailablePorts();
            }
            catch (Exception ex)
            {
                TxBl_status.Text = ex.Message;
            }

        }

        private void Sl_SendValue_PointerReleased(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            string s = "Slider value = " + Sl_SendValue.Value.ToString();
            TxBx_TxText.Text += s;
        }

        private void Sl_SendValue_ValueChanged_1(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            // evt à chaque fois que la valeur change
            //string s = "Slider value = " + e.NewValue.ToString();
            //TxBx_TxText.Text += s;
        }

        private void Sl_SendValue_LostFocus(object sender, RoutedEventArgs e)
        {
            // evt lorsque le focus est perdu = un click a lieu sur un autre outil
        }

        private async void Sl_SendValue_PointerCaptureLost(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            // evt lorsque l'outil est perd la capture du pointeur = quand le bouton est relâché
            string svStr = "Valeur curseur = " + Sl_SendValue.Value.ToString();
            try
            {
                //Launch the WriteAsync task to perform the write
                await WriteAsync(svStr + Environment.NewLine);
                TxBl_status.Text = "Envoyé: " + svStr;
            }
            catch (Exception ex)
            {
                TxBl_status.Text = "sendTextButton_Click: " + ex.Message;
            }

        }

        private void Sl_SendValue_PointerExited(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            // evt lorsque le pointeur quitte la zone de l'outil
        }

        private void Bt_SaveTestText_Click(object sender, RoutedEventArgs e)
        {
            testText = TxBx_TxText.Text;
            TxBl_status.Text = "Chaîne de test enregistrée";
            TxBx_TxText.Text = "";
        }

        private async void Bt_SendTestText_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //Launch the WriteAsync task to perform the write
                await WriteAsync(testText + Environment.NewLine);
                TxBl_status.Text = "Envoyé: " + testText;
                TxBx_TxText.Text = "";
            }
            catch (Exception ex)
            {
                TxBl_status.Text = "sendTextButton_Click: " + ex.Message;
            }

        }
    }
}
