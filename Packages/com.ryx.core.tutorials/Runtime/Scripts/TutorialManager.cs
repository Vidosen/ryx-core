using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.SmartFormat.PersistentVariables;

namespace Ryx.Core.Tutorials
{

[System.Serializable]
public class PlatformSpecificTutorials
{
    [Tooltip("Tutorial chapters for desktop platforms (keyboard/mouse)")]
    public List<TutorialChapter> desktopTutorials = new List<TutorialChapter>();

    [Tooltip("Tutorial chapters for mobile platforms (touch controls)")]
    public List<TutorialChapter> mobileTutorials = new List<TutorialChapter>();
}

public class TutorialManager : MonoBehaviour
{
    [Header("Tutorial Configuration")]
    [Tooltip("The panel containing tutorial UI elements")]
    public GameObject tutorialPanel;

    [Tooltip("Text component displaying the current task")]
    public LocalizeStringEvent taskText;

    [Tooltip("Panel for displaying hints")]
    public GameObject hintPanel;

    [Tooltip("Text component displaying hints")]
    public LocalizeStringEvent hintText;

    [Tooltip("Line renderer for displaying path to objective")]
    public LineRenderer pathLine;

    [Tooltip("How high above ground the path should be")]
    public float pathHeight = 0.2f;

    [Header("Platform-Specific Tutorial Chapters")]
    [Tooltip("Platform-specific tutorial configurations")]
    public PlatformSpecificTutorials platformTutorials = new PlatformSpecificTutorials();

    [Tooltip("Force use of mobile tutorials regardless of platform (for testing)")]
    public bool forceMobileTutorials = false;

    [Tooltip("Force use of desktop tutorials regardless of platform (for testing)")]
    public bool forceDesktopTutorials = false;

    [Header("UI Configuration")]
    [Tooltip("Prefab for checkbox item in the task list")]
    public GameObject checkboxItemPrefab;

    [Tooltip("Parent transform for checkbox items")]
    public Transform checkboxContainer;

    [Tooltip("Start tutorial automatically on scene load")]
    public bool autoStartTutorial = true;

    [Tooltip("Persist tutorial progress via the configured progress store")]
    public bool persistProgress = true;

    [Header("Intro Pages")]
    public List<TutorialIntroPage> introPages = new();
    public TutorialIntroView introView;

    private int currentChapterIndex = -1;
    private readonly List<TaskCheckbox> currentCheckboxes = new List<TaskCheckbox>();
    private NavMeshPath navPath;
    private List<TutorialChapter> activeTutorialChapters;
    private bool isMobilePlatform;
    private GameObject uiHighlightObject;
    private TutorialTrigger currentStepTrigger;
    private TutorialTrigger armedChapterStartTrigger;
    private bool isWaitingForChapterStartTrigger;
    private int pendingChapterStartIndex = -1;

    // Competitive race state
    private List<int> _raceChapterIndices;
    private readonly List<(TutorialTrigger trigger, int chapterIndex, System.Action handler)> _raceArmedEntries = new();
    private bool _raceActive;
    private bool _raceArming;
    private int _raceWinnerIndex = -1;
    private ITutorialAnalyticsSink _analyticsSink = NullTutorialAnalyticsSink.Instance;
    private ITutorialProgressStore _progressStore = NullTutorialProgressStore.Instance;
    private int _currentIntroPageIndex;
    private bool _introCompleted;

    public event System.Action<int> IntroPageStarted;
    public event System.Action<int> IntroPageCompleted;
    public event System.Action<string, string> StepStarted;
    public event System.Action<string, string> StepCompleted;
    public event System.Action<string> ChapterCompleted;
    public event System.Action TutorialCompleted;

    public int CurrentChapterIndex => currentChapterIndex;

    public int CurrentStepIndex
    {
        get
        {
            if (activeTutorialChapters != null &&
                currentChapterIndex >= 0 && currentChapterIndex < activeTutorialChapters.Count)
                return activeTutorialChapters[currentChapterIndex].currentStepIndex;
            return -1;
        }
    }

    public TutorialChapter CurrentChapter
    {
        get
        {
            if (activeTutorialChapters != null &&
                currentChapterIndex >= 0 && currentChapterIndex < activeTutorialChapters.Count)
                return activeTutorialChapters[currentChapterIndex];
            return null;
        }
    }

