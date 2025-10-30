# NDStoNCSF

A command-line program for creating an `NCSF` from a Nintendo DS ROM (although it could also work on a standalone `SDAT` too).

**NOTE:** While it is true that a standalone `SDAT` could be used, the ncsflib file that would be created from that would not use the game
code like you'd get if you provided a Nintendo DS ROM. In the event that a standalone `SDAT` is used, the ncsflib will instead be named
based on the filename of the `SDAT` provided.

To see how to use this, you can run `NDStoNCSF.exe -h`.
If you have downloaded the repository, you could also look at the Program.docopt.txt file for this information.

See the [NCSF](https://github.com/CyberBotX/NCSF) repository for other libraries and utilties, as well as license.
