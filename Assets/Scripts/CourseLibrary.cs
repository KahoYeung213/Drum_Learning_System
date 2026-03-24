using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class CourseLibrary : MonoBehaviour
{
    [Header("Course Data")]
    [SerializeField] private string jsonFileName = "courses.json";

    private readonly List<DrumCourseData> courses = new List<DrumCourseData>();

    public event Action<List<DrumCourseData>> OnCoursesLoaded;

    public IReadOnlyList<DrumCourseData> Courses => courses;

    private void Awake()
    {
        LoadCourses();
    }

    public void LoadCourses()
    {
        courses.Clear();

        string persistentPath = Path.Combine(Application.persistentDataPath, "Courses", jsonFileName);
        string streamingPath = Path.Combine(Application.streamingAssetsPath, "Courses", jsonFileName);

        string targetPath = null;
        if (File.Exists(persistentPath))
        {
            targetPath = persistentPath;
        }
        else if (File.Exists(streamingPath))
        {
            targetPath = streamingPath;
        }

        if (string.IsNullOrEmpty(targetPath))
        {
            Debug.LogWarning("[CourseLibrary] No course JSON found. Expected one of: " + persistentPath + " or " + streamingPath);
            OnCoursesLoaded?.Invoke(courses);
            return;
        }

        try
        {
            string json = File.ReadAllText(targetPath);
            CourseCatalogData catalog = JsonUtility.FromJson<CourseCatalogData>(json);

            if (catalog != null && catalog.courses != null)
            {
                courses.AddRange(catalog.courses);
            }

            Debug.Log($"[CourseLibrary] Loaded {courses.Count} courses from {targetPath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CourseLibrary] Failed to parse course JSON at {targetPath}. Error: {ex.Message}");
        }

        OnCoursesLoaded?.Invoke(courses);
    }

    public DrumCourseData GetCourseById(string courseId)
    {
        return courses.Find(c => c.id == courseId);
    }
}