    public TutorialStep CurrentStep
    {
        get
        {
            TutorialChapter chapter = CurrentChapter;
            if (chapter != null && chapter.currentStepIndex >= 0 && chapter.currentStepIndex < chapter.steps.Count)
                return chapter.steps[chapter.currentStepIndex];
            return null;
        }
    }

    public bool IsMobileTutorial => isMobilePlatform;

    public void Configure(
        ITutorialProgressStore progressStore = null,
        ITutorialAnalyticsSink analyticsSink = null)
    {
        _progressStore = progressStore ?? NullTutorialProgressStore.Instance;
        _analyticsSink = analyticsSink ?? NullTutorialAnalyticsSink.Instance;
    }

    private void Awake()
    {
        navPath = new NavMeshPath();
        DeterminePlatform();
        SetupTutorialsForPlatform();

        if (!tutorialPanel) Debug.LogWarning("Tutorial panel not assigned to TutorialManager!", gameObject);
        if (!taskText) Debug.LogWarning("Task text not assigned to TutorialManager!", gameObject);
        if (!hintPanel) Debug.LogWarning("Hint panel not assigned to TutorialManager!", gameObject);
        if (!hintText) Debug.LogWarning("Hint text not assigned to TutorialManager!", gameObject);
        if (!pathLine) Debug.LogWarning("Path line renderer not assigned to TutorialManager!", gameObject);
        if (!checkboxItemPrefab) Debug.LogWarning("Checkbox item prefab not assigned! Task checkboxes will not be displayed.", gameObject);
        if (!checkboxContainer) Debug.LogWarning("Checkbox container not assigned! Task checkboxes will not be displayed.", gameObject);

        LogConfigurationWarnings();
        HideAllTutorialElements();
        introView?.Bind(this);
    }

    public void DebugStep(string stepId)
    {
        Debug.Log($"[Tutorial Manager] Step completed: {stepId}");
    }

    private void Start()
    {
        if (autoStartTutorial)
            StartOrWaitForAutoStartTrigger();
    }

    public List<string> GetConfigurationWarnings()
    {
        var warnings = new List<string>();
        ValidateTutorialCollection(platformTutorials?.desktopTutorials, "desktop", warnings);
        ValidateTutorialCollection(platformTutorials?.mobileTutorials, "mobile", warnings);
        return warnings;
    }

    private void DeterminePlatform()
    {
        if (forceMobileTutorials)
        {
            isMobilePlatform = true;
            return;
        }

        if (forceDesktopTutorials)
        {
            isMobilePlatform = false;
            return;
        }

        isMobilePlatform = Application.isMobilePlatform || SystemInfo.deviceType == DeviceType.Handheld;
    }

    private void SetupTutorialsForPlatform()
    {
        var mobileTutorials = platformTutorials != null ? platformTutorials.mobileTutorials : null;
        var desktopTutorials = platformTutorials != null ? platformTutorials.desktopTutorials : null;

        if (isMobilePlatform)
        {
            activeTutorialChapters = mobileTutorials;
            Debug.Log("Using mobile tutorials");
        }
        else
        {
            activeTutorialChapters = desktopTutorials;
            Debug.Log("Using desktop tutorials");
        }

        if (isMobilePlatform && (activeTutorialChapters == null || activeTutorialChapters.Count == 0))
        {
            activeTutorialChapters = desktopTutorials;
            Debug.LogWarning("No mobile tutorials defined, falling back to desktop tutorials");
        }

        if (activeTutorialChapters == null)
        {
            activeTutorialChapters = new List<TutorialChapter>();
        }

        foreach (var chapter in activeTutorialChapters)
        {
            foreach (var step in chapter.steps)
            {
                if (step.stepObjectsToEnable == null)
                    continue;
                foreach (var obj in step.stepObjectsToEnable)
                {
                    if (obj)
                        obj.SetActive(false);
                }
            }
        }
    }

    private void HideAllTutorialElements()
    {
        if (tutorialPanel)
            tutorialPanel.SetActive(false);

        if (hintPanel)
            hintPanel.SetActive(false);

        if (pathLine)
            pathLine.enabled = false;

        ClearCheckboxes();
    }

    private void ClearCheckboxes()
    {
        foreach (var checkbox in currentCheckboxes)
        {
            if (checkbox)
                Destroy(checkbox.gameObject);
        }
        currentCheckboxes.Clear();
    }

    private void StartOrWaitForAutoStartTrigger() => StartTutorial();

