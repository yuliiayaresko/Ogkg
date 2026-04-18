using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

        public MainWindow() => InitializeComponent();

        public class PointEx
        {
            public double X, Y;
            public int Set;
            public PointEx(double x, double y, int set) { X = x; Y = y; Set = set; }
        }

        private void Canvas_Click(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(MyCanvas);
            var p = new PointEx(pos.X, pos.Y, ModeBox.SelectedIndex);
            points.Add(p);
            DrawPoint(p);
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            points.Clear();
            MyCanvas.Children.Clear();
            ResultText.Text = "Поле очищено";
        }

        private void Generate_Click(object sender, RoutedEventArgs e)
        {
            MyCanvas.Children.Clear();
            points.Clear();

            if (!int.TryParse(PointsCountBox.Text, out int total) || total <= 0) return;

            double w = MyCanvas.ActualWidth > 0 ? MyCanvas.ActualWidth : 800;
            double h = MyCanvas.ActualHeight > 0 ? MyCanvas.ActualHeight : 500;

            for (int i = 0; i < total / 2; i++)
            {
                points.Add(new PointEx(rand.Next(20, (int)w / 2 - 20), rand.Next(20, (int)h - 20), 0));
                points.Add(new PointEx(rand.Next((int)w / 2 + 20, (int)w - 20), rand.Next(20, (int)h - 20), 1));
            }

            int limit = 3000;
            for (int i = 0; i < Math.Min(points.Count, limit); i++) DrawPoint(points[i]);
            ResultText.Text = $"Згенеровано {points.Count} точок.";
        }

        private void FindClosest_Click(object sender, RoutedEventArgs e)
        {
            if (points.Count(p => p.Set == 0) == 0 || points.Count(p => p.Set == 1) == 0) return;

            var lines = MyCanvas.Children.OfType<Line>().ToList();
            foreach (var l in lines) MyCanvas.Children.Remove(l);

            Stopwatch sw = Stopwatch.StartNew();

            // КРОК 1: Сортування O(N log N)
            var sortedX = points.OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
            var sortedY = points.OrderBy(p => p.Y).ThenBy(p => p.X).ToList();

            // КРОК 2: Рекурсія O(N log N)
            var result = FindClosestRecursive(sortedX, sortedY);

            sw.Stop();

            if (result.p1 != null)
            {
                DrawLine(result.p1, result.p2);
                ResultText.Text = $"Dist: {result.dist:F4} | Time: {sw.Elapsed.TotalMilliseconds:F2} ms";
            }
            else
            {
                ResultText.Text = "Не знайдено пари точок з різних множин.";
            }
        }

        private (PointEx p1, PointEx p2, double dist) FindClosestRecursive(List<PointEx> px, List<PointEx> py)
        {
            if (px.Count <= 3) return BruteForce(px);

            int mid = px.Count / 2;
            PointEx midPoint = px[mid];

            // ВИПРАВЛЕННЯ 1: Використовуємо HashSet для точного визначення
            // які точки належать лівій половині px — замість порівняння по X/Y,
            // яке могло давати невідповідність при однакових координатах.
            var leftSet = new HashSet<PointEx>(px.GetRange(0, mid));

            var pyLeft = new List<PointEx>();
            var pyRight = new List<PointEx>();
            foreach (var p in py)
            {
                if (leftSet.Contains(p))
                    pyLeft.Add(p);
                else
                    pyRight.Add(p);
            }

            var resL = FindClosestRecursive(px.GetRange(0, mid), pyLeft);
            var resR = FindClosestRecursive(px.GetRange(mid, px.Count - mid), pyRight);

            // ВИПРАВЛЕННЯ 2: Обробляємо випадок коли одна або обидві половини
            // не містять точок з різних множин (p1 == null означає відсутність валідної пари).
            (PointEx p1, PointEx p2, double dist) best;
            if (resL.p1 == null && resR.p1 == null)
                best = (null, null, double.MaxValue);
            else if (resL.p1 == null)
                best = resR;
            else if (resR.p1 == null)
                best = resL;
            else
                best = (resL.dist < resR.dist) ? resL : resR;

            // Якщо валідної пари ще немає — смуга все одно може знайти
            double delta = best.dist < double.MaxValue ? best.dist : double.MaxValue;

            // Смуга формується за O(N)
            var strip = new List<PointEx>();
            foreach (var p in py)
            {
                if (delta == double.MaxValue || Math.Abs(p.X - midPoint.X) < delta)
                    strip.Add(p);
            }

            // Перевірка смуги
            for (int i = 0; i < strip.Count; i++)
            {
                for (int j = i + 1; j < strip.Count; j++)
                {
                    double dy = strip[j].Y - strip[i].Y;
                    // Якщо delta ще не знайдена — не можемо обрізати по Y, перевіряємо всі
                    if (delta < double.MaxValue && dy >= delta) break;

                    if (strip[i].Set != strip[j].Set)
                    {
                        double d = Distance(strip[i], strip[j]);
                        if (d < best.dist)
                        {
                            best.dist = d;
                            best.p1 = strip[i];
                            best.p2 = strip[j];
                            delta = d; // оновлюємо delta після знаходження першої пари
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
            // ВИПРАВЛЕННЯ 3: Якщо всі точки одного Set — явно повертаємо
            // (null, null, MaxValue), що є сигналом «валідної пари немає».
            // Викликаючий код тепер коректно обробляє цей випадок.
            return (p1, p2, min);
        }

        double Distance(PointEx a, PointEx b) =>
            Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

        void DrawPoint(PointEx p)
        {
            Ellipse el = new Ellipse
            {
                Width = 3,
                Height = 3,
                Fill = p.Set == 0 ? Brushes.LimeGreen : Brushes.OrangeRed
            };
            Canvas.SetLeft(el, p.X - 1.5);
            Canvas.SetTop(el, p.Y - 1.5);
            MyCanvas.Children.Add(el);
        }

        void DrawLine(PointEx a, PointEx b)
        {
            Line line = new Line
            {
                X1 = a.X,
                Y1 = a.Y,
                X2 = b.X,
                Y2 = b.Y,
                Stroke = Brushes.Gold,
                StrokeThickness = 3
            };
            MyCanvas.Children.Add(line);
        }
    }
}