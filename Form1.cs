using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Numerics;


namespace GrafikaKomputerowa2proj
{
    
    public partial class Form1 : Form
    {
        // Parametry
        public static double kd = 70;
        public static double ks = 70;
        public static int m = 5;
        public static Light light;
        public double light_height;
        public Color light_color;
        public System.Windows.Forms.Timer timer;
        public int FPS = 30;



        // Tekstura modyfikująca
        public bool move_light = false;
        public static bool show_triangles = false;
        public static bool modify_with_texture = false;
        public bool show_control_points = false;
        public static bool bezier_surface = true;
        public static bool functional_surface = false;
        public int control_point_radius = 10;
        public static DirectBitmap texture;

        // Bitmapa
        public static int bitmap_width = 600;
        public static int bitmap_height = 600;
        public DirectBitmap dbm;
        public static DirectBitmap image;

        // Kwadrat 4x4
        public static HPoint[,] h_array = new HPoint[4, 4];
        public HPoint current_point_clicked;
        public Point current_point_clicked_id;

        // Trójkaty
        public static int triangles_x = 20;
        public static int triangles_y = 20;
        public static int triangle_x_size;
        public static int triangle_y_size;
        public static int triangles_size;
        public static HPoint[,] triangle_points;
        public static Triangle[,,] triangles;
        public static List<int> buckets;
        
        // Preproccesed math numbers
        public static int[] factorial = new int[5];
        public static int [,] n_choose_k = new int[5,5];
        public Stopwatch stopwatch = new Stopwatch();
        public static double[,] height_of_pixel = new double[bitmap_width, bitmap_height];

        // Nowe przesuwanie 
        public static double alpha = Math.PI / 4;
        public static double beta = Math.PI / 4;
        public static bool rotate = false;


        
        public Form1()
        {
            InitializeComponent();
            Preprocess();
            Initialize_Light();
            Initialize_harray();
            Initialize_Bit_Map();
            Initialize_Triangles();
            Initialize_Timer();
            Initialize_Trackbars();
        }

        // Preprocessowanie silni, dwumianow newtona
        public void Preprocess()
        {
            for(int i=0; i<5; i++)
            {
                factorial[i] = Factorial(i);
                for(int j=0; j < 5; j++)
                {
                    n_choose_k[i, j] = Calculate_Combination(i, j);
                }
            }      
        }

        // Źródło światła
        public class Light
        {
            public double x, y, z;
            public double a;
            public int center_x, center_y;
            public double time = 0;
            public bool going_back = false;
            public int time_loop = 6000;
            public Color light_color;

            // Konstruktory
            public Light(double _x, double _y, double _z, double _a, Color _color)
            {
                a = _a;
                center_x = (int)_x;
                center_y = (int)_y;
                x = _x;
                y = _y;
                z = _z;
                light_color = _color;
            }

            // Metody
            public void Move()
            {
                double radius = 100;

                x = (int)(center_x + radius * Math.Cos(time/1000));
                y = (int)(center_y + radius * Math.Sin(time/1000));

            }

            static double FunctionCos(double t, double y)
            {
                return t * Math.Cos(t) - y;
            }

            static double FindTCos(double y)
            {
                double epsilon = 1e-6; // Precyzja rozwiązania
                double lowerBound = -3000; // Przykładowy dolny zakres
                double upperBound = 3000; // Przykładowy górny zakres

                // Sprawdź, czy znaleziono rozwiązanie w danym przedziale
                if (FunctionCos(lowerBound, y) * FunctionCos(upperBound, y) > 0)
                {
                    throw new ArgumentException("Brak rozwiązania w danym przedziale.");
                }

                double middle = 0;

                // Iteracyjnie przybliżaj się do rozwiązania
                while (upperBound - lowerBound > epsilon)
                {
                    middle = (lowerBound + upperBound) / 2;

                    // Sprawdź, czy rozwiązanie jest w lewej czy prawej połowie przedziału
                    if (FunctionCos(lowerBound, y) * FunctionCos(middle, y) < 0)
                    {
                        upperBound = middle;
                    }
                    else
                    {
                        lowerBound = middle;
                    }
                }

                return middle;
            }

            public void MoveConst()
            {
                double tcos = FindTCos(time);
                x = center_x + a * tcos * Math.Cos(tcos);
                y = center_y + a * tcos * Math.Sin(tcos);
            }
            public Vector3 Get_Vector3()
            {
                return new Vector3(x, y, z);
            }
            public void Time_Up(int time_up)
            {
                if (!going_back)
                {
                    time += time_up;

                    if (time >= time_loop)
                        going_back = !going_back;
                }
                else 
                {
                    time -= time_up;
                    if (time < 0)
                        going_back = !going_back;
                }
            }
        }

        // Punkt w 3 wymiarze
        public class HPoint
        {
            public double x, y, z;

            // Konstruktory
            public HPoint()
            {
                x = y = z = 0;
            }
            public HPoint(double _x, double _y)
            {
                x = _x;
                y = _y;
                z = 0;
            }
            public HPoint(double _x, double _y, double _z)
            {
                x = _x;
                y = _y;
                z = _z;
            }

            // Metody
            public Point convert_xy_to_point()
            {
                return new Point((int)x, (int)y);
            }
            public Vector3 Get_Vector3()
            {
                return new Vector3(x, y, z);
            }
        }

        // Edge 
        public struct Edge
        {
            public float YMin { get; set; }
            public float YMax { get; set; }
            public float X { get; set; }
            public float SlopeReciprocal { get; set; }

            public Edge(Edge e)
            {
                this.YMax = e.YMax;
                this.YMin = e.YMin;
                this.X = e.X;
                this.SlopeReciprocal = e.SlopeReciprocal;
            }
        }

