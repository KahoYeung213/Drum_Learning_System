using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CourseScaffoldingUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CourseLibrary courseLibrary;
    [SerializeField] private CourseProgressManager progressManager;
    [SerializeField] private BeatmapLibrary beatmapLibrary;
    [SerializeField] private BeatmapPlayer beatmapPlayer;
    [SerializeField] private GameModeManager gameModeManager;

    [Header("Scaffolding Lists")]
    [SerializeField] private Transform courseListRoot;
    [SerializeField] private Transform moduleListRoot;
    [SerializeField] private Transform lessonListRoot;
    [SerializeField] private Transform exerciseListRoot;
    [SerializeField] private GameObject listItemButtonPrefab;
    [SerializeField] private GameObject courseItemButtonPrefab;
    [SerializeField] private GameObject moduleItemButtonPrefab;
    [SerializeField] private GameObject lessonItemButtonPrefab;
    [SerializeField] private GameObject exerciseItemButtonPrefab;

    [Header("Module Accordion (Sidebar)")]
    [SerializeField] private bool useModuleAccordionSidebar = true;
    [SerializeField] private bool allowCollapseExpandedModule = true;
    [SerializeField] private bool autoSelectLessonOnCourseLoad = false;
    [SerializeField] private bool autoSelectLessonWhenModuleExpanded = false;
    [SerializeField] private bool normalizeSpawnedItemUIState = true;
    [SerializeField] private string moduleHeaderButtonPath = "Header";
    [SerializeField] private string moduleTitleTextPath = "Header/Title";
    [SerializeField] private string moduleProgressSliderPath = "Header/ProgressBar";
    [SerializeField] private string moduleProgressFillImagePath = "";
    [SerializeField] private string moduleProgressTextPath = "Header/ProgressText";
    [SerializeField] private string moduleLessonsContainerPath = "LessonsContainer";
    [SerializeField] private string courseItemTextPath = "Title";
    [SerializeField] private string lessonItemTextPath = "Title";
    [SerializeField] private string exerciseItemTextPath = "Title";

    [Header("Details Panel")]
    [SerializeField] private TMP_Text breadcrumbText;
    [SerializeField] private TMP_Text lessonTitleText;
    [SerializeField] private TMP_Text lessonDescriptionText;
    [SerializeField] private TMP_Text lessonObjectiveText;
    [SerializeField] private TMP_Text lessonProgressText;
    [SerializeField] private TMP_Text selectedExerciseText;

    [Header("Actions")]
    [SerializeField] private Button switchToCourseButton;
    [SerializeField] private Button backToPracticeButton;
    [SerializeField] private Button playExerciseButton;
    [SerializeField] private Button completeExerciseButton;
    [SerializeField] private Button nextLessonButton;

    private readonly List<GameObject> generatedItems = new List<GameObject>();

    private DrumCourseData selectedCourse;
    private CourseModuleData selectedModule;
    private CourseLessonData selectedLesson;
    private CourseExerciseData selectedExercise;
    private int selectedModuleIndex;
    private int selectedLessonIndex;
    private int expandedModuleIndex = -1;

    private void Start()
    {
        ResolveReferences();
        BindButtons();

        if (courseLibrary != null)
        {
            courseLibrary.OnCoursesLoaded += HandleCoursesLoaded;
            HandleCoursesLoaded(new List<DrumCourseData>(courseLibrary.Courses));
        }
        else
        {
            Debug.LogError("[CourseScaffoldingUI] CourseLibrary is missing.");
        }
    }

    private void OnDestroy()
    {
        if (courseLibrary != null)
        {
            courseLibrary.OnCoursesLoaded -= HandleCoursesLoaded;
        }
    }

    private void ResolveReferences()
    {
        if (courseLibrary == null)
        {
            courseLibrary = FindFirstObjectByType<CourseLibrary>();
        }

        if (progressManager == null)
        {
            progressManager = FindFirstObjectByType<CourseProgressManager>();
        }

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
    }

    private void BindButtons()
    {
        BindButton(switchToCourseButton, OnSwitchToCourseClicked);
        BindButton(backToPracticeButton, OnBackToPracticeClicked);
        BindButton(playExerciseButton, OnPlayExerciseClicked);
        BindButton(completeExerciseButton, OnCompleteExerciseClicked);
        BindButton(nextLessonButton, OnNextLessonClicked);
    }

    private static void BindButton(Button button, Action callback)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => callback?.Invoke());
    }

    private void HandleCoursesLoaded(List<DrumCourseData> courses)
    {
        PopulateCourses(courses);

        if (courses.Count > 0)
        {
            SelectCourse(courses[0]);
        }
        else
        {
            SetDetailText("No course data found.", string.Empty, string.Empty, string.Empty, string.Empty);
        }
    }

    private void PopulateCourses(List<DrumCourseData> courses)
    {
        ClearList(courseListRoot);

        foreach (DrumCourseData course in courses)
        {
            GameObject item = CreateListItem(courseListRoot, GetPrefabForList(ListType.Course), $"{course.title} ({course.difficulty})", false, () => SelectCourse(course), courseItemTextPath);
            if (item != null)
            {
                generatedItems.Add(item);
            }
        }
    }

    private void SelectCourse(DrumCourseData course)
    {
        selectedCourse = course;
        selectedModule = null;
        selectedLesson = null;
        selectedExercise = null;
        selectedModuleIndex = 0;
        selectedLessonIndex = 0;
        expandedModuleIndex = -1;

        PopulateModules();

        if (autoSelectLessonOnCourseLoad && !TrySelectFirstUnlockedLesson())
        {
            SetDetailText("No unlocked lessons found.", string.Empty, string.Empty, string.Empty, string.Empty);
            ClearList(exerciseListRoot);
        }
    }

    private void PopulateModules()
    {
        ClearList(moduleListRoot);

        // Keep the separate lesson panel empty when module accordion sidebar is in use.
        ClearList(lessonListRoot);

        ClearList(exerciseListRoot);

        if (selectedCourse == null || selectedCourse.modules == null)
        {
            return;
        }

        for (int i = 0; i < selectedCourse.modules.Count; i++)
        {
            CourseModuleData module = selectedCourse.modules[i];
            int capturedIndex = i;

            GameObject item = CreateListItem(moduleListRoot, GetPrefabForList(ListType.Module), module.title, false, () => SelectModule(capturedIndex));
            if (item != null)
            {
                ConfigureModuleAccordionItem(item, module, capturedIndex);
                generatedItems.Add(item);
            }
        }
    }

    private void SelectModule(int moduleIndex)
    {
        if (selectedCourse == null || selectedCourse.modules == null || moduleIndex < 0 || moduleIndex >= selectedCourse.modules.Count)
        {
            return;
        }

        selectedModuleIndex = moduleIndex;
        selectedModule = selectedCourse.modules[moduleIndex];
        selectedLesson = null;
        selectedExercise = null;
        selectedLessonIndex = -1;
        expandedModuleIndex = moduleIndex;

        if (useModuleAccordionSidebar)
        {
            PopulateModules();
            if (autoSelectLessonWhenModuleExpanded)
            {
                SelectFirstUnlockedLessonInModule(moduleIndex);
            }
        }
        else
        {
            PopulateLessons();
        }
    }

    private void PopulateLessons()
    {
        if (useModuleAccordionSidebar)
        {
            return;
        }

        ClearList(lessonListRoot);
        ClearList(exerciseListRoot);

        if (selectedCourse == null || selectedModule == null || selectedModule.lessons == null)
        {
            return;
        }

        for (int i = 0; i < selectedModule.lessons.Count; i++)
        {
            CourseLessonData lesson = selectedModule.lessons[i];
            int capturedIndex = i;

            bool unlocked = progressManager == null || progressManager.IsLessonUnlocked(selectedCourse, selectedModuleIndex, i);
            string title = unlocked ? lesson.title : "[Locked] " + lesson.title;

            GameObject item = CreateListItem(lessonListRoot, GetPrefabForList(ListType.Lesson), title, !unlocked, () =>
            {
                if (unlocked)
                {
                    SelectLesson(selectedModuleIndex, capturedIndex);
                }
            }, lessonItemTextPath);

            if (item != null)
            {
                generatedItems.Add(item);
            }
        }

        for (int i = 0; i < selectedModule.lessons.Count; i++)
        {
            bool unlocked = progressManager == null || progressManager.IsLessonUnlocked(selectedCourse, selectedModuleIndex, i);
            if (unlocked)
            {
                SelectLesson(selectedModuleIndex, i);
                return;
            }
        }

        SetDetailText("All lessons in this module are locked.", string.Empty, string.Empty, string.Empty, string.Empty);
    }

    private void SelectLesson(int moduleIndex, int lessonIndex)
    {
        if (selectedCourse == null || selectedCourse.modules == null || moduleIndex < 0 || moduleIndex >= selectedCourse.modules.Count)
        {
            return;
        }

        CourseModuleData module = selectedCourse.modules[moduleIndex];
        if (module.lessons == null || lessonIndex < 0 || lessonIndex >= module.lessons.Count)
        {
            return;
        }

        selectedModuleIndex = moduleIndex;
        selectedModule = module;

        selectedLessonIndex = lessonIndex;
        selectedLesson = module.lessons[lessonIndex];
        selectedExercise = null;

        PopulateExercises();
        RefreshDetails();
    }

    private void PopulateExercises()
    {
        ClearList(exerciseListRoot);

        if (selectedCourse == null || selectedModule == null || selectedLesson == null || selectedLesson.exercises == null)
        {
            return;
        }

        foreach (CourseExerciseData exercise in selectedLesson.exercises)
        {
            bool completed = progressManager != null && progressManager.IsExerciseCompleted(selectedCourse.id, selectedModule.id, selectedLesson.id, exercise.id);
            string title = completed ? "[Done] " + exercise.title : exercise.title;

            GameObject item = CreateListItem(exerciseListRoot, GetPrefabForList(ListType.Exercise), title, false, () => SelectExercise(exercise), exerciseItemTextPath);
            if (item != null)
            {
                generatedItems.Add(item);
            }
        }

        if (selectedLesson.exercises.Count > 0)
        {
            SelectExercise(selectedLesson.exercises[0]);
        }
    }

    private void SelectExercise(CourseExerciseData exercise)
    {
        selectedExercise = exercise;
        RefreshDetails();
    }

    private void RefreshDetails()
    {
        if (selectedCourse == null || selectedModule == null || selectedLesson == null)
        {
            SetDetailText(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
            return;
        }

        string breadcrumb = selectedCourse.title + " / " + selectedModule.title + " / " + selectedLesson.title;

        string progress = "0%";
        if (progressManager != null)
        {
            float percent = progressManager.GetLessonCompletionPercent(selectedCourse, selectedModule, selectedLesson) * 100f;
            progress = percent.ToString("F0") + "%";
        }

        string exerciseInfo = selectedExercise != null
            ? selectedExercise.title + " (Required score: " + selectedExercise.requiredScore + ")"
            : "No exercise selected";

        SetDetailText(breadcrumb, selectedLesson.title, selectedLesson.description, selectedLesson.objective, "Lesson Progress: " + progress);

        if (selectedExerciseText != null)
        {
            selectedExerciseText.text = exerciseInfo;
        }

        if (nextLessonButton != null)
        {
            bool canAdvance = progressManager != null && progressManager.IsLessonCompleted(selectedCourse, selectedModule, selectedLesson);
            nextLessonButton.interactable = canAdvance;
        }
    }

    private void OnPlayExerciseClicked()
    {
        if (selectedExercise == null || beatmapLibrary == null || beatmapPlayer == null)
        {
            return;
        }

        BeatmapData beatmap = null;

        if (!string.IsNullOrEmpty(selectedExercise.beatmapTitle))
        {
            beatmap = beatmapLibrary.GetBeatmapByTitle(selectedExercise.beatmapTitle);
        }

        if (beatmap == null && !string.IsNullOrEmpty(selectedExercise.beatmapJsonPath))
        {
            foreach (BeatmapData item in beatmapLibrary.Beatmaps)
            {
                if (item != null && !string.IsNullOrEmpty(item.jsonFilePath) && item.jsonFilePath.EndsWith(selectedExercise.beatmapJsonPath, StringComparison.OrdinalIgnoreCase))
                {
                    beatmap = item;
                    break;
                }
            }
        }

        if (beatmap == null)
        {
            Debug.LogWarning("[CourseScaffoldingUI] Could not find beatmap for exercise: " + selectedExercise.title);
            return;
        }

        beatmapPlayer.LoadBeatmap(beatmap);
    }

    private void OnCompleteExerciseClicked()
    {
        if (selectedCourse == null || selectedModule == null || selectedLesson == null || selectedExercise == null || progressManager == null)
        {
            return;
        }

        progressManager.MarkExerciseCompleted(selectedCourse.id, selectedModule.id, selectedLesson.id, selectedExercise.id, selectedExercise.requiredScore);

        if (useModuleAccordionSidebar)
        {
            PopulateModules();
            SelectLesson(selectedModuleIndex, selectedLessonIndex);
        }
        else
        {
            PopulateLessons();
            SelectLesson(selectedModuleIndex, selectedLessonIndex);
        }
    }

    private void OnNextLessonClicked()
    {
        if (selectedCourse == null || selectedCourse.modules == null)
        {
            return;
        }

        for (int moduleIndex = selectedModuleIndex; moduleIndex < selectedCourse.modules.Count; moduleIndex++)
        {
            CourseModuleData module = selectedCourse.modules[moduleIndex];
            if (module.lessons == null)
            {
                continue;
            }

            int startLesson = moduleIndex == selectedModuleIndex ? selectedLessonIndex + 1 : 0;
            for (int lessonIndex = startLesson; lessonIndex < module.lessons.Count; lessonIndex++)
            {
                bool unlocked = progressManager == null || progressManager.IsLessonUnlocked(selectedCourse, moduleIndex, lessonIndex);
                if (unlocked)
                {
                    SelectLesson(moduleIndex, lessonIndex);
                    return;
                }
            }
        }
    }

    private bool TrySelectFirstUnlockedLesson()
    {
        if (selectedCourse == null || selectedCourse.modules == null)
        {
            return false;
        }

        for (int moduleIndex = 0; moduleIndex < selectedCourse.modules.Count; moduleIndex++)
        {
            CourseModuleData module = selectedCourse.modules[moduleIndex];
            if (module.lessons == null)
            {
                continue;
            }

            for (int lessonIndex = 0; lessonIndex < module.lessons.Count; lessonIndex++)
            {
                bool unlocked = progressManager == null || progressManager.IsLessonUnlocked(selectedCourse, moduleIndex, lessonIndex);
                if (unlocked)
                {
                    SelectLesson(moduleIndex, lessonIndex);
                    return true;
                }
            }
        }

        return false;
    }

    private void SelectFirstUnlockedLessonInModule(int moduleIndex)
    {
        if (selectedCourse == null || selectedCourse.modules == null || moduleIndex < 0 || moduleIndex >= selectedCourse.modules.Count)
        {
            return;
        }

        CourseModuleData module = selectedCourse.modules[moduleIndex];
        if (module.lessons == null)
        {
            return;
        }

        for (int i = 0; i < module.lessons.Count; i++)
        {
            bool unlocked = progressManager == null || progressManager.IsLessonUnlocked(selectedCourse, moduleIndex, i);
            if (unlocked)
            {
                SelectLesson(moduleIndex, i);
                return;
            }
        }

        SetDetailText("All lessons in this module are locked.", string.Empty, string.Empty, string.Empty, string.Empty);
        ClearList(exerciseListRoot);
    }

    private void ConfigureModuleAccordionItem(GameObject moduleItem, CourseModuleData module, int moduleIndex)
    {
        if (moduleItem == null || module == null)
        {
            return;
        }

        SetItemTextByPath(moduleItem.transform, moduleTitleTextPath, module.title);

        float moduleProgress = GetModuleCompletionPercent(selectedCourse, module);
        SetModuleProgressUI(moduleItem.transform, moduleProgress);

        Transform headerTransform = FindByPathOrSelf(moduleItem.transform, moduleHeaderButtonPath);
        Button headerButton = null;

        if (headerTransform != null)
        {
            headerButton = headerTransform.GetComponent<Button>();
            if (headerButton == null)
            {
                headerButton = headerTransform.GetComponentInChildren<Button>(true);
            }
        }

        // Fallback: if configured path is wrong, bind to first button in this module item.
        if (headerButton == null)
        {
            headerButton = moduleItem.GetComponentInChildren<Button>(true);
            if (headerButton != null)
            {
                Debug.LogWarning($"[CourseScaffoldingUI] Header button path '{moduleHeaderButtonPath}' not found. Using fallback button '{headerButton.name}' for module '{module.title}'.");
            }
        }
        
        if (headerButton != null)
        {
            headerButton.onClick.RemoveAllListeners();
            UIAccordionElement accordionElement = moduleItem.GetComponent<UIAccordionElement>();
            if (accordionElement != null)
            {
                headerButton.onClick.AddListener(() =>
                {
                    bool nextState = !accordionElement.isOn;
                    if (!allowCollapseExpandedModule && !nextState)
                    {
                        nextState = true;
                    }

                    accordionElement.isOn = nextState;
                    expandedModuleIndex = nextState ? moduleIndex : -1;

                    if (nextState && autoSelectLessonWhenModuleExpanded)
                    {
                        SelectFirstUnlockedLessonInModule(moduleIndex);
                    }
                });
            }
            else
            {
                headerButton.onClick.AddListener(() => ToggleModuleAccordion(moduleIndex));
            }

            Debug.Log($"[CourseScaffoldingUI] Module header button wired for module '{module.title}'");
        }
        else
        {
            Debug.LogWarning($"[CourseScaffoldingUI] Could not find Button component on or under Header path '{moduleHeaderButtonPath}' for module '{module.title}'");
        }

        if (!useModuleAccordionSidebar)
        {
            return;
        }

        Transform lessonsContainer = FindByPathStrict(moduleItem.transform, moduleLessonsContainerPath);
        if (lessonsContainer == null)
        {
            Debug.LogWarning("[CourseScaffoldingUI] Module lessons container not found for module prefab. Check moduleLessonsContainerPath.");
            return;
        }

        bool expanded = moduleIndex == expandedModuleIndex;

        UIAccordionElement itemAccordionElement = moduleItem.GetComponent<UIAccordionElement>();
        if (itemAccordionElement != null)
        {
            itemAccordionElement.isOn = expanded;
        }
        else
        {
            lessonsContainer.gameObject.SetActive(expanded);
            if (!expanded)
            {
                return;
            }
        }

        ClearList(lessonsContainer);
        if (module.lessons == null)
        {
            return;
        }

        for (int lessonIndex = 0; lessonIndex < module.lessons.Count; lessonIndex++)
        {
            CourseLessonData lesson = module.lessons[lessonIndex];
            bool unlocked = progressManager == null || progressManager.IsLessonUnlocked(selectedCourse, moduleIndex, lessonIndex);
            bool completed = progressManager != null && progressManager.IsLessonCompleted(selectedCourse, module, lesson);
            string lessonTitle = BuildLessonLabel(lesson.title, unlocked, completed);

            int capturedLessonIndex = lessonIndex;
            CreateListItem(
                lessonsContainer,
                GetPrefabForList(ListType.Lesson),
                lessonTitle,
                !unlocked,
                () =>
                {
                    if (unlocked)
                    {
                        SelectLesson(moduleIndex, capturedLessonIndex);
                    }
                },
                lessonItemTextPath);
        }
    }

    private string BuildLessonLabel(string lessonTitle, bool unlocked, bool completed)
    {
        if (!unlocked)
        {
            return "[Locked] " + lessonTitle;
        }

        if (completed)
        {
            return "[Done] " + lessonTitle;
        }

        return lessonTitle;
    }

    private float GetModuleCompletionPercent(DrumCourseData course, CourseModuleData module)
    {
        if (progressManager == null || course == null || module == null || module.lessons == null || module.lessons.Count == 0)
        {
            return 0f;
        }

        int completedLessons = 0;
        foreach (CourseLessonData lesson in module.lessons)
        {
            if (progressManager.IsLessonCompleted(course, module, lesson))
            {
                completedLessons++;
            }
        }

        return (float)completedLessons / module.lessons.Count;
    }

    private void SetModuleProgressUI(Transform moduleItemTransform, float progress)
    {
        progress = Mathf.Clamp01(progress);

        if (!string.IsNullOrEmpty(moduleProgressSliderPath))
        {
            Transform sliderTransform = moduleItemTransform.Find(moduleProgressSliderPath);
            if (sliderTransform != null)
            {
                Slider slider = sliderTransform.GetComponent<Slider>();
                if (slider != null)
                {
                    slider.value = progress;
                }
            }
        }

        if (!string.IsNullOrEmpty(moduleProgressFillImagePath))
        {
            Transform fillTransform = moduleItemTransform.Find(moduleProgressFillImagePath);
            if (fillTransform != null)
            {
                Image fillImage = fillTransform.GetComponent<Image>();
                if (fillImage != null)
                {
                    fillImage.fillAmount = progress;
                }
            }
        }

        if (!string.IsNullOrEmpty(moduleProgressTextPath))
        {
            string percent = Mathf.RoundToInt(progress * 100f) + "%";
            SetItemTextByPath(moduleItemTransform, moduleProgressTextPath, percent);
        }
    }

    private void SetItemTextByPath(Transform root, string path, string value)
    {
        if (root == null)
        {
            return;
        }

        Transform target = FindByPathOrSelf(root, path);
        if (target == null)
        {
            TMP_Text fallbackTmp = FindBestTitleText(root);
            if (fallbackTmp != null)
            {
                fallbackTmp.text = value;
                return;
            }

            Text fallbackText = FindBestLegacyTitleText(root);
            if (fallbackText != null)
            {
                fallbackText.text = value;
            }

            return;
        }

        TMP_Text tmpText = target.GetComponent<TMP_Text>();
        if (tmpText != null)
        {
            tmpText.text = value;
            return;
        }

        Text text = target.GetComponent<Text>();
        if (text != null)
        {
            text.text = value;
        }
    }

    private TMP_Text FindBestTitleText(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        string[] preferredNames = { "Title", "HeaderText", "Label", "Text (TMP)", "Text" };
        foreach (string childName in preferredNames)
        {
            Transform t = root.Find(childName);
            if (t != null)
            {
                TMP_Text named = t.GetComponent<TMP_Text>();
                if (named != null)
                {
                    return named;
                }
            }
        }

        TMP_Text[] all = root.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text text in all)
        {
            string n = text.name;
            if (n.IndexOf("title", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("label", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return text;
            }
        }

        return all.Length > 0 ? all[0] : null;
    }

    private Text FindBestLegacyTitleText(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        Text[] all = root.GetComponentsInChildren<Text>(true);
        foreach (Text text in all)
        {
            string n = text.name;
            if (n.IndexOf("title", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("label", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return text;
            }
        }

        return all.Length > 0 ? all[0] : null;
    }

    private Transform FindByPathOrSelf(Transform root, string path)
    {
        if (root == null)
        {
            return null;
        }

        if (string.IsNullOrEmpty(path))
        {
            return root;
        }

        Transform child = root.Find(path);
        return child;
    }

    private Transform FindByPathStrict(Transform root, string path)
    {
        if (root == null || string.IsNullOrEmpty(path))
        {
            return null;
        }

        return root.Find(path);
    }

    private void ToggleModuleAccordion(int moduleIndex)
    {
        if (!useModuleAccordionSidebar)
        {
            SelectModule(moduleIndex);
            return;
        }

        bool isSameExpandedModule = expandedModuleIndex == moduleIndex;
        if (isSameExpandedModule && allowCollapseExpandedModule)
        {
            expandedModuleIndex = -1;
            selectedModuleIndex = moduleIndex;
            selectedModule = selectedCourse != null && selectedCourse.modules != null && moduleIndex >= 0 && moduleIndex < selectedCourse.modules.Count
                ? selectedCourse.modules[moduleIndex]
                : null;
            PopulateModules();
            return;
        }

        expandedModuleIndex = moduleIndex;
        selectedModuleIndex = moduleIndex;
        selectedModule = selectedCourse != null && selectedCourse.modules != null && moduleIndex >= 0 && moduleIndex < selectedCourse.modules.Count
            ? selectedCourse.modules[moduleIndex]
            : null;

        PopulateModules();

        if (autoSelectLessonWhenModuleExpanded)
        {
            SelectFirstUnlockedLessonInModule(moduleIndex);
        }
    }

    private void OnSwitchToCourseClicked()
    {
        if (gameModeManager != null)
        {
            gameModeManager.SwitchToCourseMode();
        }
    }

    private void OnBackToPracticeClicked()
    {
        if (gameModeManager != null)
        {
            gameModeManager.SetMode(true);
        }
    }

    private enum ListType
    {
        Course,
        Module,
        Lesson,
        Exercise
    }

    private GameObject GetPrefabForList(ListType listType)
    {
        GameObject typedPrefab = listType switch
        {
            ListType.Course => courseItemButtonPrefab,
            ListType.Module => moduleItemButtonPrefab,
            ListType.Lesson => lessonItemButtonPrefab,
            ListType.Exercise => exerciseItemButtonPrefab,
            _ => null
        };

        return typedPrefab != null ? typedPrefab : listItemButtonPrefab;
    }

    private GameObject CreateListItem(Transform root, GameObject prefab, string title, bool disabled, Action onClick, string preferredTextPath = "")
    {
        if (root == null || prefab == null)
        {
            return null;
        }

        GameObject item = Instantiate(prefab, root);
        item.SetActive(true);

        if (normalizeSpawnedItemUIState)
        {
            NormalizeSpawnedItemUI(item);
        }

        Button button = item.GetComponent<Button>();
        if (button != null)
        {
            button.interactable = !disabled;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick?.Invoke());
        }

        if (!string.IsNullOrEmpty(preferredTextPath))
        {
            SetItemTextByPath(item.transform, preferredTextPath, title);
        }
        else
        {
            TMP_Text tmpText = item.GetComponentInChildren<TMP_Text>(true);
            if (tmpText != null)
            {
                tmpText.text = title;
            }
            else
            {
                Text uiText = item.GetComponentInChildren<Text>(true);
                if (uiText != null)
                {
                    uiText.text = title;
                }
            }
        }

        return item;
    }

    private void NormalizeSpawnedItemUI(GameObject item)
    {
        if (item == null)
        {
            return;
        }

        // Ensure all child GameObjects are active unless accordion logic later collapses a specific container.
        Transform[] allChildren = item.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in allChildren)
        {
            if (!child.gameObject.activeSelf)
            {
                child.gameObject.SetActive(true);
            }
        }

        // Reset common UI component states that can be unintentionally saved disabled in prefab overrides.
        CanvasGroup[] canvasGroups = item.GetComponentsInChildren<CanvasGroup>(true);
        foreach (CanvasGroup group in canvasGroups)
        {
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
            group.enabled = true;
        }

        Graphic[] graphics = item.GetComponentsInChildren<Graphic>(true);
        foreach (Graphic graphic in graphics)
        {
            graphic.enabled = true;
        }

        Selectable[] selectables = item.GetComponentsInChildren<Selectable>(true);
        foreach (Selectable selectable in selectables)
        {
            selectable.enabled = true;
            selectable.interactable = true;
        }

        Button[] buttons = item.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            button.enabled = true;
            button.interactable = true;
        }

        LayoutElement[] layoutElements = item.GetComponentsInChildren<LayoutElement>(true);
        foreach (LayoutElement layoutElement in layoutElements)
        {
            layoutElement.enabled = true;
            layoutElement.ignoreLayout = false;
        }

        VerticalLayoutGroup[] verticalLayouts = item.GetComponentsInChildren<VerticalLayoutGroup>(true);
        foreach (VerticalLayoutGroup verticalLayout in verticalLayouts)
        {
            verticalLayout.enabled = true;
        }

        HorizontalLayoutGroup[] horizontalLayouts = item.GetComponentsInChildren<HorizontalLayoutGroup>(true);
        foreach (HorizontalLayoutGroup horizontalLayout in horizontalLayouts)
        {
            horizontalLayout.enabled = true;
        }

        ContentSizeFitter[] contentSizeFitters = item.GetComponentsInChildren<ContentSizeFitter>(true);
        foreach (ContentSizeFitter contentSizeFitter in contentSizeFitters)
        {
            contentSizeFitter.enabled = true;
        }
    }

    private void ClearList(Transform root)
    {
        if (root == null)
        {
            return;
        }

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Destroy(root.GetChild(i).gameObject);
        }
    }

    private void SetDetailText(string breadcrumb, string title, string description, string objective, string progress)
    {
        if (breadcrumbText != null)
        {
            breadcrumbText.text = breadcrumb;
        }

        if (lessonTitleText != null)
        {
            lessonTitleText.text = title;
        }

        if (lessonDescriptionText != null)
        {
            lessonDescriptionText.text = description;
        }

        if (lessonObjectiveText != null)
        {
            lessonObjectiveText.text = objective;
        }

        if (lessonProgressText != null)
        {
            lessonProgressText.text = progress;
        }
    }
}
