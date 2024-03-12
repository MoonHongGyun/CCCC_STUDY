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
using System.Data.SQLite;

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
        SQLiteConnection serverdb;
        private TcpListener server = null;
        NetworkStream[] streamList = new NetworkStream[10];
        int clntcnt = 0;
        readonly object thisLock = new object();
        int codenum = 1;
        string[] ColorList = new string[8] { "red", "orange", "yellow", "green", "skyblue", "blue", "purple", "pink" };
        int[] min = new int[8] { 0,10, 25, 40, 85, 110, 135, 150 };
        int[] max = new int[8] { 5, 20, 35, 75, 108, 125, 145, 165 };


        public MainWindow()
        {
            InitializeComponent();
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
                    TcpClient client = server.AcceptTcpClient();

                    NetworkStream stream = client.GetStream();
                    lock (thisLock)
                    {
                        streamList[clntcnt++] = stream;
                    }
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        tb.AppendText(" 접속 성공.\n");
                    });

                    DownloadFile(stream,client);
                }
                catch(SocketException e)
                {
                    break;
                }
                
            }

        }

        async private void DownloadFile(NetworkStream stream, TcpClient client)
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
                        byte[] bytes = new byte[1024];
                        int length = stream.Read(bytes, 0, bytes.Length);
                        if (length == 0) // 소켓 종료시
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                tb.AppendText("접속 종료\n");
                            });
                            break;
                        }
                        filedata = Encoding.Default.GetString(bytes, 0, length);
                        string[] token = filedata.Split('^');
                        filename = token[0];
                        int filelength = Convert.ToInt32(token[1]);

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
                        int byteRead = stream.Read(bytes, 0, filelength);
                        if (byteRead > 0)
                            servwriter.Write(bytes, 0, filelength);

                        servwriter.Close();
                        filestream.Close();
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            tb.AppendText(" 파일이 다운로드 되었습니다.\n");
                        });

                        SendData(stream, dirname);

                    }
                    catch(IOException e)
                    {
                        break;
                    }

                }

                stream.Close();
                client.Close();

            });
        }

        private string GetShape(OpenCvSharp.Point[] c)
        {
            string shape = "unidentified";
            double peri = Cv2.ArcLength(c, true);
            OpenCvSharp.Point[] approx = Cv2.ApproxPolyDP(c, 0.04 * peri, true); // 점갯수 반환하는거였음
            double length = Cv2.ArcLength(c, true);

            if (approx.Length == 3) //삼각형
            {
                OpenCvSharp.Rect rect;
                rect = Cv2.BoundingRect(approx); // 주어진 점을 감싸는 최소 크기의 사각형을 반환
                double ar = rect.Width / (double)rect.Height;

                if (ar >= 0.95 && ar <= 1.35) // 정삼각형
                {
                    shape = "equilateral triangle";
                }
                else // 나머지 삼각형?
                {
                    shape = "triangle";
                }
            }
            else if (approx.Length == 4 && length < 300)
            {
                OpenCvSharp.Rect rect;
                rect = Cv2.BoundingRect(approx); // 주어진 점을 감싸는 최소 크기의 사각형을 반환
                double ar = rect.Width / (double)rect.Height;
                if (ar >= 0.95 && ar <= 1.35) // 정사각형
                {
                    shape = "square";
                }
                else // 직사각형
                {
                    shape = "rectangle";
                }
            }
            else if (approx.Length == 5) // 오각형
            {
                shape = "pentagon";
            }
            else if (approx.Length == 6) // 오각형
            {
                shape = "hexagon";
            }
            else if (approx.Length == 10) //별모양
            {
                shape = "star!";
            }
            else // 원
            {
                shape = "circle";
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

                //디비오픈
                //string dbpath = @"Data Source=C:\Users\IOT\Desktop\STUDY_C#\CCCC_STUDY\CATEPRO\CATEPRO\DIV";
                //serverdb = new SQLiteConnection(dbpath);
                //serverdb.Open();

                //string[] DivList = new

                for (int tasknum = 0; tasknum < 8; tasknum++)
                {
                    Cv2.InRange(mv, new Scalar(min[tasknum], 100, 100), new Scalar(max[tasknum], 255, 255), divcolor[tasknum]);
                    Cv2.MedianBlur(mv, mv, 3);

                    Cv2.FindContours(divcolor[tasknum], out var shapecontour, out HierarchyIndex[] shapehierarchy, RetrievalModes.Tree, ContourApproximationModes.ApproxSimple);

                    Cv2.Dilate(divcolor[tasknum], divcolor[tasknum], new Mat(), new OpenCvSharp.Point(-1, -1), 3);
                    foreach (var con in shapecontour)
                    {
                        double pixel = Cv2.ContourArea(con, true);
                        if (pixel < -300)
                        {
                            //RotatedRect rect = Cv2.MinAreaRect(con);
                            mmt[tasknum] = Cv2.Moments(con);
                            OpenCvSharp.Point pnt = new OpenCvSharp.Point(mmt[tasknum].M10 / mmt[tasknum].M00 - 35, mmt[tasknum].M01 / mmt[tasknum].M00); // 중심점 찾기
                            string shape = GetShape(con); // 선분 갯수에 따라서 도형 나누기
                            Cv2.MinEnclosingCircle(con, out Point2f center, out float radius);
                            Cv2.Circle(src, new OpenCvSharp.Point(center.X, center.Y), (int)radius, Scalar.Red, 1, LineTypes.AntiAlias);
                            Cv2.PutText(src, Convert.ToString(codenum) + "_" + ColorList[tasknum] + "_" + shape, pnt, HersheyFonts.HersheySimplex, 0.25, Scalar.Black, 1); //이게 중심점에서 문자 띄우는 역할
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                tb.AppendText(Convert.ToString(codenum) + "_" + ColorList[tasknum] + "_" + shape + "\n");

                            });

                            ////db저장
                            //string sql = $"INSERT INTO RESULT VALUES({codenum++},'{shape}','{ColorList[tasknum]}')";
                            //SQLiteCommand command = new SQLiteCommand(sql, serverdb);
                            //command.ExecuteNonQuery();

                            string message = Convert.ToString(codenum++) + "^" + ColorList[tasknum] + "^" + shape;
                            byte[] msg = Encoding.UTF8.GetBytes(message);
                            stream.Write(msg, 0, msg.Length);
                            stream.Flush();

                        }
                    }
                }

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

        private void Serveropen_btn_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                tb.AppendText("서버 오픈\n");
            });
            ServerRun();
        }

        private void Serverstop_btn_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                tb.AppendText("서버 종료\n");
            });
            server.Stop();
        }
    }
}
