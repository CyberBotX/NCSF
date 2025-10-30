# NCSF123

A command-line program to play or output an `NCSF`.

In play mode, NCSF123 will play the `NCSF` to either a specified sound device or the default sound device if one is not given.

In output mode, NCSF123 will either create a RIFF WAVE for the `NCSF` or send the audio data to standard output for piping to other
programs.

To see how to use this, you can run `NCSF123.exe -h`.
If you have downloaded the repository, you could also look at the Program.docopt.txt file for this information.

The program uses a library called [SoundFlow](https://github.com/LSXPrime/SoundFlow) for play mode. It also uses its own `Stream` class,
called `NCSFPlayerStream`, to facilitate `SSEQ` playback. Since the program is a .NET program, it can be referenced by other .NET
assemblies, in case someone wants to gain access to the `NCSFPlayerStream` class from their own code.

See the [NCSF](https://github.com/CyberBotX/NCSF) repository for other libraries and utilties, as well as license.
