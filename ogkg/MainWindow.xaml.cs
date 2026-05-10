using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ClosestPoints
{
    public partial class MainWindow : Window
    {
        List<PointEx> points = new List<PointEx>();
        Random rand = new Random();
        bool isDemoMode = true;

        public MainWindow()
        {
            InitializeComponent();
        }

        public class PointEx
        {
            public double X, Y;
            public int Set;
            public PointEx(double x, double y, int set) { X = x; Y = y; Set = set; }
        }

        // ── Mode switch ──────────────────────────────────────────────────────────

        private void ModeDemo_Checked(object sender, RoutedEventArgs e)
        {
            isDemoMode = true;
            if (ModeLabel != null)
                ModeLabel.Text = "Режим: Демо — ручний ввід мишею (≤100 точок)";
        }

        private void ModeAuto_Checked(object sender, RoutedEventArgs e)
        {
            isDemoMode = false;
            if (ModeLabel != null)
                ModeLabel.Text = "Режим: Авто — генерація (до 10 000 точок)";
        }

        // ── Separator line on canvas ─────────────────────────────────────────────

        private void MyCanvas_Loaded(object sender, RoutedEventArgs e) => DrawSeparator();

        private void DrawSeparator()
        {
            RemoveSeparator();
            double w = MyCanvas.ActualWidth;
            double h = MyCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;

            var sep = new Line
            {
                X1 = w / 2, Y1 = 0, X2 = w / 2, Y2 = h,
                Stroke = new SolidColorBrush(Color.FromArgb(70, 200, 200, 200)),
                StrokeDashArray = new DoubleCollection { 6, 4 },
                StrokeThickness = 1,
                Tag = "sep"
            };
            MyCanvas.Children.Insert(0, sep);

            var lblA = new TextBlock { Text = "← Множина A", Foreground = Brushes.LimeGreen, FontSize = 12, Opacity = 0.45, Tag = "sep" };
            Canvas.SetLeft(lblA, 8); Canvas.SetTop(lblA, 4);
            MyCanvas.Children.Insert(1, lblA);

            var lblB = new TextBlock { Text = "Множина B →", Foreground = Brushes.OrangeRed, FontSize = 12, Opacity = 0.45, Tag = "sep" };
            Canvas.SetLeft(lblB, w / 2 + 8); Canvas.SetTop(lblB, 4);
            MyCanvas.Children.Insert(2, lblB);
        }

        private void RemoveSeparator()
        {
            var toRemove = MyCanvas.Children.Cast<UIElement>()
                .Where(el => (el is Line l && l.Tag is string s && s == "sep")
                          || (el is TextBlock tb && tb.Tag is string ts && ts == "sep"))
                .ToList();
            foreach (var el in toRemove) MyCanvas.Children.Remove(el);
        }

        // ── Manual input (Demo mode) ─────────────────────────────────────────────

        private void Canvas_Click(object sender, MouseButtonEventArgs e)
        {
            if (!isDemoMode)
            {
                ResultText.Text = "Ручний ввід лише в режимі «Демо». Перемкніть режим.";
                return;
            }
            if (points.Count >= 100)
            {
                ResultText.Text = "Демо: максимум 100 точок. Для більшої кількості — режим «Авто».";
                return;
            }

            var pos = e.GetPosition(MyCanvas);
            double w = MyCanvas.ActualWidth;

            // Auto-assign set by canvas half to enforce linear separability
            int set = pos.X < w / 2 ? 0 : 1;

            if (set != ModeBox.SelectedIndex)
                ResultText.Text = $"Точку додано до множини {(set == 0 ? "A" : "B")} (за позицією на канвасі).";

            var p = new PointEx(pos.X, pos.Y, set);
            points.Add(p);
            DrawPoint(p, large: true);
            UpdateCountLabel();
        }

        // ── Clear ────────────────────────────────────────────────────────────────

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            points.Clear();
            MyCanvas.Children.Clear();
            ResultText.Text = "Поле очищено.";
            DrawSeparator();
            UpdateCountLabel();
        }

        // ── Auto-generate (linearly separable) ───────────────────────────────────

        private void Generate_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(PointsCountBox.Text, out int total) || total <= 0)
            {
                ResultText.Text = "Введіть коректну кількість точок.";
                return;
            }
            if (total > 100000) total = 100000;

            MyCanvas.Children.Clear();
            points.Clear();

            double w = MyCanvas.ActualWidth > 0 ? MyCanvas.ActualWidth : 800;
            double h = MyCanvas.ActualHeight > 0 ? MyCanvas.ActualHeight : 500;
            int half = total / 2;

            for (int i = 0; i < half; i++)
            {
                // A — ліва половина, B — права: лінійна роздільність гарантована
                points.Add(new PointEx(rand.NextDouble() * (w / 2 - 30) + 10, rand.NextDouble() * (h - 20) + 10, 0));
                points.Add(new PointEx(rand.NextDouble() * (w / 2 - 30) + w / 2 + 10, rand.NextDouble() * (h - 20) + 10, 1));
            }

            const int DrawLimit = 3000;
            for (int i = 0; i < Math.Min(points.Count, DrawLimit); i++)
                DrawPoint(points[i], large: false);

            DrawSeparator();
            string note = points.Count > DrawLimit ? $" (показано {DrawLimit} з {points.Count})" : "";
            ResultText.Text = $"Згенеровано {points.Count} точок{note}.";
            UpdateCountLabel();
        }

        // ── Worst-case generation ────────────────────────────────────────────────

        // Найгірший випадок для фази смуги: всі точки потрапляють у смугу.
        // A-точки на x = w/2 - 2, B-точки на x = w/2 + 2, рівномірно по Y.
        // Відстань між множинами = 4 пікселі → смуга шириною 4 містить ВСІ n точок.
        private void WorstCase_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(PointsCountBox.Text, out int total) || total <= 0) total = 2000;
            if (total > 20000) total = 20000;

            MyCanvas.Children.Clear();
            points.Clear();

            double w = MyCanvas.ActualWidth > 0 ? MyCanvas.ActualWidth : 800;
            double h = MyCanvas.ActualHeight > 0 ? MyCanvas.ActualHeight : 500;
            int half = total / 2;

            for (int i = 0; i < half; i++)
            {
                double y = (i + 0.5) / half * (h - 20) + 10;
                points.Add(new PointEx(w / 2 - 2, y, 0));
                points.Add(new PointEx(w / 2 + 2, y, 1));
            }

            const int DrawLimit = 3000;
            for (int i = 0; i < Math.Min(points.Count, DrawLimit); i++)
                DrawPoint(points[i], large: false);

            DrawSeparator();
            ResultText.Text = $"Найгірший ввід: {points.Count} точок у смузі шириною 4px. Натисніть «Знайти пару».";
            UpdateCountLabel();
        }

        // ── Benchmark: confirm O(N log N) experimentally ──────────────────────────

        private void Benchmark_Click(object sender, RoutedEventArgs e)
        {
            int[] sizes = { 500, 1000, 2000, 5000, 10000, 30000 };
            double w = 800, h = 500;
            var sb = new StringBuilder();
            sb.AppendLine($"{"N",-8}{"Час (мс)",-14}{"N·log₂N",-14}{"мс/(N·log₂N)",-16}");
            sb.AppendLine(new string('─', 52));

            foreach (int n in sizes)
            {
                var tp = new List<PointEx>(n);
                int half = n / 2;
                for (int i = 0; i < half; i++)
                {
                    tp.Add(new PointEx(rand.NextDouble() * (w / 2 - 10) + 5, rand.NextDouble() * (h - 10) + 5, 0));
                    tp.Add(new PointEx(rand.NextDouble() * (w / 2 - 10) + w / 2 + 5, rand.NextDouble() * (h - 10) + 5, 1));
                }
                var sx = tp.OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
                var sy = tp.OrderBy(p => p.Y).ThenBy(p => p.X).ToList();

                var sw = Stopwatch.StartNew();
                FindClosestRecursive(sx, sy);
                sw.Stop();

                double ms = sw.Elapsed.TotalMilliseconds;
                double nlogn = n * Math.Log(n, 2);
                sb.AppendLine($"{n,-8}{ms,-14:F3}{nlogn,-14:F0}{(ms / nlogn * 1e6),-16:F3}");
            }
            sb.AppendLine();
            sb.AppendLine("Константа мс/(N·log₂N) має бути приблизно стала → O(N log N)");

            var bw = new Window
            {
                Title = "Benchmark — O(N log N)",
                Width = 440, Height = 280,
                Background = new SolidColorBrush(Color.FromRgb(28, 28, 28)),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };
            bw.Content = new TextBox
            {
                Text = sb.ToString(),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 13,
                Foreground = Brushes.LightGreen,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                IsReadOnly = true,
                Margin = new Thickness(14)
            };
            bw.ShowDialog();
        }

        // ── Find closest pair ────────────────────────────────────────────────────

        private void FindClosest_Click(object sender, RoutedEventArgs e)
        {
            if (points.Count(p => p.Set == 0) == 0 || points.Count(p => p.Set == 1) == 0)
            {
                ResultText.Text = "Потрібні точки обох множин A і B.";
                return;
            }

            // Перевірка лінійної роздільності: max(xA) < min(xB)
            double maxXA = points.Where(p => p.Set == 0).Max(p => p.X);
            double minXB = points.Where(p => p.Set == 1).Min(p => p.X);
            string sepWarn = maxXA >= minXB ? " ⚠ Множини не роздільні!" : "";

            // Прибираємо попередній результат, не чіпаючи роздільник
            var oldLines = MyCanvas.Children.OfType<Line>()
                .Where(l => !(l.Tag is string s && s == "sep")).ToList();
            foreach (var l in oldLines) MyCanvas.Children.Remove(l);
            var oldDots = MyCanvas.Children.OfType<Ellipse>()
                .Where(el => el.Tag is string ts && ts == "result").ToList();
            foreach (var d in oldDots) MyCanvas.Children.Remove(d);

            var sw = Stopwatch.StartNew();
            var sortedX = points.OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
            var sortedY = points.OrderBy(p => p.Y).ThenBy(p => p.X).ToList();
            var result = FindClosestRecursive(sortedX, sortedY);
            sw.Stop();

            if (result.p1 != null)
            {
                DrawResultLine(result.p1, result.p2);
                DrawResultDot(result.p1);
                DrawResultDot(result.p2);
                ResultText.Text =
                    $"Відстань: {result.dist:F4}  |  Час: {sw.Elapsed.TotalMilliseconds:F2} мс  |  N={points.Count}{sepWarn}";
            }
            else
            {
                ResultText.Text = "Не знайдено пари точок з різних множин.";
            }
        }

        // ── Core algorithm ───────────────────────────────────────────────────────

        private (PointEx p1, PointEx p2, double dist) FindClosestRecursive(
            List<PointEx> px, List<PointEx> py)
        {
            if (px.Count <= 3) return BruteForce(px);

            int mid = px.Count / 2;
            PointEx midPoint = px[mid];

            // HashSet-розбиття: коректно при однакових координатах
            var leftSet = new HashSet<PointEx>(px.GetRange(0, mid));
            var pyLeft = new List<PointEx>(mid);
            var pyRight = new List<PointEx>(px.Count - mid);
            foreach (var p in py)
            {
                if (leftSet.Contains(p)) pyLeft.Add(p);
                else pyRight.Add(p);
            }

            var resL = FindClosestRecursive(px.GetRange(0, mid), pyLeft);
            var resR = FindClosestRecursive(px.GetRange(mid, px.Count - mid), pyRight);

            (PointEx p1, PointEx p2, double dist) best;
            if (resL.p1 == null && resR.p1 == null)      best = (null, null, double.MaxValue);
            else if (resL.p1 == null)                     best = resR;
            else if (resR.p1 == null)                     best = resL;
            else best = resL.dist < resR.dist ? resL : resR;

            double delta = best.dist < double.MaxValue ? best.dist : double.MaxValue;

            // Фаза смуги: py вже відсортований по Y → O(N) для формування смуги
            var strip = new List<PointEx>();
            foreach (var p in py)
                if (delta == double.MaxValue || Math.Abs(p.X - midPoint.X) < delta)
                    strip.Add(p);

            for (int i = 0; i < strip.Count; i++)
            {
                for (int j = i + 1; j < strip.Count; j++)
                {
                    double dy = strip[j].Y - strip[i].Y;
                    if (delta < double.MaxValue && dy >= delta) break;
                    if (strip[i].Set != strip[j].Set)
                    {
                        double d = Distance(strip[i], strip[j]);
                        if (d < best.dist)
                        {
                            best = (strip[i], strip[j], d);
                            delta = d;
                        }
                    }
                }
            }
            return best;
        }

        private (PointEx p1, PointEx p2, double dist) BruteForce(List<PointEx> px)
        {
            double min = double.MaxValue;
            PointEx p1 = null, p2 = null;
            for (int i = 0; i < px.Count; i++)
                for (int j = i + 1; j < px.Count; j++)
                    if (px[i].Set != px[j].Set)
                    {
                        double d = Distance(px[i], px[j]);
                        if (d < min) { min = d; p1 = px[i]; p2 = px[j]; }
                    }
            return (p1, p2, min);
        }

        static double Distance(PointEx a, PointEx b) =>
            Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

        // ── Drawing ──────────────────────────────────────────────────────────────

        void DrawPoint(PointEx p, bool large)
        {
            double r = large ? 5 : 2;
            var el = new Ellipse
            {
                Width = r * 2, Height = r * 2,
                Fill = p.Set == 0 ? Brushes.LimeGreen : Brushes.OrangeRed
            };
            Canvas.SetLeft(el, p.X - r);
            Canvas.SetTop(el, p.Y - r);
            MyCanvas.Children.Add(el);
        }

        void DrawResultDot(PointEx p)
        {
            var el = new Ellipse
            {
                Width = 14, Height = 14,
                Fill = Brushes.Transparent,
                Stroke = Brushes.Gold,
                StrokeThickness = 2,
                Tag = "result"
            };
            Canvas.SetLeft(el, p.X - 7);
            Canvas.SetTop(el, p.Y - 7);
            MyCanvas.Children.Add(el);
        }

        void DrawResultLine(PointEx a, PointEx b)
        {
            MyCanvas.Children.Add(new Line
            {
                X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y,
                Stroke = Brushes.Gold, StrokeThickness = 2
            });
        }

        void UpdateCountLabel()
        {
            if (CountLabel == null) return;
            int nA = points.Count(p => p.Set == 0);
            int nB = points.Count(p => p.Set == 1);
            CountLabel.Text = $"A: {nA}   B: {nB}   Разом: {nA + nB}";
        }
    }
}
