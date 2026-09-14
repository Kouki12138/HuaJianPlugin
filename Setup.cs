using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace SplineSetup
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupForm());
        }
    }

    internal class SetupForm : Form
    {
        private Label status;
        private Button closeBtn;

        public SetupForm()
        {
            Text = "花键插件一键安装（HUAJIAN）";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(470, 210);
            Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);

            status = new Label();
            status.Text = "正在安装，请稍候…";
            status.Location = new Point(20, 22);
            status.Size = new Size(430, 100);
            status.TextAlign = ContentAlignment.MiddleCenter;
            Controls.Add(status);

            closeBtn = new Button();
            closeBtn.Text = "退出";
            closeBtn.Location = new Point(190, 128);
            closeBtn.Size = new Size(90, 30);
            closeBtn.Enabled = false;
            closeBtn.Click += OnCloseClick;
            Controls.Add(closeBtn);

            // 水印：一行版权信息
            Label wm = new Label();
            wm.Text = "作者：吴浩然　联系方式：18240187400";
            wm.Location = new Point(0, ClientSize.Height - 26);
            wm.Size = new Size(ClientSize.Width, 20);
            wm.TextAlign = ContentAlignment.MiddleCenter;
            wm.ForeColor = Color.FromArgb(140, 140, 140);
            wm.Font = new System.Drawing.Font("Microsoft YaHei UI", 8.5F, FontStyle.Italic);
            Controls.Add(wm);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            try
            {
                Install();
                status.Text = "安装成功！\r\n\r\n请重启 AutoCAD，然后输入命令 HUAJIAN 即可使用。";
            }
            catch (System.Exception ex)
            {
                status.Text = "安装失败：" + ex.Message;
                MessageBox.Show("安装失败：\n" + ex.Message +
                    "\n\n若提示文件被占用，请先关闭 AutoCAD，然后重新运行本安装程序。",
                    "花键插件一键安装", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                closeBtn.Enabled = true;
            }
        }

        private void OnCloseClick(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private static void Install()
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            const string pkgRes = "PackageContents.xml";
            const string dllRes = "HuaJianPlugin.dll";

            if (asm.GetManifestResourceStream(pkgRes) == null || asm.GetManifestResourceStream(dllRes) == null)
                throw new InvalidOperationException("安装程序缺少插件资源，请使用 build.cmd 重新生成。");

            string destRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Autodesk\ApplicationPlugins");
            string bundleDir = Path.Combine(destRoot, "HuaJianPlugin.bundle");
            string winDir = Path.Combine(bundleDir, "Contents", "Windows");

            // 若已安装过，先整体删除旧目录再写入（文件被 AutoCAD 占用时会在此报错）
            if (Directory.Exists(bundleDir))
                Directory.Delete(bundleDir, true);

            Directory.CreateDirectory(winDir);
            WriteResource(asm, pkgRes, Path.Combine(bundleDir, "PackageContents.xml"));
            WriteResource(asm, dllRes, Path.Combine(winDir, "HuaJianPlugin.dll"));
        }

        private static void WriteResource(Assembly asm, string resName, string path)
        {
            using (Stream s = asm.GetManifestResourceStream(resName))
            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                byte[] buf = new byte[65536];
                int n;
                while ((n = s.Read(buf, 0, buf.Length)) > 0)
                    fs.Write(buf, 0, n);
            }
        }
    }
}
