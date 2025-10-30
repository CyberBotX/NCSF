Primary repository on [GitHub](https://github.com/CyberBotX/NCSF).

[.NET]: https://dotnet.microsoft.com/en-us/download/dotnet/9.0

# NCSF

NCSF C# Libraries and Utilities by Naram "CyberBotX" Qashat.

This repository is meant to replace the utilities in the [SDATStuff](https://github.com/CyberBotX/SDATStuff) repository. Note that not
all of the utilities from that repository have been ported this this one.

## What is this?

This is a collection of libraries and utilities, written in C#, for working with NCSF files. NCSF is short for
"Nitro Composer Sound Format" and is a PSF-style music format for playing Ninteod DS music that utilized Nintendo's 1st-party SDAT
(Sound Data) container for SSEQ (Sound Sequence) files. Nitro Composer was the internal name of the tools used to create the SDAT
containers. NCSF was created to superceed the 2SF format, which is also for playing DS music. The difference between 2SF and NCSF is that
2SF contains a DS ROM and requires a player that uses a DS emulator to load that ROM and play the music, while NCSF only contains the SDAT
and uses code from a decompilation of Pokémon Diamond to play the music. 2SF could also technically play any format of DS music, while
NCSF specifically only handles music in the SDAT container format.

## History of NCSF

NCSF came about around in 2013 some time after I had attempted to make a 2SF set for Phoenix Wright: Ace Attoeney: Trails and Tribulations,
and was unable to get any audio from the Legacy of Ys driver that most 2SFs use, specifically all the music unique to that game (the 2SF
rip was able to play any of the music from the previous 2 games that was in Trials and Tribulations). While I do not know the exact reason
for why this happened, my gut feeling is that the Legacy of Ys version of the SSEQ player could not handle the position of newer music
within the SDAT.

While I'd had the idea for NCSF for a while, it was upon finding [FeOS Sound System by fincs](https://github.com/fincs/FSS), a library for
the DS homebrew OS, FeOS, that I had a way to parse and play an SSEQ. I ported FSS to work on a PC without a DS, simulating the registers
that the DS contains and finding out how to output the audio correctly. Over time, as I gained more knowledge of the SDAT container and
the SSEQ format, I would eventually extend my version of FSS so it could handle more things correctly, such as some misconceptions about
the PLAYER block and the events for controlling variables and random events. But after some reports that some songs were not being played
back correctly, without being able to find the cause, development on NCSF halted. But in late 2024, a
[decompilation of Pokémon Diamond by pret](https://github.com/pret/pokediamond) was released and the code for playing back SSEQs was there,
written in C, so I was able to utilize that code to replace the now aging FSS code, and this lead to more proper playback.

In short, NCSF could not have existed without FeOS Sound System, some code from DeSmuME (mostly tables that would've normally been in the
DS ARM7 BIOS, along with a few other concepts) and more recently the above mentioned Pokémon Diamond decompilation.

## What is contained in this repository?

* [NCSFCommon](NCSFCommon/): A base library containing classes for the various Nitro Composer components, as well as a ReplayGain analyzer
  and base classes for handling playback of an SSEQ (Channel, Player and Track).
* [NCSFPlayer](NCSFPlayer/): A library containing specialized versions of the Channel, Player and Track classes specifically for playback.
* [NCSFTimer](NCSFTimer/): A library containing specialized versions of the Channel, Player and Track classes specifically for timing of
  SSEQs.
* [2SFTagsToNCSF](2SFTagsToNCSF/): A command-line program for taking tags from an existing 2SF set (specifically the ones which used the
  Legacy of Ys driver) to an NCSF set. This is useful to avoid needing to retag an NCSF set if the contents are the same as a 2SF set.
* [NDStoNCSF](NDStoNCSF/): A command-line program for creating an NCSF from a Nintendo DS ROM (although it could also work on a standalone
  SDAT too).
* [NCSF123](NCSF123/): A command-line program to play or output an NCSF. Play refers to audio playback while output refers to either
  sending the audio data to a RIFF WAVE file or to standard output for use by other programs. While it does not currently exist, being
  able to send to standard output will be utilized by plugins for other audio players, such as Winamp.

## How to use these?

The libraries and utilities in this repository are written in .NET, and currently require [.NET 9][.NET]. To build, you will need to have
the [.NET 9 SDK][.NET] installed. You can either download and use the command-line version, or you can use Visual Studio 2022. If you wish
to just use the pre-built binaries, you only need to ensure you have the [.NET 9 Runtime][.NET] installed.

The programs are command-line only and do not have a GUI currently. Running them with no arguments should print the syntax for the program,
and all of them allow for a `-h`/`--help` argument to print out the full help.

## Contact

I can be contacted about these libraries and utilties in the following ways:
* For bug reports, submit an issue via [GitHub's issue tracker](https://github.com/CyberBotX/NCSF/issues).
* Discord: username `cyberbotx`.
* Email: [cyberbotx@cyberbotx.com](mailto:cyberbotx@cyberbotx.com), please include NCSF in your subject line.

## License

```
The MIT License (MIT)

Copyright (c) 2013-2025 Naram "CyberBotX" Qashat

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
