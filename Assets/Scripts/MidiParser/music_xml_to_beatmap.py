import sys
import json
from pathlib import Path
from collections import defaultdict
from music21 import converter, tempo, chord, instrument, meter
import math

PITCH_TO_LANE = {
    36: 0,
    49: 1,
    42: 2, 44: 2, 46: 2, 23: 2, 21: 2,
    38: 3, 40: 3,
    48: 4,
    45: 5,
    43: 6,
    51: 7,
}

def extract_tempo_map(score):
    tlist = []
    for mm in score.recurse().getElementsByClass(tempo.MetronomeMark):
        bpm = mm.number if getattr(mm, 'number', None) is not None else 120.0
        tlist.append((float(mm.offset), float(bpm)))
    if not tlist:
        tlist = [(0.0, 120.0)]
    tlist.sort(key=lambda x: x[0])
    return tlist

def beat_to_seconds(abs_beat, tempo_map):
    if not tempo_map:
        tempo_map = [(0.0, 120.0)]
    time_s = 0.0
    current_beat = 0.0
    for i, (tbeat, bpm) in enumerate(tempo_map):
        if abs_beat < tbeat:
            break
        if tbeat > current_beat:
            prev_bpm = tempo_map[i-1][1] if i-1 >= 0 else tempo_map[0][1]
            dt_beats = tbeat - current_beat
            time_s += (dt_beats * 60.0) / prev_bpm
            current_beat = tbeat
    bpm_at_current = None
    for j in range(len(tempo_map)-1, -1, -1):
        if tempo_map[j][0] <= current_beat:
            bpm_at_current = tempo_map[j][1]
            break
    if bpm_at_current is None:
        bpm_at_current = tempo_map[0][1]
    remaining_beats = abs_beat - current_beat
    time_s += (remaining_beats * 60.0) / bpm_at_current
    return time_s

def pitch_to_midi(p):
    if p is None:
        return None
    try:
        if isinstance(p, int):
            return p
        return int(p.midi)
    except Exception:
        try:
            return int(str(p))
        except Exception:
            return None

def parse_musicxml_to_beatmap(infile, spawn_lead=0.0):
    score = converter.parse(infile)
    tempo_map = extract_tempo_map(score)
    is_midi = str(infile).lower().endswith(('.mid', '.midi'))
    events = []
    pitch_counts = defaultdict(int)

    if is_midi:
        from music21 import midi as m21midi
        mf = m21midi.MidiFile()
        mf.open(infile)
        mf.read()
        mf.close()
        tpq = mf.ticksPerQuarterNote or 480
        for tr in mf.tracks:
            cum_ticks = 0
            for ev in tr.events:
                cum_ticks += getattr(ev, 'time', 0)
                evtype = getattr(ev, 'type', None)
                if evtype == 144 and getattr(ev, 'velocity', 0) > 0:
                    pitch = getattr(ev, 'pitch', None)
                    vel = getattr(ev, 'velocity', 64)
                    if pitch is None:
                        continue
                    abs_beat = float(cum_ticks) / float(tpq)
                    time_s = beat_to_seconds(abs_beat, tempo_map)
                    events.append({'midi': int(pitch), 'time': round(float(time_s), 6), 'velocity': int(vel)})
                    pitch_counts[int(pitch)] += 1
    else:
        percussion_parts = []
        for part in score.parts:
            part_name = (part.partName or "").lower()
            instrs = [i for i in part.recurse().getElementsByClass(instrument.Instrument)]
            is_perc = False
            if instrs:
                for ins in instrs:
                    clsname = (ins.__class__.__name__ or '').lower()
                    partname = (ins.partName or '').lower()
                    if 'percuss' in clsname or 'drum' in partname or 'percussion' in partname:
                        is_perc = True
            if 'perc' in part.id.lower() or 'drum' in part_name or 'percussion' in part_name:
                is_perc = True
            note_count = sum(1 for n in part.recurse().notes)
            unpitched = sum(1 for n in part.recurse().notes if getattr(n, 'pitch', None) is None)
            if unpitched > 0 and unpitched / max(1, note_count) > 0.2:
                is_perc = True
            if is_perc:
                percussion_parts.append(part)

        if not percussion_parts:
            candidate_notes = [(n, None) for n in score.recurse().notes]
        else:
            candidate_notes = []
            for p in percussion_parts:
                candidate_notes.extend([(n, p) for n in p.recurse().notes])

        PERCUSSION_NAME_TO_MIDI = {
            'kick': 36, 'bass': 36, 'snare': 38, 'hi-hat': 42, 'hihat': 42,
            'closed hi-hat': 42, 'open hi-hat': 46, 'ride': 51, 'crash': 49, 'tom': 48, 'low tom': 45,
        }

        def map_unpitched_note_to_midi(n, parent_part=None):
            try:
                instr = n.getContextByClass(instrument.Instrument)
            except Exception:
                instr = None
            candidates = []
            if instr is not None:
                iname = (getattr(instr, 'partName', None) or '') or getattr(instr, 'instrumentName', '')
                if iname:
                    candidates.append(iname.lower())
            if parent_part is not None:
                pname = (parent_part.partName or '')
                if pname:
                    candidates.append(pname.lower())
            nh = getattr(n, 'notehead', None)
            if nh:
                candidates.append(str(nh).lower())
            combined = ' '.join(candidates)
            for key, midi in PERCUSSION_NAME_TO_MIDI.items():
                if key in combined:
                    return midi
            try:
                v = int(getattr(n, 'voice', 0) or 0)
            except Exception:
                v = 0
            staff = getattr(n, 'staff', None)
            if staff is not None:
                if int(staff) == 1:
                    return 42
                elif int(staff) == 2:
                    return 38
            if v == 1:
                return 36
            if v == 2:
                return 38
            return None

        for n, parent_part in candidate_notes:
            if isinstance(n, chord.Chord):
                pitches = list(n.pitches)
            else:
                pitches = []
                if getattr(n, 'pitch', None) is not None:
                    pitches = [n.pitch]
                else:
                    mapped_midi = map_unpitched_note_to_midi(n, parent_part)
                    if mapped_midi is not None:
                        try:
                            abs_beat = n.getOffsetInHierarchy(score)
                        except Exception:
                            abs_beat = n.offset
                        time_s = beat_to_seconds(abs_beat, tempo_map)
                        vel = 64
                        if getattr(n, 'volume', None) and getattr(n.volume, 'velocity', None):
                            vel = int(n.volume.velocity)
                        events.append({"midi": int(mapped_midi), "time": round(float(time_s), 6), "velocity": int(vel)})
                        pitch_counts[mapped_midi] += 1
                        continue
                    else:
                        continue

            for p in pitches:
                midi_num = pitch_to_midi(p)
                if midi_num is None:
                    continue
                pitch_counts[midi_num] += 1
                try:
                    abs_beat = n.getOffsetInHierarchy(score)
                except Exception:
                    abs_beat = n.offset
                time_s = beat_to_seconds(abs_beat, tempo_map)
                vel = 64
                if getattr(n, 'volume', None) and getattr(n.volume, 'velocity', None):
                    vel = int(n.volume.velocity)
                events.append({"midi": int(midi_num), "time": round(float(time_s), 6), "velocity": int(vel)})

    beatmap = []
    for e in events:
        lane = PITCH_TO_LANE.get(e["midi"])
        if lane is None:
            continue
        hit_time = float(e["time"])
        spawn_time = max(0.0, hit_time - float(spawn_lead))
        beatmap.append({"lane": int(lane), "time": round(hit_time,6), "spawnTime": round(spawn_time,6), "velocity": int(e["velocity"])})

    beatmap.sort(key=lambda x: x["time"])
    cleaned = []
    last = None
    for ev in beatmap:
        if last and ev['lane'] == last['lane'] and abs(ev['time'] - last['time']) < 0.002:
            continue
        cleaned.append(ev)
        last = ev

    return cleaned, tempo_map, pitch_counts, score

