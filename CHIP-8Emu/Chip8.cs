using System;
using System.Collections.Generic;
using System.IO;

namespace CHIP_8Emu
{
    class Chip8
    {
        private const int SWidth = 64;                      // Fixed screen is 64x32
        private const int SHeight = 32;

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
                { 0x0, ClearOrRtn },
                { 0x1, Jmp },
                { 0x2, Call },
                { 0x3, SkipIfXEqual },
                { 0x4, SkipIfXNotEqual },
                { 0x5, SkipIfXEqualY },
                { 0x6, SetX },
                { 0x7, AddX },
                { 0x8, Arith },
                { 0x9, SkipIfXNotEqualY },
                { 0xa, SetI },
                { 0xb, JmpOffset },
                { 0xc, RndAndX },
                { 0xd, DrawSprite },
                { 0xe, SkipKeyed },
                { 0xf, More }
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
            // Gfx
            lines.Add("GFX".PadRight(15, '='));
            for (int y = 0; y < SHeight; y++)
            {
                string gfxLine = "";
                for (int x = 0; x < SWidth; x++)
                    gfxLine += gfx[x, y] ? "1" : "0";
                lines.Add(gfxLine);
            }
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
                S   = (byte)((op & 0xf000) >> 12),
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
            OpCodes[OpCode.S](OpCode);

            // Update timers

        }

        #region OpCodes Implementations
        //
        // OPCODES IMPLEMENTATION
        // DESCRIPTION: https://en.wikipedia.org/wiki/CHIP-8#Opcode_table
        //

        // 0x0
        // 00E0 - Clears the screen.
        // 00EE - Returns from a subroutine.
        private void ClearOrRtn(OpCodeType op)
        {
            UnimplementedInstruction();
        }

        // 0x1
        // 1NNN - Jumps to address NNN.
        private void Jmp(OpCodeType op)
        {
            UnimplementedInstruction();
        }

        // 0x2
        // 2NNN - Calls subroutine at NNN.
        private void Call(OpCodeType op)
        {
            UnimplementedInstruction();
        }

        // 0x3
        // 3XNN - Skips the next instruction if VX equals NN.
        private void SkipIfXEqual(OpCodeType op)
        {
            UnimplementedInstruction();
        }

        // 0x4
        // 4XNN - Skips the next instruction if VX doesn't equal NN.
        private void SkipIfXNotEqual(OpCodeType op)
        {
            UnimplementedInstruction();
        }

        // 0x5
        // 5XY0 - Skips the next instruction if VX equals VY.
        private void SkipIfXEqualY(OpCodeType op)
        {
            UnimplementedInstruction();
        }

        // 0x6
        // 6XNN - Sets VX to NN.
        private void SetX(OpCodeType op)
        {
            V[op.X] = op.NN;
        }

        // 0x7
        // 7XNN - Adds NN to VX.
        private void AddX(OpCodeType op)
        {
            UnimplementedInstruction();
        }

        // 0x8
        // Arithmetic switch on 0x000f.
        private void Arith(OpCodeType op)
        {
            UnimplementedInstruction();
        }

        // 0x9
        // 9XY0 - Skips the next instruction if VX doesn't equal VY.
        private void SkipIfXNotEqualY(OpCodeType op)
        {
            UnimplementedInstruction();
        }

        // 0xa
        // ANNN - Sets I to the address NNN.
        private void SetI(OpCodeType op)
        {
            I = op.NNN;
        }

        // 0xb
        // BNNN - Jumps to the address NNN plus V0.
        private void JmpOffset(OpCodeType op)
        {
            UnimplementedInstruction();
        }

        // 0xc
        // CXNN - Sets VX to the result of a bitwise and operation on
        //        a random number and NN.
        private void RndAndX(OpCodeType op)
        {
            UnimplementedInstruction();
        }

        // 0xd
        // DXYN - Draws a sprite at coordinate (VX, VY) that has a width of
        //        8 pixels and a height of N pixels. Each row of 8 pixels is
        //        read as bit-coded starting from memory location I; I value
        //        doesn’t change after the execution of this instruction. 
        //        As described above, VF is set to 1 if any screen pixels are
        //        flipped from set to unset when the sprite is drawn, and to
        //        0 if that doesn’t happen.
        private void DrawSprite(OpCodeType op)
        {
            UnimplementedInstruction();
        }

        // 0xe
        // EX9E - Skips the next instruction if the key stored in VX is pressed.
        // EXA1 - Skips the next instruction if the key stored in VX isn't pressed.
        private void SkipKeyed(OpCodeType op)
        {
            UnimplementedInstruction();
        }

        // 0xf
        // Operation switch on 0x00ff.
        private void More(OpCodeType op)
        {
            UnimplementedInstruction();
        }

        #endregion
    }
}
