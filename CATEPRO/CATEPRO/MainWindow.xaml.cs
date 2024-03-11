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
//추가한 헤더
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;

//opencv
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace CATEPRO
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    /// 
    public partial class MainWindow : System.Windows.Window
    {
        private TcpListener server = null;
        NetworkStream[] streamList = new NetworkStream[10];
        int clntcnt = 0;
        readonly object thisLock = new object();
        TcpClient client;
        int codenum = 1;
        string[] ColorList = new string[9] { "red", "orange", "yellow", "green", "skyblue", "blue", "purple", "pink", "red" };
        int[] min = new int[9] { 166, 16, 21, 41, 76, 86, 136, 146, 166 };
        int[] max = new int[9] { 180, 20, 40, 75, 85, 135, 145, 165, 180 };


        public MainWindow()
        {
            InitializeComponent();
            ServerRun();

        }

        private void ServerRun()
        {
            string bindip = "10.10.20.123";
            const int bindport = 9000;

            IPEndPoint localadr = new IPEndPoint(IPAddress.Parse(bindip), bindport);
            server = new TcpListener(localadr);

            server.Start();

            Thread t1 = new Thread(new ThreadStart(ClntManager));
            t1.Start();

        }

        private void ClntManager()
        {
            while (clntcnt < 100)
            {
                try
                {
                    client = server.AcceptTcpClient();
                }
                catch(SocketException e)
                {
                    MessageBox.Show("접속 오류");
                }
                finally
                {
                    NetworkStream stream = client.GetStream();
                    lock (thisLock)
                    {
                        streamList[clntcnt++] = stream;
                    }
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        tb.AppendText(" 접속 성공.\n");

                    });

                    DownloadFile(stream);
                }
                
            }

        }

        async private void DownloadFile(NetworkStream stream)
        {
            await Task.Run(() =>
            {
                string filename = null;
                string filedata = null;
                string dirname = null;
                FileStream filestream;
                BinaryWriter servwriter;
                while (true)
                {
                    try
                    {
                        int filelength,length;
                        byte[] bytes = new byte[1024];

                        int datacheck = length = stream.Read(bytes, 0, bytes.Length);
                        filedata = Encoding.Default.GetString(bytes, 0, length);
                        if (datacheck <= 0) // read data
                            break;

                        string[] token = filedata.Split('^');
                        filename = token[0];

                        filelength = Convert.ToInt32(token[1]);
                        //MessageBox.Show(filename);

                        dirname = "C:\\Users\\IOT\\Desktop\\testfile\\" + filename;
                        filestream = new FileStream(dirname, FileMode.Create, FileAccess.Write);
                        servwriter = new BinaryWriter(filestream);

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            tb.AppendText(dirname + "\n");
                        });

                        byte[] sendbytes = Encoding.Default.GetBytes("ok");
                        stream.Write(sendbytes, 0, sendbytes.Length);

                        //원본
                        bytes = new byte[filelength];
                        stream.Read(bytes, 0, filelength);
                        servwriter.Write(bytes, 0, filelength);

                        //servwriter.Flush();
                        servwriter.Close();
                        //filestream.Flush();
                        filestream.Close();
                    }
                    catch(FileLoadException e)
                    {
                        MessageBox.Show("오류");
                    }

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        tb.AppendText(" 파일이 다운로드 되었습니다.\n");
                    });
                    SendData(stream, dirname);

                }

                stream.Close();
                client.Close();

            });
        }

        private string GetShape(OpenCvSharp.Point[] c)
        {
            string shape = "unidentified";
            double peri = Cv2.ArcLength(c, true);
            OpenCvSharp.Point[] approx = Cv2.ApproxPolyDP(c, 0.02 * peri, true);
            double length = Cv2.ArcLength(c, true);

            if (approx.Length == 3) //if the shape is a triangle, it will have 3 vertices
            {
                shape = "triangle";
            }
            else if (approx.Length == 4 && length < 300)    //if the shape has 4 vertices, it is either a square or a rectangle
            {
                OpenCvSharp.Rect rect;
                rect = Cv2.BoundingRect(approx);
                double ar = rect.Width / (double)rect.Height;

                if (ar >= 0.95 && ar <= 1.05)
                {
                    shape = "square";
                }
                else
                {
                    shape = "rectangle";
                }
            }
            //else if(approx.Length == 10)
            //{
            //    shape = "star!";
            //    num++;
            //}
            else   //otherwise, shape is a circle
            {
                if (length < 800)
                {
                    shape = "circle";
                }
                else
                {
                    shape = " ";
                }
            }
            return shape;
        }

        async private void SendData(NetworkStream stream, string dirname)
        {
            await Task.Run(() =>
            {
                Mat src = Cv2.ImRead(dirname);

                Mat[] divcolor = new Mat[9] { new Mat(), new Mat(), new Mat(), new Mat(), new Mat(), new Mat(), new Mat(), new Mat(), new Mat() };
                Mat[] hierarchy = new Mat[9] { new Mat(), new Mat(), new Mat(), new Mat(), new Mat(), new Mat(), new Mat(), new Mat(), new Mat() };
                Moments[] mmt = new Moments[9];
                double[] cx = new double[9];
                double[] cy = new double[9];

                Mat mv = new Mat();
                Cv2.CvtColor(src, mv, ColorConversionCodes.BGR2HSV);


                Application.Current.Dispatcher.Invoke(() =>
                {
                    cam1.Source = OpenCvSharp.WpfExtensions.WriteableBitmapConverter.ToWriteableBitmap(src);
                });

                //액션 대리자
                //Action<object> FuncDIvision = (object tnum) =>
                //{
                for (int tasknum = 0; tasknum < 3; tasknum++)
                {

                    Cv2.InRange(mv, new Scalar(min[tasknum], 70, 70), new Scalar(max[tasknum], 255, 255), divcolor[tasknum]);

                    Cv2.FindContours(divcolor[tasknum], out var shapecontour, out HierarchyIndex[] shapehierarchy, RetrievalModes.Tree, ContourApproximationModes.ApproxSimple);

                    //Cv2.Dilate(divcolor[tasknum], divcolor[tasknum], new Mat(), new OpenCvSharp.Point(-1, -1), 3);
                    foreach (var con in shapecontour)
                    {
                        double pixel = Cv2.ContourArea(con, true);
                        if (pixel < -300)
                        {
                            mmt[tasknum] = Cv2.Moments(con);
                            OpenCvSharp.Point pnt = new OpenCvSharp.Point(mmt[tasknum].M10 / mmt[tasknum].M00, mmt[tasknum].M01 / mmt[tasknum].M00); // 중심점 찾기
                            string shape = GetShape(con); // 선분 갯수에 따라서 도형 나누기
                            Cv2.DrawContours(src, shapecontour, -1, Scalar.Red, 1, LineTypes.AntiAlias);
                            Cv2.PutText(src, Convert.ToString(codenum) + "_" + shape + "_" + ColorList[tasknum], pnt, HersheyFonts.HersheySimplex, 0.25, Scalar.Black, 1); //이게 중심점에서 문자 띄우는 역할
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                tb.AppendText(Convert.ToString(codenum) + "_" + shape + "_" + ColorList[tasknum] + "\n");
                            });


                            string message = Convert.ToString(codenum++) + "^" + ColorList[tasknum] + "^" + shape;
                            byte[] msg = Encoding.UTF8.GetBytes(message);
                            stream.Write(msg, 0, msg.Length);
                            Thread.Sleep(100);
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                tb.AppendText("send success");
                            });
                        }
                    }
                }

      
                    
                //};

                //Task[] divtask = new Task[9];

                //for (int i = 0; i < 2; i++)
                //{
                //    divtask[i] = Task.Run(() =>
                //    {
                //        FuncDIvision(i);
                //    });
                //}

                //divtask[0].Wait();
                //divtask[1].Wait();
                //divtask[2].Wait();

                //foreach (Task task in divtask)
                //{
                //    task.Wait();
                //}


                Application.Current.Dispatcher.Invoke(() =>
                {
                    cam2.Source = OpenCvSharp.WpfExtensions.WriteableBitmapConverter.ToWriteableBitmap(src);
                });
            });
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            server.Stop();
        }

    }
}

//int filelength, filenamelength, length;
//byte[] bytes = new byte[1024];

//int datacheck = length = stream.Read(bytes, 0, bytes.Length);
//filename = Encoding.Default.GetString(bytes, 0, length);
//                        if (datacheck <= 0) // read data
//                            break;
//                        //원본
//                        stream.Read(bytes, 0, bytes.Length);


//                        filelength = BitConverter.ToInt32(bytes, 0);

//                        bytes = new byte[filenamelength];
//                        length = stream.Read(bytes, 0, bytes.Length);
//                        filename = Encoding.Default.GetString(bytes, 0, length);

//                        bytes = new byte[4];
//                        stream.Read(bytes, 0, bytes.Length);
//                        filenamelength = BitConverter.ToInt32(bytes, 0);
