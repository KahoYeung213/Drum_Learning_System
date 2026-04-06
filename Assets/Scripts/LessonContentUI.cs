using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LessonContentUI : MonoBehaviour
{
    [Header("Lesson Content Text Fields")]
    [SerializeField] private TMP_Text lessonTitleText;
    [SerializeField] private TMP_Text lessonDescriptionText;
    [SerializeField] private TMP_Text lessonContentText;

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

    private void OnDemonstrateClicked()
    {
        if (currentLesson == null || currentLesson.exercises == null || currentLesson.exercises.Count == 0)
        {
            Debug.LogWarning("[LessonContentUI] Cannot demonstrate: lesson has no exercises.");
            return;
        }

        // Get the first exercise's beatmap
        CourseExerciseData firstExercise = currentLesson.exercises[0];
        if (string.IsNullOrEmpty(firstExercise.beatmapTitle))
        {
            Debug.LogWarning("[LessonContentUI] Cannot demonstrate: first exercise has no beatmap title.");
            return;
        }

        // Load the beatmap associated with the first exercise
        if (beatmapLibrary != null)
        {
            BeatmapData beatmap = beatmapLibrary.GetBeatmapByTitle(firstExercise.beatmapTitle);

            if (beatmap != null && beatmapPlayer != null)
            {
                beatmapPlayer.LoadBeatmap(beatmap);
            }
            else
            {
                Debug.LogError($"[LessonContentUI] Could not load beatmap '{firstExercise.beatmapTitle}'.");
                return;
            }
        }

        // Switch to freeplay mode to demonstrate
        if (gameModeManager != null)
        {
            gameModeManager.SwitchToFreePlayMode();
        }
    }

    private void OnCompleteLessonClicked()
    {
        if (currentLesson == null || currentModule == null || currentCourse == null)
        {
            Debug.LogWarning("[LessonContentUI] Cannot complete lesson: missing course/module/lesson data.");
            return;
        }

        if (progressManager != null)
        {
            // Mark all exercises in the lesson as completed with max score
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

            progressManager.SaveProgress();
        }

        OnCompleteLessonRequested?.Invoke(currentCourse.id, currentModule.id, currentLesson.id, "");
    }

    private void OnWatchVideoClicked()
    {
        if (lessonVideoPlayerUI == null)
        {
            Debug.LogWarning("[LessonContentUI] No LessonVideoPlayerUI is assigned or available.");
            return;
        }

        if (!lessonVideoPlayerUI.PlayLesson(currentLesson))
        {
            Debug.LogWarning("[LessonContentUI] No playable video source is configured for this lesson.");
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

        if (watchVideoButton != null)
        {
            watchVideoButton.interactable = false;
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
