using System.Buffers.Binary;
using System.IO.MemoryMappedFiles;
using System.Text;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance.Buffers;
using DotNext.IO.MemoryMappedFiles;
using NCSFCommon;
using NCSFCommon.NC;
using NCSFCommon.ReplayGain;
using NCSFTimer;
using Track = NCSFTimer.Track;

namespace NDStoNCSF;

class Program
{
	static int Main(string[] args) => ProgramArguments.CreateParser().
		WithVersion($"NDStoNCSF v{typeof(Program).Assembly.GetName().Version}").Parse(args).Match(Program.Run, static result =>
		{
			Console.WriteLine(result.Help);
			return 0;
		}, static result =>
		{
			Console.WriteLine(result.Version);
			return 0;
		}, static result =>
		{
			Console.Error.WriteLine(result.Usage);
			return 1;
		});

	// Comes from DeSmuME.
	static readonly ushort[] crc16tab =
	[
		0x0000, 0xC0C1, 0xC181, 0x0140, 0xC301, 0x03C0, 0x0280, 0xC241,
		0xC601, 0x06C0, 0x0780, 0xC741, 0x0500, 0xC5C1, 0xC481, 0x0440,
		0xCC01, 0x0CC0, 0x0D80, 0xCD41, 0x0F00, 0xCFC1, 0xCE81, 0x0E40,
		0x0A00, 0xCAC1, 0xCB81, 0x0B40, 0xC901, 0x09C0, 0x0880, 0xC841,
		0xD801, 0x18C0, 0x1980, 0xD941, 0x1B00, 0xDBC1, 0xDA81, 0x1A40,
		0x1E00, 0xDEC1, 0xDF81, 0x1F40, 0xDD01, 0x1DC0, 0x1C80, 0xDC41,
		0x1400, 0xD4C1, 0xD581, 0x1540, 0xD701, 0x17C0, 0x1680, 0xD641,
		0xD201, 0x12C0, 0x1380, 0xD341, 0x1100, 0xD1C1, 0xD081, 0x1040,
		0xF001, 0x30C0, 0x3180, 0xF141, 0x3300, 0xF3C1, 0xF281, 0x3240,
		0x3600, 0xF6C1, 0xF781, 0x3740, 0xF501, 0x35C0, 0x3480, 0xF441,
		0x3C00, 0xFCC1, 0xFD81, 0x3D40, 0xFF01, 0x3FC0, 0x3E80, 0xFE41,
		0xFA01, 0x3AC0, 0x3B80, 0xFB41, 0x3900, 0xF9C1, 0xF881, 0x3840,
		0x2800, 0xE8C1, 0xE981, 0x2940, 0xEB01, 0x2BC0, 0x2A80, 0xEA41,
		0xEE01, 0x2EC0, 0x2F80, 0xEF41, 0x2D00, 0xEDC1, 0xEC81, 0x2C40,
		0xE401, 0x24C0, 0x2580, 0xE541, 0x2700, 0xE7C1, 0xE681, 0x2640,
		0x2200, 0xE2C1, 0xE381, 0x2340, 0xE101, 0x21C0, 0x2080, 0xE041,
		0xA001, 0x60C0, 0x6180, 0xA141, 0x6300, 0xA3C1, 0xA281, 0x6240,
		0x6600, 0xA6C1, 0xA781, 0x6740, 0xA501, 0x65C0, 0x6480, 0xA441,
		0x6C00, 0xACC1, 0xAD81, 0x6D40, 0xAF01, 0x6FC0, 0x6E80, 0xAE41,
		0xAA01, 0x6AC0, 0x6B80, 0xAB41, 0x6900, 0xA9C1, 0xA881, 0x6840,
		0x7800, 0xB8C1, 0xB981, 0x7940, 0xBB01, 0x7BC0, 0x7A80, 0xBA41,
		0xBE01, 0x7EC0, 0x7F80, 0xBF41, 0x7D00, 0xBDC1, 0xBC81, 0x7C40,
		0xB401, 0x74C0, 0x7580, 0xB541, 0x7700, 0xB7C1, 0xB681, 0x7640,
		0x7200, 0xB2C1, 0xB381, 0x7340, 0xB101, 0x71C0, 0x7080, 0xB041,
		0x5000, 0x90C1, 0x9181, 0x5140, 0x9301, 0x53C0, 0x5280, 0x9241,
		0x9601, 0x56C0, 0x5780, 0x9741, 0x5500, 0x95C1, 0x9481, 0x5440,
		0x9C01, 0x5CC0, 0x5D80, 0x9D41, 0x5F00, 0x9FC1, 0x9E81, 0x5E40,
		0x5A00, 0x9AC1, 0x9B81, 0x5B40, 0x9901, 0x59C0, 0x5880, 0x9841,
		0x8801, 0x48C0, 0x4980, 0x8941, 0x4B00, 0x8BC1, 0x8A81, 0x4A40,
		0x4E00, 0x8EC1, 0x8F81, 0x4F40, 0x8D01, 0x4DC0, 0x4C80, 0x8C41,
		0x4400, 0x84C1, 0x8581, 0x4540, 0x8701, 0x47C0, 0x4680, 0x8641,
		0x8201, 0x42C0, 0x4380, 0x8341, 0x4100, 0x81C1, 0x8081, 0x4040
	];

