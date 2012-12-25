using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.IO;
using System.Collections;
using System.Threading;
using System.Web;
using System.Resources;

namespace CachedProxyServer
{
    public partial class Form1 : Form
    {
        private static string ProxyAddress = "127.0.0.1";   // Хост или IP-адрес
        private static int ProxyPort = 1234;    // Номер порта
        private static int IncomingTraffic = 0;
        private static int OutboundTraffic = 0;

        private static bool AppendHTML = true;  // Если true, то в html-ответы клиенту будет добавлять дополнительный код
        private static bool ShouldStop = true;

        private static readonly object syncRoot = new object();

        private static TcpListener myTCPListener;
        private static Thread NetScan;

        List<Cache> CacheList = new List<Cache>();
        

        delegate void SetTextCallback(string text, params object[] args);
        delegate void SetLabel1Callback(int input);
        delegate void SetLabel2Callback(int output);

        
        public Form1()
        {
            InitializeComponent();
            StopButton.Enabled = false;
            myTCPListener = new TcpListener(IPAddress.Parse(ProxyAddress), ProxyPort);

            SetLabel1(0);
            SetLabel2(0);
            CacheList.Capacity = 1000;
            
        }

        private void StartButton_Click(object sender, EventArgs e)
        {
            this.Icon = Properties.Resources.circle_green;
            StartButton.Enabled = false;
            StopButton.Enabled = true;
            ShouldStop = true;

            myTCPListener.Start();
            myTimer.Start();

            NetScan = new Thread(NetScaner);
            NetScan.Start();
            NetScan.IsBackground = true;

            WriteLog("Прокси-сервер успешно запущен, ожидание запросов на адрес {0} порт {1}.", ProxyAddress, ProxyPort);
        }

        private void WriteLog(string Text, params object[] args)
        {
            lock (syncRoot)
            {
                LogList.Items.Add(string.Format(DateTime.Now.ToString() + " : " + Text, args));
                LogList.SelectedIndex = LogList.Items.Count - 1;
                if (LogList.Items.Count == 1000)
                    LogList.Items.Clear();

                using (var LogFile = File.Open("log.txt", FileMode.Append))
                using (var stream = new StreamWriter(LogFile))
                {
                    stream.WriteLine(string.Format(DateTime.Now.ToString() + " : " + Text, args));
                }
            }
        }

        private void StopButton_Click(object sender, EventArgs e)
        {
            this.Icon = Properties.Resources.circle_red;
            StartButton.Enabled = true;
            StopButton.Enabled = false;
            ShouldStop = false;
            NetScan.Abort();
            myTCPListener.Stop();

            WriteLog("Прокси-сервер остановлен.");
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.Text = string.Format("Прокси-сервер. IP-адрес: {0}. Порт: {1}.", ProxyAddress, ProxyPort);
        }

        private void NetScaner()
        {
            while (ShouldStop)
            {
                // Ожидаем запросов
                if (myTCPListener.Pending())
                {
                    Thread t = new Thread(ExecuteRequest);
                    t.IsBackground = true;
                    t.Start(myTCPListener.AcceptSocket());
                }
            }
        }

        public void SetText(string text, params object[] args)
		{
            if (this.LogList.InvokeRequired)
			{	
				SetTextCallback d = new SetTextCallback(SetText);
				this.Invoke(d, new object[] { text, args });
			}
			else
			{
                WriteLog(text, args);
			}
		}

        public void SetLabel1(int input)
        {
            if (this.statusStrip1.InvokeRequired)
            {
                SetLabel1Callback d = new SetLabel1Callback(SetLabel1);
                this.Invoke(d, new object[] { input });
            }
            else
            {
                double help = input;
                if (help < 1024)
                    this.TrafficStatusLabel1.Text = help.ToString() + " байт.";
                else if (help < 1024 * 1024)
                    this.TrafficStatusLabel1.Text = Math.Round(help / 1024, 2).ToString() + " Кб.";
                else  if (help < 1024*1024*1024)
                    this.TrafficStatusLabel1.Text = Math.Round(help / (1024*1024), 2).ToString() + " Мб.";
                else
                    this.TrafficStatusLabel1.Text = Math.Round(help / (1024 * 1024 * 1024), 2).ToString() + " Гб.";
                
            }
        }