    public void StartTutorial()
    {
        UnbindChapterStartTrigger();
        SetupTutorialsForPlatform();

        if (activeTutorialChapters == null || activeTutorialChapters.Count == 0)
        {
            Debug.LogWarning("No tutorials available for the current platform!");
            return;
        }

        var progress = persistProgress ? _progressStore.Load() : NullTutorialProgressStore.Instance.Load();

        if (persistProgress && progress.IsCompleted)
        {
            Debug.Log("Tutorial already completed, skipping.");
            return;
        }

        _introCompleted = persistProgress && progress.IsIntroCompleted;

        for (int i = 0; i < activeTutorialChapters.Count; i++)
            activeTutorialChapters[i].ResetProgress();

        CleanupCurrentStep();
        HideAllTutorialElements();

        if (TryShowIntro(progress.IntroPageIndex))
            return;

        if (TryResumeFromSaved(progress))
            return;

        currentChapterIndex = -1;

        if (tutorialPanel)
            tutorialPanel.SetActive(true);

        NextChapter();
    }

    public void ShowPreviousIntroPage()
    {
        if (_introCompleted || _currentIntroPageIndex <= 0)
            return;

        ShowIntroPage(_currentIntroPageIndex - 1);
    }

    public void ShowNextIntroPage()
    {
        if (_introCompleted)
            return;

        IntroPageCompleted?.Invoke(_currentIntroPageIndex);
        if (_currentIntroPageIndex + 1 < introPages.Count)
        {
            ShowIntroPage(_currentIntroPageIndex + 1);
            return;
        }

        _introCompleted = true;
        introView?.Hide();
        SaveProgress();
        NextChapter();
    }

    private bool TryShowIntro(int introPageIndex)
    {
        if (_introCompleted || introPages == null || introPages.Count == 0 || introView == null)
            return false;

        ShowIntroPage(Mathf.Clamp(introPageIndex, 0, introPages.Count - 1));
        return true;
    }

    private void ShowIntroPage(int pageIndex)
    {
        _currentIntroPageIndex = pageIndex;
        introView.Show(introPages[pageIndex], pageIndex > 0);
        IntroPageStarted?.Invoke(pageIndex);
        SaveProgress();
    }

    private bool TryResumeFromSaved(TutorialProgressData progress)
    {
        if (progress == null)
            return false;

        var savedChapterId = progress.ChapterId;
        var savedStepId = progress.StepId;
        if (string.IsNullOrWhiteSpace(savedChapterId))
            return false;

        if (!TryFindChapterIndex(savedChapterId, out var savedChapterIndex))
            return false;

        ApplyCompletedChapterIds(progress.CompletedChapterIds, savedChapterId);

        // If resuming into a competitive chapter, build the race group around it
        if (activeTutorialChapters[savedChapterIndex].competitiveStart)
        {
            var raceGroup = CollectRaceBlockAround(savedChapterIndex);

            // Mark all chapters before the race block as completed
            int blockStart = raceGroup[0];
            for (int i = 0; i < blockStart; i++)
                MarkChapterCompleted(activeTutorialChapters[i]);

            // Remember the race group so remaining chapters race after this one completes
            _raceChapterIndices = raceGroup;

            currentChapterIndex = savedChapterIndex;
            PrepareChapterResume(activeTutorialChapters[savedChapterIndex], savedStepId);

            if (tutorialPanel)
                tutorialPanel.SetActive(true);

            // Start directly — the chapter trigger already fired before this step was saved.
            NextStep();
            return true;
        }

        for (int i = 0; i < savedChapterIndex; i++)
            MarkChapterCompleted(activeTutorialChapters[i]);

        var savedStepFound = PrepareChapterResume(activeTutorialChapters[savedChapterIndex], savedStepId);

        if (tutorialPanel)
            tutorialPanel.SetActive(true);

        if (savedStepFound)
        {
            currentChapterIndex = savedChapterIndex;
            NextStep();
        }
        else
        {
            StartCurrentChapter(savedChapterIndex, activeTutorialChapters[savedChapterIndex]);
        }

        return true;
    }

    private void ApplyCompletedChapterIds(IReadOnlyCollection<string> completedChapterIds, string currentChapterId)
    {
        if (completedChapterIds == null)
            return;

        foreach (var completedChapterId in completedChapterIds)
        {
            if (string.IsNullOrWhiteSpace(completedChapterId) || string.Equals(completedChapterId, currentChapterId))
                continue;

            if (TryFindChapterIndex(completedChapterId, out var completedChapterIndex))
                MarkChapterCompleted(activeTutorialChapters[completedChapterIndex]);
        }
    }

