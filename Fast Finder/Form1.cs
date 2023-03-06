using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Permissions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Fast_Finder
{
    public partial class Fast_Finder : Form
    {
        public readonly string cpath = $@"{(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData))}\FastFinder";
        public readonly string cfile = @"\filelist.000";
        public readonly string[] CBData = new string[4] { "Files and Folder Names", "Only File Names", "Only Folder Names", "File Extension" };
        public string search = "";

        public static class Files
        {
            public static System.Windows.Forms.Label[] label = new System.Windows.Forms.Label[0];
            public static string[] Drives;
            public static string[] FileList;
            public static int[] Fnum = new int[0];
            public static ConcurrentBag<string> folder_names = new ConcurrentBag<string>();
            public static ConcurrentBag<string> file_names = new ConcurrentBag<string>();
            public static int Folnum = 0;
        }
        public bool Labels = false;
        public bool working = false;
        public bool cancel = false;
        public bool loaded = false;
        public Fast_Finder()
        {
            InitializeComponent();
            listb1.Sorted = true;
            listb1.HorizontalScrollbar = true;
            label4.Text = string.Empty;
            comboBox1.DataSource = CBData; comboBox1.SelectedIndex = 0;
            label1.Text = string.Empty;
            this.Shown += new System.EventHandler(this.Form1_Shown);
        }
        public static byte[] Compress(byte[] data)
        {
            MemoryStream output = new MemoryStream();
            using (DeflateStream dstream = new DeflateStream(output, CompressionLevel.Optimal))
            {
                dstream.Write(data, 0, data.Length);
            }
            return output.ToArray();
        }
        public static string[] Decompress(byte[] data)
        {
            MemoryStream input = new MemoryStream(data);
            MemoryStream output = new MemoryStream();
            using (DeflateStream dstream = new DeflateStream(input, CompressionMode.Decompress))
            {
                dstream.CopyTo(output);
            }
            var decompdata = output.ToArray();
            input.Dispose(); output.Dispose();
            input.Close(); output.Close();

            List<string> list = new List<string>();
            int i = 0;
            var buffer = new MemoryStream();
            var write = new BinaryWriter(buffer);
            while (i < decompdata.Length)
            {
                if (decompdata[i] != '\n') { write.Write(decompdata[i]); i++; }
                else
                {
                    byte[] temp = buffer.ToArray();
                    list.Add(Encoding.UTF8.GetString(temp));
                    buffer.SetLength(0); i++;
                }
            }
            buffer.Dispose(); write.Dispose();
            buffer.Close(); write.Close();
            return list.ToArray();
        }
        async void Start_Search()
        {
            if (!working)
            {
                working = true;
                listb1.Items.Clear();
                int t = 0; int m = 0;
                Thread u = new Thread(new ThreadStart(Upd));
                u.Start();
                start.Text = "Cancel";
                await Task.Run(() =>
                {
                    var cond = 0;
                    var comp = "";
                    Invoke(new Action(() => cond = comboBox1.SelectedIndex));
                    for (int i = 0; i < Files.FileList.Length; i++)
                    {
                        if (cancel) break;
                        switch(cond)
                        {
                            case 0: comp = Files.FileList[i]; break;
                            case 1: comp = Path.GetFileNameWithoutExtension(Files.FileList[i]); break;
                            case 2: comp = Path.GetDirectoryName(Files.FileList[i]); break;
                            case 3: comp = Path.GetExtension(Files.FileList[i]); break;
                        }
                        t = i;
                        if (comp.Contains(search))
                        {
                            if (!listb1.Items.Contains(Files.FileList[i]))
                            {
                                if (File.Exists(Files.FileList[i])) Invoke(new Action(() => listb1.Items.Add(Files.FileList[i])));
                            }
                            m++;
                        }
                    }
                    working = false;
                    Thread.Sleep(150);
                    cancel = false;
                    Invoke(new Action(() => start.Text = "Start"));
                });

                void Upd()
                {
                    while (t != Files.FileList.Length - 1)
                    {
                        Thread.Sleep(100);
                        if (cancel) break;
                        Invoke(new Action(() => { label4.Text = $"Searched {t + 1:N0} / Matches ({m + 1:N0})"; }));
                    }
                }
            }

        }
        async void Load_List()
        {
            if (!Directory.Exists(cpath)) Directory.CreateDirectory(cpath);
            if (!File.Exists(cpath + cfile)) Populate_Files();
            else
            {
                label1.Text = "Loading Saved File List";
                await Task.Run(() =>
                {
                    Get_Drives();
                    string[] dl = new string[Files.Drives.Length];
                    int[] df = new int[Files.Drives.Length];
                    for (int i = 0; i < dl.Length; i++) dl[i] = Files.Drives[i].Split(':').First();
                    Files.FileList = Decompress(File.ReadAllBytes(cpath + cfile));
                    if (!Labels) Invoke(new Action(() => Set_Labels(Files.Drives.Length + 1)));
                    for (int p = 0; p < Files.FileList.Length; p++)
                    {
                        var c = Files.FileList[p].Split(':').First();
                        for (int i = 0; i < dl.Length; i++)
                        {
                            if (c == dl[i]) df[i] += 1;
                        }
                    }
                    Invoke(new Action(() =>
                    {
                        for (int i = 0; i < dl.Length; i++)
                        {
                            Files.label[i].Text = $"{Files.Drives[i]} {df[i]:N0}";
                        }
                        Files.label[Files.label.Length - 1].Text = $"Total ({df.Sum():N0})";
                        label1.Text = "";
                    }));
                });
            }
        }
        void Set_Labels(int t)
        {
            Files.label = new System.Windows.Forms.Label[t];
            for (int i = 0; i < Files.label.Length; i++)
            {
                Files.label[i] = new System.Windows.Forms.Label()
                {
                    AutoSize = true,
                    Font = new System.Drawing.Font("Courier New", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0),
                    Location = new System.Drawing.Point(540, 100 + (i * 13)),
                };
                Controls.Add(Files.label[i]);
            }
            Labels = true;
        }
        void Get_Drives()
        {
            var d = System.IO.DriveInfo.GetDrives();
            List<string> list = new List<string>();
            list.Sort();
            for (int i = 0; i < d.Length; i++)
            {
                list.Add(d[i].ToString());
            }
            Files.Drives = list.ToArray();
            list.Clear();
        }
        private async void Populate_Files()
        {
            if (!working)
            {
                working = true;
                button1.Enabled = false; button1.Text = "Scanning";
                Get_Drives();
                Thread[] EF = new Thread[Files.Drives.Length];
                Files.Fnum = new int[Files.Drives.Length];
                if (!Labels) Set_Labels(Files.Drives.Length + 1);
                bool[] parse = new bool[Files.Drives.Length];
                await Task.Run(() =>
                {
                    Invoke(new Action(() => label1.Text = "Getting File List."));
                    Thread c = new Thread(() => Upd());
                    c.Start();
                    for (int i = 0; i < Files.Drives.Length; i++)
                    {
                        parse[i] = false;
                        var u = i;
                        EF[i] = new Thread(() => Enum_List(Files.Drives[u], u));
                        EF[i].Start();
                    }
                    for (int i = 0; i < Files.Drives.Length; i++) EF[i].Join();
                    c.Abort();
                    Invoke(new Action(() => label1.Text = "Sorting File List"));
                    CompList();
                    Files.FileList = Decompress(File.ReadAllBytes(cpath + cfile));
                    Invoke((Action)(() => { label1.Text = ""; button1.Enabled = true; button1.Text = "Rescan"; }));
                    working = false;

                });

                void CompList()
                {
                    List<string> list = Files.file_names.ToList();
                    list.Sort();
                    Files.file_names = new ConcurrentBag<string>();
                    var buffer = new MemoryStream();
                    var write = new BinaryWriter(buffer);
                    for (int i = 0; i < list.Count; i++)
                    {
                        byte[] p = Encoding.ASCII.GetBytes(list[i]);
                        write.Write(p);
                    }
                    byte[] bytes = buffer.ToArray();
                    buffer.Flush();
                    buffer.Close();
                    list.Clear();
                    var comp = Compress(bytes);
                    File.WriteAllBytes(cpath + cfile, comp);
                    Array.Clear(bytes, 0, bytes.Length);
                }

                void Enum_List(string drive, int i)
                {
                    string[] fi;
                    Files.Fnum[i] = 0;
                    fi = Get(drive).ToArray();
                    if (fi.Length > 0) { parse[i] = true; Process_List(fi); }

                    IEnumerable<string> Get(string path)
                    {
                        IEnumerable<string> f = Enumerable.Empty<string>();
                        IEnumerable<string> d = Enumerable.Empty<string>();
                        try
                        {
                            var permission = new FileIOPermission(FileIOPermissionAccess.Read, path);
                            permission.Demand();
                            f = Directory.GetFiles(path);
                            d = Directory.GetDirectories(path);
                        }
                        catch { path = null; }
                        foreach (var file in f)
                        {
                            Files.Fnum[i]++;
                            yield return file;
                        }
                        var subdirectoryItems = d.SelectMany(Get);
                        foreach (var result in subdirectoryItems)
                        {
                            yield return result; // result;
                        }
                    }
                }
                void Process_List(string[] files)
                {
                    for (int i = 0; i < files.Length; i++)
                    {
                        Files.file_names.Add(files[i] + '\n');
                    }
                }
                void Upd()
                {
                    Thread.Sleep(200);
                    while (1 == 1)
                    {
                        Invoke(new Action(() =>
                        {
                            for (int i = 0; i < Files.label.Length - 1; i++)
                            {
                                if (EF[i].IsAlive)
                                {
                                    if (!parse[i]) Files.label[i].Text = $"{Files.Drives[i]} {Files.Fnum[i]:N0}";
                                }
                                if (!EF[i].IsAlive)
                                {
                                    if (Files.Fnum[i] > 0) Files.label[i].Text = $"{Files.Drives[i]} {Files.Fnum[i]:N0}";
                                    else Files.label[i].Text = $"{Files.Drives[i]} No Files!";
                                }
                            }
                            Files.label[Files.Drives.Length].Text = $"(Total {Files.Fnum.Sum():N0})";
                        }));
                        Thread.Sleep(50);
                    }
                }
            }
        }
        private void Button1_Click(object sender, EventArgs e)
        {
            Populate_Files();
        }
        private void Form1_Shown(object sender, EventArgs e)
        {
            loaded = true;
            Load_List();
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            if (textBox1.Text.Length > 0) search = textBox1.Text.ToString();
        }
        private void ListBox1_DoubleClick(object sender, EventArgs e)
        {
            if (listb1.SelectedItem != null)
            {
                if (listb1.SelectedItem.ToString().Length != 0)
                {
                    string filePath = listb1.SelectedItem.ToString();
                    if (!File.Exists(listb1.SelectedItem.ToString()))
                    {
                        return;
                    }
                    string argument = "/select, \"" + filePath + "\"";
                    System.Diagnostics.Process.Start("explorer.exe", argument);
                }
            }
        }
        private void TextBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyValue == (char)Keys.Enter)
            {
                e.Handled = e.SuppressKeyPress = true;
                if (textBox1.Text.Length > 0)
                {
                    if (search.Length > 0) Start_Search();
                }
            }
        }

        private void start_Click(object sender, EventArgs e)
        {
            if (working) cancel = true;
            else Start_Search();
        }

        private async void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (loaded)
            {
                if (!working && search.Length > 0) Start_Search();
                else
                {
                    cancel = true;
                    while (cancel == true)
                    { await Task.Delay(50); }
                    if (search.Length > 0) Start_Search();
                }
            }
        }
    }
}
