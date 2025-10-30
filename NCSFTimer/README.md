# NCSFTimer

A library containing specialized versions of the `Channel`, `Player` and `Track` classes specifically for timing of `SSEQ`s.

In detail:
* The `Channel` class in this library only goes through samples with no interpolation, as it is not needed for timing purposes.
* The `Player` class in this library uses a forced 44,100 Hz sample rate and contains a method to get the play length of an `SSEQ`, with it
  doing at least 1 pass where it doesn't look at samples at all (it only looks at the sequence of events on all tracks) and might do
  another pass where it does look at samples, in the case of the code being unsure about the length from the sequence data alone.
* The `Track` class in this library steps through the track in a way similar to playback, but also keeps track of when a track has looped
  or hit its end. It also contains methods to get the instruments (sometimes also known as patches) used by an `SSEQ`.

This library also contains its own `NCSF` class that contains methods for getting the time of an `SSEQ` and for calculating ReplayGain on
an `SSEQ`. It also contains an extension method for the `SDAT` class to null out unused `SBNK` instruments and `SWAV`s contains within
`SWAR`s, in an effort to reduce the compressed size of the data later. In the past, I had attempted to completely remove those items
instead, and this only lead to more problems as it required changing the IDs used on every single portion of the `SDAT`. This option might
return in the future after more debugging can be done on it.

See the [NCSF](https://github.com/CyberBotX/NCSF) repository for other libraries and utilties, as well as license.
