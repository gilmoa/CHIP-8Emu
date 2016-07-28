using System;
using System.Drawing;
using System.Windows.Forms;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System.Media;

namespace CHIP_8Emu
{
    public partial class MainForm : Form
    {
        private Chip8 Chip8;
        private Bitmap screen;
        private TimeSpan clock = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 400);
        private TimeSpan screenClock = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60);
        private SoundPlayer beepSound = new SoundPlayer(Properties.Resources.beep_sound);
        private string ROM;

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
            
            Task.Run(CPULoop);
            Task.Run(TimersLoop);
        }

        // CPULoop should run at 400Hz
        Task CPULoop()
        {
            while (true)
            {
                Chip8.Cycle();
                Thread.Sleep(clock);
            }
        }

        // TimersLoop should run at 60Hz
        Task TimersLoop()
        {
            while (true)
            {
                Chip8.TimerCycle();
                Thread.Sleep(screenClock);
            }
        }

        // KeyMap for general hardware KeyPad
        // 1 2 3 C |=> 1 2 3 4
        // 4 5 6 D |=> Q W E R
        // 7 8 9 E |=> A S D F
        // A 0 B F |=> Z X C V
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
            // CTRL + R = Reset
            if(e.Control && e.KeyCode == Keys.R)
            {
                Chip8.Sleep();
                Chip8.Reset();
                Chip8.LoadROM(ROM);
                Chip8.Wake();
            }
            // CTRL + O = Open
            else if(e.Control && e.KeyCode == Keys.O)
            {
                Chip8.Sleep();
                OpenROM();
                Chip8.Reset();
                Chip8.LoadROM(ROM);
                Chip8.Wake();
            }
            // Send key press to Chip if mapped
            else if (KeyMap.ContainsKey(e.KeyCode))
                Chip8.KeyDown(KeyMap[e.KeyCode]);
        }

        private void MapKeyUp(object sender, KeyEventArgs e)
        {
            // Send key release to Chip if mapped
            if (KeyMap.ContainsKey(e.KeyCode))
                Chip8.KeyUp(KeyMap[e.KeyCode]);
        }

        // DrawScreen function for Chip8()
        private void DrawScreen(bool[,] gfx)
        {
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 64; x++)
                {
                    screen.SetPixel(x, y, gfx[x, y] ? Color.White : Color.Black);
                }

            screenPB.Image = ResizeBitmap(screen, screenPB.Width, screenPB.Height);
        }

        // Beep function for Chip8()
        private void Beep(int ms)
        {
            beepSound.Play();
        }

        // Select MOD with Dialog
        private void OpenROM()
        {
            OpenFileDialog fd = new OpenFileDialog();
            fd.ShowDialog();
            ROM = fd.FileName;
            this.Text = "CHIP-8 Emulator : " + ROM.Substring(ROM.Length-10, 10);
        }

        // Resize Bitmap
        // We can work on 64x32 pixel and then scale with proper interpolation
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