    private static bool PrepareChapterResume(TutorialChapter chapter, string savedStepId)
    {
        if (!TryFindStepIndex(chapter, savedStepId, out var savedStepIndex))
        {
            chapter.currentStepIndex = -1;
            return false;
        }

        for (int i = 0; i < savedStepIndex; i++)
            chapter.steps[i].isCompleted = true;

        chapter.currentStepIndex = savedStepIndex - 1;
        return true;
    }

    private static bool TryFindStepIndex(TutorialChapter chapter, string stepId, out int stepIndex)
    {
        stepIndex = -1;
        if (chapter?.steps == null || string.IsNullOrWhiteSpace(stepId))
            return false;

        for (int i = 0; i < chapter.steps.Count; i++)
        {
            if (string.Equals(chapter.steps[i].stepId, stepId))
            {
                stepIndex = i;
                return true;
            }
        }

        return false;
    }

    private static void MarkChapterCompleted(TutorialChapter chapter)
    {
        foreach (var step in chapter.steps)
            step.isCompleted = true;
        chapter.isCompleted = true;
        chapter.currentStepIndex = chapter.steps.Count - 1;
    }

    private bool TryFindChapterIndex(string chapterId, out int chapterIndex)
    {
        chapterIndex = -1;

        if (activeTutorialChapters == null)
            return false;

        for (int i = 0; i < activeTutorialChapters.Count; i++)
        {
            if (activeTutorialChapters[i] != null &&
                string.Equals(activeTutorialChapters[i].chapterId, chapterId))
            {
                chapterIndex = i;
                return true;
            }
        }

        return false;
    }

    public void NextChapter()
    {
        UnbindChapterStartTrigger();
        CleanupCurrentStep();
        ClearCheckboxes();

        // If we just finished a competitive chapter, re-race the remaining ones
        if (_raceChapterIndices != null && _raceChapterIndices.Count > 0)
        {
            var remaining = _raceChapterIndices.FindAll(i => !activeTutorialChapters[i].isCompleted);
            if (remaining.Count > 0)
            {
                StartCompetitiveRace(remaining, false);
                return;
            }

            // All race chapters done — advance past the block
            currentChapterIndex = _raceChapterIndices[_raceChapterIndices.Count - 1];
            _raceChapterIndices = null;
        }

        var nextChapterIndex = currentChapterIndex + 1;

        if (nextChapterIndex >= activeTutorialChapters.Count)
        {
            CompleteTutorial();
            return;
        }

        TutorialChapter chapter = activeTutorialChapters[nextChapterIndex];

        // Check for competitive race group
        if (chapter.competitiveStart)
        {
            var raceIndices = CollectRaceGroup(nextChapterIndex);
            if (raceIndices.Count > 1)
            {
                StartCompetitiveRace(raceIndices);
                return;
            }
        }

        chapter.currentStepIndex = -1;
        StartCurrentChapter(nextChapterIndex, chapter);
    }

    public void NextStep()
    {
        if (currentChapterIndex < 0 || currentChapterIndex >= activeTutorialChapters.Count)
            return;

        TutorialChapter chapter = activeTutorialChapters[currentChapterIndex];

        CleanupCurrentStep();

        chapter.currentStepIndex++;

        if (chapter.currentStepIndex >= chapter.steps.Count)
        {
            chapter.isCompleted = true;
            NotifyChapterCompleted(chapter);
            NextChapter();
            return;
        }

        SetupCurrentStep();
        UpdateCheckboxes();
    }

    public void CompleteCurrentStep()
    {
        TutorialChapter chapter = CurrentChapter;
        if (chapter == null) return;

        TutorialStep step = CurrentStep;
        if (step == null) return;

        LogTutorialStepProgression(TutorialProgressionStatus.Complete, currentChapterIndex, chapter.chapterId, chapter.currentStepIndex, step.stepId);

        step.isCompleted = true;

        if (step.stepObjectsToEnable != null)
        {
            foreach (var obj in step.stepObjectsToEnable)
            {
                if (obj)
                    obj.SetActive(false);
            }
        }

        step.onStepCompleted?.Invoke(step.stepId);
        StepCompleted?.Invoke(chapter.chapterId, step.stepId);

        UpdateCheckboxes();

        if (chapter.AllStepsCompleted())
        {
            chapter.isCompleted = true;
            NotifyChapterCompleted(chapter);
            NextChapter();
        }
        else
        {
            NextStep();
        }
    }

