from music21 import converter, chord
p = ".\\input_musicxml\\Transcription_Lamp_Lasttrainat25O'clock.mid"
print('Path to parse:', p)
s = converter.parse(p)
print('Parts:', [p.id for p in s.parts])
notes = list(s.recurse().notes)
print('Total notes:', len(notes))
for i,n in enumerate(notes[:10]):
    print('\n--- NOTE', i, '---')
    print('repr:', repr(n))
    print('type:', type(n))
    try:
        print('offset:', n.offset)
    except Exception:
        pass
    try:
        print('pitch:', getattr(n, 'pitch', None))
    except Exception:
        pass
    try:
        print('midi (via pitch):', getattr(n.pitch, 'midi', None) if getattr(n, 'pitch', None) is not None else None)
    except Exception:
        pass
    try:
        print('pitches attr:', getattr(n, 'pitches', None))
    except Exception:
        pass
    try:
        print('attributes:', [a for a in dir(n) if not a.startswith('_')])
    except Exception:
        pass
    try:
        print('volume.velocity:', getattr(getattr(n, 'volume', None), 'velocity', None))
    except Exception:
        pass
print('\nFinished')
