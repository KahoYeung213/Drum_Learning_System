using System;
using System.Collections.Generic;
using UnityEngine;

public class CourseProgressManager : MonoBehaviour
{
    private const string ProgressKey = "DrumCourseProgress_v1";

    [SerializeField] private bool saveOnCompletion = true;
    [SerializeField] private bool resetProgressOnStartupForTesting = false;
    [SerializeField] private bool hardResetAllPlayerPrefsOnStartupForTesting = false;

    [Serializable]
    private class CourseProgressSave
    {
        public List<CompletedExerciseEntry> completedExercises = new List<CompletedExerciseEntry>();
        public List<CompletedLessonEntry> completedLessons = new List<CompletedLessonEntry>();
    }

    [Serializable]
    private class CompletedExerciseEntry
    {
        public string key;
        public int bestScore;
    }

    [Serializable]
    private class CompletedLessonEntry
    {
        public string key;
    }

    private readonly Dictionary<string, int> completedExerciseScores = new Dictionary<string, int>();
    private readonly HashSet<string> completedLessonKeys = new HashSet<string>();

    private void Awake()
    {
        if (hardResetAllPlayerPrefsOnStartupForTesting)
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            completedExerciseScores.Clear();
            Debug.Log("[CourseProgressManager] HARD reset all PlayerPrefs on startup for testing.");
            return;
        }

        if (resetProgressOnStartupForTesting)
        {
            ResetAllProgress();
            Debug.Log("[CourseProgressManager] Progress reset on startup for testing.");
        }

