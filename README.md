# Dependencies

This project is a Unity drum-learning application with a Python-based MIDI/MusicXML parser and audio trimming utilities.

## Required Tools

- Unity 6.0.2 or newer, matching the project version in `ProjectSettings/ProjectVersion.txt`
- Python 3.10 or newer
- `music21` for parsing MIDI and MusicXML files
- `ffmpeg` for audio trimming and beatmap snipping features

## Python Setup

Create a virtual environment and install the Python dependency used by the importer:

```bash
python -m venv .venv
.venv\Scripts\activate
pip install music21
```

## ffmpeg Setup

`ffmpeg` is used by the beatmap snipping workflow when audio trimming is enabled. Make sure it is available on your `PATH`:

```bash
ffmpeg -version
```

If `ffmpeg` is not installed, audio trimming will fall back to copying the full audio file, but trimming features will not be available.

## Unity Packages

The project’s Unity package dependencies are declared in `Packages/manifest.json`. Key packages include:

- `com.unity.inputsystem`
- `com.unity.render-pipelines.universal`
- `com.unity.timeline`
- `com.unity.ugui`

## Notes

- Beatmap importing expects MusicXML or MIDI input files that can be parsed by `music21`.
- The importer writes generated beatmaps into the project’s persistent data folder at runtime.
