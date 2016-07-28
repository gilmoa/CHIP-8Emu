# CHIP-8Emu
My test at writing an emulator.

***

## TODOs
 1. Proper `Beep`, possibly not halting `TimerCycle()` DUH.
 2. Input might still need fixing.
 3. `0xDXYN` looks like shit and might not be working as intended.
 4. `DrawScreen()` works fine but might need a review.
 5. Still missing OpCode `0xBNNN`. Can't test it.

***

## Tested
The emulator has been tested with the following ROMs from [David Winter](http://www.pong-story.com/chip8/):
> * 15PUZZLE
> * BLINKY
> * BLITZ
> * BREAKOUT
> * BRIX
> * CONNECT4
> * GUESS `*`
> * HIDDEN `*`
> * INVADERS
> * KALEID
> * MAZE
> * MERLIN
> * MISSILE
> * PONG
> * PONG2
> * PUZZLE `*`
> * SQUASH
> * SYZYGY
> * TANK
> * TETRIS
> * TICTAC
> * UFO
> * VBRIX
> * VERS
> * WALL
> * WIPEOFF

`*` = Speed is off, game running too fast.