    public void CompleteStepByIndex(int stepIndex)
    {
        TutorialChapter chapter = CurrentChapter;
        if (chapter == null) return;

        if (stepIndex < 0 || stepIndex >= chapter.steps.Count) return;

        TutorialStep step = chapter.steps[stepIndex];
        step.isCompleted = true;
        step.onStepCompleted?.Invoke(step.stepId);
        UpdateCheckboxes();

        if (chapter.AllStepsCompleted())
        {
            chapter.isCompleted = true;
            NotifyChapterCompleted(chapter);
            NextChapter();
        }
        else if (stepIndex == chapter.currentStepIndex)
        {
            NextStep();
        }
    }

    private void CleanupCurrentStep()
    {
        UnbindCurrentStepTrigger();

        TutorialStep step = CurrentStep;
        if (step == null) return;

        ApplyHighlights(step, false);

        if (hintPanel)
            hintPanel.SetActive(false);

        if (pathLine)
            pathLine.enabled = false;

        if (uiHighlightObject)
        {
            uiHighlightObject.SetActive(false);
            uiHighlightObject = null;
        }
    }

    private void SetupCurrentStep()
    {
        TutorialChapter chapter = CurrentChapter;
        if (chapter == null) return;

        TutorialStep step = CurrentStep;
        if (step == null) return;

        LogTutorialStepProgression(TutorialProgressionStatus.Start, currentChapterIndex, chapter.chapterId, chapter.currentStepIndex, step.stepId);
        StepStarted?.Invoke(chapter.chapterId, step.stepId);

        if (taskText)
        {
            taskText.StringReference = chapter.chapterName;
        }

        CreateCheckboxes();

        if (!step.hintText.IsEmpty && hintPanel && hintText)
        {
            hintPanel.SetActive(true);
            hintText.StringReference = step.hintText;
        }
        else if (hintPanel)
        {
            hintPanel.SetActive(false);
        }

        if (step.stepObjectsToEnable != null)
        {
            foreach (var obj in step.stepObjectsToEnable)
            {
                if (obj)
                    obj.SetActive(true);
            }
        }

        if (step.highlightUIElement)
        {
            uiHighlightObject = step.highlightUIElement.gameObject;
            uiHighlightObject.SetActive(true);
        }
        else if (uiHighlightObject)
        {
            uiHighlightObject.SetActive(false);
        }

        ApplyHighlights(step, true);

        if (step.targetDestination && pathLine)
        {
            StartCoroutine(UpdatePathRoutine(step.targetDestination));
        }

        SaveProgress();
        BindCurrentStepTrigger(step);
    }

    public void UpdateTutorialText(string key, int value)
    {
        if (CurrentStep != null && CurrentStep.description.TryGetValue(key, out var variableValue) &&
            variableValue is Variable<int> intVariable)
        {
            intVariable.Value = value;
        }
    }

    private void CreateCheckboxes()
    {
        if (!checkboxItemPrefab || !checkboxContainer) return;

        ClearCheckboxes();

        TutorialChapter chapter = CurrentChapter;
        if (chapter == null) return;

        for (int i = 0; i < chapter.steps.Count; i++)
        {
            TutorialStep step = chapter.steps[i];
            bool isCurrentStep = (i == chapter.currentStepIndex);

            if (step.isCompleted || isCurrentStep)
            {
                GameObject checkboxObj = Instantiate(checkboxItemPrefab, checkboxContainer);
                TaskCheckbox checkboxComponent = checkboxObj.GetComponent<TaskCheckbox>();
                currentCheckboxes.Add(checkboxComponent);

                if (checkboxComponent)
                {
                    checkboxComponent.stepIndex = i;
                    checkboxComponent.SetCompleted(step.isCompleted);
                    if (checkboxComponent.TextComponent)
                    {
                        var localize = checkboxComponent.TextComponent;
                        localize.StringReference = step.description;
                    }
                }

                checkboxObj.SetActive(i <= chapter.currentStepIndex || chapter.steps[i].isCompleted);
            }
        }
    }

