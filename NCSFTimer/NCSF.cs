using NCSFCommon;
using NCSFCommon.ReplayGain;

namespace NCSFTimer;

public static class NCSF
{
	/// <summary>
	/// Gets the time of an <see cref="NC.SSEQ" />.
	/// </summary>
	/// <remarks>
	/// The timing player will be run at least once (without "playing" the music).
	/// But if the song is a one-shot (not looping), the player will be run a second time,
	/// "playing" the notes to determine when silence has really occurred.
	/// </remarks>
	/// <param name="filename">The filename of the SSEQ.</param>
	/// <param name="sdat">The <see cref="NC.SDAT" /> of the SSEQ, needed to get the info associated with the SSEQ.</param>
	/// <param name="sseq">The <see cref="NC.SSEQ" /> to time.</param>
	/// <param name="tags">The existing tags of the SSEQ.</param>
	/// <param name="verbose">If <see langword="true" />, the timings will be output to the console.</param>
	/// <param name="numberOfLoops">The number of loops to time for.</param>
	/// <param name="fadeLoop">The fade time for a looped sequence.</param>
	/// <param name="fadeOneShot">The fade time for a one-shot sequence.</param>
	public static void GetTime(string filename, NCSFCommon.NC.SDAT sdat, NCSFCommon.NC.SSEQ sseq, TagList tags, bool verbose,
		uint numberOfLoops, int fadeLoop, int fadeOneShot)
	{
		// Setup the player.
		Player player = new()
		{
			Loops = numberOfLoops,
			MaxSeconds = 6000
		};
		player.PrepareSequence(sseq, 0, 0);
		player.SetUsedTrack(0, true);
		player.SequenceMain();
		// Get the time, without "playing" the notes.
		player.GetLength();
		var length = player.Length;
		// If the length was for a one-shot song, get the time again, this time "playing" the notes,
		// in order to detect when silence happens.
		bool gotLength = false;
		if (length is not null && length.Time != -1 && length.Type == PlayerTimeType.End)
		{
			// TODO: For this section, I need to figure out how to handle a case of a supposed one-shot song that has an infinitely looping
			// SWAV on a channel for a track with a length of 0, as this means to keep playing forever (should be until the channel is
			// released, but I don't think that ever happens here)
			var info = sdat.INFOSection.SEQRecord.Entries[sseq.EntryNumber].Entry!;
			var sbnkInfo = sdat.INFOSection.BANKRecord.Entries[info.Bank].Entry!;
			player = new()
			{
				SBNK = sbnkInfo.SBNK!,
				Loops = numberOfLoops,
				MaxSeconds = (uint)(length.Time + 30),
				DoNotes = true
			};
			player.PrepareSequence(sseq, 0, NCSFCommon.NCSF.ConvertScale(info.Volume == 0 ? 0x7F : info.Volume));
			var waveArchives = sbnkInfo.WaveArchives;
			var wavearcEntries = sdat.INFOSection.WAVEARCRecord.Entries;
			for (int i = 0; i < 4; ++i)
			{
				ushort swar = waveArchives[i];
				if (swar != ushort.MaxValue)
					player.SetSWAR(i, wavearcEntries[swar].Entry!.SWAR!);
			}
			var playerEntries = sdat.INFOSection.PLAYERRecord.Entries;
			var playerInfo = info.Player < playerEntries.Length ? playerEntries[info.Player].Entry : null;
			if (playerInfo is not null)
				player.ChannelMask = playerInfo.ChannelMask;
			if (player.ChannelMask == 0)
				player.ChannelMask = 0xFFFF;
			player.SetUsedTrack(0, true);
			player.SequenceMain();
			var oldLength = length;
			player.GetLength();
			length = player.Length;
			if (length?.Time != -1)
				gotLength = true;
			else
				length = oldLength;
		}
		if (length is null || length.Time == -1)
		{
			// This means that time was unable to be calculated for the sequence at all and should remove all the length-related tags.
			_ = tags.Remove("fade");
			_ = tags.Remove("length");
			if (verbose)
				Console.WriteLine($"Unable to calculate time for {filename}");
		}
		else
		{
			// This means we got a time and should add it to the tags.
			tags.AddOrReplace(("fade", $"{(length.Type == PlayerTimeType.Loop ? fadeLoop : fadeOneShot)}"));
			if (length.Time == 0)
				length.Time = 1;
			string lengthString = Common.SecondsToString(float.Ceiling(length.Time));
			tags.AddOrReplace(("length", lengthString));
			if (verbose)
			{
				Console.WriteLine($"Time for {filename}: {lengthString} ({(length.Type == PlayerTimeType.Loop ? $"timed to {numberOfLoops} loops" : "one-shot")})");
				if (length.Type == PlayerTimeType.End && !gotLength)
					Console.WriteLine("(NOTE: Was unable to detect silence at the end of the track, time may be inaccurate.)");
			}
		}
	}

