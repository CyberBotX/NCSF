# NCSFCommon

A base library for `NCSF`, containing classes for the various Nitro Composer components, as well as a ReplayGain analyzer and base classes
for handling playback of an `SSEQ` (Channel, Player and Track). It can both read and write the information for an `SDAT`.

Within the NC directory are the following classes:
* `FATRecord`: Stores the offset and size of a single file from within the `FAT` section of an `SDAT`.
* `FATSection`: Stores all the records of the `FAT` section of an `SDAT` as `FATRecord` objects.
* `INFOEntry`: An abstract class to store an entry within the `INFO` section of an `SDAT`, all of which have a file ID within the `FAT`
  section.
* `INFOEntryBANK`: An `INFO` entry for `SBNK` (Sound Bank) files within an `SDAT`, which describes which wave archives the bank uses.
* `INFOEntryPLAYER`: An `INFO` entry for a player within an `SDAT`, which describes the max number of sequences that can use it (unused in
  NCSF), a channel mask of which channels are used and the heap size of the player (also unused in NCSF).
* `INFOEntrySEQ`: An `INFO` entry for `SSEQ` (Sound Sequence) files within an `SDAT`, which describes the bank it uses, its volume, its
  channel priority (unused in NCSF), its player priority (also unused in NCSF) and the player it uses.
* `INFOEntryWAVEARC`: An `INFO` entry for `SWAR` (Sound Wave Archive) files within an `SDAT`, which describes the flags (unused in NCSF).
* `INFORecord`: Stores a collection of `INFO` entries, using one of the above classes, from within the `INFO` section of an `SDAT`.
* `INFOSection`: Stores all the records of one of the various `INFO` sections of an `SDAT` as `INFORecord` objects, one each for SEQ, BANK,
  WAVEARC and PLAYER. (SEQARC [Sequence Archive], GROUP, PLAYER2 and STRM [Stream] records are not included, and are ignored on both read
  and write.)
* `NDSStandardHeader`: An abstract class that stores a header, one common to many files in the Nintendo DS file system, on the files
  contained within an `SDAT` as well as the `SDAT` itself. It consists of a 4-byte header tag, a 4-byte magic number, the total size of
  the file (including the header), the size of the header itself and the number of blocks the header contains (1 for the files within the
  `SDAT` and either 3 or 4 for the `SDAT` itself, which depends on if the `SDAT` has a `SYMB` section or not.)
* `SBNK`: An `SBNK` (Sound Bank) file from within an `SDAT`, which contains a collection of `SBNKInstrumentEntry` objects.
* `SBNKInstrument`: A single instrument definition from within an `SBNK`, which defines the range from low note to high note of the
  instrument (this is not saved within the `SBNK`, but derived during reading), the type of instrument it is (also not saved within the
  `SBNK`, where 1-5 are used for a single instrument, 16 is used for a range of definitions and is also referred to as a drum kit, 17 is
  used for regions of defintions and is also referred to as a key split, and any other value is considered invalid), its note number, its
  ADSR (Attack, Decay, Sustain, Release) values and its panning.
* `SBNKInstrumentEntry`: An instrument entry from within an `SBNK`, which contains a collection of `SBNKInstrument` objects as well as the
  type of record it was (see the types in `SBNKInstrument`) and its offset within the `SBNK`.
* `SDAT`: The full `SDAT` (Sound Data) file, it contains the (optional) `SYMB` section, the `INFO` section, the `FAT` section and the
  (unlabeled) `FILE` section. The last section is unlabeled as it just contains all the required `SBNK`, `SSEQ` and `SWAR` files needed for
  music playback. All the sections have their size and offset listed in the header, and the normally empty reserved section of the header
  has 2 bytes used by the NCSF utilities specifically to store how many `SDAT`s were merged together to form the one being read (official
  `SDAT` files will not have this set). The class can be told to read the entire `SDAT` or to only read the data for a single `SSEQ`
  specifically. It also contains functionality to merge multiple `SDAT`s (with the addition operator), strip out excluded or duplicated
  files and fixing the offsets and sizes of the files.
* `SSEQ`: An `SSEQ` (Sound Sequence) file from within an `SDAT`, it contains the raw sequence data for later processing by `Track`.
* `SWAR`: An `SWAR` (Sound Wave Archive) file from within an `SDAT`, it contains a collection of `SWAV` files.
* `SWAV`: An `SWAV` (Sound Wave) file from within an `SDAT`, it contains the original wave type (either 8-bit PCM, signed 16-bit PCM or IMA
  ADPCM), the looping status of the sample, the sample rate of the sample, the timer of the sample (which is a Nintendo DS value that is
  derived from the sample rate and is based on the ARM7 clock speed), the loop offset (if it loops), the loop length (if it loops,
  otherwise this is the length of the entire wave) and the waveform data itself (both in its original format as well as pre-converted to
  32-bit floating-point PCM).
* `SYMBRecord`: Stores a collection of strings containing the names for files, from within the `SYMB` section of an `SDAT`. It also keeps
   track of the offset of those names within the `SYMB` section.
* `SYMBSection`: Stores all the records from within the `SYMB` section of an `SDAT` as `SYMBRecord` objects, one each for SEQ, BANK,
  WAVEARC and PLAYER. (SEQARC [Sequence Archive], GROUP, PLAYER2 and STRM [Stream] records are not included, and are ignored on both read
  and write.) This section is entirely optional and not all `SDAT`s will contain one.

Additionally, there is a ReplayGain analyzer, a modified version of [NReplayGain](https://github.com/karamanolev/NReplayGain), that is used
to calculate the album and track gains/peaks when creating an `NCSF`. More about it can be read in its own [README](ReplayGain/README.md)
file.

There are also the following classes:
* `Channel`: An abstract class for controlling the status of playback on a single one of the 16 simulated Nintendo DS sound channels. This
  version of the class contains the core functionality that is common to both playback and timing of `SSEQ`s.
* `Common`: Static methods, enumerations and values that are commonly used throughout the rest of the codebase.
* `NCSF`: Static methods for working with `NCSF` files specifically. This includes making a new `NCSF`, checking if the given `NCSF` is
  valid, extracting the program section from the `NCSF`, finding the offsets of bytes in the `NCSF` and extracting the tags block from the
  `NCSF`.
* `Player`: An abstract class for the playback of `SSEQ` files, which is used to coordinate between the channels and tracks. This version
  of the class contains the core functionality that is common to both playback and timing of `SSEQ`s.
* `TagList`: A specialized container class for the tags block of an `NCSF` file, specifically to give the functionality of a
  `Dictionary<string, string>` to be able to retrieve a tag's value by its name, while keeping the tags in insertion order similar to a
  `List<(string Name, string Value)>`.
* `Track`: An abstract class for a single track of an `SSEQ` file, which will read and parse the sequence data byte by byte. This version
  of the class contains the core functionality that is common to both playback and timing of `SSEQ`s.

See the [NCSF](https://github.com/CyberBotX/NCSF) repository for other libraries and utilties, as well as license.
