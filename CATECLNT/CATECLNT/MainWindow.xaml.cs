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
        int filelength;
        FileStream filestream;
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

            string filepath = "C:\\Users\\IOT\\Desktop\\cola\\shape" + Convert.ToString(num) + ".jpg";

            recvmessage();

        }



        async public void recvmessage() //데이터수신 ok일때만 파일전송 나머지는 테이블박스에 데이터 올림
        {
            await Task.Run(() =>
            {
                string filepath = "C:\\Users\\IOT\\Desktop\\cola\\shape" + Convert.ToString(num) + ".jpg";
                string filename = "shape" + Convert.ToString(num) + ".jpg^";
                byte[] filebyte = Encoding.UTF8.GetBytes(filename);
                int testlen;
                string checktest = "ok\n";

                byte[] rcbytes = new byte[1024];
                byte[] bytes = new byte[1024];
                while (true)
                {
                    testlen = stream.Read(rcbytes, 0, rcbytes.Length);
                    rcbytes[testlen] = 0;
                    string test = Encoding.UTF8.GetString(rcbytes, 0, testlen);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        tb.AppendText(test);
                    });

                    if(test == checktest)
                    {
                        BinaryReader clntreader = new BinaryReader(filestream);

                        bytes = clntreader.ReadBytes(filelength);
                        stream.Write(bytes, 0, bytes.Length);
                        clntreader.Close();
                    }
                    filestream.Close();
                }
                
            });
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            stream.Close();
            client.Close();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            string filepath = "C:\\Users\\IOT\\Desktop\\cola\\shape" + Convert.ToString(num) + ".jpg";
            string filename = "shape" + Convert.ToString(num) + ".jpg^";
            byte[] filebyte = Encoding.UTF8.GetBytes(filename);
            num++;

            filestream = new FileStream(filepath, FileMode.Open, FileAccess.Read);

            filelength = (int)filestream.Length;
            filename += filelength.ToString();
            byte[] bytes = new byte[1024];
            byte[] rcbytes = new byte[1024];
            bytes = Encoding.Default.GetBytes(filename);
            stream.Write(bytes, 0, bytes.Length);


        }
    }
}
