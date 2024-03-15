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
        NetworkStream[] streamList = new NetworkStream[100];
        int clntcnt = 0;
        readonly object thisLock = new object();
        int codenum = 1;
        string[] ColorList = new string[8] { "red", "orange", "yellow", "green", "skyblue", "blue", "purple", "pink" };
        string[] ShapeList = new string[8] { "circle", "equilateral_triangle", "triangle", "square", "rectangle", "pentagon", "hexagon", "star" };
        //int[] min = new int[8] { 170,5, 25, 37, 85, 114, 125, 148 };
        //int[] max = new int[8] { 180, 20, 35, 75, 113, 124, 145, 168 };
        int[] shapenum = new int[8] { 0, 0, 0, 0, 0, 0, 0, 0 };
        int[] colornum = new int[8] { 0, 0, 0, 0, 0, 0, 0, 0 };
        string colorchoice,shapechoice;
        


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
            for (int i = 0; i < 8; i++)
            {
                ResultShapeCount.Getinstance().Add(new ResultShapeCount() { dataA = ShapeList[i], dataB = shapenum[i] });
                ResultColorCount.Getinstance().Add(new ResultColorCount() { dataA = ColorList[i], dataB = colornum[i] });
            }
            Application.Current.Dispatcher.Invoke(() =>
            {
                result1_lv.ItemsSource = ResultShapeCount.Getinstance();
                result2_lv.ItemsSource = ResultColorCount.Getinstance();
            });
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
                byte[] sendbytes = Encoding.Default.GetBytes("ok\n");
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
                                tb.AppendText("접속 종료.\n");
                            });
                            break;
                        }
                        
                        
                        filedata = Encoding.Default.GetString(bytes, 0, length);
                        string[] token = filedata.Split('^');
                        filename = token[0];
                        int filelength = Convert.ToInt32(token[1]);
                        //Application.Current.Dispatcher.Invoke(() =>
                        //{
                        //    tb.AppendText(token[0] + "\n" + token[1] + "\n");
                        //});

                        dirname = "C:\\Users\\IOT\\Desktop\\testfile\\" + filename;
                        filestream = new FileStream(dirname, FileMode.Create, FileAccess.Write);
                        servwriter = new BinaryWriter(filestream);

                        stream.Write(sendbytes, 0, sendbytes.Length);
                        //Application.Current.Dispatcher.Invoke(() =>
                        //{
                        //    tb.AppendText("ok 송신완료\n");
                        //});

                        //원본
                        bytes = new byte[filelength];
                        int byteRead = stream.Read(bytes, 0, filelength);
                        if (byteRead > 0)
                            servwriter.Write(bytes, 0, filelength);

                        servwriter.Close();
                        filestream.Close();

                        SendData(stream, dirname);

                    }
                    catch(IOException e)
                    {
                        
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
                    shape = "equilateral_triangle";
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
                if (ar >= 0.95 && ar <= 1.07) // 정사각형
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
                shape = "star";
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
                //Application.Current.Dispatcher.Invoke(() =>
                //{
                //    tb.AppendText(dirname + "\n");
                //});
                Mat src = Cv2.ImRead(dirname);

                Mat[] divcolor = new Mat[9] { new Mat(), new Mat(), new Mat(), new Mat(), new Mat(), new Mat(), new Mat(), new Mat(), new Mat() };
                Mat[] hierarchy = new Mat[9] { new Mat(), new Mat(), new Mat(), new Mat(), new Mat(), new Mat(), new Mat(), new Mat(), new Mat() };
                Moments[] mmt = new Moments[9];
                double[] cx = new double[9];
                double[] cy = new double[9];

              

                Mat mv = new Mat();
                Cv2.CvtColor(src, mv, ColorConversionCodes.BGR2HSV);

                Mat mvlight = mv + 5;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    cam1.Source = OpenCvSharp.WpfExtensions.WriteableBitmapConverter.ToWriteableBitmap(src);
                });

                //디비오픈
                string dbpath = @"Data Source=C:\Users\IOT\Desktop\STUDY_C#\CCCC_STUDY\CATEPRO\CATEPRO\DIV";
                serverdb = new SQLiteConnection(dbpath);
                serverdb.Open();

                Mat redplus = new Mat();

                Cv2.InRange(mv, new Scalar(0, 60, 73), new Scalar(3, 255, 255), redplus);
                Cv2.InRange(mv, new Scalar(169, 60, 70), new Scalar(180, 255, 255), divcolor[0]);
                Cv2.InRange(mv, new Scalar(5, 50, 91), new Scalar(20, 255, 255), divcolor[1]);
                Cv2.InRange(mv, new Scalar(25, 38, 95), new Scalar(35, 255, 255), divcolor[2]);
                Cv2.InRange(mvlight, new Scalar(43, 24, 25), new Scalar(80, 255, 255), divcolor[3]);
                Cv2.InRange(mv, new Scalar(85, 50, 55), new Scalar(113, 255, 255), divcolor[4]);
                Cv2.InRange(mv, new Scalar(114, 70, 74), new Scalar(124, 255, 255), divcolor[5]);
                Cv2.InRange(mv, new Scalar(125, 45, 50), new Scalar(145, 255, 255), divcolor[6]);
                Cv2.InRange(mv, new Scalar(148, 49, 85), new Scalar(168, 255, 255), divcolor[7]);

                Cv2.Add(divcolor[0], redplus, divcolor[0]);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    cam1.Source = OpenCvSharp.WpfExtensions.WriteableBitmapConverter.ToWriteableBitmap(divcolor[3]);
                });

                for (int tasknum = 0; tasknum < 8; tasknum++)
                {
                   

                    Cv2.FindContours(divcolor[tasknum], out var shapecontour, out HierarchyIndex[] shapehierarchy, RetrievalModes.Tree, ContourApproximationModes.ApproxSimple);

                    Cv2.Dilate(divcolor[tasknum], divcolor[tasknum], new Mat(), new OpenCvSharp.Point(-1, -1), 3);
                    foreach (var con in shapecontour)
                    {
                        double pixel = Cv2.ContourArea(con, true);
                        if (pixel < -300)
                        {
                            mmt[tasknum] = Cv2.Moments(con);
                            OpenCvSharp.Point pnt = new OpenCvSharp.Point(mmt[tasknum].M10 / mmt[tasknum].M00 - 35, mmt[tasknum].M01 / mmt[tasknum].M00); // 중심점 찾기
                            string shape = GetShape(con); // 선분 갯수에 따라서 도형 나누기
                            Cv2.MinEnclosingCircle(con, out Point2f center, out float radius);
                            Cv2.Circle(src, new OpenCvSharp.Point(center.X, center.Y), (int)radius, Scalar.Red, 1, LineTypes.AntiAlias);
                            Cv2.PutText(src, Convert.ToString(codenum) + "_" + ColorList[tasknum] + "_" + shape, pnt, HersheyFonts.HersheySimplex, 0.25, Scalar.Black, 1); //이게 중심점에서 문자 띄우는 역할

                            lock (thisLock)
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    tb.AppendText(Convert.ToString(codenum) + "_" + ColorList[tasknum] + "_" + shape +"좌표 = " +pnt+ "\n");
                                    ProceedData.Getinstance().Add(new ProceedData() { dataA = codenum, dataB = shape + "-" + ColorList[tasknum] });
                                });

                                NumCount(shape, ColorList[tasknum]);

                                //db저장
                                string sql = $"INSERT INTO RESULT VALUES({codenum},'{shape}','{ColorList[tasknum]}')";
                                SQLiteCommand command = new SQLiteCommand(sql, serverdb);
                                command.ExecuteNonQuery();

                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    total_lb.Content = $"TOTAL : {codenum}";
                                });
                                string message = Convert.ToString(codenum++) + "^" + ColorList[tasknum] + "^" + shape;
                                byte[] msg = Encoding.UTF8.GetBytes(message);
                                stream.Write(msg, 0, msg.Length);
                                stream.Flush();
                                Thread.Sleep(100);
                            }
                        }
                    }
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    cam2.Source = OpenCvSharp.WpfExtensions.WriteableBitmapConverter.ToWriteableBitmap(src);
                    proceed_lv.ItemsSource = ProceedData.Getinstance();
                    proceed_lv.Items.Refresh();
                    SetNumCount();
                    //result1_lv.ItemsSource = ResultShapeCount.Getinstance();
                    //result2_lv.ItemsSource = ResultColorCount.Getinstance();
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

        class ProceedData
        {
            public int dataA { get; set; }
            public string dataB { get; set; }

            private static List<ProceedData> instance;

            public static List<ProceedData> Getinstance()
            {
                if (instance == null)
                    instance = new List<ProceedData>();
                return instance;
            }
        }

        class ResultShapeCount
        {
            public string dataA { get; set; }
            public int dataB { get; set; }

            private static List<ResultShapeCount> instance;

            public static List<ResultShapeCount> Getinstance()
            {
                if (instance == null)
                    instance = new List<ResultShapeCount>();
                return instance;
            }
        }

        class ResultColorCount
        {
            public string dataA { get; set; }
            public int dataB { get; set; }

            private static List<ResultColorCount> instance;

            public static List<ResultColorCount> Getinstance()
            {
                if (instance == null)
                    instance = new List<ResultColorCount>();
                return instance;
            }
        }

        private void NumCount(string shapecheck,string colorcheck)
        {
            
            for (int i = 0; i < 8; i++)
            {
                if (ShapeList[i] == shapecheck)
                    shapenum[i]++;
                if (ColorList[i] == colorcheck)
                    colornum[i]++;
            }

        }

        private void SetNumCount()
        {
            for (int i = 0; i < 8; i++)
            {
                ResultShapeCount rsc = ResultShapeCount.Getinstance().ElementAt(i);
                rsc.dataB = shapenum[i];
                ResultColorCount rcc = ResultColorCount.Getinstance().ElementAt(i);
                rcc.dataB = colornum[i];
            }
            result1_lv.Items.Refresh();
            result2_lv.Items.Refresh();
        }

        private void Color_cb_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            object text = color_cb.SelectedItem;
            string be = Convert.ToString(text);
            string[] findcolor = be.Split(' ');
            colorchoice = findcolor[1];
            SelectData();
        }

        private void Object_cb_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            object text = object_cb.SelectedItem;
            string be = Convert.ToString(text);
            string[] findshape = be.Split(' ');
            shapechoice = findshape[1];
            SelectData();
        }

        public class Search
        {
            public string color { get; set; }
            public string shape { get; set; }
            private static List<Search> instance;
            public static List<Search> GetInstance()
            {
                if (instance == null)
                {
                    instance = new List<Search>();
                }
                return instance;
            }
        }

        public void SelectData()
        {
            MessageBox.Show("지워야하는 갯수" + search_lv.Items.Count.ToString());
            for (int i = search_lv.Items.Count - 1; i >= 0; i--)
            {
                Search.GetInstance().RemoveAt(i);
            }

            if (colorchoice == "none")
            {
                string sql = $"SELECT COLOR, SHAPE FROM RESULT WHERE SHAPE = '{shapechoice}'";
                SQLiteCommand cmd = new SQLiteCommand(sql, serverdb);
                SQLiteDataReader rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    string a = rdr["COLOR"].ToString();
                    string b = rdr["SHAPE"].ToString();
                    Search.GetInstance().Add(new Search() { color = a, shape = b });
                }
                search_lv.ItemsSource = Search.GetInstance();
                search_lv.Items.Refresh();
            }
            else if (shapechoice == "none")
            {
                string sql = $"SELECT COLOR, SHAPE FROM RESULT WHERE COLOR = '{colorchoice}'";
                SQLiteCommand cmd = new SQLiteCommand(sql, serverdb);
                SQLiteDataReader rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    string a = rdr["COLOR"].ToString();
                    string b = rdr["SHAPE"].ToString();
                    Search.GetInstance().Add(new Search() { color = a, shape = b });
                }
                search_lv.ItemsSource = Search.GetInstance();
                search_lv.Items.Refresh();
            }
            else
            {
                string sql = $"SELECT COLOR, SHAPE FROM RESULT WHERE COLOR = '{colorchoice}' AND SHAPE = '{shapechoice}'";
                SQLiteCommand cmd = new SQLiteCommand(sql, serverdb);
                SQLiteDataReader rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    string a = rdr["COLOR"].ToString();
                    string b = rdr["SHAPE"].ToString();
                    Search.GetInstance().Add(new Search() { color = a, shape = b });
                }

                search_lv.ItemsSource = Search.GetInstance();
                search_lv.Items.Refresh();
            }
        }

    }
}
