using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Scooper
{
    public partial class MainWindow : Window
    {
        private readonly static string TIP = "输入要查找的程序",
            NOT_FOUND = "No matches found.",
            INSTALLED = "is already installed",
            UNINSTALLED = "was uninstalled",
            OTHER = "Results from other known buckets",
            SUCCESS = " was installed successfully",
            LATEST = "latest version",
            FAILED = "Update failed";

        private readonly ItemCollection ITEMS;
        private readonly List<Object> PRE_ITEMS = new();
        private int progVal = 0;

        public MainWindow()
        {
            InitializeComponent();
            ITEMS = dataList.Items;

            List();
        }

        private void List() {
            string list = Tool.Cmd("/C scoop list");

            string[] lines = list.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            for (int i = 1, j = lines.Length; i < j; i++)
            {
                string[] l = lines[i].Trim().Split(' ');
                string bucket = l[2].Replace("[", "").Replace("]", "");
                ITEMS.Add(new App(l[0], l[1], bucket));
            }
        }

        private void Search_GotFocus(object sender, RoutedEventArgs e)
        {
            if (TIP == search.Text)
            {
                search.Text = "";
            }
        }

        private void Search_LostFocus(object sender, RoutedEventArgs e)
        {
            if ("" == search.Text.Trim())
            {
                search.Text = TIP;
                Menu1();
            }
        }

        private void RenderProgBar(int value){
            progVal = value;
            if (progVal > 100)
            {
                progVal = 0;
            }
            Dispatcher.Invoke(() => (
                progbar.Value = progVal
            ));
        }

        private void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e) {
            RenderProgBar(progVal += 20);
        }

        private void Clone() {
            PRE_ITEMS.Clear();
            foreach (var item in ITEMS)
            {
                PRE_ITEMS.Add(item);
            }
        }

        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() => {
                string res = Tool.Cmd("/C scoop status");
                MessageBox.Show(res);
            });
        }

        private void Install_Click(object sender, RoutedEventArgs e)
        {
            string full = SelectName();
            if (!full.Contains("(")) {
                return;
            }

            Task.Run(() => {
                string name = full.Split("(")[0].Trim();
                string res = Tool.Cmd("/C scoop install " + name);
                Debug.WriteLine(res);
                if (res.Contains(INSTALLED))
                {
                    MessageBox.Show("已安装!");
                }
                else if (res.Contains(SUCCESS))
                {
                    MessageBox.Show(name + "安装成功!\n" + res);
                    Dispatcher.Invoke(() => {
                        ITEMS.Clear(); 
                        List();
                    });
                    Clone();
                    Menu1();
                }
                else {
                    MessageBox.Show(name + " 安装失败!");
                    res = Tool.Cmd("/C scoop uninstall " + name);
                    Debug.WriteLine("rollback: " + res);
                }
            });
        }

        private void Update_Click(object sender, RoutedEventArgs e)
        {
            string name = SelectName();
            if (name.Contains("("))
            {
                return;
            }
            Task.Run(() => {
                string res = Tool.Cmd("/C scoop update " + name);
                Debug.WriteLine(res);
                if (res.Contains(LATEST))
                {
                    MessageBox.Show("已经是最新版本!");
                }
                else if (res.Contains(SUCCESS))
                {
                    MessageBox.Show("更新成功!");
                }
                else {
                    MessageBox.Show(res);
                }
            });
        }

        private void Uninstall_Click(object sender, RoutedEventArgs e)
        {
            App app = SelectApp();

            Task.Run(() => {
                string name = app.Name;
                string res = Tool.Cmd("/C scoop uninstall " + name);
                Debug.WriteLine(res);
                if (res.Contains(UNINSTALLED))
                {
                    Dispatcher.Invoke(() => {
                        ITEMS.Remove(app);
                    });
                }
                else
                {
                    MessageBox.Show(res);
                }
            });
        }

        private void Search_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string keyword = search.Text.Trim();
                if (keyword == "") {
                    ITEMS.Clear();
                    RenderProgBar(0);
                    foreach (var item in PRE_ITEMS)
                    {
                        ITEMS.Add(item);
                    }
                    return;
                }
                search.IsEnabled = false;

                Timer timer = new();
                timer.Enabled = true;
                timer.Interval = 500;
                timer.Start();
                timer.Elapsed += new ElapsedEventHandler(Timer_Elapsed);

                Task.Run(() => {
                    try
                    {
                        string res = Tool.Cmd("/C scoop search " + keyword);
                        if (res == "" || res.Contains(NOT_FOUND))
                        {
                            MessageBox.Show("没有找到");
                        } else if (res.Contains(OTHER)) {
                            MessageBox.Show(res);
                        }else{
                            Clone();
                            Dispatcher.Invoke(() => ITEMS.Clear());
                            string[] buckets = res.Split("\r\n\r\n", StringSplitOptions.RemoveEmptyEntries);
                            for (int i = 0, j = buckets.Length; i < j; i++)
                            {
                                string[] bks = buckets[i].Split("\n", StringSplitOptions.RemoveEmptyEntries);
                                string bucket = bks[0].Replace("bucket:", "").Replace("'", "").Trim();
                                for (int n = 1, m = bks.Length; n < m; n++)
                                {
                                    string line = bks[n].Trim();
                                    string[] lines = line.Split("(");
                                    string version = lines[1].Split(")")[0];
                                    Dispatcher.Invoke(() =>
                                    {
                                        ITEMS.Add(new App(line, version, bucket));
                                    });
                                }
                            }
                            Menu2();
                        }
                    }
                    finally
                    {
                        timer.Close();
                        Dispatcher.Invoke(() => {
                            search.IsEnabled = true;
                            search.Focus();
                        });
                        RenderProgBar(100);
                    }
                });
            }
        }

        private App SelectApp() {
            App app = dataList.SelectedItem as App;
            return app;
        }

        private string SelectName() {
            return SelectApp().Name;
        }

        private class App {
            private string name;
            private string version;
            private string bucket;

            public string Name { get => name; set => name = value; }
            public string Version { get => version; set => version = value; }
            public string Bucket { get => bucket; set => bucket = value; }

            public App(string name, string version, string bucket)
            {
                Name = name;
                Version = version;
                Bucket = bucket;
            }
        }

        private void Menu1()
        {
            Dispatcher.Invoke(() => {
                install.Visibility = Visibility.Collapsed;
                update.Visibility = Visibility.Visible;
                uninstall.Visibility = Visibility.Visible;
            });
        }

        private void Menu2() {
            Dispatcher.Invoke(() => {
                install.Visibility = Visibility.Visible;
                update.Visibility = Visibility.Collapsed;
                uninstall.Visibility = Visibility.Collapsed;
            });
        }

    }
}