        public void SetLabel2(int output)
        {
            if (this.statusStrip1.InvokeRequired)
            {
                SetLabel2Callback d = new SetLabel2Callback(SetLabel2);
                this.Invoke(d, new object[] { output });
            }
            else
            {
                double help = output;
                if (help < 1024)
                    this.TrafficStatusLabel2.Text = help.ToString() + " байт.";
                else if (help < 1024 * 1024)
                    this.TrafficStatusLabel2.Text = Math.Round(help / 1024, 2).ToString() + " Кб.";
                else if (help < 1024 * 1024 * 1024)
                    this.TrafficStatusLabel2.Text = Math.Round(help / (1024 * 1024), 2).ToString() + " Мб.";
                else
                    this.TrafficStatusLabel2.Text = Math.Round(help / (1024 * 1024 * 1024), 2).ToString() + " Гб.";

            }
        }

        private void ExecuteRequest(object arg)
        {
            try
            {
                
                // Пытаемся установить соединение
                using (Socket myClient = (Socket)arg)
                {
                    bool LoadFromInternet = true;

                    if (myClient.Connected)
                    {
                        // Есть соединение. Получаем содержимое запроса
                        byte[] HTTPRequest = ReadToEnd(myClient);

                        // Парсим полученный запрос
                        HTTP.HTMLParser http = new HTTP.HTMLParser(HTTPRequest);

                        // если заголовки не найдены, значит выполнить запрос не получится
                        if (http.Items == null || http.Items.Count <= 0 || !http.Items.ContainsKey("Host"))
                        {
                            SetText("Получен запрос {0} байт, заголовки не найдены.", HTTPRequest.Length);
                            OutboundTraffic += HTTPRequest.Length;
                            SetLabel1(OutboundTraffic);
                        }
                        // заголовки найдены, можно продолжать выполнение запроса
                        else
                        {
                            if (CacheList.Count != 0)
                            {
                                foreach (var HelpHTTP in CacheList.ToArray())
                                {
                                    if (HelpHTTP.myHTTP.Path == http.Path)
                                    {
                                        
                                            byte[] HTTPResponse = null;
                                            LoadFromInternet = false;
                                            // Получаем ответ

                                            if (HelpHTTP.myReroutingHTTP.Source != null && HelpHTTP.myReroutingHTTP.Source.Length > 0)
                                            {
                                                SetText("Данные загружены из кэша!");
                                                HTTPResponse = HelpHTTP.myReroutingHTTP.Source;

                                            }

                                            if (HTTPResponse != null)
                                                myClient.Send(HTTPResponse, HTTPResponse.Length, SocketFlags.None);
                                        

                                    }
                                }
                            }
                            if (LoadFromInternet)
                            {
                                SetText("Получен запрос {0} байт, метод {1}, хост {2}:{3}", HTTPRequest.Length, http.Method, http.Host, http.Port);
                                OutboundTraffic += HTTPRequest.Length;
                                SetLabel1(OutboundTraffic);

                                byte[] HTTPResponse = null;     // Ответ клиенту

                                // Определяем IP-адрес по имени хоста
                                IPHostEntry myIPHostEntry = Dns.GetHostEntry(http.Host);
                                if (myIPHostEntry == null || myIPHostEntry.AddressList == null || myIPHostEntry.AddressList.Length <= 0)
                                {
                                    SetText("Не удалось определить IP-адрес по хосту {0}.", http.Host);
                                }
                                else
                                {
                                    SetText("IP-адреса хоста {0}: {1}", http.Host, myIPHostEntry.AddressList[0]);

                                    // Создаем точку доступа
                                    IPEndPoint myIPEndPoint = new IPEndPoint(myIPHostEntry.AddressList[0], http.Port);

                                    // Запрос к защищенному ресурсу
                                    if (http.Method == HTTP.HTMLParser.MethodsList.CONNECT)
                                    {
                                        SetText("Ошибка!!! Протокол HTTPS не реализован!");
                                    }
                                    // Обычный запрос
                                    else
                                    {
                                        // Перенаправляем запрос на указанный хост
                                        using (Socket myRerouting = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                                        {
                                            myRerouting.Connect(myIPEndPoint);
                                            if (myRerouting.Send(HTTPRequest, HTTPRequest.Length, SocketFlags.None) != HTTPRequest.Length)
                                            {
                                                SetText("Ошибка!!! Данные не были отправлены хосту {0}", http.Host);
                                            }
                                            else
                                            {
                                                // Получаем ответ
                                                HTTP.HTMLParser ReroutingHTTPResponse = new HTTP.HTMLParser(ReadToEnd(myRerouting));
                                                if (ReroutingHTTPResponse.Source != null && ReroutingHTTPResponse.Source.Length > 0)
                                                {
                                                    SetText("Получен ответ {0} байт, код состояния{1}.", ReroutingHTTPResponse.Source.Length, ReroutingHTTPResponse.StatusCode);
                                                    IncomingTraffic += ReroutingHTTPResponse.Source.Length;
                                                    SetLabel2(IncomingTraffic);

                                                    HTTPResponse = ReroutingHTTPResponse.Source;

                                                    Cache CacheRecord = new Cache(http, ReroutingHTTPResponse, DateTime.Now);
                                                    lock (syncRoot)
                                                    {
                                                        CacheList.Add(CacheRecord);
                                                    }

                                                }
                                                else
                                                    SetText("Ошибка!!! Получен ответ 0 байт.");
                                            }
                                            myRerouting.Close();
                                        }
                                    }

                                }
                                if (HTTPResponse != null)
                                    myClient.Send(HTTPResponse, HTTPResponse.Length, SocketFlags.None);
                            }
                        }
                    }
                    myClient.Close();
                }
            }
            catch (System.Exception ex)
            {
                this.SetText("Ошибка: {0}", ex.Message);
            }
        }

        private static byte[] ReadToEnd(Socket mySocket)
        {
            byte[] HelpByte = new byte[mySocket.ReceiveBufferSize];
            int len = 0;
            using (MemoryStream m = new MemoryStream())
            {
                while (mySocket.Poll(1000000, SelectMode.SelectRead) && (len = mySocket.Receive(HelpByte, mySocket.ReceiveBufferSize, SocketFlags.None)) > 0)
                {
                    m.Write(HelpByte, 0, len);
                }
                return m.ToArray();
            }
        }

        private void myTimer_Tick(object sender, EventArgs e)
        {
            if (CacheList.Count != 0)
            {
                foreach (var HelpList in CacheList.ToArray())
                {
                    if (HelpList.LiveTime.AddSeconds((double)numericUpDown1.Value) < DateTime.Now)
                    {
                        CacheList.Remove(HelpList);
                        SetText("Удалена запись кэша от {0}", HelpList.LiveTime);
                    }
                }
            }
            progressBar1.Value = CacheList.Count;
            
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            SetText("Установлено новое время жизни записи кэша: {0} секунд.", numericUpDown1.Value);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            NetScan.Abort();
            myTCPListener.Stop();
            WriteLog("Прокси-сервер остановлен");
        }

        private void ClearLogButton_Click(object sender, EventArgs e)
        {
            lock (syncRoot)
            {
                var LogFile = File.Open("log.txt", FileMode.Truncate);
                LogFile.Close();
            }
        }
    }
}
