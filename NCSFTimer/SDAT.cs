using CommunityToolkit.HighPerformance;
using DotNext.Collections.Generic;
using NCSFCommon.NC;

namespace NCSFTimer;

public static class SDATExtensions
{
	/// <summary>
	/// Nulls out the unused bank instruments and wave archive waveforms from the <see cref="SDAT" />.
	/// </summary>
	public static void NullUnusedInstrumentsAndWaveforms(this NCSFCommon.NC.SDAT sdat)
	{
		// Get all the unique patches.
		Dictionary<ushort, HashSet<ushort>> bankPatches = [];
		foreach (var (Offset, Entry) in sdat.INFOSection.SEQRecord.Entries)
			if (Offset != 0 && Entry is not null)
			{
				var data = Track.GetPatches(Entry.SSEQ!);
				if (!bankPatches.ContainsKey(Entry.Bank))
					bankPatches[Entry.Bank] = [];
				bankPatches[Entry.Bank].AddAll(data.Select(static d => d.patch));
			}

		Dictionary<ushort, HashSet<ushort>> waveArcs = [];
		var bankEntries = sdat.INFOSection.BANKRecord.Entries;
		for (uint i = 0, entries = (uint)bankEntries.Length; i < entries; ++i)
		{
			var (Offset, Entry) = bankEntries[(int)i];
			if (Offset != 0 && Entry is not null)
			{
				var sbnk = Entry.SBNK!;
				_ = bankPatches.TryGetValue((ushort)i, out var usedPatches);

				// Gather the wave archives on the instruments in used for this bank and null out the unused instruments.
				List<SBNKInstrumentEntry> newPatches = [];
				var sbnkEntries = sbnk.Entries;
				var waveArchives = Entry.WaveArchives;
				for (uint j = 0, instruments = (uint)sbnkEntries.Length; j < instruments; ++j)
				{
					var instrumentEntry = sbnkEntries[(int)j];
					bool used = usedPatches?.Contains((ushort)j) ?? false;
					newPatches.Add(used ? instrumentEntry : new());
					if (used)
						foreach (var instrument in instrumentEntry.Instruments)
						{
							ushort swar = waveArchives[instrument.SWAR];
							if (!waveArcs.ContainsKey(swar))
								waveArcs[swar] = [];
							_ = waveArcs[swar].Add(instrument.SWAV);
						}
				}
				sbnk.ReplaceInstruments(newPatches.AsSpan());
			}
		}

		var wavearcEntries = sdat.INFOSection.WAVEARCRecord.Entries;
		for (uint i = 0, entries = (uint)wavearcEntries.Length; i < entries; ++i)
		{
			var (Offset, Entry) = wavearcEntries[(int)i];
			if (Offset != 0 && Entry is not null)
			{
				var swar = Entry.SWAR!;
				_ = waveArcs.TryGetValue((ushort)i, out var usedWaveArcs);

				// Null out the unused waveforms.
				Dictionary<uint, SWAV> newWaves = [];
				foreach (var kvp in swar.SWAVs)
					newWaves[kvp.Key] = (usedWaveArcs?.Contains((ushort)kvp.Key) ?? false) ? kvp.Value : new();
				swar.ReplaceSWAVs(newWaves);
			}
		}

		// Fix the offsets and sizes.
		sdat.FixOffsetsAndSizes();
	}
}
