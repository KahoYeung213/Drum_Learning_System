using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CourseScaffoldingUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CourseLibrary courseLibrary;
    [SerializeField] private LessonContentUI lessonContentUI;

    [Header("Course Selection Buttons")]
    [SerializeField] private Button beginnerCourseButton;
    [SerializeField] private Button intermediateCourseButton;
    [SerializeField] private Button advancedCourseButton;
    [SerializeField] private string beginnerDifficultyLabel = "Beginner";
    [SerializeField] private string intermediateDifficultyLabel = "Intermediate";
    [SerializeField] private string advancedDifficultyLabel = "Advanced";

    [Header("Scaffolding Lists")]
    [SerializeField] private Transform moduleListRoot;
    [SerializeField] private GameObject listItemButtonPrefab;
    [SerializeField] private GameObject moduleItemButtonPrefab;
    [SerializeField] private GameObject lessonItemButtonPrefab;

    [Header("Module Accordion (Sidebar)")]
    [SerializeField] private bool useModuleAccordionSidebar = true;
    [SerializeField] private bool allowCollapseExpandedModule = true;
    [SerializeField] private bool autoSelectLessonWhenModuleExpanded = true;
    [SerializeField] private string moduleHeaderButtonPath = "Header";
    [SerializeField] private string moduleTitleTextPath = "Header/Title";
    [SerializeField] private string moduleProgressBarPath = "Header/RawImage";
    [SerializeField] private string moduleProgressTextPath = "Header/RawImage/Progress text";
    [SerializeField] private string moduleLessonsContainerPath = "LessonsContainer";
    [SerializeField] private string lessonItemTextPath = "Title";
    [SerializeField] private string lessonStatusTextPath = "Status";

    private DrumCourseData selectedCourse;
    private CourseModuleData selectedModule;
    private CourseLessonData selectedLesson;
    private int selectedModuleIndex = -1;
    private int selectedLessonIndex = -1;
    private int expandedModuleIndex = -1;
    private GameObject moduleTemplateInstance;
    private CourseProgressManager progressManager;
    private readonly Dictionary<int, float> progressBarBaseWidths = new Dictionary<int, float>();

    private void Start()
    {
        ResolveReferences();
        CacheModuleTemplate();
        BindCourseButtons();

        if (lessonContentUI != null)
        {
            lessonContentUI.OnCompleteLessonRequested += HandleLessonCompleted;
        }

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

        if (lessonContentUI != null)
        {
            lessonContentUI.OnCompleteLessonRequested -= HandleLessonCompleted;
        }

        UnbindCourseButtons();
    }

    private void BindCourseButtons()
    {
        if (beginnerCourseButton != null)
        {
            beginnerCourseButton.onClick.RemoveListener(OnBeginnerCourseClicked);
            beginnerCourseButton.onClick.AddListener(OnBeginnerCourseClicked);
        }

        if (intermediateCourseButton != null)
        {
            intermediateCourseButton.onClick.RemoveListener(OnIntermediateCourseClicked);
            intermediateCourseButton.onClick.AddListener(OnIntermediateCourseClicked);
        }

        if (advancedCourseButton != null)
        {
            advancedCourseButton.onClick.RemoveListener(OnAdvancedCourseClicked);
            advancedCourseButton.onClick.AddListener(OnAdvancedCourseClicked);
        }
    }

    private void UnbindCourseButtons()
    {
        if (beginnerCourseButton != null)
        {
            beginnerCourseButton.onClick.RemoveListener(OnBeginnerCourseClicked);
        }

        if (intermediateCourseButton != null)
        {
            intermediateCourseButton.onClick.RemoveListener(OnIntermediateCourseClicked);
        }

        if (advancedCourseButton != null)
        {
            advancedCourseButton.onClick.RemoveListener(OnAdvancedCourseClicked);
        }
    }

    private void OnBeginnerCourseClicked()
    {
        SelectCourseByDifficulty(beginnerDifficultyLabel);
    }

    private void OnIntermediateCourseClicked()
    {
        SelectCourseByDifficulty(intermediateDifficultyLabel);
    }

    private void OnAdvancedCourseClicked()
    {
        SelectCourseByDifficulty(advancedDifficultyLabel);
    }

    private void SelectCourseByDifficulty(string difficultyLabel)
    {
        if (courseLibrary == null || string.IsNullOrEmpty(difficultyLabel))
        {
            return;
        }

        IReadOnlyList<DrumCourseData> allCourses = courseLibrary.Courses;
        if (allCourses == null || allCourses.Count == 0)
        {
            return;
        }

        for (int i = 0; i < allCourses.Count; i++)
        {
            DrumCourseData course = allCourses[i];
            if (course != null && string.Equals(course.difficulty, difficultyLabel, StringComparison.OrdinalIgnoreCase))
            {
                SelectCourse(course);
                return;
            }
        }

        Debug.LogWarning($"[CourseScaffoldingUI] No course found for difficulty '{difficultyLabel}'.");
    }

    private void ResolveReferences()
    {
        if (courseLibrary == null)
        {
            courseLibrary = FindFirstObjectByType<CourseLibrary>();
        }

        if (lessonContentUI == null)
        {
            lessonContentUI = FindFirstObjectByType<LessonContentUI>();
        }

        if (progressManager == null)
        {
            progressManager = FindFirstObjectByType<CourseProgressManager>();
        }
    }

    private void HandleCoursesLoaded(List<DrumCourseData> courses)
    {
        if (courses == null || courses.Count == 0)
        {
            if (lessonContentUI != null)
            {
                lessonContentUI.Clear();
            }
            return;
        }

        SelectCourse(courses[0]);
    }

    private void SelectCourse(DrumCourseData course)
    {
        selectedCourse = course;
        selectedModule = null;
        selectedLesson = null;
        selectedModuleIndex = -1;
        selectedLessonIndex = -1;
        expandedModuleIndex = -1;

        PopulateModules();

        if (selectedCourse != null && selectedCourse.modules != null && selectedCourse.modules.Count > 0)
        {
            SelectFirstLessonInModule(0);
        }
        else if (lessonContentUI != null)
        {
            lessonContentUI.Clear();
        }
    }

    private void PopulateModules()
    {
        CacheModuleTemplate();
        ClearList(moduleListRoot, moduleTemplateInstance);

        if (moduleTemplateInstance != null)
        {
            moduleTemplateInstance.SetActive(false);
        }

        if (selectedCourse == null || selectedCourse.modules == null)
        {
            return;
        }

        for (int i = 0; i < selectedCourse.modules.Count; i++)
        {
            CourseModuleData module = selectedCourse.modules[i];
            int capturedIndex = i;

            GameObject moduleItem = CreateListItem(moduleListRoot, GetPrefabForList(ListType.Module), module.title, false, () => ToggleModuleAccordion(capturedIndex), moduleTitleTextPath);
            if (moduleItem == null)
            {
                continue;
            }

            ConfigureModuleAccordionItem(moduleItem, module, capturedIndex);
        }
    }

    private void ConfigureModuleAccordionItem(GameObject moduleItem, CourseModuleData module, int moduleIndex)
    {
        if (moduleItem == null || module == null)
        {
            return;
        }

        UpdateModuleProgressUI(moduleItem, module);

        Button headerButton = null;
        Transform headerTransform = FindByPathOrSelf(moduleItem.transform, moduleHeaderButtonPath);
        if (headerTransform != null)
        {
            headerButton = headerTransform.GetComponent<Button>();
            if (headerButton == null)
            {
                headerButton = headerTransform.GetComponentInChildren<Button>(true);
            }
        }

        if (headerButton != null)
        {
            headerButton.onClick.RemoveAllListeners();
            headerButton.onClick.AddListener(() => ToggleModuleAccordion(moduleIndex));
        }

        if (!useModuleAccordionSidebar)
        {
            return;
        }

        Transform lessonsContainer = FindByPathStrict(moduleItem.transform, moduleLessonsContainerPath);
        if (lessonsContainer == null)
        {
            return;
        }

        bool expanded = moduleIndex == expandedModuleIndex;
        lessonsContainer.gameObject.SetActive(expanded);
        if (!expanded)
        {
            return;
        }

        PopulateLessonsContainer(lessonsContainer, module, moduleIndex);
    }

    private void ToggleModuleAccordion(int moduleIndex)
    {
        if (selectedCourse == null || selectedCourse.modules == null || moduleIndex < 0 || moduleIndex >= selectedCourse.modules.Count)
        {
            return;
        }

        bool isSameExpanded = expandedModuleIndex == moduleIndex;
        if (isSameExpanded && allowCollapseExpandedModule)
        {
            expandedModuleIndex = -1;
            RefreshModuleAccordionVisibility();
            return;
        }

        expandedModuleIndex = moduleIndex;
        RefreshModuleAccordionVisibility();

        if (autoSelectLessonWhenModuleExpanded)
        {
            SelectFirstLessonInModule(moduleIndex);
        }
    }

    private void RefreshModuleAccordionVisibility()
    {
        if (selectedCourse == null || selectedCourse.modules == null || moduleListRoot == null)
        {
            return;
        }

        int childOffset = GetModuleChildOffset();
        int moduleCount = Mathf.Min(moduleListRoot.childCount - childOffset, selectedCourse.modules.Count);
        for (int moduleIndex = 0; moduleIndex < moduleCount; moduleIndex++)
        {
            Transform moduleItem = moduleListRoot.GetChild(moduleIndex + childOffset);
            Transform lessonsContainer = FindByPathStrict(moduleItem, moduleLessonsContainerPath);
            if (lessonsContainer == null)
            {
                continue;
            }

            bool expanded = moduleIndex == expandedModuleIndex;
            lessonsContainer.gameObject.SetActive(expanded);

            if (expanded)
            {
                PopulateLessonsContainer(lessonsContainer, selectedCourse.modules[moduleIndex], moduleIndex);
            }
        }
    }

    private void PopulateLessonsContainer(Transform lessonsContainer, CourseModuleData module, int moduleIndex)
    {
        if (lessonsContainer == null || module == null)
        {
            return;
        }

        GameObject lessonTemplate = GetLessonTemplate(lessonsContainer);
        ClearList(lessonsContainer, lessonTemplate);

        if (lessonTemplate != null)
        {
            lessonTemplate.SetActive(false);
        }

        if (module.lessons == null)
        {
            return;
        }

        for (int lessonIndex = 0; lessonIndex < module.lessons.Count; lessonIndex++)
        {
            CourseLessonData lesson = module.lessons[lessonIndex];
            int capturedLessonIndex = lessonIndex;

            GameObject lessonItem = CreateListItem(
                lessonsContainer,
                lessonTemplate != null ? lessonTemplate : GetPrefabForList(ListType.Lesson),
                lesson.title,
                false,
                () => SelectLesson(moduleIndex, capturedLessonIndex),
                lessonItemTextPath,
                true);

            if (lessonItem != null)
            {
                UpdateLessonStatusUI(lessonItem, module, lesson, capturedLessonIndex);
            }
        }

        if (lessonTemplate != null)
        {
            lessonTemplate.SetActive(false);
        }
    }

    private void CacheModuleTemplate()
    {
        if (moduleTemplateInstance != null || moduleListRoot == null || moduleListRoot.childCount == 0)
        {
            return;
        }

        moduleTemplateInstance = moduleListRoot.GetChild(0).gameObject;
    }

    private int GetModuleChildOffset()
    {
        return moduleTemplateInstance != null && moduleListRoot != null && moduleListRoot.childCount > 0 && moduleListRoot.GetChild(0).gameObject == moduleTemplateInstance ? 1 : 0;
    }

    private GameObject GetLessonTemplate(Transform lessonsContainer)
    {
        if (lessonItemButtonPrefab != null)
        {
            return lessonItemButtonPrefab;
        }

        if (lessonsContainer != null && lessonsContainer.childCount > 0)
        {
            return lessonsContainer.GetChild(0).gameObject;
        }

        return null;
    }

    private void SelectFirstLessonInModule(int moduleIndex)
    {
        if (selectedCourse == null || selectedCourse.modules == null || moduleIndex < 0 || moduleIndex >= selectedCourse.modules.Count)
        {
            return;
        }

        CourseModuleData module = selectedCourse.modules[moduleIndex];
        if (module.lessons == null || module.lessons.Count == 0)
        {
            if (lessonContentUI != null)
            {
                lessonContentUI.Clear();
            }
            return;
        }

        SelectLesson(moduleIndex, 0);
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

        if (lessonContentUI != null)
        {
            lessonContentUI.DisplayLesson(selectedLesson, selectedModule, selectedCourse);
        }

        RefreshVisibleModuleProgress();
    }

    private void HandleLessonCompleted(string courseId, string moduleId, string lessonId, string exerciseId)
    {
        RefreshVisibleModuleProgress();
    }

    private void RefreshVisibleModuleProgress()
    {
        if (selectedCourse == null || selectedCourse.modules == null || moduleListRoot == null)
        {
            return;
        }

        int childOffset = GetModuleChildOffset();
        int moduleCount = Mathf.Min(moduleListRoot.childCount - childOffset, selectedCourse.modules.Count);
        for (int moduleIndex = 0; moduleIndex < moduleCount; moduleIndex++)
        {
            Transform moduleItem = moduleListRoot.GetChild(moduleIndex + childOffset);
            UpdateModuleProgressUI(moduleItem.gameObject, selectedCourse.modules[moduleIndex]);
            RefreshLessonStatusesForModule(moduleItem.gameObject, selectedCourse.modules[moduleIndex], moduleIndex);
        }
    }

    private void RefreshLessonStatusesForModule(GameObject moduleItem, CourseModuleData module, int moduleIndex)
    {
        if (moduleItem == null || module == null || module.lessons == null)
        {
            return;
        }

        Transform lessonsContainer = FindByPathStrict(moduleItem.transform, moduleLessonsContainerPath);
        if (lessonsContainer == null)
        {
            return;
        }

        int lessonCount = Mathf.Min(lessonsContainer.childCount, module.lessons.Count);
        for (int lessonIndex = 0; lessonIndex < lessonCount; lessonIndex++)
        {
            UpdateLessonStatusUI(lessonsContainer.GetChild(lessonIndex).gameObject, module, module.lessons[lessonIndex], lessonIndex);
        }
    }

    private void UpdateLessonStatusUI(GameObject lessonItem, CourseModuleData module, CourseLessonData lesson, int lessonIndex)
    {
        if (lessonItem == null || selectedCourse == null || module == null || lesson == null)
        {
            return;
        }

        bool completed = progressManager != null && progressManager.IsLessonCompleted(selectedCourse, module, lesson);
        bool unlocked = progressManager == null || progressManager.IsLessonUnlocked(selectedCourse, FindModuleIndex(module), lessonIndex);
        bool locked = !unlocked;

        string statusLabel = completed ? "Completed" : (unlocked ? "Incomplete" : "Locked");
        Color statusColor = completed ? new Color(0.45f, 0.9f, 0.55f) : (unlocked ? new Color(0.95f, 0.82f, 0.35f) : new Color(0.75f, 0.75f, 0.75f));

        Button button = lessonItem.GetComponent<Button>();
        if (button == null)
        {
            button = lessonItem.GetComponentInChildren<Button>(true);
        }

        if (button != null)
        {
            button.interactable = !locked;
        }

        CanvasGroup canvasGroup = lessonItem.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = lessonItem.GetComponentInChildren<CanvasGroup>(true);
        }

        if (canvasGroup != null)
        {
            canvasGroup.interactable = !locked;
            canvasGroup.blocksRaycasts = !locked;
        }

        Transform statusTransform = FindByPathOrSelf(lessonItem.transform, lessonStatusTextPath);
        if (statusTransform == null)
        {
            statusTransform = FindByPathOrSelf(lessonItem.transform, "Status");
        }

        if (statusTransform != null)
        {
            TMP_Text tmp = statusTransform.GetComponent<TMP_Text>();
            if (tmp != null)
            {
                tmp.text = statusLabel;
                tmp.color = statusColor;
                return;
            }

            Text uiText = statusTransform.GetComponent<Text>();
            if (uiText != null)
            {
                uiText.text = statusLabel;
                uiText.color = statusColor;
                return;
            }
        }

        SetItemTextByPath(lessonItem.transform, lessonItemTextPath, $"{lesson.title}  [{statusLabel}]");
    }

    private int FindModuleIndex(CourseModuleData module)
    {
        if (selectedCourse == null || selectedCourse.modules == null || module == null)
        {
            return -1;
        }

        for (int i = 0; i < selectedCourse.modules.Count; i++)
        {
            if (selectedCourse.modules[i] == module)
            {
                return i;
            }
        }

        return -1;
    }

    private void UpdateModuleProgressUI(GameObject moduleItem, CourseModuleData module)
    {
        if (moduleItem == null || module == null)
        {
            return;
        }

        int totalLessons = module.lessons != null ? module.lessons.Count : 0;
        int completedLessons = 0;

        if (progressManager != null && selectedCourse != null && module.lessons != null)
        {
            for (int i = 0; i < module.lessons.Count; i++)
            {
                CourseLessonData lesson = module.lessons[i];
                if (lesson != null && progressManager.IsLessonCompleted(selectedCourse, module, lesson))
                {
                    completedLessons++;
                }
            }
        }

        float progress01 = totalLessons > 0 ? (float)completedLessons / totalLessons : 0f;

        Transform progressBarTransform = FindByPathOrSelf(moduleItem.transform, moduleProgressBarPath);
        if (progressBarTransform == null)
        {
            progressBarTransform = FindByPathOrSelf(moduleItem.transform, "header/RawImage");
        }

        RawImage progressBar = progressBarTransform != null ? progressBarTransform.GetComponent<RawImage>() : null;
        if (progressBar != null)
        {
            RectTransform barRect = progressBar.rectTransform;
            int key = barRect.GetInstanceID();
            if (!progressBarBaseWidths.ContainsKey(key))
            {
                progressBarBaseWidths[key] = Mathf.Max(0f, barRect.sizeDelta.x);
            }

            float baseWidth = progressBarBaseWidths[key];
            barRect.sizeDelta = new Vector2(baseWidth * progress01, barRect.sizeDelta.y);
        }

        Transform progressTextTransform = FindByPathOrSelf(moduleItem.transform, moduleProgressTextPath);
        if (progressTextTransform == null)
        {
            progressTextTransform = FindByPathOrSelf(moduleItem.transform, "header/RawImage/Progress text");
        }

        string progressLabel = $"{completedLessons}/{totalLessons}";
        if (progressTextTransform != null)
        {
            RectTransform textRect = progressTextTransform as RectTransform;
            if (textRect != null)
            {
                textRect.anchorMin = new Vector2(1f, textRect.anchorMin.y);
                textRect.anchorMax = new Vector2(1f, textRect.anchorMax.y);
                textRect.pivot = new Vector2(1f, textRect.pivot.y);
                textRect.anchoredPosition = new Vector2(-8f, textRect.anchoredPosition.y);
            }

            TMP_Text tmp = progressTextTransform.GetComponent<TMP_Text>();
            if (tmp != null)
            {
                tmp.text = progressLabel;
            }
            else
            {
                Text uiText = progressTextTransform.GetComponent<Text>();
                if (uiText != null)
                {
                    uiText.text = progressLabel;
                }
            }
        }
    }

    private enum ListType
    {
        Module,
        Lesson
    }

    private GameObject GetPrefabForList(ListType listType)
    {
        GameObject typedPrefab = listType switch
        {
            ListType.Module => moduleItemButtonPrefab,
            ListType.Lesson => lessonItemButtonPrefab,
            _ => null
        };

        return typedPrefab != null ? typedPrefab : listItemButtonPrefab;
    }

    private GameObject CreateListItem(Transform root, GameObject prefab, string title, bool disabled, Action onClick, string preferredTextPath = "", bool normalizeUITransform = false)
    {
        if (root == null || prefab == null)
        {
            return null;
        }

        GameObject item = Instantiate(prefab, root);
        item.SetActive(true);
        if (normalizeUITransform)
        {
            RectTransform itemRect = item.transform as RectTransform;
            if (itemRect != null)
            {
                itemRect.localScale = Vector3.one;
                itemRect.localRotation = Quaternion.identity;
                itemRect.anchoredPosition3D = Vector3.zero;
            }
            else
            {
                item.transform.localScale = Vector3.one;
                item.transform.localRotation = Quaternion.identity;
                item.transform.localPosition = Vector3.zero;
            }
        }
        EnsureCloneUIComponentsEnabled(item);

        Button button = item.GetComponent<Button>();
        if (button == null)
        {
            button = item.GetComponentInChildren<Button>(true);
        }

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

    private void EnsureCloneUIComponentsEnabled(GameObject item)
    {
        if (item == null)
        {
            return;
        }

        Behaviour[] behaviours = item.GetComponentsInChildren<Behaviour>(true);
        foreach (Behaviour behaviour in behaviours)
        {
            if (behaviour != null && !behaviour.enabled)
            {
                behaviour.enabled = true;
            }
        }

        Selectable[] selectables = item.GetComponentsInChildren<Selectable>(true);
        foreach (Selectable selectable in selectables)
        {
            if (selectable != null)
            {
                selectable.interactable = true;
            }
        }

        CanvasGroup[] canvasGroups = item.GetComponentsInChildren<CanvasGroup>(true);
        foreach (CanvasGroup canvasGroup in canvasGroups)
        {
            if (canvasGroup != null)
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
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
            TMP_Text fallbackTmp = root.GetComponentInChildren<TMP_Text>(true);
            if (fallbackTmp != null)
            {
                fallbackTmp.text = value;
                return;
            }

            Text fallbackText = root.GetComponentInChildren<Text>(true);
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

        return root.Find(path);
    }

    private Transform FindByPathStrict(Transform root, string path)
    {
        if (root == null || string.IsNullOrEmpty(path))
        {
            return null;
        }

        return root.Find(path);
    }

    private void ClearList(Transform root, GameObject preserveItem = null)
    {
        if (root == null)
        {
            return;
        }

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            GameObject child = root.GetChild(i).gameObject;
            if (preserveItem != null && child == preserveItem)
            {
                continue;
            }

            Destroy(child);
        }

        if (preserveItem != null)
        {
            preserveItem.transform.SetAsFirstSibling();
        }
    }
}