    public void UpdateCheckboxes()
    {
        TutorialChapter chapter = CurrentChapter;
        if (chapter == null) return;

        foreach (TaskCheckbox checkboxComponent in currentCheckboxes)
        {
            if (checkboxComponent != null)
            {
                if (checkboxComponent.stepIndex >= 0 && checkboxComponent.stepIndex < chapter.steps.Count)
                {
                    checkboxComponent.SetCompleted(chapter.steps[checkboxComponent.stepIndex].isCompleted);
                }
            }
        }
    }

    private IEnumerator UpdatePathRoutine(Transform target)
    {
        if (!pathLine) yield break;

        pathLine.enabled = true;

        TutorialStep step = CurrentStep;
        while (step != null && step.targetDestination == target && !step.isCompleted)
        {
            UpdatePath(target);
            yield return new WaitForSeconds(0.5f);

            step = CurrentStep;
        }

        pathLine.enabled = false;
    }

    private void UpdatePath(Transform target)
    {
        if (!pathLine || !target) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (!player) return;

        Vector3 playerPos = player.transform.position;
        Vector3 targetPos = target.position;

        if (!NavMesh.SamplePosition(targetPos, out var hit, 10f, NavMesh.AllAreas))
        {
            pathLine.enabled = false;
            return;
        }
        targetPos = hit.position;

        if (NavMesh.CalculatePath(playerPos, targetPos, NavMesh.AllAreas, navPath))
        {
            if (navPath.corners.Length > 0)
            {
                if (!pathLine.enabled)
                    pathLine.enabled = true;
                pathLine.positionCount = navPath.corners.Length;

                for (int i = 0; i < navPath.corners.Length; i++)
                {
                    var point = navPath.corners[i];
                    point.y += pathHeight;
                    pathLine.SetPosition(i, point);
                }
            }
            else
            {
                pathLine.enabled = false;
            }
        }
        else
        {
            pathLine.enabled = false;
        }
    }

    private void CompleteTutorial()
    {
        CleanupRace();
        UnbindCurrentStepTrigger();
        UnbindChapterStartTrigger();
        _analyticsSink.LogEvent("Tutorial:Complete");
        HideAllTutorialElements();
        MarkTutorialDone();
        TutorialCompleted?.Invoke();
        Debug.Log("Tutorial completed!");
    }

    public void SkipTutorial()
    {
        CleanupRace();
        UnbindChapterStartTrigger();
        _analyticsSink.LogEvent("Tutorial:Skip");
        CleanupCurrentStep();
        HideAllTutorialElements();
        currentChapterIndex = activeTutorialChapters != null ? activeTutorialChapters.Count : 0;
        MarkTutorialDone();
        Debug.Log("Tutorial skipped.");
    }

    private void SaveProgress()
    {
        if (!persistProgress)
            return;

        var chapter = CurrentChapter;
        var step = CurrentStep;
        _progressStore.SaveProgress(chapter?.chapterId ?? string.Empty, step?.stepId ?? string.Empty, _introCompleted, _currentIntroPageIndex);
    }

    private void MarkTutorialDone()
    {
        if (!persistProgress) return;
        _progressStore.MarkCompleted();
    }

    public void ResetTutorialProgress()
    {
        _progressStore.Reset();
    }

    public void SwitchPlatform(bool useMobileTutorials)
    {
        var shouldResume = CurrentStep != null || CurrentChapter != null || isWaitingForChapterStartTrigger || _raceActive;

        CleanupRace();
        UnbindChapterStartTrigger();
        CleanupCurrentStep();
        HideAllTutorialElements();

        isMobilePlatform = useMobileTutorials;
        SetupTutorialsForPlatform();

        if (!shouldResume)
            return;

        StartTutorial();
    }

    private static void ApplyHighlights(TutorialStep step, bool enable)
    {
        if (step?.highlightGameObjects == null)
            return;

        var style = new TutorialHighlightStyle(step.outlineColor, step.outlineWidth);
        foreach (var highlightGameObject in step.highlightGameObjects)
        {
            if (!highlightGameObject)
                continue;

            var components = highlightGameObject.GetComponents<MonoBehaviour>();
            foreach (var component in components)
            {
                if (component is ITutorialHighlight highlighter)
                {
                    highlighter.SetHighlighted(enable, style);
                }
            }
        }
    }

