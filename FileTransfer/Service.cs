using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using static FileTransfer.MainWindow;

namespace FileTransfer
{
    public class Service
    {
        public static string download_path = "";

        public static long download_progress = 0;

        public static long upload_progress = 0;

        public static string upload_file = "";

        public static string download_file = "";



        public static string FileTransfer(NetworkStream stream, FileTransferInfo file)
        {
            if(File.Exists(file.path))
            {
                FileStream fileStream = File.OpenRead(file.path);
                byte[] buffer = new byte[1024];
                int bytesRead = 0;
                upload_progress = 0;
                upload_file = file.name;
                while(fileStream.CanRead)
                {
                    bytesRead = fileStream.Read(buffer, 0, buffer.Length);
                    if(bytesRead <= 0)
                    {
                        break;
                    }
                    upload_progress += bytesRead;
                    stream.Write(buffer, 0, bytesRead);

                }
            }

            return "";
        }

        public static string FileReceive(NetworkStream stream, FileTransferInfo file)
        {
            int fileExist = 0;  
            while(File.Exists(download_path + FileNamePrefix(file.name, fileExist)))
            {
                fileExist++;
            }
            string filePath = download_path + FileNamePrefix(file.name, fileExist);
            FileStream fileStream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write);
            int bytesRead = 0;
            long totalBytesRead = 0;
            byte[] buffer = new byte[1024];
            download_file = file.name;
            download_progress = 0;
            while (totalBytesRead < file.size)
            {
                bytesRead = stream.Read(buffer);
                download_progress += bytesRead;
                fileStream.Write(buffer, 0, bytesRead);
                totalBytesRead += bytesRead;       
            }
            fileStream.Close();
            
            return "File: "+file.name+" received";
        }

        public static string SendFileList(NetworkStream stream, List<FileTransferInfo> list)
        {
            string totalFile = "";
            foreach(FileTransferInfo file in list)
            {
                if(file.status == FileTransferInfo.Status.upload)
                {
                    totalFile += file.name + "," + file.size + ";";
                }              
            }
            byte[] buffer = System.Text.Encoding.ASCII.GetBytes(totalFile);
            stream.Write(buffer);

            return "Send File List Completed";
        }

        public static List<FileTransferInfo> ReceiveFileList(NetworkStream stream)
        {
            List<FileTransferInfo> list = new List < FileTransferInfo >();
            byte[] buffer = new byte[1024*1020];
            int len = stream.Read(buffer);
            string data = System.Text.Encoding.ASCII.GetString(buffer, 0, len);
            foreach(string file in data.Split(";"))
            {
                if (file == "")
                {
                    break;
                }
                FileTransferInfo finfo = new FileTransferInfo(file.Split(",")[0], long.Parse(file.Split(",")[1]), FileTransferInfo.Status.download);
                list.Add(finfo);
            }

            return list;
        }

        private static string FileNamePrefix(string filename, int prefix)
        {
            if(prefix == 0)
                return filename;
            else
                return filename.Split(".")[0] + "("+prefix+")" + filename.Split(".")[1];
        }


    }
}
