# NCSFPlayer

A library containing specialized versions of the `Channel`, `Player` and `Track` classes specifically for playback.

In detail:
* The `Channel` class in this library adds handling actual `SWAV` samples, including interpolation.
* The `Player` class in this library technically does not add anything new but needs to keep references to the `Channel`s and `Track`s
  from this library specifically.
* The `Timer` class in this library steps through the track with playback in mind, and as such it needs to handle informing the
  `Channel` class about its playback.

This library also contains a class called `SWAVWrapper` that is meant to store a copy of an `SWAV`'s data along with some extra data both
before and after the data, in order to allow access past the end of the data for interpolation purposes. The extra data before the data
consists of copies of the first sample of the data, while the extra data after the data consists of either a copy of the start of the data
or 0s, depending on if the sample loops or not.

See the [NCSF](https://github.com/CyberBotX/NCSF) repository for other libraries and utilties, as well as license.
