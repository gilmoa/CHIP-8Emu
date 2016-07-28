# CHIP-8Emu
My test at writing an emulator. It works fine, not top notch, 
but I tell myself it's because it wasn't meant to be good in 
the first place.

***

## TODOs
 1. `Beep()` still confusing.
 2. `Thread.Sleep()` needs to be replaced by proper time based delay.
 4. `0xDXYN` looks like shit and might not (but should) be working as intended.
 5. `DrawScreen()` works fine but I don't really like it.

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
