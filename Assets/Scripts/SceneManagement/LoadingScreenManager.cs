using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using FirstPersonSystem.ProceduralGeneration;

namespace FirstPersonSystem.SceneManagement
{
    /// <summary>
    /// Manages displaying a full-screen black empty loading screen across scene transitions
    /// and ensures the procedural mountain generator completes terrain generation before
    /// dismissing the loading screen.
    /// </summary>
    public class LoadingScreenManager : MonoBehaviour
    {
        private static LoadingScreenManager instance;

        [Header("Overlay Settings")]
        [SerializeField] private float defaultFadeDuration = 0.35f;

        private Canvas overlayCanvas;
        private CanvasGroup overlayCanvasGroup;
        private Coroutine activeLoadCoroutine;
        private Coroutine activeFadeCoroutine;

        public static LoadingScreenManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<LoadingScreenManager>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("[LoadingScreenManager]");
                        instance = go.AddComponent<LoadingScreenManager>();
                    }
                }
                return instance;
            }
        }

        public bool IsShowing => overlayCanvas != null && overlayCanvas.enabled && overlayCanvasGroup != null && overlayCanvasGroup.alpha > 0f;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureOverlayCreated();
        }

        /// <summary>
        /// Creates the programmatic fullscreen black empty overlay canvas if not already present.
        /// </summary>
        private void EnsureOverlayCreated()
        {
            if (overlayCanvas != null) return;

            GameObject canvasGO = new GameObject("LoadingScreenCanvas");
            canvasGO.transform.SetParent(transform, false);

            overlayCanvas = canvasGO.AddComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = 32767; // Highest possible sorting order to overlay all UI and rendering
            overlayCanvas.pixelPerfect = false;

            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            overlayCanvasGroup = canvasGO.AddComponent<CanvasGroup>();
            overlayCanvasGroup.alpha = 0f;
            overlayCanvasGroup.blocksRaycasts = false;
            overlayCanvasGroup.interactable = false;

            // Full-screen solid black empty image
            GameObject blackPanel = new GameObject("BlackEmptyScreen");
            blackPanel.transform.SetParent(canvasGO.transform, false);

            Image img = blackPanel.AddComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = true;

            RectTransform rt = blackPanel.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);

            overlayCanvas.enabled = false;
        }

        /// <summary>
        /// Displays the black empty loading screen immediately.
        /// </summary>
        public static void Show()
        {
            Instance.InternalShow();
        }

        /// <summary>
        /// Dismisses the loading screen, optionally fading it out.
        /// </summary>
        public static void Hide(float fadeDuration = -1f)
        {
            if (instance != null)
            {
                instance.InternalHide(fadeDuration >= 0f ? fadeDuration : instance.defaultFadeDuration);
            }
        }

        /// <summary>
        /// Smoothly fades the screen to solid black over the specified duration.
        /// </summary>
        public static void FadeToBlack(float duration = 1.5f)
        {
            Instance.InternalFadeToBlack(duration);
        }

        /// <summary>
        /// Loads the next scene in the build settings while displaying the black loading screen
        /// until procedural terrain generation is fully completed.
        /// </summary>
        public static void LoadNextScene()
        {
            int currentBuildIndex = SceneManager.GetActiveScene().buildIndex;
            int nextBuildIndex = currentBuildIndex + 1;

            if (nextBuildIndex >= SceneManager.sceneCountInBuildSettings)
            {
                // Fallback to scene 1 (TerrainTest) if out of range
                nextBuildIndex = 1;
            }

            Instance.InternalLoadScene(nextBuildIndex, null);
        }

        /// <summary>
        /// Loads a specific scene by build index while displaying the black loading screen
        /// until procedural terrain generation completes.
        /// </summary>
        public static void LoadScene(int buildIndex)
        {
            Instance.InternalLoadScene(buildIndex, null);
        }

        /// <summary>
        /// Loads a specific scene by name while displaying the black loading screen
        /// until procedural terrain generation completes.
        /// </summary>
        public static void LoadScene(string sceneName)
        {
            Instance.InternalLoadScene(-1, sceneName);
        }

        private void InternalShow()
        {
            EnsureOverlayCreated();

            if (activeFadeCoroutine != null)
            {
                StopCoroutine(activeFadeCoroutine);
                activeFadeCoroutine = null;
            }

            overlayCanvas.enabled = true;
            overlayCanvasGroup.alpha = 1f;
            overlayCanvasGroup.blocksRaycasts = true;
            overlayCanvasGroup.interactable = true;
        }

        private void InternalHide(float fadeDuration)
        {
            if (overlayCanvas == null || !overlayCanvas.enabled) return;

            if (activeFadeCoroutine != null)
            {
                StopCoroutine(activeFadeCoroutine);
            }

            if (fadeDuration <= 0f)
            {
                overlayCanvasGroup.alpha = 0f;
                overlayCanvasGroup.blocksRaycasts = false;
                overlayCanvasGroup.interactable = false;
                overlayCanvas.enabled = false;
            }
            else
            {
                activeFadeCoroutine = StartCoroutine(FadeOutRoutine(fadeDuration));
            }
        }

        private IEnumerator FadeOutRoutine(float duration)
        {
            overlayCanvasGroup.blocksRaycasts = false;
            overlayCanvasGroup.interactable = false;

            float elapsed = 0f;
            float startAlpha = overlayCanvasGroup.alpha;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                overlayCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / duration);
                yield return null;
            }

            overlayCanvasGroup.alpha = 0f;
            overlayCanvas.enabled = false;
            activeFadeCoroutine = null;
        }

        private void InternalFadeToBlack(float duration)
        {
            EnsureOverlayCreated();

            if (activeFadeCoroutine != null)
            {
                StopCoroutine(activeFadeCoroutine);
            }

            overlayCanvas.enabled = true;
            overlayCanvasGroup.blocksRaycasts = true;
            overlayCanvasGroup.interactable = true;

            if (duration <= 0f)
            {
                overlayCanvasGroup.alpha = 1f;
            }
            else
            {
                activeFadeCoroutine = StartCoroutine(FadeInRoutine(duration));
            }
        }

        private IEnumerator FadeInRoutine(float duration)
        {
            float elapsed = 0f;
            float startAlpha = overlayCanvasGroup.alpha;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                overlayCanvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, elapsed / duration);
                yield return null;
            }

            overlayCanvasGroup.alpha = 1f;
            activeFadeCoroutine = null;
        }

        private void InternalLoadScene(int buildIndex, string sceneName)
        {
            if (activeLoadCoroutine != null)
            {
                StopCoroutine(activeLoadCoroutine);
            }

            activeLoadCoroutine = StartCoroutine(LoadSceneAndAwaitGenerationRoutine(buildIndex, sceneName));
        }

        private IEnumerator LoadSceneAndAwaitGenerationRoutine(int buildIndex, string sceneName)
        {
            // 1. Show black empty loading screen immediately
            InternalShow();

            // 2. Wait for black screen to render at least one full frame so no transition stutter is visible
            yield return null;
            yield return new WaitForEndOfFrame();

            // 3. Load target scene asynchronously
            AsyncOperation asyncOp = buildIndex >= 0
                ? SceneManager.LoadSceneAsync(buildIndex)
                : SceneManager.LoadSceneAsync(sceneName);

            if (asyncOp == null)
            {
                // Fallback attempt with "TerrainTest" if buildIndex failed
                asyncOp = SceneManager.LoadSceneAsync("TerrainTest");
            }

            if (asyncOp != null)
            {
                while (!asyncOp.isDone)
                {
                    yield return null;
                }
            }

            // 4. Scene is loaded! Check if target is a menu/start scene
            Scene currentScene = SceneManager.GetActiveScene();
            bool isMenuScene = currentScene.buildIndex == 0 ||
                               currentScene.name.Equals("Start", StringComparison.OrdinalIgnoreCase) ||
                               (sceneName != null && sceneName.Equals("Start", StringComparison.OrdinalIgnoreCase)) ||
                               (buildIndex == 0);

            if (!isMenuScene)
            {
                ProceduralMountainGenerator generator = null;
                float searchTimeout = 2.0f;
                float elapsedSearch = 0f;

                while (generator == null && elapsedSearch < searchTimeout)
                {
                    generator = FindAnyObjectByType<ProceduralMountainGenerator>();
                    if (generator != null) break;
                    elapsedSearch += Time.unscaledDeltaTime;
                    yield return null;
                }

                if (generator != null)
                {
                    // If the generator hasn't generated yet and isn't generating, run it with a random seed
                    if (!generator.IsGenerating && !generator.IsGenerationCompleted)
                    {
                        generator.GenerateWithRandomSeed();
                    }

                    // Wait until generation is completely finished
                    while (generator.IsGenerating || !generator.IsGenerationCompleted)
                    {
                        yield return null;
                    }
                }
            }
            else
            {
                // Unhide and unlock cursor for menu scene
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            // 5. Allow newly generated scene objects, terrain data, physics, and camera to settle
            yield return null;
            yield return new WaitForEndOfFrame();

            // 6. Fade out the black loading screen smoothly
            InternalHide(defaultFadeDuration);
            activeLoadCoroutine = null;
        }
    }
}
