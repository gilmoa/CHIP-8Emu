using System;
using System.Collections.Generic;
using System.IO;

namespace CHIP_8Emu
{
    class Chip8
    {
        private const int SWidth = 64;              // Fixed screen is 64x32
        private const int SHeight = 32;

        // Hardware specific function
        private Action<bool[,]> Draw;               // Draw on screen               
        private Action<int> Beep;                        // Beep

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

        // Draw only when needed
        private bool drawFlag = false;

        // Running state
        private bool running = true;

        // Instructions need random number
        Random rnd = new Random();

        // OpCodes
        private Dictionary<byte, Action<OpCodeType>> OpCodes;

        // Key currenty Pressed
        List<byte> keyPressed = new List<byte>();

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
        public Chip8(Action<bool[,]> Draw, Action<int> Beep)
        {
            this.Draw = Draw;
            this.Beep = Beep;

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
            keyPressed.Clear();                         // Pressed keys

            // Load first 0x200 bytes with fontset
            LoadMemory(FontSet, 0x00);
        }

        // Halt execution
        public void Sleep()
        {
            running = false;
        }

        // Continue execution
        public void Wake()
        {
            running = true;
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

        // Handle Bad OpCodes
        private void BadOpCode()
        {
            // Back to last OpCode
            pc -= 2;
            ushort opcode = GetOpCode();

            DumpCPUState();

            System.Windows.Forms.MessageBox.Show(
                "Bad OpCode:\n\n" + opcode.ToString("x04") + " - found at 0x" + pc.ToString("x04") + "." +
                "\n\nIs not a valid OpCode." +
                "\n\nCPU State Dumped to 'cpu.dump'.",
                "Bad OpCode",
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Error);

            Environment.Exit(1);
        }

        // Main CPU cycle
        // Fetch OpCode, increment pc and run instruction
        // Should run at 400Hz
        public void Cycle()
        {
            // Check if we are running
            if (!running)
                return;

            // Fetch Opcode
            OpCodeType OpCode = ParseOpCode(GetOpCode());

            // Increment pc
            pc += 2;

            // Run Instruction
            OpCodes[OpCode.S](OpCode);
        }

        // Cycle for timers and screen
        // Should run at 60Hz
        public void TimerCycle()
        {
            // Check if we are running
            if (!running)
                return;

            // Update timers
            if (delayTimer > 0)
                delayTimer--;

            if (soundTimer > 0)
            {
                Beep(soundTimer * (1000 / 60));
                soundTimer = 0;
            }

            //Draw
            if (drawFlag)
            {
                Draw(gfx);
                drawFlag = false;
            }
        }

        // Track key state Pressed
        public void KeyDown(byte key)
        {
            if(!keyPressed.Contains(key))
                keyPressed.Add(key);
        }

        // Track key state Released
        public void KeyUp(byte key)
        {
            keyPressed.Remove(key);
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
            switch(op.NNN)
            {
                // 00E0 - Clears the screen.
                case 0x0e0:
                    Array.Clear(gfx, 0, gfx.Length);
                    break;
                // 00EE - Returns from a subroutine.
                case 0x0ee:
                    pc = stack[--sp];
                    break;
                default:
                    UnimplementedInstruction();
                    break;
            }
        }

        // 0x1
        // 1NNN - Jumps to address NNN.
        private void Jmp(OpCodeType op)
        {
            pc = op.NNN;
        }

        // 0x2
        // 2NNN - Calls subroutine at NNN.
        private void Call(OpCodeType op)
        {
            stack[sp++] = pc;
            pc = op.NNN;
        }

        // 0x3
        // 3XNN - Skips the next instruction if VX equals NN.
        private void SkipIfXEqual(OpCodeType op)
        {
            if (V[op.X] == op.NN) pc += 2;
        }

        // 0x4
        // 4XNN - Skips the next instruction if VX doesn't equal NN.
        private void SkipIfXNotEqual(OpCodeType op)
        {
            if (V[op.X] != op.NN) pc += 2;
        }

        // 0x5
        // 5XY0 - Skips the next instruction if VX equals VY.
        private void SkipIfXEqualY(OpCodeType op)
        {
            if (op.N != 0x0)
                BadOpCode();
            if (V[op.X] == V[op.Y])
                pc += 2;
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
            V[op.X] += op.NN;
        }

        // 0x8
        // Arithmetic switch on 0x000f.
        private void Arith(OpCodeType op)
        {
            switch(op.N)
            {
                // 8XY0 - Sets VX to the value of VY.
                case 0x0:
                    V[op.X] = V[op.Y];
                    break;
                // 8XY1 - Sets VX to VX or VY.
                case 0x1:
                    V[op.X] |= V[op.Y];
                    break;
                // 8XY2 - Sets VX to VX and VY.
                case 0x2:
                    V[op.X] &= V[op.Y];
                    break;
                // 8XY3 - Sets VX to VX xor VY.
                case 0x3:
                    V[op.X] ^= V[op.Y];
                    break;
                // 8XY4 - Adds VY to VX. VF is set to 1 when there's
                //        a carry, and to 0 when there isn't.
                case 0x4:
                    V[0xf] = (byte)((V[op.X] + V[op.Y]) > 0xff ? 1 : 0);
                    V[op.X] += V[op.Y];
                    break;
                // 8XY5 - VY is subtracted from VX. VF is set to 0
                //        when there's a borrow, and 1 when there isn't.
                case 0x5:
                    V[0xf] = (byte)(V[op.Y] > V[op.X] ? 0 : 1);
                    V[op.X] -= V[op.Y];
                    break;
                // 8XY6 - Shifts VX right by one. VF is set to the value
                //        of the least significant bit of VX before the shift.
                case 0x6:
                    V[0xf] = (byte)(V[op.X] & 0x01);
                    V[op.X] /= 2;
                    break;
                // 8XY7	- Sets VX to VY minus VX. VF is set to 0 when 
                //        there's a borrow, and 1 when there isn't.
                case 0x7:
                    V[0xf] = (byte)(V[op.X] > V[op.Y] ? 0 : 1);
                    V[op.X] = (byte)(V[op.Y] - V[op.X]);
                    break;
                // 8XYE - Shifts VX left by one. VF is set to the value
                //        of the most significant bit of VX before the shift.
                case 0xe:
                    V[0xf] = (byte)(V[op.X] & 0x80);
                    V[op.X] *= 2;
                    break;
                default:
                    UnimplementedInstruction();
                    break;
            }
        }

        // 0x9
        // 9XY0 - Skips the next instruction if VX doesn't equal VY.
        private void SkipIfXNotEqualY(OpCodeType op)
        {
            if (op.N != 0x0)
                BadOpCode();
            if (V[op.X] != V[op.Y])
                pc += 2;
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
            V[op.X] = (byte)(rnd.Next(0, 256) & op.NN);
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
            byte startX = V[op.X];
            byte startY = V[op.Y];
            byte h = op.N;

            V[0xf] = 0;

            for (int i = 0; i < h; i++)
            {
                byte spriteByte = memory[I + i];
                for (int bit = 0; bit < 8; bit++)
                {
                    int x = (startX + bit);
                    int y = (startY + i);
                    if (x >= SWidth || y >= SHeight)
                        continue;

                    byte spriteBit = (byte)((spriteByte >> (7 - bit)) & 0x01);
                    byte oldBit = (byte)(gfx[x, y] ? 1 : 0);
                    if (spriteBit != oldBit)
                        drawFlag = true;

                    byte newBit = (byte)(spriteBit ^ oldBit);
                    gfx[x, y] = (newBit == 0x01) ? true : false;

                    if (oldBit == 0x01 && newBit == 0x00)
                        V[0xf] = 1;
                }
            }
        }

        // 0xe
        // EX9E - Skips the next instruction if the key stored in VX is pressed.
        // EXA1 - Skips the next instruction if the key stored in VX isn't pressed.
        private void SkipKeyed(OpCodeType op)
        {
            switch(op.NN)
            {
                // EX9E - Skips the next instruction if the key stored in VX is pressed.
                case 0x9e:
                    if (keyPressed.Contains(V[op.X]))
                        pc += 2;
                    break;
                // EXA1 - Skips the next instruction if the key stored in VX isn't pressed.
                case 0xa1:
                    if (!keyPressed.Contains(V[op.X]))
                        pc += 2;
                    break;
                default:
                    UnimplementedInstruction();
                    break;
            }
        }

        // 0xf
        // Operation switch on 0x00ff.
        private void More(OpCodeType op)
        {
            switch(op.NN)
            {
                // FX07 - Sets VX to the value of the delay timer.
                case 0x07:
                    V[op.X] = delayTimer;
                    break;
                // FX0A - A key press is awaited, and then stored in VX.
                case 0x0a:
                    if (keyPressed.Count > 0)
                        V[op.X] = keyPressed[0];
                    else
                        pc -= 2;
                    break;
                // FX15 - Sets the delay timer to VX.
                case 0x15:
                    delayTimer = V[op.X];
                    break;
                // FX18 - Sets the sound timer to VX.
                case 0x18:
                    soundTimer = V[op.X];
                    break;
                // FX1E - Adds VX to I. VF is set to 1 when range 
                //        overflow (I+VX>0xFFF), and 0 when there isn't.
                case 0x1e:
                    V[0xf] = (byte)((I + V[op.X]) > 0xfff ? 1 : 0);
                    I += V[op.X];
                    break;
                // FX29 - Sets I to the location of the sprite for the character
                //        in VX. Characters 0-F (in hexadecimal) are represented
                //        by a 4x5 font.
                case 0x29:
                    I = (ushort)(V[op.X] * 5);
                    break;
                // FX33 - Stores the binary-coded decimal representation of VX,
                //        with the most significant of three digits at the
                //        address in I, the middle digit at I plus 1, and
                //        the least significant digit at I plus 2.
                case 0x33:
                    memory[I]     = (byte)((V[op.X] / 100) % 10);
                    memory[I + 1] = (byte)((V[op.X] / 10) % 10);
                    memory[I + 2] = (byte)(V[op.X] % 10);
                    break;
                // FX55 - Stores V0 to VX (including VX) in memory starting
                //        at address I.
                case 0x55:
                    for (int i = 0; i <= op.X; i++)
                        memory[I + i] = V[i];
                    break;
                // FX65 - Fills V0 to VX (including VX) with values from
                //        memory starting at address I.
                case 0x65:
                    for (int i = 0; i <= op.X; i++)
                        V[i] = memory[I + i];
                    break;
                default:
                    UnimplementedInstruction();
                    break;
            }
        }

        #endregion
    }
}
