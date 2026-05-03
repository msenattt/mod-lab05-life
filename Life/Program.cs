using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Text.Json;
using ScottPlot;

namespace cli_life
{
    public class Config
        {
            public int Width { get; set; }
            public int Height { get; set; }
            public int CellSize { get; set; }
            public double LiveDensity { get; set; }
            public int Delay { get; set; }
        }
    public class Cell
    {
        public bool IsAlive;
        public readonly List<Cell> neighbors = new List<Cell>();
        private bool IsAliveNext;
        public void DetermineNextLiveState()
        {
            int liveNeighbors = neighbors.Where(x => x.IsAlive).Count();
            if (IsAlive)
                IsAliveNext = liveNeighbors == 2 || liveNeighbors == 3;
            else
                IsAliveNext = liveNeighbors == 3;
        }
        public void Advance()
        {
            IsAlive = IsAliveNext;
        }
    }
    public class Board
    {
        public readonly Cell[,] Cells;
        public readonly int CellSize;

        public int Columns { get { return Cells.GetLength(0); } }
        public int Rows { get { return Cells.GetLength(1); } }
        public int Width { get { return Columns * CellSize; } }
        public int Height { get { return Rows * CellSize; } }

        public Board(int width, int height, int cellSize, double liveDensity = .1)
        {
            CellSize = cellSize;

            Cells = new Cell[width / cellSize, height / cellSize];
            for (int x = 0; x < Columns; x++)
                for (int y = 0; y < Rows; y++)
                    Cells[x, y] = new Cell();

            ConnectNeighbors();
            Randomize(liveDensity);
        }

        readonly Random rand = new Random();
        public void Randomize(double liveDensity)
        {
            foreach (var cell in Cells)
                cell.IsAlive = rand.NextDouble() < liveDensity;
        }

        public void Advance()
        {
            foreach (var cell in Cells)
                cell.DetermineNextLiveState();
            foreach (var cell in Cells)
                cell.Advance();
        }

        private void ConnectNeighbors()
        {
            for (int x = 0; x < Columns; x++)
            {
                for (int y = 0; y < Rows; y++)
                {
                    int xL = (x > 0) ? x - 1 : Columns - 1;
                    int xR = (x < Columns - 1) ? x + 1 : 0;

                    int yT = (y > 0) ? y - 1 : Rows - 1;
                    int yB = (y < Rows - 1) ? y + 1 : 0;

                    Cells[x, y].neighbors.Add(Cells[xL, yT]);
                    Cells[x, y].neighbors.Add(Cells[x, yT]);
                    Cells[x, y].neighbors.Add(Cells[xR, yT]);
                    Cells[x, y].neighbors.Add(Cells[xL, y]);
                    Cells[x, y].neighbors.Add(Cells[xR, y]);
                    Cells[x, y].neighbors.Add(Cells[xL, yB]);
                    Cells[x, y].neighbors.Add(Cells[x, yB]);
                    Cells[x, y].neighbors.Add(Cells[xR, yB]);
                }
            }
        }
        
    }
    class Program
    {
        static Board board;
        static Config config;
        static int generation = 0;
        static bool isPaused = false;
        static readonly List<(int, int)> Block = new List<(int, int)>
        {
            (0,0), (0,1), (1,0), (1,1)
        };

        static readonly List<(int, int)> Barge = new List<(int, int)>
        {
            (0,0), (0,1), (1,0), (1,2), (2,1), (3,0)
        };

        static readonly List<(int, int)> Snake = new List<(int, int)>
        {
            (0,0), (0,1), (1,0), (2,0), (3,1), (3,2), (2,2)
        };

        static readonly List<(int, int)> Boat = new List<(int, int)>
        {
            (0,0), (0,1), (1,0), (1,2), (2,1)
        };

