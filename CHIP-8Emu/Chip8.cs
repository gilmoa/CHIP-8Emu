using System;
using System.Collections.Generic;
using System.IO;

namespace CHIP_8Emu
{
    class Chip8
    {
        const int SWidth = 64;                      // Fixed screen is 64x32
        const int SHeight = 32;

        // Hardware specific function
        private Action<bool[,]> draw;               // Draw on screen               
        private Action<int> beep;                   // Beep

        private byte[] memory = new byte[0x1000];   // 4K 8-bit memory
        private byte[] V = new byte[16];            // 16 8-bit registers
        private ushort I;                           // 16-bit address register
        private ushort pc = 0x200;                  // Program Counter

        private ushort[] stack = new ushort[16];    // 16 level stack
        private byte sp;                            // 8-bit stack pointer

        // pixel state
        private bool[,] gfx = new bool[SWidth, SHeight];

        // Timers
        private byte delayTimer;
        private byte soundTimer;

        // draw only when needed
        private bool drawFlag = false;

        // Instructions need random number
        Random rnd = new Random();

        // OpCodes
        private Dictionary<byte, Action<OpCodeType>> OpCodes;

        // Key currenty Pressed
        HashSet<byte> pressedKeys = new HashSet<byte>();

        // Standard CHIP 8 Fontset
        private byte[] FontSet = new byte[]
        {
            0xF0, 0x90, 0x90, 0x90, 0xF0,
            0x20, 0x60, 0x20, 0x20, 0x70,
            0xF0, 0x10, 0xF0, 0x80, 0xF0,
            0xF0, 0x10, 0xF0, 0x10, 0xF0,
            0x90, 0x90, 0xF0, 0x10, 0x10,
            0xF0, 0x80, 0xF0, 0x10, 0xF0,
            0xF0, 0x80, 0xF0, 0x90, 0xF0,
            0xF0, 0x10, 0x20, 0x40, 0x40,
            0xF0, 0x90, 0xF0, 0x90, 0xF0,
            0xF0, 0x90, 0xF0, 0x10, 0xF0,
            0xF0, 0x90, 0xF0, 0x90, 0x90,
            0xE0, 0x90, 0xE0, 0x90, 0xE0,
            0xF0, 0x80, 0x80, 0x80, 0xF0,
            0xE0, 0x90, 0x90, 0x90, 0xE0,
            0xF0, 0x80, 0xF0, 0x80, 0xF0,
            0xF0, 0x80, 0xF0, 0x80, 0x80
        };
        
        // Constructor allow hardware emulation to pass custom
        // functions to handle Draw to screen and Sound Beep
        public Chip8(Action<bool[,]> draw, Action<int> beep)
        {
            this.draw = draw;
            this.beep = beep;

            Reset();

            // OpCodes definition
            OpCodes = new Dictionary<byte, Action<OpCodeType>>()
            {
                //{ 0x0, ClearOrRtn },
                //{ 0x1, Jmp },
                //{ 0x2, Call },
                //{ 0x3, SkipIfXEqual },
                //{ 0x4, SkipIfXNotEqual },
                //{ 0x5, SkipIfXEqualY },
                //{ 0x6, SetX },
                //{ 0x7, AddX },
                //{ 0x8, Arith },
                //{ 0x9, SkipIfXNotEqualY },
                //{ 0xa, SetI },
                //{ 0xb, JmpOffset },
                //{ 0xc, RndAndX },
                //{ 0xd, DrawSprite },
                //{ 0xe, SkipKeyed },
                //{ 0xd, More }
            };
        }

        // Reset CPU starting state
        public void Reset()
        {
            Array.Clear(memory, 0, memory.Length);      // memory
            Array.Clear(V, 0, V.Length);                // registers
            Array.Clear(stack, 0, stack.Length);        // stack
            Array.Clear(gfx, 0, gfx.Length);            // pixel states

            I = 0;                                      // Address counter
            sp = 0;                                     // Stack pointer

            pc = 0x200;                                 // Program counter
                                                        // Execution starts
                                                        // at 0x200

            // Timers
            delayTimer = 0;                             
            soundTimer = 0;

            rnd = new Random();                         // Random
            pressedKeys.Clear();                        // Pressed keys

            // Load first 0x200 bytes with fontset
            LoadMemory(FontSet, 0x00);

        }

