using CommunityToolkit.Diagnostics;

namespace NCSFTimer;

/// <summary>
/// The type of a player's timing.
/// </summary>
public enum PlayerTimeType : byte
{
	/// <summary>
	/// Signifies that the time is a loop.
	/// </summary>
	Loop,
	/// <summary>
	/// Signifies that the time is a one-shot.
	/// </summary>
	End
}

/// <summary>
/// A player's timing for a track.
/// </summary>
public class PlayerTime
{
	/// <summary>
	/// The time in seconds of the track.
	/// </summary>
	public float Time { get; set; }

	/// <summary>
	/// The type of time that this timing is.
	/// </summary>
	public PlayerTimeType Type { get; set; } = PlayerTimeType.Loop;
}

public class Player : NCSFCommon.Player
{
	internal const float FakeSampleRate = 44100;
	internal const float SamplesPerClockCycle = Player.FakeSampleRate * Player.SecondsPerClockCycle;
	const float SecondsPerSample = 1 / Player.FakeSampleRate;

	protected override NCSFCommon.Track[] tracks { get; } =
		[.. Enumerable.Range(0, Player.TrackCount).Select(static _ => new Track() as NCSFCommon.Track)];
	readonly bool[] usedTracks = [.. Enumerable.Repeat(false, Player.TrackCount)];

	protected override NCSFCommon.Channel[] channels { get; } =
		[.. Enumerable.Range(0, Player.ChannelCount).Select(static _ => new Channel() as NCSFCommon.Channel)];

	public override uint SampleRate { get; set; } = (uint)Player.FakeSampleRate;

	/// <summary>
	/// The times on each of the tracks of this player.
	/// </summary>
	readonly List<PlayerTime>[] trackTimes = [.. Enumerable.Range(0, Player.TrackCount).Select(static _ => new List<PlayerTime>())];
	float trailingSilenceSeconds;
	internal float seconds;

	/// <summary>
	/// The maximum number of seconds to consider for timing.
	/// </summary>
	public uint MaxSeconds { get; set; }

	/// <summary>
	/// The number of loops to consider for timing.
	/// </summary>
	public uint Loops { get; set; }

	/// <summary>
	/// Determines if notes should be "played" or not.
	/// </summary>
	public bool DoNotes { get; set; }

	/// <summary>
	/// The total time for the sequence for this player.
	/// </summary>
	public PlayerTime? Length { get; private set; }

	public override void SequenceMain()
	{
		// Also technically a bit of the while loop from SndThread in SND_main.c of the Pokémon Diamond decompilation.
		if (this.DoNotes)
			foreach (var channel in this.channels)
				channel.Update();
		this.Main();
		if (this.DoNotes)
		{
			this.UpdateChannel();
			foreach (var channel in this.channels)
				channel.Main();
		}
	}

	public override void Main()
	{
		base.Main();

		this.seconds += Player.SecondsPerClockCycle;
	}

	public override void StepTicks()
	{
		for (int i = 0; i < Player.TrackCount; ++i)
		{
			var track = (this.GetTrack(i) as Track)!;
			if (track is not null && track.CurrentPos != -1)
			{
				if (!track.StepTicks())
					this.StopTrack(i);
				if (track.HitLoop)
				{
					this.trackTimes[i].Add(new()
					{
						Time = this.seconds,
						Type = PlayerTimeType.Loop
					});
					track.HitLoop = false;
				}
				if (track.HitEnd)
				{
					this.trackTimes[i].Add(new()
					{
						Time = this.seconds,
						Type = PlayerTimeType.End
					});
					track.HitEnd = false;
				}
			}
		}
	}

	/// <summary>
	/// Sets if a track was used for the given index.
	/// </summary>
	/// <param name="index">The index of the track to set as used or not.</param>
	/// <param name="value"><see langword="true" /> if the track should be set as used, <see langword="false" /> otherwise.</param>
	public void SetUsedTrack(int index, bool value)
	{
		Guard.IsInRange(index, 0, Player.TrackCount);

		this.usedTracks[index] = value;
	}

	PlayerTime DoLength()
	{
		uint tracksLooped = 0;
		uint tracksEnded = 0;
		float length = -1;
		var lastType = PlayerTimeType.Loop;
		uint loops = this.Loops;
		int numberOfUsedTracks = this.usedTracks.Count(static used => used);
		for (int i = 0; i < Player.TrackCount; ++i)
		{
			var times = this.trackTimes[i];
			if (times.Count != 0)
			{
				var time = times.Last();
				if (time.Type == PlayerTimeType.Loop && times.Count >= loops)
					++tracksLooped;
				else if (time.Type == PlayerTimeType.End)
					++tracksEnded;
				if (time.Time > length)
				{
					length = time.Time;
					lastType = time.Type;
				}
			}
		}
		if (tracksLooped == numberOfUsedTracks)
			return new()
			{
				Time = length,
				Type = PlayerTimeType.Loop
			};
		else if (tracksEnded == numberOfUsedTracks)
			return new()
			{
				Time = length,
				Type = PlayerTimeType.End
			};
		else if (tracksLooped + tracksEnded == numberOfUsedTracks)
			return new()
			{
				Time = length,
				Type = lastType
			};
		return new()
		{
			Time = -1,
			Type = PlayerTimeType.Loop
		};
	}

	/// <summary>
	/// Gets the length of the sequence that this player will be playing.
	/// </summary>
	public void GetLength()
	{
		bool success = false;
		try
		{
			float previousCycleRemainder = 0;
			while (true)
			{
				// If notes are being "played",
				// then we need to act like we are rendering out enough samples for the clock cycle (approximately 229-230).
				if (this.DoNotes)
				{
					float thisCycleSamplesTotal = previousCycleRemainder + Player.SamplesPerClockCycle;
					int thisCycleSamples = (int)float.Floor(thisCycleSamplesTotal);
					previousCycleRemainder = thisCycleSamplesTotal - thisCycleSamples;

					for (int i = 0; i < thisCycleSamples; ++i)
					{
						float leftChannel = 0;
						float rightChannel = 0;

						// I need to advance the sound channels here.
						foreach (var channel in this.channels)
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

						leftChannel = float.Clamp(leftChannel, -1, 1);
						rightChannel = float.Clamp(rightChannel, -1, 1);

						if (leftChannel == 0 && rightChannel == 0)
							this.trailingSilenceSeconds += Player.SecondsPerSample;
						else if (this.trailingSilenceSeconds > 0)
							this.trailingSilenceSeconds = 0;
					}
				}

				this.SequenceMain();

				if (this.DoNotes && this.trailingSilenceSeconds >= 20)
				{
					float time = this.seconds - this.trailingSilenceSeconds;
					this.Length = new()
					{
						Time = time < 0 ? 0 : time,
						Type = PlayerTimeType.End
					};
					success = true;
					break;
				}

				if (!this.DoNotes)
				{
					this.Length = this.DoLength();
					if (this.Length.Time != -1)
					{
						success = true;
						break;
					}
				}
				if (this.seconds > this.MaxSeconds)
					break;
			}
		}
		catch
		{
			success = false;
		}
		if (!success)
			this.Length = new()
			{
				Time = -1,
				Type = PlayerTimeType.Loop
			};
	}
}
