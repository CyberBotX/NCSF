namespace NCSFTimer;

public class Channel : NCSFCommon.Channel
{
	public override float GenerateSample()
	{
		if (this.Register.SamplePosition < 0)
			return 0;

		if (this.Register.Format != 3)
			return this.Register.Source!.Data[(int)this.Register.SamplePosition];
		else if (this.Id < 8)
			return 0;
		else if (this.Id < 14)
			return Channel.WaveDutyTable[this.Register.WaveDuty][(int)this.Register.SamplePosition & 0x7];
		else
		{
			if (this.Register.PSGLastCount != (uint)this.Register.SamplePosition)
			{
				uint max = (uint)this.Register.SamplePosition;
				for (uint i = this.Register.PSGLastCount; i < max; ++i)
					if ((this.Register.PSGX & 1) != 0)
					{
						this.Register.PSGX = (ushort)((this.Register.PSGX >> 1) ^ 0x6000);
						this.Register.PSGLast = -1;
					}
					else
					{
						this.Register.PSGX >>= 1;
						this.Register.PSGLast = 1;
					}

				this.Register.PSGLastCount = (uint)this.Register.SamplePosition;
			}

			return this.Register.PSGLast;
		}
	}

	public override void IncrementSample()
	{
		this.Register.SamplePosition += this.Register.SampleIncrease;
		if (this.Register.Format != 3 && this.Register.SamplePosition >= this.Register.TotalLength)
		{
			if (this.Register.RepeatMode == 1)
				while (this.Register.SamplePosition >= this.Register.TotalLength)
					this.Register.SamplePosition -= this.Register.Length;
			else
				this.Kill();
		}
	}
}
