from music21 import midi
import argparse
from pathlib import Path
import sys

parser = argparse.ArgumentParser(description="Inspect raw MIDI events")
parser.add_argument("infile", help="Path to MIDI file (required)")
args = parser.parse_args()

midi_path = Path(args.infile)
if not midi_path.exists():
    print(f"Error: file not found: {midi_path}", file=sys.stderr)
    sys.exit(1)

mf = midi.MidiFile()
mf.open(str(midi_path))
mf.read()
mf.close()

print('ticks per quarter:', mf.ticksPerQuarterNote)
for ti, tr in enumerate(mf.tracks[:4]):
    print('--- TRACK', ti, 'len events', len(tr.events))
    t = 0
    for ei, ev in enumerate(tr.events[:50]):
        t += getattr(ev, 'time', 0)
        print(f'event[{ei}] time_delta={getattr(ev,"time",None)} cum_ticks={t} type={getattr(ev,"type",None)} channel={getattr(ev,"channel",None)} pitch={getattr(ev,"pitch",None)} velocity={getattr(ev,"velocity",None)}')
    print()

print('done')