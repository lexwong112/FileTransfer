using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace FileTransfer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private int tcp_port;
        TcpListener? listener;
        IPAddress? public_ip_address;
        IPAddress? private_ip_address;
        private static string[] actions = { "PreviewFile" };

        NetworkStream? server_stream;

        DispatcherTimer timer = new DispatcherTimer();

        public MainWindow()
        {
            InitializeComponent();
            tcp_port = 14535;
            String strHostName = string.Empty;
            IPHostEntry ipEntry = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress[] addr = ipEntry.AddressList;

            timer.Tick += new EventHandler(timer_Tick);
            timer.Interval = TimeSpan.FromSeconds(1); 
            timer.Start();

            this.FileListView.ItemsSource = fileListView;

            for (int i = 0; i < addr.Length; i++)
            {
                if (addr[i].AddressFamily == AddressFamily.InterNetwork)
                {
                    private_ip_address = addr[i];
                }
            }

            public_ip_address = GetIPAddressAsync();
            if(public_ip_address == null)
            {
                PrintMsg("Cannot check your public ip address, you can get it by yourself, port number is " + tcp_port);
                updateIPAddressAndPort(IPAddress.Parse("1.1.1.1"), tcp_port);
            }
            else
            {
                updateIPAddressAndPort(public_ip_address, tcp_port);
            }
        }


        void timer_Tick(object? sender, EventArgs e)// update UI
        {
            fileListView.Clear();
            foreach (var file in fileInfoList)
            {              
                if (Service.upload_file == file.name)
                {
                    file.progress = Service.upload_progress;
                }
                else if(Service.download_file == file.name)
                {
                    file.progress = Service.download_progress;
                }
                file.display_progress = (file.progress * 100 / file.size).ToString() + " %";
                fileListView.Add(file);            
            }
        }

        public class FileTransferInfo
        {
            public string? path { get; set; }
            public string name { get; set; }
            public long size { get; set; }
            public Status status { get; set; }
            public long progress { get; set; }

            public string display_size { get; set; }
            public string display_progress { get; set; }

            public FileTransferInfo(string name, long size, Status status)
            {
                this.name = name;
                this.size = size;
                this.status = status;

                if(0 <= size && size <= 1024)
                {
                    this.display_size = size + "B";
                }
                else if(1024 <= size && size <= 1024*1024)
                {
                    this.display_size = size/1024 + "KB";
                }
                else if (1024 * 1024 <= size && size <= 1024 * 1024*1024)
                {
                    this.display_size = size / 1024 / 1024 + "MB";
                }
                else if (size > 1024 * 1024 * 1024)
                {
                    this.display_size = size / 1024 / 1024 / 1024 + "GB";
                }
                else
                {
                    this.display_size = size + "";
                }

                
                this.progress = 0;
                this.display_progress = "0 %";
            }

            public void setProgress(int bytes)
            {
                progress += bytes;
                if(progress >= size)
                {
                    this.status = Status.completed;
                }
            }

            public enum Status
            {
                upload,
                download,
                uploading,
                downloading,
                completed
            }
            
        }

        void updateIPAddressAndPort(IPAddress ip, int port)
        {
            ServerInfosBlock.Text = "Your Public IP Address: " + ip + ", Port: " + port;
        }


        void StartListening(TcpListener listener)
        {
            
            listener.Start(10);

            byte[] bytes = new byte[256];
            string data = String.Empty;
            int len = 0;

            while (true)
            {
                PrintMsg("Waiting for a connection... ");

                TcpClient client = listener.AcceptTcpClient();

                PrintMsg("Client Connected!");

                data = String.Empty;

                // Get a stream object for reading and writing
                NetworkStream stream = client.GetStream();

                while(data != "Close")
                {
                    len = stream.Read(bytes, 0, bytes.Length);
                    data = System.Text.Encoding.ASCII.GetString(bytes, 0, len);
                    PrintMsg("client: " + data);

                    switch (data)
                    {
                        case "PreviewFile":
                            Service.SendFileList(stream, fileInfoList);
                            break;

                        case "FileTransfer":
                             while(true)
                            {
                                len = stream.Read(bytes, 0, bytes.Length);
                                data = System.Text.Encoding.ASCII.GetString(bytes, 0, len);

                                if(data == "\n")
                                {
                                    PrintMsg("File Transfer Finish");
                                    break;
                                }
                                else
                                {
                                    int i = 0;
                                    foreach(var file in fileInfoList)
                                    {
                                        if(data == file.name && file.status == FileTransferInfo.Status.upload)
                                        {
                                            fileInfoList[i].status = FileTransferInfo.Status.uploading;
                                            Service.FileTransfer(stream, file);
                                            fileInfoList[i].status = FileTransferInfo.Status.completed;
                                            fileInfoList[i].progress = fileInfoList[i].size;
                                            break;
                                        }
                                        i++;
                                    }
                                }
                            }
                            break;

                        default:
                            break;
                    }

                    if(data == "Close")
                    {
                        client.Close();
                        PrintMsg("client closed");
                    }
                }                      
            }
        }

        string readline(NetworkStream stream, int len)
        {
            List<byte> lines = new List<byte>();
            byte readByte = 0;
            int total = 0;
            if (len != 0)
            {
                while (total < len)
                {
                    readByte = (byte)stream.ReadByte();
                    lines.Add(readByte);
                    total++;
                }
            }
            else
            {
                while (readByte != 10)
                {
                    readByte = (byte)stream.ReadByte();
                    lines.Add(readByte);
                }
            }

            byte[] tempByteArr = new byte[lines.Count];
            int i = 0;
            foreach(byte line in lines)
            {
                tempByteArr[i] = (byte)line;
                i++;
            }
            string tempStr = System.Text.Encoding.ASCII.GetString(tempByteArr, 0, i);


            return tempStr;
        }


        void PrintMsg(string msg)
        {
            string thread_id = "[Thread: " + Thread.CurrentThread.ManagedThreadId + "] ";


            this.Dispatcher.Invoke(() => {
                MessageBox.Text += "["+ DateTime.Now.ToShortTimeString() + "] "+ thread_id+  msg + "\n";
            });
        }

        public static IPAddress? GetIPAddressAsync()
        {
            UriBuilder builder = new("https://api.ipify.org");
            HttpClient client = new();

            var res = client.GetAsync(builder.Uri).Result;
            if (res.IsSuccessStatusCode)
            {
                var resContent = res.Content.ReadAsStringAsync().Result;
                return IPAddress.Parse(resContent);

            }
            else
            {
                return null;
            }
        }

        List<FileTransferInfo> fileInfoList = new List<FileTransferInfo>();

        void updateFileListView()
        {
            
        }


        /// /User Interface 

        BindingList<FileTransferInfo> fileListView = new BindingList<FileTransferInfo>();

        private void UploadFile(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            // Display OpenFileDialog by calling ShowDialog method 
            dlg.Multiselect = true;


            Nullable<bool> result = dlg.ShowDialog();

            // Get the selected file name and display in a TextBox 
            if (result == true)
            {
                // Open document 
                foreach (string filePath in dlg.FileNames)
                {
                    FileInfo fileInfo = new FileInfo(filePath);
                    FileTransferInfo fileTransferInfo = new FileTransferInfo(fileInfo.Name, fileInfo.Length, FileTransferInfo.Status.upload);
                    fileTransferInfo.path = filePath;
                    fileInfoList.Add(fileTransferInfo);
                }              
            }
        }

        private void StartServer(object sender, RoutedEventArgs e)
        {
            new Thread(() =>
            {              
                if(private_ip_address!= null)
                {
                    try
                    {
                        PrintMsg("Start UPNP port mapping");
                        PortForward portForward = new PortForward();
                        portForward.AddStaticPortMapping(tcp_port, tcp_port, PortForward.ProtocolType.TCP, private_ip_address.ToString(), true, "FileTransfer");
                        PrintMsg("UPNP Port Mapping completed");
                    }
                    catch(Exception e)
                    {
                        PrintMsg("Fail to start UPNP port mapping. Server Stoped! " + e.Message );
                        return;
                    }
                    
                    listener = new TcpListener(private_ip_address, tcp_port);
                }
                else
                {
                    PrintMsg("Fail to start server. Cannot find your private ip address!");
                    return;
                }
                
                
                try
                {
                    new Thread(() => StartListening(listener)).Start();
                }
                catch (Exception e)
                {
                    PrintMsg(e.Message);
                }               
            }).Start();
        }

        private void CopyIPAddress(object sender, RoutedEventArgs e)
        {
            Clipboard.SetDataObject(public_ip_address+":"+tcp_port);
            PrintMsg("Server infos copied, please send to your client");
        }

        private void PreviewFile(object sender, RoutedEventArgs e)
        {
            IPAddress? server_address;
            int server_port;
            if(ServerInfosBox.Text != null)
            {
                if (!IPAddress.TryParse(ServerInfosBox.Text.Split(":")[0], out server_address) || !int.TryParse(ServerInfosBox.Text.Split(":")[1], out server_port))
                {
                    
                    PrintMsg("Server address format incorrect, please input server ipv4 address + : + port number e.g. 58.82.204.125:12345");
                    return;
                }

                PrintMsg("Connect to server, ip address: " + server_address.ToString() + ", port :" + server_port);

                if (server_stream == null)
                {
                    TcpClient tcp_client = new TcpClient();
                    new Thread(() =>
                    {
                        try
                        {
                            if (tcp_client.ConnectAsync(server_address.ToString(), server_port).Wait(TimeSpan.FromSeconds(5)))
                            {
                                server_stream = tcp_client.GetStream();
                                byte[] buffer = System.Text.Encoding.ASCII.GetBytes("PreviewFile");
                                server_stream.Write(buffer);
                                fileInfoList.AddRange(Service.ReceiveFileList(server_stream));
                            }  
                            else
                            {
                                PrintMsg("Cannot connect to server");
                            }
                        }
                        catch(Exception ex)
                        {
                            PrintMsg(ex.Message);
                        }
                    }).Start();
                    
                }
                else
                {
                    new Thread(() =>
                    {
                        try
                        {
                            byte[] buffer = System.Text.Encoding.ASCII.GetBytes("PreviewFile");
                            server_stream.Write(buffer);
                            fileInfoList.AddRange(Service.ReceiveFileList(server_stream));
                        }
                        catch (Exception ex)
                        {
                            PrintMsg(ex.Message);
                        }
                    }).Start();
                }
            }

           
        }

        private void MessageBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if(sender != null)
            {
                TextBox? textBox = sender as TextBox;
                if(textBox != null)
                    textBox.ScrollToEnd();
            }         
        }

        private void SelectDownloadFolder(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog();
            dialog.Title = "Select a Directory"; 
            dialog.Filter = "Directory|*.this.directory";
            dialog.FileName = "select"; 
            if (dialog.ShowDialog() == true)
            {
                string path = dialog.FileName;
               
                path = path.Replace("\\select.this.directory", "");
                path = path.Replace(".this.directory", "");
               
                if (!System.IO.Directory.Exists(path))
                {
                    System.IO.Directory.CreateDirectory(path);
                }
                
                Service.download_path = path + @"\";
                PrintMsg("Download Path set to: " + path);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if(server_stream != null)
            {
                new Thread(() =>
                {
                    server_stream.Write(Encoding.ASCII.GetBytes("FileTransfer"));
                    int i = 0;
                    foreach (var file in fileInfoList)
                    {
                        if (file.status == FileTransferInfo.Status.download)
                        {
                            fileInfoList[i].status = FileTransferInfo.Status.downloading;
                            server_stream.Write(Encoding.ASCII.GetBytes(file.name));
                            Service.FileReceive(server_stream, file);
                            fileInfoList[i].status = FileTransferInfo.Status.completed;
                            fileInfoList[i].progress = fileInfoList[i].size;
                        }
                        i++;
                    }
                    server_stream.Write(Encoding.ASCII.GetBytes("\n"));
                }).Start();              
            }
            else
            {
                PrintMsg("Please input server infos and click preview file first");
            }
        }
    }                         

    
    
}
