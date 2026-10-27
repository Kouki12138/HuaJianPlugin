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
            sb.Append("  齿根圆弧 R" + F(SplineGeometry.ResolveFilletRadius(p), 3));
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
            public double thetaT;
            public double thetaRoot;
            public double rootSweep;
            public double rootCx, rootCy;
            public double rootArcStart, rootArcSweep;
            public double rootBulge;
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

                double phim = pitch / 2.0;
                double ux = Math.Cos(phim);
                double uy = Math.Sin(phim);
                double rMax = MaxRadiusAtStart(rb, c, ux, uy, rst);

                double R;
                double rt;
                if (p.MinRootFillet > 0.0)
                {
                    R = p.MinRootFillet;
                    rt = SolveTangencyForRadius(rb, ra, c, R, ux, uy);
                    if (rt > rst + 1.0e-9 || R > rMax)
                    {
                        R = rMax;
                        rt = rst;
                    }
                }
                else
                {
                    rt = SolveTangencyBottomAtRoot(rb, ra, rf, c, ux, uy);
                    R = AutoRadius(rb, c, ux, uy, rf, rt);
                    if (rt > rst + 1.0e-9 || R > rMax)
                    {
                        R = rMax;
                        rt = rst;
                    }
                }

                if (rt <= rb || rt >= ra)
                    throw new InvalidOperationException("齿根圆弧构造失败，请检查参数。");

                double th = c - InvAt(rb, rt);
                double al = Math.Acos(Math.Min(1.0, rb / rt));
                double tau = th - al;
                double nx = -Math.Sin(tau);
                double ny = Math.Cos(tau);
                double px = rt * Math.Cos(th);
                double py = rt * Math.Sin(th);
                double rho = px * ux + py * uy + R * (nx * ux + ny * uy);
                double cx = rho * ux;
                double cy = rho * uy;
                double thetaL = pitch - th;
                double plx = rt * Math.Cos(thetaL);
                double ply = rt * Math.Sin(thetaL);

                g.useFillet = true;
                g.rFlank = rt;
                g.Rfil = R;
                g.thetaT = th;
                g.rootCx = cx;
                g.rootCy = cy;
                double aP = Math.Atan2(py - cy, px - cx);
                double aL = Math.Atan2(ply - cy, plx - cx);
                double sweep = aL - aP;
                while (sweep <= -Math.PI) sweep += 2.0 * Math.PI;
                while (sweep > Math.PI) sweep -= 2.0 * Math.PI;
                g.rootBulge = Math.Tan(sweep / 4.0);
                g.rootArcStart = NormPositive(aL);
                g.rootArcSweep = NormPositive(aP - aL);
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
                g.rootBulge = Math.Tan(g.rootSweep / 4.0);
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
                        pl.AddVertexAt(pl.NumberOfVertices, Pt(g.rFlank, -g.thetaT + rot), 0.0, 0.0, 0.0);
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
                    pl.AddVertexAt(pl.NumberOfVertices, Pt(g.rFlank, -g.thetaT + rot + g.pitch), g.rootBulge, 0.0, 0.0);
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
                    pl.AddVertexAt(pl.NumberOfVertices, Pt(g.rf, nextTheta), g.rootBulge, 0.0, 0.0);
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
                    Arc rootArc = new Arc(new Point3d(g.rootCx, g.rootCy, 0.0), g.Rfil,
                        g.rootArcStart, g.rootArcStart + g.rootArcSweep);
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

        private static double MaxRadiusAtStart(double rb, double c, double ux, double uy, double r)
        {
            double th = c - InvAt(rb, r);
            double al = Math.Acos(Math.Min(1.0, rb / r));
            double tau = th - al;
            double px = r * Math.Cos(th);
            double py = r * Math.Sin(th);
            double nx = -Math.Sin(tau);
            double ny = Math.Cos(tau);
            double num = px * uy - py * ux;
            double den = nx * uy - ny * ux;
            if (Math.Abs(den) < 1.0e-12)
                throw new InvalidOperationException("无法计算齿根圆弧半径。");
            double R = -num / den;
            if (R <= 0.0)
                throw new InvalidOperationException("渐开线起始圆位置无法构造齿根圆弧。");
            return R;
        }

        private static double SolveTangencyBottomAtRoot(double rb, double ra, double rf, double c, double ux, double uy)
        {
            return ScanRoot(rb, ra, delegate(double r) {
                return AutoEquation(rb, rf, c, ux, uy, r);
            }, "无法求解齿根圆弧，请检查渐开线起始圆参数。");
        }

        private static double SolveTangencyForRadius(double rb, double ra, double c, double R, double ux, double uy)
        {
            return ScanRoot(rb, ra, delegate(double r) {
                return GivenRadiusEquation(rb, c, R, ux, uy, r);
            }, "无法求解齿根圆弧，请检查齿根圆弧半径。");
        }

        private static double ScanRoot(double rb, double ra, Func<double, double> fn, string error)
        {
            int steps = 600;
            double prevR = rb * (1.0 + 1.0e-9);
            double prevV = fn(prevR);
            for (int i = 1; i <= steps; i++)
            {
                double r = rb + (ra - rb) * i / steps;
                double v = fn(r);
                if (Math.Abs(v) < 1.0e-14) return r;
                if (prevV * v < 0.0)
                {
                    double lo = prevR;
                    double hi = r;
                    for (int j = 0; j < 100; j++)
                    {
                        double mid = 0.5 * (lo + hi);
                        double mv = fn(mid);
                        if (fn(lo) * mv <= 0.0) hi = mid;
                        else lo = mid;
                    }
                    return 0.5 * (lo + hi);
                }
                prevR = r;
                prevV = v;
            }
            throw new InvalidOperationException(error);
        }

        private static double AutoEquation(double rb, double rf, double c, double ux, double uy, double r)
        {
            double th = c - InvAt(rb, r);
            double al = Math.Acos(Math.Min(1.0, rb / r));
            double tau = th - al;
            double px = r * Math.Cos(th);
            double py = r * Math.Sin(th);
            double nx = -Math.Sin(tau);
            double ny = Math.Cos(tau);
            double ax = px - rf * ux;
            double ay = py - rf * uy;
            double bx = nx - ux;
            double by = ny - uy;
            return ax * by - ay * bx;
        }

        private static double GivenRadiusEquation(double rb, double c, double R, double ux, double uy, double r)
        {
            double th = c - InvAt(rb, r);
            double al = Math.Acos(Math.Min(1.0, rb / r));
            double tau = th - al;
            double px = r * Math.Cos(th);
            double py = r * Math.Sin(th);
            double nx = -Math.Sin(tau);
            double ny = Math.Cos(tau);
            double ax = px + R * nx;
            double ay = py + R * ny;
            return ax * uy - ay * ux;
        }

        private static double AutoRadius(double rb, double c, double ux, double uy, double rf, double rt)
        {
            double th = c - InvAt(rb, rt);
            double al = Math.Acos(Math.Min(1.0, rb / rt));
            double tau = th - al;
            double px = rt * Math.Cos(th);
            double py = rt * Math.Sin(th);
            double nx = -Math.Sin(tau);
            double ny = Math.Cos(tau);
            double den = nx * ux + ny * uy - 1.0;
            if (Math.Abs(den) < 1.0e-12)
                throw new InvalidOperationException("无法计算自动齿根圆弧半径。");
            double R = (rf - px * ux - py * uy) / den;
            if (R <= 0.0)
                throw new InvalidOperationException("自动齿根圆弧半径异常，请检查参数。");
            return R;
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

            Controls.Add(MakeLabel("齿根圆弧半径", labelX, y));
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

            Label hint = MakeLabel("渐开线起始圆填 0 = 不启用；齿根圆弧填 0 = 自动（底部与小径相切）。", labelX, y);
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
