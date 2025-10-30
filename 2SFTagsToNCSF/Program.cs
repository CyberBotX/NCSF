using System.Buffers.Binary;
using System.IO.MemoryMappedFiles;
using DotNext.IO.MemoryMappedFiles;
using NCSFCommon;
using NCSFCommon.NC;

namespace _2SFTagsToNCSF;

// NOTE: Currently have not tested this one, need to do so before release.

static class Program
{
	static void Main(string[] args) => ProgramArguments.CreateParser().
		WithVersion($"2SFTagsToNCSF v{typeof(Program).Assembly.GetName().Version}").Parse(args).Match(Program.Run, static result =>
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

	/// <summary>
	/// 2SF extensions, used to filter the files in the input directory.
	/// </summary>
	static readonly HashSet<string> twoSFExtensions =
	[
		".2sf",
		".mini2sf",
		".2sflib"
	];

	/// <summary>
	/// NCSF extensions, used to filter the files in the output directory.
	/// </summary>
	static readonly HashSet<string> ncsfExtensions =
	[
		".ncsf",
		".minincsf",
		".ncsflib"
	];

	class TwoSF
	{
		public ushort SSEQNumber { get; set; }

		public SSEQ? SSEQ { get; set; }

		public TagList TagList { get; set; } = null!;
	}

	static int Run(ProgramArguments args)
	{
		string twoSFDirectory = args.ArgInputdir!;
		string ncsfDirectory = args.ArgOutputdir!;
		if (!Directory.Exists(twoSFDirectory) || !Directory.Exists(ncsfDirectory))
		{
			Console.Error.WriteLine(ProgramArguments.Usage);
			return 1;
		}

		// Get the tags and SDATs from the 2SFs.
		Dictionary<string, SDAT> twoSFSDATs = [];
		Dictionary<string, TwoSF> twoSFs = [];
		foreach (string filename in Directory.EnumerateFiles(twoSFDirectory).
			Where(static f => Program.twoSFExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())))
			try
			{
				using FileStream twoSFFile = new(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, true);
				using var twoSFMMF = MemoryMappedFile.CreateFromFile(twoSFFile, null, twoSFFile.Length, MemoryMappedFileAccess.Read,
					HandleInheritability.None, false);
				using var twoSFMA = twoSFMMF.CreateMemoryAccessor(0, (int)twoSFFile.Length, MemoryMappedFileAccess.Read);

				var tags = NCSF.GetTagsFromPSF(twoSFMA.Memory, 0x24);
				if (tags.Contains("_lib"))
				{
					Span<byte> programSection = NCSF.GetProgramSectionFromPSF(twoSFMA.Bytes, 0x24, 8, 4, true);
					twoSFs[filename] = new()
					{
						SSEQNumber = BinaryPrimitives.ReadUInt16LittleEndian(programSection[8..]),
						SSEQ = null,
						TagList = tags
					};
				}
				else
				{
					SDAT sdat = new();
					sdat.Read(filename, twoSFMA.Bytes[NCSF.FindOffsetsInFile(SDAT.Signature, twoSFMA.Memory).First()..], false);
					twoSFSDATs[Path.GetFileName(filename)] = sdat;
				}
			}
			catch (Exception e)
			{
				Console.Error.WriteLine($"ERROR: {e.Message}");
			}

		if (twoSFSDATs.Count == 0)
		{
			Console.Error.WriteLine("ERROR: No SDAT found in the 2SFs, for some reason...");
			return 1;
		}

		// For the 2SFs that had a _lib tag, get their SSEQ from the SDAT.
		foreach (var twoSF in twoSFs.Values)
			twoSF.SSEQ = twoSFSDATs[twoSF.TagList["_lib"].Value].INFOSection.SEQRecord.Entries[twoSF.SSEQNumber].Entry?.SSEQ;

		// Get the tags and SDAT for the NCSFs.
		SDAT ncsfSDAT = new();
		Dictionary<string, (uint sseqNumber, ReadOnlyMemory<byte> sdat, TagList tags)> ncsfs = [];
		foreach (string filename in Directory.EnumerateFiles(ncsfDirectory).
			Where(static f => Program.ncsfExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())))
			try
			{
				using FileStream ncsfFile = new(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, true);
				using var ncsfMMF = MemoryMappedFile.CreateFromFile(ncsfFile, null, ncsfFile.Length, MemoryMappedFileAccess.Read,
					HandleInheritability.None, false);
				using var ncsfMA = ncsfMMF.CreateMemoryAccessor(0, (int)ncsfFile.Length, MemoryMappedFileAccess.Read);

				Span<byte> programSection = NCSF.GetProgramSectionFromPSF(ncsfMA.Bytes, 0x25, 12, 8);
				var tags = NCSF.GetTagsFromPSF(ncsfMA.Memory, 0x25);
				// If the program section is empty, this is a minincsf
				if (programSection.Length == 0)
					ncsfs[filename] = (BinaryPrimitives.ReadUInt32LittleEndian(ncsfMA.Bytes[0x10..]), Array.Empty<byte>(), tags);
				// Otherwise it is either an ncsf or an ncsflib
				else
				{
					ncsfSDAT.Read(filename, ncsfMA.Bytes[NCSF.FindOffsetsInFile(SDAT.Signature, ncsfMA.Memory).First()..]);
					if (tags.Count != 0)
						ncsfs[filename] = (0, programSection.ToArray(), tags);
				}
			}
			catch
			{
			}

		// Copy the tag data from the 2SFs to the NCSFs
		bool rename = args.OptRename;
		bool verbose = args.OptVerbose;
		Span<byte> reservedData = stackalloc byte[4];
		var seqEntries = ncsfSDAT.INFOSection.SEQRecord.Entries;
		foreach (var kvp in ncsfs)
		{
			uint sseqNumber = kvp.Value.sseqNumber;
			var sseq = seqEntries[(int)sseqNumber].Entry!.SSEQ!;

			var twoSF = twoSFs.FirstOrDefault(item => sseq.Data.Span.SequenceEqual(item.Value.SSEQ!.Data.Span));
			if (twoSF.Value is not null)
			{
				string filename = kvp.Key;
				string extension = Path.GetExtension(filename);
				if (rename)
				{
					filename = Path.Combine(ncsfDirectory, Path.GetFileNameWithoutExtension(twoSF.Key));
					if (File.Exists($"{filename}{extension}"))
						for (uint i = 1; ; ++i)
							if (!File.Exists($"{filename}_Duplicate{i}{extension}"))
							{
								filename += $"_Duplicate{i}";
								break;
							}
					filename += extension;
					File.Delete(kvp.Key);
				}
				if (verbose)
				{
					Console.WriteLine($"Copying tags from {twoSF.Key}");
					Console.WriteLine($"  to {filename}");
				}

				var twoSFTags = twoSF.Value.TagList;
				_ = twoSFTags.Remove("_lib");
				_ = twoSFTags.Remove("2sfby");
				_ = twoSFTags.Remove("length");
				_ = twoSFTags.Remove("fade");
				foreach (string exclude in args.OptExclude)
					_ = twoSFTags.Remove(exclude);

				var ncsfTags = kvp.Value.tags;
				foreach (var tag in twoSFTags)
					ncsfTags.AddOrReplace(tag);

				BinaryPrimitives.WriteUInt32LittleEndian(reservedData, sseqNumber);
				NCSF.MakeNCSF(filename, reservedData, kvp.Value.sdat.Span, ncsfTags);
			}
		}

		return 0;
	}
}