        // Bucket Sort wypelnianie wielokąta
        public class BucketSortFill
        {
            public Vector3 N1, N2, N3;
            public List<Point> points;
            public int MaxY, MinY;
            private List<Edge>[] buckets;

            public BucketSortFill(Vector3 _N1, Vector3 _N2, Vector3 _N3, List<Point> _points)
            {
                N1 = _N1;
                N2 = _N2;
                N3 = _N3;

                points = _points;

                // Inicjalizacja kubełków
                buckets = new List<Edge>[ScreenHeight];
                for (int i = 0; i < ScreenHeight; i++)
                {
                    buckets[i] = new List<Edge>();
                }

                SetFillPolygon(points);
            }

            public (double,double) center_map(double x, double y)
            {
                return (x - bitmap_width / 2, y - bitmap_height / 2);
            }

            public (double,double) alpha_func(double x, double y)
            {
                return (x * Math.Cos(alpha) - y * Math.Sin(alpha), x * Math.Sin(alpha) + y * Math.Cos(alpha));
            }

            public (double, double) beta_func(double y, double z)
            {
                return (y * Math.Cos(beta) - z * Math.Sin(beta), y * Math.Sin(beta) + z * Math.Cos(beta));
            }

            public (double, double) center_back_map(double x, double y)
            {
                return (x + bitmap_width / 2, y + bitmap_height / 2);
            }

            public Vector3 rotate_vector(Vector3 p)
            {
                (p.X, p.Y) = center_map(p.X, p.Y);
                (p.X, p.Y) = alpha_func(p.X, p.Y);
                (p.Y, p.Z) = beta_func(p.Y, p.Z);
                (p.X, p.Y) = center_back_map(p.X, p.Y);

                return p;
            }

            public void Draw_triangle(DirectBitmap dbm)
            {

                // Tablica punktów trójkąta
                Point[] t = points.ToArray();
                for (int i = 0; i < 3; i++)
                {
                    if (t[i].X >= 600 || t[i].X < 0)
                        return;
                    if (t[i].Y >= 600 || t[i].Y < 0)
                        return;
                }
               
                Vector3 p1 = rotate_vector(new Vector3(t[0].X, t[0].Y, 600*height_of_pixel[t[0].X, t[0].Y]));
                Vector3 p2 = rotate_vector(new Vector3(t[1].X, t[1].Y, 600*height_of_pixel[t[1].X, t[1].Y]));
                Vector3 p3 = rotate_vector(new Vector3(t[2].X, t[2].Y, 600*height_of_pixel[t[2].X, t[2].Y]));

                Point[] trianglePoints = new Point[3];
                trianglePoints[0] = new Point((int)(p1.X), (int)(p1.Y));
                trianglePoints[1] = new Point((int)(p2.X), (int)(p2.Y));
                trianglePoints[2] = new Point((int)(p3.X), (int)(p3.Y));

                // Rysowanie trójkąta
                using (Graphics g = Graphics.FromImage(dbm.Bitmap))
                {
                    g.DrawPolygon(new Pen(new SolidBrush(Color.Red), 1), trianglePoints);
                }
            }

            public void DrawPixel(int x, int y, DirectBitmap dbm)
            {
                Color pixelColor = image.GetPixel(x, y);
                Color textureColor = texture.GetPixel(x, y);

                double z = height_of_pixel[x, y];
                double dx = (double)(x) / bitmap_width;
                double dy = (double)(y) / bitmap_height;

                Vector3 vec = new Vector3(dx, dy, z);

                if (rotate)
                {
                    Vector3 p = new Vector3(x, y, 600 * z);
                    p = rotate_vector(p);

                    if (p.X < 0 || p.Y < 0 || p.X >= bitmap_width || p.Y >= bitmap_height)
                    {
                        //nie rysujemy, wychodzi poza mape
                        return;
                    }

                    pixelColor = image.GetPixel((int)(p.X), (int)(p.Y));
                    textureColor = texture.GetPixel((int)(p.X), (int)(p.Y));

                    vec = new Vector3(p.X / bitmap_width, p.Y / bitmap_height, p.Z / 600);
                }

                List<double> wages = Calculate_Baricentric_Wages(x,y);
                Vector3 N = N1 * wages[0] + N2 * wages[1] + N3 * wages[2];
                
                if (modify_with_texture == true)
                    N = Calculate_N_Texture(textureColor, N).Normalize();
                
                N = N.Normalize();

                Color myColor = Calculate_Lambert_Color(vec, pixelColor, light.light_color, N);

                // Tutaj połącz lokalne bitmapy w jedną główną bitmapę
                dbm.SetPixel(x, y, myColor);

                
            }

            public void SetFillPolygon(List<Point> polygonPoints)
            {
                MaxY = 0;
                MinY = ScreenHeight;

                foreach (var p in polygonPoints)
                {
                    MaxY = Math.Max(MaxY, p.Y);
                    MinY = Math.Min(MinY, p.Y);
                }
                // Utwórz krawędzie na podstawie punktów wielokąta
                List<Edge> edges = CreateEdges(polygonPoints);

                // Dodaj krawędzie do odpowiednich kubełków
                AddEdgesToBuckets(edges);
            }

            public void FillPolygon(List<Point> polygonPoints, DirectBitmap dbm)
            {

                // Iteruj po kubełkach i wypełnij obszar wielokąta
                for (int y = MinY; y < MaxY; y++)
                {
                    ProcessBucket(buckets[y], y, dbm);
                }
            }

