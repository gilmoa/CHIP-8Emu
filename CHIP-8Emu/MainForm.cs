using System;
using System.Drawing;
using System.Windows.Forms;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;

namespace CHIP_8Emu
{
    public partial class MainForm : Form
    {
        private Chip8 CPU;
        private Bitmap screen;

        public MainForm()
        {
            InitializeComponent();

            screen = new Bitmap(64, 32, PixelFormat.Format32bppArgb);
            screenPB.Image = screen;
        }

        protected override void OnLoad(EventArgs e)
        {
            CPU = new Chip8(DrawScreen, Beep);

            CPU.LoadROM("rom/PONG");

            while(true)
            {
                CPU.Cycle();
            }
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
            Console.Beep(500, ms);
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
