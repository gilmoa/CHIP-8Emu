using System;
using System.Drawing;
using System.Windows.Forms;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System.Media;
using System.Diagnostics;

namespace CHIP_8Emu
{
    public partial class MainForm : Form
    {
        private Chip8 Chip8;
        private Bitmap screen;
        private TimeSpan clock = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 400);
        private TimeSpan screenClock = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60);
        private string ROM = "rom/GILMO";
        private SoundPlayer beepSound = new SoundPlayer("beep.wav");

        public MainForm()
        {
            InitializeComponent();
            
            screen = new Bitmap(64, 32, PixelFormat.Format32bppArgb);
            screenPB.Image = screen;

            KeyDown += MapKeyDown;
            KeyUp += MapKeyUp;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            Chip8 = new Chip8(DrawScreen, Beep);
            
            Chip8.LoadROM(ROM);
            
            Task.Run(CPULoop);
            Task.Run(TimersLoop);
        }

        Task CPULoop()
        {
            while (true)
            {
                Chip8.Cycle();
                Thread.Sleep(clock);
            }
        }

        Task TimersLoop()
        {
            while (true)
            {
                Chip8.TimerCycle();
                Thread.Sleep(screenClock);
            }
        }

        private Dictionary<Keys, byte> KeyMap = new Dictionary<Keys, byte>()
        {
            { Keys.D1, 0x1 }, { Keys.D2, 0x2 },
            { Keys.D3, 0x3 }, { Keys.D4, 0xc },

            { Keys.Q, 0x4 }, { Keys.W, 0x5 },
            { Keys.E, 0x6 }, { Keys.R, 0xd },

            { Keys.A, 0x7 }, { Keys.S, 0x8 },
            { Keys.D, 0x9 }, { Keys.F, 0xe },

            { Keys.Z, 0xa }, { Keys.X, 0x0 },
            { Keys.C, 0xb }, { Keys.V, 0xf }
        };

        private void MapKeyDown(object sender, KeyEventArgs e)
        {
            if(e.Control && e.KeyCode == Keys.R)
            {
                Chip8.Sleep();
                Chip8.Reset();
                Chip8.LoadROM(ROM);
                Chip8.Wake();
            }
            else if(e.Control && e.KeyCode == Keys.O)
            {
                Chip8.Sleep();
                OpenROM();
                Chip8.Reset();
                Chip8.LoadROM(ROM);
                Chip8.Wake();
            }
            else if (KeyMap.ContainsKey(e.KeyCode))
                Chip8.KeyDown(KeyMap[e.KeyCode]);
        }

        private void MapKeyUp(object sender, KeyEventArgs e)
        {
            if (KeyMap.ContainsKey(e.KeyCode))
                Chip8.KeyUp(KeyMap[e.KeyCode]);
        }

        private void DrawScreen(bool[,] gfx)
        {
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 64; x++)
                {
                    screen.SetPixel(x, y, gfx[x, y] ? Color.White : Color.Black);
                }

            screenPB.Image = ResizeBitmap(screen, screenPB.Width, screenPB.Height);
        }

        private void Beep(int ms)
        {
            beepSound.Play();
        }

        private void OpenROM()
        {
            OpenFileDialog fd = new OpenFileDialog();
            fd.ShowDialog();
            ROM = fd.FileName;
            this.Text = "CHIP-8 Emulator : " + ROM.Substring(ROM.Length-10, 10);
        }

        private Bitmap ResizeBitmap(Bitmap original, int width, int height, InterpolationMode interpolation = InterpolationMode.NearestNeighbor)
        {
            Bitmap res = new Bitmap(width, height);
            Graphics g = Graphics.FromImage(res);
            g.InterpolationMode = interpolation;
            g.DrawImage(original, 0, 0, width, height);

            return res;
        }
    }       
}