            private List<Edge> CreateEdges(List<Point> polygonPoints)
            {
                List<Edge> edges = new List<Edge>();

                // Utwórz krawędzie na podstawie punktów wielokąta
                int numPoints = polygonPoints.Count;

                for (int i = 0; i < numPoints; i++)
                {
                    int nextIndex = (i + 1) % numPoints;
                    Point current = polygonPoints[i];
                    Point next = polygonPoints[nextIndex];

                    if ((next.Y - current.Y) != 0)
                    {
                        Edge edge = new Edge();
                        edge.YMin = Math.Min(current.Y, next.Y);
                        edge.YMax = Math.Max(current.Y, next.Y);
                        edge.X = current.Y < next.Y ? current.X : next.X;
                        edge.SlopeReciprocal = (next.X - current.X) / (next.Y - current.Y);

                        edges.Add(edge);            
                    }
                }

                return edges;
            }

            private void AddEdgesToBuckets(List<Edge> edges)
            {
                foreach (Edge edge in edges)
                {
                    int yMinBucket = (int)edge.YMin;
                    int yMaxBucket = (int)edge.YMax;
                    float diff = 0;
                    for (int y = yMinBucket; y < yMaxBucket; y++)
                    {
                        
                        Edge e = new Edge(edge);
                        diff += edge.SlopeReciprocal;
                        e.X += diff;
                        buckets[y].Add(e);
                    }
                }
            }

            private void ProcessBucket(List<Edge> bucket, int y, DirectBitmap dbm)
            {
                bucket.Sort((e1, e2) => e1.X.CompareTo(e2.X));

                for (int i = 0; i < bucket.Count; i += 2)
                {
                    Edge edge1 = bucket[i];
                    Edge edge2 = bucket[i + 1];

                    // Wypełnij obszar pomiędzy krawędziami
                    
                    for (int x = (int)(edge1.X); x < (int)(edge2.X); x++)
                    {
                        // Rysuj piksel o współrzędnych (x, y)
                        // Możesz dodać odpowiednie operacje rysowania lub zapisywania punktów w tym miejscu
                        lock (dbm)
                        {
                            DrawPixel(x, y, dbm);
                        }
                    }
                }
            }

            public List<double> Calculate_Baricentric_Wages(double x, double y)
            {
                Point A = points[0];
                Point B = points[1];
                Point C = points[2];

                double x1 = (double)(A.X) , y1 = (double)(A.Y);
                double x2 = (double)(B.X) , y2 = (double)(B.Y);
                double x3 = (double)(C.X) , y3 = (double)(C.Y);

                double denominator = (y2 - y3) * (x1 - x3) + (x3 - x2) * (y1 - y3);

                double w1 = ((y2 - y3) * (x - x3) + (x3 - x2) * (y - y3)) / denominator;
                double w2 = ((y3 - y1) * (x - x3) + (x1 - x3) * (y - y3)) / denominator;
                double w3 = 1 - w1 - w2;

                List<double> l = new List<double>();
                l.Add(w1);
                l.Add(w2);
                l.Add(w3);
                return l;
            }

            // Zakładam, że wysokość ekranu jest ustalona na 100 pikseli (do dostosowania)
            private const int ScreenHeight = 600;
        }

        // Trójkat
        public class Triangle
        {
            public HPoint x, y, z;
            public Vector3 N1, N2, N3;
            public BucketSortFill bucket_fill;

            public Triangle()
            {

            }
            public Triangle(HPoint _x, HPoint _y, HPoint _z)
            {
                x = _x;
                y = _y;
                z = _z;
            }
            public Triangle(HPoint _x, HPoint _y, HPoint _z, BucketSortFill _bucket_fill, Vector3 _N1, Vector3 _N2, Vector3 _N3)
            {
                x = _x;
                y = _y;
                z = _z;
                N1 = _N1;
                N2 = _N2;
                N3 = _N3;
                bucket_fill = _bucket_fill;
            }

            // Metody
            public void Draw_Edges(Graphics g, Pen pen)
            {
                Point[] points1 = { x.convert_xy_to_point(), y.convert_xy_to_point(), z.convert_xy_to_point() };
                g.DrawPolygon(pen, points1);
            }
            public List<Point> Get_List_Points()
            {
                List<Point> list_point = new List<Point>();
                list_point.Add(x.convert_xy_to_point());
                list_point.Add(y.convert_xy_to_point());
                list_point.Add(z.convert_xy_to_point());
                return list_point;
            }
            public PointF[] Get_List_PointsF()
            {
                PointF[] list_point = new PointF[3];
                list_point[0] = x.convert_xy_to_point();
                list_point[1] = y.convert_xy_to_point();
                list_point[2] = z.convert_xy_to_point();
                return list_point;
            }
        }

        // Wektor 3 wymiarowy
        public struct Vector3
        {
            public double X { get; set;}
            public double Y { get; set;}
            public double Z { get; set;}

            public Vector3(double x, double y, double z)
            {
                X = x;
                Y = y;
                Z = z;
            }

            // Metody
            public static Vector3 operator +(Vector3 a, Vector3 b)
            {
                return new Vector3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
            }
            public static Vector3 operator -(Vector3 a, Vector3 b)
            {
                return new Vector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
            }
            public static Vector3 operator *(Vector3 vector, double scalar)
            {
                return new Vector3(vector.X * scalar, vector.Y * scalar, vector.Z * scalar);
            }

            public Vector3 Normalize()
            {
                double len = Math.Sqrt(X * X + Y * Y + Z * Z);

                if (len == 0)
                    return new Vector3(0,0,0);

                return new Vector3(X / len, Y / len, Z / len);
            }
            public double Length()
            {
                return Math.Sqrt(X * X + Y * Y + Z * Z);
            }
        }

        // Zapożyczona z internetu Szybka Bit Mapa
        public class DirectBitmap : IDisposable
        {
            public Bitmap Bitmap { get;  set; }
            public Int32[] Bits { get; private set; }
            public bool Disposed { get; private set; }
            public int Height { get; private set; }
            public int Width { get; private set; }

