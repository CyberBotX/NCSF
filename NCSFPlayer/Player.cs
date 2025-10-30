using System.ComponentModel;

namespace NCSFPlayer;

[GenerateHelper(GenerateHelperOption.UseItselfWhenNoDescription)]
public enum Interpolation
{
	[Description("No Interpolation")]
	None,
	Linear,
	[Description("4-Point Lagrange")]
	FourPointLagrange,
	[Description("6-Point Lagrange")]
	SixPointLagrange,
	[Description("Old Sinc")]
	Sinc,
	[Description("Sinc")]
	SimpleSinc,
	Lanczos
}

public class Player : NCSFCommon.Player
{
	protected override NCSFCommon.Track[] tracks { get; } =
		[.. Enumerable.Range(0, Player.TrackCount).Select(static _ => new Track() as NCSFCommon.Track)];

	protected override NCSFCommon.Channel[] channels { get; } =
		[.. Enumerable.Range(0, Player.ChannelCount).Select(static _ => new Channel() as NCSFCommon.Channel)];

	public override uint SampleRate { get; set; }
	public Interpolation Interpolation { get; set; }
	public ushort TrackMutes { get; set; }

	public override void SequenceMain()
	{
		// Also technically a bit of the while loop from SndThread in SND_main.c of the Pokémon Diamond decompilation.
		foreach (var channel in this.channels)
			channel.Update();
		this.Main();
		this.UpdateChannel();
		foreach (var channel in this.channels)
			channel.Main();
	}

	public override void StepTicks()
	{
		for (int i = 0; i < Player.TrackCount; ++i)
		{
			var track = this.GetTrack(i);
			if (track is not null && track.CurrentPos != -1)
			{
				track.Mute = (this.TrackMutes & (1 << track.Id)) != 0;
				if (!track.StepTicks())
					this.StopTrack(i);
			}
		}
	}
}
