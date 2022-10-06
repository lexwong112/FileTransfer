using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static FileTransfer.MainWindow;

namespace FileTransfer
{
    public class FileTransferService
    {
        public int port = 8008;
        public IPEndPoint? ip_endpoint { get; set; }
        public IPAddress? public_ip_address;
        public IPAddress? private_ip_address;


        public string resource_path = "htdocs";
        Socket? server_socket;
        public bool server_started { get; set; }

        public FileTransferService()
        {
            server_started = false;
        }

        private int read_line(Socket socket, char[] message)
        {
            byte[] buffer = new byte[1];
            char result = '0';
            int read;
            int i = 0;

            NetworkStream stream = new NetworkStream(socket);

            while(result != '\n')
            {
                read = stream.ReadByte();
                if (read != -1)
                {
                    result = Convert.ToChar(read);
                    if (result == '\r')
                    {
                        socket.Receive(buffer, SocketFlags.Peek);
                        result = Convert.ToChar(buffer[0]);
                        if(result == '\n')
                        {
                            read = stream.ReadByte();
                            result = Convert.ToChar(read);
                        }                                                                
                    }
                    message[i++] = result;
                }
            }

            return i;
        }


        public void start_server()
        {
            if(private_ip_address != null)
            {
                PortForward portForward = new PortForward();
                portForward.AddStaticPortMapping(port, port, PortForward.ProtocolType.TCP, private_ip_address.ToString(), true, "FileTransfer");

                server_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                server_socket.Bind(new IPEndPoint(private_ip_address, port));
                start_listen();
            }
            else
            {
                throw new Exception("Server IP address not found");
            }

        }

        private void start_listen()
        {
            if(server_socket != null)
            {
                server_socket.Listen(10);
            }
            else
            {
                throw new Exception("Server unintialize");
            }

            while(true)
            {
                Socket client_socket = server_socket.Accept();
                new Thread(() => accept_request(client_socket)).Start();
            }
        }

        private int accept_request(Socket client_socket)
        {
            byte[] buff = new byte[1024];

            //GET / HTTP/1.1\n
            //client_socket.Receive(buff, 0, buff.Length, SocketFlags.None);

            int len = 0;
            char[] line = new char[1024];
            len = read_line(client_socket, line);

            //Get http request method
            string method = String.Empty;
            int i;
            for (i = 0;i< len; i++)
            {
                if (!Char.IsWhiteSpace(line[i]))
                {
                    method += line[i];
                }
                else
                {
                    break;
                }
            }

            //check method 
            if(String.Compare(method, "GET", true)   >= 0)
            {

            }

            //skip space character
            while(Char.IsWhiteSpace(line[i])) i++;

            //get request resource path
            string url = string.Empty;
            string request_resource = string.Empty;
            for (; i < len; i++)
            {
                if (!Char.IsWhiteSpace(line[i]))
                {
                    url += line[i];
                }
                else
                {
                    break;
                }
            }

            if (url == "/")
            {
                request_resource = resource_path + "/index.html";
            }
            else
            {
                foreach(FileTransferInfo item in MainWindow.fileInfoList)
                {
                    if("/"+item.name == url)
                    {
                        request_resource = item.path;
                    }
                }

                /*request_resource = resource_path + url;
                var ext = System.IO.Path.GetExtension(request_resource);
                if (ext == String.Empty)
                {
                    request_resource += "/index.html";
                }*/
            }

            request_resource =  request_resource.Replace("%20", " ");
            //skip space character
            while (Char.IsWhiteSpace(line[i])) i++;

            //get http protocal
            string protocal = string.Empty;
            for (; i < len; i++)
            {
                if (line[i] != '\n')
                {
                    protocal += line[i];
                }
                else
                {
                    break;
                }
            }

            //read all header data
            do
            {
                len = read_line(client_socket, line);
            }
            while (line[0] != '\n');

            //send resource
            if (!File.Exists(request_resource))
            {
                //return not found message and read remain data
                client_socket.Close();
                return -1;
            }
            else
            {
                //send header message
                send_header(client_socket, request_resource);

                //send resource
                send_resources(client_socket, request_resource);

            }

            client_socket.Close();

            return 0;
        }

        private int send_header(Socket client_socket, string request_resource)
        {
            FileInfo fileInfo = new FileInfo(request_resource);
            string content_type = "";
            //check resource type
            if(fileInfo.Extension == ".html")
            {
                content_type = "text/html";
            }
            else
            {
                content_type = "binary/octet-stream";
            }

            string header = "HTTP/1.1 200 OK\r\n";
            byte[] buffer = System.Text.Encoding.ASCII.GetBytes(header);
            client_socket.Send(buffer);

            header = "Server: FileTransferHttpd/0.1\r\n";
            buffer = System.Text.Encoding.ASCII.GetBytes(header);
            client_socket.Send(buffer);

            header = "Content-type: "+ content_type + "\r\n"; 
            buffer = System.Text.Encoding.ASCII.GetBytes(header);
            client_socket.Send(buffer);
       
            if(content_type == "binary/octet-stream")
            {
                header = "Content-Disposition: attachment; filename = \"" + fileInfo.Name + "\"\r\n";
                buffer = System.Text.Encoding.ASCII.GetBytes(header);
                client_socket.Send(buffer);
            }


            header = "\r\n";
            buffer = System.Text.Encoding.ASCII.GetBytes(header);
            client_socket.Send(buffer);
            return 0;
        }

        private int send_resources(Socket client_socket, string request_resource)
        {
            byte[] buffer = new byte[4096];
            int len = 0;

            FileStream resource = new FileStream(request_resource, FileMode.Open, FileAccess.Read);         
            while(resource.CanRead)
            {
                len = resource.Read(buffer, 0, buffer.Length);
                if(len > 0)
                {
                    client_socket.Send(buffer, 0, len, SocketFlags.None);
                }
                else
                {
                    //buffer = System.Text.Encoding.ASCII.GetBytes("\r\n");
                    //client_socket.Send(buffer);
                    break;
                }
            }
            resource.Close();            
            return 0;
        }

        public void create_index(List<FileTransferInfo> list)
        {

            FileStream fileStream = new FileStream(resource_path + "/index.html", FileMode.Create, FileAccess.Write);

            //header
            string content = "<head><title>File Transfer</title></head>\n";
            byte[] buffer = System.Text.Encoding.ASCII.GetBytes(content);
            fileStream.Write(buffer);

            content = "<body><h1>File Transfer Service Started</h1>\n";
            buffer = System.Text.Encoding.ASCII.GetBytes(content);
            fileStream.Write(buffer);

            foreach(FileTransferInfo item in list)
            {
                content = "<a href=\"" + item.name + "\">Download: "+ item.name +"</a><br>\n";
                buffer = System.Text.Encoding.ASCII.GetBytes(content);
                fileStream.Write(buffer);
            }

            content = "</body>";
            buffer = System.Text.Encoding.ASCII.GetBytes(content);
            fileStream.Write(buffer);
        }

    }


}