            protected GCHandle BitsHandle { get; private set; }

            public DirectBitmap(int width, int height)
            {
                Width = width;
                Height = height;
                Bits = new Int32[width * height];
                BitsHandle = GCHandle.Alloc(Bits, GCHandleType.Pinned);
                Bitmap = new Bitmap(width, height, width * 4, PixelFormat.Format32bppPArgb, BitsHandle.AddrOfPinnedObject());
            }

            public void SetPixel(int x, int y, Color colour)
            {
                int index = x + (y * Width);
                int col = colour.ToArgb();

                Bits[index] = col;
            }

            public Color GetPixel(int x, int y)
            {
                int index = x + (y * Width);
                int col = Bits[index];
                Color result = Color.FromArgb(col);

                return result;
            }

            public void Dispose()
            {
                if (Disposed) return;
                Disposed = true;
                Bitmap.Dispose();
                BitsHandle.Free();
            }
        }

        // Rysowanie Bit Mapy
        public void Draw_Bit_Map()
        {
            Draw_Picture_Box();
            pictureBox.Image = dbm.Bitmap;
        }

        // Inizjalizacja tablicy trójkątów
        public void Initialize_Triangles()
        {
            triangles_size = triangles_x * triangles_y;
            triangle_points = new HPoint[triangles_x, triangles_y];
            triangles = new Triangle[triangles_x - 1, triangles_y - 1, 2];
            triangle_x_size = bitmap_width / (triangles_x - 1);
            triangle_y_size = bitmap_height / (triangles_y - 1);
        }

        // Inicjalizacja bitmapy
        public void Initialize_Bit_Map()
        {
            dbm = new DirectBitmap(bitmap_width, bitmap_height);
            image = new DirectBitmap(bitmap_width, bitmap_height);
            texture = new DirectBitmap(bitmap_width + 1, bitmap_height + 1);
        }

