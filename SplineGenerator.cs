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
            sb.Append("花键（外）" + F(p.PressureAngle, 1) + "° 渐开线花键  精度等级：" + p.Grade);
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
            if (p.InvoluteStartDiameter > 0.0)
                sb.Append("  渐开线起始圆 Φ" + F(p.InvoluteStartDiameter, 3));
            sb.Append("  齿根圆角 R" + F(SplineGeometry.ResolveFilletRadius(p), 3));
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
        public double InvoluteStartDiameter;
        public string ODTol;
        public string RDTol;
        public string ThickTol;
        public string Grade;
        public bool AddNote;
        public int OutputMode;
    }

    public static class SplineGeometry
    {
        private class Geom
        {
            public int N;
            public double rp, ra, rf, rb, pitch, c, thetaTip;
            public bool useFillet;
            public double rFlank;
            public double Rfil;
            public double thetaRoot;
            public double rootSweep;
            public double qAng;
            public Point2d pL, pR, cL, cR, qL, qR;
            public double bulgeFilL, bulgeFilR, bulgeRoot;
        }

        public static double ResolveFilletRadius(SplineParams p)
        {
            Geom g = Prepare(p);
            return g.useFillet ? g.Rfil : p.MinRootFillet;
        }

        private static Geom Prepare(SplineParams p)
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
                    ")需大于基圆直径(" + (2.0 * rb).ToString("F3") + ")。");

            double pitch = 2.0 * Math.PI / N;
            double invP = Inv(alpha);
            double betaP = p.ToothThickness / (2.0 * rp);
            double c = betaP + invP;
            double thetaTip = c - InvAt(rb, ra);
            if (betaP <= 0.0) throw new InvalidOperationException("分度圆弧齿厚需大于 0。");
            if (thetaTip <= 0.0) throw new InvalidOperationException("齿顶厚度过小，请检查齿厚或齿顶圆直径。");

            Geom g = new Geom();
            g.N = N;
            g.rp = rp;
            g.ra = ra;
            g.rf = rf;
            g.rb = rb;
            g.pitch = pitch;
            g.c = c;
            g.thetaTip = thetaTip;

            if (p.InvoluteStartDiameter > 0.0)
            {
                double rst = p.InvoluteStartDiameter / 2.0;
                if (rst <= rb)
                    throw new InvalidOperationException("渐开线起始圆直径需大于基圆直径(" + (2.0 * rb).ToString("F3") + ")。");
                if (rst <= rf)
                    throw new InvalidOperationException("渐开线起始圆直径需大于齿根圆直径。");
                if (rst >= ra)
                    throw new InvalidOperationException("渐开线起始圆直径需小于齿顶圆直径。");

                double rMax = FilletRadiusAt(rb, rf, c, rst);
                double R = (p.MinRootFillet > 0.0) ? p.MinRootFillet : rMax;
                double rt = SolveTangencyRadius(rb, rf, c, R, rst);
                if (rt > rst + 1.0e-9 || FilletError(rb, rf, c, R, rst) < 0.0)
                {
                    R = rMax;
                    rt = rst;
                }

                double th = c - InvAt(rb, rt);
                Point2d pr = new Point2d(rt * Math.Cos(th), rt * Math.Sin(th));
                double al = Math.Acos(Math.Min(1.0, rb / rt));
                double tau = th + al;
                Point2d cr = new Point2d(pr.X + R * Math.Sin(tau), pr.Y - R * Math.Cos(tau));
                double cAng = Math.Atan2(cr.Y, cr.X);
                Point2d qr = new Point2d(rf * Math.Cos(cAng), rf * Math.Sin(cAng));

                g.useFillet = true;
                g.rFlank = rt;
                g.Rfil = R;
                g.pR = pr;
                g.cR = cr;
                g.qR = qr;
                g.pL = new Point2d(pr.X, -pr.Y);
                g.cL = new Point2d(cr.X, -cr.Y);
                g.qL = new Point2d(qr.X, -qr.Y);
                g.qAng = cAng;
                g.bulgeFilR = ArcBulge(g.pR, g.qR, g.cR);
                g.bulgeFilL = ArcBulge(g.qL, g.pL, g.cL);
                g.rootSweep = pitch - 2.0 * cAng;
                if (g.rootSweep <= 0.0)
                    throw new InvalidOperationException("齿根圆角过大，相邻齿根圆角干涉，请减小圆角半径。");
                g.bulgeRoot = Math.Tan(g.rootSweep / 4.0);
            }
            else
            {
                g.useFillet = false;
                g.rFlank = rf;
                g.thetaRoot = c - InvAt(rb, rf);
                if (g.thetaRoot <= 0.0) throw new InvalidOperationException("齿厚过大，请检查齿厚与分度圆参数。");
                g.rootSweep = pitch - 2.0 * g.thetaRoot;
                if (g.rootSweep <= 0.0)
                    throw new InvalidOperationException("齿根处相邻齿发生干涉，请减小齿厚或增大齿根圆直径。");
                g.bulgeRoot = Math.Tan(g.rootSweep / 4.0);
            }
            return g;
        }

        public static Polyline BuildExternalProfile(SplineParams p)
        {
            Geom g = Prepare(p);
            const int segs = 24;
            Polyline pl = new Polyline();
            for (int k = 0; k < g.N; k++)
            {
                double rot = k * g.pitch;
                if (g.useFillet)
                {
                    if (k == 0)
                        pl.AddVertexAt(pl.NumberOfVertices, Rot(g.qL, rot), 0.0, 0.0, 0.0);
                    pl.AddVertexAt(pl.NumberOfVertices, Rot(g.pL, rot), g.bulgeFilL, 0.0, 0.0);
                    for (int i = 1; i <= segs; i++)
                    {
                        double r = g.rFlank + (g.ra - g.rFlank) * i / segs;
                        pl.AddVertexAt(pl.NumberOfVertices, Pt(r, -g.c + InvAt(g.rb, r) + rot), 0.0, 0.0, 0.0);
                    }
                    pl.AddVertexAt(pl.NumberOfVertices, Pt(g.ra, g.thetaTip + rot), Math.Tan(g.thetaTip / 2.0), 0.0, 0.0);
                    for (int i = segs - 1; i >= 0; i--)
                    {
                        double r = g.rFlank + (g.ra - g.rFlank) * i / segs;
                        pl.AddVertexAt(pl.NumberOfVertices, Pt(r, g.c - InvAt(g.rb, r) + rot), 0.0, 0.0, 0.0);
                    }
                    pl.AddVertexAt(pl.NumberOfVertices, Rot(g.qR, rot), g.bulgeFilR, 0.0, 0.0);
                    pl.AddVertexAt(pl.NumberOfVertices, Rot(g.qL, rot + g.pitch), g.bulgeRoot, 0.0, 0.0);
                }
                else
                {
                    for (int i = (k == 0 ? 0 : 1); i <= segs; i++)
                    {
                        double r = g.rf + (g.ra - g.rf) * i / segs;
                        pl.AddVertexAt(pl.NumberOfVertices, Pt(r, -g.c + InvAt(g.rb, r) + rot), 0.0, 0.0, 0.0);
                    }
                    pl.AddVertexAt(pl.NumberOfVertices, Pt(g.ra, g.thetaTip + rot), Math.Tan(g.thetaTip / 2.0), 0.0, 0.0);
                    for (int i = segs - 1; i >= 0; i--)
                    {
                        double r = g.rf + (g.ra - g.rf) * i / segs;
                        pl.AddVertexAt(pl.NumberOfVertices, Pt(r, g.c - InvAt(g.rb, r) + rot), 0.0, 0.0, 0.0);
                    }
                    double nextTheta = -g.c + InvAt(g.rb, g.rf) + rot + g.pitch;
                    pl.AddVertexAt(pl.NumberOfVertices, Pt(g.rf, nextTheta), g.bulgeRoot, 0.0, 0.0);
                }
            }
            pl.Closed = true;
            return pl;
        }

        public static List<Entity> BuildExternalProfileSplines(SplineParams p)
        {
            Geom g = Prepare(p);
            const int fit = 20;
            List<Entity> parts = new List<Entity>();
            for (int k = 0; k < g.N; k++)
            {
                double rot = k * g.pitch;

                Point3dCollection leftPts = new Point3dCollection();
                for (int i = 0; i <= fit; i++)
                {
                    double r = g.rFlank + (g.ra - g.rFlank) * i / fit;
                    leftPts.Add(P3(r, -g.c + InvAt(g.rb, r)));
                }
                Spline left = new Spline(leftPts, 3, 0.0);
                RotateEntity(left, rot);
                parts.Add(left);

                Point3dCollection rightPts = new Point3dCollection();
                for (int i = 0; i <= fit; i++)
                {
                    double r = g.rFlank + (g.ra - g.rFlank) * i / fit;
                    rightPts.Add(P3(r, g.c - InvAt(g.rb, r)));
                }
                Spline right = new Spline(rightPts, 3, 0.0);
                RotateEntity(right, rot);
                parts.Add(right);

                Arc tipArc = new Arc(Point3d.Origin, g.ra, -g.thetaTip, g.thetaTip);
                RotateEntity(tipArc, rot);
                parts.Add(tipArc);

                if (g.useFillet)
                {
                    double als = Math.Atan2(g.qL.Y - g.cL.Y, g.qL.X - g.cL.X);
                    double alp = Math.Atan2(g.pL.Y - g.cL.Y, g.pL.X - g.cL.X);
                    double swL = NormPositive(alp - als);
                    Arc leftFil = new Arc(new Point3d(g.cL.X, g.cL.Y, 0.0), g.Rfil, als, als + swL);
                    RotateEntity(leftFil, rot);
                    parts.Add(leftFil);

                    double ars = Math.Atan2(g.pR.Y - g.cR.Y, g.pR.X - g.cR.X);
                    double arq = Math.Atan2(g.qR.Y - g.cR.Y, g.qR.X - g.cR.X);
                    double swR = NormPositive(arq - ars);
                    Arc rightFil = new Arc(new Point3d(g.cR.X, g.cR.Y, 0.0), g.Rfil, ars, ars + swR);
                    RotateEntity(rightFil, rot);
                    parts.Add(rightFil);

                    double qStart = Math.Atan2(g.qR.Y, g.qR.X);
                    Arc rootArc = new Arc(Point3d.Origin, g.rf, qStart, qStart + g.rootSweep);
                    RotateEntity(rootArc, rot);
                    parts.Add(rootArc);
                }
                else
                {
                    Arc rootArc = new Arc(Point3d.Origin, g.rf, g.thetaRoot, -g.thetaRoot + g.pitch);
                    RotateEntity(rootArc, rot);
                    parts.Add(rootArc);
                }
            }
            return parts;
        }

        private static double FilletRadiusAt(double rb, double rf, double c, double r)
        {
            double th = c - InvAt(rb, r);
            double al = Math.Acos(Math.Min(1.0, rb / r));
            double tau = th + al;
            double px = r * Math.Cos(th);
            double py = r * Math.Sin(th);
            double pn = px * Math.Sin(tau) - py * Math.Cos(tau);
            double denom = 2.0 * (pn - rf);
            if (Math.Abs(denom) < 1.0e-12)
                throw new InvalidOperationException("无法计算齿根圆角半径。");
            double R = (rf * rf - r * r) / denom;
            if (R <= 0.0)
                throw new InvalidOperationException("渐开线起始圆位置无法构造齿根圆角。");
            return R;
        }

        private static double SolveTangencyRadius(double rb, double rf, double c, double R, double hi)
        {
            double lo = rb * (1.0 + 1.0e-9);
            double fHi = FilletError(rb, rf, c, R, hi);
            if (fHi >= 0.0) return hi;
            double fLo = FilletError(rb, rf, c, R, lo);
            if (fLo >= 0.0) return lo;
            for (int i = 0; i < 200; i++)
            {
                double mid = 0.5 * (lo + hi);
                double fm = FilletError(rb, rf, c, R, mid);
                if (fm < 0.0) lo = mid;
                else hi = mid;
            }
            return 0.5 * (lo + hi);
        }

        private static double FilletError(double rb, double rf, double c, double R, double r)
        {
            double th = c - InvAt(rb, r);
            double al = Math.Acos(Math.Min(1.0, rb / r));
            double tau = th + al;
            double cx = r * Math.Cos(th) + R * Math.Sin(tau);
            double cy = r * Math.Sin(th) - R * Math.Cos(tau);
            return Math.Sqrt(cx * cx + cy * cy) - (rf + R);
        }

        private static double ArcBulge(Point2d a, Point2d b, Point2d center)
        {
            double a1 = Math.Atan2(a.Y - center.Y, a.X - center.X);
            double a2 = Math.Atan2(b.Y - center.Y, b.X - center.X);
            double sweep = a2 - a1;
            while (sweep <= -Math.PI) sweep += 2.0 * Math.PI;
            while (sweep > Math.PI) sweep -= 2.0 * Math.PI;
            return Math.Tan(sweep / 4.0);
        }

        private static double NormPositive(double a)
        {
            double tw = 2.0 * Math.PI;
            a = a % tw;
            if (a < 0.0) a += tw;
            return a;
        }

        private static Point2d Rot(Point2d p, double angle)
        {
            double ca = Math.Cos(angle);
            double sa = Math.Sin(angle);
            return new Point2d(p.X * ca - p.Y * sa, p.X * sa + p.Y * ca);
        }

        private static void RotateEntity(Entity ent, double angle)
        {
            if (Math.Abs(angle) > 1.0e-12)
                ent.TransformBy(Matrix3d.Rotation(angle, Vector3d.ZAxis, Point3d.Origin));
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
        private NumericUpDown startDia;
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
            ClientSize = new Size(430, 544);
            Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            DoubleBuffered = true;

            int y = 18;
            int labelX = 18;
            int ctrlX = 150;
            int ctrlW = 110;
            int tolX = 268;
            int tolW = 142;

            Controls.Add(MakeLabel("径节 (DP)", labelX, y));
            dp = MakeNumeric(ctrlX, y, ctrlW, 10m, 0, 1m, 100000m, 1m);
            Controls.Add(dp);
            y += 32;

            Controls.Add(MakeLabel("模数 (mm)", labelX, y));
            module = MakeNumeric(ctrlX, y, ctrlW, 2.54m, 4, 0.0001m, 1000m, 0.0001m);
            Controls.Add(module);
            y += 32;

            Controls.Add(MakeLabel("齿数", labelX, y));
            teeth = MakeNumeric(ctrlX, y, ctrlW, 65m, 0, 3m, 100000m, 1m);
            Controls.Add(teeth);
            y += 32;

            Controls.Add(MakeLabel("压力角 (°)", labelX, y));
            pressure = MakeNumeric(ctrlX, y, ctrlW, 14.5m, 1, 5m, 60m, 0.5m);
            Controls.Add(pressure);
            y += 32;

            Controls.Add(MakeLabel("分度圆直径", labelX, y));
            pd = MakeNumeric(ctrlX, y, ctrlW, 165.1m, 3, 0.001m, 100000m, 0.001m);
            Controls.Add(pd);
            y += 32;

            Controls.Add(MakeLabel("齿顶圆直径", labelX, y));
            od = MakeNumeric(ctrlX, y, ctrlW, 166.42m, 3, 0.001m, 100000m, 0.001m);
            odTol = MakeTextBox(tolX, y, tolW, "0 / -0.127");
            Controls.Add(od);
            Controls.Add(odTol);
            y += 32;

            Controls.Add(MakeLabel("齿根圆直径", labelX, y));
            rd = MakeNumeric(ctrlX, y, ctrlW, 160.530m, 3, 0.001m, 100000m, 0.001m);
            rdTol = MakeTextBox(tolX, y, tolW, "0 / -0.254");
            Controls.Add(rd);
            Controls.Add(rdTol);
            y += 32;

            Controls.Add(MakeLabel("分度圆弧齿厚", labelX, y));
            thick = MakeNumeric(ctrlX, y, ctrlW, 3.99m, 4, 0.0001m, 100000m, 0.0001m);
            thickTol = MakeTextBox(tolX, y, tolW, "-0.147 / -0.222");
            Controls.Add(thick);
            Controls.Add(thickTol);
            y += 32;

            Controls.Add(MakeLabel("渐开线起始圆", labelX, y));
            startDia = MakeNumeric(ctrlX, y, ctrlW, 163.540m, 3, 0m, 100000m, 0.001m);
            Controls.Add(startDia);
            y += 32;

            Controls.Add(MakeLabel("齿根圆角半径", labelX, y));
            fillet = MakeNumeric(ctrlX, y, ctrlW, 0m, 2, 0m, 100000m, 0.1m);
            Controls.Add(fillet);
            y += 32;

            Controls.Add(MakeLabel("精度等级", labelX, y));
            grade = MakeTextBox(ctrlX, y, ctrlW + tolW, "未注");
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

            Label hint = MakeLabel("渐开线起始圆填 0 = 不启用；齿根圆角填 0 = 按起始圆自动相切。", labelX, y);
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
            if ((double)startDia.Value > 0.0 &&
                !((double)rd.Value < (double)startDia.Value && (double)startDia.Value < (double)od.Value))
            {
                MessageBox.Show("渐开线起始圆直径需介于齿根圆与齿顶圆之间。", "花键生成器",
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
            p.InvoluteStartDiameter = (double)startDia.Value;
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
