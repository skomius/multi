using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Data;
using System.Threading;
using System.IO;


// Nesiseke instaliuoti MDAC. Su winform irgi nesiseke, todel padariau su WPF ir duomenis rasiau i paprasta faila.
// Timer polls linklist and updates interface (146). Special thread (162) writes to file. Rest of threads generate 
// write and sleep (79).    
namespace multi
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        LinkedList<Data> cash = new LinkedList<Data>();
        static readonly object locker = new object();

        static readonly object locker1 = new object();

        ThreadLocal<Random> localRandom = new ThreadLocal<Random>(() => new Random());

        CancellationToken cancellationToken = new CancellationToken();

        Timer timer;

        Thread IOThread;

        Data dataWrite;

        ManualResetEvent begin = new ManualResetEvent(false);
        ManualResetEvent end = new ManualResetEvent(false);

        int i = 0;

        public MainWindow()
        {
            InitializeComponent();

            Id.DisplayMemberBinding = new Binding("Id");
            Symbols.DisplayMemberBinding = new Binding("Symbols");

        }

        private void start_click(object sender, RoutedEventArgs e)
        {
            cancellationToken.IsCancellationRequested = false;
            button.IsEnabled = false;

            IOThread = new Thread(() => ioTask(cancellationToken));
            IOThread.Start();

            int threads;
            int.TryParse(textBox.Text, out threads);

            for (int i = 0; i < threads; i++)
            {
                new Thread(() => mainTask(cancellationToken)).Start();
            }

            timer = new Timer(new TimerCallback(updateListView_callback), null, 0, int.Parse(polling.Text));
        }

        private void stop_click(object sender, RoutedEventArgs e)
        {
            timer.Dispose();
            cancellationToken.Cancel();
            listView.Items.Clear();
            cash.Clear();
            button.IsEnabled = true;
            i = 0;
            Thread.Sleep(1000);
        }

        void mainTask(CancellationToken token)
        {
            ThreadLocal<Random> localRandom = new ThreadLocal<Random>(() => new Random(Thread.CurrentThread.ManagedThreadId));

            try
            {
                while (true)
                {
                    Thread.Sleep(randomTime(localRandom.Value));

                    token.ThrowIfCancellationRequested();
                    Data data = new Data();
                    data.Id = Thread.CurrentThread.ManagedThreadId.ToString();

                    data.Symbols = stringGenerator(localRandom.Value);

                    addToList(data);
                    token.ThrowIfCancellationRequested();

                    dataToIOThread(data);
                    token.ThrowIfCancellationRequested();
                }
            }
            catch (OperationCanceledException e)
            {

            }
        }

        string stringGenerator(Random random)
        {
            double rd = random.NextDouble() * 5;
            int length = 5 + (int)rd;
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var stringChars = new char[length];

            for (int i = 0; i < stringChars.Length; i++)
            {
                stringChars[i] = chars[random.Next(chars.Length)];
            }

            return new string(stringChars);
        }

        int randomTime(Random random)
        {
            double rd = random.NextDouble() * 1500;
            return (int)rd + 500;
        }

        void addToList(Data data)
        {
            lock (locker)
            {
                if (i == 20)
                {
                    cash.AddFirst(data);
                    cash.RemoveLast();
                }
                if (i < 20)
                {
                    i++;
                    cash.AddFirst(data);
                }
            }
        }

        void updateListView_callback(object obj)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                lock (locker)
                {
                    listView.Items.Clear();
                    foreach (Data data in cash)
                    {
                        listView.Items.Add(data);
                    }
                }

            }));
        }

        void ioTask(CancellationToken token)
        {
            FileStream fileStream = File.Open("data.txt", FileMode.Append);
            TextWriter textWriter = new StreamWriter(fileStream);
            try
            {
                while (true)
                {
                    end.WaitOne();
                    WriteAsyncHelper(dataWrite, textWriter);
                    end.Reset();
                    begin.Set();
                }
            }
            catch (Exception ex)
            {
                textWriter.Flush();
                fileStream.Dispose();
                textWriter.Dispose();
            }
        }

        void dataToIOThread(Data data)
        {
            lock (locker)
            {
                dataWrite = data;
                end.Set();
                begin.WaitOne();
                begin.Reset();
            }
        }

        public async void WriteAsyncHelper(Data data, TextWriter textWriter)
        {
            await textWriter.WriteLineAsync(data.Id + " " + data.Symbols);
            textWriter.Flush();
        }

        class CancellationToken
        {
            public bool IsCancellationRequested { get; set; }
            public void Cancel() { IsCancellationRequested = true; }
            public void ThrowIfCancellationRequested()
            {
                if (IsCancellationRequested)
                    throw new OperationCanceledException();
            }
        }


        public struct Data
        {
            public Data(string _id, string _symbols)
            {
                id = _id;
                symbols = _symbols;
            }

            string id;
            public string Id
            {
                get { return id; }
                set { id = value; }
            }

            string symbols;
            public string Symbols
            {
                get { return symbols; }
                set { symbols = value; }
            }
        }

    }




}