    private void LogTutorialStepProgression(TutorialProgressionStatus status, int chapterIndex, string chapterId, int stepIndex, string stepId)
    {
        var chapter = Safe(chapterId, $"Chapter{chapterIndex}");
        var step = Safe(stepId, $"Step{stepIndex}");
        _analyticsSink.LogStep(status, chapter, step);
    }

    private void NotifyChapterCompleted(TutorialChapter chapter)
    {
        chapter.onChapterCompleted?.Invoke();
        _progressStore.MarkChapterCompleted(chapter.chapterId);
        ChapterCompleted?.Invoke(chapter.chapterId);
    }

    private static string Safe(string value, string fallback = "Unknown")
    {
        return string.IsNullOrEmpty(value) ? fallback : value;
    }

    private void BindCurrentStepTrigger(TutorialStep step)
    {
        UnbindCurrentStepTrigger();

        if (step?.completionTrigger == null)
            return;

        currentStepTrigger = step.completionTrigger;
        currentStepTrigger.Triggered += HandleCurrentStepTriggered;
        currentStepTrigger.Arm(this);
    }

    private void UnbindCurrentStepTrigger()
    {
        if (currentStepTrigger == null)
            return;

        currentStepTrigger.Triggered -= HandleCurrentStepTriggered;
        currentStepTrigger.Disarm();
        currentStepTrigger = null;
    }

    private void HandleCurrentStepTriggered()
    {
        CompleteCurrentStep();
    }

    private void StartCurrentChapter(int chapterIndex, TutorialChapter chapter)
    {
        if (chapter?.startTrigger)
        {
            currentChapterIndex = -1;
            BindChapterStartTrigger(chapter.startTrigger, chapterIndex);
            return;
        }

        currentChapterIndex = chapterIndex;
        NextStep();
    }

    private void BindChapterStartTrigger(TutorialTrigger trigger, int chapterIndex)
    {
        UnbindChapterStartTrigger();

        if (!trigger)
            return;

        armedChapterStartTrigger = trigger;
        isWaitingForChapterStartTrigger = true;
        pendingChapterStartIndex = chapterIndex;
        armedChapterStartTrigger.Triggered += HandleChapterStartTriggered;
        armedChapterStartTrigger.Arm(this);
    }

    private void UnbindChapterStartTrigger()
    {
        if (armedChapterStartTrigger == null)
        {
            isWaitingForChapterStartTrigger = false;
            pendingChapterStartIndex = -1;
            return;
        }

        armedChapterStartTrigger.Triggered -= HandleChapterStartTriggered;
        armedChapterStartTrigger.Disarm();
        armedChapterStartTrigger = null;
        isWaitingForChapterStartTrigger = false;
        pendingChapterStartIndex = -1;
    }

    private void HandleChapterStartTriggered()
    {
        var chapterIndex = pendingChapterStartIndex;
        UnbindChapterStartTrigger();
        if (chapterIndex < 0 || chapterIndex >= activeTutorialChapters.Count)
            return;

        currentChapterIndex = chapterIndex;
        NextStep();
    }

    // ── Competitive Race ────────────────────────────────────────────────

    private List<int> CollectRaceGroup(int startIndex)
    {
        var indices = new List<int>();
        for (int i = startIndex; i < activeTutorialChapters.Count; i++)
        {
            if (!activeTutorialChapters[i].competitiveStart)
                break;
            if (!activeTutorialChapters[i].isCompleted)
                indices.Add(i);
        }
        return indices;
    }

    private List<int> CollectRaceBlockAround(int memberIndex)
    {
        int start = memberIndex;
        while (start > 0 && activeTutorialChapters[start - 1].competitiveStart)
            start--;

        int end = memberIndex;
        while (end < activeTutorialChapters.Count - 1 && activeTutorialChapters[end + 1].competitiveStart)
            end++;

        var indices = new List<int>();
        for (int i = start; i <= end; i++)
            indices.Add(i);
        return indices;
    }

