using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace CATECLNT
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        NetworkStream stream;
        TcpClient client;
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            string bindip = "10.10.20.123";
            int bindport = 0;
            string serverip = "10.10.20.123";
            const int serverport = 9000;

            IPEndPoint clientadr = new IPEndPoint(IPAddress.Parse(bindip), bindport);
            IPEndPoint serveradr = new IPEndPoint(IPAddress.Parse(serverip), serverport);

            client = new TcpClient(clientadr);

            client.Connect(serveradr);

            stream = client.GetStream();

            string filepath = "C:\\Users\\IOT\\Desktop\\cola\\shape.jpg";
            string filename = "shape.jpg";
            int length;

            FileStream filestream = new FileStream(filepath, FileMode.Open, FileAccess.Read);


            int filelength = (int)filestream.Length;
            byte[] bytes = BitConverter.GetBytes(filelength);
            stream.Write(bytes, 0, bytes.Length);

            int filenamelength = (int)filename.Length;
            bytes = BitConverter.GetBytes(filenamelength);
            stream.Write(bytes, 0, bytes.Length);

            bytes = Encoding.UTF8.GetBytes(filename);
            stream.Write(bytes, 0, bytes.Length);

            BinaryReader clntreader = new BinaryReader(filestream);

            if(filelength <= 100000)
            {
                bytes = clntreader.ReadBytes(filelength);
                stream.Write(bytes, 0, bytes.Length);
            }
            else
            {
                int total = filelength / 10000 + 1;
                for (int i = 0; i < total; i++)
                {
                    bytes = clntreader.ReadBytes(10000);
                    stream.Write(bytes, 0, bytes.Length);
                }
            }


            //recvmessage();

        }

        //async public void recvmessage()
        //{
        //    await Task.Run(() =>
        //    {
        //        while (true)
        //        {
        //            byte[] msg = new byte[4];
        //            int msglen;
        //            int msgbytelen;
        //            stream.Read(msg, 0, msg.Length);
        //            msgbytelen = BitConverter.ToInt32(msg, 0);
        //            msg = new byte[msgbytelen];
        //            msglen = stream.Read(msg, 0, msg.Length);
        //            string data = Encoding.UTF8.GetString(msg, 0, msglen);
        //            Application.Current.Dispatcher.Invoke(() =>
        //            {
        //                tb.AppendText(data + "\n");
        //            });
        //        }
        //    });
        //}

        private void Window_Closed(object sender, EventArgs e)
        {
            stream.Close();
            client.Close();
        }
    }
}