        static readonly List<(int, int)> Beehive = new List<(int, int)>
        {
            (0,1), (1,0), (1,2), (2,0), (2,2), (3,1)
        };
        static Config LoadConfig()
        {
            if (!File.Exists("config.json"))
            {
                var defaultConfig = new Config();
                File.WriteAllText("config.json",
                    JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true }));
                return defaultConfig;
            }
            var json = File.ReadAllText("config.json");
            return JsonSerializer.Deserialize<Config>(json);
        }
        static private void Reset()
        {
            board = new Board(
                config.Width,
                config.Height,
                config.CellSize,
                config.LiveDensity);
            generation = 0;
            isPaused = false;
        }

        static void Render()
        {
            Console.SetCursorPosition(0, 0);
            for (int row = 0; row < board.Rows; row++)
            {
                for (int col = 0; col < board.Columns; col++)   
                {
                    var cell = board.Cells[col, row];
                    if (cell.IsAlive)
                    {
                        Console.Write('*');
                    }
                    else
                    {
                        Console.Write(' ');
                    }
                }
                Console.Write('\n');
            }
        }
        static void Save(string path)
        {
            using (var writer = new StreamWriter(path))
            {
                for (int y = 0; y < board.Rows; y++)
                {
                    for (int x = 0; x < board.Columns; x++)
                    {
                        writer.Write(board.Cells[x, y].IsAlive ? '1' : '0');
                    }
                    writer.WriteLine();
                }
            }
        }

        static void Load(string path)
        {
            if (!File.Exists(path))
            {
                Console.WriteLine("\nФайл не найден");
                Console.ReadKey();
                return;
            }

            var lines = File.ReadAllLines(path);

            for (int y = 0; y < board.Rows; y++)
            {
                for (int x = 0; x < board.Columns; x++)
                {
                    board.Cells[x, y].IsAlive = lines[y][x] == '1';
                }
            }
            generation = 0;
        }

        static int CountAlive()
        {
            return board.Cells.Cast<Cell>().Count(c => c.IsAlive);
        }

        static int StepsToStable(int stableSteps = 10, int maxSteps = 1000)
        {
            int stableCount = 0;
            int prev = CountAlive();
            int steps = 0;
            while (stableCount < stableSteps && steps < maxSteps)
            {
                board.Advance();
                steps++;
                int current = CountAlive();
                if (current == prev)
                    stableCount++;
                else
                    stableCount = 0;
                prev = current;
            }

            return steps;
        }

        static void RunExperiments()
        {
            var savedBoard = board;
            var savedGeneration = generation;
            
            var basePath = Directory.GetParent(AppContext.BaseDirectory).Parent.Parent.Parent.FullName;
            var filePath = Path.Combine(basePath, "..", "Data", "data.txt");
            filePath = Path.GetFullPath(filePath);

            using (var writer = new StreamWriter(filePath))
            {
                for (double density = 0.1; density <= 0.9; density += 0.1)
                {
                    int totalSteps = 0;
                    int runs = 10;

                    for (int i = 0; i < runs; i++)
                    {
                        board = new Board(50, 20, 1, density);
                        totalSteps += StepsToStable();
                    }

                    int avg = totalSteps / runs;
                    writer.WriteLine($"{density:F1} {avg}");
                }
            }
            board = savedBoard;
            generation = savedGeneration;
            
            Console.WriteLine("Данные сохранены в файл data.txt");
        }

        static List<(int x, int y)> GetCluster(int startX, int startY, bool[,] visited)
        {
            var cluster = new List<(int, int)>();
            var queue = new Queue<(int x, int y)>();

            queue.Enqueue((startX, startY));
            visited[startX, startY] = true;

            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                cluster.Add((x, y));

                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;

                        int nx = x + dx;
                        int ny = y + dy;

                        if (nx < 0 || nx >= board.Columns ||
                            ny < 0 || ny >= board.Rows)
                            continue;

                        if (!visited[nx, ny] && board.Cells[nx, ny].IsAlive)
                        {
                            visited[nx, ny] = true;
                            queue.Enqueue((nx, ny));
                        }
                    }
                }
            }

            return cluster;
        }

        static List<(int x, int y)> Normalize(List<(int x, int y)> cluster)
        {
            int minX = cluster.Min(c => c.x);
            int minY = cluster.Min(c => c.y);

            return cluster
                .Select(c => (c.x - minX, c.y - minY))
                .OrderBy(c => c.Item1)
                .ThenBy(c => c.Item2)
                .ToList();
        }
        static int CountClusters()
        {
            bool[,] visited = new bool[board.Columns, board.Rows];
            int clusters = 0;

            for (int x = 0; x < board.Columns; x++)
            {
                for (int y = 0; y < board.Rows; y++)
                {
                    if (!visited[x, y] && board.Cells[x, y].IsAlive)
                    {
                        GetCluster(x, y, visited);
                        clusters++;
                    }
                }
            }

            return clusters;
        }

        static bool AreEqual(List<(int, int)> a, List<(int, int)> b)
        {
            if (a.Count != b.Count) return false;

            for (int i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i])
                    return false;
            }

            return true;
        }

        static string Classify(List<(int x, int y)> cluster)
        {
            var norm = Normalize(cluster);

            if (AreEqual(norm, Block)) return "Блок";
            if (AreEqual(norm, Barge)) return "Баржа";
            if (AreEqual(norm, Snake)) return "Змея";
            if (AreEqual(norm, Boat)) return "Лодка";
            if (AreEqual(norm, Beehive)) return "Улей";

            return "Неизвестно";
        }

        static void AnalyzeClusters()
        {
            bool[,] visited = new bool[board.Columns, board.Rows];

            var stats = new Dictionary<string, int>();

            for (int x = 0; x < board.Columns; x++)
            {
                for (int y = 0; y < board.Rows; y++)
                {
                    if (!visited[x, y] && board.Cells[x, y].IsAlive)
                    {
                        var cluster = GetCluster(x, y, visited);
                        var type = Classify(cluster);

                        if (!stats.ContainsKey(type))
                            stats[type] = 0;

                        stats[type]++;
                    }
                }
            }

            foreach (var kv in stats)
            {
                Console.WriteLine($"{kv.Key}: {kv.Value}");
            }
        }

        static void PlotGraph()
        {
            var basePath = Directory.GetParent(AppContext.BaseDirectory).Parent.Parent.Parent.FullName;
            var filePath = Path.Combine(basePath, "..", "Data", "data.txt");
            filePath = Path.GetFullPath(filePath);
            if (!File.Exists(filePath))
            {
                Console.WriteLine("\nФайл data.txt не найден! Сначала запустите эксперименты (клавиша D).");
                Console.ReadKey();
                return;
            }
            var lines = File.ReadAllLines(filePath);
            var densities = new List<double>();
            var steps = new List<double>();
            foreach (var line in lines)
            {
                var parts = line.Split(' ');
                if (parts.Length == 2)
                {
                    densities.Add(double.Parse(parts[0]));
                    steps.Add(double.Parse(parts[1]));
                }
            }
            var plt = new ScottPlot.Plot();
            var scatter = plt.Add.Scatter(densities.ToArray(), steps.ToArray());
            scatter.MarkerSize = 10;
            scatter.Color = ScottPlot.Color.FromHex("#1E90FF");
            plt.XLabel("Плотность живых клеток");
            plt.YLabel("Среднее число шагов до стабилизации");
            plt.Title("Зависимость шагов до стабилизации от плотности");
            plt.Grid.IsVisible = true;
            string plotPath = Path.Combine(Path.GetDirectoryName(filePath), "plot.png");
            plt.SavePng(plotPath, 800, 600);
            Console.WriteLine($"\nГрафик сохранён: {plotPath}");
            Console.ReadKey();
        }
        
        static void Main(string[] args)
        {
            config = LoadConfig();
            Reset();
            bool running = true;
            while(running)
            {
                Console.Clear();
                Render();
                Console.WriteLine($"Поколение: {generation}");
                Console.WriteLine($"Живых клеток: {CountAlive()} | Коллекций: {CountClusters()}");
                Console.WriteLine("\nS - сохранить | L - загрузить | R - обновить | D - загрузить в data.txt | P - play/pause | A - анализ типов | Q - выход");

                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true).Key;

                    if (key == ConsoleKey.S)
                        Save("save.txt");

                    if (key == ConsoleKey.L)
                    {
                        isPaused = true;
                        Console.Write("\nВведите путь к файлу: ");
                        string path = Console.ReadLine();
                        Load(path);
                    }

                    if (key == ConsoleKey.R)
                        Reset();

                    if (key == ConsoleKey.D)
                    {
                        isPaused = true;
                        RunExperiments();
                        PlotGraph();
                        isPaused = false;
                    }
                    
                    if (key == ConsoleKey.P)
                        isPaused = !isPaused;

                    if (key == ConsoleKey.A)
                        AnalyzeClusters();

                    if (key == ConsoleKey.Q)
                        running = false;
                }
                if (!isPaused)
                {
                    board.Advance();
                    generation++;
                }
                Thread.Sleep(config.Delay);
            }
        }
    }
}