        LoadProgress();
    }

    public void MarkExerciseCompleted(string courseId, string moduleId, string lessonId, string exerciseId, int score)
    {
        string key = BuildExerciseKey(courseId, moduleId, lessonId, exerciseId);

        if (completedExerciseScores.TryGetValue(key, out int previousBest))
        {
            completedExerciseScores[key] = Mathf.Max(previousBest, score);
        }
        else
        {
            completedExerciseScores.Add(key, score);
        }

        if (saveOnCompletion)
        {
            SaveProgress();
        }
    }

    public void MarkLessonCompleted(string courseId, string moduleId, string lessonId)
    {
        string key = BuildLessonKey(courseId, moduleId, lessonId);
        completedLessonKeys.Add(key);

        if (saveOnCompletion)
        {
            SaveProgress();
        }
    }

    public bool IsExerciseCompleted(string courseId, string moduleId, string lessonId, string exerciseId)
    {
        string key = BuildExerciseKey(courseId, moduleId, lessonId, exerciseId);
        return completedExerciseScores.ContainsKey(key);
    }

    public int GetBestExerciseScore(string courseId, string moduleId, string lessonId, string exerciseId)
    {
        string key = BuildExerciseKey(courseId, moduleId, lessonId, exerciseId);
        return completedExerciseScores.TryGetValue(key, out int score) ? score : 0;
    }

    public bool IsLessonUnlocked(DrumCourseData course, int moduleIndex, int lessonIndex)
    {
        if (course == null || course.modules == null || moduleIndex < 0 || moduleIndex >= course.modules.Count)
        {
            return false;
        }

        if (moduleIndex == 0 && lessonIndex == 0)
        {
            return true;
        }

        int prevModuleIndex = moduleIndex;
        int prevLessonIndex = lessonIndex - 1;

        if (prevLessonIndex < 0)
        {
            prevModuleIndex = moduleIndex - 1;
            if (prevModuleIndex < 0 || prevModuleIndex >= course.modules.Count)
            {
                return false;
            }

            int lessonCount = course.modules[prevModuleIndex].lessons != null ? course.modules[prevModuleIndex].lessons.Count : 0;
            prevLessonIndex = lessonCount - 1;
        }

        if (prevModuleIndex < 0 || prevModuleIndex >= course.modules.Count)
        {
            return false;
        }

        CourseModuleData prevModule = course.modules[prevModuleIndex];
        if (prevModule.lessons == null || prevLessonIndex < 0 || prevLessonIndex >= prevModule.lessons.Count)
        {
            return false;
        }

        return IsLessonCompleted(course, prevModule, prevModule.lessons[prevLessonIndex]);
    }

    public bool IsLessonCompleted(DrumCourseData course, CourseModuleData module, CourseLessonData lesson)
    {
        if (course == null || module == null || lesson == null)
        {
            return false;
        }

        if (completedLessonKeys.Contains(BuildLessonKey(course.id, module.id, lesson.id)))
        {
            return true;
        }

        if (lesson.exercises == null || lesson.exercises.Count == 0)
        {
            return false;
        }

        foreach (CourseExerciseData exercise in lesson.exercises)
        {
            if (!IsExerciseCompleted(course.id, module.id, lesson.id, exercise.id))
            {
                return false;
            }
        }

        return true;
    }

    public float GetLessonCompletionPercent(DrumCourseData course, CourseModuleData module, CourseLessonData lesson)
    {
        if (course == null || module == null || lesson == null)
        {
            return 0f;
        }

        if (completedLessonKeys.Contains(BuildLessonKey(course.id, module.id, lesson.id)))
        {
            return 1f;
        }

        if (lesson.exercises == null || lesson.exercises.Count == 0)
        {
            return 0f;
        }

        int completed = 0;
        foreach (CourseExerciseData exercise in lesson.exercises)
        {
            if (IsExerciseCompleted(course.id, module.id, lesson.id, exercise.id))
            {
                completed++;
            }
        }

        return (float)completed / lesson.exercises.Count;
    }

    public void SaveProgress()
    {
        CourseProgressSave save = new CourseProgressSave();

        foreach (KeyValuePair<string, int> pair in completedExerciseScores)
        {
            save.completedExercises.Add(new CompletedExerciseEntry
            {
                key = pair.Key,
                bestScore = pair.Value
            });
        }

        foreach (string lessonKey in completedLessonKeys)
        {
            save.completedLessons.Add(new CompletedLessonEntry
            {
                key = lessonKey
            });
        }

        string json = JsonUtility.ToJson(save);
        PlayerPrefs.SetString(ProgressKey, json);
        PlayerPrefs.Save();
    }

    public void LoadProgress()
    {
        completedExerciseScores.Clear();

        if (!PlayerPrefs.HasKey(ProgressKey))
        {
            return;
        }

        string json = PlayerPrefs.GetString(ProgressKey, string.Empty);
        if (string.IsNullOrEmpty(json))
        {
            return;
        }

        try
        {
            CourseProgressSave save = JsonUtility.FromJson<CourseProgressSave>(json);
            if (save == null || save.completedExercises == null)
            {
                return;
            }

            foreach (CompletedExerciseEntry entry in save.completedExercises)
            {
                if (!string.IsNullOrEmpty(entry.key))
                {
                    completedExerciseScores[entry.key] = entry.bestScore;
                }
            }

            if (save.completedLessons != null)
            {
                foreach (CompletedLessonEntry entry in save.completedLessons)
                {
                    if (!string.IsNullOrEmpty(entry.key))
                    {
                        completedLessonKeys.Add(entry.key);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[CourseProgressManager] Failed to load progress: {ex.Message}");
        }
    }

    public void ResetAllProgress()
    {
        completedExerciseScores.Clear();
        completedLessonKeys.Clear();
        PlayerPrefs.DeleteKey(ProgressKey);
        PlayerPrefs.Save();
    }

    [ContextMenu("Testing/Reset Course Progress (Key Only)")]
    private void ContextResetCourseProgress()
    {
        ResetAllProgress();
        Debug.Log("[CourseProgressManager] Context reset: course progress key deleted.");
    }

    [ContextMenu("Testing/Hard Reset All PlayerPrefs")]
    private void ContextHardResetAllPlayerPrefs()
    {
        completedExerciseScores.Clear();
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("[CourseProgressManager] Context HARD reset: all PlayerPrefs deleted.");
    }

    private static string BuildExerciseKey(string courseId, string moduleId, string lessonId, string exerciseId)
    {
        return $"{courseId}|{moduleId}|{lessonId}|{exerciseId}";
    }

    private static string BuildLessonKey(string courseId, string moduleId, string lessonId)
    {
        return $"{courseId}|{moduleId}|{lessonId}";
    }
}