	/// <summary>
	/// Calculates the CRC-16/MODBUS checksum for the given data.
	/// </summary>
	/// <param name="data">The <see cref="ReadOnlySpan{T}" /> to calculate the checksum for.</param>
	/// <returns>The checksum of the data.</returns>
	static ushort CalcCRC16(ReadOnlySpan<byte> data)
	{
		ushort crc = ushort.MaxValue;
		foreach (byte value in data)
			crc = (ushort)((crc >> 8) ^ Program.crc16tab[(crc ^ value) & 0xFF]);
		return crc;
	}

	/// <summary>
	/// Checks if the given data corresponds to a commercial Nintendo DS ROM.
	/// </summary>
	/// <remarks>
	/// Some of the checks come from DeSmuME.
	/// The reason for only checking for a commercial ROM is that Homebrew ROMs do not contain a game code.
	/// </remarks>
	/// <param name="span">The <see cref="ReadOnlySpan{T}" /> to read from.</param>
	/// <returns><see langword="true" /> if the data is a commercial Nintendo DS ROM, <see langword="false" /> otherwise.</returns>
	static bool IsValidNDSROM(ReadOnlySpan<byte> span)
	{
		// Check if Unit Code is between 0 and 3.
		if (span[0x12] > 3)
			return false;

		// Check if ARM9 ROM Offset is at least 0x4000 (if it is less it is most likely Homebrew).
		if (BinaryPrimitives.ReadUInt32LittleEndian(span[0x20..]) < 0x4000)
			return false;

		// Check that the Header Size is 0x4000.
		if (BinaryPrimitives.ReadUInt32LittleEndian(span[0x84..]) != 0x4000)
			return false;

		// Check that the first byte of the Nintendo Logo is correct.
		if (span[0xC0] != 0x24)
			return false;

		// Calculate the CRC16 of the Nintendo Logo and check that it matches the file and
		// that the file has the expected value as well (which is 0xCF56).
		ushort crcOfLogo = Program.CalcCRC16(span.Slice(0xC0, 0x9C));
		ushort crcInFile = BinaryPrimitives.ReadUInt16LittleEndian(span[0x15C..]);
		if (crcOfLogo != crcInFile || crcInFile != 0xCF56)
			return false;

		// After all of that, we'll assume we have a valid commercial NDS ROM.
		return true;
	}

	/// <summary>
	/// Compares the data of two SSEQs to see if they match.
	/// </summary>
	/// <remarks>
	/// Patches are skipped over, just in case they were shifted around by a previous creation of the SSEQ.
	/// </remarks>
	/// <param name="dataA">The first SSEQ's data.</param>
	/// <param name="dataB">The second SSEQ's data.</param>
	/// <returns><see langword="true" /> if the data matches, <see langword="false" /> otherwise.</returns>
	static bool CompareSSEQData(ReadOnlySpan<byte> dataA, ReadOnlySpan<byte> dataB)
	{
		using UnmanagedReadOnlyMemoryManager<byte> dataAMemoryManager = new(dataA);
		using UnmanagedReadOnlyMemoryManager<byte> dataBMemoryManager = new(dataB);
		var patchesA = Track.GetPatches(dataAMemoryManager.Memory);
		var patchesB = Track.GetPatches(dataBMemoryManager.Memory);
		int patchCount = patchesA.Count;
		// If the number of patches used doesn't match, then the data doesn't match.
		if (patchesB.Count != patchCount)
			return false;
		uint lastPosA = 0;
		uint lastPosB = 0;
		for (int i = 0; i <= patchCount; ++i)
		{
			uint nextPosA, nextPosB;
			if (i == patchCount)
			{
				nextPosA = (uint)dataA.Length;
				nextPosB = (uint)dataB.Length;
			}
			else
			{
				nextPosA = patchesA[i].position;
				// Skip over usage of patch 0.
				if (nextPosA == uint.MaxValue)
					continue;
				nextPosB = patchesB[i].position;
			}
			// If the amount of data since the last patch (or from the beginning of the data) doesn't match, then the data doesn't match.
			if (nextPosA - lastPosA != nextPosB - lastPosB)
				return false;
			// Check if the actual data since the last patch (or from the beginning of the data) doesn't match.
			if (!dataA[(int)lastPosA..(int)nextPosA].SequenceEqual(dataB[(int)lastPosB..(int)nextPosB]))
				return false;
			if (i != patchCount)
			{
				lastPosA = (uint)(patchesA[i].position + ((int)patchesA[i].patch).VLVLength());
				lastPosB = (uint)(patchesB[i].position + ((int)patchesB[i].patch).VLVLength());
			}
		}
		return true;
	}