        #region DEBUGGING FUNCTIONS
        //
        // SET OF DEBBUGING FUNCTIONS
        //

        // Dump CPU state to file @"cpu.dump"
        private void DumpCPUState()
        {
            List<string> lines = new List<string>();
            lines.Add("CHIP 8 CPU STATE DUMP");
            // Counters
            lines.Add("COUNTERS".PadRight(15, '='));
            lines.Add("I [0x" + I.ToString("x04") + "]     SP [0x" + sp.ToString("x02") +"]     PC [0x" + pc.ToString("x04") + "]");
            // Registers
            lines.Add("REGISTERS".PadRight(15, '='));
            for (int i = 0; i < 16; i += 2)
                lines.Add
                (
                    "V" + i.ToString("X") + " [0x" + V[i].ToString("x04") + "]    " +
                    "V" + (i + 1).ToString("X") + " [0x" + V[(i + 1)].ToString("x04") + "]    " +
                    "S" + i.ToString("X") + " [0x" + stack[i].ToString("x04") + "]    " +
                    "S" + (i + 1).ToString("X") + " [0x" + stack[(i + 1)].ToString("x04") + "]"
                );
            // Memory
            lines.Add("MEMORY".PadRight(15, '='));
            string memoryHeader = "       ";
            for (int x = 0; x <= 0xf; x++)
                memoryHeader += x.ToString("x02") + " ";

            lines.Add(memoryHeader);

            string memoryLine = "0x" + 0.ToString("x04") + " ";
            for (int x = 0; x < memory.Length; x++)
            {
                if (x % 0x10 == 0 && x > 0)
                {
                    lines.Add(memoryLine);
                    memoryLine = "0x" + x.ToString("x04") + " ";
                }
                memoryLine += memory[x].ToString("x02") + " ";
                
            }
            lines.Add(memoryLine);
            // End
            lines.Add("END".PadRight(15, '='));
            // Write out
            File.WriteAllLines(@"cpu.dump", lines.ToArray());
        }

        // Unimplemented Instruction handler
        private void UnimplementedInstruction()
        {
            // Back to last OpCode
            pc -= 2;
            ushort opcode = GetOpCode();

            DumpCPUState();

            System.Windows.Forms.MessageBox.Show(
                "Unimplemented Instruction:\n\n" + opcode.ToString("x04") + " - found at 0x" + pc.ToString("x04") + "." +
                "\n\nCPU State Dumped to 'cpu.dump'.",
                "Unimplemented Instruction",
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Error);

            Environment.Exit(1);
        }

        // Test every screen pixel
        public void TestScreen()
        {
            for (int y = 0; y < SHeight; y++)
            {
                for (int x = 0; x < SWidth; x++)
                {
                    gfx[x, y] = true;
                    draw(gfx);
                }
            }

            Array.Clear(gfx, 0, gfx.Length);
            draw(gfx);
        }

        //
        // END DEBUGGING FUNCTIONS
        //
        #endregion

        // Load full byte array into memory starting at offset
        private void LoadMemory(byte[] buffer, int offset)
        {
            Array.Copy(buffer, 0, memory, offset, buffer.Length);
        }

        // Load program at path into execution memory
        public void LoadROM(string path)
        {
            LoadMemory(File.ReadAllBytes(path), 0x200);
        }

        // Get OpCode from memory
        private ushort GetOpCode()
        {
            return (ushort)((memory[pc] << 8) | (memory[pc + 1]));
        }

        // Parse OpCode to struct
        private OpCodeType ParseOpCode(ushort op)
        {
            return new OpCodeType()
            {
                opcode = op,
                X   = (byte)((op & 0x0f00) >> 8),
                Y   = (byte)((op & 0x00f0) >> 4),
                N   = (byte)(op & 0x000f),
                NN  = (byte)(op & 0x00ff),
                NNN = (ushort)(op & 0x0fff)
            };
        }

        // Main CPU cycle
        // Fetch OpCode, increment pc, run instruction
        public void Cycle()
        {
            // Fetch Opcode
            OpCodeType OpCode = ParseOpCode(GetOpCode());

            // Increment pc
            pc += 2;

            // Run Instruction

            // Update timers
            
        }
    }
}