    private void StartCompetitiveRace(List<int> indices, bool updateRaceGroup = true)
    {
        CleanupRace();
        if (updateRaceGroup)
            _raceChapterIndices = indices;

        currentChapterIndex = -1;
        _raceActive = true;
        _raceArming = true;
        _raceWinnerIndex = -1;

        foreach (int idx in indices)
        {
            var chapter = activeTutorialChapters[idx];
            var trigger = chapter.startTrigger;

            if (!trigger)
            {
                // No start trigger means "ready immediately"
                _raceWinnerIndex = idx;
                break;
            }

            int capturedIdx = idx;
            System.Action handler = () => HandleRaceTriggerFired(capturedIdx);
            _raceArmedEntries.Add((trigger, capturedIdx, handler));
            trigger.Triggered += handler;
            trigger.Arm(this);

            // If a synchronous fire already chose a winner, stop arming
            if (_raceWinnerIndex >= 0)
                break;
        }

        _raceArming = false;

        if (_raceWinnerIndex >= 0)
            ExecuteRaceWinner(_raceWinnerIndex);
    }

    private void HandleRaceTriggerFired(int chapterIndex)
    {
        if (!_raceActive)
            return;

        if (_raceArming)
        {
            // Synchronous fire during Arm() — defer to after arming loop
            if (_raceWinnerIndex < 0)
                _raceWinnerIndex = chapterIndex;
            return;
        }

        ExecuteRaceWinner(chapterIndex);
    }

    private void ExecuteRaceWinner(int winnerIndex)
    {
        // Disarm all race triggers
        foreach (var (trigger, _, handler) in _raceArmedEntries)
        {
            if (trigger)
            {
                trigger.Triggered -= handler;
                trigger.Disarm();
            }
        }
        _raceArmedEntries.Clear();
        _raceActive = false;

        currentChapterIndex = winnerIndex;
        var chapter = activeTutorialChapters[winnerIndex];
        chapter.currentStepIndex = -1;

        if (tutorialPanel)
            tutorialPanel.SetActive(true);

        NextStep();
    }

    private void CleanupRace()
    {
        if (_raceArmedEntries.Count > 0)
        {
            foreach (var (trigger, _, handler) in _raceArmedEntries)
            {
                if (trigger)
                {
                    trigger.Triggered -= handler;
                    trigger.Disarm();
                }
            }
            _raceArmedEntries.Clear();
        }
        _raceActive = false;
        _raceArming = false;
        _raceWinnerIndex = -1;
    }

    // ── End Competitive Race ────────────────────────────────────────────

    private void LogConfigurationWarnings()
    {
        foreach (var warning in GetConfigurationWarnings())
        {
            Debug.LogWarning($"[Tutorial Manager] {warning}", gameObject);
        }
    }

    private static void ValidateTutorialCollection(List<TutorialChapter> chapters, string label, List<string> warnings)
    {
        if (chapters == null || chapters.Count == 0)
            return;

        var chapterIds = new HashSet<string>();
        var assignedTriggers = new HashSet<TutorialTrigger>();

        for (int chapterIndex = 0; chapterIndex < chapters.Count; chapterIndex++)
        {
            var chapter = chapters[chapterIndex];
            if (chapter == null)
            {
                warnings.Add($"{label} tutorial contains a null chapter at index {chapterIndex}.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(chapter.chapterId))
            {
                warnings.Add($"{label} tutorial chapter at index {chapterIndex} is missing chapterId.");
            }
            else if (!chapterIds.Add(chapter.chapterId))
            {
                warnings.Add($"{label} tutorial contains duplicate chapterId '{chapter.chapterId}'.");
            }

            if (chapter.startTrigger && !assignedTriggers.Add(chapter.startTrigger))
            {
                warnings.Add($"{label} tutorial reuses trigger '{chapter.startTrigger.name}' across chapter start and/or step completion bindings.");
            }

            var stepIds = new HashSet<string>();
            for (int stepIndex = 0; stepIndex < chapter.steps.Count; stepIndex++)
            {
                var step = chapter.steps[stepIndex];
                if (step == null)
                {
                    warnings.Add($"{label} tutorial chapter '{Safe(chapter.chapterId, chapterIndex.ToString())}' contains a null step at index {stepIndex}.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(step.stepId))
                {
                    warnings.Add($"{label} tutorial step at {Safe(chapter.chapterId, chapterIndex.ToString())}[{stepIndex}] is missing stepId.");
                }
                else if (!stepIds.Add(step.stepId))
                {
                    warnings.Add($"{label} tutorial chapter '{chapter.chapterId}' contains duplicate stepId '{step.stepId}'.");
                }

                if (step.completionTrigger && !assignedTriggers.Add(step.completionTrigger))
                {
                    warnings.Add($"{label} tutorial reuses trigger '{step.completionTrigger.name}' across multiple steps.");
                }
            }
        }
    }

}

}