	/// <summary>
	/// A dictionary of values for converting the 4th character of the NDS ROM gamecode to a country code,
	/// similar to the serial number found on the game cartridges.
	/// </summary>
	static readonly Dictionary<char, string> CountryCodes = new()
	{
		['A'] = "UNK", // Meant for Asian, but only 2 games were known to use it and they used EUR or EUU
		['B'] = "UNK", // Not actually used so I don't expect this one to ever happen
		['C'] = "CHN",
		['D'] = "NOE", // While most games used this, 2 used GER...
		['E'] = "USA", // While most games used this, 2 used CAN...
		['F'] = "FRA", // While most games used this, 4 used NOE...
		['G'] = "UNK", // This code was not actually supposed to be used, but there are 2 known games that did use it and they used EUU or GRE
		['H'] = "HOL",
		['I'] = "ITA",
		['J'] = "JPN",
		['K'] = "KOR",
		['L'] = "CAN", // It should be USA #2 but of the 5 known games that used this code, only 1 used USA
		['M'] = "SWE", // While most games used this, 1 used NOR...
		['N'] = "NOR", // While most games used this, 1 used SWE...
		['O'] = "INT",
		['P'] = "EUR", // While most games used this, 4 used UKV...
		['Q'] = "DAN", // While most games used this, 5 used DEN...
		['R'] = "RUS",
		['S'] = "SPA", // A lot of games used this but a lot also used ESP...
		['T'] = "USA", // Only 2 known games used this, it should've been for USA+AUS, but that does not fit the serial number scheme...
		['U'] = "AUS", // While most games used this, 1 used UKV...
		['V'] = "EUU", // I am unsure what to use here, it should've been for EUR+AUS but the codes of EUR, EUU, EUY, EUZ and UKV were found with EUU being the most common...
		['W'] = "EUU", // Not many games used this one, it is meant to be another EUR code, the codes of EUU, EUY, JPN and USA were found with EUU being the most common...
		['X'] = "EUU", // Another EUR code, the codes of BRA, CAN, EUR, EUU, NOE, SCN and USA were found with EUU being the most common...
		['Y'] = "EUU", // Another EUR code, the codes of EUR, EUT, EUU, EUZ, SCN and USA were found with EUU being the most common...
		['Z'] = "EUU", // Another EUR code, the codes of CAN, ESP, EUU, SCN and USA were found with EUU being the most common...
	};

	/// <summary>
	/// NCSF extensions, used to filter the files in the output directory if any exist.
	/// </summary>
	static readonly HashSet<string> ncsfExtensions =
	[
		".ncsf",
		".minincsf",
		".ncsflib"
	];

	static int MaxLength(ReadOnlySpan<(uint Offset, INFOEntrySEQ? Entry)> span)
	{
		int max = 0;
		foreach (var (_, Entry) in span)
		{
			int length = Entry?.OriginalFilename.Length ?? 0;
			if (length > max)
				max = length;
		}
		return max;
	}