def extract_metadata(score, tempo_map, beatmap, infile):
    meta = getattr(score, 'metadata', None)
    title = getattr(meta, 'title', None) or Path(infile).stem
    bpms = [b for (_, b) in tempo_map]
    bpm_min = min(bpms) if bpms else None
    bpm_max = max(bpms) if bpms else None
    bpm_avg = sum(bpms)/len(bpms) if bpms else None
    ts_objs = list(score.recurse().getElementsByClass(meter.TimeSignature))
    time_signatures = sorted({ts.ratioString for ts in ts_objs}) if ts_objs else []
    length_seconds = max((e['time'] for e in beatmap), default=0.0)
    total_beats = None
    if bpm_avg and bpm_avg > 0:
        total_beats = (length_seconds / 60.0) * bpm_avg
    events_count = len(beatmap)
    lanes_used = sorted({e['lane'] for e in beatmap})
    density = events_count / length_seconds if length_seconds > 0 else 0.0
    return {
        "title": title,
        "source": str(infile),
        "tempo_map": tempo_map,
        "bpm_min": bpm_min,
        "bpm_max": bpm_max,
        "bpm_avg": round(bpm_avg,2) if bpm_avg else None,
        "time_signatures": time_signatures,
        "length_seconds": round(length_seconds,6),
        "approx_total_beats": round(total_beats,2) if total_beats else None,
        "events_count": events_count,
        "lanes_used": lanes_used,
        "events_per_second": round(density,3)
    }

def main():
    if len(sys.argv) < 3:
        print("Usage: python music_xml_to_beatmap.py input_musicxml/ output.json")
        sys.exit(1)
    infile = sys.argv[1]
    outfile = sys.argv[2]
    spawn_lead = float(sys.argv[3]) if len(sys.argv) >= 4 else 2.0
    beatmap, tempo_map, pitch_counts, score = parse_musicxml_to_beatmap(infile, spawn_lead=spawn_lead)
    metadata = extract_metadata(score, tempo_map, beatmap, infile)
    out_obj = {"metadata": metadata, "beatmap": beatmap}
    Path(outfile).parent.mkdir(parents=True, exist_ok=True)
    with open(outfile, "w", encoding="utf-8") as f:
        json.dump(out_obj, f, indent=2)
    print(f"Wrote {len(beatmap)} beatmap events to {outfile}")
    print("Metadata:", metadata)

if __name__ == "__main__":
    main()
