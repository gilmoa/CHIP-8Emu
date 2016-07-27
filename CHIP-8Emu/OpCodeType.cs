namespace CHIP_8Emu
{
    // CHIP 8 opcode definition
    struct OpCodeType
    {
        public ushort opcode;
        public ushort NNN;
        public byte X, Y, N, NN;
    }
}
