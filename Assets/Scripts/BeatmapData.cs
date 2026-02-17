using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class BeatmapNote
{
    public int lane;
    public float time;
    public float spawnTime;
    public float velocity;
}

[Serializable]
public class BeatmapData
{
    public string title;
    public string midiFilePath;
    public string audioFilePath;
    public string jsonFilePath;
    public BeatmapMetadata metadata;
    public List<BeatmapNote> beatmap;
    
    [Serializable]
    public class BeatmapMetadata
    {
        public string title;
        public string source;
        public float bpm_min;
        public float bpm_max;
        public float bpm_avg;
        public List<string> time_signatures;
        public float length_seconds;
        public float approx_total_beats;
        public int events_count;
        public List<int> lanes_used;
        public float events_per_second;
    }
    
    public string GetDisplayInfo()
    {
        if (metadata == null) return "No metadata available";
        
        string info = $"Duration: {metadata.length_seconds:F1}s\n";
        info += $"BPM: {metadata.bpm_avg:F0}";
        if (metadata.bpm_min != metadata.bpm_max)
            info += $" ({metadata.bpm_min:F0}-{metadata.bpm_max:F0})";
        info += $"\nNotes: {metadata.events_count}\n";
        info += $"Density: {metadata.events_per_second:F2} notes/sec";
        
        return info;
    }
}
