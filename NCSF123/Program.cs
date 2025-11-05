using System.Runtime.InteropServices;
using DocoptNet;
using NCSFPlayer;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Components;
using SoundFlow.Enums;
using SoundFlow.Providers;
using SoundFlow.Structs;
using Timer = System.Timers.Timer;

namespace NCSF123;

public static class Program
{
	public static void Main(string[] args) => ProgramArguments.CreateParser().
		WithVersion($"NCSF123 v{typeof(Program).Assembly.GetName().Version}").Parse(args).Match(Program.Run, static result =>
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

	static int Run(ProgramArguments args)
	{
		if (args.CmdPlay || args.CmdOutput)
		{
			var clipProtect = PeakTypeHelper.GetEnumFromDescriptionFast(args.OptClipProtect);
			if (clipProtect is null)
			{
				Console.Error.WriteLine("The given clip protect value was not found.");
				return 1;
			}
			int defaultFadeInMS = NCSFCommon.Common.StringToMS(args.OptFade);
			Interpolation interpolation;
			if (args.OptInterpolation == "list")
			{
				Console.WriteLine("The following interpolation methods are available:");
				foreach (var value in Enum.GetValues<Interpolation>())
					Console.WriteLine($"{(int)value,2}: {value.GetDescriptionFast()}");
				Console.WriteLine("If the interpolation name has a space in it, enclose it within quotes.");
				return 0;
			}
			else if (int.TryParse(args.OptInterpolation, out int interpolationNumber))
			{
				interpolation = (Interpolation)interpolationNumber;
				if (interpolation is < Interpolation.None or > Interpolation.Sinc)
				{
					Console.Error.WriteLine("There is no interpolation method for the number you provided.");
					return 1;
				}
			}
			else
			{
				var tempInterpolation = InterpolationHelper.GetEnumFromDescriptionFast(args.OptInterpolation);
				if (tempInterpolation is null)
				{
					Console.Error.WriteLine("The given interpolation method was not found.");
					return 1;
				}
				interpolation = tempInterpolation.Value;
			}
			int defaultLengthInMS = NCSFCommon.Common.StringToMS(args.OptLength);
			static int ProcessSoloOrMute(string label, StringList list, ref ushort solosOrMutes, bool invert)
			{
				foreach (string soloOrMute in list)
				{
					if (!int.TryParse(soloOrMute, out int channelOrTrack))
					{
						Console.Error.WriteLine($"One of the {label}s you gave was not a number.");
						return 1;
					}
					if (channelOrTrack is < 1 or > 16)
					{
						Console.Error.WriteLine($"A {label} must be a number between 1 and 16.");
						return 1;
					}
					if (invert)
						solosOrMutes &= (ushort)~(1 << (channelOrTrack - 1));
					else
						solosOrMutes |= (ushort)(1 << (channelOrTrack - 1));
				}
				return 0;
			}
			ushort channelMutes = 0;
			if (!args.OptMuteChannel.IsEmpty)
			{
				int ret = ProcessSoloOrMute("channel mute", args.OptMuteChannel, ref channelMutes, false);
				if (ret != 0)
					return ret;
			}
			else if (!args.OptSoloChannel.IsEmpty)
			{
				channelMutes = 0xFFFF;
				int ret = ProcessSoloOrMute("channel solo", args.OptSoloChannel, ref channelMutes, true);
				if (ret != 0)
					return ret;
			}
			ushort trackMutes = 0;
			if (!args.OptMuteTrack.IsEmpty)
			{
				int ret = ProcessSoloOrMute("track mute", args.OptMuteTrack, ref trackMutes, false);
				if (ret != 0)
					return ret;
			}
			else if (!args.OptSoloTrack.IsEmpty)
			{
				trackMutes = 0xFFFF;
				int ret = ProcessSoloOrMute("track solo", args.OptSoloTrack, ref trackMutes, true);
				if (ret != 0)
					return ret;
			}
			var replayGain = VolumeTypeHelper.GetEnumFromDescriptionFast(args.OptReplayGain);
			if (replayGain is null)
			{
				Console.Error.WriteLine("The given ReplayGain value was not found.");
				return 1;
			}
			if (!int.TryParse(args.OptSampleRate, out int sampleRate) || sampleRate <= 0)
			{
				Console.Error.WriteLine("Sample rate must be a positive whole number.");
				return 1;
			}
			uint skipSilence = uint.Parse(args.OptSkipSilence);
			float volume = float.Parse(args.OptVolume);
			if (args.CmdPlay)
			{
				if (args.OptForever)
				{
					Console.Error.WriteLine("Currently, playing forever with the play command is not allowed.");
					return 1;
				}

				using MiniAudioEngine audioEngine = new();

				if (audioEngine.PlaybackDevices.Length == 0)
				{
					Console.Error.WriteLine("Your system has no playback device.");
					return 1;
				}

				DeviceInfo? selectedDeviceInfo = null;
				if (args.OptDevice == "list")
				{
					Console.WriteLine("The following devices are available:");
					foreach (var deviceInfo in audioEngine.PlaybackDevices)
						Console.WriteLine($"- {deviceInfo.Name}");
					Console.WriteLine("If the device name has a space in it, enclose it within quotes.");
					return 0;
				}
				else if (args.OptDevice is not null)
				{
					var deviceInfo = Array.Find(audioEngine.PlaybackDevices, pd => pd.Name == args.OptDevice);
					if (deviceInfo.Id == 0)
					{
						Console.Error.WriteLine($"The given device name was not found.");
						return 1;
					}
					selectedDeviceInfo = deviceInfo;
				}
				AudioFormat format = new()
				{
					SampleRate = sampleRate,
					Channels = 2,
					Format = SampleFormat.F32
				};
				using var playbackDevice = audioEngine.InitializePlaybackDevice(selectedDeviceInfo, format);

				using NCSFPlayerStream stream = new(args.ArgInput!, (uint)sampleRate, interpolation, skipSilence, defaultLengthInMS,
					defaultFadeInMS, replayGain!.Value, clipProtect!.Value, false, volume, channelMutes, trackMutes, false);
				using RawDataProvider provider = new(stream, SampleFormat.F32, sampleRate, 2);
				SoundPlayer player = new(audioEngine, format, provider);

				Console.WriteLine("Current song");
				Console.WriteLine("============");
				if (stream.Tags.Contains("game"))
					Console.WriteLine($"Game: {stream.Tags["game"].Value}");
				if (stream.Tags.Contains("disc"))
					Console.WriteLine($"Disc: {stream.Tags["disc"].Value}");
				if (stream.Tags.Contains("track"))
					Console.WriteLine($"Track: {stream.Tags["track"].Value}");
				if (stream.Tags.Contains("title"))
					Console.WriteLine($"Title: {stream.Tags["title"].Value}");
				if (stream.Tags.Contains("artist"))
					Console.WriteLine($"Artist: {stream.Tags["artist"].Value}");
				if (stream.Tags.Contains("year"))
					Console.WriteLine($"Year: {stream.Tags["year"].Value}");
				if (stream.Tags.Contains("copyright"))
					Console.WriteLine($"Copyright: {stream.Tags["copyright"].Value}");
				if (stream.Tags.Contains("comment"))
					Console.WriteLine($"Comment: {stream.Tags["comment"].Value}");
				Console.WriteLine($"Volume: {stream.VolumeModification}");
				Console.WriteLine();

				playbackDevice.MasterMixer.AddComponent(player);
				playbackDevice.Start();
				player.Play();

				Console.WriteLine("Controls:");
				Console.WriteLine(@"  'p' to pause/play, 'q' to quit,
  left/right arrow to seek backward/forward 5 seconds,
  shift + left/right arrow to seek backward/forward 20 seconds");
				Console.WriteLine();

				var (Left, Top) = Console.GetCursorPosition();
				Timer timer = new(250);
				timer.Elapsed += (_, _) =>
				{
					Console.SetCursorPosition(Left, Top);
					Console.Write($"{player.State}: {TimeSpan.FromSeconds(player.Time):mm':'ss} / {TimeSpan.FromSeconds(player.Duration):mm':'ss}     ");
					if (player.State == PlaybackState.Stopped)
						timer.Stop();
				};
				timer.Start();

				bool quit = false;
				while (player.State != PlaybackState.Stopped && !quit)
				{
					bool seek = false;
					int seekTime = 0;
					if (Console.KeyAvailable)
					{
						var keyInfo = Console.ReadKey(true);

						switch (keyInfo.Key)
						{
							case ConsoleKey.P:
								if (player.State == PlaybackState.Playing)
									player.Pause();
								else
									player.Play();
								break;
							case ConsoleKey.Q:
								quit = true;
								break;
							case ConsoleKey.LeftArrow:
							case ConsoleKey.RightArrow:
								seek = true;
								seekTime =
									keyInfo.Modifiers == ConsoleModifiers.Shift ? 20 : 5 * (keyInfo.Key == ConsoleKey.LeftArrow ? -1 : 1);
								break;
						}
					}
					if (seek)
					{
						player.Pause();
						_ = player.Seek(float.Clamp(player.Time + seekTime, 0, player.Duration));
						player.Play();
					}
				}

				playbackDevice.Stop();

				Console.WriteLine();
			}
			else if (args.CmdOutput)
			{
				if (args.OptOutput != "-" && !args.OptOutput.EndsWith(".wav"))
				{
					Console.Error.WriteLine("Output filename must end in .wav.");
					return 1;
				}
				using NCSFPlayerStream stream = new(args.ArgInput!, (uint)sampleRate, interpolation, skipSilence, defaultLengthInMS,
					defaultFadeInMS, replayGain!.Value, clipProtect!.Value, args.OptForever, volume, channelMutes, trackMutes, false);
				Stream output;
				int bitrate = -1;
				if (args.OptOutput == "-")
					output = Console.OpenStandardOutput();
				else
				{
					if (!int.TryParse(args.OptBitrate, out bitrate) || bitrate is not (16 or 32))
					{
						Console.Error.WriteLine("Only bitrates of 16 or 32 are allowed when ouputting to a .wav file.");
						return 1;
					}
					if (args.OptForever)
					{
						Console.Error.WriteLine("Playing forever is not allowed when outputting to a .wav file.");
						return 1;
					}
					output = new MemoryStream();
				}
				using (output)
				{
					int dataSize = 0;
					Span<byte> buffer = stackalloc byte[2304];
					while (true)
					{
						int bytesRead = stream.Read(buffer);
						if (bytesRead == 0)
							break;
						output.Write(buffer);
						dataSize += bytesRead;
					}
					if (args.OptOutput != "-")
					{
						bool is32BitFloat = bitrate == 32;
						int samples = (int)output.Length / 8; // 4 bytes per sample, 2 channels
						int blockAlign = is32BitFloat ? 8 : 4;
						int length = samples * blockAlign;
						using FileStream fs = new(args.OptOutput, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
						fs.Write("RIFF"u8);
						fs.Write(BitConverter.GetBytes(length + 36));
						fs.Write("WAVE"u8);
						fs.Write("fmt "u8);
						fs.Write(BitConverter.GetBytes(is32BitFloat ? 18 : 16)); // Chunk body size
						fs.Write(BitConverter.GetBytes((short)(is32BitFloat ? 3 : 1))); // Format tag (1 = Integer PCM, 3 = Floating Point PCM)
						fs.Write(BitConverter.GetBytes((short)2)); // Number of channels (2 = Stereo)
						fs.Write(BitConverter.GetBytes(sampleRate));
						fs.Write(BitConverter.GetBytes(sampleRate * blockAlign)); // Average bytes per sample
						fs.Write(BitConverter.GetBytes((short)blockAlign)); // Block align (basically bytes per sample / 8 then multiplied by channel count)
						fs.Write(BitConverter.GetBytes((short)(blockAlign * 4))); // Bits per sample
						if (is32BitFloat)
						{
							fs.Write(BitConverter.GetBytes((short)0)); // Only for Floating Point PCM, extension size, which is 0
							// Floating Point PCM requires this extra "fact" chunk
							fs.Write("fact"u8);
							fs.Write(BitConverter.GetBytes(4)); // Chunk body size
							fs.Write(BitConverter.GetBytes(samples)); // Number of sample frames
						}
						fs.Write("data"u8);
						fs.Write(BitConverter.GetBytes(length));
						var data = MemoryMarshal.Cast<byte, float>((output as MemoryStream)!.GetBuffer())[..(samples * 2)];
						if (is32BitFloat)
							foreach (float sample in data)
								fs.Write(BitConverter.GetBytes(sample));
						else
							foreach (float sample in data)
								fs.Write(BitConverter.GetBytes((short)(sample * short.MaxValue)));
						Console.WriteLine($"Output saved to {args.OptOutput}");
					}
				}
			}
		}
		return 0;
	}
}
