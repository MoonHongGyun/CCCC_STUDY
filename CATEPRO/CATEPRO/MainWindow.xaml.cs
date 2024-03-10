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
        string[] ColorList = new string[8] { "red", "orange", "yellow", "green", "skyblue", "blue", "purple","red" };
        int[] min = new int[8] { 166, 16, 21, 41, 76, 86,136,166 };
        int[] max = new int[8] { 180, 20, 40, 75, 85, 135, 145,180};
        

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
            while(true)
            {
                client = server.AcceptTcpClient();

                NetworkStream stream = client.GetStream();
                lock(thisLock)
                {
                    streamList[clntcnt++] = stream;
                }

                DownloadFile(stream);

            }

        }

        async private void DownloadFile(NetworkStream stream)
        {
            await Task.Run(() =>
            {
                int filelength, filenamelength, length;
                string data = null;
                string filename = null;
                int total = 0;

                byte[] bytes = new byte[4];
                stream.Read(bytes, 0, bytes.Length);
                filelength = BitConverter.ToInt32(bytes, 0);

                bytes = new byte[4];
                stream.Read(bytes, 0, bytes.Length);
                filenamelength = BitConverter.ToInt32(bytes, 0);

                bytes = new byte[filenamelength];
                length = stream.Read(bytes, 0, bytes.Length);
                filename = Encoding.Default.GetString(bytes, 0, length);

                string dirname = "C:\\Users\\IOT\\Desktop\\testfile\\" + filename;
                FileStream filestream = new FileStream(dirname, FileMode.Create, FileAccess.Write);
                BinaryWriter servwriter = new BinaryWriter(filestream);

                if (filelength <= 100000)
                {
                    bytes = new byte[filelength];
                    length = stream.Read(bytes, 0, bytes.Length);
                    servwriter.Write(bytes, 0, length);
                }
                else
                {
                    while(total < filelength)
                    {
                        bytes = new byte[10000];
                        length = stream.Read(bytes, 0, bytes.Length);
                        servwriter.Write(bytes, 0, length);
                        total += length;
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            tb.AppendText(Convert.ToString(total)+"\n");
                        });
                    }
                }
               

                Application.Current.Dispatcher.Invoke(() =>
                {
                    tb.AppendText(filename + " 파일이 다운로드 되었습니다.\n");
                });

                Mat src = Cv2.ImRead(dirname);


                Mat[] divcolor = new Mat[9] { new Mat(), new Mat(), new Mat(), new Mat(), new Mat(), new Mat(), new Mat(), new Mat(), new Mat() };
                Mat[] hierarchy = new Mat[9] { new Mat(), new Mat(), new Mat(), new Mat(), new Mat(), new Mat(), new Mat(), new Mat(), new Mat() };
                Moments[] mmt = new Moments[9];
                double[] cx = new double[9];
                double[] cy = new double[9];

                Mat mv = new Mat();
                Mat black = new Mat();
                Cv2.CvtColor(src, mv, ColorConversionCodes.BGR2HSV);


                Application.Current.Dispatcher.Invoke(() =>
                {
                    cam1.Source = OpenCvSharp.WpfExtensions.WriteableBitmapConverter.ToWriteableBitmap(src);
                });

                //액션 대리자
                Action<object> FuncDIvision = (object tnum) =>
                {
                    int tasknum = (int)tnum;

                    Cv2.InRange(mv, new Scalar(min[tasknum], 70, 70), new Scalar(max[tasknum], 255, 255), divcolor[tasknum]);

                    Cv2.FindContours(divcolor[tasknum], out var shapecontour, out HierarchyIndex[] shapehierarchy, RetrievalModes.Tree, ContourApproximationModes.ApproxSimple);


                    foreach (var con in shapecontour)
                    {
                        double pixel = Cv2.ContourArea(con, true);
                        if (pixel < -300)
                        {
                            mmt[tasknum] = Cv2.Moments(con);
                            OpenCvSharp.Point pnt = new OpenCvSharp.Point(mmt[tasknum].M10 / mmt[tasknum].M00, mmt[tasknum].M01 / mmt[tasknum].M00); // 중심점 찾기
                            string shape = GetShape(con); // 선분 갯수에 따라서 도형 나누기
                            lock (thisLock)
                            {
                                Cv2.PutText(src, Convert.ToString(codenum) + "_" + shape + "_" + ColorList[tasknum], pnt, HersheyFonts.HersheySimplex, 0.25, Scalar.Black, 1); //이게 중심점에서 문자 띄우는 역할
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    tb.AppendText(Convert.ToString(codenum) + "_" + shape + "_" + ColorList[tasknum]+"\n");
                                });
                                //string message = Convert.ToString(codenum++) + "^" + ColorList[tasknum] + "^" + shape;
                                //byte[] msg = Encoding.UTF8.GetBytes(message);
                                //int msglen = (int)message.Length;
                                //byte[] msglenbyte = BitConverter.GetBytes(msglen);
                                //stream.Write(msglenbyte, 0, msglenbyte.Length);
                                //stream.Write(msg, 0, msg.Length);
                                //Application.Current.Dispatcher.Invoke(() =>
                                //{
                                //    tb.AppendText("send success");
                                //});
                            }
                        }
                    }
                };

                Task[] divtask = new Task[8];

                for (int i = 0; i < 8; i++)
                {
                    divtask[i] = Task.Run(() =>
                    {
                        FuncDIvision(i);
                    });
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        tb.AppendText(Convert.ToString(i));
                    });
                }

                //divtask[0].Wait();
                //divtask[1].Wait();
                //divtask[2].Wait();

                foreach (Task task in divtask)
                {
                    task.Wait();
                }


                Application.Current.Dispatcher.Invoke(() =>
                {
                    cam2.Source = OpenCvSharp.WpfExtensions.WriteableBitmapConverter.ToWriteableBitmap(src);
                });


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

        private void Window_Closed(object sender, EventArgs e)
        {
            server.Stop();
        }
    }
}

//foreach (var con in contour)
//{
//    double pixel = con.ContourArea();
//    if (pixel > 700)
//    {
//        mmt[tasknum] = Cv2.Moments(con);
//        cx[tasknum] = mmt[tasknum].M10 / mmt[tasknum].M00;
//        cy[tasknum] = mmt[tasknum].M01 / mmt[tasknum].M00;

//        Cv2.CvtColor(con, black, ColorConversionCodes.BGR2GRAY);

//        Cv2.FindContours(black, out var shapecontour, out HierarchyIndex[] shapehierarchy, RetrievalModes.Tree, ContourApproximationModes.ApproxSimple);

//        foreach (var c in shapecontour)
//        {
//            string shape = GetShape(c); // 선분 갯수에 따라서 도형 나누기
//            Cv2.PutText(src, shape, new OpenCvSharp.Point(cx[tasknum], cy[tasknum]), HersheyFonts.Italic, 0.5, Scalar.Black, 1);
//        }
//        //lock (thisLock)
//        //{
//        //    Cv2.PutText(src, Convert.ToString(codenum) + "-" + ColorList[tasknum], new OpenCvSharp.Point(cx[tasknum] , cy[tasknum]), HersheyFonts.HersheySimplex, 0.5, Scalar.Black, 1, LineTypes.AntiAlias);
//        //    Application.Current.Dispatcher.Invoke(() =>
//        //    {
//        //        tb.AppendText(Convert.ToString(codenum++) + ".st " + ColorList[tasknum] + "\n");
//        //    });
//        //}
//    }
//}