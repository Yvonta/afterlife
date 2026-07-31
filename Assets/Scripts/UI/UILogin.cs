using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;

namespace Yvonta.UI
{
    public class UILogin : MonoBehaviour
    {
        private TMP_InputField emailInputField;
        private TMP_InputField passwordInputField;
        private Button loginButton;
        private Button forgotPasswordButton;
        private Button registerButton;
        private TextMeshProUGUI statusText;
        private GameObject loginPanelObj;

        public UnityEvent<string, string> OnLoginSubmitted = new UnityEvent<string, string>();
        public UnityEvent OnForgotPasswordClicked = new UnityEvent();
        public UnityEvent OnRegisterClicked = new UnityEvent();

        public void BuildUI(Transform parentCanvasTransform)
        {
            loginPanelObj = new GameObject("LoginPanel");
            loginPanelObj.transform.SetParent(parentCanvasTransform, false);
            RectTransform panelRect = loginPanelObj.AddComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(400, 500);
            Image panelImage = loginPanelObj.AddComponent<Image>();
            panelImage.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

            VerticalLayoutGroup layout = loginPanelObj.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(30, 30, 30, 30);
            layout.spacing = 15;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            TMP_InputField CreateInputField(string placeholderText, TMP_InputField.ContentType contentType)
            {
                GameObject inputObj = new GameObject("InputField_" + contentType);
                inputObj.transform.SetParent(loginPanelObj.transform, false);
                inputObj.AddComponent<RectTransform>().sizeDelta = new Vector2(0, 50);
                
                Image bg = inputObj.AddComponent<Image>();
                bg.color = new Color(0.9f, 0.9f, 0.9f);

                TMP_InputField inputField = inputObj.AddComponent<TMP_InputField>();
                inputField.contentType = contentType;

                GameObject textArea = new GameObject("TextArea");
                textArea.transform.SetParent(inputObj.transform, false);
                RectTransform taRect = textArea.AddComponent<RectTransform>();
                taRect.anchorMin = Vector2.zero;
                taRect.anchorMax = Vector2.one;
                taRect.offsetMin = new Vector2(10, 5);
                taRect.offsetMax = new Vector2(-10, -5);

                GameObject textObj = new GameObject("Text");
                textObj.transform.SetParent(textArea.transform, false);
                TextMeshProUGUI textComp = textObj.AddComponent<TextMeshProUGUI>();
                textComp.fontSize = 18;
                textComp.color = Color.black;

                GameObject holderObj = new GameObject("Placeholder");
                holderObj.transform.SetParent(textArea.transform, false);
                TextMeshProUGUI holderComp = holderObj.AddComponent<TextMeshProUGUI>();
                holderComp.text = placeholderText;
                holderComp.fontSize = 18;
                holderComp.color = new Color(0.5f, 0.5f, 0.5f, 0.7f);

                inputField.textComponent = textComp;
                inputField.placeholder = holderComp;
                return inputField;
            }

            emailInputField = CreateInputField("Enter email...", TMP_InputField.ContentType.EmailAddress);
            passwordInputField = CreateInputField("Enter password...", TMP_InputField.ContentType.Password);

            Button CreateButton(string labelText, ColorBlock colors)
            {
                GameObject btnObj = new GameObject("Button_" + labelText);
                btnObj.transform.SetParent(loginPanelObj.transform, false);
                btnObj.AddComponent<RectTransform>().sizeDelta = new Vector2(0, 45);

                Image btnImg = btnObj.AddComponent<Image>();
                Button btn = btnObj.AddComponent<Button>();
                btn.colors = colors;

                GameObject textObj = new GameObject("Text");
                textObj.transform.SetParent(btnObj.transform, false);
                RectTransform tRect = textObj.AddComponent<RectTransform>();
                tRect.anchorMin = Vector2.zero;
                tRect.anchorMax = Vector2.one;

                TextMeshProUGUI btnText = textObj.AddComponent<TextMeshProUGUI>();
                btnText.text = labelText;
                btnText.fontSize = 18;
                btnText.alignment = TextAlignmentOptions.Center;
                btnText.color = Color.white;

                return btn;
            }

            ColorBlock defaultColors = ColorBlock.defaultColorBlock;
            defaultColors.normalColor = new Color(0.2f, 0.6f, 0.9f);
            defaultColors.highlightedColor = new Color(0.3f, 0.7f, 1f);

            loginButton = CreateButton("Login", defaultColors);
            forgotPasswordButton = CreateButton("Forgot Password?", defaultColors);
            registerButton = CreateButton("Register", defaultColors);

            GameObject statusObj = new GameObject("StatusText");
            statusObj.transform.SetParent(loginPanelObj.transform, false);
            statusObj.AddComponent<RectTransform>().sizeDelta = new Vector2(0, 40);
            statusText = statusObj.AddComponent<TextMeshProUGUI>();
            statusText.fontSize = 14;
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.color = Color.yellow;

            if (loginButton != null) loginButton.onClick.AddListener(HandleLoginClicked);
            if (forgotPasswordButton != null) forgotPasswordButton.onClick.AddListener(HandleForgotPasswordClicked);
            if (registerButton != null) registerButton.onClick.AddListener(HandleRegisterClicked);

            if (emailInputField != null)
            {
                emailInputField.Select();
                emailInputField.ActivateInputField();
            }

            SetVisible(true);
        }

        public void SetVisible(bool isVisible)
        {
            if (loginPanelObj != null)
            {
                loginPanelObj.SetActive(isVisible);
            }
        }

        private void HandleLoginClicked()
        {
            string email = emailInputField != null ? emailInputField.text.Trim() : string.Empty;
            string password = passwordInputField != null ? passwordInputField.text : string.Empty;

            if (ValidateInputs(email, password))
            {
                SetStatusMessage(string.Empty);
                OnLoginSubmitted?.Invoke(email, password);
            }
        }

        private void HandleForgotPasswordClicked()
        {
            OnForgotPasswordClicked?.Invoke();
        }

        private void HandleRegisterClicked()
        {
            OnRegisterClicked?.Invoke();
        }

        private bool ValidateInputs(string email, string password)
        {
            if (string.IsNullOrEmpty(email))
            {
                SetStatusMessage("Please enter your email address.");
                return false;
            }

            if (!email.Contains("@") || !email.Contains("."))
            {
                SetStatusMessage("Please enter a valid email address.");
                return false;
            }

            if (string.IsNullOrEmpty(password))
            {
                SetStatusMessage("Please enter your password.");
                return false;
            }

            return true;
        }

        public void SetStatusMessage(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }

        public void SetInteractable(bool state)
        {
            if (emailInputField != null) emailInputField.interactable = state;
            if (passwordInputField != null) passwordInputField.interactable = state;
            if (loginButton != null) loginButton.interactable = state;
            if (forgotPasswordButton != null) forgotPasswordButton.interactable = state;
            if (registerButton != null) registerButton.interactable = state;
        }
    }
}