        // Inicjalizacja harray
        public void Initialize_harray()
        {
            Random rnd = new Random();

            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    double rand = rnd.Next() % (light_height - 5);

                    if (i <= 2 && i >= 1 && j <= 2 && j >= 1)
                        h_array[i, j] = new HPoint((double)(i) * 1 / 3, (double)(j) * 1 / 3, 0.5);
                    else
                        h_array[i, j] = new HPoint((double)(i) * 1 / 3, (double)(j) * 1 / 3, 0);
                }
            }
        }

        // Inicjalizacja timer'u
        public void Initialize_Timer()
        {
            timer = new System.Windows.Forms.Timer();
            timer.Interval = 1000 / FPS;
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        // Inicjalizacja światła
        public void Initialize_Light()
        {
            double a = 0.1;
            light_height = 1;

            light_color = Color.FromArgb(255, 255, 255);
            light = new Light(bitmap_width / 2, bitmap_height / 2, light_height, a, light_color);
        }

        // Inicjalizacja trackbarów
        public void Initialize_Trackbars()
        {
            trackBar1.Minimum = 0;  // Ustawienie minimalnej wartości
            trackBar1.Maximum = 100;  // Ustawienie maksymalnej wartości
            trackBar1.TickFrequency = 10;  // Ustawienie częstotliwości podziałki
            trackBar1.Value = (int)kd;  // Ustawienie początkowej wartości
            trackBar1.Scroll += trackBar1_Scroll;  // Dodanie obsługi zdarzenia Scroll

            trackBar2.Minimum = 0;  // Ustawienie minimalnej wartości
            trackBar2.Maximum = 100;  // Ustawienie maksymalnej wartości
            trackBar2.TickFrequency = 10;  // Ustawienie częstotliwości podziałki
            trackBar2.Value = (int)ks;  // Ustawienie początkowej wartości
            trackBar2.Scroll += trackBar2_Scroll;  // Dodanie obsługi zdarzenia Scroll

            trackBar3.Minimum = 1;  // Ustawienie minimalnej wartości
            trackBar3.Maximum = 100;  // Ustawienie maksymalnej wartości
            trackBar3.TickFrequency = 3;  // Ustawienie częstotliwości podziałki
            trackBar3.Value = m;  // Ustawienie początkowej wartości
            trackBar3.Scroll += trackBar3_Scroll;  // Dodanie obsługi zdarzenia Scroll

            trackBar4.Minimum = 3;  // Ustawienie minimalnej wartości
            trackBar4.Maximum = 100;  // Ustawienie maksymalnej wartości
            trackBar4.TickFrequency = 3;  // Ustawienie częstotliwości podziałki
            trackBar4.Value = triangles_x;  // Ustawienie początkowej wartości
            trackBar4.Scroll += trackBar4_Scroll;  // Dodanie obsługi zdarzenia Scroll

            trackBar5.Minimum = 0;  // Ustawienie minimalnej wartości
            trackBar5.Maximum = 100;  // Ustawienie maksymalnej wartości
            trackBar5.TickFrequency = 10;  // Ustawienie częstotliwości podziałki
            trackBar5.Value = 0;  // Ustawienie początkowej wartości
            trackBar5.Scroll += trackBar5_Scroll;  // Dodanie obsługi zdarzenia Scroll

            trackBar6.Minimum = 100;  // Ustawienie minimalnej wartości
            trackBar6.Maximum = 500;  // Ustawienie maksymalnej wartości
            trackBar6.TickFrequency = 100;  // Ustawienie częstotliwości podziałki
            trackBar6.Value = (int)(light.z)*100;  // Ustawienie początkowej wartości
            trackBar6.Scroll += trackBar6_Scroll;  // Dodanie obsługi zdarzenia Scroll

            trackBar7.Minimum = 0;  // Ustawienie minimalnej wartości
            trackBar7.Maximum = (int)(90 * Math.PI / 2);  // Ustawienie maksymalnej wartości
            trackBar7.TickFrequency = 100;  // Ustawienie częstotliwości podziałki
            trackBar7.Value = (int)(alpha * (int)(90 * Math.PI / 2));  // Ustawienie początkowej wartości
            trackBar7.Scroll += trackBar7_Scroll;  // Dodanie obsługi zdarzenia Scroll

            trackBar8.Minimum = 0;  // Ustawienie minimalnej wartości
            trackBar8.Maximum = (int)(45*Math.PI/2);  // Ustawienie maksymalnej wartości
            trackBar8.TickFrequency = 100;  // Ustawienie częstotliwości podziałki
            trackBar8.Value = (int)(beta * (int)(45 * Math.PI / 2));  // Ustawienie początkowej wartości
            trackBar8.Scroll += trackBar8_Scroll;  // Dodanie obsługi zdarzenia Scroll

        }

        // Szybkie potegowanie
        public static double MyPow(double num, int exp)
        {
            double result = 1.0;
            while (exp > 0)
            {
                if (exp % 2 == 1)
                    result *= num;
                exp >>= 1;
                num *= num;
            }

            return result;
        }

        // Funckja beziera
        public static double Bezier_Function(int i, int n, double t)
        {
            return n_choose_k[n, i] * MyPow(t, i) * MyPow(1 - t, n - i);
        }

        //Pochodna funkcji beziera po t
        public double Bezier_Deriv_Function(int i, int n, double t)
        {
            if (i == 0)
                return - n * MyPow(1 - t, n - 1);

            if (t == 1)
                return 0;
            return n_choose_k[n, i] * MyPow(t,i - 1) * (n * t - i) * (-(MyPow(1 - t, n - 1 - i)));
        }

        // Suma funkcji beziera liczac pochodna po x
        public double Bezier_Z_Deriv_Function(double x, double y)
        {
            double ans = 0;

            if(x > 1 || y > 1)
            {
                return 0;
            }

            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                {

                    double a = Bezier_Deriv_Function(i, 3, x);
                    double b = Bezier_Function(j, 3, y);
                    ans += h_array[i, j].z * a * b ;
                }

            return ans;
        }

        // Suma funkcji beziera
        public static double Bezier_Z_Function(double x, double y)
        {
            double ans = 0;

            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++) 
                {
                    ans += h_array[i,j].z * Bezier_Function(i, 3, x) * Bezier_Function(j, 3, y);
                }

            return ans;
        }

        public Vector3 P(double u, double v)
        {
            return new Vector3(u, v, Bezier_Z_Function(u, v));
        }

        // Pochodna po u wektora P
        public  Vector3 Pu(double u, double v)
        {
            int ix = (int)(u * bitmap_width) % bitmap_width;
            int iy = (int)(v * bitmap_height) % bitmap_height;
            return new Vector3(1, 0, height_of_pixel[ix, iy]);
        }

        // Pochodna po v wektora P
        public Vector3 Pv(double u, double v)
        {
            int ix = (int)(u * bitmap_width) % bitmap_width;
            int iy = (int)(v * bitmap_height) % bitmap_height;
            return new Vector3(0, 1, height_of_pixel[iy, ix]);
        }

        // Silnia 
        public static int Factorial(int number)
        {
            if (number == 0)
            {
                return 1;
            }

            int result = 1;
            for (int i = 1; i <= number; i++)
            {
                result *= i;
            }

            return result;
        }        

        // Kombinacje n po k
        public static int Calculate_Combination(int n, int k)
        {
            if (k < 0 || k > n)
            {
                return 0; // Kombinacja nie istnieje dla k większego niż n lub k mniejszego niż 0.
            }

            // Obliczanie silni dla n, k oraz n-k
            int nFactorial = Factorial(n);
            int kFactorial = Factorial(k);
            int nkFactorial = Factorial(n - k);

            // Obliczanie kombinacji
            return nFactorial / (kFactorial * nkFactorial);
        }

        // Iloczyn wektorowy
        public Vector3 Cross_Product(Vector3 A, Vector3 B)
        {
            double resultX = A.Y * B.Z - A.Z * B.Y;
            double resultY = A.Z * B.X - A.X * B.Z;
            double resultZ = A.X * B.Y - A.Y * B.X;

            return new Vector3(resultX, resultY, resultZ);
        }

        // Pomnoz macierz 3x3 z wektorem
        public static Vector3 Multiply_Matrix_Vector(Vector3 A, Vector3 B, Vector3 C, Vector3 N)
        {
            double x = A.X * N.X + B.X * N.Y + C.X * N.Z;
            double y = A.Y * N.X + B.Y * N.Y + C.Y * N.Z;
            double z = A.Z * N.X + B.Z * N.Y + C.Z * N.Z;

            return new Vector3(x, y, z);
        }

        // Skaluje wektor coloru na wartosci od -1 do 1 
        public static Vector3 Scale_Color(Vector3 A)
        {
            int push = 255 / 2;
            
            A.X -= push;
            A.X /= push;
            A.Y -= push;
            A.Y /= push;
            A.Z -= push;
            A.Z /= push;

            return A;
        }

        // Iloczyn skalarny
        public static double Dot_Product(Vector3 A, Vector3 B)
        {
            return A.X *B.X + A.Y * B.Y + A.Z * B.Z;
        }

        // Iloczyn wektorowy
        public static Vector3 Vector_Product(Vector3 A, Vector3 B)
        {
            double x = A.Y * B.Z - A.Z * B.Y;
            double y = A.Z * B.X - A.X * B.Z;
            double z = A.X * B.Y - A.Y * B.X;

            return new Vector3(x,y,z);
        }

        // Funkcja lamberta - liczenie koloru
        public static int Calculate_Lambert_ColorRGB(int Lo, int Ll, double cos1, double cos2)
        {
            double d_kd = kd / 100;
            double d_ks = ks / 100;

            double d_Lo = (double)Lo / 255;
            double d_Ll = (double)Ll / 255;

            double d_I = d_kd * d_Ll * d_Lo * cos1 + d_ks * d_Ll * d_Lo * Math.Pow(cos2, m);

            int I = (int)(d_I * 255);

            if (I > 255)
                return 255;

            return I;
        }

        // Funkcja lambera
        public static Color Calculate_Lambert_Color(Vector3 p, Color Lobject, Color Llight, Vector3 N)
        {
            Vector3 light_vec = light.Get_Vector3();
                light_vec.X /= bitmap_width;
                light_vec.Y /= bitmap_height;

            Vector3 L = (light_vec - p).Normalize();
            Vector3 V = new Vector3(0, 0, 1);
            Vector3 R = ( (N*2*Dot_Product(N, L)) - L).Normalize();

            double cos1 = Dot_Product(N, L); 
            double cos2 = Dot_Product(V, R);

            if (cos1 < 0)
                cos1 = 0;
            if (cos2 < 0)
                cos2 = 0;

            int i1 = Calculate_Lambert_ColorRGB(Lobject.R, Llight.R, cos1, cos2);
            int i2 = Calculate_Lambert_ColorRGB(Lobject.G, Llight.G, cos1, cos2);
            int i3 = Calculate_Lambert_ColorRGB(Lobject.B, Llight.B, cos1, cos2);

            return Color.FromArgb(i1,i2,i3);            
        }

        // Obliczamy wektor normalny bez uzycia tekstury.
        public Vector3 Calculate_N(double x, double y)
        {
            double u = x / bitmap_width;
            double v = y / bitmap_height;

            Vector3 pu = Pu(u, v);
            Vector3 pv = Pv(u, v);

            return Cross_Product(pu, pv);
        }

        // Obliczamy wektor normalny używając tekstury.
        public static Vector3 Calculate_N_Texture(Color textureColor, Vector3 N_surface)
        {
            Vector3 texture_color = new Vector3(textureColor.R, textureColor.G, textureColor.B);
            texture_color = Scale_Color(texture_color).Normalize();

            Vector3 B;

            if (N_surface.X == 0 && N_surface.Y == 0 && N_surface.Z == 1)
                B = new Vector3(0, 1, 0);
            else
                B = Vector_Product(N_surface, new Vector3(0, 0, 1)).Normalize();

            Vector3 T = Vector_Product(B, N_surface).Normalize();

            return Multiply_Matrix_Vector(T, B, N_surface, texture_color);
        }

        // Obliczamy wektor normalny
        public Vector3 Caluclate_N_Final(Vector3 A)
        {
            int x = (int)A.X; // Kompenzacja dla okna formularza
            int y = (int)A.Y; // Kompenzacja dla okna formularza

            Vector3 N = Calculate_N(x, y);

            return N.Normalize();
        }

        // Malowanie trójkata
        public void Draw_Triangle(Triangle t, DirectBitmap dbm)
        {
            t.bucket_fill.FillPolygon(t.Get_List_Points(), dbm);
        }

        // Form Obsługa
        private void Form1_Load(object sender, EventArgs e)
        {
            string image_path = Path.Combine(Application.StartupPath, "..\\..\\pic1.png");
            string picture_path = Path.Combine(Application.StartupPath, "..\\..\\texture1.png");

            Bitmap new_image = new Bitmap(image_path);
            Bitmap new_texture = new Bitmap(picture_path);

            // Utwórz obiekt Graphics do rysowania na nowym obrazie
            using (Graphics g = Graphics.FromImage(image.Bitmap))
            {
                // Ustaw jakość rysowania (opcjonalne)
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

                // Narysuj oryginalny obraz na nowym obrazie z nowymi wymiarami
                g.DrawImage(new_image, 0, 0, bitmap_width, bitmap_height);
            }

            using (Graphics g = Graphics.FromImage(texture.Bitmap))
            {
                // Ustaw jakość rysowania (opcjonalne)
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

                // Narysuj oryginalny obraz na nowym obrazie z nowymi wymiarami
                g.DrawImage(new_texture, 0, 0, bitmap_width + 1, bitmap_height + 1);
            }

            Reinitialize_Triangles();
            Draw_Bit_Map();
            pictureBox.Refresh();
        }

        // Rysowanie okregu
        public void Draw_Circle(int centerX, int centerY, int radius, Graphics g, Brush brush)
        {
            // Rysuj i wypełnij okrąg
            g.FillEllipse(brush, centerX - radius, centerY - radius, 2 * radius, 2 * radius);

            // Zwolnij zasoby
            brush.Dispose();
         }

        // Główna funkcja rysujaca
        public void Draw_Picture_Box()
        {
            Graphics g = Graphics.FromImage(dbm.Bitmap);
            g.Clear(Color.WhiteSmoke);

            int height = triangles.GetLength(0);
            int width = triangles.GetLength(1);

            Parallel.For(0, height, y =>
            {
                for (int x = 0; x < width; ++x)
                {
                    var t1 = triangles[y, x, 1];
                    Draw_Triangle(t1, dbm);
                    var t2 = triangles[y, x, 0];
                    Draw_Triangle(t2, dbm);

                    if (show_triangles && !rotate)
                        lock (g)
                        {
                            g.DrawPolygon(new Pen(Color.Red), t1.Get_List_Points().ToArray());
                            g.DrawPolygon(new Pen(Color.Red), t2.Get_List_Points().ToArray());
                            //g.FillPolygon(new SolidBrush(Color.Green), t1.Get_List_PointsF());
                            //g.FillPolygon(new SolidBrush(Color.Green), t2.Get_List_PointsF());
                        }
                }
            }
            );

            Parallel.For(0, height, y =>
            {
                for (int x = 0; x < width; ++x)
                {
                    var t1 = triangles[y, x, 1];
                    var t2 = triangles[y, x, 0];

                    if (rotate && show_triangles)
                    {
                        lock (dbm)
                        {
                            t1.bucket_fill.Draw_triangle(dbm);
                            t2.bucket_fill.Draw_triangle(dbm);
                        }
                    }

                }
            }
            );
            // Rysowanie punktow kontrolnych
            if (show_control_points)
                for (int i = 0; i < 4; i++)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        if (current_point_clicked_id.X == i && current_point_clicked_id.Y == j)
                        {
                            Draw_Circle((int)(i * 0.333333 * bitmap_width), (int)(j * 0.333333 * bitmap_width), control_point_radius, g, new SolidBrush(Color.Green));
                        }
                        else
                        {
                            Draw_Circle((int)(i * 0.333333 * bitmap_width), (int)(j * 0.333333 * bitmap_width), control_point_radius, g, new SolidBrush(Color.Red));
                        }
                    }
                }

            //Rysowanie światła
            Pen lightning = new Pen(Color.Yellow, 20);
            g.DrawRectangle(lightning, new Rectangle((int)light.x, (int)light.y, 1, 1));
        }

        // Wykonywanie timeru
        private void Timer_Tick(object sender, EventArgs e)
        {
            if (move_light)
            {
                stopwatch.Start();
                Draw_Bit_Map();
                pictureBox.Refresh();
                stopwatch.Stop();
                light.Time_Up(50);
                light.Move();
            }
        }

        //Obsługa suwaków
        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            kd = trackBar1.Value;
        }
        private void trackBar2_Scroll(object sender, EventArgs e)
        {
            ks = trackBar2.Value;
        }
        private void trackBar3_Scroll(object sender, EventArgs e)
        {
            m = trackBar3.Value;
        }
        private void trackBar4_Scroll(object sender, EventArgs e)
        {
            triangles_x = trackBar4.Value;
            triangles_y = trackBar4.Value;
            Initialize_Triangles();
            Reinitialize_Triangles();
            pictureBox.Refresh();
            Draw_Bit_Map();
        }
        private void trackBar5_Scroll(object sender, EventArgs e)
        {
            if (current_point_clicked != null)
            {
                current_point_clicked.z = (double)(trackBar5.Value) / 100;
                Initialize_Triangles();
                Reinitialize_Triangles();
                pictureBox.Refresh();
                Draw_Bit_Map();
            }
        }
        private void trackBar6_Scroll(object sender, EventArgs e)
        {
            light.z = (double)(trackBar6.Value)/100;
            pictureBox.Refresh();
            Draw_Bit_Map();
        }
        private void trackBar7_Scroll(object sender, EventArgs e)
        {
            alpha = (double)(trackBar7.Value)/ (int)(70 * Math.PI / 2);
            Initialize_Triangles();
            Reinitialize_Triangles();
            pictureBox.Refresh();
            Draw_Bit_Map();
        }
        private void trackBar8_Scroll(object sender, EventArgs e)
        {
            beta = (double)(trackBar8.Value) / (int)(45 * Math.PI / 2);
            Initialize_Triangles();
            Reinitialize_Triangles();
            pictureBox.Refresh();
            Draw_Bit_Map();
        }

        // Pobierz zdjecie
        private void ImportButton_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();

            openFileDialog.Filter = "Pliki obrazów|*.jpg;*.jpeg;*.png;*.gif;*.bmp|Wszystkie pliki|*.*";

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                Bitmap new_image = new Bitmap(openFileDialog.FileName);

                using (Graphics g = Graphics.FromImage(image.Bitmap))
                {
                    // Ustaw jakość rysowania (opcjonalne)
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

                    // Narysuj oryginalny obraz na nowym obrazie z nowymi wymiarami
                    g.DrawImage(new_image, 0, 0, bitmap_width, bitmap_height);
                }

            }
            
            Reinitialize_Triangles();
            Draw_Picture_Box();
            Draw_Bit_Map();
            pictureBox.Refresh();
        }

        // Pobierz mape wektorw normalnych
        private void ImportTexture_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();

            openFileDialog.Filter = "Pliki obrazów|*.jpg;*.jpeg;*.png;*.gif;*.bmp|Wszystkie pliki|*.*";

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                Bitmap new_texture= new Bitmap(openFileDialog.FileName);

                using (Graphics g = Graphics.FromImage(texture.Bitmap))
                {
                    // Ustaw jakość rysowania (opcjonalne)
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

                    // Narysuj oryginalny obraz na nowym obrazie z nowymi wymiarami
                    g.DrawImage(new_texture, 0, 0, bitmap_width, bitmap_height);
                }
            }

            Reinitialize_Triangles();
            Draw_Picture_Box();
            Draw_Bit_Map();
            pictureBox.Refresh();
        }

        // Stwórz trójkaty (używamy przy zmianie trojkatow lub h_array)
        private void Reinitialize_Triangles()
        {
            for (int i = 0; i < triangles_x; i++)
            {
                for (int j = 0; j < triangles_y; j++)
                {
                    double x = triangle_x_size * i;
                    double y = bitmap_width - triangle_y_size * (j);
                    triangle_points[i, j] = new HPoint(x, y, Bezier_Z_Function(x / bitmap_width, y / bitmap_height));
                }
            }

            for (int i = 0; i < triangles_x - 1; i++)
            {
                for (int j = 0; j < triangles_y - 1; j++)
                {
                    Vector3 p1 = triangle_points[i, j].Get_Vector3();
                    Vector3 p2_1 = triangle_points[i + 1, j].Get_Vector3();
                    Vector3 p2_2 = triangle_points[i, j + 1].Get_Vector3();
                    Vector3 p3 = triangle_points[i + 1, j + 1].Get_Vector3();

                    Vector3 N1_1 = Caluclate_N_Final(p1);
                    Vector3 N2_1 = Caluclate_N_Final(p2_1);
                    Vector3 N3_1 = Caluclate_N_Final(p3);
                    Vector3 N1_2 = Caluclate_N_Final(p1);
                    Vector3 N2_2 = Caluclate_N_Final(p2_2);
                    Vector3 N3_2 = Caluclate_N_Final(p3);

                    List<Point> list1 = new List<Point>();
                    list1.Add(triangle_points[i, j].convert_xy_to_point());
                    list1.Add(triangle_points[i + 1, j].convert_xy_to_point());
                    list1.Add(triangle_points[i + 1, j + 1].convert_xy_to_point());

                    List<Point> list2 = new List<Point>();
                    list2.Add(triangle_points[i, j].convert_xy_to_point());
                    list2.Add(triangle_points[i, j + 1].convert_xy_to_point());
                    list2.Add(triangle_points[i + 1, j + 1].convert_xy_to_point());

                    BucketSortFill polyfill1 = new BucketSortFill(N1_1, N2_1, N3_1, list1);
                    BucketSortFill polyfill2 = new BucketSortFill(N1_2, N2_2, N3_2, list2);

                    triangles[i, j, 0] = new Triangle(triangle_points[i, j], triangle_points[i + 1, j], triangle_points[i + 1, j + 1], polyfill1, N1_1, N2_1, N3_1);
                    triangles[i, j, 1] = new Triangle(triangle_points[i, j], triangle_points[i, j + 1], triangle_points[i + 1, j + 1], polyfill2, N1_2, N2_2, N3_2);
                }
            }

            for (int i = 0; i < bitmap_width; i++)
            {
                for(int j=0; j < bitmap_height; j++)
                {
                    double dx = (double)(i) / bitmap_width;
                    double dy = (double)(j) / bitmap_height;

                    if(bezier_surface)
                        height_of_pixel[i, j] = Bezier_Z_Function(dx, dy);
                    else
                        height_of_pixel[i, j] = Math.Sin(dx*Math.PI/2 + dy* Math.PI/2);
                }
            }
        }

        // Checkboxy

        // Modyfikuj teksturą
        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            modify_with_texture = !modify_with_texture;
            Reinitialize_Triangles();
            Draw_Picture_Box();
            Draw_Bit_Map();
            pictureBox.Refresh();
        }

        // Ruch światła
        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            move_light = !move_light;
        }

        // Pokaż trójkaty
        private void checkBox3_CheckedChanged(object sender, EventArgs e)
        {
            show_triangles = !show_triangles;
            Draw_Picture_Box();
            Draw_Bit_Map();
            pictureBox.Refresh();
        }

        // Pokaż punkty kontrolne
        private void checkBox4_CheckedChanged(object sender, EventArgs e)
        {
            show_control_points = !show_control_points;
            Draw_Picture_Box();
            Draw_Bit_Map();
            pictureBox.Refresh();
        }

        // Sprawdz czy punkt jest  w okregu
        private bool IsPointInsideCircle(int x, int y, int centerX, int centerY)
        {
            double distance = Math.Sqrt(Math.Pow(x - centerX, 2) + Math.Pow(y - centerY, 2));

            return distance <= control_point_radius;
        }

        // Obsługa dotykania pictureboxa
        private void pictureBox_MouseClick(object sender, MouseEventArgs e)
        {
            for(int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++) 
                {
                    int centerx = (int)(h_array[i, j].x * bitmap_width);
                    int centery = (int)(h_array[i, j].y * bitmap_height);
                    int x = e.X;
                    int y = e.Y;

                    if (IsPointInsideCircle(x, y, centerx, centery))
                    {
                        current_point_clicked = h_array[i, j];
                        current_point_clicked_id = new Point(i, j);
                        pictureBox.Refresh();
                        trackBar5.Value = (int)(current_point_clicked.z * 100);
                        Draw_Bit_Map();
                    }
                }
            }
        }

        // Wybor koloru swiatla
        private void button1_Click(object sender, EventArgs e)
        {
            using (ColorDialog colorDialog = new ColorDialog())
            {
                // Ustaw aktualny kolor okna dialogowego na kolor aktualnego okręgu
                colorDialog.Color = light_color;

                // Jeśli użytkownik wybierze kolor i kliknie OK
                if (colorDialog.ShowDialog() == DialogResult.OK)
                {
                    // Zaktualizuj kolor okręgu
                    light.light_color = colorDialog.Color;
                }
            }

            Draw_Picture_Box();
            Draw_Bit_Map();
            pictureBox.Refresh();
        }

        // Po zalaczeniu picturebox'a
        private void pictureBox_LoadCompleted(object sender, AsyncCompletedEventArgs e)
        {
            Draw_Picture_Box();
            Draw_Bit_Map();
            pictureBox.Refresh();
        }

        private void checkBox5_CheckedChanged(object sender, EventArgs e)
        {
            bezier_surface = !bezier_surface;
            functional_surface = !functional_surface;
            Reinitialize_Triangles();
            Draw_Picture_Box();
            Draw_Bit_Map();
            pictureBox.Refresh();

        }

        private void checkBox6_CheckedChanged(object sender, EventArgs e)
        {
            rotate = !rotate;
            Reinitialize_Triangles();
            Draw_Picture_Box();
            Draw_Bit_Map();
            pictureBox.Refresh();
        }

        private void pictureBox_Click(object sender, EventArgs e)
        {

        }


    }
}