	public static void CalculateReplayGain(string filename, NCSFCommon.NC.SDAT sdat, NCSFCommon.NC.SSEQ sseq, TagList tags, bool verbose,
		AlbumGain albumGain)
	{
		if (tags.Contains("length"))
		{
			// Setup the player.
			var info = sdat.INFOSection.SEQRecord.Entries[sseq.EntryNumber].Entry!;
			var sbnkInfo = sdat.INFOSection.BANKRecord.Entries[info.Bank].Entry!;
			Player player = new()
			{
				SBNK = sbnkInfo.SBNK!,
				DoNotes = true
			};
			player.PrepareSequence(sseq, 0, NCSFCommon.NCSF.ConvertScale(info.Volume == 0 ? 0x7F : info.Volume));
			var waveArchives = sbnkInfo.WaveArchives;
			var wavearcEntries = sdat.INFOSection.WAVEARCRecord.Entries;
			for (int i = 0; i < 4; ++i)
			{
				ushort swar = waveArchives[i];
				if (swar != ushort.MaxValue)
					player.SetSWAR(i, wavearcEntries[swar].Entry!.SWAR!);
			}
			var playerEntries = sdat.INFOSection.PLAYERRecord.Entries;
			var playerInfo = info.Player < playerEntries.Length ? playerEntries[info.Player].Entry : null;
			if (playerInfo is not null)
				player.ChannelMask = playerInfo.ChannelMask;
			if (player.ChannelMask == 0)
				player.ChannelMask = 0xFFFF;
			player.SequenceMain();

			int length = Common.StringToMS(tags["length"].Value) / 1000;
			float previousCycleRemainder = 0;
			TrackGain trackGain = new((int)Player.FakeSampleRate, 16);
			Span<int> leftSamples = stackalloc int[(int)double.Ceiling(Player.SamplesPerClockCycle)];
			Span<int> rightSamples = stackalloc int[(int)double.Ceiling(Player.SamplesPerClockCycle)];
			while (true)
			{
				float thisCycleSamplesTotal = previousCycleRemainder + Player.SamplesPerClockCycle;
				int thisCycleSamples = (int)float.Floor(thisCycleSamplesTotal);
				previousCycleRemainder = thisCycleSamplesTotal - thisCycleSamples;

				for (int i = 0; i < thisCycleSamples; ++i)
				{
					float leftChannel = 0;
					float rightChannel = 0;

					// I need to advance the sound channels here.
					foreach (var channel in player.Channels)
						if (channel.IsActive() && channel.Register.Enable)
						{
							float sample = channel.GenerateSample();
							channel.IncrementSample();

							byte dataShift = channel.Register.VolumeDivisor;
							if (dataShift == 3)
								dataShift = 4;
							sample = Player.MulDiv7(sample, channel.Register.VolumeMultiplier) * dataShift switch
							{
								1 => 0.5f,
								2 => 0.25f,
								4 => 0.0625f,
								_ => 1
							};

							byte panning = channel.Register.Panning;
							leftChannel += Player.MulDiv7(sample, (byte)(127 - panning));
							rightChannel += Player.MulDiv7(sample, panning);
						}

					leftSamples[i] = (int)(leftChannel * short.MaxValue);
					rightSamples[i] = (int)(rightChannel * short.MaxValue);
				}

				trackGain.AnalyzeSamples(leftSamples[..thisCycleSamples], rightSamples[..thisCycleSamples]);

				player.SequenceMain();

				if (player.seconds > length)
					break;
			}
			albumGain.AppendTrackData(trackGain);
			double gain = trackGain.GetGain();
			string gainStr = $"{(gain < 0 ? "" : "+")}{gain:F2} dB";
			string peakStr = $"{trackGain.GetPeak():F9}";
			tags.AddOrReplace(("replaygain_track_gain", gainStr));
			tags.AddOrReplace(("replaygain_track_peak", peakStr));
			if (verbose)
				Console.WriteLine($"Track ReplayGain for {filename}: {gainStr} / {peakStr} peak");
		}
		else if (verbose)
			Console.WriteLine($"Because no time was found for {filename}, ReplayGain cannot be calculated.");
	}
}
