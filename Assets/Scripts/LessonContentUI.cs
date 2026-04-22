using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LessonContentUI : MonoBehaviour
{
    [Header("Lesson Content Text Fields")]
    [SerializeField] private TMP_Text lessonTitleText;
    [SerializeField] private TMP_Text lessonDescriptionText;
    [SerializeField] private TMP_Text lessonContentText;
    [SerializeField] private TMP_Text learningObjectivesText;
    [SerializeField] private TMP_Text exerciseTitleText;

    [Header("Action Buttons")]
    [SerializeField] private Button demonstrateButton;
    [SerializeField] private Button watchVideoButton;
    [SerializeField] private Button closeVideoButton;
    [SerializeField] private Button completeLessonButton;

    [Header("References")]
    [SerializeField] private BeatmapLibrary beatmapLibrary;
    [SerializeField] private BeatmapPlayer beatmapPlayer;
    [SerializeField] private GameModeManager gameModeManager;
    [SerializeField] private LessonVideoPlayerUI lessonVideoPlayerUI;

    private CourseLessonData currentLesson;
    private CourseModuleData currentModule;
    private DrumCourseData currentCourse;
    private CourseProgressManager progressManager;

    public event Action<CourseLessonData> OnLessonDisplayed;
    public event Action<string, string, string, string> OnCompleteLessonRequested;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        BindButtons();
    }

    private void ResolveReferences()
    {
        if (beatmapLibrary == null)
        {
            beatmapLibrary = FindFirstObjectByType<BeatmapLibrary>();
        }

        if (beatmapPlayer == null)
        {
            beatmapPlayer = FindFirstObjectByType<BeatmapPlayer>();
        }

        if (gameModeManager == null)
        {
            gameModeManager = FindFirstObjectByType<GameModeManager>();
        }

        if (progressManager == null)
        {
            progressManager = FindFirstObjectByType<CourseProgressManager>();
        }

        if (lessonVideoPlayerUI == null)
        {
            lessonVideoPlayerUI = FindFirstObjectByType<LessonVideoPlayerUI>();
        }
    }

    private void BindButtons()
    {
        if (demonstrateButton != null)
        {
            demonstrateButton.onClick.AddListener(OnDemonstrateClicked);
        }

        if (watchVideoButton != null)
        {
            watchVideoButton.onClick.AddListener(OnWatchVideoClicked);
        }

        if (closeVideoButton != null)
        {
            closeVideoButton.onClick.AddListener(OnCloseVideoClicked);
            closeVideoButton.gameObject.SetActive(false);
        }

        if (completeLessonButton != null)
        {
            completeLessonButton.onClick.AddListener(OnCompleteLessonClicked);
        }
    }

    private void OnDestroy()
    {
        if (demonstrateButton != null)
        {
            demonstrateButton.onClick.RemoveListener(OnDemonstrateClicked);
        }

        if (watchVideoButton != null)
        {
            watchVideoButton.onClick.RemoveListener(OnWatchVideoClicked);
        }

        if (closeVideoButton != null)
        {
            closeVideoButton.onClick.RemoveListener(OnCloseVideoClicked);
        }

        if (completeLessonButton != null)
        {
            completeLessonButton.onClick.RemoveListener(OnCompleteLessonClicked);
        }
    }

    /// <summary>
    /// Populate the lesson content panel with the given lesson data.
    /// </summary>
    public void DisplayLesson(CourseLessonData lesson, CourseModuleData module, DrumCourseData course)
    {
        if (lesson == null)
        {
            Debug.LogWarning("[LessonContentUI] Attempted to display null lesson.");
            return;
        }

        currentLesson = lesson;
        currentModule = module;
        currentCourse = course;

        // Populate text fields
        if (lessonTitleText != null)
        {
            lessonTitleText.text = lesson.title ?? string.Empty;
        }

        if (lessonDescriptionText != null)
        {
            lessonDescriptionText.text = lesson.description ?? string.Empty;
        }

        if (lessonContentText != null)
        {
            lessonContentText.text = lesson.objective ?? string.Empty;
        }

        if (learningObjectivesText != null)
        {
            learningObjectivesText.text = BuildLearningObjectivesText(lesson);
        }

        if (exerciseTitleText != null)
        {
            if (lesson.exercises != null && lesson.exercises.Count > 0)
            {
                exerciseTitleText.text = lesson.exercises[0].title ?? string.Empty;
            }
            else
            {
                exerciseTitleText.text = string.Empty;
            }
        }

        bool hasExercises = lesson.exercises != null && lesson.exercises.Count > 0;
        UpdateExerciseActionVisibility(hasExercises);
        TryAutoCompleteLessonWithoutExercises(hasExercises);

        bool hasPlayableVideo = lessonVideoPlayerUI != null && lessonVideoPlayerUI.HasPlayableVideo(lesson);

        if (watchVideoButton != null)
        {
            watchVideoButton.interactable = hasPlayableVideo;
        }

        if (!hasPlayableVideo)
        {
            if (lessonVideoPlayerUI != null)
            {
                lessonVideoPlayerUI.Hide();
            }

            if (closeVideoButton != null)
            {
                closeVideoButton.gameObject.SetActive(false);
            }
        }
        else if (lessonVideoPlayerUI != null)
        {
            bool hasPreview = lessonVideoPlayerUI.PreviewLesson(lesson);
            if (closeVideoButton != null)
            {
                closeVideoButton.gameObject.SetActive(hasPreview);
            }
        }
        else if (closeVideoButton != null)
        {
            closeVideoButton.gameObject.SetActive(false);
        }

        OnLessonDisplayed?.Invoke(lesson);
    }

    private void UpdateExerciseActionVisibility(bool hasExercises)
    {
        if (demonstrateButton != null)
        {
            demonstrateButton.gameObject.SetActive(hasExercises);
        }

        if (completeLessonButton != null)
        {
            completeLessonButton.gameObject.SetActive(hasExercises);
        }
    }

    private void TryAutoCompleteLessonWithoutExercises(bool hasExercises)
    {
        if (hasExercises || progressManager == null || currentCourse == null || currentModule == null || currentLesson == null)
        {
            return;
        }

        if (progressManager.IsLessonCompleted(currentCourse, currentModule, currentLesson))
        {
            return;
        }

        progressManager.MarkLessonCompleted(currentCourse.id, currentModule.id, currentLesson.id);
        progressManager.SaveProgress();
        OnCompleteLessonRequested?.Invoke(currentCourse.id, currentModule.id, currentLesson.id, "");
    }

    private void OnDemonstrateClicked()
    {
        if (currentLesson == null || currentLesson.exercises == null || currentLesson.exercises.Count == 0)
        {
            const string warningMessage = "[LessonContentUI] Cannot demonstrate: lesson has no exercises.";
            Debug.LogWarning(warningMessage);
            AppErrorPopup.Show(warningMessage);
            return;
        }

        // Get the first exercise's beatmap
        CourseExerciseData firstExercise = currentLesson.exercises[0];
        if (string.IsNullOrEmpty(firstExercise.beatmapTitle) && string.IsNullOrEmpty(firstExercise.beatmapJsonPath))
        {
            const string warningMessage = "[LessonContentUI] Cannot demonstrate: first exercise has no beatmap title or beatmap path.";
            Debug.LogWarning(warningMessage);
            AppErrorPopup.Show(warningMessage);
            return;
        }

        BeatmapData beatmap = null;

        // Try title lookup first (normal flow).
        if (beatmapLibrary != null && !string.IsNullOrEmpty(firstExercise.beatmapTitle))
        {
            beatmap = beatmapLibrary.GetBeatmapByTitle(firstExercise.beatmapTitle);
        }

        // If the beatmap was removed from the runtime library, recover it from disk path.
        if (beatmap == null)
        {
            beatmap = TryLoadBeatmapFromExercise(firstExercise);
        }

        if (beatmap != null && beatmapPlayer != null)
        {
            beatmapPlayer.LoadBeatmap(beatmap);
        }
        else
        {
            Debug.LogError($"[LessonContentUI] Could not load beatmap '{firstExercise.beatmapTitle}'.");
            return;
        }

        // Switch to freeplay mode to demonstrate
        if (gameModeManager != null)
        {
            gameModeManager.SwitchToFreePlayMode();
        }
    }

    private BeatmapData TryLoadBeatmapFromExercise(CourseExerciseData exercise)
    {
        if (beatmapLibrary == null || exercise == null || string.IsNullOrWhiteSpace(exercise.beatmapJsonPath))
        {
            return null;
        }

        string relativeJsonPath = exercise.beatmapJsonPath.Replace('/', Path.DirectorySeparatorChar);

        string persistentJsonPath = Path.Combine(Application.persistentDataPath, "Beatmaps", relativeJsonPath);
        string streamingJsonPath = Path.Combine(Application.streamingAssetsPath, "Beatmaps", relativeJsonPath);

        string selectedJsonPath = null;
        if (File.Exists(persistentJsonPath))
        {
            selectedJsonPath = persistentJsonPath;
        }
        else if (File.Exists(streamingJsonPath))
        {
            selectedJsonPath = streamingJsonPath;
        }

        if (string.IsNullOrEmpty(selectedJsonPath))
        {
            return null;
        }

        string folderPath = Path.GetDirectoryName(selectedJsonPath);
        if (string.IsNullOrEmpty(folderPath))
        {
            return null;
        }

        BeatmapData loadedBeatmap = beatmapLibrary.LoadBeatmapFromFolder(folderPath);
        if (loadedBeatmap != null)
        {
            beatmapLibrary.AddBeatmap(loadedBeatmap);
        }

        return loadedBeatmap;
    }

    private void OnCompleteLessonClicked()
    {
        if (currentLesson == null || currentModule == null || currentCourse == null)
        {
            const string warningMessage = "[LessonContentUI] Cannot complete lesson: missing course/module/lesson data.";
            Debug.LogWarning(warningMessage);
            AppErrorPopup.Show(warningMessage);
            return;
        }

        if (progressManager != null)
        {
            if (currentLesson.exercises == null || currentLesson.exercises.Count == 0)
            {
                progressManager.MarkLessonCompleted(currentCourse.id, currentModule.id, currentLesson.id);
            }
            else
            {
                // Mark all exercises in the lesson as completed with max score.
                foreach (CourseExerciseData exercise in currentLesson.exercises)
                {
                    progressManager.MarkExerciseCompleted(
                        currentCourse.id,
                        currentModule.id,
                        currentLesson.id,
                        exercise.id,
                        100 // Full score
                    );
                }
            }

            progressManager.SaveProgress();
        }

        OnCompleteLessonRequested?.Invoke(currentCourse.id, currentModule.id, currentLesson.id, "");
    }

    private void OnWatchVideoClicked()
    {
        if (lessonVideoPlayerUI == null)
        {
            const string warningMessage = "[LessonContentUI] No LessonVideoPlayerUI is assigned or available.";
            Debug.LogWarning(warningMessage);
            AppErrorPopup.Show(warningMessage);
            return;
        }

        if (!lessonVideoPlayerUI.PlayLesson(currentLesson))
        {
            const string warningMessage = "[LessonContentUI] No playable video source is configured for this lesson.";
            Debug.LogWarning(warningMessage);
            AppErrorPopup.Show(warningMessage);
            return;
        }

        if (closeVideoButton != null)
        {
            closeVideoButton.gameObject.SetActive(true);
        }
    }

    private void OnCloseVideoClicked()
    {
        if (lessonVideoPlayerUI != null)
        {
            lessonVideoPlayerUI.Hide();
        }

        if (closeVideoButton != null)
        {
            closeVideoButton.gameObject.SetActive(false);
        }
    }

    private string BuildLearningObjectivesText(CourseLessonData lesson)
    {
        if (lesson == null)
        {
            return string.Empty;
        }

        if (lesson.learningObjectives == null || lesson.learningObjectives.Count == 0)
        {
            return lesson.objective ?? string.Empty;
        }

        return "- " + string.Join("\n- ", lesson.learningObjectives);
    }

    /// <summary>
    /// Clear the lesson content panel.
    /// </summary>
    public void Clear()
    {
        currentLesson = null;
        currentModule = null;
        currentCourse = null;

        if (lessonTitleText != null)
        {
            lessonTitleText.text = string.Empty;
        }

        if (lessonDescriptionText != null)
        {
            lessonDescriptionText.text = string.Empty;
        }

        if (lessonContentText != null)
        {
            lessonContentText.text = string.Empty;
        }

        if (learningObjectivesText != null)
        {
            learningObjectivesText.text = string.Empty;
        }

        if (exerciseTitleText != null)
        {
            exerciseTitleText.text = string.Empty;
        }

        if (watchVideoButton != null)
        {
            watchVideoButton.interactable = false;
        }

        if (demonstrateButton != null)
        {
            demonstrateButton.gameObject.SetActive(false);
        }

        if (completeLessonButton != null)
        {
            completeLessonButton.gameObject.SetActive(false);
        }

        if (closeVideoButton != null)
        {
            closeVideoButton.gameObject.SetActive(false);
        }

        if (lessonVideoPlayerUI != null)
        {
            lessonVideoPlayerUI.Hide();
        }
    }
}