	static int Run(ProgramArguments args)
	{
		// Show the usage and quit if the user provided non-integers for the following options.
		if (!int.TryParse(args.OptTime, out int time) || !int.TryParse(args.OptFadeLoop, out int fadeLoop) ||
			!int.TryParse(args.OptFadeOneShot, out int fadeOneShot))
		{
			Console.Error.WriteLine(ProgramArguments.Usage);
			return 1;
		}

		if (time == 0 && args.OptReplayGain)
		{
			Console.Error.WriteLine("Error: ReplayGain calculation not allowed when not timing.");
			return 1;
		}

		// Get exclude and include filenames from the command line.
		List<Common.KeepInfo> includesAndExcludes = [];
		var excludes = args.OptExclude;
		if (excludes.Count != 0)
			includesAndExcludes.AddRange(excludes.Select(static x => new Common.KeepInfo(x, Common.KeepType.Exclude)));
		var includes = args.OptInclude;
		if (includes.Count != 0)
			includesAndExcludes.AddRange(includes.Select(static i => new Common.KeepInfo(i, Common.KeepType.Include)));

		string? createSmap = args.OptCreateSmap;
		string? useSmap = args.OptUseSmap;
		bool auto = args.OptAuto;
		if (createSmap is not null || useSmap is not null)
		{
			if (includesAndExcludes.Count != 0)
			{
				Console.Error.WriteLine("Error: Command line exclusions and inclusions are not allowed when working with SMAP files.");
				return 1;
			}
			if (auto)
			{
				Console.Error.WriteLine("Error: Fully automatic mode is not allowed when working with SMAP files.");
				return 1;
			}
		}

		string ndsFilename = args.ArgInput!;

		try
		{
			// Read NDS ROM.
			if (!File.Exists(ndsFilename))
				ThrowHelper.ThrowArgumentException(nameof(ndsFilename), $"File {ndsFilename} does not exist.");

			using FileStream fs = new(ndsFilename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, true);
			using var mmf =
				MemoryMappedFile.CreateFromFile(fs, null, fs.Length, MemoryMappedFileAccess.Read, HandleInheritability.None, false);
			using var ma = mmf.CreateMemoryAccessor(0, (int)fs.Length, MemoryMappedFileAccess.Read);

			bool isROM = Program.IsValidNDSROM(ma.Bytes);

			string dirName = args.OptDestination ??
				Path.Combine(Path.GetDirectoryName(ndsFilename) ?? "", $"{Path.GetFileNameWithoutExtension(ndsFilename)}_NDStoNCSF");

			bool nocopy = args.OptNocopy;
			int verbosity = args.OptV;

			Dictionary<string, TagList> savedTags = [];
			Dictionary<string, string> filenames = [];
			Dictionary<string, List<SSEQ>> oldSDATFiles = [];
			if (Directory.Exists(dirName))
			{
				// This gets the NCSF-related files from the output directory if they exist.
				var files = Directory.EnumerateFiles(dirName).
					Where(static f => Program.ncsfExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));

				// The next block is for copying the tags data from the existing files if the user did not request to not do so.
				if (!nocopy)
					foreach (string file in files)
					{
						string extension = Path.GetExtension(file).ToLowerInvariant();

						using FileStream ncsfFile = new(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, true);
						using var ncsfMMF = MemoryMappedFile.CreateFromFile(ncsfFile, null, ncsfFile.Length, MemoryMappedFileAccess.Read,
							HandleInheritability.None, false);
						using var ncsfMA = ncsfMMF.CreateMemoryAccessor(0, (int)ncsfFile.Length, MemoryMappedFileAccess.Read);

						// This block is meant for ncsflib and ncsf, which contain a program section.
						// It will store the SSEQs from a possible previous run of this program for comparison later.
						if (extension != ".minincsf")
						{
							Span<byte> sdatBytes = NCSFCommon.NCSF.GetProgramSectionFromPSF(ncsfMA.Bytes, 0x25, 12, 8);
							if (sdatBytes.Length == 0)
								ThrowHelper.ThrowInvalidDataException($"Program section for {file} was empty.");

							SDAT sdat = new();
							sdat.Read(file, sdatBytes);
							if (sdat.SYMBSection is not null)
							{
								var seqSYMBEntries = sdat.SYMBSection.SEQRecord.Entries;
								var seqINFOEntries = sdat.INFOSection.SEQRecord.Entries;
								for (uint i = 0, count = (uint)seqSYMBEntries.Length; i < count; ++i)
								{
									string name = seqSYMBEntries[(int)i].Name!;
									if (!oldSDATFiles.TryGetValue(name, out var oldFiles))
										oldSDATFiles[name] = oldFiles = [];
									oldFiles.Add(seqINFOEntries[(int)i].Entry!.SSEQ!);
								}
							}
						}
						// This block is meant for minincsf and ncsf, which contain tags.
						// It will store the tags from a possible previous run of this program for use later.
						if (extension != ".ncsflib")
						{
							string filename = Path.GetFileName(file);
							var tags = NCSFCommon.NCSF.GetTagsFromPSF(ncsfMA.Memory, 0x25);
							// If 2SF to NCSF was used, don't use the tags for this file at all,
							// they might not be valid for use with NDS to NCSF's purposes.
							if (tags.Contains("ncsfby") && tags["ncsfby"].Value != "2SF to NCSF")
							{
								if (tags.Contains("origFilename"))
								{
									string fullOrigFilename =
										$"{(tags.Contains("origSDAT") ? $"{tags["origSDAT"].Value}/" : "")}{tags["origFilename"].Value}";
									savedTags[fullOrigFilename] = tags;
									filenames[fullOrigFilename] = filename;
								}
								else
									savedTags[filename] = tags;
							}
						}
					}

				// Only remove the files if we are not creating an SMAP.
				if (createSmap is null)
					foreach (string file in files)
						File.Delete(file);
			}
			else
				_ = Directory.CreateDirectory(dirName);
			if (verbosity != 0)
				Console.WriteLine($"Output will go to {dirName}");

			// Get game code (only possible if the file is a commercial Nintendo DS ROM).
			string gameCode = isROM ? Encoding.ASCII.GetString(ma.Bytes[0x0C..0x10]) : "????";

			// Get if the file is a DSi ROM or not.
			bool DSi = isROM && (BinaryPrimitives.ReadUInt32LittleEndian(ma.Bytes[0x180..]) == 0x8D898581U ||
				BinaryPrimitives.ReadUInt32LittleEndian(ma.Bytes[0x184..]) == 0x8C888480U);

			// Search for SDATs and merge them into one.
			SDAT finalSDAT = new();
			if (verbosity != 0)
				Console.WriteLine("Searching for SDATs...");

			int sdatNumber = 0;
			foreach (int sdatOffset in NCSFCommon.NCSF.FindOffsetsInFile(SDAT.Signature, ma.Memory))
				try
				{
					SDAT sdat = new();
					sdat.Read($"{sdatNumber++ + 1}", ma.Bytes[sdatOffset..]);
					finalSDAT += sdat;
					if (verbosity != 0)
					{
						int count = sdat.INFOSection.SEQRecord.Entries.Length;
						Console.WriteLine($"Found SDAT with {count} SSEQ{(count == 1 ? "" : "s")}.");
					}
				}
				catch
				{
					--sdatNumber;
				}

			// Fail if we do not have any SSEQs (which could also mean that there were no SDATs in the file).
			var seqEntries = finalSDAT.INFOSection.SEQRecord.Entries;
			if (seqEntries.Length == 0)
			{
				Directory.Delete(dirName);
				ThrowHelper.ThrowInvalidOperationException("There were no SSEQs or SDATs found within the given file.");
			}

			if (createSmap is not null)
			{
				// Create an SMAP-like file.
				string smapFilename = Path.IsPathRooted(createSmap) ? createSmap : Path.Combine(dirName, createSmap);

				StringBuilder sb = new();
				_ = sb.AppendLine("# NOTE: This SMAP is not identical to SMAPs generated by other tools.");
				_ = sb.AppendLine("#       It is meant for use with NDStoNCSF.");
				_ = sb.AppendLine("# To exclude an SSEQ from the final NCSF set, you can put a '#' symbol");
				_ = sb.AppendLine("# in front of the SSEQ's label.");
				_ = sb.AppendLine();
				if (finalSDAT.SYMBSection is null)
				{
					_ = sb.AppendLine("# NOTE: This SDAT did not have a SYMB section. Labels were generated.");
					_ = sb.AppendLine();
				}
				_ = sb.AppendLine("# SEQ:");
				int maxLength = Program.MaxLength(seqEntries);
				if (maxLength < 26)
					maxLength = 26;
				_ = sb.Append($"# {"label".PadRight(maxLength)}number ");
				if (sdatNumber > 1)
					_ = sb.Append("SDAT# ");
				_ = sb.AppendLine("fileID bnk vol cpr ppr ply       size name");
				var fatRecords = finalSDAT.FATSection.Records;
				for (uint i = 0, count = (uint)seqEntries.Length; i < count; ++i)
				{
					var (Offset, Entry) = seqEntries[(int)i];
					if (Offset != 0 && Entry is not null)
					{
						_ = sb.Append($"  {Entry.OriginalFilename.PadRight(maxLength)}");
						_ = sb.Append($"{i,6}");
						if (sdatNumber > 1)
							_ = sb.Append($"{Entry.SDATNumber,6}");
						_ = sb.Append($"{Entry.FileID,7}");
						_ = sb.Append($"{Entry.Bank,4}");
						_ = sb.Append($"{(int)Entry.Volume,4}");
						_ = sb.Append($"{(int)Entry.ChannelPriority,4}");
						_ = sb.Append($"{(int)Entry.PlayerPriority,4}");
						_ = sb.Append($"{(int)Entry.Player,4}");
						_ = sb.Append($"{fatRecords[(int)Entry.FileID].Size,11}");
						_ = sb.AppendLine(@$" \Seq\{Entry.OriginalFilename}.sseq");
					}
					else
						_ = sb.AppendLine($"{i}".PadLeft(maxLength + 8));
				}

				File.WriteAllText(smapFilename, $"{sb}");

				Console.WriteLine($"Created SMAP: {smapFilename}");
				return 0;
			}

			if (useSmap is not null)
			{
				// First, process the SMAP-like file.
				string smapFilename = Path.IsPathRooted(useSmap) ? useSmap : Path.Combine(dirName, useSmap);
				if (!File.Exists(smapFilename))
					ThrowHelper.ThrowArgumentException("The given SMAP filename was not found.");

				string[] lines = File.ReadAllLines(smapFilename);
				foreach (string line in lines)
					// Skip blank lines, commented lines (those starting with #) or lines that don't contain an SSEQ on them.
					if (!string.IsNullOrEmpty(line) && line[0] != '#' && line[2] != ' ')
					{
						// We only need to extract the label and the SDAT# (if it exists).
						string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
						includesAndExcludes.Add(new($"{(sdatNumber > 1 ? $"{parts[2]}/" : "")}{parts[0]}", Common.KeepType.Include));
					}

				// Second, mark all entries not included from the SMAP as being excluded.
				foreach (var (Offset, Entry) in finalSDAT.INFOSection.SEQRecord.Entries)
					if (Offset != 0 && Entry is not null &&
						Common.IncludeFilename(Entry.OriginalFilename, Entry.SDATNumber, includesAndExcludes) == Common.KeepType.Neither)
						includesAndExcludes.Add(new(Entry.FullFilename(sdatNumber > 1), Common.KeepType.Exclude));
			}

			bool dontStrip = args.OptDontStrip;
			List<string> oldSDATFilesList = [];
			if (!dontStrip)
			{
				// Pre-exclude/include removal, will remove duplicates but not files that the user asked to exclude.
				var tempIncludesAndExcludes = includesAndExcludes.ToList();
				if (!nocopy)
					foreach (var (Offset, Entry) in finalSDAT.INFOSection.SEQRecord.Entries)
						if (Offset != 0 && Entry is not null)
						{
							string filename = Entry.SSEQ!.OriginalFilename!;
							string fullFilename = Entry.FullFilename(sdatNumber > 1);

							// If this file was neither included or excluded on the command line,
							// we need to check if it already existed in the old SDAT.
							if (Common.IncludeFilename(filename, Entry.SDATNumber, includesAndExcludes) == Common.KeepType.Neither)
							{
								// First check by filename as well as data.
								bool isOld = oldSDATFiles.TryGetValue(filename, out var oldFiles);
								bool preExclude = true;
								var thisData = Entry.SSEQ.Data.Span;
								void DataCompare(SSEQ compareTo, ReadOnlySpan<byte> thisData)
								{
									if (preExclude && Program.CompareSSEQData(thisData, compareTo.Data.Span))
										preExclude = false;
								}
								if (isOld)
									foreach (var oldFile in oldFiles!)
										DataCompare(oldFile, thisData);
								// If we are still excluding the file,
								// then we will check by binary comparing the data only to every file in the old SDAT.
								if (preExclude)
									foreach (var oldFile in oldSDATFiles.Where(kvp => kvp.Key != filename).
										SelectMany(static kvp => kvp.Value))
										DataCompare(oldFile, thisData);
								// Now, if we are still excluding the file, we add it to the temp list,
								// otherwise we put it into a list to keep.
								if (preExclude)
									tempIncludesAndExcludes.Add(new(fullFilename, Common.KeepType.Exclude));
								else
									oldSDATFilesList.Add(fullFilename);
							}
						}
				finalSDAT.Strip(tempIncludesAndExcludes, verbosity > 1, false);
			}

			// Only do the following for includes/excludes if we are not using an SMAP (when we are, this has already been done).
			if (useSmap is null)
				// Output which files are included/excluded, asking only if the auto option was not given.
				foreach (var (Offset, Entry) in finalSDAT.INFOSection.SEQRecord.Entries)
					if (Offset != 0 && Entry is not null)
					{
						string filename = Entry.SSEQ!.OriginalFilename!;
						string fullFilename = Entry.FullFilename(sdatNumber > 1);
						string verboseFilename = $"{filename}{(sdatNumber > 1 ? $" (from SDAT #{Entry.SDATNumber})" : "")}";

						var keep = Common.IncludeFilename(filename, Entry.SDATNumber, includesAndExcludes);

						if (keep == Common.KeepType.Exclude)
							Console.WriteLine($"{verboseFilename} was excluded on the command line.");
						else if (keep == Common.KeepType.Include)
							Console.WriteLine($"{verboseFilename} was included on the command line.");
						else
						{
							bool defaultToKeep = nocopy || oldSDATFiles.Count == 0 || oldSDATFilesList.Contains(fullFilename);
							if (auto)
							{
								if (defaultToKeep)
									Console.WriteLine($"{verboseFilename} was included automatically.");
								else
								{
									Console.WriteLine($"{verboseFilename} was excluded automatically.");
									includesAndExcludes.Add(new(fullFilename, Common.KeepType.Exclude));
								}
							}
							else
							{
								Console.Write($"Would you like to keep {verboseFilename}? [{(defaultToKeep ? 'Y' : 'y')}/{(defaultToKeep ? 'n' : 'N')}] ");
								bool hadInput;
								string? input;
								do
								{
									input = Console.ReadLine();
									hadInput = !string.IsNullOrEmpty(input);
									if (hadInput)
										input = $"{char.ToLower(input![0])}{(input.Length > 1 ? input[1..] : "")}";
								} while (hadInput && input![0] != 'y' && input[0] != 'n');
								if ((!hadInput && !defaultToKeep) || (hadInput && input![0] == 'n'))
									includesAndExcludes.Add(new(fullFilename, Common.KeepType.Exclude));
							}
						}
					}

			// Post-exclude/include removal, removes the rest of the files unless the user didn't want to strip files.
			if (dontStrip)
				finalSDAT.FixOffsetsAndSizes();
			else
			{
				finalSDAT.Strip(includesAndExcludes, verbosity > 1);
				//finalSDAT.StripBanksAndWaveArcs();
				finalSDAT.NullUnusedInstrumentsAndWaveforms();
				finalSDAT.Strip([], verbosity > 1);
			}

			seqEntries = finalSDAT.INFOSection.SEQRecord.Entries;

			// Create data for SDAT.
			using var memoryOwner = MemoryOwner<byte>.Allocate((int)finalSDAT.Size);
			finalSDAT.Write(memoryOwner.Span);

			if (seqEntries.Length == 1)
			{
				var sseq = seqEntries[0].Entry!.SSEQ!;

				// Make single NCSF.
				TagList tags =
				[
					("utf8", "1"),
					("ncsfby", "NDS to NCSF")
				];

				// If we had saved tags from a previous run and the nocopy option isn't being used,
				// copy the old tags before creating the file (NOTE: we also have to redo setting
				// the utf8 and ncsfby tags, just in case).
				if (!nocopy && savedTags.TryGetValue(sseq.OriginalFilename!, out var savedTagsForFile))
				{
					tags = savedTagsForFile.Clone();
					tags.AddOrReplace(("utf8", "1"));
					tags.AddOrReplace(("ncsfby", "NDS to NCSF"));
				}

				string ncsfFilename = $"{sseq.Filename}.ncsf";
				if (time != 0)
				{
					NCSFTimer.NCSF.GetTime(ncsfFilename, finalSDAT, sseq, tags, verbosity != 0, (uint)time, fadeLoop, fadeOneShot);
					if (args.OptReplayGain)
					{
						AlbumGain albumGain = new();
						NCSFTimer.NCSF.CalculateReplayGain(ncsfFilename, finalSDAT, sseq, tags, verbosity != 0, albumGain);
						tags.AddOrReplace(("replaygain_album_gain", $"{albumGain.GetGain():F2} dB"));
						tags.AddOrReplace(("replaygain_album_peak", $"{albumGain.GetPeak():F9}"));
					}
				}

				NCSFCommon.NCSF.MakeNCSF(Path.Combine(dirName, ncsfFilename), BitConverter.GetBytes(0), memoryOwner.Span, tags);
				if (verbosity != 0)
					Console.WriteLine($"Created {ncsfFilename}");
			}
			else
			{
				// Determine filename to use for the NCSFLIB
				// (will either be NTR/TWL-<gamecode>-<countrycode> or the given filename minus extension).
				string gameSerial = Program.CountryCodes.TryGetValue(gameCode[3], out string? country) ?
					$"{(DSi ? "TWL" : "NTR")}-{gameCode}-{country}" : Path.GetFileNameWithoutExtension(ndsFilename);

				// Make NCSFLIB.
				string ncsflibFilename = $"{gameSerial}.ncsflib";
				NCSFCommon.NCSF.MakeNCSF(Path.Combine(dirName, ncsflibFilename), [], memoryOwner.Span);
				if (verbosity != 0)
					Console.WriteLine($"Created {ncsflibFilename}");

				// Make multiple MININCSFs.
				TagList tags =
				[
					("_lib", ncsflibFilename),
					("utf8", "1"),
					("ncsfby", "NDS to NCSF")
				];

				AlbumGain albumGain = new();
				Dictionary<uint, TagList> fileTags = new(seqEntries.Length);
				for (uint i = 0, count = (uint)seqEntries.Length; i < count; ++i)
				{
					var (Offset, Entry) = seqEntries[(int)i];
					if (Offset != 0 && Entry is not null)
					{
						string minincsfFilename = $"{Entry.SSEQ!.Filename}.minincsf";

						var thisTags = tags.Clone();
						string fullFilename = Entry.FullFilename(sdatNumber > 1);
						// If we had saved tags from a previous run and the nocopy option isn't being used,
						// copy the old tags before creating the file (NOTE: we also have to redo setting
						// the _lib, utf8 and ncsfby tags, just in case).
						if (!args.OptNocopy && savedTags.TryGetValue(fullFilename, out var thisSavedTags))
						{
							thisTags = thisSavedTags.Clone();
							thisTags.AddOrReplace(("_lib", ncsflibFilename));
							thisTags.AddOrReplace(("utf8", "1"));
							thisTags.AddOrReplace(("ncsfby", "NDS to NCSF"));
						}

						thisTags.AddOrReplace(("origFilename", Entry.SSEQ.OriginalFilename!));
						if (sdatNumber > 1)
							thisTags.AddOrReplace(("origSDAT", Entry.SDATNumber));

						// If this file was renamed from the generated name, then use the old filename instead
						// (This will only work if there was a SYMB section in the SDAT).
						if (filenames.TryGetValue(fullFilename, out string? filename))
							minincsfFilename = filename;

						if (time != 0)
						{
							NCSFTimer.NCSF.GetTime(minincsfFilename, finalSDAT, Entry.SSEQ, thisTags, verbosity != 0, (uint)time, fadeLoop,
								fadeOneShot);
							if (args.OptReplayGain)
								NCSFTimer.NCSF.CalculateReplayGain(minincsfFilename, finalSDAT, Entry.SSEQ, thisTags, verbosity != 0,
									albumGain);
						}

						fileTags[i] = thisTags;
					}
				}
				string gainStr = "";
				string peakStr = "";
				if (args.OptReplayGain)
				{
					double gain = albumGain.GetGain();
					gainStr = $"{(gain < 0 ? "" : "+")}{gain:F2} dB";
					peakStr = $"{albumGain.GetPeak():F9}";
					if (verbosity != 0)
						Console.WriteLine($"Album ReplayGain: {gainStr} / {peakStr} peak");
				}
				for (uint i = 0, count = (uint)seqEntries.Length; i < count; ++i)
				{
					var (Offset, Entry) = seqEntries[(int)i];
					if (Offset != 0 && Entry is not null)
					{
						string minincsfFilename = $"{Entry.SSEQ!.Filename}.minincsf";

						string fullFilename = Entry.FullFilename(sdatNumber > 1);
						// See above about renamed files
						if (filenames.TryGetValue(fullFilename, out string? filename))
							minincsfFilename = filename;

						var thisTags = fileTags[i];
						if (args.OptReplayGain)
						{
							thisTags.AddOrReplace(("replaygain_album_gain", gainStr));
							thisTags.AddOrReplace(("replaygain_album_peak", peakStr));
						}

						NCSFCommon.NCSF.MakeNCSF(Path.Combine(dirName, minincsfFilename), BitConverter.GetBytes(i), [], thisTags);
						if (verbosity != 0)
							Console.WriteLine($"Created {minincsfFilename}");
					}
				}
			}
		}
		catch (Exception e)
		{
			Console.Error.WriteLine($"Error: {e.Message}");
			Console.Error.WriteLine();
			Console.Error.WriteLine("Full Exception:");
			Console.Error.WriteLine($"{e}");
			return 1;
		}

		return 0;
	}
}
