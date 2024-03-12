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
        int num = 1;
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

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            string filepath = "C:\\Users\\IOT\\Desktop\\cola\\shape" + Convert.ToString(num) + ".jpg";
            string filename = "shape" + Convert.ToString(num) +".jpg^" ;
            byte[] filebyte = Encoding.UTF8.GetBytes(filename);
            //num++;


            FileStream filestream = new FileStream(filepath, FileMode.Open, FileAccess.Read);


            int filelength = (int)filestream.Length;
            filename += filelength.ToString();
            byte[] bytes = new byte[1024];
            bytes = Encoding.Default.GetBytes(filename);
            stream.Write(bytes, 0, bytes.Length);

            stream.Read(bytes, 0, bytes.Length);

            BinaryReader clntreader = new BinaryReader(filestream);

            bytes = clntreader.ReadBytes(filelength);
            stream.Write(bytes, 0, bytes.Length);

            RecvMsg();

            filestream.Close();
            clntreader.Close();

        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {

        }

        async public void RecvMsg()
        {
            await Task.Run(() =>
            {
                while(true)
                {
                    byte[] bytes = new byte[1024];
                    stream.Read(bytes, 0, bytes.Length);
                    string test = Encoding.UTF8.GetString(bytes, 0, bytes.Length);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        tb.AppendText(test);
                    });
                }
            });
        }
    }
}
