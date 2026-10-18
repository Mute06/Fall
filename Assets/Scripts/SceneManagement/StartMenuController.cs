using UnityEngine;
using UnityEngine.UI;

namespace FirstPersonSystem.SceneManagement
{
    /// <summary>
    /// Attached to the Start Button (or Canvas) in the Start scene.
    /// When clicked, triggers loading the next scene in the build (TerrainTest)
    /// accompanied by the black loading screen.
    /// </summary>
    public class StartMenuController : MonoBehaviour
    {
        [Tooltip("Reference to the Start Button. If unassigned, will be auto-located on this GameObject or its children.")]
        [SerializeField] private Button startButton;

        private void Awake()
        {
            if (startButton == null)
            {
                startButton = GetComponent<Button>();
            }
            if (startButton == null)
            {
                startButton = GetComponentInChildren<Button>();
            }
        }

        private void OnEnable()
        {
            if (startButton != null)
            {
                startButton.onClick.AddListener(OnStartButtonClicked);
            }
        }

        private void OnDisable()
        {
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(OnStartButtonClicked);
            }
        }

        /// <summary>
        /// Invoked when the Start Button is clicked.
        /// </summary>
        public void OnStartButtonClicked()
        {
            if (startButton == null)
            {
                startButton = GetComponent<Button>();
                if (startButton == null)
                {
                    startButton = GetComponentInChildren<Button>();
                }
            }

            if (startButton != null)
            {
                startButton.interactable = false;
            }

            LoadingScreenManager.LoadNextScene();
        }
    }
}
