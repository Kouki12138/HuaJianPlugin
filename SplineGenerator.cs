// 花键参数生成器 - AutoCAD 2024 .NET 插件
// 命令: HUAJIAN
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace SplineTool
{
    public class SplineCommands : IExtensionApplication
    {
        public void Initialize()
        {
        }

        public void Terminate()
        {
        }

        [CommandMethod("HUAJIAN")]
        public void DrawSpline()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;

            SplineParamsDialog dlg = new SplineParamsDialog();
            if (dlg.ShowDialog() != DialogResult.OK) return;
            SplineParams p = dlg.Result;

            PromptPointResult ppr = ed.GetPoint("\n指定花键中心点: ");
            if (ppr.Status != PromptStatus.OK) return;
            Point3d center = ppr.Value;

            try
            {
                using (doc.LockDocument())
                using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                {
                    Database db = doc.Database;
                    BlockTableRecord btr = tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite) as BlockTableRecord;
                    LinetypeTable lt = tr.GetObject(db.LinetypeTableId, OpenMode.ForRead) as LinetypeTable;
                    bool hasCenter = lt.Has("CENTER");

                    if (p.OutputMode == 1)
                    {
                        List<Entity> parts = SplineGeometry.BuildExternalProfileSplines(p);
                        foreach (Entity ent in parts)
                        {
                            ent.TransformBy(Matrix3d.Displacement(center.GetAsVector()));
                            btr.AppendEntity(ent);
                            tr.AddNewlyCreatedDBObject(ent, true);
                        }
                    }
                    else
                    {
                        Polyline profile = SplineGeometry.BuildExternalProfile(p);
                        profile.TransformBy(Matrix3d.Displacement(center.GetAsVector()));
                        btr.AppendEntity(profile);
                        tr.AddNewlyCreatedDBObject(profile, true);
                    }

                    Circle pc = new Circle(center, Vector3d.ZAxis, p.PitchDiameter / 2.0);
                    pc.ColorIndex = 8;
                    if (hasCenter) pc.Linetype = "CENTER";
                    btr.AppendEntity(pc);
                    tr.AddNewlyCreatedDBObject(pc, true);

                    double len = p.OutsideDiameter / 2.0 + 12.0;
                    Line lx = new Line(center + new Vector3d(-len, 0, 0), center + new Vector3d(len, 0, 0));
                    Line ly = new Line(center + new Vector3d(0, -len, 0), center + new Vector3d(0, len, 0));
                    lx.ColorIndex = 8;
                    ly.ColorIndex = 8;
                    if (hasCenter)
                    {
                        lx.Linetype = "CENTER";
                        ly.Linetype = "CENTER";
                    }
                    btr.AppendEntity(lx);
                    tr.AddNewlyCreatedDBObject(lx, true);
                    btr.AppendEntity(ly);
                    tr.AddNewlyCreatedDBObject(ly, true);

                    if (p.AddNote)
                    {
                        MText note = new MText();
                        note.SetDatabaseDefaults(db);
                        note.Location = center + new Vector3d(0, p.OutsideDiameter / 2.0 + 30.0, 0);
                        note.TextHeight = 4.0;
                        note.Width = 340.0;
                        note.Contents = BuildNote(p);
                        btr.AppendEntity(note);
                        tr.AddNewlyCreatedDBObject(note, true);
                    }

                    // 水印：作者与联系方式（始终生成，独立于参数注释）
                    MText wm = new MText();
                    wm.SetDatabaseDefaults(db);
                    wm.Location = center + new Vector3d(-28.0, -(p.OutsideDiameter / 2.0 + 25.0), 0);
                    wm.TextHeight = 3.0;
                    wm.ColorIndex = 9;
                    wm.Contents = "作者：吴浩然  联系方式：18240187400";
                    btr.AppendEntity(wm);
                    tr.AddNewlyCreatedDBObject(wm, true);

                    tr.Commit();
                }
                string modeName = (p.OutputMode == 1) ? "样条曲线模式（每齿独立对象）" : "整体多段线模式";
                ed.WriteMessage("\n花键轮廓已生成：{0} 齿，分度圆 Φ{1:F3}（{2}）。", p.Teeth, p.PitchDiameter, modeName);
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n生成失败：" + ex.Message);
                MessageBox.Show("生成失败：" + ex.Message, "花键生成器", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string BuildNote(SplineParams p)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("花键（外）30° 渐开线花键  精度等级：" + p.Grade);
            sb.Append("\\P");
            sb.Append("N=" + p.Teeth + "  DP=" + F(p.DiametralPitch, 3) + "  m=" + F(p.Module, 4) + "mm  α=" + F(p.PressureAngle, 1) + "°");
            sb.Append("\\P");
            sb.Append("Dp=Φ" + F(p.PitchDiameter, 3));
            sb.Append("  Da=Φ" + F(p.OutsideDiameter, 3));
            if (!String.IsNullOrEmpty(p.ODTol)) sb.Append("(" + p.ODTol + ")");
            sb.Append("  Df=Φ" + F(p.RootDiameter, 3));
            if (!String.IsNullOrEmpty(p.RDTol)) sb.Append("(" + p.RDTol + ")");
            sb.Append("\\P");
            sb.Append("分度圆弧齿厚=" + F(p.ToothThickness, 4));
            if (!String.IsNullOrEmpty(p.ThickTol)) sb.Append("(" + p.ThickTol + ")");
            sb.Append("  最小齿根圆弧 R" + F(p.MinRootFillet, 2));
            return sb.ToString();
        }

        private static string F(double v, int d)
        {
            return v.ToString("F" + d);
        }
    }

    public class SplineParams
    {
        public double DiametralPitch;
        public double Module;
        public int Teeth;
        public double PressureAngle;
        public double PitchDiameter;
        public double OutsideDiameter;
        public double RootDiameter;
        public double ToothThickness;
        public double MinRootFillet;
        public string ODTol;
        public string RDTol;
        public string ThickTol;
        public string Grade;
        public bool AddNote;
        public int OutputMode;
    }

    public static class SplineGeometry
    {
        public static Polyline BuildExternalProfile(SplineParams p)
        {
            int N = p.Teeth;
            double alpha = p.PressureAngle * Math.PI / 180.0;
            double rp = p.PitchDiameter / 2.0;
            double ra = p.OutsideDiameter / 2.0;
            double rf = p.RootDiameter / 2.0;
            double rb = rp * Math.Cos(alpha);

            if (N < 3) throw new InvalidOperationException("齿数需大于等于 3。");
            if (!(rf < rp && rp < ra)) throw new InvalidOperationException("直径需满足：齿根圆 < 分度圆 < 齿顶圆。");
            if (rf <= rb)
                throw new InvalidOperationException("齿根圆直径(" + (2.0 * rf).ToString("F3") +
                    ")需大于基圆直径(" + (2.0 * rb).ToString("F3") + ")，当前版本暂不支持根切花键。");

            double pitch = 2.0 * Math.PI / N;
            double invP = Inv(alpha);
            double betaP = p.ToothThickness / (2.0 * rp);
            double c = betaP + invP;
            double invF = InvAt(rb, rf);
            double invA = InvAt(rb, ra);
            double thetaRoot = c - invF;
            double thetaTip = c - invA;
            if (thetaRoot <= 0.0) throw new InvalidOperationException("齿厚过大，请检查齿厚与分度圆参数。");
            double rootSweep = pitch - 2.0 * thetaRoot;
            if (rootSweep <= 0.0)
                throw new InvalidOperationException("齿根处相邻齿发生干涉，请减小齿厚或增大齿根圆直径。");

            const int segs = 24;
            Polyline pl = new Polyline();
            for (int k = 0; k < N; k++)
            {
                double rot = k * pitch;

                // 左齿廓：齿根 -> 齿顶（极角增大）
                for (int i = (k == 0 ? 0 : 1); i <= segs; i++)
                {
                    double r = rf + (ra - rf) * i / segs;
                    double theta = -c + InvAt(rb, r) + rot;
                    pl.AddVertexAt(pl.NumberOfVertices, Pt(r, theta), 0.0, 0.0, 0.0);
                }

                // 齿顶圆弧
                double bulgeTip = Math.Tan(thetaTip / 2.0);
                pl.AddVertexAt(pl.NumberOfVertices, Pt(ra, thetaTip + rot), bulgeTip, 0.0, 0.0);

                // 右齿廓：齿顶 -> 齿根（极角增大）
                for (int i = segs - 1; i >= 0; i--)
                {
                    double r = rf + (ra - rf) * i / segs;
                    double theta = c - InvAt(rb, r) + rot;
                    pl.AddVertexAt(pl.NumberOfVertices, Pt(r, theta), 0.0, 0.0, 0.0);
                }

                // 齿根圆弧 -> 下一齿左齿廓齿根
                double bulgeRoot = Math.Tan(rootSweep / 4.0);
                double nextTheta = -c + invF + rot + pitch;
                pl.AddVertexAt(pl.NumberOfVertices, Pt(rf, nextTheta), bulgeRoot, 0.0, 0.0);
            }
            pl.Closed = true;
            return pl;
        }

        public static List<Entity> BuildExternalProfileSplines(SplineParams p)
        {
            int N = p.Teeth;
            double alpha = p.PressureAngle * Math.PI / 180.0;
            double rp = p.PitchDiameter / 2.0;
            double ra = p.OutsideDiameter / 2.0;
            double rf = p.RootDiameter / 2.0;
            double rb = rp * Math.Cos(alpha);

            if (N < 3) throw new InvalidOperationException("齿数需大于等于 3。");
            if (!(rf < rp && rp < ra)) throw new InvalidOperationException("直径需满足：齿根圆 < 分度圆 < 齿顶圆。");
            if (rf <= rb)
                throw new InvalidOperationException("齿根圆直径(" + (2.0 * rf).ToString("F3") +
                    ")需大于基圆直径(" + (2.0 * rb).ToString("F3") + ")，当前版本暂不支持根切花键。");

            double pitch = 2.0 * Math.PI / N;
            double invP = Inv(alpha);
            double betaP = p.ToothThickness / (2.0 * rp);
            double c = betaP + invP;
            double invF = InvAt(rb, rf);
            double invA = InvAt(rb, ra);
            double thetaRoot = c - invF;
            double thetaTip = c - invA;
            if (thetaRoot <= 0.0) throw new InvalidOperationException("齿厚过大，请检查齿厚与分度圆参数。");
            double rootSweep = pitch - 2.0 * thetaRoot;
            if (rootSweep <= 0.0)
                throw new InvalidOperationException("齿根处相邻齿发生干涉，请减小齿厚或增大齿根圆直径。");

            const int fit = 20; // 每条齿廓样条的拟合点数
            List<Entity> parts = new List<Entity>();
            for (int k = 0; k < N; k++)
            {
                double rot = k * pitch;

                // 左齿廓样条：齿根 -> 齿顶
                Point3dCollection leftPts = new Point3dCollection();
                for (int i = 0; i <= fit; i++)
                {
                    double r = rf + (ra - rf) * i / fit;
                    leftPts.Add(P3(r, -c + InvAt(rb, r) + rot));
                }
                Spline left = new Spline(leftPts, 3, 0.0);
                parts.Add(left);

                // 右齿廓样条：齿根 -> 齿顶
                Point3dCollection rightPts = new Point3dCollection();
                for (int i = 0; i <= fit; i++)
                {
                    double r = rf + (ra - rf) * i / fit;
                    rightPts.Add(P3(r, c - InvAt(rb, r) + rot));
                }
                Spline right = new Spline(rightPts, 3, 0.0);
                parts.Add(right);

                // 齿顶圆弧（真圆弧）
                Arc tipArc = new Arc(Point3d.Origin, ra, -thetaTip + rot, thetaTip + rot);
                parts.Add(tipArc);

                // 齿根圆弧（真圆弧，连接本齿右齿根与下一齿左齿根）
                Arc rootArc = new Arc(Point3d.Origin, rf, thetaRoot + rot, -thetaRoot + rot + pitch);
                parts.Add(rootArc);
            }
            return parts;
        }

        private static double Inv(double alpha)
        {
            return Math.Tan(alpha) - alpha;
        }

        private static double InvAt(double rb, double r)
        {
            double t = Math.Sqrt(Math.Max(r * r / (rb * rb) - 1.0, 0.0));
            return t - Math.Atan(t);
        }

        private static Point2d Pt(double r, double theta)
        {
            return new Point2d(r * Math.Cos(theta), r * Math.Sin(theta));
        }

        private static Point3d P3(double r, double theta)
        {
            return new Point3d(r * Math.Cos(theta), r * Math.Sin(theta), 0.0);
        }
    }

    public class SplineParamsDialog : Form
    {
        private NumericUpDown dp;
        private NumericUpDown module;
        private NumericUpDown teeth;
        private NumericUpDown pressure;
        private NumericUpDown pd;
        private NumericUpDown od;
        private NumericUpDown rd;
        private NumericUpDown thick;
        private NumericUpDown fillet;
        private TextBox odTol;
        private TextBox rdTol;
        private TextBox thickTol;
        private TextBox grade;
        private CheckBox addNote;
        private ComboBox outputMode;
        private bool syncing;

        public SplineParams Result { get; private set; }

        public SplineParamsDialog()
        {
            Text = "花键参数生成器";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(430, 512);
            Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            DoubleBuffered = true;

            int y = 18;
            int labelX = 18;
            int ctrlX = 150;
            int ctrlW = 110;
            int tolX = 268;
            int tolW = 142;

            Controls.Add(MakeLabel("径节 (DP)", labelX, y));
            dp = MakeNumeric(ctrlX, y, ctrlW, 16m, 0, 1m, 100000m, 1m);
            Controls.Add(dp);
            y += 32;

            Controls.Add(MakeLabel("模数 (mm)", labelX, y));
            module = MakeNumeric(ctrlX, y, ctrlW, 1.5875m, 4, 0.0001m, 1000m, 0.0001m);
            Controls.Add(module);
            y += 32;

            Controls.Add(MakeLabel("齿数", labelX, y));
            teeth = MakeNumeric(ctrlX, y, ctrlW, 112m, 0, 3m, 100000m, 1m);
            Controls.Add(teeth);
            y += 32;

            Controls.Add(MakeLabel("压力角 (°)", labelX, y));
            pressure = MakeNumeric(ctrlX, y, ctrlW, 30m, 1, 5m, 60m, 0.5m);
            Controls.Add(pressure);
            y += 32;

            Controls.Add(MakeLabel("分度圆直径", labelX, y));
            pd = MakeNumeric(ctrlX, y, ctrlW, 177.8m, 3, 0.001m, 100000m, 0.001m);
            Controls.Add(pd);
            y += 32;

            Controls.Add(MakeLabel("齿顶圆直径", labelX, y));
            od = MakeNumeric(ctrlX, y, ctrlW, 179.3m, 3, 0.001m, 100000m, 0.001m);
            odTol = MakeTextBox(tolX, y, tolW, "0 / -0.25");
            Controls.Add(od);
            Controls.Add(odTol);
            y += 32;

            Controls.Add(MakeLabel("齿根圆直径", labelX, y));
            rd = MakeNumeric(ctrlX, y, ctrlW, 174.94m, 3, 0.001m, 100000m, 0.001m);
            rdTol = MakeTextBox(tolX, y, tolW, "0 / -0.4");
            Controls.Add(rd);
            Controls.Add(rdTol);
            y += 32;

            Controls.Add(MakeLabel("分度圆弧齿厚", labelX, y));
            thick = MakeNumeric(ctrlX, y, ctrlW, 2.494m, 4, 0.0001m, 100000m, 0.0001m);
            thickTol = MakeTextBox(tolX, y, tolW, "-0.03 / -0.0825");
            Controls.Add(thick);
            Controls.Add(thickTol);
            y += 32;

            Controls.Add(MakeLabel("最小齿根圆弧", labelX, y));
            fillet = MakeNumeric(ctrlX, y, ctrlW, 0.6m, 2, 0m, 100000m, 0.1m);
            Controls.Add(fillet);
            y += 32;

            Controls.Add(MakeLabel("精度等级", labelX, y));
            grade = MakeTextBox(ctrlX, y, ctrlW + tolW, "5级（ANSI B92.1b-1996）");
            Controls.Add(grade);
            y += 34;

            Controls.Add(MakeLabel("轮廓输出", labelX, y));
            outputMode = MakeCombo(ctrlX, y, 260);
            outputMode.Items.Add("整体多段线（1 个整体对象）");
            outputMode.Items.Add("样条曲线（每齿独立：2 样条 + 齿顶弧 + 齿根弧）");
            outputMode.SelectedIndex = 0;
            Controls.Add(outputMode);
            y += 34;

            addNote = new CheckBox();
            addNote.Text = "在图形旁生成参数注释文字";
            addNote.Checked = true;
            addNote.Location = new Point(labelX, y);
            addNote.AutoSize = true;
            Controls.Add(addNote);
            y += 28;

            Label hint = MakeLabel("点击【确定】后，在图形中拾取花键中心点。", labelX, y);
            hint.ForeColor = Color.Gray;
            Controls.Add(hint);
            y += 30;

            Button ok = new Button();
            ok.Text = "确定";
            ok.Location = new Point(ctrlX, y);
            ok.Size = new Size(90, 28);
            ok.Click += OnOk;
            Controls.Add(ok);

            Button cancel = new Button();
            cancel.Text = "取消";
            cancel.Location = new Point(ctrlX + 100, y);
            cancel.Size = new Size(90, 28);
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;

            // 界面水印：底部版权信息
            Label wmFooter = new Label();
            wmFooter.Text = "作者：吴浩然　联系方式：18240187400";
            wmFooter.Location = new Point(0, ClientSize.Height - 24);
            wmFooter.Size = new Size(ClientSize.Width, 20);
            wmFooter.TextAlign = ContentAlignment.MiddleCenter;
            wmFooter.ForeColor = Color.FromArgb(140, 140, 140);
            wmFooter.Font = new System.Drawing.Font("Microsoft YaHei UI", 8F, FontStyle.Italic);
            Controls.Add(wmFooter);

            dp.ValueChanged += OnDpChanged;
            module.ValueChanged += OnModuleChanged;
            teeth.ValueChanged += OnTeethChanged;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // 背景斜向水印
            Graphics g = e.Graphics;
            string wm = "作者：吴浩然　18240187400";
            using (System.Drawing.Font f = new System.Drawing.Font(this.Font.FontFamily, 24F, FontStyle.Bold, GraphicsUnit.Point))
            {
                SizeF sz = g.MeasureString(wm, f);
                float cx = ClientSize.Width / 2f;
                float cy = ClientSize.Height / 2f - 10f;
                g.TranslateTransform(cx, cy);
                g.RotateTransform(-25f);
                g.TranslateTransform(-sz.Width / 2f, -sz.Height / 2f);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(34, 120, 120, 120)))
                {
                    g.DrawString(wm, f, b, 0f, 0f);
                }
                g.ResetTransform();
            }
        }

        private void OnDpChanged(object sender, EventArgs e)
        {
            if (syncing) return;
            syncing = true;
            try
            {
                if (dp.Value > 0m) module.Value = Math.Round(25.4m / dp.Value, 4);
                SyncFromModule();
            }
            finally
            {
                syncing = false;
            }
        }

        private void OnModuleChanged(object sender, EventArgs e)
        {
            if (syncing) return;
            syncing = true;
            try
            {
                SyncFromModule();
            }
            finally
            {
                syncing = false;
            }
        }

        private void OnTeethChanged(object sender, EventArgs e)
        {
            if (syncing) return;
            syncing = true;
            try
            {
                pd.Value = Math.Round(module.Value * teeth.Value, 3);
            }
            finally
            {
                syncing = false;
            }
        }

        private void SyncFromModule()
        {
            if (module.Value > 0m)
            {
                dp.Value = Math.Round(25.4m / module.Value, 3);
                pd.Value = Math.Round(module.Value * teeth.Value, 3);
            }
        }

        private void OnOk(object sender, EventArgs e)
        {
            double m = (double)module.Value;
            double n = (double)teeth.Value;
            double d = (double)pd.Value;
            double computed = m * n;
            if (Math.Abs(d - computed) > 0.1)
            {
                DialogResult r = MessageBox.Show(
                    "分度圆直径与 模数×齿数 不一致（应为 " + computed.ToString("F3") + "）。是否仍按当前输入继续？",
                    "花键生成器", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r != DialogResult.Yes) return;
            }
            if (!((double)rd.Value < (double)pd.Value && (double)pd.Value < (double)od.Value))
            {
                MessageBox.Show("直径需满足：齿根圆 < 分度圆 < 齿顶圆。", "花键生成器",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if ((double)thick.Value <= 0.0)
            {
                MessageBox.Show("分度圆弧齿厚需大于 0。", "花键生成器",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SplineParams p = new SplineParams();
            p.DiametralPitch = (double)dp.Value;
            p.Module = (double)module.Value;
            p.Teeth = (int)teeth.Value;
            p.PressureAngle = (double)pressure.Value;
            p.PitchDiameter = (double)pd.Value;
            p.OutsideDiameter = (double)od.Value;
            p.RootDiameter = (double)rd.Value;
            p.ToothThickness = (double)thick.Value;
            p.MinRootFillet = (double)fillet.Value;
            p.ODTol = odTol.Text.Trim();
            p.RDTol = rdTol.Text.Trim();
            p.ThickTol = thickTol.Text.Trim();
            p.Grade = grade.Text.Trim();
            p.AddNote = addNote.Checked;
            p.OutputMode = outputMode.SelectedIndex;
            Result = p;
            DialogResult = DialogResult.OK;
        }

        private static Label MakeLabel(string text, int x, int y)
        {
            Label l = new Label();
            l.Text = text;
            l.Location = new Point(x, y + 3);
            l.AutoSize = true;
            return l;
        }

        private static NumericUpDown MakeNumeric(int x, int y, int w, decimal value, int decimals,
            decimal min, decimal max, decimal inc)
        {
            NumericUpDown n = new NumericUpDown();
            n.Location = new Point(x, y);
            n.Width = w;
            n.DecimalPlaces = decimals;
            n.Minimum = min;
            n.Maximum = max;
            n.Increment = inc;
            n.Value = value;
            return n;
        }

        private static TextBox MakeTextBox(int x, int y, int w, string text)
        {
            TextBox t = new TextBox();
            t.Location = new Point(x, y);
            t.Width = w;
            t.Text = text;
            return t;
        }

        private static ComboBox MakeCombo(int x, int y, int w)
        {
            ComboBox c = new ComboBox();
            c.DropDownStyle = ComboBoxStyle.DropDownList;
            c.Location = new Point(x, y);
            c.Width = w;
            return c;
        }
    }
}
